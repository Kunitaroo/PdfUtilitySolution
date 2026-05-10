using System.Collections.Generic;
using PdfUtility.Core.Documents;

namespace PdfUtility.Core.Internal
{
    /// <summary>
    /// PdfRawParserが解析した結果を保持する内部コンテキスト。
    /// PdfIncrementalWriterがインクリメンタルアップデートを生成する際に参照する。
    /// </summary>
    internal class PdfDocumentContext
    {
        /// <summary>元PDFの全バイト列（読み取り専用）。</summary>
        internal byte[] OriginalBytes { get; set; }

        /// <summary>最新（最後の）xref開始オフセット。trailerの/Prevに書く値。</summary>
        internal long LatestXrefOffset { get; set; }

        /// <summary>全オブジェクトのxrefエントリ（objNum → XrefEntry）。</summary>
        internal Dictionary<int, XrefEntry> XrefEntries { get; set; }

        /// <summary>trailerの/Sizeの値。次のオブジェクト番号採番の基準。</summary>
        internal int TotalObjectCount { get; set; }

        /// <summary>DocumentCatalog（/Root）への間接参照。</summary>
        internal PdfRef RootRef { get; set; }

        /// <summary>ページ情報リスト（インデックス0 = ページ1）。</summary>
        internal List<InternalPageInfo> Pages { get; set; }

        /// <summary>暗号化情報。/Encrypt が無いPDFでは null。</summary>
        internal PdfEncryptionInfo Encryption { get; set; }
    }

    /// <summary>
    /// /Encrypt 辞書から取得した暗号化メタ情報。
    /// </summary>
    internal class PdfEncryptionInfo
    {
        /// <summary>セキュリティハンドラー名（通常は "Standard"）。</summary>
        internal string Filter { get; set; }

        /// <summary>暗号化アルゴリズムバージョン（V）。</summary>
        internal int V { get; set; }

        /// <summary>標準セキュリティハンドラーのリビジョン（R）。</summary>
        internal int R { get; set; }

        /// <summary>キー長（ビット）。</summary>
        internal int KeyLengthBits { get; set; }

        /// <summary>パーミッションフラグ（P）。32bit 符号付き。</summary>
        internal int Permissions { get; set; }

        /// <summary>オーナーパスワードハッシュ（O）。32 バイト。</summary>
        internal byte[] OwnerHash { get; set; }

        /// <summary>ユーザーパスワードハッシュ（U）。32 バイト。</summary>
        internal byte[] UserHash { get; set; }

        /// <summary>トレイラー /ID 配列の先頭要素（ファイルID）。</summary>
        internal byte[] FileId { get; set; }

        /// <summary>
        /// 空のオープンパスワード（パスワードなし）で開けるかどうか。
        /// true なら「パーミッション専用」、false なら「オープンパスワード必須」。
        /// </summary>
        internal bool IsOpenableWithoutPassword { get; set; }

        /// <summary>
        /// 編集・変更系の制限が掛かっているかどうか。/P の bit 4（modify）または bit 6（modify annotations）が無効。
        /// </summary>
        internal bool HasEditRestrictions { get; set; }
    }

    /// <summary>
    /// xrefエントリ1件分の情報。
    /// </summary>
    internal struct XrefEntry
    {
        /// <summary>オブジェクトのバイトオフセット（type=1）。</summary>
        internal long Offset { get; set; }

        /// <summary>世代番号。</summary>
        internal int Generation { get; set; }

        /// <summary>使用中（n）かフリー（f）か。</summary>
        internal bool InUse { get; set; }

        /// <summary>xrefストリームのtype=2（オブジェクトストリーム内）の場合true。</summary>
        internal bool IsCompressed { get; set; }

        /// <summary>オブジェクトストリームのオブジェクト番号（IsCompressed=trueの場合）。</summary>
        internal int StreamObjNum { get; set; }

        /// <summary>オブジェクトストリーム内のインデックス（IsCompressed=trueの場合）。</summary>
        internal int IndexInStream { get; set; }
    }

    /// <summary>
    /// PDF間接参照（X Y R）。
    /// </summary>
    internal struct PdfRef
    {
        internal int ObjectNumber { get; set; }
        internal int Generation { get; set; }

        public override string ToString() => $"{ObjectNumber} {Generation} R";
    }

    /// <summary>
    /// 内部で保持するページ情報。
    /// </summary>
    internal class InternalPageInfo
    {
        /// <summary>1始まりのページ番号。</summary>
        internal int PageNumber { get; set; }

        /// <summary>ページオブジェクトのオブジェクト番号。</summary>
        internal int ObjectNumber { get; set; }

        /// <summary>ページオブジェクトの世代番号。</summary>
        internal int Generation { get; set; }

        /// <summary>MediaBox [llx lly urx ury]。</summary>
        internal double[] MediaBox { get; set; }

        /// <summary>ページ幅（ポイント）。</summary>
        internal double Width => MediaBox != null && MediaBox.Length == 4 ? MediaBox[2] - MediaBox[0] : 595.28;

        /// <summary>ページ高さ（ポイント）。</summary>
        internal double Height => MediaBox != null && MediaBox.Length == 4 ? MediaBox[3] - MediaBox[1] : 841.89;

        /// <summary>既存の/Contentsの値（null、PdfRef、またはPdfRef[]）。</summary>
        internal object ExistingContents { get; set; }
    }
}
