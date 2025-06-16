using System;
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

    public int SortOrder { get; set; }

    public string Html { get; set; } = string.Empty;

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public int? PermissionId { get; set; }

    public int ViewCount { get; set; }

    public Page? Page { get; set; }

    public Permission? Permission { get; set; }
}
