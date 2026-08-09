using MongoDB.Driver;
using SpendPulse.Client.Models;

namespace SpendPulse.Server.Repositories;

public class SyncHistoryRepository(IMongoDatabase database) : ISyncHistoryRepository
{
    private readonly IMongoCollection<SyncHistoryEntry> _collection =
        database.GetCollection<SyncHistoryEntry>("syncHistory");

    public async Task Add(SyncHistoryEntry entry)
    {
        await _collection.InsertOneAsync(entry);
    }

    public async Task<SyncHistoryPage> GetPage(int page, int pageSize)
    {
        var totalCount = (int)await _collection.CountDocumentsAsync(FilterDefinition<SyncHistoryEntry>.Empty);
        var entries = await _collection.Find(FilterDefinition<SyncHistoryEntry>.Empty)
            .SortByDescending(e => e.StartedAt)
            .Skip(page * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return new SyncHistoryPage { Entries = entries, TotalCount = totalCount };
    }
}
