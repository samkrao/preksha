using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace SubscriptionTracker.AvaloniaApp.Converters;

public sealed class StringNotOkConverter : IValueConverter
{
    public static readonly StringNotOkConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s)
            return false;

        // Enable button only if status is NOT "OK"
        return !string.Equals(s, "OK", StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
