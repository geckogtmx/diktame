using System.Globalization;

namespace DiktaMe.Core.Vision.Annotations;

/// <summary>The type of annotation drawn on a screenshot.</summary>
public enum AnnotationType
{
    Arrow,
    Rectangle,
    Ellipse,
    Freehand,
    Text,
    Step,
    Highlight,
    Blur,
}

/// <summary>
/// A single annotation drawn on a captured screenshot.
/// Each annotation knows how to describe itself for AI context injection.
/// </summary>
public interface IAnnotation
{
    AnnotationType Type { get; }
    byte ColorR { get; }
    byte ColorG { get; }
    byte ColorB { get; }
    double StrokeWidth { get; }

    /// <summary>
    /// Returns a human-readable description of this annotation for injection
    /// into an AI system prompt. E.g. "Arrow pointing from (150,200) to (400,200)".
    /// </summary>
    string ToPromptContext();
}

public sealed record ArrowAnnotation(
    double StartX, double StartY,
    double EndX, double EndY,
    byte ColorR, byte ColorG, byte ColorB,
    double StrokeWidth) : IAnnotation
{
    public AnnotationType Type => AnnotationType.Arrow;
    public string ToPromptContext() =>
        string.Format(CultureInfo.InvariantCulture,
            "Arrow pointing from ({0:F0},{1:F0}) to ({2:F0},{3:F0})",
            StartX, StartY, EndX, EndY);
}

public sealed record RectAnnotation(
    double X, double Y, double Width, double Height,
    byte ColorR, byte ColorG, byte ColorB,
    double StrokeWidth, bool Filled) : IAnnotation
{
    public AnnotationType Type => AnnotationType.Rectangle;
    public string ToPromptContext() =>
        string.Format(CultureInfo.InvariantCulture,
            "Rectangle highlighting region ({0:F0},{1:F0}) to ({2:F0},{3:F0})",
            X, Y, X + Width, Y + Height);
}

public sealed record EllipseAnnotation(
    double CenterX, double CenterY,
    double RadiusX, double RadiusY,
    byte ColorR, byte ColorG, byte ColorB,
    double StrokeWidth) : IAnnotation
{
    public AnnotationType Type => AnnotationType.Ellipse;
    public string ToPromptContext() =>
        string.Format(CultureInfo.InvariantCulture,
            "Ellipse circling area around ({0:F0},{1:F0})",
            CenterX, CenterY);
}

public sealed record FreehandAnnotation(
    IReadOnlyList<(double X, double Y)> Points,
    byte ColorR, byte ColorG, byte ColorB,
    double StrokeWidth) : IAnnotation
{
    public AnnotationType Type => AnnotationType.Freehand;
    public string ToPromptContext() =>
        Points.Count > 0
            ? string.Format(CultureInfo.InvariantCulture,
                "Freehand stroke near ({0:F0},{1:F0}), {2} points",
                Points[0].X, Points[0].Y, Points.Count)
            : "Freehand stroke (empty)";
}

public sealed record TextAnnotation(
    double X, double Y, string Content,
    byte ColorR, byte ColorG, byte ColorB,
    double FontSize) : IAnnotation
{
    public AnnotationType Type => AnnotationType.Text;
    public double StrokeWidth => 0;
    public string ToPromptContext() =>
        string.Format(CultureInfo.InvariantCulture,
            "Text label \"{0}\" at ({1:F0},{2:F0})",
            Content, X, Y);
}

public sealed record StepAnnotation(
    double X, double Y, int Number,
    byte ColorR, byte ColorG, byte ColorB) : IAnnotation
{
    public AnnotationType Type => AnnotationType.Step;
    public double StrokeWidth => 2;
    public string ToPromptContext() =>
        string.Format(CultureInfo.InvariantCulture,
            "Step {0} marked at ({1:F0},{2:F0})",
            Number, X, Y);
}

public sealed record HighlightAnnotation(
    double X, double Y, double Width, double Height,
    byte ColorR, byte ColorG, byte ColorB) : IAnnotation
{
    public AnnotationType Type => AnnotationType.Highlight;
    public double StrokeWidth => 0;
    public string ToPromptContext() =>
        string.Format(CultureInfo.InvariantCulture,
            "Highlighted region ({0:F0},{1:F0}) to ({2:F0},{3:F0})",
            X, Y, X + Width, Y + Height);
}

public sealed record BlurAnnotation(
    double X, double Y, double Width, double Height,
    int PixelSize = 10) : IAnnotation
{
    public AnnotationType Type => AnnotationType.Blur;
    public byte ColorR => 128;
    public byte ColorG => 128;
    public byte ColorB => 128;
    public double StrokeWidth => 0;
    public string ToPromptContext() =>
        string.Format(CultureInfo.InvariantCulture,
            "Region blurred/redacted at ({0:F0},{1:F0}) to ({2:F0},{3:F0}) — likely contains PII",
            X, Y, X + Width, Y + Height);
}

/// <summary>Utility for assembling annotation context into AI system prompt.</summary>
public static class AnnotationContext
{
    public static string BuildPromptContext(IReadOnlyList<IAnnotation> annotations)
    {
        if (annotations.Count == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>(annotations.Count + 2)
        {
            "The user has annotated this screenshot with the following markups:",
        };

        foreach (var a in annotations)
        {
            lines.Add($"- {a.ToPromptContext()}");
        }

        lines.Add("Focus your analysis on the annotated areas.");
        return string.Join('\n', lines);
    }
}
