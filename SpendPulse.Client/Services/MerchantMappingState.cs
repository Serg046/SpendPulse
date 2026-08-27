namespace SpendPulse.Client.Services;

public class MerchantMappingState
{
    public event Action? Changed;

    public void NotifyChanged() => Changed?.Invoke();
}
