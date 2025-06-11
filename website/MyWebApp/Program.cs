using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MyWebApp.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddDbContext<MyWebApp.Data.ApplicationDbContext>(options =>
{
    switch (provider.ToLowerInvariant())
    {
        case "postgresql":
        case "npgsql":
            options.UseNpgsql(connectionString);
            break;
        case "sqlite":
            options.UseSqlite(connectionString);
            break;
        default:
            options.UseSqlServer(connectionString);
            break;
    }
});
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddSession();

var app = builder.Build();

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
