namespace PdfUtility.Core.Drawing
{
    /// <summary>
    /// PDF描画で使用するRGB色。各成分は0〜255。
    /// </summary>
    public class PdfColor
    {
        public int R { get; set; }
        public int G { get; set; }
        public int B { get; set; }

        public PdfColor() { }

        public PdfColor(int r, int g, int b)
        {
            R = r; G = g; B = b;
        }

        // よく使う色
        public static PdfColor Black => new PdfColor(0, 0, 0);
        public static PdfColor White => new PdfColor(255, 255, 255);
        public static PdfColor Red   => new PdfColor(255, 0, 0);
        public static PdfColor Blue  => new PdfColor(0, 0, 255);
        public static PdfColor Green => new PdfColor(0, 128, 0);
        public static PdfColor Gray  => new PdfColor(128, 128, 128);
        public static PdfColor LightGray => new PdfColor(211, 211, 211);

        /// <summary>PDF演算子用に0〜1のスケールで返す（例: "0 0 0"）。</summary>
        internal string ToPdfRgb()
        {
            double r = R / 255.0;
            double g = G / 255.0;
            double b = B / 255.0;
            return $"{r.ToInvariant(4)} {g.ToInvariant(4)} {b.ToInvariant(4)}";
        }
    }

    internal static class DoubleColorExtensions
    {
        internal static string ToInvariant(this double value, int decimals)
            => value.ToString("F" + decimals, System.Globalization.CultureInfo.InvariantCulture);
    }
}
