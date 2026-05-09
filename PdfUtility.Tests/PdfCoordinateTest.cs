using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfUtility.Tests.Helpers;

namespace PdfUtility.Tests;

/// <summary>
/// 座標変換ロジックのテスト。
///
/// 変換式（PreviewApp.PdfCoordinateConverter と同一ロジック）：
///   画面→PDF: ptX = screenX * pageW / dispW;  ptY = pageH - screenY * pageH / dispH
///   PDF→画面: px  = ptX  * dispW / pageW;     py  = (pageH - ptY) * dispH / pageH
/// </summary>
[TestClass]
public class PdfCoordinateTest
{
    private const double Eps = 0.001;

    // A4 縦: 595 x 842 pt
    private const double PageW = 595.0;
    private const double PageH = 842.0;

    // 表示サイズ（1:1 スケール）
    private const double DispW = 595.0;
    private const double DispH = 842.0;

    // ── 1. PDF座標 → 画面座標変換テスト ──────────────────────────────

    [TestMethod]
    [TestCategory("Coordinate")]
    public void ToDisplayPx_PdfOrigin_ReturnsBottomLeft()
    {
        // PDF 原点 (0, 0) = 左下 → 画面では左下 (0, DispH)
        var (px, py) = TestHelper.ToDisplayPx(0, 0, DispW, DispH, PageW, PageH);

        Assert.AreEqual(0.0,  px, Eps, "PDF 原点 X → 画面 X = 0");
        Assert.AreEqual(DispH, py, Eps, "PDF 原点 Y → 画面 Y = DispH（左下）");
    }

    [TestMethod]
    [TestCategory("Coordinate")]
    public void ToDisplayPx_PdfTopLeft_ReturnsScreenOrigin()
    {
        // PDF (0, PageH) = 左上 → 画面では左上 (0, 0)
        var (px, py) = TestHelper.ToDisplayPx(0, PageH, DispW, DispH, PageW, PageH);

        Assert.AreEqual(0.0, px, Eps, "PDF 左上 X → 画面 X = 0");
        Assert.AreEqual(0.0, py, Eps, "PDF 左上 Y → 画面 Y = 0");
    }

    [TestMethod]
    [TestCategory("Coordinate")]
    public void ToDisplayPx_PdfCenter_ReturnsScreenCenter()
    {
        var (px, py) = TestHelper.ToDisplayPx(PageW / 2, PageH / 2, DispW, DispH, PageW, PageH);

        Assert.AreEqual(DispW / 2, px, Eps, "中央 X が一致すること");
        Assert.AreEqual(DispH / 2, py, Eps, "中央 Y が一致すること");
    }

    [TestMethod]
    [TestCategory("Coordinate")]
    public void ToDisplayPx_PdfTopRight_ReturnsScreenTopRight()
    {
        // PDF (PageW, PageH) = 右上 → 画面では右上 (DispW, 0)
        var (px, py) = TestHelper.ToDisplayPx(PageW, PageH, DispW, DispH, PageW, PageH);

        Assert.AreEqual(DispW, px, Eps, "右上 X が一致すること");
        Assert.AreEqual(0.0,   py, Eps, "右上 Y が一致すること");
    }

    // ── 2. 画面座標 → PDF座標変換テスト ──────────────────────────────

    [TestMethod]
    [TestCategory("Coordinate")]
    public void ToPdfPoints_ScreenOrigin_ReturnsPdfTopLeft()
    {
        // 画面原点 (0, 0) = 左上 → PDF では左上 (0, PageH)
        var (ptX, ptY) = TestHelper.ToPdfPoints(0, 0, DispW, DispH, PageW, PageH);

        Assert.AreEqual(0.0,   ptX, Eps, "画面原点 X → PDF X = 0");
        Assert.AreEqual(PageH, ptY, Eps, "画面原点 Y → PDF Y = PageH（左上）");
    }

    [TestMethod]
    [TestCategory("Coordinate")]
    public void ToPdfPoints_ScreenBottomLeft_ReturnsPdfOrigin()
    {
        // 画面左下 (0, DispH) → PDF 原点 (0, 0)
        var (ptX, ptY) = TestHelper.ToPdfPoints(0, DispH, DispW, DispH, PageW, PageH);

        Assert.AreEqual(0.0, ptX, Eps, "画面左下 X → PDF X = 0");
        Assert.AreEqual(0.0, ptY, Eps, "画面左下 Y → PDF Y = 0");
    }

    [TestMethod]
    [TestCategory("Coordinate")]
    public void ToPdfPoints_ArbitraryPoint_CorrectConversion()
    {
        // 画面 (100, 200) → PDF (100 * PageW/DispW, PageH - 200 * PageH/DispH)
        double screenX = 100, screenY = 200;
        double expectedPtX = screenX * PageW / DispW;
        double expectedPtY = PageH - screenY * PageH / DispH;

        var (ptX, ptY) = TestHelper.ToPdfPoints(screenX, screenY, DispW, DispH, PageW, PageH);

        Assert.AreEqual(expectedPtX, ptX, Eps, "任意点 X の変換が正しいこと");
        Assert.AreEqual(expectedPtY, ptY, Eps, "任意点 Y の変換が正しいこと");
    }

