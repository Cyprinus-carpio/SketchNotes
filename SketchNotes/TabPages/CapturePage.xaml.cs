using SketchNotes.CaptureEncoder;
using SketchNotes.Commands;
using SketchNotes.FileIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Input.Inking.Analysis;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using MUXC = Microsoft.UI.Xaml.Controls;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace SketchNotes.TabPages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary
    class ResolutionItem
    {
        public string DisplayName { get; set; }
        public CaptureEncoder.SizeUInt32 Resolution { get; set; }

        public bool IsZero() { return Resolution.Width == 0 || Resolution.Height == 0; }
    }

    class BitrateItem
    {
        public string DisplayName { get; set; }
        public uint Bitrate { get; set; }
    }

    class FrameRateItem
    {
        public string DisplayName { get; set; }
        public uint FrameRate { get; set; }
    }

    public sealed partial class CapturePage : Page
    {
        public CapturePage()
        {
            InitializeComponent();

            Window.Current.CoreWindow.SizeChanged += CoreWindow_SizeChanged;
            MainInkCanvas.InkPresenter.StrokeInput.StrokeStarted += StrokeInput_StrokeStarted;
            MainInkCanvas.InkPresenter.StrokeInput.StrokeEnded += StrokeInput_StrokeEnded;
            AutoTasksTimer.Tick += TimeSpan_Tick;
            ChangeViewTimer.Tick += ChangeViewTimer_Tick;

            MainInkCanvas.InkPresenter.InputDeviceTypes =
                Windows.UI.Core.CoreInputDeviceTypes.Mouse |
                Windows.UI.Core.CoreInputDeviceTypes.Pen |
                Windows.UI.Core.CoreInputDeviceTypes.Touch;

            InkDrawingAttributes drawingAttributes = new InkDrawingAttributes();
            drawingAttributes.IgnorePressure = false;
            drawingAttributes.FitToCurve = true;
            MainInkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributes);

            MainBallpointPen.SelectedStrokeWidth = 4;
            SubBallpointPen.SelectedStrokeWidth = 4;
            MainPencil.SelectedStrokeWidth = 6;
            MainBallpointPen.SelectedBrushIndex = 7;
            MainPencil.SelectedBrushIndex = 7;
            SubBallpointPen.SelectedBrushIndex = 7;
            DragTranslation = new TranslateTransform();
            DragBtn.RenderTransform = DragTranslation;
            LockFontIcon.Glyph = "\uE785";
            SubToolBar.Translation = new Vector3(0, 0, 32);
            AutoTasksTimer.Interval = TimeSpan.FromMilliseconds(500);
            ChangeViewTimer.Interval = TimeSpan.FromMilliseconds(500);

            RefreshDisplay();

            if (!GraphicsCaptureSession.IsSupported())
            {
                IsEnabled = false;

                MainPage.RootPage.SendNotification("无法捕获",
                    "此设备不支持屏幕录制。",
                    Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error);

                return;
            }

            var compositor = Window.Current.Compositor;
            PreviewBrush = compositor.CreateSurfaceBrush();
            PreviewBrush.Stretch = CompositionStretch.Uniform;
            PreviewVisual = compositor.CreateSpriteVisual();
            PreviewVisual.RelativeSizeAdjustment = Vector2.One;
            PreviewVisual.Brush = PreviewBrush;
            ElementCompositionPreview.SetElementChildVisual(CapturePreviewGrid, PreviewVisual);

            Device = D3DDeviceManager.Device;
            var settings = GetCachedSettings();
            Resolutions = new List<ResolutionItem>();

            foreach (var resolution in EncoderPresets.Resolutions)
            {
                Resolutions.Add(new ResolutionItem()
                {
                    DisplayName = $"{resolution.Width} × {resolution.Height}",
                    Resolution = resolution,
                });
            }

            ResolutionComboBox.ItemsSource = Resolutions;
            ResolutionComboBox.SelectedIndex = GetResolutionIndex(settings.CaptureWidth, settings.CaptureHeight);

            Bitrates = new List<BitrateItem>();
            foreach (var bitrate in EncoderPresets.Bitrates)
            {
                var mbps = (float)bitrate / 1000000;
                Bitrates.Add(new BitrateItem()
                {
                    DisplayName = $"{mbps:0.##} Mbps",
                    Bitrate = bitrate,
                });
            }
            BitrateComboBox.ItemsSource = Bitrates;
            BitrateComboBox.SelectedIndex = GetBitrateIndex(settings.CaptureBitrate);

            FrameRates = new List<FrameRateItem>();
            foreach (var frameRate in EncoderPresets.FrameRates)
            {
                FrameRates.Add(new FrameRateItem()
                {
                    DisplayName = $"{frameRate} fps",
                    FrameRate = frameRate,
                });
            }
            FrameRateComboBox.ItemsSource = FrameRates;
            FrameRateComboBox.SelectedIndex = GetFrameRateIndex(settings.CaptureFrameRate);

            if (ApiInformation.IsPropertyPresent(typeof(GraphicsCaptureSession).FullName, nameof(GraphicsCaptureSession.IsCursorCaptureEnabled)))
            {
                IncludeCursorCheckBox.Visibility = Visibility.Visible;
                IncludeCursorCheckBox.Checked += IncludeCursorCheckBox_Checked;
                IncludeCursorCheckBox.Unchecked += IncludeCursorCheckBox_Checked;
            }
            IncludeCursorCheckBox.IsChecked = settings.CaptureIncludeCursor;
        }

        private DispatcherTimer AutoTasksTimer = new DispatcherTimer();
        private DispatcherTimer ChangeViewTimer = new DispatcherTimer();
        private Point StrokeEndedPoint;
        private InkAnalyzer MainInkAnalyzer = new InkAnalyzer();
        private IReadOnlyList<InkStroke> MainInkStrokes = null;
        private InkAnalysisResult MainInkAnalysisResults = null;
        private Polyline Lasso;
        private Rect BoundingRect;
        private bool IsBoundRect;
        private TranslateTransform DragTranslation;
        private const int FPS = 60;
        private Guid PageGuid = Guid.NewGuid();
        public static NotePage PaintPage;
        private DateTimeOffset BeginTimeOfRecordedSession;
        private DateTimeOffset EndTimeOfRecordedSession;
        private TimeSpan DurationOfRecordedSession;
        private DateTime BeginTimeOfReplay;
        private DispatcherTimer InkReplayTimer;
        private InkStrokeBuilder StrokeBuilder;
        private IReadOnlyList<InkStroke> StrokesToReplay;
        private IDirect3DDevice Device;
        private List<ResolutionItem> Resolutions;
        private List<BitrateItem> Bitrates;
        private List<FrameRateItem> FrameRates;
        private CapturePreview Preview;
        private SpriteVisual PreviewVisual;
        private CompositionSurfaceBrush PreviewBrush;
        public double PageWidth;
        public double PageHeight;

        private void TimeSpan_Tick(object sender, object e)
        {
            if (StrokeEndedPoint != null && AutoMoveToggleButton.IsChecked == true)
            {
                if (StrokeEndedPoint.X - MainScrollViewer.HorizontalOffset /
                    MainScrollViewer.ZoomFactor + 100 >= (MainScrollViewer.ActualWidth - 100) /
                    MainScrollViewer.ZoomFactor)
                {
                    MainScrollViewer.ChangeView(MainScrollViewer.HorizontalOffset + 100, null, null);
                }
                if (StrokeEndedPoint.X - MainScrollViewer.HorizontalOffset /
                    MainScrollViewer.ZoomFactor + 100 <= 100 / MainScrollViewer.ZoomFactor)
                {
                    MainScrollViewer.ChangeView(MainScrollViewer.HorizontalOffset - 100, null, null);
                }
                if (StrokeEndedPoint.Y - MainScrollViewer.VerticalOffset /
                    MainScrollViewer.ZoomFactor + 100 >= (MainScrollViewer.ActualHeight - 100) /
                    MainScrollViewer.ZoomFactor)
                {
                    MainScrollViewer.ChangeView(null, MainScrollViewer.VerticalOffset + 100, null);
                }
                if (StrokeEndedPoint.Y - MainScrollViewer.VerticalOffset /
                    MainScrollViewer.ZoomFactor + 100 <= 100 / MainScrollViewer.ZoomFactor)
                {
                    MainScrollViewer.ChangeView(null, MainScrollViewer.VerticalOffset - 100, null);
                }
            }

            //if (RecognitionToggleButton.IsChecked == true)
            //{
            //    RecognizeStorks();
            //}

            //if (MainPage.RootPage.BackupToggleSwitch.IsOn == true)
            //{
            //    MainPage.RootPage.AutoSaveProgressBar.Value = 0;
            //    MainPage.RootPage.AutoSaveProgressBar.IsIndeterminate = true;
            //    MainPage.RootPage.AutoSaveTextBlock.Text = "正在运行...";

            //    MainPage.RootPage.RefreshBackgroundTasks(1);

            //    IRandomAccessStream stream = new InMemoryRandomAccessStream();
            //    await MainInkCanvas.InkPresenter.StrokeContainer.SaveAsync(stream);
            //    int result = await BackupOperator.SaveBackup(stream, PageGuid, App.NoteBookGuid);

            //    if (result == 1)
            //    {
            //        MainPage.RootPage.AutoSaveProgressBar.Value = 100;
            //        MainPage.RootPage.AutoSaveProgressBar.ShowError = false;
            //        MainPage.RootPage.AutoSaveProgressBar.IsIndeterminate = false;
            //        MainPage.RootPage.AutoSaveTextBlock.Text = "已完成";
            //    }
            //    else
            //    {
            //        MainPage.RootPage.AutoSaveProgressBar.Value = 100;
            //        MainPage.RootPage.AutoSaveProgressBar.IsIndeterminate = false;
            //        MainPage.RootPage.AutoSaveProgressBar.ShowError = true;
            //        MainPage.RootPage.AutoSaveTextBlock.Text = "请重试";

            //        MainPage.RootPage.SendNotification("自动备份",
            //            "自动备份任务繁忙，请稍后重试。",
            //            MUXC.InfoBarSeverity.Error);
            //    }

            //    MainPage.RootPage.RefreshBackgroundTasks(1);
            //}

            AutoTasksTimer.Stop();
        }

        private void ChangeViewTimer_Tick(object sender, object e)
        {
            // 计算宽度和高度的缩放比例。
            if (MainRefreshContainer.ActualWidth != 0 &&
                MainRefreshContainer.ActualHeight != 0 &&
                MainStackPanel.ActualWidth != 0 &&
                MainStackPanel.ActualHeight != 0)
            {
                float widthRatio = (float)((MainRefreshContainer.ActualWidth - 50) / MainStackPanel.ActualWidth);
                float heightRatio = (float)((MainRefreshContainer.ActualHeight - 50) / MainStackPanel.ActualHeight);

                // 使用较小的缩放比例以确保控件完全适应父控件。
                float scaleRatio = Math.Min(widthRatio, heightRatio);

                // 应用缩放比例。
                MainScrollViewer.ChangeView(0, 0, scaleRatio);
            }

            ChangeViewTimer.Stop();
        }

        private void SetNewPage()
        {
            MainStackPanel.Children.Clear();

            var resolutionItem = (ResolutionItem)ResolutionComboBox.SelectedItem;
            var width = resolutionItem.Resolution.Width;
            var height = resolutionItem.Resolution.Height;

            var grid = new Grid
            {
                Width = width,
                Height = height
            };

            MainStackPanel.Children.Add(grid);
            RefreshDisplay();
        }

        private void SwitchCoreInputDeviceTypes()
        {
            if (MainTouchToggleButton.IsChecked == true)
            {
                MainInkCanvas.InkPresenter.InputDeviceTypes =
                    Windows.UI.Core.CoreInputDeviceTypes.Mouse |
                    Windows.UI.Core.CoreInputDeviceTypes.Pen |
                    Windows.UI.Core.CoreInputDeviceTypes.Touch;
            }
            else
            {
                MainInkCanvas.InkPresenter.InputDeviceTypes =
                    Windows.UI.Core.CoreInputDeviceTypes.Mouse |
                    Windows.UI.Core.CoreInputDeviceTypes.Pen;
            }
        }

        private void StrokeInput_StrokeEnded(InkStrokeInput sender, PointerEventArgs args)
        {
            StrokeEndedPoint = args.CurrentPoint.Position;
            AutoTasksTimer.Start();
        }

        private void StrokeInput_StrokeStarted(InkStrokeInput sender, PointerEventArgs args)
        {
            AutoTasksTimer.Stop();

            MainPivot.SelectedIndex = 1;
            DragBtn.Visibility = Visibility.Collapsed;
            // LeftTopBtn.Visibility = Visibility.Collapsed;

            MainInkCanvas.InkPresenter.UnprocessedInput.PointerPressed -= UnprocessedInput_PointerPressed;
            MainInkCanvas.InkPresenter.UnprocessedInput.PointerMoved -= UnprocessedInput_PointerMoved;
            MainInkCanvas.InkPresenter.UnprocessedInput.PointerReleased -= UnprocessedInput_PointerReleased;
        }

        private void ResetViewBtn_Click(object sender, RoutedEventArgs e)
        {
            MainScrollViewer.ChangeView(MainScrollViewer.ScrollableWidth / 2,
                MainScrollViewer.ScrollableHeight / 2, 1);
        }

        private void RefreshDisplay()
        {
            if (MainPage.RootPage.IsSketch)
            {
                SketchToggleButton.IsChecked = true;
            }
            else
            {
                SketchToggleButton.IsChecked = false;
            }

            if (Application.Current.RequestedTheme == ApplicationTheme.Light)
            {
                //SwitchThemeFontIcon.Glyph = "\uE708";
            }
            else
            {
                //SwitchThemeFontIcon.Glyph = "\uE706";
            }

            if (Window.Current.Bounds.Width <= 600)
            {
                if (App.IsCompactOverlay == true)
                {
                    MainPivot.Visibility = Visibility.Collapsed;
                    SubInkToolBar.Visibility = Visibility.Visible;
                    SubAppBarSeparator.Visibility = Visibility.Visible;
                    SubToolBar.Margin = new Thickness(0, 0, 24, 24);
                    MainRefreshContainer.Margin = new Thickness(0, 0, 0, 0);
                }
                else
                {
                    MainPivot.Margin = new Thickness(4, -10, 188, 10);
                }
            }
            else
            {
                SubInkToolBar.Visibility = Visibility.Collapsed;
                SubAppBarSeparator.Visibility = Visibility.Collapsed;
                MainPivot.Visibility = Visibility.Visible;
                MainPivot.Margin = new Thickness(188, -10, 188, 10);
                SubToolBar.Margin = new Thickness(0, 0, 24, 124);
                MainRefreshContainer.Margin = new Thickness(0, 0, 0, 100);
            }

            ChangeViewTimer.Stop();
            ChangeViewTimer.Start();
        }

        private void CoreWindow_SizeChanged(CoreWindow sender, WindowSizeChangedEventArgs args)
        {
            RefreshDisplay();
        }

        private void MoveInkToolbarCustomPenButton_Checked(object sender, RoutedEventArgs e)
        {
            MainInkCanvas.InkPresenter.IsInputEnabled = false;
            MainInkCanvas.IsHitTestVisible = false;
            SelectionCanvas.IsHitTestVisible = false;
        }

        private void MoveInkToolbarCustomPenButton_Unchecked(object sender, RoutedEventArgs e)
        {
            MainInkCanvas.InkPresenter.IsInputEnabled = true;
            MainInkCanvas.IsHitTestVisible = true;
            SelectionCanvas.IsHitTestVisible = true;
        }

        private void SwitchThemeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (MainGrid.RequestedTheme == ElementTheme.Light)
            {
                //SwitchThemeFontIcon.Glyph = "\uE706";
                MainGrid.RequestedTheme = ElementTheme.Dark;
            }
            else if (MainGrid.RequestedTheme == ElementTheme.Dark)
            {
                // SwitchThemeFontIcon.Glyph = "\uE708";
                MainGrid.RequestedTheme = ElementTheme.Light;
            }
            else
            {
                if (Application.Current.RequestedTheme == ApplicationTheme.Light)
                {
                    // SwitchThemeFontIcon.Glyph = "\uE706";
                    MainGrid.RequestedTheme = ElementTheme.Dark;
                }
                else
                {
                    //SwitchThemeFontIcon.Glyph = "\uE708";
                    MainGrid.RequestedTheme = ElementTheme.Light;
                }
            }
        }

        private async void RecognizeStorks()
        {
            MainInkStrokes = MainInkCanvas.InkPresenter.StrokeContainer.GetStrokes();

            if (MainInkStrokes.Count > 0)
            {
                MainInkAnalyzer.AddDataForStrokes(MainInkStrokes);

                // In this example, we try to recognizing both 
                // writing and drawing, so the platform default 
                // of "InkAnalysisStrokeKind.Auto" is used.
                // If you're only interested in a specific type of recognition,
                // such as writing or drawing, you can constrain recognition 
                // using the SetStrokDataKind method as follows:
                // foreach (var stroke in strokesText)
                // {
                //     analyzerText.SetStrokeDataKind(
                //      stroke.Id, InkAnalysisStrokeKind.Writing);
                // }
                // This can improve both efficiency and recognition results.
                MainInkAnalysisResults = await MainInkAnalyzer.AnalyzeAsync();

                // Have ink strokes on the canvas changed?
                if (MainInkAnalysisResults.Status == InkAnalysisStatus.Updated)
                {
                    // Find all strokes that are recognized as handwriting and 
                    // create a corresponding ink analysis InkWord node.
                    //var inkwordNodes = MainInkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkWord);

                    // Iterate through each InkWord node.
                    // Draw primary recognized text on recognitionCanvas 
                    // (for this example, we ignore alternatives), and delete 
                    // ink analysis data and recognized strokes.
                    //foreach (InkAnalysisInkWord node in inkwordNodes)
                    //{
                    //    // Draw a TextBlock object on the recognitionCanvas.
                    //    DrawText(node.RecognizedText, node.BoundingRect);

                    //    foreach (var strokeId in node.GetStrokeIds())
                    //    {
                    //        var stroke = MainInkCanvas.InkPresenter.StrokeContainer.GetStrokeById(strokeId);
                    //        stroke.Selected = true;
                    //    }
                    //    MainInkAnalyzer.RemoveDataForStrokes(node.GetStrokeIds());
                    //}
                    //MainInkCanvas.InkPresenter.StrokeContainer.DeleteSelected();

                    // Find all strokes that are recognized as a drawing and 
                    // create a corresponding ink analysis InkDrawing node.
                    var inkdrawingNodes = MainInkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkDrawing);
                    // Iterate through each InkDrawing node.
                    // Draw recognized shapes on recognitionCanvas and
                    // delete ink analysis data and recognized strokes.
                    foreach (InkAnalysisInkDrawing node in inkdrawingNodes)
                    {
                        if (node.DrawingKind == InkAnalysisDrawingKind.Drawing)
                        {
                            // Catch and process unsupported shapes (lines and so on) here.
                        }
                        // Process generalized shapes here (ellipses and polygons).
                        else
                        {
                            // Draw an Ellipse object on the recognitionCanvas (circle is a specialized ellipse).
                            if (node.DrawingKind == InkAnalysisDrawingKind.Circle || node.DrawingKind == InkAnalysisDrawingKind.Ellipse)
                            {
                                DrawEllipse(node);
                            }
                            // Draw a Polygon object on the recognitionCanvas.
                            else
                            {
                                DrawPolygon(node);
                            }
                            foreach (var strokeId in node.GetStrokeIds())
                            {
                                var stroke = MainInkCanvas.InkPresenter.StrokeContainer.GetStrokeById(strokeId);
                                stroke.Selected = true;
                            }
                        }
                        MainInkAnalyzer.RemoveDataForStrokes(node.GetStrokeIds());
                    }
                    MainInkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
                }
            }
        }

        //private void DrawText(string recognizedText, Rect boundingRect)
        //{
        //    TextBlock text = new TextBlock();
        //    Canvas.SetTop(text, boundingRect.Top);
        //    Canvas.SetLeft(text, boundingRect.Left);

        //    text.Text = recognizedText;
        //    text.FontSize = boundingRect.CaptureHeight;
        //    text.Foreground = MainBallpointPen.SelectedBrush;
        //    MainCanvas.Children.Add(text);
        //}

        private void DrawEllipse(InkAnalysisInkDrawing shape)
        {
            var points = shape.Points;
            Ellipse ellipse = new Ellipse();

            ellipse.Width = shape.BoundingRect.Width;
            ellipse.Height = shape.BoundingRect.Height;

            Canvas.SetTop(ellipse, shape.BoundingRect.Top);
            Canvas.SetLeft(ellipse, shape.BoundingRect.Left);

            ellipse.Stroke = MainBallpointPen.SelectedBrush;
            ellipse.StrokeThickness = MainBallpointPen.SelectedStrokeWidth;
        }

        private void DrawPolygon(InkAnalysisInkDrawing shape)
        {
            List<Point> points = new List<Point>(shape.Points);
            Polygon polygon = new Polygon();

            foreach (Point point in points)
            {
                polygon.Points.Add(point);
            }

            polygon.Stroke = MainBallpointPen.SelectedBrush;
            polygon.StrokeThickness = MainBallpointPen.SelectedStrokeWidth;
        }

        private void UpBtn_Click(object sender, RoutedEventArgs e)
        {

        }

        private void LeftBtn_Click(object sender, RoutedEventArgs e)
        {

        }

        private void RightBtn_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            if (MainInkCanvas.InkPresenter.StrokeContainer.GetStrokes().Count > 0)
            {
                MainInkCanvas.InkPresenter.StrokeContainer.Clear();
                // Save the restore point.
                await InkCanvasOperator.SaveRestorePoint();
            }
        }

        private void DrawBoundingRect()
        {
            SelectionCanvas.Children.Clear();

            if (BoundingRect.Width <= 0 || BoundingRect.Height <= 0)
            {
                return;
            }

            var rectangle = new Rectangle()
            {
                Stroke = new SolidColorBrush(Colors.Gray),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection() { 5, 2 },
                Width = BoundingRect.Width,
                Height = BoundingRect.Height
            };

            Canvas.SetLeft(rectangle, BoundingRect.X);
            Canvas.SetTop(rectangle, BoundingRect.Y);

            SelectionCanvas.Children.Add(rectangle);

            DragTranslation.X = 0;
            DragTranslation.Y = 0;
            DragBtn.Width = BoundingRect.Width;
            DragBtn.Height = BoundingRect.Height;
            DragBtn.Margin = new Thickness(BoundingRect.X, BoundingRect.Y, 0, 0);
            MainPivot.SelectedIndex = 3;
        }

        private void DragBtn_ManipulationDelta(object sender, Windows.UI.Xaml.Input.ManipulationDeltaRoutedEventArgs e)
        {
            DragTranslation.X += e.Delta.Translation.X / MainScrollViewer.ZoomFactor;
            DragTranslation.Y += e.Delta.Translation.Y / MainScrollViewer.ZoomFactor;

            Point point = new Point(e.Delta.Translation.X / MainScrollViewer.ZoomFactor,
                e.Delta.Translation.Y / MainScrollViewer.ZoomFactor);
            MainInkCanvas.InkPresenter.StrokeContainer.MoveSelected(point);
        }

        private void DragBtn_Click(object sender, RoutedEventArgs e)
        {
            MainPivot.SelectedIndex = 3;
        }

        private void DragBtn_ManipulationCompleted(object sender, Windows.UI.Xaml.Input.ManipulationCompletedRoutedEventArgs e)
        {
            MainPivot.SelectedIndex = 3;

            // Save the restore point.
            InkCanvasOperator.AutoSaveTimer?.Stop();
            InkCanvasOperator.TimeSpanSetup();
            InkCanvasOperator.AutoSaveTimer.Start();
        }

        private void UnprocessedInput_PointerPressed(InkUnprocessedInput sender, PointerEventArgs args)
        {
            Lasso = new Polyline()
            {
                Stroke = new SolidColorBrush(Colors.Gray),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection() { 5, 2 },
            };

            Lasso.Points.Add(args.CurrentPoint.RawPosition);
            SelectionCanvas.Children.Add(Lasso);
            IsBoundRect = true;

            DragBtn.Visibility = Visibility.Collapsed;
        }

        private void UnprocessedInput_PointerMoved(InkUnprocessedInput sender, PointerEventArgs args)
        {
            if (IsBoundRect)
            {
                Lasso.Points.Add(args.CurrentPoint.RawPosition);
            }
        }

        private void UnprocessedInput_PointerReleased(InkUnprocessedInput sender, PointerEventArgs args)
        {
            MainPivot.SelectedIndex = 1;

            Lasso.Points.Add(args.CurrentPoint.RawPosition);

            BoundingRect = MainInkCanvas.InkPresenter.StrokeContainer.SelectWithPolyLine(Lasso.Points);
            IsBoundRect = false;

            SelectionCanvas.Children.Clear();

            if (BoundingRect.Width <= 0 || BoundingRect.Height <= 0)
            {
                return;
            }

            DragBtn.Width = BoundingRect.Width;
            DragBtn.Height = BoundingRect.Height;
            DragBtn.Margin = new Thickness(BoundingRect.X, BoundingRect.Y, 0, 0);
            DragBtn.Visibility = Visibility.Visible;
            //LeftTopBtn.Visibility = Visibility.Visible;
            MainPivot.SelectedIndex = 3;
        }

        private void SelectionInkToolbarCustomPenButton_Checked(object sender, RoutedEventArgs e)
        {
            MainInkCanvas.InkPresenter.InputProcessingConfiguration.Mode = InkInputProcessingMode.None;
            MainInkCanvas.InkPresenter.UnprocessedInput.PointerPressed += UnprocessedInput_PointerPressed;
            MainInkCanvas.InkPresenter.UnprocessedInput.PointerMoved += UnprocessedInput_PointerMoved;
            MainInkCanvas.InkPresenter.UnprocessedInput.PointerReleased += UnprocessedInput_PointerReleased;
        }

        private async void StartRecordingBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Preview == null)
            {
                ContentDialog errorDialog = new ContentDialog()
                {
                    Title = "出现错误",
                    Content = "没有捕获对象。",
                    CloseButtonText = "确定"
                };

                await errorDialog.ShowAsync();
            }
            else
            {
                var ensureDialog = new ContentDialog
                {
                    Title = "开始捕获",
                    Content = "此操作将清空画布。",
                    PrimaryButtonText = "继续",
                    SecondaryButtonText = "取消",
                };

                var result = await ensureDialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    // Get our encoder properties.
                    var frameRateItem = (FrameRateItem)FrameRateComboBox.SelectedItem;
                    var resolutionItem = (ResolutionItem)ResolutionComboBox.SelectedItem;
                    var bitrateItem = (BitrateItem)BitrateComboBox.SelectedItem;

                    var useSourceSize = resolutionItem.IsZero();
                    var width = resolutionItem.Resolution.Width;
                    var height = resolutionItem.Resolution.Height;
                    var bitrate = bitrateItem.Bitrate;
                    var frameRate = frameRateItem.FrameRate;
                    var includeCursor = GetIncludeCursor();

                    // Use the capture item's size for the encoding if desired.
                    if (useSourceSize)
                    {
                        var targetSize = Preview.Target.Size;
                        width = (uint)targetSize.Width;
                        height = (uint)targetSize.Height;
                    }
                    var resolution = new CaptureEncoder.SizeUInt32() { Width = width, Height = height };

                    var recordingOptions = new RecordingOptions(Preview.Target, resolution, bitrate, frameRate, includeCursor);
                    Preview.Dispose();
                    Preview = null;
                    StartRecordingBtn.IsEnabled = false;

                    Frame.Navigate(typeof(RecordingPage), recordingOptions, new DrillInNavigationTransitionInfo());
                }
            }
        }

        private void CaptureBtn_Click(object sender, RoutedEventArgs e)
        {
            StartCapture();
        }

        private async void ScreenClipBtn_Click(object sender, RoutedEventArgs e)
        {
            string uriToLaunch = "ms-screenclip:";
            var uri = new Uri(uriToLaunch);
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        private void LockBtn_Click(object sender, RoutedEventArgs e)
        {
            if (LockFontIcon.Glyph == "\uE785")
            {
                LockFontIcon.Glyph = "\uE72E";

                StopPreview();
            }
            else
            {
                LockFontIcon.Glyph = "\uE785";
            }
        }

        private void SketchToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            if (MainPage.RootPage.IsMiniSketch)
            {
                MainPage.RootPage.MiniSketch.Visibility = Visibility.Visible;
            }
            else
            {
                MainPage.RootPage.SketchGrid.Visibility = Visibility.Visible;
            }

            MainPage.RootPage.IsSketch = true;
        }

        private void SketchToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            MainPage.RootPage.SketchGrid.Visibility = Visibility.Collapsed;
            MainPage.RootPage.MiniSketch.Visibility = Visibility.Collapsed;
            MainPage.RootPage.IsSketch = false;
        }

        private void OnTouchToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            SwitchCoreInputDeviceTypes();
        }

        private void OnTouchToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            SwitchCoreInputDeviceTypes();
        }

        private void GridToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            MainAlignmentGrid.Visibility = Visibility.Visible;
        }

        private void GridToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            MainAlignmentGrid.Visibility = Visibility.Collapsed;
        }

        private async void OpenInkFileMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            MUXC.ProgressBar progressBar = new MUXC.ProgressBar
            {
                IsIndeterminate = true
            };

            ContentDialog dialog = new ContentDialog()
            {
                Title = "正在打开文件",
                Content = progressBar,
            };

            _ = dialog.ShowAsync();

            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };

            picker.FileTypeFilter.Add(".ink");
            var pickedFile = await picker.PickSingleFileAsync();

            if (pickedFile != null)
            {
                try
                {
                    await InkOperator.LoadInk(pickedFile, MainInkCanvas);
                    dialog.Hide();
                    await InkCanvasOperator.SaveRestorePoint();
                }
                catch (Exception ex)
                {
                    dialog.Title = "出现错误";
                    dialog.CloseButtonText = "确定";
                    dialog.Content = ex;
                }
            }
            else
            {
                dialog.Hide();
            }
        }

        private async void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await CommandsOperator.SaveStep();
            }
            catch (Exception ex)
            {
                MainPage.RootPage.SendNotification("卸载页面失败", ex.ToString(), MUXC.InfoBarSeverity.Error);
            }
        }

        private void InkColorPicker_ColorChanged(MUXC.ColorPicker sender, MUXC.ColorChangedEventArgs args)
        {
            var inkStrokes = MainInkCanvas.InkPresenter.StrokeContainer.GetStrokes();

            foreach (var stroke in inkStrokes)
            {
                if (stroke.Selected == true)
                {
                    InkDrawingAttributes drawingAttributes = stroke.DrawingAttributes;
                    drawingAttributes.Color = InkColorPicker.Color;
                    stroke.DrawingAttributes = drawingAttributes;
                }
            }

            // Save the restore point.
            InkCanvasOperator.AutoSaveTimer?.Stop();
            InkCanvasOperator.TimeSpanSetup();
            InkCanvasOperator.AutoSaveTimer.Start();
        }

        private async void RectangleInkMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            var inkStrokes = MainInkCanvas.InkPresenter.StrokeContainer.GetStrokes();

            foreach (var stroke in inkStrokes)
            {
                if (stroke.Selected == true && stroke.DrawingAttributes.Kind != InkDrawingAttributesKind.Pencil)
                {
                    InkDrawingAttributes drawingAttributes = stroke.DrawingAttributes;
                    drawingAttributes.PenTip = PenTipShape.Rectangle;
                    stroke.DrawingAttributes = drawingAttributes;
                }
            }

            // Save the restore point.
            await InkCanvasOperator.SaveRestorePoint();
        }

        private async void CircleInkMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            var inkStrokes = MainInkCanvas.InkPresenter.StrokeContainer.GetStrokes();

            foreach (var stroke in inkStrokes)
            {
                if (stroke.Selected == true && stroke.DrawingAttributes.Kind != InkDrawingAttributesKind.Pencil)
                {
                    InkDrawingAttributes drawingAttributes = stroke.DrawingAttributes;
                    drawingAttributes.PenTip = PenTipShape.Circle;
                    stroke.DrawingAttributes = drawingAttributes;
                }
            }

            // Save the restore point.
            await InkCanvasOperator.SaveRestorePoint();
        }

        private async void OpaqueInkMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            var inkStrokes = MainInkCanvas.InkPresenter.StrokeContainer.GetStrokes();

            foreach (var stroke in inkStrokes)
            {
                if (stroke.Selected == true && stroke.DrawingAttributes.Kind != InkDrawingAttributesKind.Pencil)
                {
                    InkDrawingAttributes drawingAttributes = stroke.DrawingAttributes;
                    drawingAttributes.DrawAsHighlighter = false;
                    stroke.DrawingAttributes = drawingAttributes;
                }
            }

            // Save the restore point.
            await InkCanvasOperator.SaveRestorePoint();
        }

        private async void TransparentInkMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            var inkStrokes = MainInkCanvas.InkPresenter.StrokeContainer.GetStrokes();

            foreach (var stroke in inkStrokes)
            {
                if (stroke.Selected == true && stroke.DrawingAttributes.Kind != InkDrawingAttributesKind.Pencil)
                {
                    InkDrawingAttributes drawingAttributes = stroke.DrawingAttributes;
                    drawingAttributes.DrawAsHighlighter = true;
                    stroke.DrawingAttributes = drawingAttributes;
                }
            }

            // Save the restore point.
            await InkCanvasOperator.SaveRestorePoint();
        }

        private void PlayInkBtn_Click(object sender, RoutedEventArgs e)
        {
            OnReplay();
        }

        private void OnReplay()
        {
            StopInkBtn.IsEnabled = true;
            UndoBtn.IsEnabled = false;
            RedoBtn.IsEnabled = false;
            PastBtn.IsEnabled = false;
            PlayInkBtn.IsEnabled = false;
            MainInkCanvas.InkPresenter.IsInputEnabled = false;
            DragBtn.Visibility = Visibility.Collapsed;

            SelectionCanvas.Children.Clear();

            if (StrokeBuilder == null)
            {
                StrokeBuilder = new InkStrokeBuilder();
                InkReplayTimer = new DispatcherTimer();
                InkReplayTimer.Interval = new TimeSpan(TimeSpan.TicksPerSecond / FPS);
                InkReplayTimer.Tick += InkReplayTimer_Tick;
            }

            StrokesToReplay = MainInkCanvas.InkPresenter.StrokeContainer.GetStrokes();

            // Calculate the beginning of the earliest stroke and the end of the latest stroke.
            // This establishes the time period during which the strokes were collected.
            BeginTimeOfRecordedSession = DateTimeOffset.MaxValue;
            EndTimeOfRecordedSession = DateTimeOffset.MinValue;
            foreach (InkStroke stroke in StrokesToReplay)
            {
                DateTimeOffset? startTime = stroke.StrokeStartedTime;
                TimeSpan? duration = stroke.StrokeDuration;

                if (startTime.HasValue && duration.HasValue)
                {
                    if (BeginTimeOfRecordedSession > startTime.Value)
                    {
                        BeginTimeOfRecordedSession = startTime.Value;
                    }
                    if (EndTimeOfRecordedSession < startTime.Value + duration.Value)
                    {
                        EndTimeOfRecordedSession = startTime.Value + duration.Value;
                    }
                }
            }

            // If we found at least one stroke with a timestamp, then we can replay.
            if (BeginTimeOfRecordedSession != DateTimeOffset.MaxValue)
            {
                DurationOfRecordedSession = EndTimeOfRecordedSession - BeginTimeOfRecordedSession;

                ReplayProgressBar.Maximum = DurationOfRecordedSession.TotalMilliseconds;
                ReplayProgressBar.Value = 0.0;
                ReplayProgressBar.Visibility = Visibility.Visible;

                BeginTimeOfReplay = DateTime.Now;
                InkReplayTimer.Start();
            }
            else
            {
                // There was nothing to replay. Either there were no strokes at all,
                // or none of the strokes had timestamps.
                StopReplay();
            }
        }


        private void StopReplay()
        {
            InkReplayTimer?.Stop();

            PlayInkBtn.IsEnabled = true;
            MainInkCanvas.InkPresenter.IsInputEnabled = true;
            ReplayProgressBar.Visibility = Visibility.Collapsed;
            StopInkBtn.IsEnabled = false;
            UndoBtn.IsEnabled = true;
            PastBtn.IsEnabled = true;
        }

        private void InkReplayTimer_Tick(object sender, object e)
        {
            var currentTimeOfReplay = DateTimeOffset.Now;
            TimeSpan timeElapsedInReplay = currentTimeOfReplay - BeginTimeOfReplay;

            ReplayProgressBar.Value = timeElapsedInReplay.TotalMilliseconds;

            DateTimeOffset timeEquivalentInRecordedSession = BeginTimeOfRecordedSession + timeElapsedInReplay;
            MainInkCanvas.InkPresenter.StrokeContainer = GetCurrentStrokesView(timeEquivalentInRecordedSession);

            if (timeElapsedInReplay > DurationOfRecordedSession)
            {
                StopReplay();
            }
        }

        private InkStrokeContainer GetCurrentStrokesView(DateTimeOffset time)
        {
            var inkStrokeContainer = new InkStrokeContainer();

            // The purpose of this sample is to demonstrate the timestamp usage,
            // not the algorithm. (The time complexity of the code is O(N^2).)
            foreach (InkStroke stroke in StrokesToReplay)
            {
                InkStroke s = GetPartialStroke(stroke, time);
                if (s != null)
                {
                    inkStrokeContainer.AddStroke(s);
                }
            }

            return inkStrokeContainer;
        }

        private InkStroke GetPartialStroke(InkStroke stroke, DateTimeOffset time)
        {
            DateTimeOffset? startTime = stroke.StrokeStartedTime;
            TimeSpan? duration = stroke.StrokeDuration;

            if (!startTime.HasValue || !duration.HasValue)
            {
                // If a stroke does not have valid timestamp, then treat it as
                // having been drawn before the playback started.
                // We must return a clone of the stroke, because a single stroke cannot
                // exist in more than one container.
                return stroke.Clone();
            }

            if (time < startTime.Value)
            {
                // Stroke has not started
                return null;
            }

            if (time >= startTime.Value + duration.Value)
            {
                // Stroke has already ended.
                // We must return a clone of the stroke, because a single stroke cannot exist in more than one container.
                return stroke.Clone();
            }

            // Stroke has started but not yet ended.
            // Create a partial stroke on the assumption that the ink points are evenly distributed in time.
            IReadOnlyList<InkPoint> points = stroke.GetInkPoints();
            var portion = (time - startTime.Value).TotalMilliseconds / duration.Value.TotalMilliseconds;
            var count = (int)((points.Count - 1) * portion) + 1;
            var partiaStroke = StrokeBuilder.CreateStrokeFromInkPoints(points.Take(count),
                System.Numerics.Matrix3x2.Identity, startTime, time - startTime);
            partiaStroke.DrawingAttributes = stroke.DrawingAttributes;

            return partiaStroke;
        }

        private void StopInkBtn_Click(object sender, RoutedEventArgs e)
        {
            StopReplay();
        }

        private void MainRefreshContainer_RefreshRequested(RefreshContainer sender, RefreshRequestedEventArgs args)
        {
            SetNewPage();
        }

        private void MainScrollViewer_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {
            double thickness = 2 / MainScrollViewer.ZoomFactor;
            DragBtn.BorderThickness = new Thickness(thickness, thickness, thickness, thickness);
        }

        private async void CutBtn_Click(object sender, RoutedEventArgs e)
        {
            MainInkCanvas.InkPresenter.StrokeContainer.CopySelectedToClipboard();
            MainInkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
            SelectionCanvas.Children.Clear();

            DragBtn.Visibility = Visibility.Collapsed;
            //LeftTopBtn.Visibility = Visibility.Collapsed;

            // Save the restore point.
            await InkCanvasOperator.SaveRestorePoint();
        }

        private void CopyBtn_Click(object sender, RoutedEventArgs e)
        {
            MainInkCanvas.InkPresenter.StrokeContainer.CopySelectedToClipboard();
        }

        private async void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            MainInkCanvas.InkPresenter.StrokeContainer.DeleteSelected();
            SelectionCanvas.Children.Clear();
            DragBtn.Visibility = Visibility.Collapsed;

            // Save the restore point.
            await InkCanvasOperator.SaveRestorePoint();
        }

        private async void PastBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MainInkCanvas.InkPresenter.StrokeContainer.CanPasteFromClipboard())
                {
                    Point point = new Point
                    {
                        X = StrokeEndedPoint.X + 20,
                        Y = StrokeEndedPoint.Y + 20
                    };

                    MainInkCanvas.InkPresenter.StrokeContainer.PasteFromClipboard(point);

                    // Save the restore point.
                    await InkCanvasOperator.SaveRestorePoint();
                }
                else
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "无法粘贴墨迹",
                        CloseButtonText = "确定",
                        Content = "剪贴板数据无效。"
                    };
                    await dialog.ShowAsync();
                }
            }
            catch (Exception)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "无法粘贴墨迹",
                    CloseButtonText = "确定",
                    Content = "出现错误。"
                };
                await dialog.ShowAsync();
            }
        }

        private async void MainScrollViewer_Loaded(object sender, RoutedEventArgs e)
        {
            MainScrollViewer.ChangeView(MainScrollViewer.ScrollableWidth / 2,
                MainScrollViewer.ScrollableHeight / 2, null);
            RefreshDisplay();

            MUXC.ProgressBar progressBar = new MUXC.ProgressBar
            {
                IsIndeterminate = true
            };

            ContentDialog dialog = new ContentDialog()
            {
                Title = "正在准备页面",
                Content = progressBar,
            };

            _ = dialog.ShowAsync();

            try
            {
                await CommandsOperator.SetPage(
                    App.NoteBookGuid,
                    PageGuid,
                    UndoBtn,
                    RedoBtn,
                    DragBtn,
                    MainInkCanvas,
                    MainInkToolbar);
                dialog.Hide();
            }
            catch (Exception ex)
            {
                dialog.Title = "出现错误";
                dialog.CloseButtonText = "确定";
                dialog.Content = ex;
            }
        }

        private void IncludeCursorCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (Preview != null)
            {
                Preview.IsCursorCaptureEnabled = ((CheckBox)sender).IsChecked.Value;
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            CacheCurrentSettings();
            base.OnNavigatingFrom(e);
        }

        public async void StartCapture()
        {
            var settings = GetCurrentSettings();
            PageWidth = settings.CaptureWidth;
            PageHeight = settings.CaptureHeight;

            var picker = new GraphicsCapturePicker();
            var item = await picker.PickSingleItemAsync();

            if (item != null)
            {
                StartPreview(item);
            }
            else
            {
                StopPreview();
            }
        }

        private void StartPreview(GraphicsCaptureItem item)
        {
            SetNewPage();

            MainRefreshContainer.Visibility = Visibility.Visible;
            CaptureInfoStackPanel.Visibility = Visibility.Collapsed;

            var compositor = Window.Current.Compositor;
            Preview?.Dispose();
            Preview = new CapturePreview(Device, item);

            var surface = Preview.CreateSurface(compositor);
            PreviewBrush.Surface = surface;
            Preview.StartCapture();
            
            var includeCursor = GetIncludeCursor();

            if (!includeCursor)
            {
                Preview.IsCursorCaptureEnabled = includeCursor;
            }

            StartRecordingBtn.IsEnabled = true;
            LockBtn.IsEnabled = true;
            LockFontIcon.Glyph = "\uE785";
        }

        public void StopPreview()
        {
            Preview?.Dispose();
            Preview = null;
            StartRecordingBtn.IsEnabled = false;
            LockBtn.IsEnabled = false;
        }

        private bool GetIncludeCursor()
        {
            if (IncludeCursorCheckBox.Visibility == Visibility.Visible)
            {
                return IncludeCursorCheckBox.IsChecked.Value;
            }

            return true;
        }

        private AppSettings GetCurrentSettings()
        {
            var resolutionItem = (ResolutionItem)ResolutionComboBox.SelectedItem;
            var width = resolutionItem.Resolution.Width;
            var height = resolutionItem.Resolution.Height;
            var bitrateItem = (BitrateItem)BitrateComboBox.SelectedItem;
            var bitrate = bitrateItem.Bitrate;
            var frameRateItem = (FrameRateItem)FrameRateComboBox.SelectedItem;
            var frameRate = frameRateItem.FrameRate;
            var includeCursor = GetIncludeCursor();

            return new AppSettings { CaptureWidth = width, CaptureHeight = height, CaptureBitrate = bitrate, CaptureFrameRate = frameRate, CaptureIncludeCursor = includeCursor };
        }

        private AppSettings GetCachedSettings()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            var result = new AppSettings
            {
                CaptureWidth = 1920,
                CaptureHeight = 1080,
                CaptureBitrate = 18000000,
                CaptureFrameRate = 60,
                CaptureIncludeCursor = true
            };

            // Resolution.
            if (localSettings.Values.TryGetValue(nameof(AppSettings.CaptureWidth), out var width) &&
                localSettings.Values.TryGetValue(nameof(AppSettings.CaptureHeight), out var height))
            {
                result.CaptureWidth = (uint)width;
                result.CaptureHeight = (uint)height;
            }

            // CaptureBitrate.
            if (localSettings.Values.TryGetValue(nameof(AppSettings.CaptureBitrate), out var bitrate))
            {
                result.CaptureBitrate = (uint)bitrate;
            }

            // Frame rate.
            if (localSettings.Values.TryGetValue(nameof(AppSettings.CaptureFrameRate), out var frameRate))
            {
                result.CaptureFrameRate = (uint)frameRate;
            }

            // Include cursor.
            if (localSettings.Values.TryGetValue(nameof(AppSettings.CaptureIncludeCursor), out var includeCursor))
            {
                result.CaptureIncludeCursor = (bool)includeCursor;
            }

            return result;
        }

        public void CacheCurrentSettings()
        {
            var settings = GetCurrentSettings();
            CacheSettings(settings);
        }

        private static void CacheSettings(AppSettings settings)
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[nameof(AppSettings.CaptureWidth)] = settings.CaptureWidth;
            localSettings.Values[nameof(AppSettings.CaptureHeight)] = settings.CaptureHeight;
            localSettings.Values[nameof(AppSettings.CaptureBitrate)] = settings.CaptureBitrate;
            localSettings.Values[nameof(AppSettings.CaptureFrameRate)] = settings.CaptureFrameRate;
            localSettings.Values[nameof(AppSettings.CaptureIncludeCursor)] = settings.CaptureIncludeCursor;
        }

        private int GetResolutionIndex(uint width, uint height)
        {
            for (var i = 0; i < Resolutions.Count; i++)
            {
                var resolution = Resolutions[i];
                if (resolution.Resolution.Width == width &&
                    resolution.Resolution.Height == height)
                {
                    return i;
                }
            }
            return -1;
        }

        private int GetBitrateIndex(uint bitrate)
        {
            for (var i = 0; i < Bitrates.Count; i++)
            {
                if (Bitrates[i].Bitrate == bitrate)
                {
                    return i;
                }
            }
            return -1;
        }

        private int GetFrameRateIndex(uint frameRate)
        {
            for (var i = 0; i < FrameRates.Count; i++)
            {
                if (FrameRates[i].FrameRate == frameRate)
                {
                    return i;
                }
            }
            return -1;
        }

        private static T ParseEnumValue<T>(string input)
        {
            return (T)Enum.Parse(typeof(T), input, false);
        }

        struct AppSettings
        {
            public uint CaptureWidth;
            public uint CaptureHeight;
            public uint CaptureBitrate;
            public uint CaptureFrameRate;
            public bool CaptureIncludeCursor;
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            MainPivot.SelectedIndex = 4;
        }

        private void OneNoteToggleButton_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void OneNoteToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {

        }
    }
}