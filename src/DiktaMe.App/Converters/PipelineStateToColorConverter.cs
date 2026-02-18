namespace DiktaMe.App.Converters;

using DiktaMe.Core.Pipeline;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

public sealed class PipelineStateToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush Green = new(ColorHelper.FromArgb(255, 74, 222, 128));
    private static readonly SolidColorBrush Red = new(ColorHelper.FromArgb(255, 248, 113, 113));
    private static readonly SolidColorBrush Amber = new(ColorHelper.FromArgb(255, 251, 191, 36));
    private static readonly SolidColorBrush Blue = new(ColorHelper.FromArgb(255, 96, 165, 250));
    private static readonly SolidColorBrush Purple = new(ColorHelper.FromArgb(255, 167, 139, 250));
    private static readonly SolidColorBrush BrightRed = new(ColorHelper.FromArgb(255, 239, 68, 68));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is PipelineState state ? state switch
        {
            PipelineState.Idle => Green,
            PipelineState.Recording => Red,
            PipelineState.Transcribing => Amber,
            PipelineState.Processing => Blue,
            PipelineState.Injecting => Purple,
            PipelineState.Error => BrightRed,
            _ => Green,
        } : Green;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
