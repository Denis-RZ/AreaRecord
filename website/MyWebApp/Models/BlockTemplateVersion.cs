using System;
using System.ComponentModel.DataAnnotations;

namespace MyWebApp.Models;

public class BlockTemplateVersion
{
    public int Id { get; set; }

    [Required]
    public int BlockTemplateId { get; set; }

    public string Html { get; set; } = string.Empty;

    public DateTime Created { get; set; } = DateTime.UtcNow;

    public BlockTemplate? Template { get; set; }
}
