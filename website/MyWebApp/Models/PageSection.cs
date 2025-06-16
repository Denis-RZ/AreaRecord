using System.ComponentModel.DataAnnotations;

namespace MyWebApp.Models;

public enum PageSectionType
{
    Html,
    Markdown,
    Image,
    Video,
    Code
}

public class PageSection
{
    public int Id { get; set; }

    [Required]
    public int PageId { get; set; }

    [Required]
    [MaxLength(64)]
    public string Area { get; set; } = string.Empty;

    public PageSectionType Type { get; set; } = PageSectionType.Html;

    public string Html { get; set; } = string.Empty;

    public Page? Page { get; set; }
}
