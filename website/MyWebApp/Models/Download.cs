using System;

namespace MyWebApp.Models
{
    public class Download
    {
        public int Id { get; set; }
        public string UserIP { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public DateTime DownloadTime { get; set; }
        public bool IsSuccessful { get; set; }
        public string? SessionId { get; set; }
        public string? Country { get; set; }
        public int? DownloadFileId { get; set; }
        public DownloadFile? DownloadFile { get; set; }
    }
}
