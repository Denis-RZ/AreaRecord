using System.ComponentModel.DataAnnotations;

namespace MyWebApp.Models;

public class Page
{
    public int Id { get; set; }

    [Required]
    [MaxLength(128)]
    public string Slug { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string Title { get; set; } = string.Empty;

    public string? HeaderHtml { get; set; }

    public string BodyHtml { get; set; } = string.Empty;

    public string? FooterHtml { get; set; }
}
