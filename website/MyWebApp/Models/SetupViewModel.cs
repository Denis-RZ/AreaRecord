namespace MyWebApp.Models;

public class SetupViewModel
{
    public bool CanConnect { get; set; }
    public string ConnectionString { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? ResultMessage { get; set; }
}
