using Microsoft.AspNetCore.Mvc;
using MyWebApp.Services;

namespace MyWebApp.Controllers;

public class CaptchaController : Controller
{
    private readonly CaptchaService _captchaService;

    public CaptchaController(CaptchaService captchaService)
    {
        _captchaService = captchaService;
    }

    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Image()
    {
        var code = _captchaService.GetCurrentCode() ?? _captchaService.CreateChallenge();
        var bytes = _captchaService.RenderImage(code);
        return File(bytes, "image/png");
    }
}
