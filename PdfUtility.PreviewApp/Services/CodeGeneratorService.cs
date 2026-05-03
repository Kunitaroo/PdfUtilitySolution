using PdfUtility.PreviewApp.Models;
using System.Collections.Generic;
using System.Linq;

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

    public string GenerateFromElement(PreviewElement element, int pageNumber, CodeLanguage language)
    {
        return language switch
        {
            CodeLanguage.CSharp => GenerateElementCSharp(element, pageNumber),
            CodeLanguage.VbNet  => GenerateElementVbNet(element, pageNumber),
            _                   => string.Empty
        };
    }

    public string GenerateAllElements(IEnumerable<PreviewElement> elements, int pageNumber, CodeLanguage language)
    {
        var list = elements.ToList();
        if (list.Count == 0) return string.Empty;

        return language switch
        {
            CodeLanguage.CSharp => GenerateAllCSharp(list, pageNumber),
            CodeLanguage.VbNet  => GenerateAllVbNet(list, pageNumber),
            _                   => string.Empty
        };
    }

    // ===== 座標クリック生成 =====

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

    // ===== プレビュー要素単体生成 =====

    private string GenerateElementCSharp(PreviewElement element, int pageNumber)
    {
        return element.Type switch
        {
            PreviewElementType.Text =>
$@"new TextDrawCommand
{{
    PageNumber = {pageNumber},
    X = {element.X:F1},
    Y = {element.Y:F1},
    Text = ""{element.Text}"",
    FontName = ""MS明朝"",
    FontSize = {element.FontSize:F0},
    FontColor = PdfColor.Black,
    HorizontalAlign = PdfHorizontalAlign.Left
}};",
            PreviewElementType.Rectangle =>
$@"new RectangleDrawCommand
{{
    PageNumber = {pageNumber},
    X = {element.X:F1},
    Y = {element.Y:F1},
    Width = {element.Width:F1},
    Height = {element.Height:F1},
    BorderColor = PdfColor.Black,
    BorderWidth = 1.0
}};",
            PreviewElementType.Image =>
$@"new ImageDrawCommand
{{
    PageNumber = {pageNumber},
    X = {element.X:F1},
    Y = {element.Y:F1},
    Width = {element.Width:F1},
    Height = {element.Height:F1},
    ImagePath = @""{element.ImagePath ?? ""}""
}};",
            _ => string.Empty
        };
    }

    private string GenerateElementVbNet(PreviewElement element, int pageNumber)
    {
        return element.Type switch
        {
            PreviewElementType.Text =>
$@"New TextDrawCommand With {{
    .PageNumber = {pageNumber},
    .X = {element.X:F1},
    .Y = {element.Y:F1},
    .Text = ""{element.Text}"",
    .FontName = ""MS明朝"",
    .FontSize = {element.FontSize:F0},
    .FontColor = PdfColor.Black,
    .HorizontalAlign = PdfHorizontalAlign.Left
}}",
            PreviewElementType.Rectangle =>
$@"New RectangleDrawCommand With {{
    .PageNumber = {pageNumber},
    .X = {element.X:F1},
    .Y = {element.Y:F1},
    .Width = {element.Width:F1},
    .Height = {element.Height:F1},
    .BorderColor = PdfColor.Black,
    .BorderWidth = 1.0
}}",
            PreviewElementType.Image =>
$@"New ImageDrawCommand With {{
    .PageNumber = {pageNumber},
    .X = {element.X:F1},
    .Y = {element.Y:F1},
    .Width = {element.Width:F1},
    .Height = {element.Height:F1},
    .ImagePath = ""{element.ImagePath ?? ""}""
}}",
            _ => string.Empty
        };
    }

    // ===== 全要素まとめ生成 =====

    private string GenerateAllCSharp(List<PreviewElement> elements, int pageNumber)
    {
        var items = elements.Select(e => IndentBlock(GenerateElementCSharpInner(e, pageNumber), "    "));
        return $"var commands = new List<PdfDrawCommand>\n{{\n{string.Join(",\n", items)}\n}};";
    }

    private string GenerateAllVbNet(List<PreviewElement> elements, int pageNumber)
    {
        var items = elements.Select(e => IndentBlock(GenerateElementVbNetInner(e, pageNumber), "    "));
        return $"Dim commands As New List(Of PdfDrawCommand) From {{\n{string.Join(",\n", items)}\n}}";
    }

    private string GenerateElementCSharpInner(PreviewElement element, int pageNumber)
    {
        return element.Type switch
        {
            PreviewElementType.Text =>
$@"new TextDrawCommand
{{
    PageNumber = {pageNumber},
    X = {element.X:F1},
    Y = {element.Y:F1},
    Text = ""{element.Text}"",
    FontName = ""MS明朝"",
    FontSize = {element.FontSize:F0},
    FontColor = PdfColor.Black,
    HorizontalAlign = PdfHorizontalAlign.Left
}}",
            PreviewElementType.Rectangle =>
$@"new RectangleDrawCommand
{{
    PageNumber = {pageNumber},
    X = {element.X:F1},
    Y = {element.Y:F1},
    Width = {element.Width:F1},
    Height = {element.Height:F1},
    BorderColor = PdfColor.Black,
    BorderWidth = 1.0
}}",
            PreviewElementType.Image =>
$@"new ImageDrawCommand
{{
    PageNumber = {pageNumber},
    X = {element.X:F1},
    Y = {element.Y:F1},
    Width = {element.Width:F1},
    Height = {element.Height:F1},
    ImagePath = @""{element.ImagePath ?? ""}""
}}",
            _ => string.Empty
        };
    }

    private string GenerateElementVbNetInner(PreviewElement element, int pageNumber)
    {
        return element.Type switch
        {
            PreviewElementType.Text =>
$@"New TextDrawCommand With {{
    .PageNumber = {pageNumber},
    .X = {element.X:F1},
    .Y = {element.Y:F1},
    .Text = ""{element.Text}"",
    .FontName = ""MS明朝"",
    .FontSize = {element.FontSize:F0},
    .FontColor = PdfColor.Black,
    .HorizontalAlign = PdfHorizontalAlign.Left
}}",
            PreviewElementType.Rectangle =>
$@"New RectangleDrawCommand With {{
    .PageNumber = {pageNumber},
    .X = {element.X:F1},
    .Y = {element.Y:F1},
    .Width = {element.Width:F1},
    .Height = {element.Height:F1},
    .BorderColor = PdfColor.Black,
    .BorderWidth = 1.0
}}",
            PreviewElementType.Image =>
$@"New ImageDrawCommand With {{
    .PageNumber = {pageNumber},
    .X = {element.X:F1},
    .Y = {element.Y:F1},
    .Width = {element.Width:F1},
    .Height = {element.Height:F1},
    .ImagePath = ""{element.ImagePath ?? ""}""
}}",
            _ => string.Empty
        };
    }

    private static string IndentBlock(string block, string indent)
    {
        var lines = block.Split('\n');
        return string.Join("\n", lines.Select(l => indent + l));
    }
}
