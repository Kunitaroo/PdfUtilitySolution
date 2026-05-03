using Microsoft.Win32;
using PdfUtility.PreviewApp.Helpers;
using PdfUtility.PreviewApp.Models;
using PdfUtility.PreviewApp.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PdfUtility.PreviewApp.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly PdfRenderService _pdfService = new();
    private readonly CodeGeneratorService _codeService = new();
    private readonly PdfCoordinateConverter _converter = new();

    // ===== 既存フィールド =====
    private BitmapSource? _pdfPageImage;
    private int _currentPage = 1;
    private int _totalPages;
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

    // ===== プレビューフィールド =====
    private PreviewElementType _previewType = PreviewElementType.Text;
    private string _previewText = "サンプルテキスト";
    private double _previewX = 100;
    private double _previewY = 700;
    private double _previewWidth = 100;
    private double _previewHeight = 50;
    private double _previewFontSize = 12;
    private string _previewImagePath = string.Empty;
    private PreviewElement? _selectedPreviewElement;

    // ===== 既存プロパティ =====

    public PdfCoordinateConverter Converter => _converter;

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

    // ===== コードセクションタイトル =====

    public string CodeSectionTitle => _selectedPreviewElement != null
        ? "生成コード（選択中の要素）"
        : "生成コード（座標情報）";

    // ===== プレビュープロパティ =====

    public ObservableCollection<PreviewElement> PreviewElements { get; } = new();

    public PreviewElement? SelectedPreviewElement
    {
        get => _selectedPreviewElement;
        set
        {
            if (SetField(ref _selectedPreviewElement, value))
            {
                DeletePreviewCommand?.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(CodeSectionTitle));
                RegenerateCode();
            }
        }
    }

    public bool IsTextMode
    {
        get => _previewType == PreviewElementType.Text;
        set { if (value) SetPreviewType(PreviewElementType.Text); }
    }

    public bool IsRectMode
    {
        get => _previewType == PreviewElementType.Rectangle;
        set { if (value) SetPreviewType(PreviewElementType.Rectangle); }
    }

    public bool IsImageMode
    {
        get => _previewType == PreviewElementType.Image;
        set { if (value) SetPreviewType(PreviewElementType.Image); }
    }

    public bool IsRectOrImageMode => _previewType != PreviewElementType.Text;

    private void SetPreviewType(PreviewElementType type)
    {
        _previewType = type;
        OnPropertyChanged(nameof(IsTextMode));
        OnPropertyChanged(nameof(IsRectMode));
        OnPropertyChanged(nameof(IsImageMode));
        OnPropertyChanged(nameof(IsRectOrImageMode));
    }

    public string PreviewText
    {
        get => _previewText;
        set => SetField(ref _previewText, value);
    }

    public double PreviewX
    {
        get => _previewX;
        set => SetField(ref _previewX, value);
    }

    public double PreviewY
    {
        get => _previewY;
        set => SetField(ref _previewY, value);
    }

    public double PreviewWidth
    {
        get => _previewWidth;
        set => SetField(ref _previewWidth, value);
    }

    public double PreviewHeight
    {
        get => _previewHeight;
        set => SetField(ref _previewHeight, value);
    }

    public double PreviewFontSize
    {
        get => _previewFontSize;
        set => SetField(ref _previewFontSize, value);
    }

    public string PreviewImagePath
    {
        get => _previewImagePath;
        private set
        {
            SetField(ref _previewImagePath, value);
            OnPropertyChanged(nameof(PreviewImageFileName));
            OnPropertyChanged(nameof(HasPreviewImage));
        }
    }

    public string PreviewImageFileName => string.IsNullOrEmpty(_previewImagePath)
        ? ""
        : System.IO.Path.GetFileName(_previewImagePath);

    public bool HasPreviewImage => !string.IsNullOrEmpty(_previewImagePath);

    // ===== コマンド =====

    public AsyncRelayCommand OpenPdfCommand      { get; }
    public AsyncRelayCommand PrevPageCommand     { get; }
    public AsyncRelayCommand NextPageCommand     { get; }
    public RelayCommand      CopyCodeCommand     { get; }
    public RelayCommand      AddPreviewCommand   { get; }
    public RelayCommand      DeletePreviewCommand { get; }
    public RelayCommand      ClearPreviewCommand { get; }
    public RelayCommand      SelectImageCommand  { get; }
    public RelayCommand      GenerateAllCodeCommand { get; }

    public MainViewModel()
    {
        OpenPdfCommand      = new AsyncRelayCommand(OpenPdfAsync);
        PrevPageCommand     = new AsyncRelayCommand(PrevPageAsync, () => CurrentPage > 1);
        NextPageCommand     = new AsyncRelayCommand(NextPageAsync, () => CurrentPage < TotalPages);
        CopyCodeCommand     = new RelayCommand(CopyCode, () => !string.IsNullOrEmpty(GeneratedCode));
        AddPreviewCommand   = new RelayCommand(AddPreview, () => _converter.IsInitialized);
        DeletePreviewCommand = new RelayCommand(DeletePreview, () => SelectedPreviewElement != null);
        ClearPreviewCommand = new RelayCommand(ClearPreview, () => PreviewElements.Count > 0);
        SelectImageCommand  = new RelayCommand(SelectImage);
        GenerateAllCodeCommand = new RelayCommand(GenerateAllCode, () => PreviewElements.Count > 0);
    }

    // ===== 既存メソッド =====

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
            TotalPages  = (int)_pdfService.PageCount;
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
        AddPreviewCommand.RaiseCanExecuteChanged();
    }

    private async Task LoadCurrentPageAsync()
    {
        var bmp = await _pdfService.RenderPageAsync((uint)(CurrentPage - 1));

        PageDisplayWidth  = bmp.Width;
        PageDisplayHeight = bmp.Height;
        PagePointWidth    = _pdfService.CurrentPagePointWidth;
        PagePointHeight   = _pdfService.CurrentPagePointHeight;

        _converter.Update(bmp.Width, bmp.Height, PagePointWidth, PagePointHeight);

        PdfPageImage = bmp;
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
        SelectionWidth  = 0;
        SelectionHeight = 0;
        // 選択を解除してから座標コードを生成
        _selectedPreviewElement = null;
        DeletePreviewCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedPreviewElement));
        OnPropertyChanged(nameof(CodeSectionTitle));
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
            X      = Math.Min(x1, x2),
            Y      = Math.Max(y1, y2),
            PageNumber = CurrentPage,
            Width  = Math.Abs(x2 - x1),
            Height = Math.Abs(y2 - y1),
        };
        SelectionWidth  = _lastCoordinate.Width;
        SelectionHeight = _lastCoordinate.Height;
        // 選択を解除してから座標コードを生成
        _selectedPreviewElement = null;
        DeletePreviewCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedPreviewElement));
        OnPropertyChanged(nameof(CodeSectionTitle));
        RegenerateCode();
        CopyCodeCommand.RaiseCanExecuteChanged();
    }

    private void RegenerateCode()
    {
        var lang = _isCSharpSelected ? CodeLanguage.CSharp : CodeLanguage.VbNet;
        GeneratedCode = _selectedPreviewElement != null
            ? _codeService.GenerateFromElement(_selectedPreviewElement, CurrentPage, lang)
            : _codeService.Generate(_lastCoordinate, lang);
        CopyCodeCommand.RaiseCanExecuteChanged();
    }

    private void CopyCode()
    {
        if (!string.IsNullOrEmpty(GeneratedCode))
            Clipboard.SetText(GeneratedCode);
    }

    private void GenerateAllCode()
    {
        var lang = _isCSharpSelected ? CodeLanguage.CSharp : CodeLanguage.VbNet;
        GeneratedCode = _codeService.GenerateAllElements(PreviewElements, CurrentPage, lang);
        CopyCodeCommand.RaiseCanExecuteChanged();
    }

    // ===== 要素移動後の更新（Step B: ドラッグ位置調整） =====

    public void NotifyElementPositionChanged(PreviewElement element)
    {
        // 同一要素の再選択でも確実にコードを再生成するため直接フィールドを更新
        _selectedPreviewElement = element;
        DeletePreviewCommand?.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedPreviewElement));
        OnPropertyChanged(nameof(CodeSectionTitle));
        RegenerateCode();
    }

    // ===== プレビューメソッド =====

    private void AddPreview()
    {
        var element = new PreviewElement
        {
            Type      = _previewType,
            X         = PreviewX,
            Y         = PreviewY,
            Width     = PreviewWidth,
            Height    = PreviewHeight,
            Text      = PreviewText,
            FontSize  = PreviewFontSize,
            ImagePath = _previewType == PreviewElementType.Image ? PreviewImagePath : null,
        };
        PreviewElements.Add(element);
        ClearPreviewCommand.RaiseCanExecuteChanged();
        GenerateAllCodeCommand.RaiseCanExecuteChanged();
    }

    private void DeletePreview()
    {
        if (SelectedPreviewElement == null) return;
        PreviewElements.Remove(SelectedPreviewElement);
        SelectedPreviewElement = null;
        DeletePreviewCommand.RaiseCanExecuteChanged();
        ClearPreviewCommand.RaiseCanExecuteChanged();
        GenerateAllCodeCommand.RaiseCanExecuteChanged();
    }

    private void ClearPreview()
    {
        PreviewElements.Clear();
        SelectedPreviewElement = null;
        ClearPreviewCommand.RaiseCanExecuteChanged();
        GenerateAllCodeCommand.RaiseCanExecuteChanged();
    }

    private void SelectImage()
    {
        var dlg = new OpenFileDialog
        {
            Title  = "画像ファイルを選択",
            Filter = "画像ファイル (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|すべてのファイル (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true)
            PreviewImagePath = dlg.FileName;
    }
}
