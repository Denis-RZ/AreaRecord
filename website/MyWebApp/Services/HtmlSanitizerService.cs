using Ganss.Xss;

namespace MyWebApp.Services;

public class HtmlSanitizerService
{
    private readonly HtmlSanitizer _sanitizer = new();

    public string Sanitize(string? html)
    {
        return html == null ? string.Empty : _sanitizer.Sanitize(html);
    }
}
