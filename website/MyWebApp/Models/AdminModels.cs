using System;
using System.Collections.Generic;

namespace MyWebApp.Models
{
    public class AdminDashboardViewModel
    {
        public int TotalDownloads { get; set; }
        public int FailedDownloads { get; set; }
        public int DownloadsLast24h { get; set; }
        public double AverageQueryTimeMs { get; set; }
        public IList<CountryCount> TopCountries { get; set; } = new List<CountryCount>();
        public SystemInfoViewModel SystemInfo { get; set; } = new SystemInfoViewModel();
    }

    public class DownloadStatsViewModel
    {
        public IList<Download> Downloads { get; set; } = new List<Download>();
        public int Page { get; set; }
        public int TotalPages { get; set; }
        public string? Search { get; set; }
        public string? Status { get; set; }
    }

    public class SystemInfoViewModel
    {
        public TimeSpan Uptime { get; set; }
        public string DotNetVersion { get; set; } = string.Empty;
        public DateTime Started { get; set; }
    }

    public class CountryCount
    {
        public string Country { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class FileStatsViewModel
    {
        public DownloadFile File { get; set; } = new DownloadFile();
        public int DownloadCount { get; set; }
    }

    public class RoleEditViewModel
    {
        public Role Role { get; set; } = new Role();
        public IList<int> SelectedPermissions { get; set; } = new List<int>();
    }
}
