using System.Collections.Generic;

namespace MyWebApp.Models;

public class PreviewRequest
{
    public string Layout { get; set; } = "single-column";
    public string Title { get; set; } = string.Empty;
    public Dictionary<string, string> Zones { get; set; } = new();
}

