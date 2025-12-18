namespace SubscriptionTracker.AvaloniaApp.Models;

public sealed class AppointmentRow
{
    public long AppointmentId { get; init; }
    public string Time { get; init; } = "";
    public string Name { get; init; } = "";
    public string Plan { get; init; } = "";
    public string LastPaymentDate { get; init; } = "";
    public string DueStatus { get; init; } = "";
    public string MemberId { get; init; } = "";
    public string When { get; init; } = "";   // yyyy-MM-dd HH:mm
}
