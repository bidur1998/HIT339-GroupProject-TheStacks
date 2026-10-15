using System.Security.Cryptography;
using System.Text;
using LibraryManagementSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace LibraryManagementSystem.Api;

/// <summary>
/// Checks an API key against the keys configured in ApiSettings:Clients.
/// Uses a constant-time comparison so response timing can't be used to
/// guess a key one character at a time.
/// </summary>
public class ApiKeyValidator
{
    public const string HeaderName = "X-Api-Key";

    private readonly ApiSettings _settings;

    public ApiKeyValidator(IOptions<ApiSettings> settings)
    {
        _settings = settings.Value;
    }

    /// <summary>Returns the client name for a valid key, or null.</summary>
    public string? Validate(string? presentedKey)
    {
        if (string.IsNullOrWhiteSpace(presentedKey))
        {
            return null;
        }

        var presented = Encoding.UTF8.GetBytes(presentedKey);
        foreach (var client in _settings.Clients.Where(c => !string.IsNullOrWhiteSpace(c.Key)))
        {
            var expected = Encoding.UTF8.GetBytes(client.Key);
            if (CryptographicOperations.FixedTimeEquals(presented, expected))
            {
                return client.Name;
            }
        }

        return null;
    }
}

/// <summary>
/// Put [RequireApiKey] on an API controller to make every action need a
/// valid X-Api-Key header. Missing/invalid keys get 401 with a JSON
/// problem description (never the HTML error page).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireApiKeyAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var validator = context.HttpContext.RequestServices.GetRequiredService<ApiKeyValidator>();
        string? key = context.HttpContext.Request.Headers[ApiKeyValidator.HeaderName].FirstOrDefault();
        string? clientName = validator.Validate(key);

        if (clientName is null)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "A valid API key is required.",
                Detail = $"Send your key in the '{ApiKeyValidator.HeaderName}' request header."
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        // Available to the controller/logging if needed.
        context.HttpContext.Items["ApiClient"] = clientName;
    }
}
