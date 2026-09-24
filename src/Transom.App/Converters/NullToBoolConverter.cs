using Microsoft.UI.Xaml.Data;

namespace Transom.App.Converters;

/// <summary>True when the bound value is a non-null, non-empty string — used to drive an
/// InfoBar's IsOpen from a nullable status message.</summary>
public sealed class NullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => !string.IsNullOrEmpty(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}