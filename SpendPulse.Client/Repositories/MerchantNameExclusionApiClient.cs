using System.Net.Http.Json;
using SpendPulse.Client.Models;

namespace SpendPulse.Client.Repositories;

public class MerchantNameExclusionApiClient(HttpClient http) : IMerchantNameExclusionRepository
{
    public async Task<List<MerchantNameExclusion>> GetAll()
    {
        return await http.GetFromJsonAsync<List<MerchantNameExclusion>>("api/merchant-name-exclusions") ?? [];
    }

    public async Task Add(string word, string? merchantName = null)
    {
        var url = merchantName is null
            ? "api/merchant-name-exclusions"
            : $"api/merchant-name-exclusions?merchantName={Uri.EscapeDataString(merchantName)}";
        var response = await http.PostAsJsonAsync(url, word);
        response.EnsureSuccessStatusCode();
    }

    public async Task Remove(string word, string? merchantName = null)
    {
        var url = merchantName is null
            ? "api/merchant-name-exclusions/remove"
            : $"api/merchant-name-exclusions/remove?merchantName={Uri.EscapeDataString(merchantName)}";
        var response = await http.PostAsJsonAsync(url, word);
        response.EnsureSuccessStatusCode();
    }
}
