namespace PdfUtility.Core.Drawing
{
    /// <summary>
    /// テキストの書字方向。
    /// </summary>
    public enum WritingMode
    {
        /// <summary>横書き（左→右、上→下）。既定値。</summary>
        Horizontal = 0,

        /// <summary>縦書き（上→下、右列→左列）。</summary>
        Vertical = 1,
    }
}
