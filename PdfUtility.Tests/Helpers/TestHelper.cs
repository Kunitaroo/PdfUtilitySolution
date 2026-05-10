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

    /// <summary>
    /// 標準セキュリティ（V=1, R=2, 40bit RC4）で暗号化されたミニマル PDF を返す。
    /// userPassword・ownerPassword とパーミッションフラグを引数で指定する。
    /// </summary>
    /// <param name="userPassword">ユーザーパスワード（空文字 = オープンパスワードなし）</param>
    /// <param name="ownerPassword">オーナーパスワード（空文字 = ユーザーパスワードと同じ扱い）</param>
    /// <param name="permissionFlags">/P 値（32bit signed）。-4 = 全許可。</param>
    public static byte[] CreateEncryptedPdf(
        string userPassword, string ownerPassword, int permissionFlags)
    {
        // 固定の File ID（テストの再現性のため）
        byte[] fileId = new byte[]
        {
            0x10, 0x32, 0x54, 0x76, 0x98, 0xBA, 0xDC, 0xFE,
            0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF
        };

        const int v = 1, r = 2, keyLengthBytes = 5;
        byte[] userBytes  = System.Text.Encoding.GetEncoding(28591).GetBytes(userPassword  ?? "");
        byte[] ownerBytes = System.Text.Encoding.GetEncoding(28591).GetBytes(ownerPassword ?? "");

        byte[] o = ComputeOForTest(ownerBytes, userBytes, r, keyLengthBytes);
        byte[] u = ComputeUForTest(userBytes, o, permissionFlags, fileId, r, keyLengthBytes);

        using var ms = new MemoryStream();
        void W(string s) { var b = System.Text.Encoding.GetEncoding(28591).GetBytes(s); ms.Write(b, 0, b.Length); }

        W("%PDF-1.4\n%\xff\xff\xff\xff\n");
        var offsets = new long[5];

        offsets[1] = ms.Position;
        W("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        offsets[2] = ms.Position;
        W("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        offsets[3] = ms.Position;
        W("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.28 841.89] >>\nendobj\n");

        offsets[4] = ms.Position;
        W($"4 0 obj\n<< /Filter /Standard /V {v} /R {r} /Length {keyLengthBytes * 8} /P {permissionFlags}");
        W(" /O <"); W(ToHex(o)); W(">");
        W(" /U <"); W(ToHex(u)); W(">");
        W(" >>\nendobj\n");

        long xrefOffset = ms.Position;
        W("xref\n0 5\n");
        W("0000000000 65535 f \r\n");
        for (int i = 1; i <= 4; i++) W($"{offsets[i]:D10} 00000 n \r\n");
        W("trailer\n<< /Size 5 /Root 1 0 R /Encrypt 4 0 R /ID [<");
        W(ToHex(fileId)); W("> <"); W(ToHex(fileId)); W(">] >>\n");
        W($"startxref\n{xrefOffset}\n%%EOF\n");
        return ms.ToArray();
    }

    private static string ToHex(byte[] data)
    {
        var sb = new System.Text.StringBuilder(data.Length * 2);
        foreach (byte b in data) sb.AppendFormat("{0:X2}", b);
        return sb.ToString();
    }

    // ── テスト用：標準セキュリティの /O・/U 計算（V=1/2, R=2/3 対応） ───────

    private static readonly byte[] PdfPasswordPadding =
    {
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41,
        0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80,
        0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A
    };

    private static byte[] PadPwd(byte[]? pwd)
    {
        byte[] r = new byte[32];
        int len = Math.Min(pwd?.Length ?? 0, 32);
        if (len > 0 && pwd != null) Buffer.BlockCopy(pwd, 0, r, 0, len);
        for (int i = len; i < 32; i++) r[i] = PdfPasswordPadding[i - len];
        return r;
    }

    private static byte[] Md5(byte[] data)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        return md5.ComputeHash(data);
    }

    private static byte[] Rc4(byte[] key, byte[] data)
    {
        byte[] s = new byte[256];
        for (int i = 0; i < 256; i++) s[i] = (byte)i;
        int j = 0;
        for (int i = 0; i < 256; i++)
        {
            j = (j + s[i] + key[i % key.Length]) & 0xFF;
            (s[i], s[j]) = (s[j], s[i]);
        }
        byte[] o = new byte[data.Length];
        int ii = 0, jj = 0;
        for (int k = 0; k < data.Length; k++)
        {
            ii = (ii + 1) & 0xFF;
            jj = (jj + s[ii]) & 0xFF;
            (s[ii], s[jj]) = (s[jj], s[ii]);
            o[k] = (byte)(data[k] ^ s[(s[ii] + s[jj]) & 0xFF]);
        }
        return o;
    }

    private static byte[] ComputeOForTest(byte[] ownerPwd, byte[] userPwd, int r, int keyLenBytes)
    {
        byte[] padded = PadPwd(ownerPwd != null && ownerPwd.Length > 0 ? ownerPwd : userPwd);
        byte[] hash = Md5(padded);
        if (r >= 3)
        {
            byte[] partial = new byte[keyLenBytes];
            for (int i = 0; i < 50; i++)
            {
                Buffer.BlockCopy(hash, 0, partial, 0, keyLenBytes);
                hash = Md5(partial);
            }
        }
        byte[] key = new byte[keyLenBytes];
        Buffer.BlockCopy(hash, 0, key, 0, keyLenBytes);

        byte[] paddedUser = PadPwd(userPwd);
        byte[] result = Rc4(key, paddedUser);
        if (r >= 3)
        {
            byte[] xorKey = new byte[keyLenBytes];
            for (int i = 1; i <= 19; i++)
            {
                for (int j = 0; j < keyLenBytes; j++) xorKey[j] = (byte)(key[j] ^ i);
                result = Rc4(xorKey, result);
            }
        }
        return result;
    }

    private static byte[] ComputeUForTest(byte[] userPwd, byte[] o, int p, byte[] firstId, int r, int keyLenBytes)
    {
        byte[] padded = PadPwd(userPwd);
        byte[] pBytes =
        {
            (byte)(p & 0xFF), (byte)((p >> 8) & 0xFF),
            (byte)((p >> 16) & 0xFF), (byte)((p >> 24) & 0xFF)
        };
        byte[] input = new byte[padded.Length + o.Length + 4 + firstId.Length];
        int pos = 0;
        Buffer.BlockCopy(padded, 0, input, pos, padded.Length); pos += padded.Length;
        Buffer.BlockCopy(o, 0, input, pos, o.Length); pos += o.Length;
        Buffer.BlockCopy(pBytes, 0, input, pos, 4); pos += 4;
        Buffer.BlockCopy(firstId, 0, input, pos, firstId.Length);

        byte[] hash = Md5(input);
        if (r >= 3)
        {
            byte[] partial = new byte[keyLenBytes];
            for (int i = 0; i < 50; i++)
            {
                Buffer.BlockCopy(hash, 0, partial, 0, keyLenBytes);
                hash = Md5(partial);
            }
        }
        byte[] key = new byte[keyLenBytes];
        Buffer.BlockCopy(hash, 0, key, 0, keyLenBytes);

        if (r == 2)
            return Rc4(key, PdfPasswordPadding);

        byte[] hashInput = new byte[PdfPasswordPadding.Length + firstId.Length];
        Buffer.BlockCopy(PdfPasswordPadding, 0, hashInput, 0, PdfPasswordPadding.Length);
        Buffer.BlockCopy(firstId, 0, hashInput, PdfPasswordPadding.Length, firstId.Length);
        byte[] uHash = Md5(hashInput);
        byte[] result = Rc4(key, uHash);
        byte[] xorKey2 = new byte[keyLenBytes];
        for (int i = 1; i <= 19; i++)
        {
            for (int j = 0; j < keyLenBytes; j++) xorKey2[j] = (byte)(key[j] ^ i);
            result = Rc4(xorKey2, result);
        }
        byte[] u32 = new byte[32];
        Buffer.BlockCopy(result, 0, u32, 0, Math.Min(16, result.Length));
        return u32;
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
