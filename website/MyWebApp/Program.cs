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
builder.Services.AddSession();
builder.Services.AddSingleton<MyWebApp.Services.CacheService>();
builder.Services.AddSingleton<MyWebApp.Services.LayoutService>();
builder.Services.AddScoped<MyWebApp.Services.SchemaValidator>();
builder.Services.AddOptions<MyWebApp.Options.AdminAuthOptions>()
    .Bind(builder.Configuration.GetSection("AdminAuth"))
    .Validate(o =>
        !string.IsNullOrWhiteSpace(o.Username) &&
        !string.IsNullOrWhiteSpace(o.Password),
        "Admin credentials required");
builder.Services.AddSingleton<IConfigureOptions<AdminAuthOptions>, AdminAuthOptionsSetup>();
builder.Services.AddSingleton<IPostConfigureOptions<AdminAuthOptions>, AdminAuthOptionsSetup>();
builder.Services.AddOptions<MyWebApp.Options.CaptchaOptions>()
    .Bind(builder.Configuration.GetSection("Captcha"));

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
                Area TEXT NOT NULL,
                Html TEXT,
                FOREIGN KEY(PageId) REFERENCES Pages(Id) ON DELETE CASCADE
            )");
            db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IX_PageSections_PageId_Area ON PageSections(PageId, Area)");
            db.Database.ExecuteSqlRaw(@"INSERT INTO PageSections (Id, PageId, Area, Html) VALUES
                (1, 1, 'header', '<div class ""container-fluid nav-container""><a class=""logo"" href=""/"">Screen Area Recorder Pro</a><nav class=""site-nav""><a href=""/"">Home</a> <a href=""/Download"">Download</a> <a href=""/Home/Faq"">FAQ</a> <a href=""/Home/Privacy"">Privacy</a> <a href=""/Setup"">Setup</a> <a href=""/Account/Login"">Login</a></nav></div>'),
                (2, 1, 'footer', '<div class ""container"">&copy; 2025 - Screen Area Recorder Pro</div>')");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Schema upgrade failed: {ex.Message}");
    }
}
