using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyWebApp.Data;
using MyWebApp.Services;
using MyWebApp.Options;
using MyWebApp.Models;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Collections.Generic;
using System;
using System.IO;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);
var startupLogger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Startup");

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddProvider(new FileLoggerProvider(Path.Combine(builder.Environment.ContentRootPath, "Logs", "app.log")));

// Allow connection string overrides from environment-specific files,
// environment variables, and command-line arguments
builder.Configuration
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\mssqllocaldb;Database=MyWebAppDb;Trusted_Connection=True;MultipleActiveResultSets=true";
var provider = builder.Configuration["DatabaseProvider"] ?? "SqlServer";
var testOptions = new DbContextOptionsBuilder<ApplicationDbContext>();
switch (provider.ToLowerInvariant())
{
    case "postgresql":
    case "npgsql":
        testOptions.UseNpgsql(connectionString);
        break;
    case "sqlite":
        testOptions.UseSqlite(connectionString);
        break;
    default:
        testOptions.UseSqlServer(connectionString);
        break;
}

var needFallback = false;
try
{
    using var testCtx = new ApplicationDbContext(testOptions.Options);
    needFallback = !testCtx.Database.CanConnect();
}
catch (System.Data.Common.DbException)
{
    needFallback = true;
}
catch (InvalidOperationException)
{
    needFallback = true;
}

if (needFallback && provider.ToLowerInvariant() != "sqlite")
{
    startupLogger.LogWarning("Falling back to SQLite due to database connection failure.");
    provider = "Sqlite";
    connectionString = "Data Source=mywebapp.db";
}

// Append provider specific defaults
if (provider.Equals("postgresql", StringComparison.OrdinalIgnoreCase) || provider.Equals("npgsql", StringComparison.OrdinalIgnoreCase))
{
    if (!connectionString.Contains("Pooling", StringComparison.OrdinalIgnoreCase))
    {
        connectionString += (connectionString.EndsWith(";") ? string.Empty : ";") +
            "Pooling=true;MinPoolSize=1;MaxPoolSize=20;ConnectionIdleLifetime=300;Max Auto Prepare=20;Auto Prepare Min Usages=2";
    }
}
else if (provider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
{
    if (!connectionString.Contains("Cache=", StringComparison.OrdinalIgnoreCase))
    {
        connectionString += (connectionString.EndsWith(";") ? string.Empty : ";") +
            "Cache=Shared";
    }
}
builder.Services.AddSingleton<QueryMetrics>();
builder.Services.AddSingleton<QueryLoggingInterceptor>();

builder.Services.AddDbContext<MyWebApp.Data.ApplicationDbContext>((sp, options) =>
{
    switch (provider.ToLowerInvariant())
    {
        case "postgresql":
        case "npgsql":
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure();
                npgsql.CommandTimeout(60);
            });
            break;
        case "sqlite":
            options.UseSqlite(connectionString);
            break;
        default:
            options.UseSqlServer(connectionString, sql =>
                sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(30), null)
                   .CommandTimeout(60));
            break;
    }
    options.AddInterceptors(sp.GetRequiredService<QueryLoggingInterceptor>());
}, optionsLifetime: ServiceLifetime.Singleton);
builder.Services.AddDbContextFactory<MyWebApp.Data.ApplicationDbContext>((sp, options) =>
{
    switch (provider.ToLowerInvariant())
    {
        case "postgresql":
        case "npgsql":
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure();
                npgsql.CommandTimeout(60);
            });
            break;
        case "sqlite":
            options.UseSqlite(connectionString);
            break;
        default:
            options.UseSqlServer(connectionString, sql =>
                sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(30), null)
                   .CommandTimeout(60));
            break;
    }
    options.AddInterceptors(sp.GetRequiredService<QueryLoggingInterceptor>());
});
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
var sessionTimeout = builder.Configuration.GetValue<int>("Session:TimeoutMinutes", 30);
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(sessionTimeout);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<MyWebApp.Services.CacheService>();
builder.Services.AddSingleton<MyWebApp.Services.LayoutService>();
builder.Services.AddSingleton<MyWebApp.Services.TokenRenderService>();
builder.Services.AddSingleton<MyWebApp.Services.HtmlSanitizerService>();
builder.Services.AddSingleton<MyWebApp.Services.ContentProcessingService>();
builder.Services.AddSingleton<MyWebApp.Services.ThemeService>();
builder.Services.AddSingleton<MyWebApp.Services.CaptchaService>();
var smtpSection = builder.Configuration.GetSection("Smtp");
if (!string.IsNullOrWhiteSpace(smtpSection["Host"]))
{
    builder.Services.Configure<MyWebApp.Options.SmtpOptions>(smtpSection);
    builder.Services.AddSingleton<MyWebApp.Services.IEmailSender, MyWebApp.Services.SmtpEmailSender>();
}
else
{
    builder.Services.AddSingleton<MyWebApp.Services.IEmailSender, MyWebApp.Services.LoggingEmailSender>();
}
builder.Services.AddScoped<MyWebApp.Services.SchemaValidator>();
builder.Services.AddOptions<MyWebApp.Options.AdminAuthOptions>()
    .Bind(builder.Configuration.GetSection("AdminAuth"))
    .Validate(o =>
        !string.IsNullOrWhiteSpace(o.Username) &&
        !string.IsNullOrWhiteSpace(o.Password),
        "Admin credentials required");
