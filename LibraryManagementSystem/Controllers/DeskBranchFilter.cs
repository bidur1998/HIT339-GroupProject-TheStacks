using LibraryManagementSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LibraryManagementSystem.Controllers;

/// <summary>
/// Global filter: for signed-in staff, puts the current desk branch into
/// ViewData so the layout can show "Desk: Darwin City". The layout never
/// reads the session itself (unit material rule) - it only reads ViewData.
/// </summary>
public class DeskBranchFilter : IAsyncActionFilter
{
    private readonly IBranchContext _branchContext;

    public DeskBranchFilter(IBranchContext branchContext)
    {
        _branchContext = branchContext;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.Controller is Controller controller && context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            var branch = await _branchContext.GetCurrentBranchAsync();
            controller.ViewData["DeskBranchName"] = branch?.Name ?? "No branch";
            controller.ViewData["DeskBranchCode"] = branch?.Code;
        }

        await next();
    }
}
