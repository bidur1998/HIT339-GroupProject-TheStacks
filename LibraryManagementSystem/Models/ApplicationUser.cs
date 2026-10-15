using Microsoft.AspNetCore.Identity;

namespace LibraryManagementSystem.Models;

/// <summary>
/// The three staff account types (Admin, Reception, Manager) all use this
/// same Identity user class; what they can do is controlled entirely by
/// which Role they belong to (see DbInitializer and the [Authorize(Roles=..)]
/// attributes on the controllers), not by separate classes. This keeps
/// authentication generic and authorisation declarative, as covered in the
/// Security and Authorisation unit material.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>The three role names used throughout the system.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Reception = "Reception";
    public const string Manager = "Manager";
}
