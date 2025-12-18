using Avalonia.Controls;
using SubscriptionTracker.AvaloniaApp.Models;
using System;
using System.Collections.ObjectModel;

namespace SubscriptionTracker.AvaloniaApp.Views;

public partial class MembersTab : UserControl
{
    private readonly ObservableCollection<MemberSearchItem> _results = new();
private const int PageSize = 50;
private int _offset = 0;
private string _lastQuery = "";

    public MembersTab()
    {
        InitializeComponent();
PrevBtn.Click += (_, __) =>
{
    _offset = Math.Max(0, _offset - PageSize);
    LoadSearchPage();
};

NextBtn.Click += (_, __) =>
{
    _offset += PageSize;
    LoadSearchPage();
};
        ResultsList.ItemsSource = _results;
        ResultsList.SelectionChanged += ResultsList_SelectionChanged;

        SearchBtn.Click += SearchBtn_Click;
        NewBtn.Click += (_, __) => ResetNew();

        EditDateCheck.IsCheckedChanged += (_, __) =>
        {
            DatePicker.IsEnabled = EditDateCheck.IsChecked == true;
        };

        SaveBtn.Click += SaveBtn_Click;
        UpdateBtn.Click += UpdateBtn_Click;

        ResetNew();
    }

    private Window OwnerWindow() => (Window)VisualRoot!;

    private void ResetNew()
    {
        _results.Clear();
        ResultsList.SelectedItem = null;

        IdBox.Text = Guid.NewGuid().ToString("N");
        NameBox.Text = "";
        SearchNameBox.Text = "";

        DatePicker.SelectedDate = DateTime.Today;
        EditDateCheck.IsChecked = false;
        DatePicker.IsEnabled = false;

        MonthlyRb.IsChecked = true;
        NameBox.Focus();
    }

    private string SelectedPlan() => WeeklyRb.IsChecked == true ? "Weekly" : "Monthly";

    private void SetPlan(string plan)
    {
        if (string.Equals(plan, "Weekly", StringComparison.OrdinalIgnoreCase))
        {
            WeeklyRb.IsChecked = true;
            MonthlyRb.IsChecked = false;
        }
        else
        {
            MonthlyRb.IsChecked = true;
            WeeklyRb.IsChecked = false;
        }
    }
private async void SearchBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
{
    var q = (SearchNameBox.Text ?? "").Trim();
    if (string.IsNullOrWhiteSpace(q))
    {
        await Ui.Msg(OwnerWindow(), "Enter part of a name to search.");
        return;
    }

    _lastQuery = q;
    _offset = 0;
    LoadSearchPage();
}
private void LoadSearchPage()
{
    _results.Clear();
    ResultsList.SelectedItem = null;

    using var con = Db.Open();
    using var cmd = con.CreateCommand();

    // fetch one extra row to detect "has next page"
    cmd.CommandText = @"
SELECT MemberId, Name
FROM Members
WHERE Name LIKE @q COLLATE NOCASE
ORDER BY Name
LIMIT @lim OFFSET @off;";
    cmd.Parameters.AddWithValue("@q", $"%{_lastQuery}%");
    cmd.Parameters.AddWithValue("@lim", PageSize + 1);
    cmd.Parameters.AddWithValue("@off", _offset);

    using var r = cmd.ExecuteReader();

    int count = 0;
    while (r.Read())
    {
        if (count < PageSize)
        {
            _results.Add(new Models.MemberSearchItem
            {
                MemberId = r.GetString(0),
                Name = r.GetString(1)
            });
        }
        count++;
    }

    bool hasNext = count > PageSize;
    bool hasPrev = _offset > 0;

    PrevBtn.IsEnabled = hasPrev;
    NextBtn.IsEnabled = hasNext;

    var pageNo = (_offset / PageSize) + 1;
    PageText.Text = $"Page {pageNo} (showing {_results.Count})";
}

