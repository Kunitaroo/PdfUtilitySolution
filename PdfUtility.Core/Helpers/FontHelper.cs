using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PdfUtility.Core.Exceptions;

namespace PdfUtility.Core.Helpers
{
    /// <summary>
    /// TTC / TTF フォントのメモリ展開・テーブル解析を担当するヘルパー。
    /// ファイルへの書き出しは一切行わない。
    /// </summary>
    public static class FontHelper
    {
        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// TTC または TTF ファイルを読み込み、単独の TTF バイト列を返す。
        /// TTC の場合はメモリ上で指定インデックスのフォントを展開する。
        /// </summary>
        public static byte[] LoadFontBytes(string fontPath, int ttcIndex = 0)
        {
            if (!File.Exists(fontPath))
                throw new PdfLoadException($"フォントファイルが見つかりません: {fontPath}");

            string ext = Path.GetExtension(fontPath).ToLowerInvariant();
            byte[] data = File.ReadAllBytes(fontPath);

            if (ext == ".ttc")
                return ExtractTtfFromTtc(data, ttcIndex);
            if (ext == ".ttf" || ext == ".otf")
                return data;

            throw new PdfLoadException($"未対応のフォント形式: {ext}");
        }

        /// <summary>
        /// TTC バイト列から指定インデックスの TTF バイト列をメモリ上で展開して返す。
        /// オフセットを再計算するため、得られたバイト列は単独の TTF として有効。
        /// </summary>
        public static byte[] ExtractTtfFromTtc(byte[] ttcData, int fontIndex)
        {
            if (ttcData.Length < 12)
                throw new PdfException("TTCファイルが不正です（ヘッダが短い）");

            string sig = Encoding.ASCII.GetString(ttcData, 0, 4);
            if (sig != "ttcf")
                throw new PdfException($"TTCシグネチャが不正: {sig}");

            int numFonts = (int)ReadU32(ttcData, 8);
            if (fontIndex < 0 || fontIndex >= numFonts)
                throw new PdfException($"TTCインデックス {fontIndex} は範囲外です（フォント数: {numFonts}）");

            int fontOffset = (int)ReadU32(ttcData, 12 + fontIndex * 4);
            if (fontOffset + 12 > ttcData.Length)
                throw new PdfException("フォントオフセットが不正です");

            int numTables = ReadU16(ttcData, fontOffset + 4);

            // テーブルディレクトリを収集（オフセットは TTC 先頭からの絶対値）
            var tables = new List<TableEntry>(numTables);
            int tableDirStart = fontOffset + 12;
            for (int i = 0; i < numTables; i++)
            {
                int e = tableDirStart + i * 16;
                tables.Add(new TableEntry
                {
                    Tag      = Encoding.ASCII.GetString(ttcData, e, 4),
                    CheckSum = ReadU32(ttcData, e + 4),
                    SrcOffset = (int)ReadU32(ttcData, e + 8),
                    Length   = (int)ReadU32(ttcData, e + 12),
                });
            }

            // 新しい TTF を構築
            using (var ms = new MemoryStream())
            {
                // Offset Table（12 バイト）をそのままコピー
                ms.Write(ttcData, fontOffset, 12);

                // テーブルディレクトリ領域を仮確保（後で書き直す）
                int dirPos = (int)ms.Position;
                byte[] emptyDir = new byte[numTables * 16];
                ms.Write(emptyDir, 0, emptyDir.Length);

                // 各テーブルのデータを書き出し、新オフセットを記録
                int[] newOffsets = new int[tables.Count];
                for (int i = 0; i < tables.Count; i++)
                {
                    newOffsets[i] = (int)ms.Position;
                    ms.Write(ttcData, tables[i].SrcOffset, tables[i].Length);
                    // 4 バイト境界にパディング
                    int pad = (4 - tables[i].Length % 4) % 4;
                    for (int p = 0; p < pad; p++) ms.WriteByte(0);
                }

                // バイト配列化してからテーブルディレクトリを書き直す
                byte[] result = ms.ToArray();
                for (int i = 0; i < tables.Count; i++)
                {
                    int de = dirPos + i * 16;
                    byte[] tagB = Encoding.ASCII.GetBytes(tables[i].Tag);
                    Array.Copy(tagB, 0, result, de, 4);
                    WriteU32(result, de + 4,  tables[i].CheckSum);
                    WriteU32(result, de + 8,  (uint)newOffsets[i]);
                    WriteU32(result, de + 12, (uint)tables[i].Length);
                }

                return result;
            }
        }

