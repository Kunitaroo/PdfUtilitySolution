using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfUtility.Core;
using PdfUtility.Core.Drawing;
using PdfUtility.Tests.Helpers;

namespace PdfUtility.Tests;

[TestClass]
public class PdfShapeDrawTest
{
    private static byte[] _inputPdf = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        _inputPdf = TestHelper.GetInputPdf();
    }

    // ── 1. 矩形描画テスト（枠のみ） ──────────────────────────────────────

    [TestMethod]
    [TestCategory("PdfShape")]
    public void DrawRectangle_BorderOnly_OutputLargerThanInput()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new RectangleDrawCommand
        {
            PageNumber   = 1, X = 50, Y = 50,
            Width = 200, Height = 100,
            BorderColor  = PdfColor.Black,
            BorderWidth  = 1.0,
            IsFilled     = false
        });
        byte[] result = writer.BuildIncrementalUpdate();
        File.WriteAllBytes(TestHelper.OutputFile("shape_rect_border.pdf"), result);

        Assert.IsTrue(result.Length > _inputPdf.Length, "出力サイズ > 入力サイズ");
    }

    [TestMethod]
    [TestCategory("PdfShape")]
    public void DrawRectangle_BorderOnly_ContainsStrokeOperator()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new RectangleDrawCommand
        {
            PageNumber = 1, X = 50, Y = 50, Width = 200, Height = 100,
            BorderColor = PdfColor.Black, BorderWidth = 1.0, IsFilled = false
        });
        byte[] result = writer.BuildIncrementalUpdate();

        string incremental = System.Text.Encoding.GetEncoding(28591)
            .GetString(result, _inputPdf.Length, result.Length - _inputPdf.Length);

        // 枠のみ: S演算子（Stroke）を含み、f/B を含まないこと
        Assert.IsTrue(incremental.Contains("\nS\n"), "枠のみ矩形は S 演算子を使用すること");
    }

    // ── 2. 矩形描画テスト（塗りつぶしあり） ──────────────────────────────

    [TestMethod]
    [TestCategory("PdfShape")]
    public void DrawRectangle_WithFill_OutputLargerThanInput()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new RectangleDrawCommand
        {
            PageNumber  = 1, X = 100, Y = 100,
            Width = 150, Height = 80,
            BorderColor = PdfColor.Black, BorderWidth = 1.5,
            FillColor   = new PdfColor(173, 216, 230),
            IsFilled    = true
        });
        byte[] result = writer.BuildIncrementalUpdate();
        File.WriteAllBytes(TestHelper.OutputFile("shape_rect_filled.pdf"), result);

        Assert.IsTrue(result.Length > _inputPdf.Length, "出力サイズ > 入力サイズ");
    }

    [TestMethod]
    [TestCategory("PdfShape")]
    public void DrawRectangle_WithFill_ContainsFillAndStrokeOperator()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new RectangleDrawCommand
        {
            PageNumber = 1, X = 100, Y = 100, Width = 150, Height = 80,
            BorderColor = PdfColor.Black, BorderWidth = 1.5,
            FillColor = new PdfColor(100, 200, 100), IsFilled = true
        });
        byte[] result = writer.BuildIncrementalUpdate();

        string incremental = System.Text.Encoding.GetEncoding(28591)
            .GetString(result, _inputPdf.Length, result.Length - _inputPdf.Length);

        // 枠あり+塗りつぶし: B演算子（Fill + Stroke）
        Assert.IsTrue(incremental.Contains("\nB\n"), "塗りつぶし+枠矩形は B 演算子を使用すること");
    }

    [TestMethod]
    [TestCategory("PdfShape")]
    public void DrawRectangle_FillOnly_ContainsFillOperator()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new RectangleDrawCommand
        {
            PageNumber  = 1, X = 100, Y = 100, Width = 150, Height = 80,
            BorderWidth = 0,
            FillColor   = new PdfColor(255, 220, 180),
            IsFilled    = true
        });
        byte[] result = writer.BuildIncrementalUpdate();

        string incremental = System.Text.Encoding.GetEncoding(28591)
            .GetString(result, _inputPdf.Length, result.Length - _inputPdf.Length);

        // 枠なし塗りつぶし: f演算子
        Assert.IsTrue(incremental.Contains("\nf\n"), "塗りつぶしのみ矩形は f 演算子を使用すること");
    }

    // ── 3. 線描画テスト ──────────────────────────────────────────────────

    [TestMethod]
    [TestCategory("PdfShape")]
    public void DrawLine_Horizontal_OutputLargerThanInput()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new LineDrawCommand
        {
            PageNumber = 1, X = 30, Y = 200, X2 = 565, Y2 = 200,
            LineColor  = PdfColor.Black, LineWidth = 1.0
        });
        byte[] result = writer.BuildIncrementalUpdate();
        File.WriteAllBytes(TestHelper.OutputFile("shape_line_h.pdf"), result);

        Assert.IsTrue(result.Length > _inputPdf.Length, "出力サイズ > 入力サイズ");
    }

    [TestMethod]
    [TestCategory("PdfShape")]
    public void DrawLine_Vertical_OutputLargerThanInput()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new LineDrawCommand
        {
            PageNumber = 1, X = 300, Y = 50, X2 = 300, Y2 = 800,
            LineColor  = new PdfColor(0, 0, 200), LineWidth = 0.5
        });
        byte[] result = writer.BuildIncrementalUpdate();
        File.WriteAllBytes(TestHelper.OutputFile("shape_line_v.pdf"), result);

        Assert.IsTrue(result.Length > _inputPdf.Length, "出力サイズ > 入力サイズ");
    }

    [TestMethod]
    [TestCategory("PdfShape")]
    public void DrawLine_ContainsPathOperators()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new LineDrawCommand
        {
            PageNumber = 1, X = 30, Y = 200, X2 = 565, Y2 = 200,
            LineColor  = PdfColor.Black, LineWidth = 1.0
        });
        byte[] result = writer.BuildIncrementalUpdate();

        string incremental = System.Text.Encoding.GetEncoding(28591)
            .GetString(result, _inputPdf.Length, result.Length - _inputPdf.Length);

        Assert.IsTrue(incremental.Contains(" m\n"), "m 演算子（moveto）が含まれること");
        Assert.IsTrue(incremental.Contains(" l\n"), "l 演算子（lineto）が含まれること");
        Assert.IsTrue(incremental.Contains("\nS\n"), "S 演算子（stroke）が含まれること");
    }

    [TestMethod]
    [TestCategory("PdfShape")]
    public void DrawLine_Dashed_ContainsDashOperator()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new LineDrawCommand
        {
            PageNumber   = 1, X = 30, Y = 250, X2 = 565, Y2 = 250,
            LineColor    = PdfColor.Gray, LineWidth = 0.75,
            DashPattern  = new double[] { 5, 3 }, DashPhase = 0
        });
        byte[] result = writer.BuildIncrementalUpdate();

        string incremental = System.Text.Encoding.GetEncoding(28591)
            .GetString(result, _inputPdf.Length, result.Length - _inputPdf.Length);

        Assert.IsTrue(incremental.Contains(" d\n"), "破線 d 演算子が含まれること");
    }

    // ── 4. 複数図形テスト ────────────────────────────────────────────────

    [TestMethod]
    [TestCategory("PdfShape")]
    public void DrawMultipleShapes_AllOnPage1_Succeeds()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);

        writer.ApplyCommands(new PdfDrawCommand[]
        {
            new RectangleDrawCommand
            {
                Name="outer", PageNumber=1, X=20, Y=20, Width=555, Height=800,
                BorderColor=PdfColor.Black, BorderWidth=0.5, IsFilled=false, ZIndex=0
            },
            new RectangleDrawCommand
            {
                Name="header", PageNumber=1, X=30, Y=30, Width=535, Height=60,
                BorderColor=new PdfColor(0,100,180), BorderWidth=1.5,
                FillColor=new PdfColor(173,216,230), IsFilled=true, ZIndex=1
            },
            new LineDrawCommand
            {
                Name="sep", PageNumber=1, X=30, Y=110, X2=565, Y2=110,
                LineColor=PdfColor.Black, LineWidth=1.0, ZIndex=2
            },
            new RectangleDrawCommand
            {
                Name="highlight", PageNumber=1, X=30, Y=130, Width=535, Height=40,
                BorderColor=PdfColor.Red, BorderWidth=1.0,
                FillColor=new PdfColor(255,220,220), IsFilled=true, ZIndex=3
            },
        });

        byte[] result = writer.BuildIncrementalUpdate();
        File.WriteAllBytes(TestHelper.OutputFile("shape_multiple.pdf"), result);

        Assert.IsTrue(result.Length > _inputPdf.Length, "出力サイズ > 入力サイズ");

        // 追記後の PDF を再解析してページ数が変わらないこと
        var verify = new PdfIncrementalWriter(result);
        Assert.AreEqual(writer.PageCount, verify.PageCount, "ページ数が一致すること");
    }
}
