using SpendPulse.Client.Models;

namespace SpendPulse.Client.Repositories;

public interface ISyncStatusRepository
{
    Task<string?> GetUsername();

    Task<bool> IsAdmin();

    Task<DateTime?> GetLastDataUpdate();

    Task<bool> IsTokenExpiringSoon();

    Task<DateTime?> Sync();

    Task<string?> RefreshToken(string? code = null);

    Task<SyncHistoryPage> GetSyncHistory(int page, int pageSize);
}
