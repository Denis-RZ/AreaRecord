using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyWebApp.Data;
using MyWebApp.Services;
using MyWebApp.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using System;

var builder = WebApplication.CreateBuilder(args);
var startupLogger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Startup");

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
            "Cache=Shared;Journal Mode=WAL;Synchronous=Normal";
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
        "Admin credentials required")
    .ValidateOnStart();
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
