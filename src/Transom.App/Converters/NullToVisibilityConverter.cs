using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Transom.App.Converters;

/// <summary>Visible when the bound value is a non-null, non-empty string — used to show a
/// section only once its backing data has loaded.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}