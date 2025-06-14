using System;
using System.Collections.Generic;

namespace MyWebApp.Models
{
    public class DownloadFile
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty; // original name or relative path
        public string Description { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public byte[]? Data { get; set; }
        public DateTime Created { get; set; }
        public ICollection<Download>? Downloads { get; set; }
    }
}
