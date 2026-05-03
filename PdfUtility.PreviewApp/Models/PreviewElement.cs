using System.ComponentModel;

namespace PdfUtility.PreviewApp.Models;

public enum PreviewElementType { Text, Rectangle, Image }

public class PreviewElement : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private double _x;
    private double _y;

    public PreviewElementType Type { get; set; }

    public double X
    {
        get => _x;
        set
        {
            _x = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(X)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
        }
    }

    public double Y
    {
        get => _y;
        set
        {
            _y = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Y)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
        }
    }

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
