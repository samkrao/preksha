using Avalonia.Controls;
using SubscriptionTracker.AvaloniaApp.Models;
using System;
using System.Collections.ObjectModel;

namespace SubscriptionTracker.AvaloniaApp.Views;

public partial class AppointmentsTab : UserControl
{
    private readonly ObservableCollection<MemberSearchItem> _matches = new();
    private readonly ObservableCollection<AppointmentRow> _today = new();
private const int ApptPageSize = 50;
private int _apptOffset = 0;
private string _apptLastQuery = "";

    private MemberSearchItem? _selected;

    public AppointmentsTab()
    {
        InitializeComponent();
        ApptPrevBtn.Click += (_, __) =>
        {
            _apptOffset = Math.Max(0, _apptOffset - ApptPageSize);
            LoadMemberSearchPage();
        };

        ApptNextBtn.Click += (_, __) =>
        {
            _apptOffset += ApptPageSize;
            LoadMemberSearchPage();
        };

        TodayGrid.SelectionChanged += (_, __) =>
            {
                if (TodayGrid.SelectedItem is not SubscriptionTracker.AvaloniaApp.Models.AppointmentRow r)
                    return;

                ApptDatePicker.SelectedDate = DateTime.Today; // appointments grid shows today only

                if (TimeSpan.TryParse(r.Time, out var ts))
                    ApptTimePicker.SelectedTime = ts;
            };

        ApptResultsList.ItemsSource = _matches;
        ApptResultsList.SelectionChanged += ApptResultsList_SelectionChanged;

        TodayGrid.ItemsSource = _today;

        ApptSearchBtn.Click += (_, __) => SearchMembers();
        AddApptBtn.Click += async (_, __) => await AddAppointment();
        //ShowTodayBtn.Click += (_, __) => LoadToday();
        ApptClearBtn.Click += (_, __) =>
        {
            ApptSearchBox.Text = "";
            _matches.Clear();
            ApptResultsList.SelectedItem = null;
            _selected = null;
            SelectedMemberText.Text = "";
            ApptStatusText.Text = "";
        };


        ApptDatePicker.SelectedDate = DateTime.Today;
        ApptTimePicker.SelectedTime = TimeSpan.FromHours(DateTime.Now.Hour);

        LoadToday();

        RangeDatePicker.SelectedDate = DateTime.Today;

        RefreshRangeBtn.Click += (_, __) => LoadRange();
        RangeCombo.SelectionChanged += (_, __) => LoadRange();
        RangeDatePicker.SelectedDateChanged += (_, __) => LoadRange();

        LoadRange();
    }

    private Window OwnerWindow() => (Window)VisualRoot!;

    private async void SearchMembers()
    {
        var q = (ApptSearchBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(q))
        {
            await Ui.Msg(OwnerWindow(), "Type part of a member name first.");
            return;
        }

        _apptLastQuery = q;
        _apptOffset = 0;
        LoadMemberSearchPage();
    }
    private void LoadMemberSearchPage()
    {
        _matches.Clear();
        _selected = null;
        SelectedMemberText.Text = "";

        using var con = Db.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
    SELECT MemberId, Name
    FROM Members
    WHERE Name LIKE @q COLLATE NOCASE
    ORDER BY Name
    LIMIT @lim OFFSET @off;";
        cmd.Parameters.AddWithValue("@q", $"%{_apptLastQuery}%");
        cmd.Parameters.AddWithValue("@lim", ApptPageSize + 1);
        cmd.Parameters.AddWithValue("@off", _apptOffset);

        using var r = cmd.ExecuteReader();

        int count = 0;
        while (r.Read())
        {
            if (count < ApptPageSize)
            {
                _matches.Add(new Models.MemberSearchItem
                {
                    MemberId = r.GetString(0),
                    Name = r.GetString(1)
                });
            }
            count++;
        }

        bool hasNext = count > ApptPageSize;
        bool hasPrev = _apptOffset > 0;

        ApptPrevBtn.IsEnabled = hasPrev;
        ApptNextBtn.IsEnabled = hasNext;

        var pageNo = (_apptOffset / ApptPageSize) + 1;
        ApptPageText.Text = $"Page {pageNo} (showing {_matches.Count})";
    }


