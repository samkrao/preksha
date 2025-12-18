using Avalonia.Controls;
using SubscriptionTracker.AvaloniaApp.Models;
using System;
using System.Collections.ObjectModel;

namespace SubscriptionTracker.AvaloniaApp.Views;

public partial class ReportsTab : UserControl
{
    private readonly ObservableCollection<ReportRow> _rows = new();

    public ReportsTab()
    {
        InitializeComponent();
        ClearSearchBtn.Click += (_, __) =>
        {
            SearchBox.Text = "";
            LoadReport();
        };

        Grid.ItemsSource = _rows;

        RefreshBtn.Click += (_, __) => LoadReport();
        ViewCombo.SelectionChanged += (_, __) => LoadReport();
        SearchBox.TextChanged += (_, __) => LoadReport();

        PaySelectedBtn.Click += async (_, __) =>
        {
            if (Grid.SelectedItem is not ReportRow row)
            {
                await Ui.Msg(OwnerWindow(), "Select a row first.");
                return;
            }

            var today = DateTime.Today.ToString("yyyy-MM-dd");
            using var con = Db.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"INSERT INTO Payments(MemberId, PaymentDate) VALUES (@id, @date);";
            cmd.Parameters.AddWithValue("@id", row.MemberId);
            cmd.Parameters.AddWithValue("@date", today);
            cmd.ExecuteNonQuery();

            await Ui.Msg(OwnerWindow(), "Payment recorded.");
            LoadReport();
        };

        LoadReport();
    }

    private Window OwnerWindow() => (Window)VisualRoot!;

    private bool IsAllMode() => ViewCombo.SelectedIndex == 1;

    private async void LoadReport()
    {
        try
        {
            _rows.Clear();

            var allMode = IsAllMode();
            var search = (SearchBox.Text ?? "").Trim();

            using var con = Db.Open();
            using var cmd = con.CreateCommand();

            var whereName = "";
            if (!string.IsNullOrWhiteSpace(search))
            {
                whereName = " AND m.Name LIKE @q ";
                cmd.Parameters.AddWithValue("@q", $"%{search}%");
            }

            if (!allMode)
            {
                cmd.CommandText = $@"
SELECT m.MemberId, m.Name, m.Plan, MAX(p.PaymentDate) AS LastPaymentDate
FROM Members m
LEFT JOIN Payments p ON p.MemberId = m.MemberId
WHERE 1=1 {whereName}
GROUP BY m.MemberId, m.Name, m.Plan
HAVING
  (m.Plan = 'Weekly'  AND COALESCE(MAX(p.PaymentDate),'0000-01-01') <= date('now','-7 day'))
  OR
  (m.Plan = 'Monthly' AND COALESCE(MAX(p.PaymentDate),'0000-01-01') <= date('now','-1 month'))
ORDER BY LastPaymentDate ASC, m.Name ASC;";
            }
            else
            {
                cmd.CommandText = $@"
SELECT m.MemberId, m.Name, m.Plan, COALESCE(MAX(p.PaymentDate), '') AS LastPaymentDate
FROM Members m
LEFT JOIN Payments p ON p.MemberId = m.MemberId
WHERE 1=1 {whereName}
GROUP BY m.MemberId, m.Name, m.Plan
ORDER BY m.Name ASC;";
            }

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                _rows.Add(new ReportRow
                {
                    MemberId = r.GetString(0),
                    Name = r.GetString(1),
                    Plan = r.GetString(2),
                    LastPaymentDate = r.GetString(3)
                });
            }

            StatusText.Text = $"Mode={(allMode ? "All" : "Payment Due")}, Rows={_rows.Count}, DB={Db.CurrentDbPath}";
        }
        catch (Exception ex)
        {
            StatusText.Text = "ERROR: " + ex.Message;
            await Ui.Msg(OwnerWindow(), "Report load failed:\n" + ex);
        }
    }
}
