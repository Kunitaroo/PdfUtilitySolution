using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
using System.Windows.Media.Imaging;

namespace PdfUtility.PreviewApp.Services;

public class PdfRenderService
{
    private PdfDocument? _document;

    public uint PageCount => _document?.PageCount ?? 0;

    // Page dimensions in DIPs (96 DPI) from Windows.Data.Pdf
    public double CurrentPageDipWidth { get; private set; }
    public double CurrentPageDipHeight { get; private set; }

    // Page dimensions in PDF points (72 DPI): 1 DIP = 72/96 pt = 0.75 pt
    public double CurrentPagePointWidth  => CurrentPageDipWidth  * 72.0 / 96.0;
    public double CurrentPagePointHeight => CurrentPageDipHeight * 72.0 / 96.0;

    public async Task OpenAsync(string filePath)
    {
        var file = await StorageFile.GetFileFromPathAsync(filePath);
        _document = await PdfDocument.LoadFromFileAsync(file);
    }

    public async Task<BitmapSource> RenderPageAsync(uint pageIndex, double renderDpi = 150)
    {
        if (_document == null)
            throw new InvalidOperationException("PDF が読み込まれていません。");

        using var page = _document.GetPage(pageIndex);

        CurrentPageDipWidth = page.Size.Width;
        CurrentPageDipHeight = page.Size.Height;

        double scale = renderDpi / 96.0;
        var options = new PdfPageRenderOptions
        {
            DestinationWidth  = (uint)(page.Size.Width  * scale),
            DestinationHeight = (uint)(page.Size.Height * scale),
        };

        using var stream = new InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(stream, options);

        // DataReader で WinRT ストリームをバイト配列に変換
        stream.Seek(0);
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        uint size = (uint)stream.Size;
        await reader.LoadAsync(size);
        var bytes = new byte[size];
        reader.ReadBytes(bytes);

        using var ms = new System.IO.MemoryStream(bytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = ms;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
