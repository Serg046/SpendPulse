using SpendPulse.Client.Models;

namespace SpendPulse.Client.Repositories;

public interface IMerchantGroupRepository
{
    public const string OthersGroupName = "Others";
    public const string PendingGroupName = "Pending";

    Task<List<string>> GetGroups();

    Task AddGroup(string name);

    Task RemoveGroup(string name);

    Task RenameGroup(string oldName, string newName);

    Task<List<MerchantGroupAssignment>> GetAssignments();

    Task SetAssignment(string merchantName, string groupName);
}
