namespace LibraryManagementSystem.Services;

// Strongly-typed settings for the Group Project modules. Each class is bound
// to a section of appsettings.json in Program.cs, so a branch can change
// behaviour (its branch code, reminder timing, API keys...) without code changes.

/// <summary>"BranchSettings" section - which branch this installation runs at.</summary>
public class BranchSettings
{
    /// <summary>
    /// The branch this installation belongs to (e.g. DAR, CAS, PAL). Every
    /// location uses the same code and the same instructions - only this
    /// value changes (in appsettings.json, an environment variable, or the
    /// location's Git branch).
    /// </summary>
    public string DefaultBranchCode { get; set; } = "DAR";

    /// <summary>IANA time zone used to decide whether a branch is open right now.</summary>
    public string TimeZoneId { get; set; } = "Australia/Darwin";
}

/// <summary>"NotificationSettings" section.</summary>
public class NotificationSettings
{
    /// <summary>Send a "due soon" reminder when a loan is due within this many days.</summary>
    public int DueSoonDays { get; set; } = 2;

    /// <summary>How often the background reminder check runs.</summary>
    public int CheckIntervalMinutes { get; set; } = 60;

    /// <summary>Turn the automatic background check on/off (staff can still run it manually).</summary>
    public bool EnableBackgroundChecks { get; set; } = true;
}

/// <summary>"ReservationSettings" section.</summary>
public class ReservationSettings
{
    /// <summary>How long an item stays on the hold shelf before the hold expires.</summary>
    public int HoldPickupDays { get; set; } = 3;

    /// <summary>Stops one patron reserving half the catalogue.</summary>
    public int MaxActiveHoldsPerBorrower { get; set; } = 5;
}

/// <summary>"KioskSettings" section.</summary>
public class KioskSettings
{
    /// <summary>Kiosk session ends after this many seconds without a tap (shared public device).</summary>
    public int IdleTimeoutSeconds { get; set; } = 120;

    public int MaxPinAttempts { get; set; } = 5;

    public int PinLockoutMinutes { get; set; } = 15;
}

/// <summary>"ApiSettings" section - keys for the public read-only REST API.</summary>
public class ApiSettings
{
    public List<ApiClientKey> Clients { get; set; } = new();

    /// <summary>Requests allowed per client per minute (rate limiting).</summary>
    public int PermitsPerMinute { get; set; } = 60;
}

public class ApiClientKey
{
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
}
