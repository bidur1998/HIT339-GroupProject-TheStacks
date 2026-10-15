namespace LibraryManagementSystem.Services;

/// <summary>A book or album as described by an external metadata provider.</summary>
public record ExternalMediaRecord(
    string ExternalId,
    string MediaType,
    string Title,
    string Creator,
    string? Genre,
    int? Year,
    string? Description);

/// <summary>
/// An external public book/media metadata source (Group Project - External
/// Book/Media Data Importer). The app ships a MOCK provider only; because
/// the importer depends on this interface, a real HTTP client (e.g. for a
/// public catalogue API) could be registered in Program.cs later instead.
/// </summary>
public interface IMediaMetadataProvider
{
    string ProviderName { get; }

    Task<List<ExternalMediaRecord>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<ExternalMediaRecord?> GetByIdAsync(string externalId, CancellationToken cancellationToken = default);
}

/// <summary>
/// SIMULATED external provider. Returns records from a small built-in data
/// set after a short artificial delay, so the admin screen behaves like it
/// is calling a remote service. No network calls are made. The records are
/// sample data for testing, and the IDs are made up ("MOCK-...").
/// </summary>
public class MockMediaMetadataProvider : IMediaMetadataProvider
{
    private static readonly List<ExternalMediaRecord> Catalogue = new()
    {
        new("MOCK-BK-0001", "Book", "Pride and Prejudice", "Jane Austen", "Classic", 1813, "Elizabeth Bennet and Mr Darcy, and the trouble with first impressions."),
        new("MOCK-BK-0002", "Book", "Frankenstein", "Mary Shelley", "Gothic", 1818, "A young scientist creates life and must face what he has made."),
        new("MOCK-BK-0003", "Book", "The Lord of the Rings", "J.R.R. Tolkien", "Fantasy", 1954, "The quest to destroy the One Ring."),
        new("MOCK-BK-0004", "Book", "The Silmarillion", "J.R.R. Tolkien", "Fantasy", 1977, "Myths and legends of Middle-earth's First Age."),
        new("MOCK-BK-0005", "Book", "The Pragmatic Programmer", "Andrew Hunt and David Thomas", "Software Engineering", 1999, "Practical advice for working programmers."),
        new("MOCK-BK-0006", "Book", "Design Patterns", "Gamma, Helm, Johnson and Vlissides", "Software Engineering", 1994, "Reusable object-oriented design patterns."),
        new("MOCK-BK-0007", "Book", "Cloudstreet", "Tim Winton", "Australian Fiction", 1991, "Two families share a house in post-war Perth."),
        new("MOCK-BK-0008", "Book", "The Secret River", "Kate Grenville", "Historical Fiction", 2005, "A settler family on the Hawkesbury River in the early 1800s."),
        new("MOCK-BK-0009", "Book", "Dark Emu", "Bruce Pascoe", "Non-fiction", 2014, "Aboriginal agriculture and land management before colonisation."),
        new("MOCK-BK-0010", "Book", "Foundation", "Isaac Asimov", "Science Fiction", 1951, "A mathematician plans to shorten a galactic dark age."),
        new("MOCK-MU-0001", "Music", "Rumours", "Fleetwood Mac", "Rock", 1977, "Studio album."),
        new("MOCK-MU-0002", "Music", "Diesel and Dust", "Midnight Oil", "Rock", 1987, "Studio album."),
        new("MOCK-MU-0003", "Music", "Kind of Blue", "Miles Davis", "Jazz", 1959, "Studio album."),
        new("MOCK-MU-0004", "Music", "Back in Black", "AC/DC", "Hard Rock", 1980, "Studio album."),
        new("MOCK-MU-0005", "Music", "The Dark Side of the Moon", "Pink Floyd", "Progressive Rock", 1973, "Studio album.")
    };

    public string ProviderName => "Mock Open Catalogue (simulated)";

    public async Task<List<ExternalMediaRecord>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        await Task.Delay(250, cancellationToken); // pretend network latency

        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<ExternalMediaRecord>();
        }

        var q = query.Trim();
        return Catalogue
            .Where(r => r.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || r.Creator.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || (r.Genre?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(20)
            .ToList();
    }

    public async Task<ExternalMediaRecord?> GetByIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);
        return Catalogue.FirstOrDefault(r => r.ExternalId.Equals(externalId, StringComparison.OrdinalIgnoreCase));
    }
}
