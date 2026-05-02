namespace PdfUtility.PreviewApp.Helpers;

// 画面座標（WPF論理px・左上原点）← → PDF座標（ポイント・左下原点）の変換
public class PdfCoordinateConverter
{
    // BitmapSource.Width/Height（WPF論理px = PixelWidth * 96 / DpiX）
    private double _displayWidth;
    private double _displayHeight;

    private double _pagePointWidth;
    private double _pagePointHeight;

    public bool IsInitialized => _displayWidth > 0;
    public double PagePointWidth => _pagePointWidth;
    public double PagePointHeight => _pagePointHeight;

    // bmpLogicalWidth/Height : BitmapSource.Width / Height（WPF論理px、DPI補正済み）
    // pagePointWidth/Height  : PDFページサイズ（ポイント、72DPI）
    public void Update(double bmpLogicalWidth, double bmpLogicalHeight,
                       double pagePointWidth, double pagePointHeight)
    {
        _displayWidth = bmpLogicalWidth;
        _displayHeight = bmpLogicalHeight;
        _pagePointWidth = pagePointWidth;
        _pagePointHeight = pagePointHeight;
    }

    // mouseX/Y : Image コントロール上の WPF 論理px（GetPosition の戻り値）
    // 戻り値   : PDF座標（左下原点、ポイント単位）
    public (double x, double y) ToPdfPoints(double mouseX, double mouseY)
    {
        double ptX = mouseX * _pagePointWidth  / _displayWidth;
        double ptY = _pagePointHeight - mouseY * _pagePointHeight / _displayHeight;
        return (Math.Clamp(ptX, 0, _pagePointWidth),
                Math.Clamp(ptY, 0, _pagePointHeight));
    }

    // PDF座標（ポイント）→ 画面上のWPF論理px（グリッド描画用）
    public (double x, double y) ToDisplayPx(double ptX, double ptY)
    {
        double px = ptX * _displayWidth  / _pagePointWidth;
        double py = (_pagePointHeight - ptY) * _displayHeight / _pagePointHeight;
        return (px, py);
    }
}
