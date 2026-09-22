using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Transom.App.Converters;

/// <summary>Converts a URL string to an ImageSource. WinRT rejects an unconverted string for an
/// ImageSource-typed x:Bind target (e.g. PersonPicture.ProfilePicture) with ArgumentException —
/// there is no implicit string→ImageSource conversion, unlike bool→Visibility or numeric→string.
/// Null, empty, or invalid URLs convert to null (PersonPicture handles a null ProfilePicture
/// fine — it falls back to initials/a placeholder).</summary>
public sealed class UrlToImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string url || string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return new BitmapImage(uri);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}