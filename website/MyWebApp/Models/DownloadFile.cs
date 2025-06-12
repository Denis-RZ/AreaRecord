using System;
using System.Collections.Generic;

namespace MyWebApp.Models
{
    public class DownloadFile
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty; // relative path or url
        public string Description { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public ICollection<Download>? Downloads { get; set; }
    }
}
