
using DiktaMe.Core.Pipeline;
using Microsoft.UI.Xaml.Data;

namespace DiktaMe.App.Converters;
public sealed class PipelineStateToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is PipelineState state ? state switch
        {
            PipelineState.Idle => "READY",
            PipelineState.Recording => "LISTENING",
            PipelineState.Transcribing => "TRANSCRIBING",
            PipelineState.Processing => "THINKING",
            PipelineState.Injecting => "TYPING",
            PipelineState.Error => "ERROR",
            _ => "READY",
        } : "READY";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
