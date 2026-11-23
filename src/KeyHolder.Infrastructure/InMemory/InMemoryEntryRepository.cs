using KeyHolder.Domain.Entries;
using KeyHolder.Domain.Tags;
using KeyHolder.Infrastructure.FakeData;

namespace KeyHolder.Infrastructure.InMemory;

/// <summary>
/// Thread-safe in-memory repository. It behaves like the SQLite repository and is used for contract tests
/// and design-time previews.
/// </summary>
public sealed class InMemoryEntryRepository : IEntryRepository, IVaultStateStore, IFakeDataStore, ITagColorStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, VaultEntry> _entries = [];
    private readonly Dictionary<(TagKind Kind, string Key), TagColorAssignment> _tagColors = [];
    private bool _dirty;
    private bool _seeded;

    public Task<IReadOnlyList<EntrySummary>> ListSummariesAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<EntrySummary>>(
                _entries.Values.Select(entry => entry.ToSummary()).ToList());
        }
    }

    public Task<EntrySummary?> GetSummaryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_entries.TryGetValue(id, out var entry) ? entry.ToSummary() : null);
        }
    }

    public Task<EntrySecret?> GetSecretAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_entries.TryGetValue(id, out var entry) ? entry.Secret : null);
        }
    }

    public Task<VaultEntry?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_entries.GetValueOrDefault(id));
        }
    }

    public Task<IReadOnlyList<TagColorAssignment>> ListTagColorsAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<TagColorAssignment>>([.. _tagColors.Values]);
        }
    }

    public Task AddAsync(
        VaultEntry entry,
        IReadOnlyCollection<TagColorAssignment>? tagColors = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var valid = entry with
        {
            Metadata = EntryValidator.EnsureValid(entry.Metadata),
            Secret = EntryValidator.EnsureValid(entry.Secret),
        };
        var colors = TagColorAssignments.Normalize(tagColors);

        lock (_gate)
        {
            if (!_entries.TryAdd(valid.Id, valid))
            {
                throw new InvalidOperationException("An entry with this id already exists.");
            }

            SaveTagColors(colors);
            _dirty = true;
        }

        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(
        Guid id,
        EntryMetadata metadata,
        EntrySecret? newSecret,
        DateTimeOffset now,
        IReadOnlyCollection<TagColorAssignment>? tagColors = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var normalizedMetadata = EntryValidator.EnsureValid(metadata);
        var normalizedSecret = newSecret is null ? null : EntryValidator.EnsureValid(newSecret);
        var colors = TagColorAssignments.Normalize(tagColors);

        lock (_gate)
        {
            if (!_entries.TryGetValue(id, out var existing))
            {
                return Task.FromResult(false);
            }

            _entries[id] = existing with
            {
                Metadata = normalizedMetadata,
                Secret = normalizedSecret ?? existing.Secret,
                UpdatedAt = now,
            };
            SaveTagColors(colors);
            _dirty = true;
            return Task.FromResult(true);
        }
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var removed = _entries.Remove(id);
            _dirty |= removed;
            return Task.FromResult(removed);
        }
    }

    public Task<bool> GetLocalChangedSinceReplicaAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_dirty);
        }
    }

    public Task<bool> SeedOnceAsync(
        IReadOnlyList<VaultEntry> entries,
        IReadOnlyCollection<TagColorAssignment>? tagColors = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var colors = TagColorAssignments.Normalize(tagColors);

        lock (_gate)
        {
            if (_seeded)
            {
                return Task.FromResult(false);
            }

            foreach (var entry in entries)
            {
                _entries[entry.Id] = entry with
                {
                    Metadata = EntryValidator.EnsureValid(entry.Metadata),
                    Secret = EntryValidator.EnsureValid(entry.Secret),
                };
            }

            SaveTagColors(colors);
            _seeded = true;
            return Task.FromResult(true);
        }
    }

    // Must be called inside the lock.
    private void SaveTagColors(IReadOnlyList<TagColorAssignment> colors)
    {
        foreach (var assignment in colors)
        {
            var key = (assignment.Kind, TagColors.KeyOf(assignment.Name));
            if (assignment.Color == TagColor.Default)
            {
                _tagColors.Remove(key);
            }
            else
            {
                _tagColors[key] = assignment;
            }
        }
    }
}
