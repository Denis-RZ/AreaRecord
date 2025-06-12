using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyWebApp.Data;
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
catch
{
    needFallback = true;
}

if (needFallback && provider.ToLowerInvariant() != "sqlite")
{
    startupLogger.LogWarning("Falling back to SQLite due to database connection failure.");
    provider = "Sqlite";
    connectionString = "Data Source=mywebapp.db";
}
builder.Services.AddDbContext<MyWebApp.Data.ApplicationDbContext>(options =>
{
    switch (provider.ToLowerInvariant())
    {
        case "postgresql":
        case "npgsql":
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.EnableRetryOnFailure());
            break;
        case "sqlite":
            options.UseSqlite(connectionString);
            break;
        default:
            options.UseSqlServer(connectionString, sql =>
                sql.EnableRetryOnFailure());
            break;
    }
});
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddSession();

var app = builder.Build();

// Ensure database is up to date
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        if (db.Database.CanConnect())
        {
            db.Database.Migrate();
        }
        else
        {
            app.Logger.LogWarning("Could not connect to the database. Migrations were not applied.");
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Database migration failed during startup.");
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

app.Run();
