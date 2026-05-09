using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfUtility.Core;
using PdfUtility.Core.Drawing;
using PdfUtility.Tests.Helpers;

namespace PdfUtility.Tests;

[TestClass]
public class PdfImageDrawTest
{
    private static byte[] _inputPdf = null!;
    private static byte[] _pngBytes = null!;
    private static byte[] _jpegBytes = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        _inputPdf  = TestHelper.GetInputPdf();
        _pngBytes  = GetOrCreatePng();
        _jpegBytes = TestHelper.CreateMinimalJpeg(100, 50);
    }

    private static byte[] GetOrCreatePng()
    {
        // testdata/sample_blue.png があれば使用
        string samplePng = Path.Combine(TestHelper.TestdataDir, "sample_blue.png");
        if (File.Exists(samplePng)) return File.ReadAllBytes(samplePng);

        // なければ 200x100 青色 PNG を生成して保存
        byte[] png = TestHelper.CreateSolidColorPng(200, 100, 50, 100, 200);
        Directory.CreateDirectory(TestHelper.TestdataDir);
        File.WriteAllBytes(samplePng, png);
        return png;
    }

    // ── 1. JPEG 画像埋め込みテスト ─────────────────────────────────────

    [TestMethod]
    [TestCategory("PdfImage")]
    public void EmbedJpeg_OutputLargerThanInput()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new ImageDrawCommand
        {
            PageNumber = 1, X = 50, Y = 100,
            Width = 100, Height = 50,
            ImageBytes = _jpegBytes
        });
        byte[] result = writer.BuildIncrementalUpdate();
        File.WriteAllBytes(TestHelper.OutputFile("image_jpeg.pdf"), result);

        Assert.IsTrue(result.Length > _inputPdf.Length, "JPEG 埋め込み後のサイズ > 入力サイズ");
    }

    [TestMethod]
    [TestCategory("PdfImage")]
    public void EmbedJpeg_ContainsDctDecodeFilter()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new ImageDrawCommand
        {
            PageNumber = 1, X = 50, Y = 100,
            Width = 100, Height = 50,
            ImageBytes = _jpegBytes
        });
        byte[] result = writer.BuildIncrementalUpdate();

        string incremental = System.Text.Encoding.GetEncoding(28591)
            .GetString(result, _inputPdf.Length, result.Length - _inputPdf.Length);

        Assert.IsTrue(incremental.Contains("/DCTDecode"),
            "JPEG 埋め込みには /DCTDecode フィルタが含まれること");
        Assert.IsTrue(incremental.Contains("/Subtype /Image"),
            "/Subtype /Image が含まれること");
    }

    // ── 2. PNG 画像埋め込みテスト ─────────────────────────────────────

    [TestMethod]
    [TestCategory("PdfImage")]
    public void EmbedPng_OutputLargerThanInput()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new ImageDrawCommand
        {
            PageNumber = 1, X = 30, Y = 100,
            Width = 150, Height = 80,
            ImageBytes = _pngBytes
        });
        byte[] result = writer.BuildIncrementalUpdate();
        File.WriteAllBytes(TestHelper.OutputFile("image_png.pdf"), result);

        Assert.IsTrue(result.Length > _inputPdf.Length, "PNG 埋め込み後のサイズ > 入力サイズ");
    }

    [TestMethod]
    [TestCategory("PdfImage")]
    public void EmbedPng_ContainsFlatDecodeFilter()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new ImageDrawCommand
        {
            PageNumber = 1, X = 30, Y = 100,
            Width = 150, Height = 80,
            ImageBytes = _pngBytes
        });
        byte[] result = writer.BuildIncrementalUpdate();

        string incremental = System.Text.Encoding.GetEncoding(28591)
            .GetString(result, _inputPdf.Length, result.Length - _inputPdf.Length);

        Assert.IsTrue(incremental.Contains("/FlateDecode"),
            "PNG 埋め込みには /FlateDecode フィルタが含まれること");
        Assert.IsTrue(incremental.Contains("/Subtype /Image"),
            "/Subtype /Image が含まれること");
    }

    [TestMethod]
    [TestCategory("PdfImage")]
    public void EmbedPng_CanReopenPdf()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new ImageDrawCommand
        {
            PageNumber = 1, X = 30, Y = 100,
            Width = 150, Height = 80,
            ImageBytes = _pngBytes
        });
        byte[] result = writer.BuildIncrementalUpdate();

        // 追記後の PDF を再解析できること
        var verify = new PdfIncrementalWriter(result);
        Assert.AreEqual(writer.PageCount, verify.PageCount, "再解析後のページ数が一致すること");
    }

    // ── 3. アスペクト比維持テスト ──────────────────────────────────────

    [TestMethod]
    [TestCategory("PdfImage")]
    public void EmbedImage_KeepAspectRatioTrue_OutputLargerThanInput()
    {
        // 200x100 px 画像（アスペクト比 2:1）を 200x200 の領域に KeepAspectRatio=true で配置
        byte[] png = TestHelper.CreateSolidColorPng(200, 100, 200, 100, 50);

        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new ImageDrawCommand
        {
            PageNumber     = 1, X = 30, Y = 200,
            Width = 200, Height = 200,
            ImageBytes     = png,
            KeepAspectRatio = true
        });
        byte[] result = writer.BuildIncrementalUpdate();
        File.WriteAllBytes(TestHelper.OutputFile("image_aspect.pdf"), result);

        Assert.IsTrue(result.Length > _inputPdf.Length,
            "KeepAspectRatio=true でも出力サイズ > 入力サイズ");
    }

    [TestMethod]
    [TestCategory("PdfImage")]
    public void EmbedImage_KeepAspectRatioFalse_OutputLargerThanInput()
    {
        byte[] png = TestHelper.CreateSolidColorPng(200, 100, 100, 200, 50);

        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new ImageDrawCommand
        {
            PageNumber      = 1, X = 30, Y = 400,
            Width = 200, Height = 200,
            ImageBytes      = png,
            KeepAspectRatio = false
        });
        byte[] result = writer.BuildIncrementalUpdate();

        Assert.IsTrue(result.Length > _inputPdf.Length,
            "KeepAspectRatio=false でも出力サイズ > 入力サイズ");
    }

    [TestMethod]
    [TestCategory("PdfImage")]
    public void EmbedMultipleImages_SamePage_Succeeds()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);

        byte[] png1 = TestHelper.CreateSolidColorPng(100, 100, 200,  50,  50);
        byte[] png2 = TestHelper.CreateSolidColorPng(100, 100,  50, 200,  50);
        byte[] png3 = TestHelper.CreateSolidColorPng(100, 100,  50,  50, 200);

        writer.ApplyCommands(new[]
        {
            new ImageDrawCommand { PageNumber=1, X= 30, Y=300, Width=80, Height=80, ImageBytes=png1 },
            new ImageDrawCommand { PageNumber=1, X=130, Y=300, Width=80, Height=80, ImageBytes=png2 },
            new ImageDrawCommand { PageNumber=1, X=230, Y=300, Width=80, Height=80, ImageBytes=png3 },
        });

        byte[] result = writer.BuildIncrementalUpdate();
        File.WriteAllBytes(TestHelper.OutputFile("image_multiple.pdf"), result);

        Assert.IsTrue(result.Length > _inputPdf.Length, "複数画像埋め込み後のサイズ > 入力サイズ");
    }
}
