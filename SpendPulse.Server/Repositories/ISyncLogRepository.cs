using SpendPulse.Client.Models;

namespace SpendPulse.Server.Repositories;

public interface ISyncLogRepository
{
    Task Add(SyncLogEntry entry);

    Task<SyncLogPage> GetPage(int page, int pageSize);
}
