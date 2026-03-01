
using DiktaMe.App.ViewModels;
using Microsoft.UI.Xaml.Data;

namespace DiktaMe.App.Converters;

/// <summary>
/// Converts <see cref="RefineMode"/> enum to display text for Control Panel toggle.
/// </summary>
public sealed class RefineModeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is RefineMode mode
            ? mode == RefineMode.Auto ? "AUTO" : "VOICE"
            : "VOICE";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
