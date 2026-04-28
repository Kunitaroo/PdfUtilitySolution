namespace PdfUtility.Core.Drawing
{
    /// <summary>
    /// 線描画命令。
    /// (X,Y) → (X2,Y2) の直線を引く。座標はページ左上原点。
    /// </summary>
    public class LineDrawCommand : PdfDrawCommand
    {
        /// <summary>終点のX座標（pt）。ページ左端を0とする。</summary>
        public double X2 { get; set; }

        /// <summary>終点のY座標（pt）。ページ上端を0とする。</summary>
        public double Y2 { get; set; }

        /// <summary>線の色。</summary>
        public PdfColor LineColor { get; set; } = PdfColor.Black;

        /// <summary>線の太さ（pt）。</summary>
        public double LineWidth { get; set; } = 1.0;

        /// <summary>破線パターン。null または空の場合は実線。</summary>
        /// <remarks>例: [3, 1] = 3pt描画・1pt空白を繰り返す</remarks>
        public double[] DashPattern { get; set; }

        /// <summary>破線のオフセット（DashPattern 使用時）。</summary>
        public double DashPhase { get; set; }
    }
}
