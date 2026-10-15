using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Services;

/// <summary>
/// All notification wording in one place, so it's easy to review and to
/// unit test. Each method returns the email and SMS versions together.
/// </summary>
public static class NotificationTemplates
{
    private const string Sign = "\n\nThe Stacks Library";

    private static string D(DateTime date) => date.ToString("ddd d MMM yyyy");

    public static NotificationMessage ItemBorrowed(Loan loan, string itemName, string? branchName) =>
        new(NotificationType.ItemBorrowed,
            $"You borrowed \"{itemName}\"",
            $"Hi {loan.Borrower?.FullName},\n\nYou borrowed \"{itemName}\"{(branchName is null ? "" : $" at {branchName}")}. " +
            $"It is due back on {D(loan.DueDate)}." + Sign,
            $"The Stacks: \"{Trim(itemName)}\" borrowed, due {D(loan.DueDate)}.");

    public static NotificationMessage DueSoon(Loan loan, string itemName, int daysLeft) =>
        new(NotificationType.DueSoon,
            $"Reminder: \"{itemName}\" is due {(daysLeft == 0 ? "today" : $"in {daysLeft} day{(daysLeft == 1 ? "" : "s")}")}",
            $"Hi {loan.Borrower?.FullName},\n\nJust a reminder that \"{itemName}\" is due back on {D(loan.DueDate)}. " +
            "Late returns are fined per day, so please return it on time." + Sign,
            $"The Stacks: \"{Trim(itemName)}\" is due {D(loan.DueDate)}.");

    public static NotificationMessage OverdueFineAccruing(Loan loan, string itemName, int daysOverdue, decimal fineSoFar, decimal finePerDay) =>
        new(NotificationType.OverdueFineAccruing,
            $"Overdue: \"{itemName}\" - fine now ${fineSoFar:0.00}",
            $"Hi {loan.Borrower?.FullName},\n\n\"{itemName}\" was due on {D(loan.DueDate)} and is now {daysOverdue} day{(daysOverdue == 1 ? "" : "s")} overdue. " +
            $"A fine of ${finePerDay:0.00} per day is accruing (${fineSoFar:0.00} so far). Please return it as soon as you can." + Sign,
            $"The Stacks: \"{Trim(itemName)}\" is {daysOverdue}d overdue. Fine so far ${fineSoFar:0.00}.");

    public static NotificationMessage FineIssued(Loan loan, string itemName, decimal amount, int daysOverdue) =>
        new(NotificationType.FineIssued,
            $"Fine of ${amount:0.00} for \"{itemName}\"",
            $"Hi {loan.Borrower?.FullName},\n\nThanks for returning \"{itemName}\". It was {daysOverdue} day{(daysOverdue == 1 ? "" : "s")} late, " +
            $"so a fine of ${amount:0.00} has been added to your account. You can pay it at any branch reception desk. " +
            "You won't be able to borrow more items until it is paid." + Sign,
            $"The Stacks: fine ${amount:0.00} added for late return of \"{Trim(itemName)}\". Pay at any desk.");

    public static NotificationMessage FinePaid(Loan loan, string itemName, decimal amount) =>
        new(NotificationType.FinePaid,
            $"Payment received: ${amount:0.00}",
            $"Hi {loan.Borrower?.FullName},\n\nWe've received your payment of ${amount:0.00} for the late return of \"{itemName}\". Thank you." + Sign,
            $"The Stacks: payment of ${amount:0.00} received. Thank you.");

    public static NotificationMessage ReservationPlaced(Reservation reservation, string itemName, int queuePosition, string pickupBranchName) =>
        new(NotificationType.ReservationPlaced,
            $"Hold placed on \"{itemName}\"",
            $"Hi {reservation.Borrower?.FullName},\n\nYou're number {queuePosition} in the queue for \"{itemName}\". " +
            $"We'll let you know as soon as it's ready to collect at {pickupBranchName}." + Sign,
            $"The Stacks: hold placed on \"{Trim(itemName)}\". You're #{queuePosition} in the queue.");

    public static NotificationMessage ReservationReady(Reservation reservation, string itemName, string pickupBranchName, DateTime collectBy) =>
        new(NotificationType.ReservationReady,
            $"Ready to collect: \"{itemName}\"",
            $"Hi {reservation.Borrower?.FullName},\n\nGood news - \"{itemName}\" is waiting for you on the hold shelf at {pickupBranchName}. " +
            $"Please collect it by {D(collectBy)}, or it will go to the next person in the queue." + Sign,
            $"The Stacks: \"{Trim(itemName)}\" is ready at {pickupBranchName}. Collect by {D(collectBy)}.");

    public static NotificationMessage ReservationCancelled(Reservation reservation, string itemName, string reason) =>
        new(NotificationType.ReservationCancelled,
            $"Hold cancelled: \"{itemName}\"",
            $"Hi {reservation.Borrower?.FullName},\n\nYour hold on \"{itemName}\" has been cancelled: {reason}" + Sign,
            $"The Stacks: hold on \"{Trim(itemName)}\" cancelled - {reason}");

    /// <summary>Keeps SMS text short by shortening long titles.</summary>
    private static string Trim(string text) => text.Length <= 40 ? text : text[..37] + "...";
}
