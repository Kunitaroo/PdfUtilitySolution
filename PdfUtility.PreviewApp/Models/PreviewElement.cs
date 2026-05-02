namespace PdfUtility.PreviewApp.Models;

public enum PreviewElementType { Text, Rectangle, Image }

public class PreviewElement
{
    public PreviewElementType Type { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Text { get; set; } = string.Empty;
    public double FontSize { get; set; } = 10;
    public string? ImagePath { get; set; }

    public string DisplayName => Type switch
    {
        PreviewElementType.Text      => $"[T] ({X:F0},{Y:F0}) \"{(Text.Length > 10 ? Text[..10] + "…" : Text)}\"",
        PreviewElementType.Rectangle => $"[□] ({X:F0},{Y:F0}) {Width:F0}×{Height:F0}",
        PreviewElementType.Image     => $"[画] ({X:F0},{Y:F0}) {Width:F0}×{Height:F0}",
        _ => ""
    };
}
