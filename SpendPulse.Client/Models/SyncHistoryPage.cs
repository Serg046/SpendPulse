namespace SpendPulse.Client.Models;

public class SyncHistoryPage
{
    public List<SyncHistoryEntry> Entries { get; set; } = [];

    public int TotalCount { get; set; }
}
