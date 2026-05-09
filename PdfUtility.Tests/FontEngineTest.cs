using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfUtility.Core.Exceptions;
using PdfUtility.Core.Helpers;
using PdfUtility.Tests.Helpers;

namespace PdfUtility.Tests;

[TestClass]
public class FontEngineTest
{
    private static string? _minchoPath;
    private static string? _gothicPath;
    private static string? _ttfPath;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        _minchoPath = TestHelper.GetFontPath("msmincho.ttc");
        _gothicPath = TestHelper.GetFontPath("msgothic.ttc");
        _ttfPath = TestHelper.GetFontPath("arial.ttf")
                ?? TestHelper.GetFontPath("times.ttf");
    }

    // ── 1. TTCファイル読み込みテスト ──────────────────────────────────────

    [TestMethod]
    [TestCategory("FontEngine")]
    public void LoadTtc_MsMincho_Index0_Succeeds()
    {
        TestHelper.SkipIfNotFound(_minchoPath, "msmincho.ttc");

        byte[] ttf = FontHelper.LoadFontBytes(_minchoPath!, 0);

        Assert.IsTrue(ttf.Length > 1000, "TTF展開結果は 1KB 以上あるはず");
        // TrueType / OpenType シグネチャ検証
        Assert.IsTrue(
            (ttf[0] == 0x00 && ttf[1] == 0x01) ||  // TrueType
            (ttf[0] == 0x4F && ttf[1] == 0x54),     // OTF "OT"
            "TTF シグネチャが不正");
        int upm = FontHelper.GetUnitsPerEm(ttf);
        Assert.IsTrue(upm > 0, "unitsPerEm > 0");
    }

    [TestMethod]
    [TestCategory("FontEngine")]
    public void LoadTtc_MsGothic_Index0_Succeeds()
    {
        TestHelper.SkipIfNotFound(_gothicPath, "msgothic.ttc");

        byte[] ttf = FontHelper.LoadFontBytes(_gothicPath!, 0);

        Assert.IsTrue(ttf.Length > 1000, "TTF展開結果は 1KB 以上あるはず");
        int upm = FontHelper.GetUnitsPerEm(ttf);
        Assert.IsTrue(upm > 0, "unitsPerEm > 0");
        string psName = FontHelper.GetPostScriptName(ttf);
        Assert.IsFalse(string.IsNullOrEmpty(psName), "PostScript 名が取得できること");
    }

    // ── 2. TTFファイル読み込みテスト ──────────────────────────────────────

    [TestMethod]
    [TestCategory("FontEngine")]
    public void LoadTtf_ReturnsValidFontBytes()
    {
        TestHelper.SkipIfNotFound(_ttfPath, "TTF フォント（arial.ttf 等）");

        byte[] ttf = FontHelper.LoadFontBytes(_ttfPath!);

        Assert.IsTrue(ttf.Length > 100, "TTF バイト列が取得できること");
        int upm = FontHelper.GetUnitsPerEm(ttf);
        Assert.IsTrue(upm > 0, "unitsPerEm > 0");
    }

    [TestMethod]
    [TestCategory("FontEngine")]
    public void LoadTtf_GlyphIdAscii_NotZero()
    {
        TestHelper.SkipIfNotFound(_ttfPath, "TTF フォント（arial.ttf 等）");

        byte[] ttf = FontHelper.LoadFontBytes(_ttfPath!);
        int glyphId = FontHelper.GetGlyphId(ttf, 'A');

        Assert.IsTrue(glyphId > 0, "ASCII 'A' のグリフIDは 0 より大きいはず");
    }

    // ── 3. fsType 判定テスト ──────────────────────────────────────────────

    [TestMethod]
    [TestCategory("FontEngine")]
    public void FsType_0x0008_EmbedFontTrue()
    {
        TestHelper.SkipIfNotFound(_minchoPath, "msmincho.ttc");

        byte[] ttf = FontHelper.LoadFontBytes(_minchoPath!, 0);
        byte[] patched = TestHelper.PatchFsType(ttf, 0x0008);

        ushort fsType = FontHelper.GetFsType(patched);
        Assert.AreEqual((ushort)0x0008, fsType, "fsType は 0x0008 であること");
        // bit1 (Restricted License) が立っていなければ埋め込み可
        Assert.AreEqual(0, fsType & 0x0002, "EmbedFont=true（bit1 がゼロ）");
    }

    [TestMethod]
    [TestCategory("FontEngine")]
    public void FsType_0x0002_EmbedFontFalse()
    {
        TestHelper.SkipIfNotFound(_minchoPath, "msmincho.ttc");

        byte[] ttf = FontHelper.LoadFontBytes(_minchoPath!, 0);
        byte[] patched = TestHelper.PatchFsType(ttf, 0x0002);

        ushort fsType = FontHelper.GetFsType(patched);
        Assert.AreEqual((ushort)0x0002, fsType, "fsType は 0x0002 であること");
        // bit1 が立っていれば埋め込み禁止
        Assert.AreNotEqual(0, fsType & 0x0002, "EmbedFont=false（bit1 が非ゼロ）");
    }

    [TestMethod]
    [TestCategory("FontEngine")]
    public void FsType_0x0002_PdfDoesNotEmbedFontFile()
    {
        TestHelper.SkipIfNotFound(_minchoPath, "msmincho.ttc");

        // fsType=0x0002 の一時フォントファイルを作成
        byte[] ttf = FontHelper.LoadFontBytes(_minchoPath!, 0);
        byte[] patched = TestHelper.PatchFsType(ttf, 0x0002);
        string tempFont = Path.Combine(Path.GetTempPath(), $"test_restricted_{Guid.NewGuid()}.ttf");

        try
        {
            File.WriteAllBytes(tempFont, patched);

            byte[] inputPdf = TestHelper.CreateMinimalA4Pdf();
            var writer = new PdfUtility.Core.PdfIncrementalWriter(inputPdf);
            writer.ApplyCommand(new PdfUtility.Core.Drawing.TextDrawCommand
            {
                PageNumber   = 1, X = 50, Y = 100,
                Text         = "Test",
                FontFilePath = tempFont, TtcFontIndex = 0,
                FontSize     = 10
            });
            byte[] result = writer.BuildIncrementalUpdate();

            string incremental = System.Text.Encoding.GetEncoding(28591)
                .GetString(result, inputPdf.Length, result.Length - inputPdf.Length);

            Assert.IsFalse(incremental.Contains("/FontFile2"),
                "EmbedFont=false のときは /FontFile2 を含まないこと");
        }
        finally
        {
            try { File.Delete(tempFont); } catch { }
        }
    }

    // ── 4. 不正ファイルの例外テスト ───────────────────────────────────────

    [TestMethod]
    [TestCategory("FontEngine")]
    [ExpectedException(typeof(PdfLoadException))]
    public void LoadFont_NonExistentFile_ThrowsPdfLoadException()
    {
        FontHelper.LoadFontBytes(@"C:\NonExistent\no_such_font.ttf");
    }

    [TestMethod]
    [TestCategory("FontEngine")]
    public void LoadFont_NonExistentFile_ExceptionMessageContainsPath()
    {
        const string fakePath = @"C:\NonExistent\missing.ttf";
        try
        {
            FontHelper.LoadFontBytes(fakePath);
            Assert.Fail("例外が発生するはず");
        }
        catch (PdfLoadException ex)
        {
            StringAssert.Contains(ex.Message, "missing.ttf");
        }
    }
}
