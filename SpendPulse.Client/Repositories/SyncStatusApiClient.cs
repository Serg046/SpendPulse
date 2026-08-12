using System.Net.Http.Json;
using System.Text.Json;
using SpendPulse.Client.Models;

namespace SpendPulse.Client.Repositories;

public class SyncStatusApiClient(HttpClient http) : ISyncStatusRepository
{
    public async Task<string?> GetUsername()
    {
        return await http.GetFromJsonAsync<string?>("api/sync-status/username");
    }

    public async Task<bool> IsAdmin()
    {
        return await http.GetFromJsonAsync<bool>("api/sync-status/is-admin");
    }

    public async Task<DateTime?> GetLastDataUpdate()
    {
        return await http.GetFromJsonAsync<DateTime?>("api/sync-status/last-data-update");
    }

    public async Task<bool> IsTokenExpiringSoon()
    {
        return await http.GetFromJsonAsync<bool>("api/sync-status/is-token-expiring-soon");
    }

    public async Task<DateTime?> Sync()
    {
        var response = await http.PostAsync("api/sync-status/sync", null);
        return await response.Content.ReadFromJsonAsync<DateTime?>();
    }

    public async Task<string?> RefreshToken(string? code = null)
    {
        var url = code is null
            ? "api/sync-status/refresh-token"
            : $"api/sync-status/refresh-token?code={Uri.EscapeDataString(code)}";
        var response = await http.PostAsync(url, null);
        var body = await response.Content.ReadAsStringAsync();
        return string.IsNullOrEmpty(body) ? null : JsonSerializer.Deserialize<string>(body);
    }

    public async Task<SyncLogPage> GetSyncLog(int page, int pageSize)
    {
        return await http.GetFromJsonAsync<SyncLogPage>($"api/sync-status/log?page={page}&pageSize={pageSize}") ?? new SyncLogPage();
    }
}
