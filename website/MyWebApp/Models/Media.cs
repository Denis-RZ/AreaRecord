using System;
using System.ComponentModel.DataAnnotations;

namespace MyWebApp.Models;

public class Media
{
    public int Id { get; set; }

    [Required]
    [MaxLength(256)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(512)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? ContentType { get; set; }

    public long Size { get; set; }

    [MaxLength(256)]
    public string? AltText { get; set; }

    public DateTime Uploaded { get; set; } = DateTime.UtcNow;
}