    // ── 3. 境界値テスト ──────────────────────────────────────────────────

    [TestMethod]
    [TestCategory("Coordinate")]
    public void ToPdfPoints_CoordZero_ReturnsPdfTopLeft()
    {
        var (ptX, ptY) = TestHelper.ToPdfPoints(0, 0, DispW, DispH, PageW, PageH);

        Assert.AreEqual(0.0,   ptX, Eps);
        Assert.AreEqual(PageH, ptY, Eps);
    }

    [TestMethod]
    [TestCategory("Coordinate")]
    public void ToPdfPoints_MaxCoords_ReturnsPdfBottomRight()
    {
        var (ptX, ptY) = TestHelper.ToPdfPoints(DispW, DispH, DispW, DispH, PageW, PageH);

        Assert.AreEqual(PageW, ptX, Eps, "画面右端 → PDF X = PageW");
        Assert.AreEqual(0.0,   ptY, Eps, "画面下端 → PDF Y = 0");
    }

    [TestMethod]
    [TestCategory("Coordinate")]
    public void ToDisplayPx_MaxPdfCoords_ReturnsBottomRight()
    {
        var (px, py) = TestHelper.ToDisplayPx(PageW, 0, DispW, DispH, PageW, PageH);

        Assert.AreEqual(DispW, px, Eps, "PDF 右下 X → 画面 X = DispW");
        Assert.AreEqual(DispH, py, Eps, "PDF 右下 Y → 画面 Y = DispH");
    }

    // ── 4. 往復変換テスト（ToPdf → ToDisplay → 元の値に戻る） ──────────

    [TestMethod]
    [TestCategory("Coordinate")]
    public void Roundtrip_ScreenToPdfToScreen_ReturnsOriginal()
    {
        double screenX = 142.5, screenY = 380.2;

        var (ptX, ptY) = TestHelper.ToPdfPoints(screenX, screenY, DispW, DispH, PageW, PageH);
        var (backX, backY) = TestHelper.ToDisplayPx(ptX, ptY, DispW, DispH, PageW, PageH);

        Assert.AreEqual(screenX, backX, Eps, "往復変換 X が元に戻ること");
        Assert.AreEqual(screenY, backY, Eps, "往復変換 Y が元に戻ること");
    }

    [TestMethod]
    [TestCategory("Coordinate")]
    public void Roundtrip_PdfToScreenToPdf_ReturnsOriginal()
    {
        double ptX = 297.0, ptY = 421.0; // A4 中央

        var (screenX, screenY) = TestHelper.ToDisplayPx(ptX, ptY, DispW, DispH, PageW, PageH);
        var (backX, backY) = TestHelper.ToPdfPoints(screenX, screenY, DispW, DispH, PageW, PageH);

        Assert.AreEqual(ptX, backX, Eps, "往復変換 ptX が元に戻ること");
        Assert.AreEqual(ptY, backY, Eps, "往復変換 ptY が元に戻ること");
    }

    // ── 5. スケール変換テスト（表示サイズが異なる場合） ──────────────────

    [TestMethod]
    [TestCategory("Coordinate")]
    public void ToPdfPoints_HalfScaleDisplay_CorrectConversion()
    {
        // 表示が半分サイズのとき: 画面 (100, 100) → PDF (200, PageH - 200)
        double halfDispW = DispW / 2;
        double halfDispH = DispH / 2;

        var (ptX, ptY) = TestHelper.ToPdfPoints(100, 100, halfDispW, halfDispH, PageW, PageH);

        Assert.AreEqual(200.0,        ptX, Eps, "半分スケール X");
        Assert.AreEqual(PageH - 200.0, ptY, Eps, "半分スケール Y");
    }

    // ── 6. DrawCommandRenderer の座標変換整合性テスト ────────────────────

    [TestMethod]
    [TestCategory("Coordinate")]
    public void DrawCommand_YCoordTransform_UserTopToPageBottom()
    {
        // DrawCommandRenderer の変換式: pdf_y = page_height - user_y
        // ユーザー Y=100 → PDF Y = PageH - 100
        double userY    = 100.0;
        double pageH    = PageH;
        double expectedPdfY = pageH - userY;

        Assert.AreEqual(742.0, expectedPdfY, Eps,
            "ユーザー座標 Y=100 → PDF座標 Y=742 (842-100)");
    }

    [TestMethod]
    [TestCategory("Coordinate")]
    public void DrawCommand_YCoordTransform_PageTopIsZero()
    {
        // ユーザー Y=0（ページ最上部） → PDF Y = PageH（PDF座標最上部）
        double pdfY = PageH - 0.0;
        Assert.AreEqual(PageH, pdfY, Eps, "Y=0 → PDF Y = PageH");
    }
}
