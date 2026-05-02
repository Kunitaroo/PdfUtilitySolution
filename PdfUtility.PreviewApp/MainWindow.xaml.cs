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
        Loaded += (_, _) => ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    // ViewModel のプロパティ変更を監視してグリッドを再描画
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.PdfPageImage)
                           or nameof(MainViewModel.IsGridVisible))
        {
            // レイアウト更新後に描画（Loaded 優先度で確実に ActualSize が確定してから）
            Dispatcher.InvokeAsync(DrawGrid, DispatcherPriority.Loaded);
        }
    }

    // ===== グリッド描画 =====

    private void DrawGrid()
    {
        GridOverlay.Children.Clear();

        var vm = ViewModel;
        if (!vm.IsGridVisible || vm.PdfPageImage == null) return;

        double imgW = vm.PageDisplayWidth;   // WPF 論理px（bmp.Width）
        double imgH = vm.PageDisplayHeight;
        double ptW  = vm.PagePointWidth;     // PDF ページ幅（ポイント）
        double ptH  = vm.PagePointHeight;

        if (imgW <= 0 || imgH <= 0 || ptW <= 0 || ptH <= 0) return;

        // 1ポイントあたりの WPF 論理px
        double scaleX = imgW / ptW;
        double scaleY = imgH / ptH;

        const double gridStep = 50.0; // 50pt 間隔

        // グレー（薄め）：アルファ160、R=170 G=175 B=185
        var gridBrush = new SolidColorBrush(Color.FromArgb(160, 170, 175, 185));
        gridBrush.Freeze();

        // 縦線（PDF の X 軸方向：左から右）
        for (double xPt = 0; xPt <= ptW + 0.1; xPt += gridStep)
        {
            double xPx = xPt * scaleX;
            GridOverlay.Children.Add(new Line
            {
                X1 = xPx, Y1 = 0,
                X2 = xPx, Y2 = imgH,
                Stroke          = gridBrush,
                StrokeThickness = xPt == 0 ? 1.0 : 0.5,
                IsHitTestVisible = false,
            });
        }

        // 横線（PDF の Y 軸方向：下から上 → 画面は上から下なので反転）
        for (double yPt = 0; yPt <= ptH + 0.1; yPt += gridStep)
        {
            double yPx = imgH - yPt * scaleY; // Y 軸反転
            GridOverlay.Children.Add(new Line
            {
                X1 = 0,    Y1 = yPx,
                X2 = imgW, Y2 = yPx,
                Stroke          = gridBrush,
                StrokeThickness = yPt == 0 ? 1.0 : 0.5,
                IsHitTestVisible = false,
            });
        }
    }

    // ===== マウスイベント（Step 3：座標表示 / Step 5：クリック固定 / Step 7：範囲選択）=====

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
            Width  = w,
            Height = h,
            Stroke = new SolidColorBrush(Color.FromArgb(220, 30, 100, 200)),
            StrokeThickness = 1.5,
            Fill   = new SolidColorBrush(Color.FromArgb(40, 30, 100, 200)),
            IsHitTestVisible = false,
        };
        System.Windows.Controls.Canvas.SetLeft(rect, Math.Min(start.X, current.X));
        System.Windows.Controls.Canvas.SetTop(rect,  Math.Min(start.Y, current.Y));
        SelectionCanvas.Children.Add(rect);
    }
}
