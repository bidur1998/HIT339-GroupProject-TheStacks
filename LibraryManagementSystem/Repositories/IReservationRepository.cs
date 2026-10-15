using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Repositories;

public interface IReservationRepository
{
    Task<Reservation?> GetByIdAsync(int id);

    /// <summary>Active reservations (Waiting / AwaitingTransfer / Ready) for one item, oldest first - the queue.</summary>
    Task<List<Reservation>> GetActiveQueueForItemAsync(int itemId);

    Task<List<Reservation>> GetActiveForBorrowerAsync(int borrowerId);

    /// <summary>Every active reservation across the library, for the reception hold-shelf screen.</summary>
    Task<List<Reservation>> GetAllActiveAsync();

    /// <summary>Ready-for-pickup holds whose pickup deadline has passed.</summary>
    Task<List<Reservation>> GetExpiredReadyHoldsAsync(DateTime nowUtc);

    Task<List<Reservation>> GetAllAsync();
    Task AddAsync(Reservation reservation);
    Task UpdateAsync(Reservation reservation);
}
