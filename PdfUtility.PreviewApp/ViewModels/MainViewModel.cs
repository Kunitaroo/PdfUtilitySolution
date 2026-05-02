using Microsoft.Win32;
using PdfUtility.PreviewApp.Helpers;
using PdfUtility.PreviewApp.Models;
using PdfUtility.PreviewApp.Services;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PdfUtility.PreviewApp.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly PdfRenderService _pdfService = new();
    private readonly CodeGeneratorService _codeService = new();
    private readonly PdfCoordinateConverter _converter = new();

    private BitmapSource? _pdfPageImage;
    private int _currentPage = 1;
    private int _totalPages;
    // グリッド描画用：ページの表示サイズ（WPF論理px）とPDFサイズ（ポイント）
    public double PageDisplayWidth  { get; private set; }
    public double PageDisplayHeight { get; private set; }
    public double PagePointWidth    { get; private set; }
    public double PagePointHeight   { get; private set; }
    private bool _isGridVisible;
    private double _mouseX;
    private double _mouseY;
    private double _clickedX;
    private double _clickedY;
    private double _selectionWidth;
    private double _selectionHeight;
    private string _generatedCode = string.Empty;
    private bool _isCSharpSelected = true;
    private string _statusMessage = "「PDF を開く」でファイルを選択してください";
    private CoordinateInfo _lastCoordinate = new();

    public BitmapSource? PdfPageImage
    {
        get => _pdfPageImage;
        private set
        {
            SetField(ref _pdfPageImage, value);
            OnPropertyChanged(nameof(NoPdfVisibility));
        }
    }

    public int CurrentPage
    {
        get => _currentPage;
        private set { SetField(ref _currentPage, value); OnPropertyChanged(nameof(PageInfo)); }
    }

    public int TotalPages
    {
        get => _totalPages;
        private set { SetField(ref _totalPages, value); OnPropertyChanged(nameof(PageInfo)); }
    }

    public string PageInfo => TotalPages > 0 ? $"{CurrentPage} / {TotalPages}" : "- / -";

    public bool IsGridVisible
    {
        get => _isGridVisible;
        set => SetField(ref _isGridVisible, value);
    }

    public double MouseX
    {
        get => _mouseX;
        set => SetField(ref _mouseX, value);
    }

    public double MouseY
    {
        get => _mouseY;
        set => SetField(ref _mouseY, value);
    }

    public double ClickedX
    {
        get => _clickedX;
        private set => SetField(ref _clickedX, value);
    }

    public double ClickedY
    {
        get => _clickedY;
        private set => SetField(ref _clickedY, value);
    }

    public double SelectionWidth
    {
        get => _selectionWidth;
        set => SetField(ref _selectionWidth, value);
    }

    public double SelectionHeight
    {
        get => _selectionHeight;
        set => SetField(ref _selectionHeight, value);
    }

    public string GeneratedCode
    {
        get => _generatedCode;
        private set => SetField(ref _generatedCode, value);
    }

    public bool IsCSharpSelected
    {
        get => _isCSharpSelected;
        set
        {
            if (SetField(ref _isCSharpSelected, value))
            {
                OnPropertyChanged(nameof(IsVbNetSelected));
                RegenerateCode();
            }
        }
    }

    public bool IsVbNetSelected
    {
        get => !_isCSharpSelected;
        set => IsCSharpSelected = !value;
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public Visibility NoPdfVisibility => PdfPageImage == null ? Visibility.Visible : Visibility.Collapsed;

    public AsyncRelayCommand OpenPdfCommand { get; }
    public AsyncRelayCommand PrevPageCommand { get; }
    public AsyncRelayCommand NextPageCommand { get; }
    public RelayCommand CopyCodeCommand { get; }

    public MainViewModel()
    {
        OpenPdfCommand = new AsyncRelayCommand(OpenPdfAsync);
        PrevPageCommand = new AsyncRelayCommand(PrevPageAsync, () => CurrentPage > 1);
        NextPageCommand = new AsyncRelayCommand(NextPageAsync, () => CurrentPage < TotalPages);
        CopyCodeCommand = new RelayCommand(CopyCode, () => !string.IsNullOrEmpty(GeneratedCode));
    }

    private async Task OpenPdfAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title = "PDF ファイルを選択",
            Filter = "PDF ファイル (*.pdf)|*.pdf|すべてのファイル (*.*)|*.*",
        };

        if (dlg.ShowDialog() != true) return;

        StatusMessage = "読み込み中...";
        try
        {
            await _pdfService.OpenAsync(dlg.FileName);
            TotalPages = (int)_pdfService.PageCount;
            CurrentPage = 1;
            await LoadCurrentPageAsync();
            StatusMessage = $"読み込み完了  ({TotalPages} ページ)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"エラー: {ex.Message}";
            MessageBox.Show($"PDF の読み込みに失敗しました:\n{ex.Message}", "エラー",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        PrevPageCommand.RaiseCanExecuteChanged();
        NextPageCommand.RaiseCanExecuteChanged();
    }

    private async Task LoadCurrentPageAsync()
    {
        var bmp = await _pdfService.RenderPageAsync((uint)(CurrentPage - 1));

        // bmp.Width は WPF 論理px（DPI補正済み）。GetPosition() と同じ座標系。
        PageDisplayWidth  = bmp.Width;
        PageDisplayHeight = bmp.Height;
        PagePointWidth    = _pdfService.CurrentPagePointWidth;
        PagePointHeight   = _pdfService.CurrentPagePointHeight;

        _converter.Update(bmp.Width, bmp.Height, PagePointWidth, PagePointHeight);

        PdfPageImage = bmp; // 最後に設定してグリッド再描画イベントを1回だけ発火
    }

    private async Task PrevPageAsync()
    {
        if (CurrentPage <= 1) return;
        CurrentPage--;
        await LoadCurrentPageAsync();
        PrevPageCommand.RaiseCanExecuteChanged();
        NextPageCommand.RaiseCanExecuteChanged();
    }

    private async Task NextPageAsync()
    {
        if (CurrentPage >= TotalPages) return;
        CurrentPage++;
        await LoadCurrentPageAsync();
        PrevPageCommand.RaiseCanExecuteChanged();
        NextPageCommand.RaiseCanExecuteChanged();
    }

    public void UpdateMouseCoordinate(double pixelX, double pixelY)
    {
        if (!_converter.IsInitialized) return;
        var (x, y) = _converter.ToPdfPoints(pixelX, pixelY);
        MouseX = x;
        MouseY = y;
    }

    public void SetClickCoordinate(double pixelX, double pixelY)
    {
        if (!_converter.IsInitialized) return;
        var (x, y) = _converter.ToPdfPoints(pixelX, pixelY);
        ClickedX = x;
        ClickedY = y;
        _lastCoordinate = new CoordinateInfo { X = x, Y = y, PageNumber = CurrentPage };
        SelectionWidth = 0;
        SelectionHeight = 0;
        RegenerateCode();
        CopyCodeCommand.RaiseCanExecuteChanged();
    }

    public void SetSelection(double sx, double sy, double ex, double ey)
    {
        if (!_converter.IsInitialized) return;
        var (x1, y1) = _converter.ToPdfPoints(sx, sy);
        var (x2, y2) = _converter.ToPdfPoints(ex, ey);
        ClickedX = Math.Min(x1, x2);
        ClickedY = Math.Max(y1, y2);
        _lastCoordinate = new CoordinateInfo
        {
            X = Math.Min(x1, x2),
            Y = Math.Max(y1, y2),
            PageNumber = CurrentPage,
            Width  = Math.Abs(x2 - x1),
            Height = Math.Abs(y2 - y1),
        };
        SelectionWidth  = _lastCoordinate.Width;
        SelectionHeight = _lastCoordinate.Height;
        RegenerateCode();
        CopyCodeCommand.RaiseCanExecuteChanged();
    }

    private void RegenerateCode()
    {
        var lang = _isCSharpSelected ? CodeLanguage.CSharp : CodeLanguage.VbNet;
        GeneratedCode = _codeService.Generate(_lastCoordinate, lang);
    }

    private void CopyCode()
    {
        if (!string.IsNullOrEmpty(GeneratedCode))
            Clipboard.SetText(GeneratedCode);
    }
}