    private void ApptResultsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ApptResultsList.SelectedItem is not MemberSearchItem item) return;
        _selected = item;
        SelectedMemberText.Text = item.Name;
    }

    private async System.Threading.Tasks.Task AddAppointment()
    {
        if (_selected is null)
        {
            await Ui.Msg(OwnerWindow(), "Select a member from Matches first.");
            return;
        }

        var d = ApptDatePicker.SelectedDate ?? DateTime.Today;
        var t = ApptTimePicker.SelectedTime ?? TimeSpan.Zero;
        var apptAt = new DateTime(d.Year, d.Month, d.Day, t.Hours, t.Minutes, 0);
        var apptText = apptAt.ToString("yyyy-MM-dd HH:mm");

        using var con = Db.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "INSERT INTO Appointments(MemberId, ApptAt) VALUES(@id, @at);";
        cmd.Parameters.AddWithValue("@id", _selected.MemberId);
        cmd.Parameters.AddWithValue("@at", apptText);
        cmd.ExecuteNonQuery();

        await Ui.Msg(OwnerWindow(), $"Appointment saved for {_selected.Name} at {apptText}");
        LoadToday();
    }

    private void LoadToday()
    {
        _today.Clear();

        using var con = Db.Open();
        using var cmd = con.CreateCommand();

        cmd.CommandText = @"
WITH LastPay AS (
  SELECT MemberId, COALESCE(MAX(PaymentDate),'0000-01-01') AS LastPaymentDate
  FROM Payments
  GROUP BY MemberId
)
SELECT a.AppointmentId,
       strftime('%H:%M', a.ApptAt) as ApptTime,
       m.MemberId,
       m.Name,
       m.Plan,
       COALESCE(lp.LastPaymentDate,'') as LastPaymentDate,
       CASE
         WHEN m.Plan='Weekly'  AND lp.LastPaymentDate <= date('now','-7 day')  THEN 'DUE'
         WHEN m.Plan='Monthly' AND lp.LastPaymentDate <= date('now','-1 month') THEN 'DUE'
         ELSE 'OK'
       END as DueStatus
FROM Appointments a
JOIN Members m ON m.MemberId = a.MemberId
LEFT JOIN LastPay lp ON lp.MemberId = m.MemberId
WHERE date(a.ApptAt) = date('now')
ORDER BY a.ApptAt ASC;";

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            _today.Add(new AppointmentRow
            {
                AppointmentId = r.GetInt64(0),
                Time = r.GetString(1),
                MemberId = r.GetString(2),
                Name = r.GetString(3),
                Plan = r.GetString(4),
                LastPaymentDate = r.GetString(5),
                DueStatus = r.GetString(6)
            });
        }

        ApptStatusText.Text = $"Today's appointments: {_today.Count} | DB={Db.CurrentDbPath}";
    }

    private async void PaymentDone_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button btn)
            return;

        // The row is the DataContext of the button
        if (btn.DataContext is not SubscriptionTracker.AvaloniaApp.Models.AppointmentRow row)
            return;

        var today = DateTime.Today.ToString("yyyy-MM-dd");

        using var con = Db.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "INSERT INTO Payments(MemberId, PaymentDate) VALUES(@id, @d);";
        cmd.Parameters.AddWithValue("@id", row.MemberId);
        cmd.Parameters.AddWithValue("@d", today);
        cmd.ExecuteNonQuery();

        await Ui.Msg(OwnerWindow(), $"Payment recorded for {row.Name} on {today}");
        LoadToday(); // refresh Due/OK + LastPaymentDate in the grid
    }
    private async void UpdateAppt_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button btn)
            return;

        if (btn.DataContext is not SubscriptionTracker.AvaloniaApp.Models.AppointmentRow row)
            return;

        // Take new date+time from the pickers
        var d = ApptDatePicker.SelectedDate ?? DateTime.Today;
        var t = ApptTimePicker.SelectedTime ?? TimeSpan.Zero;
        var newAt = new DateTime(d.Year, d.Month, d.Day, t.Hours, t.Minutes, 0);
        var newAtText = newAt.ToString("yyyy-MM-dd HH:mm");

        using var con = Db.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE Appointments SET ApptAt=@at WHERE AppointmentId=@aid;";
        cmd.Parameters.AddWithValue("@at", newAtText);
        cmd.Parameters.AddWithValue("@aid", row.AppointmentId);

        var updated = cmd.ExecuteNonQuery();
        await Ui.Msg(OwnerWindow(),
            updated > 0
                ? $"Appointment updated for {row.Name} to {newAtText}"
                : "Update failed (appointment not found).");

        LoadToday(); // refresh grid
    }

    private void LoadRange()
    {
        _today.Clear();

        var selected = RangeDatePicker.SelectedDate ?? DateTime.Today;
        var isWeekly = RangeCombo.SelectedIndex == 1; // 0=Daily, 1=Weekly

        DateTime startDate = selected.Date;
        DateTime endDateExclusive;

        if (!isWeekly)
        {
            // Daily = selected day only
            endDateExclusive = startDate.AddDays(1);
        }
        else
        {
            // Weekly = next 7 days starting from selected date
            endDateExclusive = startDate.AddDays(7);
        }
        var mode = RangeCombo.SelectedIndex == 1 ? "Weekly" : "Daily"; // 0=Daily, 1=Weekly

     /*
        if (isWeekly == "Daily")
        {
            startDate = selected.Date;
            endDateExclusive = startDate.AddDays(1);
        }
        else
        {
            // Week = Monday to Sunday (change if you want Sunday-start)
            int diff = ((int)selected.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            startDate = selected.Date.AddDays(-diff);
            endDateExclusive = startDate.AddDays(7);
        }
*/
        var startText = startDate.ToString("yyyy-MM-dd");
        var endText = endDateExclusive.ToString("yyyy-MM-dd");

        using var con = Db.Open();
        using var cmd = con.CreateCommand();

        cmd.CommandText = @"
    WITH LastPay AS (
    SELECT MemberId, COALESCE(MAX(PaymentDate),'0000-01-01') AS LastPaymentDate
    FROM Payments
    GROUP BY MemberId
    )
    SELECT a.AppointmentId,
        a.ApptAt,
        m.MemberId,
        m.Name,
        m.Plan,
        COALESCE(lp.LastPaymentDate,'') as LastPaymentDate,
        CASE
            WHEN m.Plan='Weekly'  AND lp.LastPaymentDate <= date('now','-7 day')  THEN 'DUE'
            WHEN m.Plan='Monthly' AND lp.LastPaymentDate <= date('now','-1 month') THEN 'DUE'
            ELSE 'OK'
        END as DueStatus
    FROM Appointments a
    JOIN Members m ON m.MemberId = a.MemberId
    LEFT JOIN LastPay lp ON lp.MemberId = m.MemberId
    WHERE a.ApptAt >= @start AND a.ApptAt < @end
    ORDER BY a.ApptAt ASC;
    ";

        // ApptAt stored as "yyyy-MM-dd HH:mm" so string range works correctly
        cmd.Parameters.AddWithValue("@start", startText + " 00:00");
        cmd.Parameters.AddWithValue("@end", endText + " 00:00");

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            _today.Add(new AppointmentRow
            {
                AppointmentId = r.GetInt64(0),
                When = r.GetString(1),
                MemberId = r.GetString(2),
                Name = r.GetString(3),
                Plan = r.GetString(4),
                LastPaymentDate = r.GetString(5),
                DueStatus = r.GetString(6)
            });
        }

        ApptStatusText.Text =
        isWeekly
      ? $"Weekly (next 7 days) | {startText} → {endText} | Rows={_today.Count}"
      : $"Daily | {startText} | Rows={_today.Count}";

    }
    private async void CancelAppt_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button btn)
            return;

        if (btn.DataContext is not Models.AppointmentRow row)
            return;

        var confirm = await Ui.Confirm(
            OwnerWindow(),
            $"Cancel appointment for {row.Name} at {row.When} ?");

        if (!confirm)
            return;

        using var con = Db.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM Appointments WHERE AppointmentId=@id;";
        cmd.Parameters.AddWithValue("@id", row.AppointmentId);
        cmd.ExecuteNonQuery();

        LoadRange(); // refresh daily / next-7-days list
    }


}
