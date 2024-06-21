using SketchNotes.Commands;
using SketchNotes.ContentDialogs;
using SketchNotes.FileIO;
using SketchNotes.TTS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Globalization.NumberFormatting;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Input.Inking.Analysis;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using MUXC = Microsoft.UI.Xaml.Controls;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace SketchNotes.TabPages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class NotePage : Page
    {
        public NotePage()
        {
            InitializeComponent();

            Window.Current.CoreWindow.SizeChanged += CoreWindow_SizeChanged;
            MainInkCanvas.InkPresenter.StrokeInput.StrokeStarted += StrokeInput_StrokeStarted;
            MainInkCanvas.InkPresenter.StrokeInput.StrokeEnded += StrokeInput_StrokeEnded;
            AutoTasksTimer.Tick += TimeSpan_Tick;

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
            LockFontIcon.Glyph = "\uE785";
            SubToolBar.Translation = new Vector3(0, 0, 32);

            AddNewPage();
            RefreshDisplay();
            RefreshPensColor();
        }

        private DispatcherTimer AutoTasksTimer = new DispatcherTimer();
        private Point StrokeEndedPoint;
        private InkAnalyzer MainInkAnalyzer = new InkAnalyzer();
        private IReadOnlyList<InkStroke> MainInkStrokes = null;
        private InkAnalysisResult MainInkAnalysisResults = null;
        private Polyline Lasso;
        private Rect BoundingRect;
        private bool IsBoundRect;
        private bool IsSpeechStarted = false;
        private int SpeechId = -1;
        private const int FPS = 60;
        private string ElementType;
        private string[] SpeechLines;
        private double AutoMoveDistance;
        private double AutoMoveLength;
        private Guid PageGuid = Guid.NewGuid();
        private Matrix3x2 ResizeInkMatrix;
        private DateTimeOffset BeginTimeOfRecordedSession;
        private DateTimeOffset EndTimeOfRecordedSession;
        private TimeSpan DurationOfRecordedSession;
        private DateTime BeginTimeOfReplay;
        private DispatcherTimer InkReplayTimer;
        private InkStrokeBuilder StrokeBuilder;
        private IReadOnlyList<InkStroke> StrokesToReplay;
        private MediaPlayer SpeechMediaPlayer = new MediaPlayer();
        private MediaPlaybackList SpeechMediaPlaybackList = new MediaPlaybackList();

        private void GetCachedSettings()
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;
            var speechName = container.Values["SpeechName"];
            var speechId = container.Values["SpeechId"];
            var enableAutoMove = container.Values["EnableAutoMove"];
            var autoMoveDistance = container.Values["AutoMoveDistance"];
            var autoMoveTime = container.Values["AutoMoveTime"];
            var autoMoveLength = container.Values["AutoMoveLength"];

            if (speechName != null)
            {
                SpeechNameTextBlock.Text = (string)speechName;
            }

            if (speechId != null)
            {
                SpeechId = (int)speechId;
            }

            if ((string)enableAutoMove != "False")
            {
                AutoMoveToggleButton.IsChecked = true;
            }
            else
            {
                AutoMoveToggleButton.IsChecked = false;
            }

            if (autoMoveDistance == null)
            {
                AutoMoveDistance = 100;
            }
            else
            {
                AutoMoveDistance = (double)autoMoveDistance;
            }

            if (autoMoveTime == null)
            {
                AutoTasksTimer.Interval = TimeSpan.FromMilliseconds(500);
            }
            else
            {
                AutoTasksTimer.Interval = TimeSpan.FromMilliseconds((double)autoMoveTime * 1000);
            }

            if (autoMoveLength == null)
            {
                AutoMoveLength = 100;
            }
            else
            {
                AutoMoveLength = (double)autoMoveLength;
            }
        }

        private void TimeSpan_Tick(object sender, object e)
        {
            if (StrokeEndedPoint != null && AutoMoveToggleButton.IsChecked == true)
            {
                if (StrokeEndedPoint.X - MainScrollViewer.HorizontalOffset /
                    MainScrollViewer.ZoomFactor + 100 >= (MainScrollViewer.ActualWidth - AutoMoveDistance) /
                    MainScrollViewer.ZoomFactor)
                {
                    MainScrollViewer.ChangeView(MainScrollViewer.HorizontalOffset + AutoMoveLength, null, null);
                }

                if (StrokeEndedPoint.X - MainScrollViewer.HorizontalOffset /
                    MainScrollViewer.ZoomFactor + 100 <= AutoMoveDistance / MainScrollViewer.ZoomFactor)
                {
                    MainScrollViewer.ChangeView(MainScrollViewer.HorizontalOffset - AutoMoveLength, null, null);
                }

                if (StrokeEndedPoint.Y - MainScrollViewer.VerticalOffset /
                    MainScrollViewer.ZoomFactor + 100 >= (MainScrollViewer.ActualHeight - AutoMoveDistance) /
                    MainScrollViewer.ZoomFactor)
                {
                    MainScrollViewer.ChangeView(null, MainScrollViewer.VerticalOffset + AutoMoveLength, null);
                }

                if (StrokeEndedPoint.Y - MainScrollViewer.VerticalOffset /
                    MainScrollViewer.ZoomFactor + 100 <= AutoMoveDistance / MainScrollViewer.ZoomFactor)
                {
                    MainScrollViewer.ChangeView(null, MainScrollViewer.VerticalOffset - AutoMoveLength, null);
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
            //            "自动备份任务繁忙，正在重试。",
            //            MUXC.InfoBarSeverity.Error);
            //    }

            //    MainPage.RootPage.RefreshBackgroundTasks(1);
            //}

            AutoTasksTimer.Stop();
        }

        private void AddNewPage()
        {
            var grid = new Grid
            {
                Width = 840,
                Height = 1188
            };

            MainStackPanel.Children.Add(grid);
        }

        private void SetNumberBoxNumberFormatter()
        {
            IncrementNumberRounder rounder = new IncrementNumberRounder
            {
                Increment = 0.01,
                RoundingAlgorithm = RoundingAlgorithm.RoundHalfUp
            };

            DecimalFormatter formatter = new DecimalFormatter
            {
                IntegerDigits = 1,
                FractionDigits = 2,
                NumberRounder = rounder
            };

            SpeechLengthNumberBox.NumberFormatter = formatter;
            SpeechSampleNoiseNumberBox.NumberFormatter = formatter;
            SpeechRandomNoiseNumberBox.NumberFormatter = formatter;
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
            MainScrollViewer.ChangeView(MainScrollViewer.ScrollableWidth / 2, 0, 1);
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
                SwitchThemeFontIcon.Glyph = "\uE708";
            }
            else
            {
                SwitchThemeFontIcon.Glyph = "\uE706";
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
                SwitchThemeFontIcon.Glyph = "\uE706";
                MainGrid.RequestedTheme = ElementTheme.Dark;
            }
            else if (MainGrid.RequestedTheme == ElementTheme.Dark)
            {
                SwitchThemeFontIcon.Glyph = "\uE708";
                MainGrid.RequestedTheme = ElementTheme.Light;
            }
            else
            {
                if (Application.Current.RequestedTheme == ApplicationTheme.Light)
                {
                    SwitchThemeFontIcon.Glyph = "\uE706";
                    MainGrid.RequestedTheme = ElementTheme.Dark;
                }
                else
                {
                    SwitchThemeFontIcon.Glyph = "\uE708";
                    MainGrid.RequestedTheme = ElementTheme.Light;
                }
            }

            RefreshPensColor();
        }

        private void RefreshPensColor()
        {
            if (MainGrid.RequestedTheme == ElementTheme.Light)
            {
                MainBallpointPen.SelectedBrushIndex = 0;
                SubBallpointPen.SelectedBrushIndex = 0;
                MainPencil.SelectedBrushIndex = 0;
            }
            else if (MainGrid.RequestedTheme == ElementTheme.Dark)
            {
                MainBallpointPen.SelectedBrushIndex = 1;
                SubBallpointPen.SelectedBrushIndex = 1;
                MainPencil.SelectedBrushIndex = 1;
            }
            else
            {
                if (App.RootTheme == ElementTheme.Light)
                {
                    MainBallpointPen.SelectedBrushIndex = 0;
                    SubBallpointPen.SelectedBrushIndex = 0;
                    MainPencil.SelectedBrushIndex = 0;
                }
                else
                {
                    MainBallpointPen.SelectedBrushIndex = 1;
                    SubBallpointPen.SelectedBrushIndex = 1;
                    MainPencil.SelectedBrushIndex = 1;
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
                    var inkwordNodes = MainInkAnalyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkWord);

                    // Iterate through each InkWord node.
                    // Draw primary recognized text on recognitionCanvas 
                    // (for this example, we ignore alternatives), and delete 
                    // ink analysis data and recognized strokes.
                    foreach (InkAnalysisInkWord node in inkwordNodes)
                    {
                        // Draw a TextBlock object on the recognitionCanvas.
                        DrawText(node.RecognizedText, node.BoundingRect);

                        foreach (var strokeId in node.GetStrokeIds())
                        {
                            var stroke = MainInkCanvas.InkPresenter.StrokeContainer.GetStrokeById(strokeId);
                            stroke.Selected = true;
                        }
                        MainInkAnalyzer.RemoveDataForStrokes(node.GetStrokeIds());
                    }
                    MainInkCanvas.InkPresenter.StrokeContainer.DeleteSelected();

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

        private void DrawText(string recognizedText, Rect boundingRect)
        {
            TextBlock text = new TextBlock();
            Canvas.SetTop(text, boundingRect.Top);
            Canvas.SetLeft(text, boundingRect.Left);

            text.Text = recognizedText;
            text.FontSize = boundingRect.Height;
            text.Foreground = MainBallpointPen.SelectedBrush;
            MainCanvas.Children.Add(text);
        }

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
            MainCanvas.Children.Add(ellipse);
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
            MainCanvas.Children.Add(polygon);
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

        private void DragBtn_ManipulationDelta(object sender, Windows.UI.Xaml.Input.ManipulationDeltaRoutedEventArgs e)
        {
            Point point = new Point(e.Delta.Translation.X / MainScrollViewer.ZoomFactor,
                e.Delta.Translation.Y / MainScrollViewer.ZoomFactor);
            MainInkCanvas.InkPresenter.StrokeContainer.MoveSelected(point);

            var x = e.Delta.Translation.X / MainScrollViewer.ZoomFactor + DragBtn.Margin.Left;
            var y = e.Delta.Translation.Y / MainScrollViewer.ZoomFactor + DragBtn.Margin.Top;
            SetDecorators(x, y, DragBtn.Width, DragBtn.Height);
        }

        private void DragBtn_Click(object sender, RoutedEventArgs e)
        {
            MainPivot.SelectedIndex = 4;
        }

        private void DragBtn_ManipulationCompleted(object sender, Windows.UI.Xaml.Input.ManipulationCompletedRoutedEventArgs e)
        {
            MainPivot.SelectedIndex = 4;

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
            //LeftTopBtn.Visibility = Visibility.Collapsed;
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

            //var inkStrokes = MainInkCanvas.InkPresenter.StrokeContainer.GetStrokes();

            //foreach (var stroke in inkStrokes)
            //{
            //    if (stroke.Selected == true)
            //    {
            //        ResizeInkMatrix = stroke.PointTransform;

            //        break;
            //    }
            //}

            DragBtn.Width = BoundingRect.Width;
            DragBtn.Height = BoundingRect.Height;
            DragBtn.Margin = new Thickness(BoundingRect.X, BoundingRect.Y, 0, 0);
            DragBtn.Visibility = Visibility.Visible;
            //LeftTopBtn.Visibility = Visibility.Visible;
            MainPivot.SelectedIndex = 4;
            ElementType = "Ink";

            //SetDecorators(BoundingRect.X, BoundingRect.Y, BoundingRect.Width, BoundingRect.Height);
        }

        private void MainScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (MainScrollViewer.VerticalOffset == MainScrollViewer.ScrollableHeight)
            {
                ExtendCanvasTeachingTip.IsOpen = true;
            }
            else
            {
                ExtendCanvasTeachingTip.IsOpen = false;
            }
        }

        private void SetDecorators(double btnX, double btnY, double btnWidth, double btnHeight)
        {
            //    if ((MainGrid.ActualWidth * MainScrollViewer.ZoomFactor > MainScrollViewer.ActualWidth || MainGrid.ActualWidth * MainScrollViewer.ZoomFactor > MainScrollViewer.ActualWidth) && ElementType != "Ink")
            //    {
            //        double x = (btnX + 101) * MainScrollViewer.ZoomFactor - MainScrollViewer.HorizontalOffset;
            //        double y = (btnY + 101) * MainScrollViewer.ZoomFactor - MainScrollViewer.VerticalOffset;
            //        double width = btnWidth * MainScrollViewer.ZoomFactor;
            //        double height = btnHeight * MainScrollViewer.ZoomFactor;

            //        LeftTopBtn.Margin = new Thickness(x - 5, y - 5, 0, 0);
            //        TopBtn.Margin = new Thickness(x + width / 2 - 6, y - 5, 0, 0);
            //        RightTopBtn.Margin = new Thickness(x + width - 7, y - 5, 0, 0);
            //        RightBtn.Margin = new Thickness(x + width - 7, y + height / 2 - 6, 0, 0);
            //        RightDownBtn.Margin = new Thickness(x + width - 7, y + height - 7, 0, 0);
            //        DownBtn.Margin = new Thickness(x + width / 2 - 6, y + height - 7, 0, 0);
            //        LeftDownBtn.Margin = new Thickness(x - 5, y + height - 7, 0, 0);
            //        LeftBtn.Margin = new Thickness(x - 5, y + height / 2 - 6, 0, 0);

            //        LeftTopBtn.Visibility = Visibility.Visible;
            //    }
            //    else
            //    {
            //        LeftTopBtn.Visibility = Visibility.Collapsed;
            //    }

            //    double thickness = 2 / MainScrollViewer.ZoomFactor;

            DragBtn.Margin = new Thickness(btnX, btnY, 0, 0);
            DragBtn.Width = btnWidth;
            DragBtn.Height = btnHeight;
        }

        //private void SetElements()
        //{
        //    if (ElementType == "Ink")
        //    {
        //        Matrix3x2 matrix = new Matrix3x2();
        //        matrix.M11 = 0;
        //        matrix.M12 = 0;
        //    }
        //    else if (ElementType == "Picture")
        //    {

        //    }
        //}

        private void SelectionInkToolbarCustomPenButton_Checked(object sender, RoutedEventArgs e)
        {
            MainInkCanvas.InkPresenter.InputProcessingConfiguration.Mode = InkInputProcessingMode.None;
            MainInkCanvas.InkPresenter.UnprocessedInput.PointerPressed += UnprocessedInput_PointerPressed;
            MainInkCanvas.InkPresenter.UnprocessedInput.PointerMoved += UnprocessedInput_PointerMoved;
            MainInkCanvas.InkPresenter.UnprocessedInput.PointerReleased += UnprocessedInput_PointerReleased;
        }

        private async void ScreenClipBtn_Click(object sender, RoutedEventArgs e)
        {
            string uriToLaunch = "ms-screenclip:";
            var uri = new Uri(uriToLaunch);
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        private void ScrollDownBtn_Click(object sender, RoutedEventArgs e)
        {
            MainScrollViewer.ChangeView(null,
                MainScrollViewer.VerticalOffset + MainScrollViewer.ActualHeight / 2, null);

            if (MainScrollViewer.VerticalOffset == MainScrollViewer.ScrollableHeight)
            {
                AddNewPage();
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
                catch (Exception)
                {
                    dialog.Title = "出现错误";
                    dialog.CloseButtonText = "确定";
                    progressBar.IsIndeterminate = false;
                    progressBar.Value = 100;
                    progressBar.ShowError = true;
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
            //IRandomAccessStream stream = new InMemoryRandomAccessStream();
            //await MainInkCanvas.InkPresenter.StrokeContainer.SaveAsync(stream);
            //UndoRedoOperator.SaveRecoveryPoint(stream, PageGuid, App.NoteBookGuid, CurrentStep);

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
            //LeftTopBtn.Visibility = Visibility.Collapsed;

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
            //CurrentStep++;
            //TotalStep = CurrentStep;

            StopReplay();
        }

        private void MainRefreshContainer_RefreshRequested(RefreshContainer sender, RefreshRequestedEventArgs args)
        {
            AddNewPage();
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
            RefreshDisplay();
            GetCachedSettings();

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

        private void MainScrollViewer_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {
            double thickness = 2 / MainScrollViewer.ZoomFactor;
            DragBtn.BorderThickness = new Thickness(thickness, thickness, thickness, thickness);
        }

        private async void InsertImageBtn_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker openFile = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                ViewMode = PickerViewMode.List
            };

            openFile.FileTypeFilter.Add(".png");
            openFile.FileTypeFilter.Add(".jpg");
            openFile.FileTypeFilter.Add(".jpeg");
            openFile.FileTypeFilter.Add(".jpe");
            openFile.FileTypeFilter.Add(".jfif");
            openFile.FileTypeFilter.Add(".bmp");
            openFile.FileTypeFilter.Add(".dib");
            openFile.FileTypeFilter.Add(".gif");
            openFile.FileTypeFilter.Add(".heic");
            openFile.FileTypeFilter.Add(".hif");

            // 选取单个文件。
            StorageFile file = await openFile.PickSingleFileAsync();

            if (file != null)
            {
            }
        }

        private async void SpeechStartBtn_Click(object sender, RoutedEventArgs e)
        {
            SpeechContentDialog speechDialog = new SpeechContentDialog();
            await speechDialog.ShowAsync();

            if (speechDialog.SpeechText != "")
            {
                if (SpeechId == -1)
                {
                    SelectCharacterContentDialog selectCharacterDialog = new SelectCharacterContentDialog();
                    await selectCharacterDialog.ShowAsync();

                    GetCachedSettings();

                    if (SpeechId != -1)
                    {
                        await GenerateSpeech(speechDialog.SpeechText, SpeechId, speechDialog.AddToExistence);
                    }
                }
                else
                {
                    await GenerateSpeech(speechDialog.SpeechText, SpeechId, speechDialog.AddToExistence);
                }
            }
        }

        public async Task GenerateSpeech(
            string text,
            int id,
            bool addToExistence)
        {
            SpeechStartBtn.IsEnabled = false;
            SpeechPlayProgressBar.IsIndeterminate = true;
            SpeechPlayProgressBar.Visibility = Visibility.Visible;
            SpeechBufferProgressBar.Visibility = Visibility.Collapsed;

            try
            {
                SpeechLines = await TextClipper.ClipText(text);

                SpeechMediaPlayer.AutoPlay = true;
                SpeechMediaPlayer.Source = SpeechMediaPlaybackList;
                SpeechMediaPlaybackList.MaxPlayedItemsToKeepOpen = 0;

                if (!addToExistence)
                {
                    SpeechMediaPlaybackList.Items.Clear();
                }

                SpeechMediaPlayer.MediaEnded -= MediaPlayer_MediaEnded;
                SpeechMediaPlayer.CurrentStateChanged -= SpeechMediaPlayer_CurrentStateChanged;
                SpeechMediaPlaybackList.CurrentItemChanged -= SpeechMediaPlaybackList_CurrentItemChanged;
                SpeechMediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
                SpeechMediaPlayer.CurrentStateChanged += SpeechMediaPlayer_CurrentStateChanged;
                SpeechMediaPlaybackList.CurrentItemChanged += SpeechMediaPlaybackList_CurrentItemChanged;
                SpeechBufferProgressBar.Maximum = SpeechLines.Length;
                SpeechPlayProgressBar.Maximum = SpeechLines.Length;

                int currentReadLine = 0;

                while (currentReadLine < SpeechLines.Length)
                {
                    string format;

                    if (FormatComboBox.SelectedIndex == 0)
                    {
                        format = "wav";
                    }
                    else if (FormatComboBox.SelectedIndex == 1)
                    {
                        format = "mp3";
                    }
                    else if (FormatComboBox.SelectedIndex == 2)
                    {
                        format = "ogg";
                    }
                    else
                    {
                        format = "flac";
                    }

                    var file = await SpeechClient.GetSpeech(
                        PageGuid,
                        App.NoteBookGuid,
                        SpeechLines[currentReadLine],
                        id,
                        format,
                        SpeechRandomNoiseNumberBox.Value,
                        SpeechSampleNoiseNumberBox.Value);

                    currentReadLine++;

                    SpeechPlayProgressBar.IsIndeterminate = false;
                    SpeechBufferProgressBar.Visibility = Visibility.Visible;
                    SpeechBufferProgressBar.ShowError = false;
                    SpeechBufferProgressBar.Value = currentReadLine;

                    if (file != null)
                    {
                        MediaSource media = MediaSource.CreateFromStorageFile(file);
                        MediaPlaybackItem item = new MediaPlaybackItem(media);
                        MediaItemDisplayProperties props = item.GetDisplayProperties();
                        props.Type = Windows.Media.MediaPlaybackType.Music;
                        props.MusicProperties.Title = "大声朗读";
                        props.MusicProperties.Artist = SpeechNameTextBlock.Text;
                        item.ApplyDisplayProperties(props);
                        SpeechMediaPlaybackList.Items.Add(item);
                        SpeechPlayBtn.IsEnabled = true;

                        // 实现自动续播。
                        if (IsSpeechStarted == false)
                        {
                            PlaySpeech();
                        }
                    }
                    else
                    {
                        SpeechBufferProgressBar.ShowError = true;
                    }
                }

                SpeechStartBtn.IsEnabled = true;
            }
            catch (Exception ex)
            {
                SpeechStartBtn.IsEnabled = true;
                SpeechPlayProgressBar.Visibility = Visibility.Collapsed;
                SpeechBufferProgressBar.Visibility = Visibility.Collapsed;

                MainPage.RootPage.SendNotification("无法朗读", ex.ToString(),
                    Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error);
            }
        }

        private async void SpeechMediaPlayer_CurrentStateChanged(MediaPlayer sender, object args)
        {
            if (SpeechMediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    SpeechPlayFontIcon.Glyph = "\uE769";
                });
            }
            else
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    SpeechPlayFontIcon.Glyph = "\uE768";
                });
            }
        }

        private async void SpeechMediaPlaybackList_CurrentItemChanged(MediaPlaybackList sender, CurrentMediaPlaybackItemChangedEventArgs args)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                SpeechPlayProgressBar.Value = SpeechMediaPlaybackList.Items.IndexOf(SpeechMediaPlaybackList.CurrentItem) + 1;
                SpeechMediaPlayer.Volume = SpeechVolumeSlider.Value / 100;
                SpeechMediaPlayer.PlaybackSession.PlaybackRate = SpeechSpeedSlider.Value;
            });
        }

        private void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
        {
            PlaySpeech();
        }

        private void PlaySpeech()
        {
            if (SpeechMediaPlaybackList.Items.IndexOf(SpeechMediaPlaybackList.CurrentItem) < SpeechMediaPlaybackList.Items.Count - 1)
            {
                IsSpeechStarted = true;
                SpeechMediaPlaybackList.MoveNext();
                SpeechMediaPlayer.Play();
            }
            else
            {
                IsSpeechStarted = false;
            }
        }

        private void SpeechPreviousBtn_Click(object sender, RoutedEventArgs e)
        {
            SpeechMediaPlaybackList.MovePrevious();
            SpeechMediaPlayer.Play();
        }

        private void SpeechDisposeBtn_Click(object sender, RoutedEventArgs e)
        {
            SpeechBufferProgressBar.Visibility = Visibility.Collapsed;
            SpeechPlayProgressBar.Visibility = Visibility.Collapsed;
            SpeechPlayBtn.IsEnabled = false;
            SpeechMediaPlaybackList.Items.Clear();
        }

        private void SpeechNextBtn_Click(object sender, RoutedEventArgs e)
        {
            SpeechMediaPlaybackList.MoveNext();
            SpeechMediaPlayer.Play();
        }

        private void SpeechPlayBtn_Click(object sender, RoutedEventArgs e)
        {
            if (SpeechMediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
            {
                SpeechMediaPlayer.Pause();
            }
            else
            {
                SpeechMediaPlayer.Play();
            }
        }

        private async void SpeechSelectBtn_Click(object sender, RoutedEventArgs e)
        {
            SelectCharacterContentDialog dialog = new SelectCharacterContentDialog();
            await dialog.ShowAsync();

            GetCachedSettings();
        }

        private void MainPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainPivot.SelectedIndex == 5)
            {
                GetCachedSettings();
                SetNumberBoxNumberFormatter();
            }
        }

        private void SpeechNumberBox_OnValueChanged(MUXC.NumberBox sender, MUXC.NumberBoxValueChangedEventArgs args)
        {
            if (double.IsNaN(sender.Value))
            {
                if (sender.Name == "SpeechLengthNumberBox")
                {
                    sender.Value = 1.00;
                }
                else if (sender.Name == "SpeechSampleNoiseNumberBox")
                {
                    sender.Value = 0.33;
                }
                else if (sender.Name == "SpeechRandomNoiseNumberBox")
                {
                    sender.Value = 0.40;
                }
            }
        }

        private void SpeechVolumeSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            SpeechMediaPlayer.Volume = SpeechVolumeSlider.Value / 100;
        }

        private void SpeechSpeedSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            SpeechMediaPlayer.PlaybackSession.PlaybackRate = SpeechSpeedSlider.Value;
        }

        private void AutoMoveToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;
            container.Values["EnableAutoMove"] = "True";
        }

        private void AutoMoveToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;
            container.Values["EnableAutoMove"] = "False";
        }
    }
}