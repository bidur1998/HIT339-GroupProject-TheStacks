using System.Text.RegularExpressions;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LibraryManagementSystem.Services;

public enum PatronAuthStatus
{
    Success,
    InvalidCredentials,
    NoPinSet,
    LockedOut
}

public record PatronAuthResult(PatronAuthStatus Status, Borrower? Borrower, string Message)
{
    public bool Succeeded => Status == PatronAuthStatus.Success;
}

/// <summary>
/// Identifies a member of the public by library card number + PIN, for the
/// self-service kiosk and the online hold form. Patrons are not Identity
/// users, but their PIN is still hashed with Identity's PasswordHasher and
/// repeated wrong guesses lock the card for a while.
/// </summary>
public interface IPatronAuthService
{
    Task<PatronAuthResult> VerifyAsync(string cardNumber, string pin);

    /// <summary>Hashes and stores a new PIN on the borrower (does not save - caller saves).</summary>
    void SetPin(Borrower borrower, string pin);

    /// <summary>Makes a new card number that isn't already in use, e.g. C1000123.</summary>
    Task<string> GenerateCardNumberAsync();
}

public class PatronAuthService : IPatronAuthService
{
    private static readonly Regex PinFormat = new(@"^\d{4,6}$");

    private readonly IBorrowerRepository _borrowers;
    private readonly IPasswordHasher<Borrower> _hasher;
    private readonly TimeProvider _time;
    private readonly KioskSettings _settings;

    public PatronAuthService(
        IBorrowerRepository borrowers,
        IPasswordHasher<Borrower> hasher,
        TimeProvider time,
        IOptions<KioskSettings> settings)
    {
        _borrowers = borrowers;
        _hasher = hasher;
        _time = time;
        _settings = settings.Value;
    }

    public static bool IsValidPinFormat(string? pin) => pin is not null && PinFormat.IsMatch(pin);

    public async Task<PatronAuthResult> VerifyAsync(string cardNumber, string pin)
    {
        // Same message for "no such card" and "wrong PIN", so the form can't
        // be used to find out which card numbers exist.
        const string generic = "That card number and PIN don't match our records.";

        if (string.IsNullOrWhiteSpace(cardNumber) || string.IsNullOrWhiteSpace(pin))
        {
            return new PatronAuthResult(PatronAuthStatus.InvalidCredentials, null, generic);
        }

        var borrower = await _borrowers.GetByCardNumberAsync(cardNumber);
        if (borrower is null)
        {
            return new PatronAuthResult(PatronAuthStatus.InvalidCredentials, null, generic);
        }

        var now = _time.GetUtcNow().UtcDateTime;
        if (borrower.PinLockedUntil.HasValue && borrower.PinLockedUntil > now)
        {
            return new PatronAuthResult(PatronAuthStatus.LockedOut, null,
                "This card is temporarily locked after too many wrong PINs. Please try again later or ask at the desk.");
        }

        if (string.IsNullOrEmpty(borrower.PinHash))
        {
            return new PatronAuthResult(PatronAuthStatus.NoPinSet, null,
                "This card doesn't have a PIN yet. Please ask reception to set one up.");
        }

        var result = _hasher.VerifyHashedPassword(borrower, borrower.PinHash, pin);
        if (result == PasswordVerificationResult.Failed)
        {
            borrower.FailedPinAttempts++;
            if (borrower.FailedPinAttempts >= _settings.MaxPinAttempts)
            {
                borrower.PinLockedUntil = now.AddMinutes(_settings.PinLockoutMinutes);
                borrower.FailedPinAttempts = 0;
                await _borrowers.UpdateAsync(borrower);
                return new PatronAuthResult(PatronAuthStatus.LockedOut, null,
                    $"Too many wrong PINs. This card is locked for {_settings.PinLockoutMinutes} minutes.");
            }

            await _borrowers.UpdateAsync(borrower);
            return new PatronAuthResult(PatronAuthStatus.InvalidCredentials, null, generic);
        }

        if (borrower.FailedPinAttempts != 0 || borrower.PinLockedUntil.HasValue)
        {
            borrower.FailedPinAttempts = 0;
            borrower.PinLockedUntil = null;
            await _borrowers.UpdateAsync(borrower);
        }

        return new PatronAuthResult(PatronAuthStatus.Success, borrower, "Welcome back.");
    }

    public void SetPin(Borrower borrower, string pin)
    {
        if (!IsValidPinFormat(pin))
        {
            throw new BusinessRuleException("A PIN must be 4 to 6 digits.");
        }

        borrower.PinHash = _hasher.HashPassword(borrower, pin);
        borrower.FailedPinAttempts = 0;
        borrower.PinLockedUntil = null;
    }

    public async Task<string> GenerateCardNumberAsync()
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            string candidate = "C" + Random.Shared.Next(1_000_000, 9_999_999);
            if (!await _borrowers.CardNumberExistsAsync(candidate))
            {
                return candidate;
            }
        }

        throw new BusinessRuleException("Couldn't generate a unique card number - please try again.");
    }
}
