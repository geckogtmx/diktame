namespace DiktaMe.App.Converters;

using DiktaMe.Core.Config;
using Microsoft.UI.Xaml.Data;

public sealed class PrivacyLevelToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is PrivacyLevel level ? level switch
        {
            PrivacyLevel.Ghost => "Ghost",
            PrivacyLevel.Stats => "Stats Only",
            PrivacyLevel.Balanced => "Balanced",
            PrivacyLevel.Full => "Full",
            _ => "Balanced",
        } : "Balanced";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
