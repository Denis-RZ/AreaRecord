using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using Markdig;
using MyWebApp.Models;

namespace MyWebApp.Services;

public class ContentProcessingService
{
    private readonly HtmlSanitizerService _sanitizer;

    public ContentProcessingService(HtmlSanitizerService sanitizer)
    {
        _sanitizer = sanitizer;
    }

    public async Task PrepareHtmlAsync(PageSection model, IFormFile? file)
    {
        switch (model.Type)
        {
            case PageSectionType.Html:
                model.Html = _sanitizer.Sanitize(model.Html);
                break;
            case PageSectionType.Markdown:
                var html = Markdown.ToHtml(model.Html ?? string.Empty);
                model.Html = _sanitizer.Sanitize(html);
                break;
            case PageSectionType.Code:
                model.Html = $"<pre><code>{System.Net.WebUtility.HtmlEncode(model.Html)}</code></pre>";
                break;
            case PageSectionType.Image:
            case PageSectionType.Video:
                if (file != null && file.Length > 0)
                {
                    var uploads = Path.Combine("wwwroot", "uploads");
                    Directory.CreateDirectory(uploads);
                    var name = Path.GetFileName(file.FileName);
                    var path = Path.Combine(uploads, name);
                    using var stream = new FileStream(path, FileMode.Create);
                    await file.CopyToAsync(stream);
                    if (model.Type == PageSectionType.Image)
                        model.Html = $"<img src='/uploads/{name}' alt='' />";
                    else
                        model.Html = $"<video controls src='/uploads/{name}'></video>";
                }
                break;
        }
    }
}
