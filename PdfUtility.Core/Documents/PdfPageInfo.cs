namespace PdfUtility.Core.Documents
{
    /// <summary>
    /// PDFページのサイズ情報。
    /// 単位はポイント（1pt = 1/72インチ）。
    /// </summary>
    public class PdfPageInfo
    {
        /// <summary>1始まりのページ番号。</summary>
        public int PageNumber { get; set; }

        /// <summary>ページ幅（ポイント）。</summary>
        public double Width { get; set; }

        /// <summary>ページ高さ（ポイント）。</summary>
        public double Height { get; set; }

        /// <summary>A4縦（210×297mm）かどうか。</summary>
        public bool IsA4Portrait => Width < Height && Width >= 594 && Width <= 596 && Height >= 841 && Height <= 843;
    }
}
