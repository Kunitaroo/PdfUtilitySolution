using PdfUtility.PreviewApp.Models;
using PdfUtility.PreviewApp.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PdfUtility.PreviewApp;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private Point _dragStart;
    private bool _isDragging;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.PreviewElements.CollectionChanged += (_, _) =>
            Dispatcher.InvokeAsync(DrawPreviewOverlay, DispatcherPriority.Loaded);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.PdfPageImage)
                           or nameof(MainViewModel.IsGridVisible))
        {
            Dispatcher.InvokeAsync(DrawGrid, DispatcherPriority.Loaded);
        }
        if (e.PropertyName == nameof(MainViewModel.PdfPageImage))
        {
            Dispatcher.InvokeAsync(DrawPreviewOverlay, DispatcherPriority.Loaded);
        }
    }

    // ===== グリッド描画 =====

    private void DrawGrid()
    {
        GridOverlay.Children.Clear();

        var vm = ViewModel;
        if (!vm.IsGridVisible || vm.PdfPageImage == null) return;

        double imgW = vm.PageDisplayWidth;
        double imgH = vm.PageDisplayHeight;
        double ptW  = vm.PagePointWidth;
        double ptH  = vm.PagePointHeight;

        if (imgW <= 0 || imgH <= 0 || ptW <= 0 || ptH <= 0) return;

        double scaleX = imgW / ptW;
        double scaleY = imgH / ptH;

        const double gridStep = 50.0;

        var gridBrush = new SolidColorBrush(Color.FromArgb(160, 170, 175, 185));
        gridBrush.Freeze();

        for (double xPt = 0; xPt <= ptW + 0.1; xPt += gridStep)
        {
            double xPx = xPt * scaleX;
            GridOverlay.Children.Add(new Line
            {
                X1 = xPx, Y1 = 0,
                X2 = xPx, Y2 = imgH,
                Stroke           = gridBrush,
                StrokeThickness  = xPt == 0 ? 1.0 : 0.5,
                IsHitTestVisible = false,
            });
        }

        for (double yPt = 0; yPt <= ptH + 0.1; yPt += gridStep)
        {
            double yPx = imgH - yPt * scaleY;
            GridOverlay.Children.Add(new Line
            {
                X1 = 0,    Y1 = yPx,
                X2 = imgW, Y2 = yPx,
                Stroke           = gridBrush,
                StrokeThickness  = yPt == 0 ? 1.0 : 0.5,
                IsHitTestVisible = false,
            });
        }
    }

    // ===== プレビューオーバーレイ描画 =====

    private void DrawPreviewOverlay()
    {
        PreviewCanvas.Children.Clear();

        var vm = ViewModel;
        if (vm.PdfPageImage == null || !vm.Converter.IsInitialized) return;

        double scaleY = vm.PageDisplayHeight / vm.PagePointHeight;

        foreach (var element in vm.PreviewElements)
        {
            switch (element.Type)
            {
                case PreviewElementType.Text:
                    DrawPreviewText(element, scaleY);
                    break;
                case PreviewElementType.Rectangle:
                    DrawPreviewRect(element);
                    break;
                case PreviewElementType.Image:
                    DrawPreviewImage(element);
                    break;
            }
        }
    }

    private void DrawPreviewText(PreviewElement element, double scaleY)
    {
        var (screenX, screenY) = ViewModel.Converter.ToDisplayPx(element.X, element.Y);
        double screenFontSize = Math.Max(element.FontSize * scaleY, 8);

        var text = new System.Windows.Controls.TextBlock
        {
            Text             = element.Text,
            FontSize         = screenFontSize,
            Foreground       = new SolidColorBrush(Color.FromArgb(200, 30, 80, 220)),
            IsHitTestVisible = false,
        };
        System.Windows.Controls.Canvas.SetLeft(text, screenX);
        System.Windows.Controls.Canvas.SetTop(text,  screenY - screenFontSize);
        PreviewCanvas.Children.Add(text);
    }

    private void DrawPreviewRect(PreviewElement element)
    {
        // PDF座標: (X, Y) は左下原点。枠の左上は (X, Y+Height)。
        var (left, top)     = ViewModel.Converter.ToDisplayPx(element.X, element.Y + element.Height);
        var (right, bottom) = ViewModel.Converter.ToDisplayPx(element.X + element.Width, element.Y);

        double w = Math.Max(right - left, 1);
        double h = Math.Max(bottom - top, 1);

        var rect = new System.Windows.Shapes.Rectangle
        {
            Width            = w,
            Height           = h,
            Stroke           = new SolidColorBrush(Color.FromArgb(200, 30, 80, 220)),
            StrokeThickness  = 1.5,
            Fill             = new SolidColorBrush(Color.FromArgb(40, 30, 80, 220)),
            IsHitTestVisible = false,
        };
        System.Windows.Controls.Canvas.SetLeft(rect, left);
        System.Windows.Controls.Canvas.SetTop(rect,  top);
        PreviewCanvas.Children.Add(rect);
    }

    private void DrawPreviewImage(PreviewElement element)
    {
        if (string.IsNullOrEmpty(element.ImagePath)) return;

        var (left, top)     = ViewModel.Converter.ToDisplayPx(element.X, element.Y + element.Height);
        var (right, bottom) = ViewModel.Converter.ToDisplayPx(element.X + element.Width, element.Y);

        double w = Math.Max(right - left, 1);
        double h = Math.Max(bottom - top, 1);

        try
        {
            // BitmapImage(Uri) は非同期読み込みのため、BeginInit/EndInit + OnLoad で同期読み込みする
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.UriSource   = new Uri(element.ImagePath, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();

            var img = new System.Windows.Controls.Image
            {
                Source           = bmp,
                Width            = w,
                Height           = h,
                Opacity          = 0.6,
                Stretch          = System.Windows.Media.Stretch.Fill,
                IsHitTestVisible = false,
            };
            System.Windows.Controls.Canvas.SetLeft(img, left);
            System.Windows.Controls.Canvas.SetTop(img,  top);
            PreviewCanvas.Children.Add(img);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DrawPreviewImage] 読み込み失敗: {ex.Message}");
        }
    }

    // ===== マウスイベント =====

    private void PdfImage_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(PdfImage);
        ViewModel.UpdateMouseCoordinate(pos.X, pos.Y);

        if (_isDragging)
            UpdateDragRect(_dragStart, pos);
    }

    private void PdfImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(PdfImage);
        _isDragging = true;
        ((UIElement)sender).CaptureMouse();
    }

    private void PdfImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        var pos = e.GetPosition(PdfImage);
        ((UIElement)sender).ReleaseMouseCapture();
        _isDragging = false;

        SelectionCanvas.Children.Clear();

        double dx = Math.Abs(pos.X - _dragStart.X);
        double dy = Math.Abs(pos.Y - _dragStart.Y);

        if (dx < 4 && dy < 4)
            ViewModel.SetClickCoordinate(pos.X, pos.Y);
        else
            ViewModel.SetSelection(_dragStart.X, _dragStart.Y, pos.X, pos.Y);
    }

    // ===== ドラッグ矩形描画 =====

    private void UpdateDragRect(Point start, Point current)
    {
        SelectionCanvas.Children.Clear();

        double w = Math.Abs(current.X - start.X);
        double h = Math.Abs(current.Y - start.Y);
        if (w < 2 && h < 2) return;

        var rect = new System.Windows.Shapes.Rectangle
        {
            Width            = w,
            Height           = h,
            Stroke           = new SolidColorBrush(Color.FromArgb(220, 30, 100, 200)),
            StrokeThickness  = 1.5,
            Fill             = new SolidColorBrush(Color.FromArgb(40, 30, 100, 200)),
            IsHitTestVisible = false,
        };
        System.Windows.Controls.Canvas.SetLeft(rect, Math.Min(start.X, current.X));
        System.Windows.Controls.Canvas.SetTop(rect,  Math.Min(start.Y, current.Y));
        SelectionCanvas.Children.Add(rect);
    }
}