builder.Services.AddSingleton<IConfigureOptions<AdminAuthOptions>, AdminAuthOptionsSetup>();
builder.Services.AddSingleton<IPostConfigureOptions<AdminAuthOptions>, AdminAuthOptionsSetup>();

var app = builder.Build();

// Ensure database is created and optimized
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var cacheService = scope.ServiceProvider.GetRequiredService<CacheService>();
    try
    {
        if (db.Database.EnsureCreated())
        {
            app.Logger.LogInformation("Database schema created.");
        }
        if (provider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
        {
            db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
            db.Database.ExecuteSqlRaw("PRAGMA synchronous=NORMAL;");

            UpgradeDownloadFilesTable(db);
            UpgradePageSectionsTable(db);
            UpgradePagesTable(db);
            UpgradeMediaItemsTable(db);
            UpgradeBlockTemplatesTable(db);
            UpgradePermissionsTable(db);
            UpgradeLayoutHeader(db);
        }
        if (db.Database.CanConnect())
        {
            cacheService.WarmCache(db);
        }
        else
        {
            app.Logger.LogWarning("Could not connect to the database. Schema creation may have failed.");
        }
    }
    catch (System.Data.Common.DbException ex)
    {
        app.Logger.LogError(ex, "Database initialization failed during startup.");
    }
    catch (InvalidOperationException ex)
    {
        app.Logger.LogError(ex, "Database initialization failed during startup.");
    }
}

// Verify Quill client library is present
var quillFiles = new[] { "quill.js", "quill.snow.css" };
foreach (var name in quillFiles)
{
    var path = Path.Combine(app.Environment.WebRootPath ?? "wwwroot",
        "lib", "quill", "dist", name);
    if (File.Exists(path))
        continue;

    app.Logger.LogWarning("Missing Quill asset at {Path}", path);
    try
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using var http = new HttpClient();
        var url = $"https://cdn.jsdelivr.net/npm/quill@2.0.2/dist/{name}";
        var data = http.GetByteArrayAsync(url).GetAwaiter().GetResult();
        File.WriteAllBytes(path, data);
        app.Logger.LogInformation("Downloaded Quill asset {Name} from CDN", name);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Failed to download Quill asset {Name}", name);
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "pages",
    pattern: "{*slug}",
    defaults: new { controller = "Pages", action = "Show" });

app.Run();

