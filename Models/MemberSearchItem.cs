namespace SubscriptionTracker.AvaloniaApp.Models;

public sealed class MemberSearchItem
{
    public string MemberId { get; init; } = "";
    public string Name { get; init; } = "";
    public override string ToString() => Name;
}
