using MongoDB.Driver;
using SpendPulse.Client.Models;

namespace SpendPulse.Server.Repositories;

public class SyncLogRepository(IMongoDatabase database) : ISyncLogRepository
{
    private readonly IMongoCollection<SyncLogEntry> _collection =
        database.GetCollection<SyncLogEntry>("syncLog");

    public async Task Add(SyncLogEntry entry)
    {
        await _collection.InsertOneAsync(entry);
    }

    public async Task<SyncLogPage> GetPage(int page, int pageSize)
    {
        var totalCount = (int)await _collection.CountDocumentsAsync(FilterDefinition<SyncLogEntry>.Empty);
        var entries = await _collection.Find(FilterDefinition<SyncLogEntry>.Empty)
            .SortByDescending(e => e.StartedAt)
            .Skip(page * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return new SyncLogPage { Entries = entries, TotalCount = totalCount };
    }
}
