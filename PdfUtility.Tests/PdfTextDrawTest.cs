using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfUtility.Core;
using PdfUtility.Core.Drawing;
using PdfUtility.Tests.Helpers;

namespace PdfUtility.Tests;

[TestClass]
public class PdfTextDrawTest
{
    private static string? _minchoPath;
    private static string? _gothicPath;
    private static byte[] _inputPdf = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        _minchoPath = TestHelper.GetFontPath("msmincho.ttc");
        _gothicPath = TestHelper.GetFontPath("msgothic.ttc");
        _inputPdf   = TestHelper.GetInputPdf();
    }

    // ── 1. MS明朝でテキスト描画テスト ────────────────────────────────────

    [TestMethod]
    [TestCategory("PdfText")]
    public void DrawText_MsMincho_OutputLargerThanInput()
    {
        TestHelper.SkipIfNotFound(_minchoPath, "msmincho.ttc");

        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new TextDrawCommand
        {
            PageNumber   = 1, X = 50, Y = 100,
            Text         = "MS明朝テスト：業務帳票システム",
            FontFilePath = _minchoPath!, TtcFontIndex = 0,
            FontSize     = 12, FontColor = PdfColor.Black
        });
        byte[] result = writer.BuildIncrementalUpdate();
        string outPath = TestHelper.OutputFile("text_mincho.pdf");
        File.WriteAllBytes(outPath, result);

        Assert.IsTrue(result.Length > _inputPdf.Length, "出力サイズ > 入力サイズ");
    }

    [TestMethod]
    [TestCategory("PdfText")]
    public void DrawText_MsMincho_OutputContainsFontStructures()
    {
        TestHelper.SkipIfNotFound(_minchoPath, "msmincho.ttc");

        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new TextDrawCommand
        {
            PageNumber   = 1, X = 50, Y = 100,
            Text         = "テスト",
            FontFilePath = _minchoPath!, TtcFontIndex = 0,
            FontSize     = 10
        });
        byte[] result = writer.BuildIncrementalUpdate();

        string incremental = System.Text.Encoding.GetEncoding(28591)
            .GetString(result, _inputPdf.Length, result.Length - _inputPdf.Length);

        Assert.IsTrue(incremental.Contains("/Type0"),
            "Type0 フォント辞書が含まれること");
        Assert.IsTrue(incremental.Contains("/ToUnicode"),
            "ToUnicode CMap が含まれること");
    }

    // ── 2. MSゴシックでテキスト描画テスト ──────────────────────────────

    [TestMethod]
    [TestCategory("PdfText")]
    public void DrawText_MsGothic_OutputLargerThanInput()
    {
        TestHelper.SkipIfNotFound(_gothicPath, "msgothic.ttc");

        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new TextDrawCommand
        {
            PageNumber   = 1, X = 50, Y = 150,
            Text         = "MSゴシックテスト：株式会社サンプル",
            FontFilePath = _gothicPath!, TtcFontIndex = 0,
            FontSize     = 12, FontColor = PdfColor.Black
        });
        byte[] result = writer.BuildIncrementalUpdate();
        File.WriteAllBytes(TestHelper.OutputFile("text_gothic.pdf"), result);

        Assert.IsTrue(result.Length > _inputPdf.Length, "出力サイズ > 入力サイズ");
    }

    [TestMethod]
    [TestCategory("PdfText")]
    public void DrawText_MsGothic_CanReopenAndCountPages()
    {
        TestHelper.SkipIfNotFound(_gothicPath, "msgothic.ttc");

        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new TextDrawCommand
        {
            PageNumber   = 1, X = 50, Y = 150,
            Text         = "MSゴシック再解析テスト",
            FontFilePath = _gothicPath!, TtcFontIndex = 0,
            FontSize     = 10
        });
        byte[] result = writer.BuildIncrementalUpdate();

        var verify = new PdfIncrementalWriter(result);
        Assert.AreEqual(writer.PageCount, verify.PageCount, "追記後もページ数が一致すること");
    }

    // ── 3. 複数フォントサイズテスト ──────────────────────────────────────

    [TestMethod]
    [TestCategory("PdfText")]
    [DataRow(8.0,  "8pt テキスト")]
    [DataRow(10.0, "10pt テキスト")]
    [DataRow(12.0, "12pt テキスト")]
    [DataRow(14.0, "14pt テキスト")]
    public void DrawText_MultipleFontSizes_Succeeds(double fontSize, string label)
    {
        TestHelper.SkipIfNotFound(_minchoPath, "msmincho.ttc");

        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new TextDrawCommand
        {
            PageNumber   = 1, X = 50, Y = 50 + fontSize * 3,
            Text         = $"{label}：日本語テキスト サンプル",
            FontFilePath = _minchoPath!, TtcFontIndex = 0,
            FontSize     = fontSize, FontColor = PdfColor.Black
        });
        byte[] result = writer.BuildIncrementalUpdate();

        Assert.IsTrue(result.Length > _inputPdf.Length,
            $"FontSize={fontSize}pt でも出力サイズ > 入力サイズ");
    }

    // ── 4. 太字テスト ──────────────────────────────────────────────────

    [TestMethod]
    [TestCategory("PdfText")]
    public void DrawText_IsBold_OutputContainsBoldOperators()
    {
        TestHelper.SkipIfNotFound(_minchoPath, "msmincho.ttc");

        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new TextDrawCommand
        {
            PageNumber   = 1, X = 50, Y = 100,
            Text         = "太字テスト Bold Text",
            FontFilePath = _minchoPath!, TtcFontIndex = 0,
            FontSize     = 12, IsBold = true
        });
        byte[] result = writer.BuildIncrementalUpdate();
        File.WriteAllBytes(TestHelper.OutputFile("text_bold.pdf"), result);

        string incremental = System.Text.Encoding.GetEncoding(28591)
            .GetString(result, _inputPdf.Length, result.Length - _inputPdf.Length);

        // IsBold=true のとき描画モード 2 (Tr) が含まれること
        Assert.IsTrue(incremental.Contains("2 Tr"), "太字シミュレーション用 Tr 演算子が含まれること");
        Assert.IsTrue(result.Length > _inputPdf.Length, "出力サイズ > 入力サイズ");
    }

    // ── 5. 水平配置テスト ───────────────────────────────────────────────

    [TestMethod]
    [TestCategory("PdfText")]
    public void DrawText_HorizontalAlign_Left_Succeeds()
    {
        TestHelper.SkipIfNotFound(_minchoPath, "msmincho.ttc");
        DrawTextWithAlign(PdfHorizontalAlign.Left,   "align_left.pdf");
    }

    [TestMethod]
    [TestCategory("PdfText")]
    public void DrawText_HorizontalAlign_Center_Succeeds()
    {
        TestHelper.SkipIfNotFound(_minchoPath, "msmincho.ttc");
        DrawTextWithAlign(PdfHorizontalAlign.Center, "align_center.pdf");
    }

    [TestMethod]
    [TestCategory("PdfText")]
    public void DrawText_HorizontalAlign_Right_Succeeds()
    {
        TestHelper.SkipIfNotFound(_minchoPath, "msmincho.ttc");
        DrawTextWithAlign(PdfHorizontalAlign.Right,  "align_right.pdf");
    }

    private void DrawTextWithAlign(PdfHorizontalAlign align, string outFilename)
    {
        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new TextDrawCommand
        {
            PageNumber      = 1, X = 30, Y = 100,
            Width           = 535,
            Text            = $"配置テスト [{align}]：請求書",
            FontFilePath    = _minchoPath!, TtcFontIndex = 0,
            FontSize        = 12,
            HorizontalAlign = align,
            FontColor       = PdfColor.Black
        });
        byte[] result = writer.BuildIncrementalUpdate();
        File.WriteAllBytes(TestHelper.OutputFile(outFilename), result);

        Assert.IsTrue(result.Length > _inputPdf.Length, $"{align}: 出力サイズ > 入力サイズ");
    }
}
