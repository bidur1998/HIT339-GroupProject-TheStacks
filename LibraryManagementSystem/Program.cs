using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using LibraryManagementSystem.Api;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.Services;
using LibraryManagementSystem.Services.Reports;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration ---
builder.Services.Configure<LibrarySettings>(builder.Configuration.GetSection("LibrarySettings"));
// Group Project settings sections (see Services/Settings.cs).
builder.Services.Configure<BranchSettings>(builder.Configuration.GetSection("BranchSettings"));
builder.Services.Configure<NotificationSettings>(builder.Configuration.GetSection("NotificationSettings"));
builder.Services.Configure<ReservationSettings>(builder.Configuration.GetSection("ReservationSettings"));
builder.Services.Configure<KioskSettings>(builder.Configuration.GetSection("KioskSettings"));
builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));

// --- Database (EF Core, SQLite) ---
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- ASP.NET Core Identity (staff logins: Admin / Reception / Manager) ---
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Kept deliberately simple for a teaching/demo system - see the
        // technical documentation for what a production deployment should
        // tighten further.
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(4);
});

// --- Dependency injection: high-level modules (controllers, services)
// depend on these abstractions; the concrete, EF Core-backed classes are
// registered here and only here (Dependency Inversion Principle). ---
builder.Services.AddScoped<IItemRepository, ItemRepository>();
builder.Services.AddScoped<IBorrowerRepository, BorrowerRepository>();
builder.Services.AddScoped<ILoanRepository, LoanRepository>();
builder.Services.AddScoped<IFineCalculator, FineCalculator>();
builder.Services.AddScoped<ILoanService, LoanService>();
builder.Services.AddScoped<IStatisticsService, StatisticsService>();

// Group Project: new repositories.
builder.Services.AddScoped<IBranchRepository, BranchRepository>();
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<ITransferRepository, TransferRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();

// Group Project: new services.
// TimeProvider.System is .NET's built-in clock; tests swap in a fake one.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IBranchContext, BranchContext>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<ITransferService, TransferService>();
builder.Services.AddScoped<IPatronAuthService, PatronAuthService>();
builder.Services.AddScoped<IPatronAccountService, PatronAccountService>();
builder.Services.AddScoped<IPasswordHasher<Borrower>, PasswordHasher<Borrower>>();

// Notifications: SIMULATED senders. Swap these two lines for real email/SMS
// providers and nothing else in the app needs to change.
builder.Services.AddScoped<INotificationChannelSender, SimulatedEmailSender>();
builder.Services.AddScoped<INotificationChannelSender, SimulatedSmsSender>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddHostedService<ReminderBackgroundService>();

// Importer: the external metadata provider is a MOCK (no network calls).
builder.Services.AddSingleton<IMediaMetadataProvider, MockMediaMetadataProvider>();
builder.Services.AddScoped<ICatalogueImportService, CatalogueImportService>();

// Reports and exporters.
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddSingleton<IReportExporter, CsvReportExporter>();
builder.Services.AddSingleton<IReportExporter, PdfReportExporter>();

// Public API: key checking, rate limiting and CORS.
builder.Services.AddSingleton<ApiKeyValidator>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("public-api", httpContext =>
    {
        int permits = builder.Configuration.GetValue("ApiSettings:PermitsPerMinute", 60);
        // Limit per API key (or per IP address if no key was sent).
        string partition = httpContext.Request.Headers[ApiKeyValidator.HeaderName].FirstOrDefault()
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
    // Slows down PIN guessing at the kiosk and on the online hold form
    // (on top of the per-card lockout in PatronAuthService).
    options.AddPolicy("patron-pin", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddCors(options =>
{
    // Read-only API: any website may call it, but only with GET.
    options.AddPolicy("PublicApi", policy => policy
        .AllowAnyOrigin()
        .WithMethods("GET")
        .WithHeaders(ApiKeyValidator.HeaderName));
});

// --- Sessions (used to carry short-lived staff workflow state, e.g. a
// "last scanned code" convenience on the reception screen; per the unit
// material, session data is read only from controllers, never from views
// - ViewBag/ViewData/TempData carry it the rest of the way). Group Project:
// also holds the kiosk patron session, desk branch and CSV import preview. ---
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddControllersWithViews(options =>
    {
        // Shows the staff member's desk branch in the layout (Group Project).
        options.Filters.Add<LibraryManagementSystem.Controllers.DeskBranchFilter>();
    })
    // API returns enums as text ("Available") instead of numbers.
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// --- Error handling (per the Errors unit material) ---
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error/Index");
    // Friendly HTML status pages for the website, but not for /api - API
    // clients should get JSON problem responses, not an HTML page.
    app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api"),
        branch => branch.UseStatusCodePagesWithReExecute("/Error/StatusCode/{0}"));
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseCors();
app.UseRateLimiter();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    await DbInitializer.SeedAsync(scope.ServiceProvider);
}

app.Run();

/// <summary>Lets integration tests reference the app's entry point if needed.</summary>
public partial class Program { }
