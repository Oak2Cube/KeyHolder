using KeyHolder.Domain.Entries;
using KeyHolder.Domain.Locking;

namespace KeyHolder.Infrastructure.FakeData;

/// <summary>
/// Adds obviously fake demo entries on first run. Every value is marked FAKE or uses the
/// reserved <c>example.test</c> domain. Never put real credentials in this vault.
/// </summary>
public sealed class FakeDataSeeder
{
    private readonly IFakeDataStore _store;
    private readonly IClock _clock;

    public FakeDataSeeder(IFakeDataStore store, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);

        _store = store;
        _clock = clock;
    }

    /// <summary>
    /// Seeds the demo entries once per vault.
    /// </summary>
    /// <returns><see langword="true"/> when entries were added.</returns>
    public Task<bool> SeedIfNeededAsync(CancellationToken cancellationToken = default) =>
        _store.SeedOnceAsync(CreateEntries(_clock.UtcNow), cancellationToken);

    /// <summary>
    /// The demo entries. Update times are spread over several days so Date sorting is visible.
    /// </summary>
    public static IReadOnlyList<VaultEntry> CreateEntries(DateTimeOffset now)
    {
        var specs = new (EntryMetadata Metadata, EntrySecret Secret)[]
        {
            (
                new EntryMetadata(
                    "Fake Mail",
                    "fake.user",
                    "fake.user@example.test",
                    "https://mail.example.test",
                    ["Email", "Account"],
                    ["Sensitive"],
                    5),
                new EntrySecret("FAKE-mail-password-1", "FAKE DATA: not a real account.")),
            (
                new EntryMetadata(
                    "Fake Bank",
                    "fake.customer",
                    "fake.customer@example.test",
                    "https://bank.example.test",
                    ["Account"],
                    ["Finance", "Sensitive"],
                    10),
                new EntrySecret("FAKE-bank-password-2", "FAKE DATA: pretend security answers.")),
            (
                new EntryMetadata(
                    "Fake Cloud Storage",
                    "fake.storage",
                    null,
                    "https://storage.example.test",
                    ["Account"],
                    [],
                    3),
                new EntrySecret("FAKE-storage-password-3", null)),
            (
                new EntryMetadata(
                    "Fake Social Network",
                    "fake_social",
                    "social@example.test",
                    "https://social.example.test",
                    ["Account"],
                    ["Public"],
                    1),
                new EntrySecret("FAKE-social-password-4", "FAKE DATA")),
            (
                new EntryMetadata(
                    "Fake Wi-Fi Router",
                    "admin",
                    null,
                    "http://192.0.2.1",
                    ["Network"],
                    ["Location"],
                    null),
                new EntrySecret("FAKE-router-password-5", "FAKE DATA: 192.0.2.0/24 is a documentation range.")),
            (
                new EntryMetadata(
                    "Fake Phone PIN",
                    null,
                    null,
                    null,
                    ["Phone number"],
                    ["Sensitive"],
                    8),
                new EntrySecret("FAKE-0000", "FAKE DATA")),
            (
                new EntryMetadata(
                    "Fake Streaming Service",
                    "fake.viewer",
                    "viewer@example.test",
                    "https://stream.example.test",
                    ["Account"],
                    [],
                    null),
                new EntrySecret("FAKE-stream-password-6", null)),
            (
                new EntryMetadata(
                    "Fake Tax Portal",
                    "fake.taxpayer",
                    "tax@example.test",
                    "https://tax.example.test",
                    ["Account", "Document"],
                    ["Finance", "Sensitive"],
                    9),
                new EntrySecret("FAKE-tax-password-7", "FAKE DATA: not a real tax id.")),
            (
                new EntryMetadata(
                    "Fake Gym Membership",
                    null,
                    "gym@example.test",
                    "https://gym.example.test",
                    ["Account"],
                    [],
                    2),
                new EntrySecret("FAKE-gym-password-8", null)),
            (
                new EntryMetadata(
                    "Fake Notes Only",
                    null,
                    null,
                    null,
                    ["Note"],
                    [],
                    null),
                new EntrySecret(null, "FAKE DATA: an entry with no password.")),
        };

        var entries = new List<VaultEntry>(specs.Length);
        for (var i = 0; i < specs.Length; i++)
        {
            var created = now.AddDays(-(specs.Length - i));
            var entry = VaultEntry.Create(specs[i].Metadata, specs[i].Secret, created, GuidFor(i));
            entries.Add(entry);
        }

        return entries;
    }

    // Stable ids keep the demo data predictable in tests.
    private static Guid GuidFor(int index) =>
        new(0x4B455948, 0x4F4C, 0x4445, 0x46, 0x41, 0x4B, 0x45, 0x00, 0x00, 0x00, (byte)index);
}
