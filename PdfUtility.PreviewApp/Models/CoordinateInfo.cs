namespace PdfUtility.PreviewApp.Models;

public class CoordinateInfo
{
    public double X { get; set; }
    public double Y { get; set; }
    public int PageNumber { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool HasSelection => Width > 0 && Height > 0;
}
