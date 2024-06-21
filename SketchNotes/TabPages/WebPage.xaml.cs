using Microsoft.Web.WebView2.Core;
using SketchNotes.Commands;
using SketchNotes.FileIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Input.Inking.Analysis;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;
using MUXC = Microsoft.UI.Xaml.Controls;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace SketchNotes.TabPages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class WebPage : Page
    {
        public WebPage()
        {
            InitializeComponent();

            Window.Current.CoreWindow.SizeChanged += CoreWindow_SizeChanged;
            MainInkCanvas.InkPresenter.StrokeInput.StrokeStarted += StrokeInput_StrokeStarted;
            MainInkCanvas.InkPresenter.StrokeInput.StrokeEnded += StrokeInput_StrokeEnded;
            AutoTasksTimer.Tick += AutoTasksTimer_Tick;
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
            MainPencil.SelectedStrokeWidth = 6;
            MainBallpointPen.SelectedBrushIndex = 7;
            MainPencil.SelectedBrushIndex = 7;
            DragTranslation = new TranslateTransform();
            DragBtn.RenderTransform = DragTranslation;
            AddressToolBar.Translation = new Vector3(0, 0, 32);
            AutoTasksTimer.Interval = TimeSpan.FromMilliseconds(500);
            ChangeViewTimer.Interval = TimeSpan.FromMilliseconds(500);

            AddNewPage();
            RefreshDisplay();
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
        private bool PageLoaded = false;
        private bool IsWebLoading = false;
        private bool IsWebSuccess = true;
        private TranslateTransform DragTranslation;
        private const int FPS = 60;
        private int PageCount = 1;
        private Guid PageGuid = Guid.NewGuid();
        private DateTimeOffset BeginTimeOfRecordedSession;
        private DateTimeOffset EndTimeOfRecordedSession;
        private TimeSpan DurationOfRecordedSession;
        private DateTime BeginTimeOfReplay;
        private DispatcherTimer InkReplayTimer;
        private InkStrokeBuilder StrokeBuilder;
        private IReadOnlyList<InkStroke> StrokesToReplay;

        private void AutoTasksTimer_Tick(object sender, object e)
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
            if (SubModeSegmented.SelectedIndex == 0)
            {
                OutBorder.BorderThickness = new Thickness(0, 0, 0, 0);

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
            }
            else
            {
                OutBorder.BorderThickness = new Thickness(100, 100, 100, 100);
            }

            ChangeViewTimer.Stop();
        }

        private void GetCachedSettings()
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;
            var enableBackground = container.Values["WebPageEnableBackground"];
            var liveBackground = container.Values["WebPageLiveBackground"];
            var opacityBackground = container.Values["WebPageOpacityBackground"];

            if ((string)enableBackground != "False")
            {
                BackgroundMediaElement.PosterSource = new BitmapImage(
                    new Uri("ms-appx:///Assets/WebPage.jpg"));

                if ((string)liveBackground != "False")
                {
                    BackgroundMediaElement.Source = new Uri("ms-appx:///Assets/WebPage.mp4");
                }

                if (opacityBackground != null)
                {
                    BackgroundMediaElement.Opacity = (double)opacityBackground / 100;
                }

                BackgroundMediaElement.Visibility = Visibility.Visible;
            }
            else
            {
                BackgroundMediaElement.Visibility = Visibility.Collapsed;
            }
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
                    AddressAutoSuggestBox.Visibility = Visibility.Collapsed;
                    AddressToolBar.Margin = new Thickness(0, 0, 24, 24);
                    MainRefreshContainer.Margin = new Thickness(0, 0, 0, 0);
                }
                else
                {
                    MainPivot.Margin = new Thickness(4, 0, 188, 10);
                }
            }
            else
            {
                MainPivot.Visibility = Visibility.Visible;
                AddressAutoSuggestBox.Visibility = Visibility.Visible;
                MainPivot.Margin = new Thickness(188, 0, 188, 10);
                AddressToolBar.Margin = new Thickness(0, 0, 24, 124);
                MainRefreshContainer.Margin = new Thickness(0, 0, 0, 100);
            }

            ChangeViewTimer.Stop();
            ChangeViewTimer.Start();
        }

        private void AddNewPage()
        {
            var grid = new Grid
            {
                Width = 1188,
                Height = 840
            };

            MainStackPanel.Children.Add(grid);
        }

        private void CoreWindow_SizeChanged(CoreWindow sender, WindowSizeChangedEventArgs args)
        {
            RefreshDisplay();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (MainRefreshContainer.Visibility == Visibility.Collapsed &&
                ErrorInfoStackPanel.Visibility == Visibility.Collapsed)
            {
                GetCachedSettings();
            }

            if (!PageLoaded)
            {
                try
                {
                    Uri uri = new Uri(MainPage.RootPage.NewPageUrl);
                    MainWebView.Source = uri;
                }
                catch (Exception)
                {
                    // TODO
                }

                MoveInkToolbarCustomPenButton.IsChecked = true;
                PageLoaded = true;
            }
        }

        private void OnNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            MainWebView.Source = new Uri(e.Uri.ToString());
            e.Handled = true;
        }

        private void MainWebView_NavigationStarting(Microsoft.UI.Xaml.Controls.WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs args)
        {
            IsWebLoading = true;
            WebProgressBar.ShowError = false;
            WebProgressBar.Visibility = Visibility.Visible;
            MainWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            MainWebView.CoreWebView2.Settings.IsPinchZoomEnabled = false;
        }

        private void MainWebView_NavigationCompleted(Microsoft.UI.Xaml.Controls.WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            if (!args.IsSuccess)
            {
                if (args.WebErrorStatus == CoreWebView2WebErrorStatus.ServerUnreachable)
                {
                    ShowErrorInfo(6);
                }
                else if (args.WebErrorStatus == CoreWebView2WebErrorStatus.Timeout)
                {
                    ShowErrorInfo(7);
                }
                else if (args.WebErrorStatus == CoreWebView2WebErrorStatus.ErrorHttpInvalidServerResponse)
                {
                    ShowErrorInfo(8);
                }
                else if (args.WebErrorStatus == CoreWebView2WebErrorStatus.Disconnected)
                {
                    ShowErrorInfo(11);
                }
                else if (args.WebErrorStatus == CoreWebView2WebErrorStatus.CannotConnect)
                {
                    ShowErrorInfo(12);
                }
                else if (args.WebErrorStatus == CoreWebView2WebErrorStatus.HostNameNotResolved)
                {
                    ShowErrorInfo(13);
                }
                else if (args.WebErrorStatus == CoreWebView2WebErrorStatus.OperationCanceled)
                {
                    ShowErrorInfo(14);
                }
                else if (args.WebErrorStatus == CoreWebView2WebErrorStatus.RedirectFailed)
                {
                    ShowErrorInfo(15);
                }
                else if (args.WebErrorStatus == CoreWebView2WebErrorStatus.UnexpectedError)
                {
                    ShowErrorInfo(16);
                }
                else
                {
                    ShowErrorInfo(19);
                }
            }
            else
            {
                MainRefreshContainer.Visibility = Visibility.Visible;
                ErrorInfoStackPanel.Visibility = Visibility.Collapsed;
                WebProgressBar.Visibility = Visibility.Collapsed;
            }

            IsWebLoading = false;
            IsWebSuccess = args.IsSuccess;
            MainBackBtn.IsEnabled = true;
            MainRefreshBtn.IsEnabled = true;
            BackgroundMediaElement.Visibility = Visibility.Collapsed;
            BackgroundMediaElement.Source = null;
            BackgroundMediaElement.PosterSource = null;
            AddressAutoSuggestBox.Text = MainWebView.Source.ToString();

            if (MainWebView.CanGoForward == false)
            {
                MainForwardBtn.IsEnabled = false;
            }

            RefreshDisplay();
        }

        private void ShowErrorInfo(int mode)
        {
            MainRefreshContainer.Visibility = Visibility.Collapsed;
            WebProgressBar.ShowError = true;

            if (mode == 6)
            {
                ErrorInfoImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/WebCannotConnect.png"));
                ErrorInfoTitleTextBlock.Text = "拒绝访问";
                ErrorInfoDescriptionTextBlock.Text = "无法访问主机。";
            }
            else if (mode == 7)
            {
                ErrorInfoImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/WebTimeout.png"));
                ErrorInfoTitleTextBlock.Text = "连接已超时";
                ErrorInfoDescriptionTextBlock.Text = "花了太长时间进行响应。";
            }
            else if (mode == 8)
            {
                ErrorInfoImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/WebDisconnected.png"));
                ErrorInfoTitleTextBlock.Text = "欲买桂花同载酒...";
                ErrorInfoDescriptionTextBlock.Text = "服务器返回了无效或无法识别的响应。";
            }
            else if (mode == 11)
            {
                ErrorInfoImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/WebCannotConnect.png"));
                ErrorInfoTitleTextBlock.Text = "你被 Internet 抛弃了";
                ErrorInfoDescriptionTextBlock.Text = "Internet 连接已丢失。";
            }
            else if (mode == 12)
            {
                ErrorInfoImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/WebCannotConnect.png"));
                ErrorInfoTitleTextBlock.Text = "你被 Internet 抛弃了";
                ErrorInfoDescriptionTextBlock.Text = "未建立与目标的连接。";
            }
            else if (mode == 13)
            {
                ErrorInfoImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/WebCannotConnect.png"));
                ErrorInfoTitleTextBlock.Text = "欲买桂花同载酒...";
                ErrorInfoDescriptionTextBlock.Text = "无法解析提供的主机名。";
            }
            else if (mode == 14)
            {
                ErrorInfoImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/WebAborted.png"));
                ErrorInfoTitleTextBlock.Text = "操作已取消";
                ErrorInfoDescriptionTextBlock.Text = "已取消当前操作。";
            }
            else if (mode == 15)
            {
                ErrorInfoImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/WebRedirectFailed.png"));
                ErrorInfoTitleTextBlock.Text = "重来？";
                ErrorInfoDescriptionTextBlock.Text = "请求重定向失败。";
            }
            else if (mode == 16)
            {
                ErrorInfoImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/WebUnknownError.png"));
                ErrorInfoTitleTextBlock.Text = "出现错误";
                ErrorInfoDescriptionTextBlock.Text = "发生意外错误。";
            }
            else
            {
                ErrorInfoImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/WebDisconnected.png"));
                ErrorInfoTitleTextBlock.Text = "欲买桂花同载酒...";
                ErrorInfoDescriptionTextBlock.Text = "无法访问此页面。";
            }

            ErrorInfoStackPanel.Visibility = Visibility.Visible;
        }

        private void MainWebView_CoreProcessFailed(Microsoft.UI.Xaml.Controls.WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2ProcessFailedEventArgs args)
        {

        }

        private void MainWebView_CoreWebView2Initialized(Microsoft.UI.Xaml.Controls.WebView2 sender, Microsoft.UI.Xaml.Controls.CoreWebView2InitializedEventArgs args)
        {
            MainWebView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
        }

        private void BackBtn_OnClick(object sender, RoutedEventArgs e)
        {
            MainWebView.GoBack();

            MainForwardBtn.IsEnabled = true;

            if (MainWebView.CanGoBack == false)
            {
                MainBackBtn.IsEnabled = false;
                MainRefreshBtn.IsEnabled = false;
                AddressAutoSuggestBox.Text = "";
                MainRefreshContainer.Visibility = Visibility.Collapsed;
                ErrorInfoStackPanel.Visibility = Visibility.Collapsed;

                GetCachedSettings();
            }
        }

        private void ForwardBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (MainRefreshContainer.Visibility == Visibility.Visible)
            {
                MainWebView.GoForward();
            }
            else
            {
                if (IsWebSuccess)
                {
                    MainRefreshContainer.Visibility = Visibility.Visible;
                }
                else
                {
                    ErrorInfoStackPanel.Visibility = Visibility.Visible;
                }

                AddressAutoSuggestBox.Text = MainWebView.Source.ToString();
                BackgroundMediaElement.Visibility = Visibility.Collapsed;
                BackgroundMediaElement.Source = null;
                BackgroundMediaElement.PosterSource = null;
            }

            MainBackBtn.IsEnabled = true;
            MainRefreshBtn.IsEnabled = true;

            if (MainWebView.CanGoForward == false)
            {
                MainForwardBtn.IsEnabled = false;
            }
        }

        private void RefreshBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (!IsWebLoading)
            {
                MainWebView.Reload();
            }
        }

        private void AddressAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (sender.Text != "")
            {
                if (Regex.IsMatch(sender.Text, @"((http|https|ftp)://)(([a-zA-Z0-9\._-]+\.[a-zA-Z]{2,6})|([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}))(:[0-9]{1,4})*(/[a-zA-Z0-9\&%_\./-~-]*)?"))
                {
                    MainWebView.Source = new Uri(sender.Text);
                }
                else if (Regex.IsMatch(sender.Text, @"(([a-zA-Z0-9\._-]+\.[a-zA-Z]{2,6})|([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}))(:[0-9]{1,4})*(/[a-zA-Z0-9\&%_\./-~-]*)?"))
                {
                    MainWebView.Source = new Uri("https://" + sender.Text);
                }
                else if (Regex.IsMatch(sender.Text, @"(file://)"))
                {
                    MainWebView.Source = new Uri(sender.Text);
                }
                else if (Regex.IsMatch(sender.Text, @"(edge://)"))
                {
                    MainWebView.Source = new Uri(sender.Text);
                }
                else
                {
                    if (Regex.IsMatch(sender.Text, @"([a-zA-Z]:\\)"))
                    {
                        MainWebView.Source = new Uri("file://" + sender.Text);
                    }
                    else
                    {
                        MainWebView.Source = new Uri("https://www.bing.com/search?q=" + sender.Text);
                    }
                }
            }
        }

        private void DetailInfoHyperlinkButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void AddressAutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (AddressAutoSuggestBox.Text != "")
            {
                if (Regex.IsMatch(AddressAutoSuggestBox.Text, @"((http|https|ftp)://)(([a-zA-Z0-9\._-]+\.[a-zA-Z]{2,6})|([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}))(:[0-9]{1,4})*(/[a-zA-Z0-9\&%_\./-~-]*)?"))
                {
                    UrlFontIcon.Glyph = "\uE774";
                    UrlTextBlock.Text = sender.Text;
                }
                else if (Regex.IsMatch(sender.Text, @"(file://)"))
                {
                    UrlFontIcon.Glyph = "\uE7C3";
                    UrlTextBlock.Text = sender.Text;
                }
                else if (Regex.IsMatch(sender.Text, @"(edge://)"))
                {
                    UrlFontIcon.Glyph = "\uE90F";
                    UrlTextBlock.Text = sender.Text;
                }
                else
                {
                    if (Regex.IsMatch(sender.Text, @"([a-zA-Z]:\\)"))
                    {
                        UrlFontIcon.Glyph = "\uE7C3";
                        UrlTextBlock.Text = "file://" + sender.Text;
                    }
                    else
                    {
                        UrlFontIcon.Glyph = "\uE774";
                        UrlTextBlock.Text = "https://" + sender.Text;
                    }
                }

                SearchTextBlock.Text = sender.Text;
            }
        }

        private void AddressAutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem == UrlStackPanel)
            {
                sender.Text = UrlTextBlock.Text;
            }
            else
            {
                sender.Text = "https://www.bing.com/search?q=" + sender.Text;
            }
        }

        private void DragArea_DragDelta(object sender, Windows.UI.Xaml.Controls.Primitives.DragDeltaEventArgs e)
        {
            double x = -AddressToolBar.Margin.Right;
            double y = -AddressToolBar.Margin.Bottom;
            x += e.HorizontalChange;
            y += e.VerticalChange;

            if (x < 0 && y < -100)
            {
                AddressToolBar.Margin = new Thickness(0, 0, -x, -y);
            }
        }

        private void CloseAddressToolBarBtn_Click(object sender, RoutedEventArgs e)
        {
            AddressToolBar.Visibility = Visibility.Collapsed;
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

            BackgroundMediaElement.Source = null;
            BackgroundMediaElement.PosterSource = null;
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

        private void StrokeInput_StrokeEnded(InkStrokeInput sender, PointerEventArgs args)
        {
            StrokeEndedPoint = args.CurrentPoint.Position;
            AutoTasksTimer.Start();
        }

        private void StrokeInput_StrokeStarted(InkStrokeInput sender, PointerEventArgs args)
        {
            AutoTasksTimer?.Stop();

            MainPivot.SelectedIndex = 1;
            DragBtn.Visibility = Visibility.Collapsed;

            MainInkCanvas.InkPresenter.UnprocessedInput.PointerPressed -= UnprocessedInput_PointerPressed;
            MainInkCanvas.InkPresenter.UnprocessedInput.PointerMoved -= UnprocessedInput_PointerMoved;
            MainInkCanvas.InkPresenter.UnprocessedInput.PointerReleased -= UnprocessedInput_PointerReleased;
        }

        private void ResetViewBtn_Click(object sender, RoutedEventArgs e)
        {
            MainScrollViewer.ChangeView(MainScrollViewer.ScrollableWidth / 2, 0, 1);
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

            DragBtn.Width = BoundingRect.Width;
            DragBtn.Height = BoundingRect.Height;
            DragBtn.Margin = new Thickness(BoundingRect.X, BoundingRect.Y, 0, 0);
            DragBtn.Visibility = Visibility.Visible;
            MainPivot.SelectedIndex = 3;
        }

        private void SelectionInkToolbarCustomPenButton_Checked(object sender, RoutedEventArgs e)
        {
            MainBallpointPen.IsChecked = false;
            MainPencil.IsChecked = false;
            MainHighlighter.IsChecked = false;

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

        private async void MainRefreshContainer_RefreshRequested(Windows.UI.Xaml.Controls.RefreshContainer sender, Windows.UI.Xaml.Controls.RefreshRequestedEventArgs args)
        {
            if (MainModeSegmented.SelectedIndex == 0)
            {
                MainWebView.Reload();
            }
            else
            {
                PageCount = MainStackPanel.Children.Count + 1;
                await RestorePage();
            }
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

        private async void MainModeSegmented_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainRefreshContainer.Visibility == Visibility.Visible)
            {
                if (SubModeSegmented.SelectedIndex == 0)
                {
                    RefreshImage.Visibility = Visibility.Collapsed;
                    MainImage.Visibility = Visibility.Collapsed;
                    MainImage.Source = null;
                    RefreshFontIcon.Glyph = "\uE72C";
                    PageCount = MainStackPanel.Children.Count;
                    MainStackPanel.Children.Clear();
                    AddNewPage();
                }
                else
                {
                    RefreshImage.Visibility = Visibility.Visible;
                    RefreshFontIcon.Glyph = "\uE74B";
                    await RestorePage();
                }

                RefreshDisplay();
            }
        }

        private async Task RestorePage()
        {
            WebProgressBar.Visibility = Visibility.Visible;
            ReloadImageBtn.IsEnabled = false;
            MainStackPanel.Children.Clear();

            for (int i = 1; i <= PageCount; i++)
            {
                AddNewPage();
            }

            await Task.Delay(500);

            RenderTargetBitmap bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(MainWebView);
            MainImage.Source = bitmap;
            MainImage.Visibility = Visibility.Visible;
            WebProgressBar.Visibility = Visibility.Collapsed;
            ReloadImageBtn.IsEnabled = true;
        }

        private void ResetAddressToolBarBtn_Click(object sender, RoutedEventArgs e)
        {
            AddressToolBar.Margin = new Thickness(0, 0, 24, 124);
            AddressToolBar.Visibility = Visibility.Visible;
        }

        private void DevToolBtn_Click(object sender, RoutedEventArgs e)
        {
            MainWebView.CoreWebView2.OpenDevToolsWindow();
        }

        private async void ReloadImageBtn_Click(object sender, RoutedEventArgs e)
        {
            await RestorePage();
        }
    }
}
