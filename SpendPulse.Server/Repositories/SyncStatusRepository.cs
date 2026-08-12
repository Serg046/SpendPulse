using SpendPulse.Client.Models;
using SpendPulse.Client.Repositories;
using SpendPulse.Server.Services;

namespace SpendPulse.Server.Repositories;

public class SyncStatusRepository(ISettingsRepository settingsRepository, SyncService syncService, ISyncLogRepository syncLogRepository, IHttpContextAccessor httpContextAccessor, IConfiguration configuration) : ISyncStatusRepository
{
    private const int TokenExpiryWarningDays = 3;

    public Task<string?> GetUsername() =>
        Task.FromResult(httpContextAccessor.HttpContext?.User.Identity?.Name);

    public Task<bool> IsAdmin() =>
        Task.FromResult(httpContextAccessor.HttpContext?.User.IsInRole("Admin") ?? false);

    public async Task<DateTime?> GetLastDataUpdate()
    {
        var settings = await settingsRepository.Get();
        return settings.BankSession.LastDataUpdate;
    }

    public async Task<bool> IsTokenExpiringSoon()
    {
        var settings = await settingsRepository.Get();
        var lifetimeDays = configuration.GetValue<int>("EnableBanking:TokenLifetimeDays");
        var expiresAt = settings.BankSession.LastTokenUpdate.AddDays(lifetimeDays);
        return expiresAt - DateTime.UtcNow <= TimeSpan.FromDays(TokenExpiryWarningDays);
    }

    public async Task<DateTime?> Sync()
    {
        await syncService.Sync();
        return await GetLastDataUpdate();
    }

    public async Task<string?> RefreshToken(string? code = null) => await syncService.RefreshToken(code);

    public async Task<SyncLogPage> GetSyncLog(int page, int pageSize) => await syncLogRepository.GetPage(page, pageSize);
}
