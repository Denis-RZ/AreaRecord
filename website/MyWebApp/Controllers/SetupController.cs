using Microsoft.AspNetCore.Mvc;
using MyWebApp.Data;

namespace MyWebApp.Controllers;

public class SetupController : Controller
{
    private readonly ApplicationDbContext _context;

    public SetupController(ApplicationDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        bool canConnect;
        try
        {
            canConnect = _context.Database.CanConnect();
        }
        catch
        {
            canConnect = false;
        }
        return View(canConnect);
    }
}
