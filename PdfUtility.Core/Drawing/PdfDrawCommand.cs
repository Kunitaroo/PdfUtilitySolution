namespace PdfUtility.Core.Drawing
{
    /// <summary>
    /// PDF描画命令の抽象基底クラス。
    ///
    /// 座標系：ページ左上を原点(0,0)とし、X軸は右方向、Y軸は下方向を正とする。
    /// （PDF仕様の左下原点とは異なる。レンダラー内部で変換する。）
    /// 単位はポイント（1pt ≈ 0.353mm）。
    /// </summary>
    public abstract class PdfDrawCommand
    {
        /// <summary>描画対象ページ（1始まり）。</summary>
        public int PageNumber { get; set; } = 1;

        /// <summary>描画基準点のX座標（pt）。ページ左端を0とする。</summary>
        public double X { get; set; }

        /// <summary>描画基準点のY座標（pt）。ページ上端を0とする。</summary>
        public double Y { get; set; }

        /// <summary>描画順序。小さい値が先（背面）、大きい値が後（前面）。</summary>
        public int ZIndex { get; set; }

        /// <summary>命令の識別名（任意）。デバッグ用。</summary>
        public string Name { get; set; }
    }
}
