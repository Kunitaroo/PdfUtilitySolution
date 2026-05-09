using System.IO.Compression;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfUtility.Core.Drawing;

namespace PdfUtility.Tests.Helpers;

/// <summary>
/// テスト全体で共有するユーティリティ。
/// </summary>
internal static class TestHelper
{
    // ── ディレクトリ解決 ─────────────────────────────────────────────────

    private static string? _solutionDir;

    public static string SolutionDir
    {
        get
        {
            if (_solutionDir != null) return _solutionDir;
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.GetFiles(dir.FullName, "*.sln").Length > 0)
                {
                    _solutionDir = dir.FullName;
                    return _solutionDir;
                }
                dir = dir.Parent;
            }
            _solutionDir = AppContext.BaseDirectory;
            return _solutionDir;
        }
    }

    public static string TestdataDir => Path.Combine(SolutionDir, "testdata");

    public static string OutputDir
    {
        get
        {
            string dir = Path.Combine(SolutionDir, "output", "tests");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string OutputFile(string filename) => Path.Combine(OutputDir, filename);

    // ── フォントパス解決 ──────────────────────────────────────────────────

    /// <summary>
    /// フォントファイルパスを返す。testdata/fonts/ → C:\Windows\Fonts\ の順で探す。
    /// 見つからなければ null。
    /// </summary>
    public static string? GetFontPath(string filename)
    {
        string candidate1 = Path.Combine(TestdataDir, "fonts", filename);
        if (File.Exists(candidate1)) return candidate1;

        string candidate2 = Path.Combine(@"C:\Windows\Fonts", filename);
        if (File.Exists(candidate2)) return candidate2;

        return null;
    }

    /// <summary>
    /// パスが null または存在しないなら Assert.Inconclusive でテストをスキップする。
    /// </summary>
    public static void SkipIfNotFound(string? path, string description)
    {
        if (path == null || !File.Exists(path))
            Assert.Inconclusive($"テストに必要なファイルが見つかりません: {description}");
    }

    // ── テスト用 PDF 生成 ────────────────────────────────────────────────

    /// <summary>
    /// テスト用ミニマル A4 PDF（1ページ・空コンテンツ）を返す。
    /// testdata/PDF編集.pdf がある場合はそちらを優先する。
    /// </summary>
    public static byte[] GetInputPdf()
    {
        string realPdf = Path.Combine(TestdataDir, "PDF編集.pdf");
        if (File.Exists(realPdf)) return File.ReadAllBytes(realPdf);
        return CreateMinimalA4Pdf();
    }

    /// <summary>ミニマル A4 PDF を返す（1ページ・コンテンツなし）。</summary>
    public static byte[] CreateMinimalA4Pdf()
    {
        using var ms = new MemoryStream();
        void W(string s) { var b = System.Text.Encoding.ASCII.GetBytes(s); ms.Write(b, 0, b.Length); }

        W("%PDF-1.4\n%\xff\xff\xff\xff\n");
        var offsets = new long[4];

        offsets[1] = ms.Position;
        W("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        offsets[2] = ms.Position;
        W("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        offsets[3] = ms.Position;
        W("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.28 841.89] >>\nendobj\n");

        long xrefOffset = ms.Position;
        W("xref\n0 4\n");
        W("0000000000 65535 f \r\n");
        for (int i = 1; i <= 3; i++) W($"{offsets[i]:D10} 00000 n \r\n");
        W("trailer\n<< /Size 4 /Root 1 0 R >>\n");
        W($"startxref\n{xrefOffset}\n%%EOF\n");
        return ms.ToArray();
    }

    /// <summary>ミニマル A4 PDF を返す（2ページ）。</summary>
    public static byte[] CreateMinimalA4Pdf2Pages()
    {
        using var ms = new MemoryStream();
        void W(string s) { var b = System.Text.Encoding.ASCII.GetBytes(s); ms.Write(b, 0, b.Length); }

        W("%PDF-1.4\n%\xff\xff\xff\xff\n");
        var offsets = new long[6];

        offsets[1] = ms.Position;
        W("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        offsets[2] = ms.Position;
        W("2 0 obj\n<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>\nendobj\n");

        offsets[3] = ms.Position;
        W("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.28 841.89] >>\nendobj\n");

        offsets[4] = ms.Position;
        W("4 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.28 841.89] >>\nendobj\n");

        long xrefOffset = ms.Position;
        W("xref\n0 5\n");
        W("0000000000 65535 f \r\n");
        for (int i = 1; i <= 4; i++) W($"{offsets[i]:D10} 00000 n \r\n");
        W("trailer\n<< /Size 5 /Root 1 0 R >>\n");
        W($"startxref\n{xrefOffset}\n%%EOF\n");
        return ms.ToArray();
    }

    // ── テスト用画像生成 ─────────────────────────────────────────────────

    /// <summary>単色 PNG バイト列を生成する。</summary>
    public static byte[] CreateSolidColorPng(int width, int height, byte r, byte g, byte b)
    {
        int stride = width * 3;
        byte[] rawData = new byte[(1 + stride) * height];
        int pos = 0;
        for (int row = 0; row < height; row++)
        {
            rawData[pos++] = 0; // フィルタなし
            for (int col = 0; col < width; col++)
            {
                rawData[pos++] = r;
                rawData[pos++] = g;
                rawData[pos++] = b;
            }
        }
        return BuildPng(width, height, 8, 2, rawData);
    }

    /// <summary>
    /// 最小限有効な JPEG バイト列を生成する（幅・高さを埋め込み）。
    /// </summary>
    public static byte[] CreateMinimalJpeg(int width = 10, int height = 10)
    {
        // SOI + SOF0 + EOI の最小 JPEG 構造
        var bytes = new List<byte>
        {
            0xFF, 0xD8,             // SOI
            0xFF, 0xC0,             // SOF0
            0x00, 0x0B,             // segment length = 11
            0x08,                   // precision = 8
            (byte)(height >> 8), (byte)(height & 0xFF),  // height
            (byte)(width  >> 8), (byte)(width  & 0xFF),  // width
            0x03,                   // components = 3
            0x01, 0x11, 0x00,       // component 1
            0x02, 0x11, 0x01,       // component 2
            0x03, 0x11, 0x01,       // component 3
            0xFF, 0xD9              // EOI
        };
        return bytes.ToArray();
    }

    // ── fsType パッチヘルパー ─────────────────────────────────────────────

    /// <summary>
    /// TTF バイト列の OS/2 テーブルの fsType を指定値に書き換えたコピーを返す。
    /// </summary>
    public static byte[] PatchFsType(byte[] ttfData, ushort newFsType)
    {
        var patched = (byte[])ttfData.Clone();
        if (patched.Length < 12) return patched;

        int numTables = (patched[4] << 8) | patched[5];
        for (int i = 0; i < numTables; i++)
        {
            int e = 12 + i * 16;
            if (e + 16 > patched.Length) break;
            if (System.Text.Encoding.ASCII.GetString(patched, e, 4) != "OS/2") continue;
            int os2Off = (int)(((uint)patched[e + 8] << 24) | ((uint)patched[e + 9] << 16)
                             | ((uint)patched[e + 10] << 8) | patched[e + 11]);
            if (os2Off + 10 <= patched.Length)
            {
                patched[os2Off + 8] = (byte)(newFsType >> 8);
                patched[os2Off + 9] = (byte)(newFsType & 0xFF);
            }
            return patched;
        }
        return patched;
    }

    // ── 座標変換ヘルパー ─────────────────────────────────────────────────

    /// <summary>
    /// 画面座標（WPF論理px・左上原点）→ PDF座標（ポイント・左下原点）。
    /// </summary>
    public static (double x, double y) ToPdfPoints(
        double screenX, double screenY,
        double displayWidth, double displayHeight,
        double pageWidth, double pageHeight)
    {
        double ptX = screenX * pageWidth / displayWidth;
        double ptY = pageHeight - screenY * pageHeight / displayHeight;
        return (Math.Clamp(ptX, 0, pageWidth), Math.Clamp(ptY, 0, pageHeight));
    }

    /// <summary>
    /// PDF座標（ポイント・左下原点）→ 画面座標（WPF論理px・左上原点）。
    /// </summary>
    public static (double x, double y) ToDisplayPx(
        double ptX, double ptY,
        double displayWidth, double displayHeight,
        double pageWidth, double pageHeight)
    {
        double px = ptX * displayWidth / pageWidth;
        double py = (pageHeight - ptY) * displayHeight / pageHeight;
        return (px, py);
    }

    // ── PNG ビルダー（内部） ──────────────────────────────────────────────

    private static byte[] BuildPng(int width, int height, int bitDepth, int colorType, byte[] rawPixelData)
    {
        byte[] idatData = CompressZlib(rawPixelData);
        using var ms = new MemoryStream();
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);

        WritePngChunk(ms, "IHDR", new byte[]
        {
            (byte)(width  >> 24), (byte)(width  >> 16), (byte)(width  >> 8), (byte)width,
            (byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height,
            (byte)bitDepth, (byte)colorType, 0, 0, 0
        });
        WritePngChunk(ms, "IDAT", idatData);
        WritePngChunk(ms, "IEND", Array.Empty<byte>());
        return ms.ToArray();
    }

    private static void WritePngChunk(MemoryStream ms, string type, byte[] data)
    {
        int len = data.Length;
        ms.WriteByte((byte)(len >> 24)); ms.WriteByte((byte)(len >> 16));
        ms.WriteByte((byte)(len >> 8));  ms.WriteByte((byte)len);
        byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        ms.Write(typeBytes, 0, 4);
        if (data.Length > 0) ms.Write(data, 0, data.Length);
        byte[] crcInput = new byte[4 + data.Length];
        Array.Copy(typeBytes, crcInput, 4);
        Array.Copy(data, 0, crcInput, 4, data.Length);
        uint crc = ComputeCrc32(crcInput);
        ms.WriteByte((byte)(crc >> 24)); ms.WriteByte((byte)(crc >> 16));
        ms.WriteByte((byte)(crc >> 8));  ms.WriteByte((byte)crc);
    }

    private static byte[] CompressZlib(byte[] data)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0x78); ms.WriteByte(0x9C);
        using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(data, 0, data.Length);
        uint adler = ComputeAdler32(data);
        ms.WriteByte((byte)(adler >> 24)); ms.WriteByte((byte)(adler >> 16));
        ms.WriteByte((byte)(adler >> 8));  ms.WriteByte((byte)adler);
        return ms.ToArray();
    }

    private static uint ComputeAdler32(byte[] data)
    {
        const uint MOD = 65521;
        uint a = 1, b = 0;
        foreach (byte bt in data) { a = (a + bt) % MOD; b = (b + a) % MOD; }
        return (b << 16) | a;
    }

    private static uint ComputeCrc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte bt in data)
        {
            crc ^= bt;
            for (int k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return ~crc;
    }
}
