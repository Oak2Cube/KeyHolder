using KeyHolder.Domain.Entries;
using KeyHolder.Infrastructure.InMemory;

namespace KeyHolder.IntegrationTests.Persistence;

public sealed class InMemoryEntryRepositoryTests : EntryRepositoryContract
{
    protected override IEntryRepository CreateRepository() => new InMemoryEntryRepository();
}
