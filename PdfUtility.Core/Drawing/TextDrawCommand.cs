namespace PdfUtility.Core.Drawing
{
    /// <summary>
    /// テキスト描画命令。
    /// X,Y はページ左上原点でテキストのベースライン位置を指定する。
    /// Width > 0 のとき HorizontalAlign の基準ボックスとして使用する。
    /// </summary>
    public class TextDrawCommand : PdfDrawCommand
    {
        /// <summary>描画するテキスト文字列。</summary>
        public string Text { get; set; }

        /// <summary>
        /// テキストボックスの幅（pt）。水平配置の基準。
        /// 0 以下の場合は Left 固定扱い（X が描画開始位置）。
        /// </summary>
        public double Width { get; set; }

        /// <summary>フォントファイルのパス（TTF / TTC）。必須。</summary>
        public string FontFilePath { get; set; }

        /// <summary>TTC 内のフォントインデックス（0 始まり）。TTF の場合は無視。</summary>
        public int TtcFontIndex { get; set; }

        /// <summary>表示用フォント名（PDF リソースキーにも使用）。省略時は "F" + 連番。</summary>
        public string FontName { get; set; }

        /// <summary>フォントサイズ（pt）。</summary>
        public double FontSize { get; set; } = 10.0;

        /// <summary>文字色。null の場合は黒。</summary>
        public PdfColor FontColor { get; set; }

        /// <summary>水平配置。Width > 0 のとき有効。</summary>
        public PdfHorizontalAlign HorizontalAlign { get; set; } = PdfHorizontalAlign.Left;

        /// <summary>垂直配置（将来拡張用。現バージョンでは未使用）。</summary>
        public PdfVerticalAlign VerticalAlign { get; set; } = PdfVerticalAlign.Top;

        /// <summary>太字（将来拡張用。現バージョンでは未使用）。</summary>
        public bool IsBold { get; set; }

        /// <summary>折り返し（将来拡張用。現バージョンでは未使用）。</summary>
        public bool MultiLine { get; set; }
    }
}
