using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;

namespace LibraryManagementSystem.Services;

public class TransferException : BusinessRuleException
{
    public TransferException(string message) : base(message) { }
}

/// <summary>
/// Inter-branch transfers (Group Project - Multi-Branch Inventory).
/// Request -> Dispatch (sending branch) -> Receive (receiving branch).
/// The item's CurrentBranch only changes on Receive, because until then it
/// is physically in a van/tote and not on either branch's shelf.
/// </summary>
public interface ITransferService
{
    Task<BranchTransfer> RequestAsync(int itemId, int toBranchId, string requestedBy, string? reason);
    Task<BranchTransfer> DispatchAsync(int transferId, string staffName, int? actingBranchId);
    Task<BranchTransfer> ReceiveAsync(int transferId, string staffName, int? actingBranchId);
    Task<BranchTransfer> CancelAsync(int transferId);
}

public class TransferService : ITransferService
{
    private readonly ITransferRepository _transfers;
    private readonly IItemRepository _items;
    private readonly IBranchRepository _branches;
    private readonly IReservationService _reservations;
    private readonly TimeProvider _time;

    public TransferService(
        ITransferRepository transfers,
        IItemRepository items,
        IBranchRepository branches,
        IReservationService reservations,
        TimeProvider time)
    {
        _transfers = transfers;
        _items = items;
        _branches = branches;
        _reservations = reservations;
        _time = time;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public async Task<BranchTransfer> RequestAsync(int itemId, int toBranchId, string requestedBy, string? reason)
    {
        var item = await _items.GetByIdAsync(itemId)
            ?? throw new TransferException("That item could not be found.");

        if (item.CurrentBranchId is null)
        {
            throw new TransferException($"'{item.Name}' isn't assigned to a branch yet - set its branch on the item first.");
        }

        if (item.CurrentBranchId == toBranchId)
        {
            throw new TransferException($"'{item.Name}' is already at that branch.");
        }

        if (item.Status == ItemStatus.Borrowed)
        {
            throw new TransferException($"'{item.Name}' is on loan. It can be transferred once it has been returned.");
        }

        if (item.Status == ItemStatus.Destroyed)
        {
            throw new TransferException($"'{item.Name}' is marked Destroyed and can't be transferred.");
        }

        if (await _transfers.GetOpenForItemAsync(itemId) is not null)
        {
            throw new TransferException($"'{item.Name}' already has a transfer in progress.");
        }

        var toBranch = await _branches.GetByIdAsync(toBranchId)
            ?? throw new TransferException("Please choose a valid destination branch.");

        var transfer = new BranchTransfer
        {
            ItemId = item.Id,
            FromBranchId = item.CurrentBranchId.Value,
            ToBranchId = toBranch.Id,
            Status = TransferStatus.Requested,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            RequestedAt = Now,
            RequestedBy = requestedBy
        };

        await _transfers.AddAsync(transfer);
        return transfer;
    }

    public async Task<BranchTransfer> DispatchAsync(int transferId, string staffName, int? actingBranchId)
    {
        var transfer = await GetAsync(transferId);

        if (transfer.Status != TransferStatus.Requested)
        {
            throw new TransferException("Only a requested transfer can be dispatched.");
        }

        if (actingBranchId.HasValue && actingBranchId != transfer.FromBranchId)
        {
            throw new TransferException($"Only staff at {transfer.FromBranch?.Name ?? "the sending branch"} can dispatch this item.");
        }

        transfer.Status = TransferStatus.InTransit;
        transfer.DispatchedAt = Now;
        transfer.DispatchedBy = staffName;
        await _transfers.UpdateAsync(transfer);
        return transfer;
    }

    public async Task<BranchTransfer> ReceiveAsync(int transferId, string staffName, int? actingBranchId)
    {
        var transfer = await GetAsync(transferId);

        if (transfer.Status != TransferStatus.InTransit)
        {
            throw new TransferException("Only an item that has been dispatched (in transit) can be received.");
        }

        if (actingBranchId.HasValue && actingBranchId != transfer.ToBranchId)
        {
            throw new TransferException($"Only staff at {transfer.ToBranch?.Name ?? "the receiving branch"} can receive this item.");
        }

        transfer.Status = TransferStatus.Received;
        transfer.ReceivedAt = Now;
        transfer.ReceivedBy = staffName;
        await _transfers.UpdateAsync(transfer);

        var item = await _items.GetByIdAsync(transfer.ItemId)
            ?? throw new TransferException("The item on this transfer no longer exists.");
        item.CurrentBranchId = transfer.ToBranchId;
        await _items.UpdateAsync(item);

        // If someone reserved this item for pickup here, it's now ready for them.
        await _reservations.OnItemArrivedAtBranchAsync(item);

        return transfer;
    }

    public async Task<BranchTransfer> CancelAsync(int transferId)
    {
        var transfer = await GetAsync(transferId);

        if (!transfer.IsOpen)
        {
            throw new TransferException("This transfer is already finished.");
        }

        if (transfer.ReservationId.HasValue)
        {
            throw new TransferException("This transfer was created for a patron's reservation. Cancel the reservation first if it's no longer needed.");
        }

        transfer.Status = TransferStatus.Cancelled;
        transfer.CancelledAt = Now;
        await _transfers.UpdateAsync(transfer);
        return transfer;
    }

    private async Task<BranchTransfer> GetAsync(int transferId) =>
        await _transfers.GetByIdAsync(transferId)
        ?? throw new TransferException("That transfer could not be found.");
}
