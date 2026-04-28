namespace PdfUtility.Core.Drawing
{
    /// <summary>
    /// 矩形描画命令。
    /// X,Y はページ左上原点で矩形の左上角を指定する。
    /// </summary>
    public class RectangleDrawCommand : PdfDrawCommand
    {
        /// <summary>矩形の幅（pt）。</summary>
        public double Width { get; set; }

        /// <summary>矩形の高さ（pt）。</summary>
        public double Height { get; set; }

        /// <summary>枠線の色。null の場合は枠線なし。</summary>
        public PdfColor BorderColor { get; set; } = PdfColor.Black;

        /// <summary>塗りつぶしの色。null の場合は塗りつぶしなし。</summary>
        public PdfColor FillColor { get; set; }

        /// <summary>枠線の太さ（pt）。0 の場合は枠線なし。</summary>
        public double BorderWidth { get; set; } = 1.0;

        /// <summary>true のとき FillColor で塗りつぶす。</summary>
        public bool IsFilled { get; set; }
    }
}
