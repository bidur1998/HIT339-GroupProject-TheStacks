using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagementSystem.Controllers;

/// <summary>
/// Custom error handling (per the Errors unit material): friendly pages
/// instead of a raw stack trace once the app is out of Development mode,
/// covering both unhandled exceptions and HTTP status codes like 404.
/// </summary>
[AllowAnonymous]
[Route("Error")]
public class ErrorController : Controller
{
    [Route("Index")]
    public IActionResult Index()
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        ViewBag.OriginalPath = feature?.Path;
        return View();
    }

    // Named HandleStatusCode (not StatusCode) because Controller already
    // has its own built-in method called StatusCode(int) for returning a
    // raw status code result - using the same name here would silently
    // hide that inherited method. The URL path is set by the [Route]
    // attribute below, not by this method's name, so the address
    // Program.cs redirects to ("/Error/StatusCode/{0}") is unaffected.
    [Route("StatusCode/{code:int}")]
    public IActionResult HandleStatusCode(int code)
    {
        ViewBag.StatusCodeValue = code;
        return View("StatusCode");
    }
}