        /// <summary>
        /// Unicode コードポイント → グリフ ID（cmap format 4 を解析）。
        /// 対応グリフがない場合は 0（.notdef）を返す。
        /// </summary>
        public static int GetGlyphId(byte[] ttfData, int unicode)
        {
            int cmapOff = FindTable(ttfData, "cmap");
            if (cmapOff < 0)
                throw new PdfException("cmapテーブルが見つかりません");

            int numSubtables = ReadU16(ttfData, cmapOff + 2);
            int bestOff = -1;

            // platformID=3, encodingID=1 (Windows Unicode BMP) を優先
            for (int i = 0; i < numSubtables; i++)
            {
                int rec = cmapOff + 4 + i * 8;
                int pid = ReadU16(ttfData, rec);
                int eid = ReadU16(ttfData, rec + 2);
                int off = cmapOff + (int)ReadU32(ttfData, rec + 4);
                if (pid == 3 && eid == 1) { bestOff = off; break; }
                if (pid == 0 && bestOff < 0) bestOff = off;
            }

            if (bestOff < 0)
                throw new PdfException("対応するcmapサブテーブルが見つかりません");

            int format = ReadU16(ttfData, bestOff);
            if (format != 4)
                throw new PdfException($"cmap format {format} は未対応（format 4 のみ対応）");

            return ParseCmapFormat4(ttfData, bestOff, unicode);
        }

        /// <summary>
        /// グリフ ID → アドバンス幅（フォントユニット単位、hmtx テーブル参照）。
        /// </summary>
        public static int GetAdvanceWidth(byte[] ttfData, int glyphId)
        {
            int hheaOff = FindTable(ttfData, "hhea");
            if (hheaOff < 0)
                throw new PdfException("hheaテーブルが見つかりません");

            int numberOfHMetrics = ReadU16(ttfData, hheaOff + 34);

            int hmtxOff = FindTable(ttfData, "hmtx");
            if (hmtxOff < 0)
                throw new PdfException("hmtxテーブルが見つかりません");

            // numberOfHMetrics より大きいグリフには最後の advanceWidth が適用される
            int idx = glyphId < numberOfHMetrics ? glyphId : numberOfHMetrics - 1;
            return ReadU16(ttfData, hmtxOff + idx * 4);
        }

        /// <summary>
        /// head テーブルから unitsPerEm を取得する。
        /// </summary>
        public static int GetUnitsPerEm(byte[] ttfData)
        {
            int headOff = FindTable(ttfData, "head");
            if (headOff < 0)
                throw new PdfException("headテーブルが見つかりません");
            return ReadU16(ttfData, headOff + 18);
        }

        /// <summary>
        /// name テーブルから PostScript 名（nameID=6）を取得する。
        /// </summary>
        public static string GetPostScriptName(byte[] ttfData)
        {
            int nameOff = FindTable(ttfData, "name");
            if (nameOff < 0) return "UnknownFont";

            int count      = ReadU16(ttfData, nameOff + 2);
            int strOff     = ReadU16(ttfData, nameOff + 4);
            int storageBase = nameOff + strOff;

            string macResult = null;
            for (int i = 0; i < count; i++)
            {
                int rec = nameOff + 6 + i * 12;
                if (rec + 12 > ttfData.Length) break;
                int pid    = ReadU16(ttfData, rec);
                int eid    = ReadU16(ttfData, rec + 2);
                int nameId = ReadU16(ttfData, rec + 6);
                int len    = ReadU16(ttfData, rec + 8);
                int soff   = ReadU16(ttfData, rec + 10);
                if (nameId != 6) continue;

                int strStart = storageBase + soff;
                if (strStart + len > ttfData.Length) continue;

                if (pid == 3 && eid == 1)
                    return Encoding.BigEndianUnicode.GetString(ttfData, strStart, len);
                if (pid == 1 && macResult == null)
                    macResult = Encoding.GetEncoding(28591).GetString(ttfData, strStart, len);
            }
            return macResult ?? "UnknownFont";
        }

        /// <summary>
        /// OS/2 または hhea テーブルから Ascender / Descender / CapHeight（フォントユニット）を取得する。
        /// </summary>
        public static void GetAscenderDescender(byte[] ttfData,
            out int ascender, out int descender, out int capHeight)
        {
            int upm = GetUnitsPerEm(ttfData);
            int os2Off = FindTable(ttfData, "OS/2");
            if (os2Off >= 0 && os2Off + 78 <= ttfData.Length)
            {
                int version = ReadU16(ttfData, os2Off);
                ascender  = ReadS16(ttfData, os2Off + 68);
                descender = ReadS16(ttfData, os2Off + 70);
                capHeight = (version >= 2 && os2Off + 90 <= ttfData.Length)
                    ? ReadS16(ttfData, os2Off + 88)
                    : (int)(ascender * 0.7);
                return;
            }
            int hheaOff = FindTable(ttfData, "hhea");
            if (hheaOff >= 0 && hheaOff + 8 <= ttfData.Length)
            {
                ascender  = ReadS16(ttfData, hheaOff + 4);
                descender = ReadS16(ttfData, hheaOff + 6);
                capHeight = (int)(ascender * 0.7);
                return;
            }
            ascender  = upm * 8 / 10;
            descender = -(upm * 2 / 10);
            capHeight = upm * 7 / 10;
        }

