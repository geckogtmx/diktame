namespace DiktaMe.App.Converters;

using Microsoft.UI.Xaml.Data;

public sealed class ApiKeyMaskConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string key || key.Length < 8)
            return value?.ToString() ?? "";

        return $"{key[..4]}...{key[^4..]}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
