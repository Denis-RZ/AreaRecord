using Microsoft.Extensions.Configuration;

namespace MyWebApp.Services;

public class ThemeService
{
    private readonly IConfiguration _config;

    public ThemeService(IConfiguration config)
    {
        _config = config;
    }

    public string ThemeName => _config["Theme:Name"] ?? "dark";

    public string GetCssPath()
    {
        return $"~/css/theme-{ThemeName}.css";
    }
}
