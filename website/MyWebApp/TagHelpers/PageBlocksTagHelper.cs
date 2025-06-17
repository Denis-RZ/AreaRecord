using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Data;

namespace MyWebApp.TagHelpers;

[HtmlTargetElement("page-blocks")]
public class PageBlocksTagHelper : TagHelper
{
    private readonly ApplicationDbContext _db;
    public PageBlocksTagHelper(ApplicationDbContext db)
    {
        _db = db;
    }

    public int PageId { get; set; }
    public string Area { get; set; } = string.Empty;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        var htmlParts = await _db.PageSections.AsNoTracking()
            .Where(s => s.PageId == PageId && s.Area == Area)
            .OrderBy(s => s.SortOrder)
            .Select(s => s.Html)
            .ToListAsync();
        output.Content.SetHtmlContent(string.Join(System.Environment.NewLine, htmlParts));
    }
}