    private async void ResultsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ResultsList.SelectedItem is not MemberSearchItem item)
            return;

        using var con = Db.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT MemberId, Name, JoinDate, Plan FROM Members WHERE MemberId=@i LIMIT 1;";
        cmd.Parameters.AddWithValue("@i", item.MemberId);

        using var r = cmd.ExecuteReader();
        if (!r.Read())
        {
            await Ui.Msg(OwnerWindow(), "Selected member not found.");
            return;
        }

        var id = r.GetString(0);
        var name = r.GetString(1);
        var joinDate = r.GetString(2);
        var plan = r.GetString(3);

        IdBox.Text = id;
        NameBox.Text = name;

        if (DateTime.TryParse(joinDate, out var dt))
            DatePicker.SelectedDate = dt.Date;
        else
            DatePicker.SelectedDate = DateTime.Today;

        SetPlan(plan);
    }

    private async void SaveBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var name = (NameBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            await Ui.Msg(OwnerWindow(), "Name is required.");
            return;
        }

        var key = NameKeyUtil.HashNormalized(name);
        var id = (IdBox.Text ?? "").Trim();
        var plan = SelectedPlan();
        var date = (DatePicker.SelectedDate ?? DateTime.Today).ToString("yyyy-MM-dd");

        using var con = Db.Open();

        using (var chk = con.CreateCommand())
        {
            chk.CommandText = "SELECT Name FROM Members WHERE NameKey=@k LIMIT 1;";
            chk.Parameters.AddWithValue("@k", key);
            var existing = chk.ExecuteScalar() as string;
            if (!string.IsNullOrEmpty(existing))
            {
                await Ui.Msg(OwnerWindow(), $"Name already exists (case-insensitive).\n\nExisting: {existing}\nEntered:  {name}");
                return;
            }
        }

        using var tx = con.BeginTransaction();
        try
        {
            using (var cmd = con.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT INTO Members(MemberId,Name,JoinDate,Plan,NameKey)
                                    VALUES(@i,@n,@d,@p,@k);";
                cmd.Parameters.AddWithValue("@i", id);
                cmd.Parameters.AddWithValue("@n", name);
                cmd.Parameters.AddWithValue("@d", date);
                cmd.Parameters.AddWithValue("@p", plan);
                cmd.Parameters.AddWithValue("@k", key);
                cmd.ExecuteNonQuery();
            }

            using (var cmd = con.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT INTO Payments(MemberId,PaymentDate) VALUES(@i,@d);";
                cmd.Parameters.AddWithValue("@i", id);
                cmd.Parameters.AddWithValue("@d", date);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
            await Ui.Msg(OwnerWindow(), "Saved new member.");
            ResetNew();
        }
        catch (Exception ex)
        {
            tx.Rollback();
            await Ui.Msg(OwnerWindow(), "Error: " + ex.Message);
        }
    }

    private async void UpdateBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var memberId = (IdBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(memberId))
        {
            await Ui.Msg(OwnerWindow(), "MemberId missing. Select a member from Matches first.");
            return;
        }

        var newName = (NameBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            await Ui.Msg(OwnerWindow(), "Name is required.");
            return;
        }

        var newKey = NameKeyUtil.HashNormalized(newName);
        var plan = SelectedPlan();

        using var con = Db.Open();

        using (var chk = con.CreateCommand())
        {
            chk.CommandText = "SELECT MemberId, Name FROM Members WHERE NameKey=@k LIMIT 1;";
            chk.Parameters.AddWithValue("@k", newKey);
            using var r = chk.ExecuteReader();
            if (r.Read())
            {
                var existingId = r.GetString(0);
                var existingName = r.GetString(1);
                if (!string.Equals(existingId, memberId, StringComparison.OrdinalIgnoreCase))
                {
                    await Ui.Msg(OwnerWindow(), $"Cannot update. Another member already uses this name:\n\n{existingName}");
                    return;
                }
            }
        }

        string? joinDate = null;
        if (EditDateCheck.IsChecked == true)
            joinDate = (DatePicker.SelectedDate ?? DateTime.Today).ToString("yyyy-MM-dd");

        using var cmd = con.CreateCommand();
        cmd.CommandText = joinDate is null
            ? "UPDATE Members SET Name=@n, NameKey=@k, Plan=@p WHERE MemberId=@i;"
            : "UPDATE Members SET Name=@n, NameKey=@k, Plan=@p, JoinDate=@d WHERE MemberId=@i;";
        cmd.Parameters.AddWithValue("@n", newName);
        cmd.Parameters.AddWithValue("@k", newKey);
        cmd.Parameters.AddWithValue("@p", plan);
        if (joinDate is not null) cmd.Parameters.AddWithValue("@d", joinDate);
        cmd.Parameters.AddWithValue("@i", memberId);

        var rows = cmd.ExecuteNonQuery();
        await Ui.Msg(OwnerWindow(), rows > 0 ? "Member updated." : "Member not found (MemberId).");
    }
}
