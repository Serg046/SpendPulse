using SpendPulse.Client.Models;

namespace SpendPulse.Client.Repositories;

public interface IMerchantNameExclusionRepository
{
    Task<List<MerchantNameExclusion>> GetAll();

    Task Add(string word, string? merchantName = null);

    Task Remove(string word, string? merchantName = null);
}
