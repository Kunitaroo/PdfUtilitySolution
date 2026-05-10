namespace PdfUtility.Core.Drawing
{
    /// <summary>
    /// 縦書き時の行揃え（列の先頭位置 cmd.Y を基準にした列方向の配置）。
    /// </summary>
    public enum PdfVerticalTextAlign
    {
        /// <summary>列の先頭（最上段）から書き始める。既定値。</summary>
        Top    = 0,

        /// <summary>列の中央に配置（cmd.Width を列の長さとして使用）。</summary>
        Middle = 1,

        /// <summary>列の末尾（最下段）に配置（cmd.Width を列の長さとして使用）。</summary>
        Bottom = 2,
    }
}
