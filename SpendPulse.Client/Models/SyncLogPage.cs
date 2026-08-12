namespace SpendPulse.Client.Models;

public class SyncLogPage
{
    public List<SyncLogEntry> Entries { get; set; } = [];

    public int TotalCount { get; set; }
}
