using System.Collections.Generic;

namespace PdfUtility.Core.Internal
{
    /// <summary>
    /// PDF に埋め込む 1 フォント分の情報とオブジェクト番号を保持する。
    /// PdfIncrementalWriter の BuildIncrementalUpdate 内で生成・使用する。
    /// </summary>
    internal class FontResource
    {
        /// <summary>PDF リソース辞書内のフォント名（例: "F0"）。</summary>
        internal string PdfName { get; set; }

        /// <summary>フォントファイルパス。</summary>
        internal string FilePath { get; set; }

        /// <summary>TTC インデックス。</summary>
        internal int TtcIndex { get; set; }

        /// <summary>展開済み TTF バイト列。</summary>
        internal byte[] TtfBytes { get; set; }

        /// <summary>
        /// PDF にフォントデータを埋め込むか否か。
        /// fsType=0x0002 (Restricted License) のフォントは false になる。
        /// </summary>
        internal bool EmbedFont { get; set; } = true;

        /// <summary>unitsPerEm（head テーブルより）。</summary>
        internal int UnitsPerEm { get; set; }

        /// <summary>PostScript 名（name テーブル nameID=6）。</summary>
        internal string PostScriptName { get; set; }

        // FontDescriptor 用メトリクス（フォントユニット）
        internal int Ascender  { get; set; }
        internal int Descender { get; set; }
        internal int CapHeight { get; set; }
        internal int[] BBox    { get; set; }   // [xMin, yMin, xMax, yMax]

        // PDF オブジェクト番号（BuildIncrementalUpdate の Phase B 冒頭で採番）
        internal int Type0ObjNum      { get; set; }
        internal int CidFontObjNum    { get; set; }
        internal int DescriptorObjNum { get; set; }
        internal int FontFile2ObjNum  { get; set; }
        internal int ToUnicodeObjNum  { get; set; }

        /// <summary>使用文字 Unicode コードポイント → グリフ ID。</summary>
        internal Dictionary<int, int> UnicodeToGlyph { get; } = new Dictionary<int, int>();

        /// <summary>グリフ ID → アドバンス幅（フォントユニット）。</summary>
        internal Dictionary<int, int> GlyphWidths { get; } = new Dictionary<int, int>();
    }
}
