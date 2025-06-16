using Microsoft.AspNetCore.Http;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace MyWebApp.Services;

public class CaptchaService
{
    private readonly IHttpContextAccessor _accessor;
    private static readonly char[] _chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();
    private static readonly Random _rng = new();
    private const string SessionKey = "captcha_code";

    public CaptchaService(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public string CreateChallenge()
    {
        var code = new string(Enumerable.Range(0, 5).Select(_ => _chars[_rng.Next(_chars.Length)]).ToArray());
        _accessor.HttpContext?.Session.SetString(SessionKey, code);
        return code;
    }

    public string? GetCurrentCode() => _accessor.HttpContext?.Session.GetString(SessionKey);

    public byte[] RenderImage(string code)
    {
        using var bmp = new Bitmap(120, 40);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        using var font = new Font(FontFamily.GenericSansSerif, 20, FontStyle.Bold);
        for (int i = 0; i < code.Length; i++)
        {
            var brush = new SolidBrush(Color.FromArgb(_rng.Next(0, 150), _rng.Next(0, 150), _rng.Next(0, 150)));
            g.DrawString(code[i].ToString(), font, brush, 10 + i * 20, _rng.Next(0, 10));
        }
        for (int i = 0; i < 3; i++)
        {
            g.DrawLine(Pens.Silver, _rng.Next(0, 120), _rng.Next(0, 40), _rng.Next(0, 120), _rng.Next(0, 40));
        }
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    public bool Validate(string input)
    {
        var expected = GetCurrentCode();
        return !string.IsNullOrEmpty(expected) &&
               !string.IsNullOrEmpty(input) &&
               string.Equals(expected, input, StringComparison.OrdinalIgnoreCase);
    }
}
