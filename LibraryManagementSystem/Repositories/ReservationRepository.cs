using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Repositories;

public class ReservationRepository : IReservationRepository
{
    private readonly ApplicationDbContext _context;

    public ReservationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    private IQueryable<Reservation> WithDetails =>
        _context.Reservations
            .Include(r => r.Item).ThenInclude(i => i.CurrentBranch)
            .Include(r => r.Borrower)
            .Include(r => r.PickupBranch);

    // Written as explicit comparisons (not an array .Contains) so EF Core
    // translates it to plain SQL "Status = 'Waiting' OR ..." with the
    // enum-to-string conversion, which is reliable on SQLite.
    private static readonly System.Linq.Expressions.Expression<Func<Reservation, bool>> IsActiveReservation =
        r => r.Status == ReservationStatus.Waiting
             || r.Status == ReservationStatus.AwaitingTransfer
             || r.Status == ReservationStatus.ReadyForPickup;

    public async Task<Reservation?> GetByIdAsync(int id) =>
        await WithDetails.FirstOrDefaultAsync(r => r.Id == id);

    public async Task<List<Reservation>> GetActiveQueueForItemAsync(int itemId) =>
        await WithDetails
            .Where(r => r.ItemId == itemId)
            .Where(IsActiveReservation)
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .ToListAsync();

    public async Task<List<Reservation>> GetActiveForBorrowerAsync(int borrowerId) =>
        await WithDetails
            .Where(r => r.BorrowerId == borrowerId)
            .Where(IsActiveReservation)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();

    public async Task<List<Reservation>> GetAllActiveAsync() =>
        await WithDetails
            .Where(IsActiveReservation)
            .OrderBy(r => r.ItemId)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync();

    public async Task<List<Reservation>> GetExpiredReadyHoldsAsync(DateTime nowUtc) =>
        await WithDetails
            .Where(r => r.Status == ReservationStatus.ReadyForPickup
                        && r.HoldExpiresAt != null
                        && r.HoldExpiresAt < nowUtc)
            .ToListAsync();

    public async Task<List<Reservation>> GetAllAsync() =>
        await WithDetails.OrderByDescending(r => r.CreatedAt).ToListAsync();

    public async Task AddAsync(Reservation reservation)
    {
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Reservation reservation)
    {
        _context.Reservations.Update(reservation);
        await _context.SaveChangesAsync();
    }
}
