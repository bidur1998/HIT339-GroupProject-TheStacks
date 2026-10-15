using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagementSystem.Controllers;

[AllowAnonymous]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    /// <summary>Group Project: public documentation page for the read-only REST API.</summary>
    public IActionResult Developers()
    {
        return View();
    }
}
