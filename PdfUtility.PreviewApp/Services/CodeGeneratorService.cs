using PdfUtility.PreviewApp.Models;

namespace PdfUtility.PreviewApp.Services;

public class CodeGeneratorService
{
    public string Generate(CoordinateInfo info, CodeLanguage language)
    {
        return language switch
        {
            CodeLanguage.CSharp => GenerateCSharp(info),
            CodeLanguage.VbNet  => GenerateVbNet(info),
            _                   => string.Empty
        };
    }

    private string GenerateCSharp(CoordinateInfo info)
    {
        if (info.HasSelection)
            return $@"new RectangleDrawCommand
{{
    PageNumber = {info.PageNumber},
    X = {info.X:F1},
    Y = {info.Y:F1},
    Width = {info.Width:F1},
    Height = {info.Height:F1},
    BorderColor = PdfColor.Black,
    BorderWidth = 1.0
}};";

        return $@"new TextDrawCommand
{{
    PageNumber = {info.PageNumber},
    X = {info.X:F1},
    Y = {info.Y:F1},
    FontName = ""MS明朝"",
    FontSize = 10,
    FontColor = PdfColor.Black,
    HorizontalAlign = PdfHorizontalAlign.Left
}};";
    }

    private string GenerateVbNet(CoordinateInfo info)
    {
        if (info.HasSelection)
            return $@"New RectangleDrawCommand With {{
    .PageNumber = {info.PageNumber},
    .X = {info.X:F1},
    .Y = {info.Y:F1},
    .Width = {info.Width:F1},
    .Height = {info.Height:F1},
    .BorderColor = PdfColor.Black,
    .BorderWidth = 1.0
}}";

        return $@"New TextDrawCommand With {{
    .PageNumber = {info.PageNumber},
    .X = {info.X:F1},
    .Y = {info.Y:F1},
    .FontName = ""MS明朝"",
    .FontSize = 10,
    .FontColor = PdfColor.Black,
    .HorizontalAlign = PdfHorizontalAlign.Left
}}";
    }
}
