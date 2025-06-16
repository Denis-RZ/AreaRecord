using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyWebApp.Models;

public class BlockTemplate
{
    public int Id { get; set; }

    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    public string Html { get; set; } = string.Empty;

    public ICollection<BlockTemplateVersion> Versions { get; set; } = new List<BlockTemplateVersion>();
}
