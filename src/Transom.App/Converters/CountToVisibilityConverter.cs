using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Transom.App.Converters;

/// <summary>Hides the image tray's ScrollViewer entirely when there are no images, so it doesn't
/// reserve empty space above the character count.</summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}