using System;

namespace MyWebApp.Models;

public static class ConnectionHelper
{
    public static string BuildConnectionString(string provider, string server, string database, string username, string password)
    {
        switch (provider.ToLowerInvariant())
        {
            case "postgresql":
            case "npgsql":
                return $"Host={server};Database={database};Username={username};Password={password}";
            case "sqlite":
                return $"Data Source={database}";
            default:
                if (string.IsNullOrEmpty(username))
                {
                    return $"Server={server};Database={database};Trusted_Connection=True;TrustServerCertificate=true";
                }
                return $"Server={server};Database={database};User Id={username};Password={password};TrustServerCertificate=true";
        }
    }

    public static void ParseConnectionString(string provider, string connectionString, out string server, out string database, out string username, out string password)
    {
        server = database = username = password = string.Empty;
        if (string.IsNullOrEmpty(connectionString))
            return;

        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            var key = kv[0].Trim().ToLowerInvariant();
            var value = kv[1].Trim();
            switch (key)
            {
                case "server":
                case "data source":
                case "host":
                    server = value;
                    break;
                case "database":
                case "initial catalog":
                    database = value;
                    break;
                case "user id":
                case "username":
                case "uid":
                    username = value;
                    break;
                case "password":
                case "pwd":
                    password = value;
                    break;
            }
        }
    }
}
