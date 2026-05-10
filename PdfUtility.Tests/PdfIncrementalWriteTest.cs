using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfUtility.Core;
using PdfUtility.Core.Drawing;
using PdfUtility.Core.Exceptions;
using PdfUtility.Core.Options;
using PdfUtility.Tests.Helpers;

namespace PdfUtility.Tests;

[TestClass]
public class PdfIncrementalWriteTest
{
    private static byte[] _inputPdf = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        _inputPdf = TestHelper.GetInputPdf();
    }

    // ── 1. PDF 追記テスト ────────────────────────────────────────────────

    [TestMethod]
    [TestCategory("PdfIncremental")]
    public void IncrementalUpdate_OutputLargerThanInput()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new RectangleDrawCommand
        {
            PageNumber = 1, X = 50, Y = 50, Width = 200, Height = 100,
            BorderColor = PdfColor.Black, BorderWidth = 1.0, IsFilled = false
        });
        byte[] result = writer.BuildIncrementalUpdate();
        File.WriteAllBytes(TestHelper.OutputFile("incremental_basic.pdf"), result);

        Assert.IsTrue(result.Length > _inputPdf.Length,
            "インクリメンタル更新後のサイズ > 元サイズ");
    }

    [TestMethod]
    [TestCategory("PdfIncremental")]
    public void IncrementalUpdate_StartsWithOriginalBytes()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new RectangleDrawCommand
        {
            PageNumber = 1, X = 50, Y = 50, Width = 100, Height = 100,
            BorderColor = PdfColor.Black, BorderWidth = 1.0, IsFilled = false
        });
        byte[] result = writer.BuildIncrementalUpdate();

        // 出力の先頭は元の PDF バイト列と一致すること（インクリメンタル方式の保証）
        for (int i = 0; i < _inputPdf.Length; i++)
            Assert.AreEqual(_inputPdf[i], result[i],
                $"offset {i} で元バイト列と一致しないこと");
    }

    [TestMethod]
    [TestCategory("PdfIncremental")]
    public void IncrementalUpdate_HasIncrementalStructure()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new RectangleDrawCommand
        {
            PageNumber = 1, X = 50, Y = 50, Width = 100, Height = 100,
            BorderColor = PdfColor.Black, BorderWidth = 1.0, IsFilled = false
        });
        byte[] result = writer.BuildIncrementalUpdate();

        string incremental = System.Text.Encoding.GetEncoding(28591)
            .GetString(result, _inputPdf.Length, result.Length - _inputPdf.Length);

        // インクリメンタルアップデートは xref + trailer + startxref + %%EOF を含むこと
        Assert.IsTrue(incremental.Contains("xref\n"),    "xref セクションが含まれること");
        Assert.IsTrue(incremental.Contains("trailer\n"), "trailer が含まれること");
        Assert.IsTrue(incremental.Contains("startxref"), "startxref が含まれること");
        Assert.IsTrue(incremental.Contains("%%EOF"),     "%%EOF が含まれること");
        Assert.IsTrue(incremental.Contains("/Prev"),     "trailer に /Prev が含まれること");
    }

    [TestMethod]
    [TestCategory("PdfIncremental")]
    public void IncrementalUpdate_CanReopenResult()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);
        writer.ApplyCommand(new RectangleDrawCommand
        {
            PageNumber = 1, X = 50, Y = 50, Width = 100, Height = 100,
            BorderColor = PdfColor.Red, BorderWidth = 2.0, IsFilled = false
        });
        byte[] result = writer.BuildIncrementalUpdate();

        // 追記後の PDF を再度読み込めること
        var verify = new PdfIncrementalWriter(result);
        Assert.AreEqual(writer.PageCount, verify.PageCount, "ページ数が一致すること");
    }

    [TestMethod]
    [TestCategory("PdfIncremental")]
    public void IncrementalUpdate_PageInfoPreserved()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);
        var pageInfoBefore = writer.GetPageInfo(1);

        writer.ApplyCommand(new RectangleDrawCommand
        {
            PageNumber = 1, X = 10, Y = 10, Width = 50, Height = 50,
            BorderColor = PdfColor.Black, BorderWidth = 1.0, IsFilled = false
        });
        byte[] result = writer.BuildIncrementalUpdate();

        var verify = new PdfIncrementalWriter(result);
        var pageInfoAfter = verify.GetPageInfo(1);

        Assert.AreEqual(pageInfoBefore.Width,  pageInfoAfter.Width,  0.1, "ページ幅が保持されること");
        Assert.AreEqual(pageInfoBefore.Height, pageInfoAfter.Height, 0.1, "ページ高が保持されること");
    }

    // ── 2. 複数ページテスト ───────────────────────────────────────────────

    [TestMethod]
    [TestCategory("PdfIncremental")]
    public void MultiPage_AppendToPage2_Succeeds()
    {
        byte[] twopagePdf = TestHelper.CreateMinimalA4Pdf2Pages();
        var writer = new PdfIncrementalWriter(twopagePdf);

        Assert.AreEqual(2, writer.PageCount, "2ページPDFのページ数が2であること");

        writer.ApplyCommand(new RectangleDrawCommand
        {
            PageNumber = 2, X = 50, Y = 50, Width = 100, Height = 100,
            BorderColor = PdfColor.Blue, BorderWidth = 1.5, IsFilled = false
        });
        byte[] result = writer.BuildIncrementalUpdate();
        File.WriteAllBytes(TestHelper.OutputFile("incremental_page2.pdf"), result);

        Assert.IsTrue(result.Length > twopagePdf.Length, "追記後のサイズ > 元サイズ");

        var verify = new PdfIncrementalWriter(result);
        Assert.AreEqual(2, verify.PageCount, "追記後もページ数が2であること");
    }

    [TestMethod]
    [TestCategory("PdfIncremental")]
    public void MultiPage_AppendToBothPages_Succeeds()
    {
        byte[] twopagePdf = TestHelper.CreateMinimalA4Pdf2Pages();
        var writer = new PdfIncrementalWriter(twopagePdf);

        writer.ApplyCommands(new PdfDrawCommand[]
        {
            new RectangleDrawCommand
            {
                PageNumber=1, X=50, Y=50, Width=100, Height=50,
                BorderColor=PdfColor.Black, BorderWidth=1.0, IsFilled=false
            },
            new RectangleDrawCommand
            {
                PageNumber=2, X=50, Y=50, Width=100, Height=50,
                BorderColor=PdfColor.Red,   BorderWidth=1.0, IsFilled=false
            },
        });
        byte[] result = writer.BuildIncrementalUpdate();

        Assert.IsTrue(result.Length > twopagePdf.Length, "2ページ追記後のサイズ > 元サイズ");
    }

    [TestMethod]
    [TestCategory("PdfIncremental")]
    public void RealPdf_MultiPage_PageCountCorrect()
    {
        // testdata/PDF編集.pdf が存在する場合、ページ数が正しく取得できること
        string realPdf = Path.Combine(TestHelper.TestdataDir, "PDF編集.pdf");
        TestHelper.SkipIfNotFound(realPdf, "testdata/PDF編集.pdf");

        byte[] bytes = File.ReadAllBytes(realPdf);
        var writer = new PdfIncrementalWriter(bytes);

        Assert.IsTrue(writer.PageCount > 0, "ページ数 > 0");
        var docInfo = writer.GetDocumentInfo();
        Assert.AreEqual(writer.PageCount, docInfo.PageCount, "ドキュメント情報のページ数と一致");
    }

    // ── 3. 例外テスト ────────────────────────────────────────────────────

    [TestMethod]
    [TestCategory("PdfIncremental")]
    [ExpectedException(typeof(PdfLoadException))]
    public void Load_EmptyBytes_ThrowsPdfLoadException()
    {
        _ = new PdfIncrementalWriter(Array.Empty<byte>());
    }

    [TestMethod]
    [TestCategory("PdfIncremental")]
    [ExpectedException(typeof(PdfLoadException))]
    public void Load_InvalidPdfBytes_ThrowsPdfLoadException()
    {
        byte[] garbage = System.Text.Encoding.ASCII.GetBytes(
            "This is not a PDF file at all.");
        _ = new PdfIncrementalWriter(garbage);
    }

    [TestMethod]
    [TestCategory("PdfIncremental")]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Load_NullBytes_ThrowsArgumentNullException()
    {
        _ = new PdfIncrementalWriter(null!);
    }

    [TestMethod]
    [TestCategory("PdfIncremental")]
    public void ApplyCommand_InvalidPageNumber_ThrowsException()
    {
        var writer = new PdfIncrementalWriter(_inputPdf);
        bool threw = false;
        try
        {
            writer.ApplyCommand(new RectangleDrawCommand
            {
                PageNumber = 9999, X = 0, Y = 0, Width = 10, Height = 10,
                BorderColor = PdfColor.Black, BorderWidth = 1.0
            });
        }
        catch (Exception)
        {
            threw = true;
        }
        Assert.IsTrue(threw, "存在しないページ番号では例外が発生すること");
    }

    // ── testdata 生成（一度実行すれば testdata/ に PDF が出力される） ────────

    [TestMethod]
    [TestCategory("TestDataSetup")]
    public void _GenerateEncryptionTestPdfs()
    {
        Directory.CreateDirectory(TestHelper.TestdataDir);

        byte[] encrypted = TestHelper.CreateEncryptedPdf(
            userPassword:    "secret123",
            ownerPassword:   "secret123",
            permissionFlags: -4);
        File.WriteAllBytes(Path.Combine(TestHelper.TestdataDir, "encrypted.pdf"), encrypted);

        byte[] permissionLocked = TestHelper.CreateEncryptedPdf(
            userPassword:    "",
            ownerPassword:   "ownerpass",
            permissionFlags: unchecked((int)0xFFFFFBD4));
        File.WriteAllBytes(Path.Combine(TestHelper.TestdataDir, "permission_locked.pdf"), permissionLocked);
    }

    // ── 4. 暗号化・パーミッションPDFテスト ─────────────────────────────────

    [TestMethod]
    [TestCategory("PdfIncremental")]
    public void Load_EncryptedPdf_ThrowsPdfLoadException()
    {
        // オープンパスワード付き暗号化PDF（ユーザー/オーナー両方に "secret123"）
        byte[] encryptedPdf = TestHelper.CreateEncryptedPdf(
            userPassword:    "secret123",
            ownerPassword:   "secret123",
            permissionFlags: -4); // 全許可

        var ex = Assert.ThrowsException<PdfLoadException>(
            () => new PdfIncrementalWriter(encryptedPdf));
        StringAssert.Contains(ex.Message, "パスワード保護");
    }

    [TestMethod]
    [TestCategory("PdfIncremental")]
    public void Load_PermissionLockedPdf_ThrowsPdfLoadException()
    {
        // パスワード無し・編集禁止のPDF（modify(0x08) と modify_annotations(0x20) と assemble(0x400) をクリア）
        // P = -1068 = 0xFFFFFBD4
        byte[] permissionPdf = TestHelper.CreateEncryptedPdf(
            userPassword:    "",
            ownerPassword:   "ownerpass",
            permissionFlags: unchecked((int)0xFFFFFBD4));

        var ex = Assert.ThrowsException<PdfLoadException>(
            () => new PdfIncrementalWriter(permissionPdf));
        StringAssert.Contains(ex.Message, "編集が禁止");
    }

    [TestMethod]
    [TestCategory("PdfIncremental")]
    public void Load_PermissionLockedPdf_WithRejectFalse_ContinuesProcessing()
    {
        byte[] permissionPdf = TestHelper.CreateEncryptedPdf(
            userPassword:    "",
            ownerPassword:   "ownerpass",
            permissionFlags: unchecked((int)0xFFFFFBD4));

        var options = new PdfUtilityOptions { RejectPermissionLockedPdf = false };

        // 例外が発生せず、Writerが構築できることを検証
        var writer = new PdfIncrementalWriter(permissionPdf, options);
        Assert.IsNotNull(writer, "RejectPermissionLockedPdf=false なら Load が成功すること");
        Assert.AreEqual(1, writer.PageCount, "ページ数が取得できること");
    }
}
