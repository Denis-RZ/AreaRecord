using System.ComponentModel.DataAnnotations;

namespace MyWebApp.Models;

public class PageSection
{
    public int Id { get; set; }

    [Required]
    public int PageId { get; set; }

    [Required]
    [MaxLength(64)]
    public string Area { get; set; } = string.Empty;

    public string Html { get; set; } = string.Empty;

    public Page? Page { get; set; }
}
