namespace SubscriptionTracker.AvaloniaApp.Models;

public sealed class ReportRow
{
    public string MemberId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Plan { get; init; } = "";
    public string LastPaymentDate { get; init; } = "";
}
