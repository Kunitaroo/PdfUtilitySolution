namespace PdfUtility.Core.Drawing
{
    /// <summary>
    /// 画像埋め込み命令。X,Y はページ左上原点で画像の左上角を指定する。
    /// JPEG（.jpg）・PNG（.png）対応。
    /// </summary>
    public class ImageDrawCommand : PdfDrawCommand
    {
        /// <summary>画像のバイト列（JPEGまたはPNG）。</summary>
        public byte[] ImageBytes { get; set; }

        /// <summary>描画幅（pt）。0 以下の場合は画像の元サイズを使用する。</summary>
        public double Width { get; set; }

        /// <summary>描画高さ（pt）。0 以下の場合は画像の元サイズを使用する。</summary>
        public double Height { get; set; }

        /// <summary>true のとき、Width/Height の範囲内でアスペクト比を維持する（未実装：現在は無視）。</summary>
        public bool KeepAspectRatio { get; set; }
    }
}