        /// <summary>
        /// OS/2 テーブルから fsType フィールドを取得する。
        /// 0x0002 (Restricted License) は PDF へのフォント埋め込みが禁止されている。
        /// </summary>
        public static ushort GetFsType(byte[] ttfData)
        {
            int os2Off = FindTable(ttfData, "OS/2");
            if (os2Off < 0 || os2Off + 10 > ttfData.Length) return 0;
            return ReadU16(ttfData, os2Off + 8);
        }

        /// <summary>
        /// head テーブルから FontBBox [xMin, yMin, xMax, yMax]（フォントユニット）を取得する。
        /// </summary>
        public static int[] GetBoundingBox(byte[] ttfData)
        {
            int headOff = FindTable(ttfData, "head");
            if (headOff < 0 || headOff + 44 > ttfData.Length)
                return new[] { -200, -200, 1200, 1000 };
            return new[]
            {
                (int)ReadS16(ttfData, headOff + 36),
                (int)ReadS16(ttfData, headOff + 38),
                (int)ReadS16(ttfData, headOff + 40),
                (int)ReadS16(ttfData, headOff + 42),
            };
        }

        // ── Private: cmap format 4 解析 ──────────────────────────────────────

        private static int ParseCmapFormat4(byte[] data, int offset, int unicode)
        {
            int segCountX2  = ReadU16(data, offset + 6);
            int segCount    = segCountX2 / 2;

            // 各配列の先頭バイト位置
            int endBase      = offset + 14;
            int startBase    = endBase   + segCountX2 + 2; // +2 は reservedPad
            int deltaBase    = startBase + segCountX2;
            int rangeOffBase = deltaBase + segCountX2;

            for (int i = 0; i < segCount; i++)
            {
                int endCode = ReadU16(data, endBase + i * 2);
                if (unicode > endCode) continue;

                int startCode = ReadU16(data, startBase + i * 2);
                if (unicode < startCode) return 0; // マッピングなし

                int idDelta      = ReadS16(data, deltaBase + i * 2);
                int idRangeOff   = ReadU16(data, rangeOffBase + i * 2);

                if (idRangeOff == 0)
                    return (unicode + idDelta) & 0xFFFF;

                // idRangeOffset は &idRangeOffset[i] からの相対バイトオフセット
                int glyphPtrAddr = (rangeOffBase + i * 2) + idRangeOff + (unicode - startCode) * 2;
                int glyphId      = ReadU16(data, glyphPtrAddr);
                if (glyphId == 0) return 0;
                return (glyphId + idDelta) & 0xFFFF;
            }

            return 0;
        }

        // ── Private: TTF テーブル検索 ─────────────────────────────────────────

        private static int FindTable(byte[] ttfData, string tag)
        {
            if (ttfData.Length < 12) return -1;
            int numTables = ReadU16(ttfData, 4);
            for (int i = 0; i < numTables; i++)
            {
                int e = 12 + i * 16;
                if (e + 16 > ttfData.Length) break;
                if (Encoding.ASCII.GetString(ttfData, e, 4) == tag)
                    return (int)ReadU32(ttfData, e + 8);
            }
            return -1;
        }

        // ── Private: バイナリ読み書き（ビッグエンディアン）──────────────────

        private static ushort ReadU16(byte[] d, int i)
            => (ushort)((d[i] << 8) | d[i + 1]);

        private static uint ReadU32(byte[] d, int i)
            => ((uint)d[i] << 24) | ((uint)d[i + 1] << 16) | ((uint)d[i + 2] << 8) | d[i + 3];

        private static short ReadS16(byte[] d, int i)
            => (short)((d[i] << 8) | d[i + 1]);

        private static void WriteU32(byte[] d, int i, uint v)
        {
            d[i]     = (byte)(v >> 24);
            d[i + 1] = (byte)(v >> 16);
            d[i + 2] = (byte)(v >> 8);
            d[i + 3] = (byte)v;
        }

        // ── Private: 内部構造体 ───────────────────────────────────────────────

        private struct TableEntry
        {
            public string Tag;
            public uint   CheckSum;
            public int    SrcOffset;
            public int    Length;
        }
    }
}
