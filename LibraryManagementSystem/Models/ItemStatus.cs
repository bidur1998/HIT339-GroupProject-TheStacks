namespace LibraryManagementSystem.Models;

/// <summary>
/// Every item in the library - regardless of whether it is a Book, Toy or
/// Music item - carries exactly one of these statuses, as required by the
/// assignment brief.
/// </summary>
public enum ItemStatus
{
    Available,
    Borrowed,
    Damaged,
    Destroyed
}
