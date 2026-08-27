using SpendPulse.Client.Models;
using SpendPulse.Client.Repositories;
using MongoDB.Driver;

namespace SpendPulse.Server.Repositories;

public class MerchantNameExclusionRepository(IMongoDatabase database) : IMerchantNameExclusionRepository
{
    private readonly IMongoCollection<MerchantNameExclusion> _collection =
        database.GetCollection<MerchantNameExclusion>("merchantNameExclusions");

    public async Task<List<MerchantNameExclusion>> GetAll()
    {
        return await _collection.Find(FilterDefinition<MerchantNameExclusion>.Empty).ToListAsync();
    }

    public async Task Add(string word, string? merchantName = null)
    {
        var filter = Builders<MerchantNameExclusion>.Filter.Eq(e => e.Word, word) &
                     Builders<MerchantNameExclusion>.Filter.Eq(e => e.MerchantName, merchantName);
        var update = Builders<MerchantNameExclusion>.Update
            .Set(e => e.Word, word)
            .Set(e => e.MerchantName, merchantName);

        await _collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
    }

    public async Task Remove(string word, string? merchantName = null)
    {
        var filter = Builders<MerchantNameExclusion>.Filter.Eq(e => e.Word, word) &
                     Builders<MerchantNameExclusion>.Filter.Eq(e => e.MerchantName, merchantName);
        await _collection.DeleteOneAsync(filter);
    }
}
