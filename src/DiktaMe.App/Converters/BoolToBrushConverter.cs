
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace DiktaMe.App.Converters;
public sealed class BoolToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush TrueBrush = new(ColorHelper.FromArgb(255, 74, 222, 128));
    private static readonly SolidColorBrush FalseBrush = new(ColorHelper.FromArgb(0, 0, 0, 0));

    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? TrueBrush : FalseBrush;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
