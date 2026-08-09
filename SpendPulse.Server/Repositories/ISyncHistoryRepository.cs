using SpendPulse.Client.Models;

namespace SpendPulse.Server.Repositories;

public interface ISyncHistoryRepository
{
    Task Add(SyncHistoryEntry entry);

    Task<SyncHistoryPage> GetPage(int page, int pageSize);
}
