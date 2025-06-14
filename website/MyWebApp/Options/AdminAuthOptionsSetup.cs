using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MyWebApp.Data;
using MyWebApp.Models;

namespace MyWebApp.Options
{
    public class AdminAuthOptionsSetup : IConfigureOptions<AdminAuthOptions>, IPostConfigureOptions<AdminAuthOptions>
    {
        private readonly IConfiguration _configuration;
        private readonly IDbContextFactory<ApplicationDbContext> _factory;

        public AdminAuthOptionsSetup(IConfiguration configuration, IDbContextFactory<ApplicationDbContext> factory)
        {
            _configuration = configuration;
            _factory = factory;
        }

        public void Configure(AdminAuthOptions options)
        {
            _configuration.GetSection("AdminAuth").Bind(options);
            try
            {
                using var db = _factory.CreateDbContext();
                db.Database.EnsureCreated();
                var cred = db.AdminCredentials.FirstOrDefault();
                if (cred != null)
                {
                    options.Username = cred.Username;
                    options.Password = cred.Password;
                }
            }
            catch
            {
                // database might not be available yet
            }
        }

        public void PostConfigure(string? name, AdminAuthOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.Username) || string.IsNullOrWhiteSpace(options.Password))
            {
                options.Username = string.IsNullOrWhiteSpace(options.Username) ? "admin" : options.Username;
                options.Password = string.IsNullOrWhiteSpace(options.Password) ? "admin" : options.Password;
                try
                {
                    using var db = _factory.CreateDbContext();
                    db.Database.EnsureCreated();
                    if (!db.AdminCredentials.Any())
                    {
                        db.AdminCredentials.Add(new AdminCredential { Username = options.Username, Password = options.Password });
                        db.SaveChanges();
                    }
                }
                catch
                {
                    // ignore if we cannot write to the database during startup
                }
            }
        }
    }
}
