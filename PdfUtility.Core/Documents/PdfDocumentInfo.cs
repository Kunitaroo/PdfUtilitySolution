using System.Collections.Generic;

namespace PdfUtility.Core.Documents
{
    /// <summary>
    /// 読み込んだPDFの文書レベル情報。
    /// </summary>
    public class PdfDocumentInfo
    {
        /// <summary>総ページ数。</summary>
        public int PageCount { get; set; }

        /// <summary>各ページのサイズ情報（インデックス0 = ページ1）。</summary>
        public IReadOnlyList<PdfPageInfo> Pages { get; set; }

        /// <summary>PDFバージョン文字列（例: "1.7"）。</summary>
        public string PdfVersion { get; set; }
    }
}