static void UpgradeDownloadFilesTable(ApplicationDbContext db)
{
    try
    {
        using var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA table_info('DownloadFiles')";
        using var reader = cmd.ExecuteReader();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }
        if (!columns.Contains("ContentType"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE DownloadFiles ADD COLUMN ContentType TEXT");
        }
        if (!columns.Contains("Data"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE DownloadFiles ADD COLUMN Data BLOB");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Schema upgrade failed: {ex.Message}");
    }
}

static void UpgradePageSectionsTable(ApplicationDbContext db)
{
    try
    {
        using var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='PageSections'";
        var exists = cmd.ExecuteScalar() != null;
        if (!exists)
        {
            db.Database.ExecuteSqlRaw(@"CREATE TABLE PageSections (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PageId INTEGER NOT NULL,
                Zone TEXT NOT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                Type INTEGER NOT NULL DEFAULT 0,
                Html TEXT,
                StartDate TEXT,
                EndDate TEXT,
                PermissionId INTEGER,
                FOREIGN KEY(PageId) REFERENCES Pages(Id) ON DELETE CASCADE
            )");
            db.Database.ExecuteSqlRaw("CREATE INDEX IX_PageSections_PageId_Zone_SortOrder ON PageSections(PageId, Zone, SortOrder)");
            db.Database.ExecuteSqlRaw(@"INSERT INTO PageSections (Id, PageId, Zone, SortOrder, Type, Html) VALUES
                (1, 1, 'header', 0, 0, '<div class ""container-fluid nav-container""><a class=""logo"" href=""/"">Screen Area Recorder Pro</a><nav class=""site-nav""><a href=""/"">Home</a> {{nav}} <a href=""/Download"">Download</a> <a href=""/Home/Faq"">FAQ</a> <a href=""/Home/Privacy"">Privacy</a> <a href=""/Setup"">Setup</a> <a href=""/Account/Login"">Login</a></nav></div>'),
                (2, 1, 'footer', 0, 0, '<div class ""container"">&copy; 2025 - Screen Area Recorder Pro</div>')");
        }
        else
        {
            cmd.CommandText = "PRAGMA table_info('PageSections')";
            using var reader = cmd.ExecuteReader();
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                columns.Add(reader.GetString(1));
            }
            reader.Close();
            if (columns.Contains("Area") && !columns.Contains("Zone"))
                db.Database.ExecuteSqlRaw("ALTER TABLE PageSections RENAME COLUMN Area TO Zone");
            if (!columns.Contains("SortOrder"))
                db.Database.ExecuteSqlRaw("ALTER TABLE PageSections ADD COLUMN SortOrder INTEGER NOT NULL DEFAULT 0");
            if (!columns.Contains("Type"))
                db.Database.ExecuteSqlRaw("ALTER TABLE PageSections ADD COLUMN Type INTEGER NOT NULL DEFAULT 0");
            if (!columns.Contains("StartDate"))
                db.Database.ExecuteSqlRaw("ALTER TABLE PageSections ADD COLUMN StartDate TEXT");
            if (!columns.Contains("EndDate"))
                db.Database.ExecuteSqlRaw("ALTER TABLE PageSections ADD COLUMN EndDate TEXT");
            if (!columns.Contains("PermissionId"))
                db.Database.ExecuteSqlRaw("ALTER TABLE PageSections ADD COLUMN PermissionId INTEGER");

            cmd.CommandText = "PRAGMA index_list('PageSections')";
            using var idx = cmd.ExecuteReader();
            var indexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (idx.Read())
            {
                indexes.Add(idx.GetString(1));
            }
            idx.Close();
            if (indexes.Contains("IX_PageSections_PageId_Area"))
                db.Database.ExecuteSqlRaw("DROP INDEX IX_PageSections_PageId_Area");
            if (!indexes.Contains("IX_PageSections_PageId_Zone_SortOrder"))
                db.Database.ExecuteSqlRaw("CREATE INDEX IX_PageSections_PageId_Zone_SortOrder ON PageSections(PageId, Zone, SortOrder)");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Schema upgrade failed: {ex.Message}");
    }
}

static void UpgradePagesTable(ApplicationDbContext db)
{
    try
    {
        using var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA table_info('Pages')";
        using var reader = cmd.ExecuteReader();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }
        if (columns.Contains("HeaderHtml"))
            db.Database.ExecuteSqlRaw("ALTER TABLE Pages DROP COLUMN HeaderHtml");
        if (columns.Contains("BodyHtml"))
            db.Database.ExecuteSqlRaw("ALTER TABLE Pages DROP COLUMN BodyHtml");
        if (columns.Contains("FooterHtml"))
            db.Database.ExecuteSqlRaw("ALTER TABLE Pages DROP COLUMN FooterHtml");
        if (!columns.Contains("Layout"))
        {
            db.Database.ExecuteSqlRaw(
                "ALTER TABLE Pages ADD COLUMN Layout TEXT NOT NULL DEFAULT 'single-column'");
        }
        if (!columns.Contains("MetaDescription"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Pages ADD COLUMN MetaDescription TEXT");
        }
        if (!columns.Contains("MetaKeywords"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Pages ADD COLUMN MetaKeywords TEXT");
        }
        if (!columns.Contains("OgTitle"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Pages ADD COLUMN OgTitle TEXT");
        }
        if (!columns.Contains("OgDescription"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Pages ADD COLUMN OgDescription TEXT");
        }
        if (!columns.Contains("IsPublished"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Pages ADD COLUMN IsPublished INTEGER NOT NULL DEFAULT 0");
        }
        if (!columns.Contains("PublishDate"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Pages ADD COLUMN PublishDate TEXT");
        }
        if (!columns.Contains("Category"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Pages ADD COLUMN Category TEXT");
        }
        if (!columns.Contains("Tags"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Pages ADD COLUMN Tags TEXT");
        }
        if (!columns.Contains("FeaturedImage"))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Pages ADD COLUMN FeaturedImage TEXT");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Schema upgrade failed: {ex.Message}");
    }
}

static void UpgradeMediaItemsTable(ApplicationDbContext db)
{
    try
    {
        using var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='MediaItems'";
        var exists = cmd.ExecuteScalar() != null;
        if (!exists)
        {
            db.Database.ExecuteSqlRaw(@"CREATE TABLE MediaItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FileName TEXT NOT NULL,
                FilePath TEXT NOT NULL,
                ContentType TEXT,
                Size INTEGER NOT NULL,
                AltText TEXT,
                Uploaded TEXT NOT NULL
            )");
            db.Database.ExecuteSqlRaw("CREATE INDEX IX_MediaItems_FileName ON MediaItems(FileName)");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Schema upgrade failed: {ex.Message}");
    }
}

static void UpgradeBlockTemplatesTable(ApplicationDbContext db)
{
    try
    {
        using var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='BlockTemplates'";
        var exists = cmd.ExecuteScalar() != null;
        if (!exists)
        {
            db.Database.ExecuteSqlRaw(@"CREATE TABLE BlockTemplates (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Html TEXT
            )");
            db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IX_BlockTemplates_Name ON BlockTemplates(Name)");
        }
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='BlockTemplateVersions'";
        exists = cmd.ExecuteScalar() != null;
        if (!exists)
        {
            db.Database.ExecuteSqlRaw(@"CREATE TABLE BlockTemplateVersions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                BlockTemplateId INTEGER NOT NULL,
                Html TEXT,
                Created TEXT NOT NULL,
                FOREIGN KEY(BlockTemplateId) REFERENCES BlockTemplates(Id) ON DELETE CASCADE
            )");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Schema upgrade failed: {ex.Message}");
    }
}

static void UpgradePermissionsTable(ApplicationDbContext db)
{
    try
    {
        using var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Permissions'";
        var exists = cmd.ExecuteScalar() != null;
        if (!exists)
        {
            db.Database.ExecuteSqlRaw(@"CREATE TABLE Permissions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL
            )");
            db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IX_Permissions_Name ON Permissions(Name)");
        }
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='RolePermissions'";
        exists = cmd.ExecuteScalar() != null;
        if (!exists)
        {
            db.Database.ExecuteSqlRaw(@"CREATE TABLE RolePermissions (
                RoleId INTEGER NOT NULL,
                PermissionId INTEGER NOT NULL,
                PRIMARY KEY(RoleId, PermissionId),
                FOREIGN KEY(RoleId) REFERENCES Roles(Id) ON DELETE CASCADE,
                FOREIGN KEY(PermissionId) REFERENCES Permissions(Id) ON DELETE CASCADE
            )");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Schema upgrade failed: {ex.Message}");
    }
}

static void UpgradeLayoutHeader(ApplicationDbContext db)
{
    try
    {
        var layoutId = db.Pages
            .AsNoTracking()
            .Where(p => p.Slug == "layout")
            .Select(p => p.Id)
            .FirstOrDefault();
        if (layoutId == 0)
            return;

        var section = db.PageSections
            .FirstOrDefault(s => s.PageId == layoutId && s.Zone == "header");
        if (section == null)
            return;

        if (section.Html != null &&
            !section.Html.Contains("{{nav}}", StringComparison.OrdinalIgnoreCase))
        {
            if (section.Html.Contains("</nav>", StringComparison.OrdinalIgnoreCase))
            {
                section.Html = section.Html.Replace("</nav>", " {{nav}} </nav>", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                section.Html += " {{nav}}";
            }
            db.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Schema upgrade failed: {ex.Message}");
    }
}
