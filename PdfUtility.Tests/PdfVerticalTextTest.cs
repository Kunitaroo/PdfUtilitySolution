using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfUtility.Core;
using PdfUtility.Core.Drawing;
using PdfUtility.Tests.Helpers;

namespace PdfUtility.Tests;

[TestClass]
public class PdfVerticalTextTest
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

    private static string ReadIncrementalSegment(byte[] result, byte[] input)
        => System.Text.Encoding.GetEncoding(28591)
            .GetString(result, input.Length, result.Length - input.Length);

    /// <summary>
    /// インクリメンタル領域から最初のコンテンツストリーム（描画演算子列）だけを抜き出す。
    /// FontFile2 等のバイナリストリームにテキスト演算子と同じバイト列が偶然現れることを避ける。
    /// </summary>
    private static string ExtractFirstContentStream(string segment)
    {
        int streamStart = segment.IndexOf("stream\n", StringComparison.Ordinal);
        if (streamStart < 0) return "";
        streamStart += "stream\n".Length;
        int streamEnd = segment.IndexOf("\nendstream", streamStart, StringComparison.Ordinal);
        if (streamEnd < 0) return "";
        return segment.Substring(streamStart, streamEnd - streamStart);
    }

    // ── 1. 基本：縦書き出力サイズ・構造テスト ────────────────────────────

    [TestMethod]
    [TestCategory("PdfVerticalText")]
    public void DrawVerticalText_Basic_OutputLargerThanInput()
    {
        TestHelper.SkipIfNotFound(_minchoPath, "msmincho.ttc");

        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new TextDrawCommand
        {
            PageNumber   = 1, X = 500, Y = 80,
            Text         = "縦書きテスト",
            FontFilePath = _minchoPath!, TtcFontIndex = 0,
            FontSize     = 14, FontColor = PdfColor.Black,
            WritingMode  = WritingMode.Vertical,
        });
        byte[] result = writer.BuildIncrementalUpdate();
        File.WriteAllBytes(TestHelper.OutputFile("vertical_basic.pdf"), result);

        Assert.IsTrue(result.Length > _inputPdf.Length, "縦書き出力サイズ > 入力サイズ");
    }

    [TestMethod]
    [TestCategory("PdfVerticalText")]
    public void DrawVerticalText_EmitsOneTmPerCharacter()
    {
        TestHelper.SkipIfNotFound(_minchoPath, "msmincho.ttc");

        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new TextDrawCommand
        {
            PageNumber   = 1, X = 400, Y = 100,
            Text         = "あいう",  // 3文字
            FontFilePath = _minchoPath!, TtcFontIndex = 0,
            FontSize     = 12,
            WritingMode  = WritingMode.Vertical,
        });
        byte[] result = writer.BuildIncrementalUpdate();
        string content = ExtractFirstContentStream(ReadIncrementalSegment(result, _inputPdf));

        // 縦書きは文字ごとに Tm + Tj を発行する
        int tmCount = System.Text.RegularExpressions.Regex.Matches(content, @"\bTm\b").Count;
        int tjCount = System.Text.RegularExpressions.Regex.Matches(content, @"\bTj\b").Count;
        Assert.AreEqual(3, tmCount, "3文字分の Tm が発行されること");
        Assert.AreEqual(3, tjCount, "3文字分の Tj が発行されること");
    }

    // ── 2. ASCII 回転テスト ───────────────────────────────────────────────

    [TestMethod]
    [TestCategory("PdfVerticalText")]
    public void DrawVerticalText_AsciiCharacters_UseRotationMatrix()
    {
        TestHelper.SkipIfNotFound(_minchoPath, "msmincho.ttc");

        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new TextDrawCommand
        {
            PageNumber   = 1, X = 400, Y = 100,
            Text         = "ABC",  // 全部 ASCII → 全文字回転
            FontFilePath = _minchoPath!, TtcFontIndex = 0,
            FontSize     = 12,
            WritingMode  = WritingMode.Vertical,
        });
        byte[] result = writer.BuildIncrementalUpdate();
        string content = ExtractFirstContentStream(ReadIncrementalSegment(result, _inputPdf));

        // 90° CW 回転行列：[0 -1 1 0 tx ty]
        int rotationMatrixCount = System.Text.RegularExpressions.Regex.Matches(
            content, @"0 -1 1 0 [\-\d\.]+ [\-\d\.]+ Tm").Count;
        Assert.AreEqual(3, rotationMatrixCount,
            "ASCII 3文字分の回転行列 Tm が発行されること");
    }

    [TestMethod]
    [TestCategory("PdfVerticalText")]
    public void DrawVerticalText_JapaneseAndAscii_MixesUprightAndRotated()
    {
        TestHelper.SkipIfNotFound(_minchoPath, "msmincho.ttc");

        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new TextDrawCommand
        {
            PageNumber   = 1, X = 400, Y = 100,
            Text         = "あA",  // 直立 + 回転
            FontFilePath = _minchoPath!, TtcFontIndex = 0,
            FontSize     = 12,
            WritingMode  = WritingMode.Vertical,
        });
        byte[] result = writer.BuildIncrementalUpdate();
        string content = ExtractFirstContentStream(ReadIncrementalSegment(result, _inputPdf));

        // あ = 直立 (1 0 0 1 ...)、A = 回転 (0 -1 1 0 ...)
        int uprightCount  = System.Text.RegularExpressions.Regex.Matches(
            content, @"1 0 0 1 [\-\d\.]+ [\-\d\.]+ Tm").Count;
        int rotatedCount = System.Text.RegularExpressions.Regex.Matches(
            content, @"0 -1 1 0 [\-\d\.]+ [\-\d\.]+ Tm").Count;

        Assert.AreEqual(1, uprightCount,  "和字1文字は直立で配置されること");
        Assert.AreEqual(1, rotatedCount,  "ASCII 1文字は回転で配置されること");
    }

    // ── 3. 句読点・記号テスト ────────────────────────────────────────────

    [TestMethod]
    [TestCategory("PdfVerticalText")]
    public void DrawVerticalText_PunctuationOffsetUpperRight()
    {
        TestHelper.SkipIfNotFound(_minchoPath, "msmincho.ttc");

        var writer = new PdfIncrementalWriter(_inputPdf);
        // 「あ。い、う」: あ→直立、。→右上、い→直立、、→右上、う→直立
        writer.ApplyCommand(new TextDrawCommand
        {
            PageNumber   = 1, X = 400, Y = 100,
            Text         = "あ。い、う",
            FontFilePath = _minchoPath!, TtcFontIndex = 0,
            FontSize     = 14,
            WritingMode  = WritingMode.Vertical,
        });
        byte[] result = writer.BuildIncrementalUpdate();
        File.WriteAllBytes(TestHelper.OutputFile("vertical_punctuation.pdf"), result);
        string content = ExtractFirstContentStream(ReadIncrementalSegment(result, _inputPdf));

        int uprightCount = System.Text.RegularExpressions.Regex.Matches(
            content, @"1 0 0 1 [\-\d\.]+ [\-\d\.]+ Tm").Count;
        // 直立3文字 + 句読点2文字（こちらも Identity 行列なので同じパターン） = 5
        Assert.AreEqual(5, uprightCount,
            "和字3文字＋句読点2文字 = 5回の Tm が発行されること（句読点は別座標へオフセット）");
    }

    [TestMethod]
    [TestCategory("PdfVerticalText")]
    public void DrawVerticalText_BracketsRotated()
    {
        TestHelper.SkipIfNotFound(_minchoPath, "msmincho.ttc");

        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new TextDrawCommand
        {
            PageNumber   = 1, X = 400, Y = 100,
            Text         = "「縦書き」",  // 「 」 は回転
            FontFilePath = _minchoPath!, TtcFontIndex = 0,
            FontSize     = 14,
            WritingMode  = WritingMode.Vertical,
        });
        byte[] result = writer.BuildIncrementalUpdate();
        string content = ExtractFirstContentStream(ReadIncrementalSegment(result, _inputPdf));

        int rotatedCount = System.Text.RegularExpressions.Regex.Matches(
            content, @"0 -1 1 0 [\-\d\.]+ [\-\d\.]+ Tm").Count;
        Assert.AreEqual(2, rotatedCount,
            "鉤括弧2つは回転行列で配置されること");
    }

    // ── 4. 複数列（改行で右→左に列が流れる） ─────────────────────────────

    [TestMethod]
    [TestCategory("PdfVerticalText")]
    public void DrawVerticalText_MultipleColumns_FlowsRightToLeft()
    {
        TestHelper.SkipIfNotFound(_minchoPath, "msmincho.ttc");

        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new TextDrawCommand
        {
            PageNumber   = 1, X = 500, Y = 80,
            Text         = "右列\n中列\n左列",
            FontFilePath = _minchoPath!, TtcFontIndex = 0,
            FontSize     = 14, FontColor = PdfColor.Black,
            WritingMode  = WritingMode.Vertical,
        });
        byte[] result = writer.BuildIncrementalUpdate();
        File.WriteAllBytes(TestHelper.OutputFile("vertical_multicolumn.pdf"), result);
        string content = ExtractFirstContentStream(ReadIncrementalSegment(result, _inputPdf));

        // 各列の最初の文字は「右」「中」「左」。Tm の tx 値（5 番目の数字）は単調減少するはず。
        // 抽出して順序を確認する。
        var matches = System.Text.RegularExpressions.Regex.Matches(
            content, @"1 0 0 1 (?<tx>[\-\d\.]+) [\-\d\.]+ Tm\s*<[0-9A-F]+> Tj");
        Assert.IsTrue(matches.Count >= 6, "6回以上の文字描画があること");

        double tx0 = double.Parse(matches[0].Groups["tx"].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        // 最後の列の最初の文字 = 6番目の文字描画
        double txLast = double.Parse(matches[matches.Count - 1].Groups["tx"].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.IsTrue(txLast < tx0,
            $"列が右から左へ流れること (tx_first={tx0}, tx_last={txLast})");
    }

    // ── 5. 横書き・縦書き混在テスト ───────────────────────────────────────

    [TestMethod]
    [TestCategory("PdfVerticalText")]
    public void DrawText_HorizontalAndVerticalMixed_BothRendered()
    {
        TestHelper.SkipIfNotFound(_minchoPath, "msmincho.ttc");

        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommands(new PdfDrawCommand[]
        {
            new TextDrawCommand
            {
                PageNumber   = 1, X = 50, Y = 50,
                Text         = "横書きヘッダー",
                FontFilePath = _minchoPath!, TtcFontIndex = 0,
                FontSize     = 12,
                WritingMode  = WritingMode.Horizontal,
            },
            new TextDrawCommand
            {
                PageNumber   = 1, X = 500, Y = 100,
                Text         = "縦書き本文",
                FontFilePath = _minchoPath!, TtcFontIndex = 0,
                FontSize     = 14,
                WritingMode  = WritingMode.Vertical,
            },
        });
        byte[] result = writer.BuildIncrementalUpdate();
        File.WriteAllBytes(TestHelper.OutputFile("vertical_mixed.pdf"), result);
        string content = ExtractFirstContentStream(ReadIncrementalSegment(result, _inputPdf));

        // 横書きは Td、縦書きは Tm を使うため両方が含まれること
        Assert.IsTrue(content.Contains(" Td"), "横書き由来の Td が含まれること");
        Assert.IsTrue(content.Contains(" Tm"), "縦書き由来の Tm が含まれること");
    }

    // ── 6. VerticalTextAlign 動作確認 ───────────────────────────────────

    [TestMethod]
    [TestCategory("PdfVerticalText")]
    public void DrawVerticalText_VerticalAlignBottom_OffsetsStartY()
    {
        TestHelper.SkipIfNotFound(_minchoPath, "msmincho.ttc");

        var topWriter = new PdfIncrementalWriter(_inputPdf);
        topWriter.ApplyCommand(new TextDrawCommand
        {
            PageNumber        = 1, X = 400, Y = 100, Width = 200,
            Text              = "ああ",
            FontFilePath      = _minchoPath!, TtcFontIndex = 0,
            FontSize          = 14,
            WritingMode       = WritingMode.Vertical,
            VerticalTextAlign = PdfVerticalTextAlign.Top,
        });
        byte[] topResult = topWriter.BuildIncrementalUpdate();
        string topSeg = ExtractFirstContentStream(ReadIncrementalSegment(topResult, _inputPdf));

        var botWriter = new PdfIncrementalWriter(_inputPdf);
        botWriter.ApplyCommand(new TextDrawCommand
        {
            PageNumber        = 1, X = 400, Y = 100, Width = 200,
            Text              = "ああ",
            FontFilePath      = _minchoPath!, TtcFontIndex = 0,
            FontSize          = 14,
            WritingMode       = WritingMode.Vertical,
            VerticalTextAlign = PdfVerticalTextAlign.Bottom,
        });
        byte[] botResult = botWriter.BuildIncrementalUpdate();
        string botSeg = ExtractFirstContentStream(ReadIncrementalSegment(botResult, _inputPdf));

        // Top と Bottom で最初の文字の ty 値（PDF Y）が異なるはず
        var topMatch = System.Text.RegularExpressions.Regex.Match(
            topSeg, @"1 0 0 1 [\-\d\.]+ (?<ty>[\-\d\.]+) Tm\s*<[0-9A-F]+> Tj");
        var botMatch = System.Text.RegularExpressions.Regex.Match(
            botSeg, @"1 0 0 1 [\-\d\.]+ (?<ty>[\-\d\.]+) Tm\s*<[0-9A-F]+> Tj");

        double topTy = double.Parse(topMatch.Groups["ty"].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        double botTy = double.Parse(botMatch.Groups["ty"].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.IsTrue(botTy < topTy,
            $"Bottom 配置は Top より下方（PDF Y が小さい）に配置されること (Top={topTy}, Bottom={botTy})");
    }

    // ── 7. 総合：vertical_test.pdf 生成 ──────────────────────────────────

    [TestMethod]
    [TestCategory("PdfVerticalText")]
    public void GenerateVerticalTestPdf()
    {
        TestHelper.SkipIfNotFound(_minchoPath, "msmincho.ttc");

        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommands(new PdfDrawCommand[]
        {
            // 横書きタイトル
            new TextDrawCommand
            {
                PageNumber   = 1, X = 50, Y = 50,
                Text         = "縦書きテストサンプル",
                FontFilePath = _minchoPath!, TtcFontIndex = 0,
                FontSize     = 16, FontColor = PdfColor.Black,
                WritingMode  = WritingMode.Horizontal,
            },
            // 1: 基本縦書き（右側列）
            new TextDrawCommand
            {
                PageNumber   = 1, X = 540, Y = 100,
                Text         = "山路を登りながら",
                FontFilePath = _minchoPath!, TtcFontIndex = 0,
                FontSize     = 14, FontColor = PdfColor.Black,
                WritingMode  = WritingMode.Vertical,
            },
            // 2: 句読点を含む
            new TextDrawCommand
            {
                PageNumber   = 1, X = 500, Y = 100,
                Text         = "考えた。",
                FontFilePath = _minchoPath!, TtcFontIndex = 0,
                FontSize     = 14,
                WritingMode  = WritingMode.Vertical,
            },
            // 3: 括弧と英数混在
            new TextDrawCommand
            {
                PageNumber   = 1, X = 460, Y = 100,
                Text         = "「PDF」を作る",
                FontFilePath = _minchoPath!, TtcFontIndex = 0,
                FontSize     = 14,
                WritingMode  = WritingMode.Vertical,
            },
            // 4: 複数列（RTL）
            new TextDrawCommand
            {
                PageNumber   = 1, X = 400, Y = 100,
                Text         = "右列\n中列\n左列",
                FontFilePath = _minchoPath!, TtcFontIndex = 0,
                FontSize     = 14, FontColor = PdfColor.Red,
                WritingMode  = WritingMode.Vertical,
            },
            // 5: 数字＋長音
            new TextDrawCommand
            {
                PageNumber   = 1, X = 200, Y = 100,
                Text         = "AB123ーDEF",
                FontFilePath = _minchoPath!, TtcFontIndex = 0,
                FontSize     = 14, FontColor = PdfColor.Black,
                WritingMode  = WritingMode.Vertical,
            },
        });
        byte[] result = writer.BuildIncrementalUpdate();
        string outPath = TestHelper.OutputFile("vertical_test.pdf");
        File.WriteAllBytes(outPath, result);

        Assert.IsTrue(result.Length > _inputPdf.Length, "vertical_test.pdf が生成されること");
        Assert.IsTrue(File.Exists(outPath), $"出力ファイルが存在すること: {outPath}");

        // 再パースで構造が壊れていないことを確認
        var verify = new PdfIncrementalWriter(result);
        Assert.IsTrue(verify.PageCount > 0, "再パース時にページ数が取得できること");
    }
}
