using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia;

namespace SubscriptionTracker.AvaloniaApp.Views;

public static class Ui
{
    public static async System.Threading.Tasks.Task Msg(Window owner, string text, string title = "Info")
    {
        var w = new Window
        {
            Width = 580,
            Height = 260,
            Title = title,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var b = new Button { Content = "OK", Width = 90, HorizontalAlignment = HorizontalAlignment.Center };
        b.Click += (_, __) => w.Close();

        w.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
                b
            }
        };

        await w.ShowDialog(owner);
    }

    public static async Task<bool> Confirm(Window owner, string text)
{
    var dlg = new Window
    {
        Title = "Confirm",
        Width = 380,
        Height = 150,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        CanResize = false
    };

    var yesBtn = new Button { Content = "Yes", Width = 90 };
    var noBtn  = new Button { Content = "No",  Width = 90 };

    bool result = false;

    yesBtn.Click += (_, __) => { result = true; dlg.Close(); };
    noBtn.Click  += (_, __) => { result = false; dlg.Close(); };

    dlg.Content = new StackPanel
    {
        Margin = new Thickness(16),
        Spacing = 12,
        Children =
        {
            new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Children = { yesBtn, noBtn }
            }
        }
    };

    await dlg.ShowDialog(owner);
    return result;
}

}
