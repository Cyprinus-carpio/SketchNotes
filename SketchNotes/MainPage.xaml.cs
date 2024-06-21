using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;
using SketchNotes.ContentDialogs;
using SketchNotes.TabPages;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using MUXC = Microsoft.UI.Xaml.Controls;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace SketchNotes
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();

            NavigationCacheMode = NavigationCacheMode.Enabled;

            // Hide default title bar.
            CoreApplicationViewTitleBar coreTitleBar =
                CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;

            // Set caption buttons background to transparent.
            ApplicationViewTitleBar titleBar =
                ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;

            // Set XAML element as a drag region.
            Window.Current.SetTitleBar(AppTitleBar);

            // Register a handler for when the size of the overlaid caption control changes.
            coreTitleBar.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;

            // Register a handler for when the title bar visibility changes.
            // For example, when the title bar is invoked in full screen mode.
            coreTitleBar.IsVisibleChanged += CoreTitleBar_IsVisibleChanged;

            // Register a handler for when the window activation changes.
            Window.Current.CoreWindow.Activated += CoreWindow_Activated;
            Window.Current.CoreWindow.SizeChanged += CoreWindow_SizeChanged;

            SketchInkCanvas.InkPresenter.InputDeviceTypes =
                Windows.UI.Core.CoreInputDeviceTypes.Mouse |
                Windows.UI.Core.CoreInputDeviceTypes.Pen |
                Windows.UI.Core.CoreInputDeviceTypes.Touch;

            if (TitleBarGrid.RequestedTheme == ElementTheme.Light)
            {
                SketchBallpointPen.SelectedBrushIndex = 0;
                SketchPencil.SelectedBrushIndex = 0;
            }
            else if (TitleBarGrid.RequestedTheme == ElementTheme.Dark)
            {
                SketchBallpointPen.SelectedBrushIndex = 1;
                SketchPencil.SelectedBrushIndex = 1;
            }
            else
            {
                if (App.RootTheme == ElementTheme.Light)
                {
                    SketchBallpointPen.SelectedBrushIndex = 0;
                    SketchPencil.SelectedBrushIndex = 0;
                }
                else
                {
                    SketchBallpointPen.SelectedBrushIndex = 1;
                    SketchPencil.SelectedBrushIndex = 1;
                }
            }

            RootPage = this;
            SketchBallpointPen.SelectedStrokeWidth = 4;
            SketchPencil.SelectedStrokeWidth = 6;
            CompactViewFontIcon.Glyph = "\uE8A0";
            MainInfoBadge.Value = 0;

            var oauthSettings = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView("Tokens");
            ClientId = oauthSettings.GetString("AppId");
            Scopes = oauthSettings.GetString("Scopes").Split(" ");

            RefreshDisplay();
            ShowBackupTip();
        }

        public int PageCount;
        public bool IsSketch;
        public bool IsMiniSketch;
        public string NewPageUrl;
        public static MainPage RootPage;
        private static IPublicClientApplication PublicClientApp;
        private string[] Scopes;
        private string ClientId;
        private string Authority = "https://login.microsoftonline.com/common";
        private string MSGraphURL = "https://graph.microsoft.com/v1.0/";
        private static AuthenticationResult AuthResult;
        private static IAccount CurrentUserAccount;

        private void GetCachedSettings()
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;
            var isSignedIn = container.Values["IsSignedIn"];

            if ((string)isSignedIn == "True")
            {
                SignIn();
            }
        }

        private void RefreshDisplay()
        {
            if (HomeTabViewItem.IsSelected == true)
            {
                LeftPageTextBlock.Text = "主页";
                LeftNumberBox.Text = "";
                RightPageTextBlock.Text = "主页";
                RightNumberBox.Text = "";

                SketchGrid.Visibility = Visibility.Collapsed;
                MiniSketch.Visibility = Visibility.Collapsed;
            }
            else
            {
                int page = MainTabView.SelectedIndex;
                int count = MainTabView.TabItems.Count - 1;
                LeftPageTextBlock.Text = count + " /";
                LeftNumberBox.Text = page + "";
                RightPageTextBlock.Text = count + " /";
                RightNumberBox.Text = page + "";

                if (IsSketch)
                {
                    if (IsMiniSketch)
                    {
                        MiniSketch.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        SketchGrid.Visibility = Visibility.Visible;
                    }
                }
            }

            if (MainTabView.SelectedIndex == MainTabView.TabItems.Count - 1)
            {
                LeftNextFontIcon.Glyph = "\uE710";
                RightNextFontIcon.Glyph = "\uE710";
            }
            else
            {
                LeftNextFontIcon.Glyph = "\uE72A";
                RightNextFontIcon.Glyph = "\uE72A";
            }

            if (MainTabView.SelectedIndex == 0)
            {
                LeftPreviousBtn.IsEnabled = false;
                RightPreviousBtn.IsEnabled = false;
            }
            else
            {
                LeftPreviousBtn.IsEnabled = true;
                RightPreviousBtn.IsEnabled = true;
            }

            ApplicationView view = ApplicationView.GetForCurrentView();
            bool isInFullScreenMode = view.IsFullScreenMode;

            if (isInFullScreenMode)
            {
                FullScreenFontIcon.Glyph = "\uE73F";
                AppTitleStackPanel.Visibility = Visibility.Collapsed;
                AppIcon.Visibility = Visibility.Collapsed;
                MainAutoSuggestBox.Visibility = Visibility.Collapsed;
                MainTabView.Margin = new Thickness(0, -40, 0, 0);
                NotificationStackPanel.Margin = new Thickness(0, 0, 0, 0);
                MiniSketch.Margin = new Thickness(0, 24, 0, 0);
                SketchGrid.Margin = new Thickness(0, 0, 0, 0);
            }
            else
            {
                if (Window.Current.Bounds.Width <= 600)
                {
                    if (App.IsCompactOverlay == true)
                    {
                        RightStackPanel.Visibility = Visibility.Collapsed;
                        AccountBtn.Visibility = Visibility.Collapsed;
                        BackToWindowBtn.Visibility = Visibility.Visible;
                        ToolBarBorder.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        MainAutoSuggestBox.Visibility = Visibility.Collapsed;
                    }

                    SearchBtn.Visibility = Visibility.Visible;
                    FileNameDropDownButton.Visibility = Visibility.Collapsed;
                    LeftStackPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    if (Window.Current.Bounds.Width <= 1000)
                    {
                        MainAutoSuggestBox.Visibility = Visibility.Collapsed;
                        SearchBtn.Visibility = Visibility.Visible;

                        if (Window.Current.Bounds.Width <= 720)
                        {
                            AccountBtn.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            AccountBtn.Visibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        AccountBtn.Visibility = Visibility.Visible;
                        TitleBarGrid.Visibility = Visibility.Visible;
                        MainAutoSuggestBox.Visibility = Visibility.Visible;
                        SearchBtn.Visibility = Visibility.Collapsed;
                    }

                    FileNameDropDownButton.Visibility = Visibility.Visible;
                    LeftStackPanel.Visibility = Visibility.Visible;
                    RightStackPanel.Visibility = Visibility.Visible;
                    BackToWindowBtn.Visibility = Visibility.Collapsed;
                    ToolBarBorder.Visibility = Visibility.Visible;
                }

                FullScreenFontIcon.Glyph = "\uE740";
                AppTitleStackPanel.Visibility = Visibility.Visible;
                AppIcon.Visibility = Visibility.Visible;
                MainTabView.Margin = new Thickness(0, 48, 0, 0);
                NotificationStackPanel.Margin = new Thickness(0, 88, 0, 0);
                MiniSketch.Margin = new Thickness(0, 112, 0, 0);
                SketchGrid.Margin = new Thickness(0, 88, 0, 0);
            }
        }

        private void RefreshSketch()
        {
            if (ResizeGrid.ActualHeight > MainTabView.ActualHeight - 140 && IsSketch)
            {
                ResizeGrid.Height = MainTabView.ActualHeight - 140;
            }
        }

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            CoreApplicationViewTitleBar coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
        }

        private void CoreTitleBar_IsVisibleChanged(CoreApplicationViewTitleBar sender, object args)
        {
            if (sender.IsVisible)
            {
                TitleBarGrid.Visibility = Visibility.Visible;
            }
            else
            {
                TitleBarGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void CoreWindow_Activated(CoreWindow sender, WindowActivatedEventArgs args)
        {
            //UISettings settings = new UISettings();
            //if (args.WindowActivationState == CoreWindowActivationState.Deactivated)
            //{
            //    AppTitleTextBlock.Foreground =
            //       new Windows.UI.Xaml.Media.SolidColorBrush(settings.UIElementColor(UIElementType.GrayText));
            //}
            //else
            //{
            //    AppTitleTextBlock.Foreground =
            //       new Windows.UI.Xaml.Media.SolidColorBrush(settings.UIElementColor(UIElementType.WindowText));
            //}
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e != null)
            {
                base.OnNavigatedTo(e);
                string receiveData = (string)e.Parameter;

                if (receiveData == "-1" && !App.IsContentDialogOpen())
                {
                    LaunchErrorContentDialog dialog = new LaunchErrorContentDialog();
                    await dialog.ShowAsync();
                }
                else if (receiveData == "0")
                {
                    MainTabView.TabItems.Add(CreateNewTab(MainTabView.TabItems.Count, 0));
                }
                else if (receiveData == "1")
                {
                    MainTabView.TabItems.Add(CreateNewTab(MainTabView.TabItems.Count, 1));
                }
                else if (receiveData == "2")
                {
                    MainTabView.TabItems.Add(CreateNewTab(MainTabView.TabItems.Count, 2));
                }
                else if (receiveData == "3")
                {
                    MainTabView.TabItems.Add(CreateNewTab(MainTabView.TabItems.Count, 3));
                }
                else if (receiveData == "4")
                {
                    MainTabView.TabItems.Add(CreateNewTab(MainTabView.TabItems.Count, 4));
                }
                else if (receiveData == "5")
                {
                    MainTabView.TabItems.Add(CreateNewTab(MainTabView.TabItems.Count, 5));
                }

                MainTabView.SelectedIndex = PageCount;
                //if (receiveData == "2")
                //{
                //    MainTabView.TabItems.Add(CreateNewTab(MainTabView.TabItems.Count, 2));
                //    MainTabView.SelectedIndex = PageCount;
                //    MainTabView.IsAddTabButtonVisible = false;
                //    //LeftSettingsCard.IsEnabled = false;
                //    //RightSettingsCard.IsEnabled = false;
                //    HomeTabViewItem.IsEnabled = false;
                //}
            }
        }

        private void MainTabView_AddButtonClick(MUXC.TabView sender, object args)
        {
            sender.TabItems.Add(CreateNewTab(sender.TabItems.Count, 0));
            RefreshDisplay();

            MainTabView.SelectedIndex = PageCount;
        }

        public MUXC.TabViewItem CreateNewTab(int index, int newPageMode)
        {
            PageCount = index;
            MUXC.TabViewItem newItem = new MUXC.TabViewItem();

            if (newPageMode == 0)
            {
                Frame frame = new Frame();
                frame.Navigate(typeof(NotePage));
                newItem.Content = frame;
                newItem.Header = "笔记";
                newItem.IconSource = new MUXC.SymbolIconSource()
                {
                    Symbol = Symbol.Highlight
                };
            }
            else if (newPageMode == 1)
            {
                Frame frame = new Frame();
                frame.Navigate(typeof(WebPage), NewPageUrl);
                newItem.Content = frame;
                newItem.Header = "Web 页";
                newItem.IconSource = new MUXC.SymbolIconSource()
                {
                    Symbol = Symbol.Page2
                };
            }
            else if (newPageMode == 2)
            {
                Frame frame = new Frame();
                frame.Navigate(typeof(MapPage));
                newItem.Content = frame;
                newItem.Header = "地图";
                newItem.IconSource = new MUXC.SymbolIconSource()
                {
                    Symbol = Symbol.Map
                };
            }
            else if (newPageMode == 3)
            {
                Frame frame = new Frame();
                frame.Navigate(typeof(CapturePage));
                newItem.Content = frame;
                newItem.Header = "捕获";
                newItem.IconSource = new MUXC.SymbolIconSource()
                {
                    Symbol = Symbol.Video
                };
            }
            else if (newPageMode == 4)
            {
                Frame frame = new Frame();
                frame.Navigate(typeof(TimerPage));
                newItem.Content = frame;
                newItem.Header = "计时器";
                newItem.IconSource = new MUXC.SymbolIconSource()
                {
                    Symbol = Symbol.Clock
                };
            }
            else if (newPageMode == 5)
            {
                Frame frame = new Frame();
                frame.Navigate(typeof(OOBEPage));
                newItem.Content = frame;
                newItem.Header = "欢迎使用 SketchNotes";
                newItem.IconSource = new MUXC.SymbolIconSource()
                {
                    Symbol = Symbol.OutlineStar
                };
            }
            //else if (newPageMode == 6)
            //{
            //    Frame frame = new Frame();
            //    frame.Navigate(typeof(InfinitePage));
            //    newItem.Content = frame;
            //    newItem.Header = "第 " + PageCount + " 页 - TEST";
            //    newItem.IconSource = new MUXC.SymbolIconSource()
            //    {
            //        Symbol = Symbol.Document
            //    };
            //}
            return newItem;
        }

        public void SendNotification(string title, string message, MUXC.InfoBarSeverity infoBarSeverity)
        {
            MainInfoBadge.Value++;

            var infoBar = new MUXC.InfoBar()
            {
                Title = title,
                Message = message,
                Severity = infoBarSeverity,
                IsOpen = true,
                CornerRadius = new CornerRadius(0, 0, 0, 0),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            infoBar.CloseButtonClick += InfoBar_CloseButtonClick;
            MainInfoBadge.Visibility = Visibility.Visible;
            NotificationStackPanel.Children.Add(infoBar);
            NotificationStackPanel.Visibility = Visibility.Visible;
        }

        private void InfoBar_CloseButtonClick(MUXC.InfoBar sender, object args)
        {
            MainInfoBadge.Value--;
            sender.CloseButtonClick -= InfoBar_CloseButtonClick;
            NotificationStackPanel.Children.Remove(sender);

            if (MainInfoBadge.Value == 0)
            {
                MainInfoBadge.Visibility = Visibility.Collapsed;
                NotificationStackPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void MainTabView_TabCloseRequested(MUXC.TabView sender, MUXC.TabViewTabCloseRequestedEventArgs args)
        {
            sender.TabItems.Remove(args.Tab);

            RefreshDisplay();
        }

        public void RefreshBackgroundTasks(int model)
        {
            if (StorageProgressBar.Value == 100 &&
                AutoSaveProgressBar.Value == 100 &&
                TempProgressBar.Value == 100)
            {
                BackgroundTaskFontIcon.Visibility = Visibility.Visible;
                BackgroundTaskProgressRing.Visibility = Visibility.Collapsed;
            }
            else
            {
                BackgroundTaskFontIcon.Visibility = Visibility.Collapsed;
                BackgroundTaskProgressRing.Visibility = Visibility.Visible;
            }

            if (model == 1)
            {
                AutoSaveListViewItem.Visibility = Visibility.Visible;
            }
            else if (model == 2)
            {
                TempListViewItem.Visibility = Visibility.Visible;
            }
        }

        private void MainTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshDisplay();
        }

        private void CoreWindow_SizeChanged(CoreWindow sender, WindowSizeChangedEventArgs args)
        {
            RefreshDisplay();
            RefreshSketch();
        }

        private void FullScreenBtn_Click(object sender, RoutedEventArgs e)
        {
            ApplicationView view = ApplicationView.GetForCurrentView();
            bool isInFullScreenMode = view.IsFullScreenMode;
            if (isInFullScreenMode)
            {
                view.ExitFullScreenMode();
            }
            else
            {
                view.TryEnterFullScreenMode();
            }
        }

        private void Next(object sender, RoutedEventArgs e)
        {
            int selected = MainTabView.SelectedIndex;

            if (selected >= MainTabView.TabItems.Count - 1)
            {
                MainTabView.TabItems.Add(CreateNewTab(MainTabView.TabItems.Count, 0));
            }

            MainTabView.SelectedIndex = selected + 1;
        }

        private void Previous(object sender, RoutedEventArgs e)
        {
            int selected = MainTabView.SelectedIndex;

            if (selected > 0)
            {
                MainTabView.SelectedIndex = selected - 1;
            }
        }

        public void CloseCurrentPage()
        {
            MainTabView.TabItems.Remove(MainTabView.TabItems[MainTabView.SelectedIndex]);
        }

        private async void PicInPicBtn_Click(object sender, RoutedEventArgs e)
        {
            var preferences = ViewModePreferences.CreateDefault(ApplicationViewMode.CompactOverlay);
            preferences.CustomSize = new Windows.Foundation.Size(500, 500);
            await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay, preferences);

            App.IsCompactOverlay = true;
        }

        private void CompactViewBtn_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabView.TabWidthMode == MUXC.TabViewWidthMode.Equal)
            {
                MainTabView.TabWidthMode = MUXC.TabViewWidthMode.Compact;
                CompactViewFontIcon.Glyph = "\uE89F";
            }
            else
            {
                MainTabView.TabWidthMode = MUXC.TabViewWidthMode.Equal;
                CompactViewFontIcon.Glyph = "\uE8A0";
            }
        }

        private void BackupTeachingTip_CloseButtonClick(MUXC.TeachingTip sender, object args)
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;
            container.Values["BackupTip"] = "False";
        }

        private void ShowBackupTip()
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;
            string tip = container.Values["BackupTip"] as string;

            if (tip != "False")
            {
                BackupTeachingTip.IsOpen = true;
            }
        }

        private void NotificationBtn_Click(object sender, RoutedEventArgs e)
        {
            if (MainInfoBadge.Value != 0)
            {
                NotificationStackPanel.Visibility = NotificationStackPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            GetCachedSettings();

            try
            {
                StorageProgressBar.Value = 0;
                StorageProgressBar.IsIndeterminate = true;
                StorageTextBlock.Text = "正在运行...";

                RefreshBackgroundTasks(0);

                Windows.Storage.StorageFolder storageFolder =
                    Windows.Storage.ApplicationData.Current.LocalFolder;
                Windows.Storage.StorageFolder backupFolder =
                    await storageFolder.CreateFolderAsync("Backup", CreationCollisionOption.OpenIfExists);
                Windows.Storage.StorageFolder tempFolder =
                    await storageFolder.CreateFolderAsync("Temp", CreationCollisionOption.OpenIfExists);
                Windows.Storage.StorageFolder ttsFolder =
                    await storageFolder.CreateFolderAsync("TTS", CreationCollisionOption.OpenIfExists);

                var tempItems = await tempFolder.GetItemsAsync();
                var ttsItems = await ttsFolder.GetItemsAsync();

                foreach (var item in tempItems)
                {
                    if (item.Name != App.NoteBookGuid.ToString())
                    {
                        await item.DeleteAsync();
                    }
                }

                foreach (var item in ttsItems)
                {
                    if (item.Name != App.NoteBookGuid.ToString())
                    {
                        await item.DeleteAsync();
                    }
                }

                ulong folderSize = 0;
                var parentFolders = await backupFolder.GetFoldersAsync();

                foreach (Windows.Storage.StorageFolder parentFolder in parentFolders)
                {
                    var files = await parentFolder.GetFilesAsync();

                    foreach (Windows.Storage.StorageFile file in files)
                    {
                        BasicProperties basicProperties = await file.GetBasicPropertiesAsync();
                        folderSize += basicProperties.Size;
                    }
                }

                var output = folderSize / (1024.0 * 1024.0);

                if (output > 100)
                {
                    SendNotification("存储感知",
                        "自动备份的文件占用的硬盘空间已高达 " + output + " MB，请转到存储以释放存储空间。",
                        MUXC.InfoBarSeverity.Warning);
                }

                StorageProgressBar.Value = 100;
                StorageProgressBar.ShowError = false;
                StorageProgressBar.IsIndeterminate = false;
                StorageTextBlock.Text = "已完成";

                RefreshBackgroundTasks(0);
            }
            catch (Exception)
            {
                StorageProgressBar.Value = 100;
                StorageProgressBar.IsIndeterminate = false;
                StorageProgressBar.ShowError = true;
                StorageTextBlock.Text = "出现错误";

                RefreshBackgroundTasks(0);
            }
        }

        private void BackupToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (BackupToggleSwitch.IsOn == true && MainInfoBadge != null)
            {
                SendNotification("自动备份",
                    "自动备份已启用，需要做出更改以刷新备份。",
                    MUXC.InfoBarSeverity.Warning);
            }
        }

        private void AddPageMenuFlyoutItem_OnClick(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem item = (MenuFlyoutItem)sender;
            string tag = item.Tag.ToString();

            if (tag == "Note")
            {
                App.CreatNewTab("mascot-sketchnotes://new/note");
            }
            else if (tag == "Web")
            {
                App.CreatNewTab("mascot-sketchnotes://new/web");
            }
            else if (tag == "Map")
            {
                App.CreatNewTab("mascot-sketchnotes://new/map");
            }
            else if (tag == "Capture")
            {
                App.CreatNewTab("mascot-sketchnotes://new/capture");
            }
            else if (tag == "Timer")
            {
                App.CreatNewTab("mascot-sketchnotes://new/timer");
            }
            else
            {
                App.CreatNewTab("mascot-sketchnotes://new/oobe");
            }

            MainTabView.SelectedIndex = MainTabView.TabItems.Count - 1;
        }

        private void HomeMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            HomeTabViewItem.IsSelected = true;
        }

        private void CloseAppMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            CoreApplication.Exit();
        }

        private void SignInMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            SignIn();
        }

        public async void SignIn()
        {
            AccountPersonPicture.Visibility = Visibility.Collapsed;
            AccountProgressRing.Visibility = Visibility.Visible;

            try
            {
                // Sign-in user using MSAL and obtain an access token for MS Graph
                GraphServiceClient graphClient = await SignInAndInitializeGraphServiceClient(Scopes);

                // Call the /me endpoint of Graph
                User graphUser = await graphClient.Me.GetAsync();
                Stream photo = await graphClient.Me.Photo.Content.GetAsync();

                if (graphUser != null)
                {
                    ApplicationDataContainer container = ApplicationData.Current.LocalSettings;
                    container.Values["IsSignedIn"] = "True";

                    Windows.Storage.StorageFolder storageFolder =
                        Windows.Storage.ApplicationData.Current.LocalFolder;
                    Windows.Storage.StorageFile file =
                        await storageFolder.CreateFileAsync("UserPhoto.tmp",
                        Windows.Storage.CreationCollisionOption.ReplaceExisting);

                    if (photo != null)
                    {
                        var stream = await file.OpenStreamForWriteAsync();
                        photo.CopyTo(stream);
                        stream.Dispose();
                    }

                    // Go back to the UI thread to make changes to the UI.
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        AccountTextBlock.Text = graphUser.DisplayName;

                        if (photo != null)
                        {
                            BitmapImage bitmapImage = new BitmapImage
                            {
                                UriSource = new Uri(file.Path),
                            };
                            AccountPersonPicture.ProfilePicture = bitmapImage;
                        }

                        //ResultText.Text = "Display Name: " + graphUser.DisplayName + "\nBusiness Phone: " + graphUser.BusinessPhones.FirstOrDefault()
                        //                  + "\nGiven Name: " + graphUser.GivenName + "\nid: " + graphUser.Id
                        //                  + "\nUser Principal Name: " + graphUser.UserPrincipalName;
                        //DisplayBasicTokenInfo(authResult);
                        //SignOutButton.Visibility = Visibility.Visible;
                    });
                }
            }
            catch (MsalException msalEx)
            {
                SendNotification("无法登录到 Microsoft", msalEx.ToString(), MUXC.InfoBarSeverity.Error);
            }
            catch (Exception ex)
            {
                SendNotification("无法登录到 Microsoft", ex.ToString(), MUXC.InfoBarSeverity.Error);
            }

            AccountPersonPicture.Visibility = Visibility.Visible;
            AccountProgressRing.Visibility = Visibility.Collapsed;
        }

        public async Task<BitmapImage> ImageFromBytes(byte[] bytes)
        {
            var image = new BitmapImage();

            try
            {
                var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                await stream.WriteAsync(bytes.AsBuffer());
                stream.Seek(0);
                await image.SetSourceAsync(stream);
            }
            catch (Exception ex)
            {
                SendNotification("无法获取账户头像", ex.ToString(), MUXC.InfoBarSeverity.Error);
            }

            return image;
        }

        private async Task<GraphServiceClient> SignInAndInitializeGraphServiceClient(string[] scopes)
        {
            var tokenProvider = new TokenProvider(SignInUserAndGetTokenUsingMSAL, scopes);
            var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);
            var graphClient = new GraphServiceClient(authProvider, MSGraphURL);

            return await Task.FromResult(graphClient);
        }

        private async Task<string> SignInUserAndGetTokenUsingMSAL(string[] scopes)
        {
            // Initialize the MSAL library by building a public client application
            PublicClientApp = PublicClientApplicationBuilder.Create(ClientId)
                .WithAuthority(Authority)
                .WithBroker(true)
                .Build();

            CurrentUserAccount = CurrentUserAccount ?? (await PublicClientApp.GetAccountsAsync()).FirstOrDefault();

            try
            {
                AuthResult = await PublicClientApp.AcquireTokenSilent(scopes, CurrentUserAccount)
                                                  .ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                AuthResult = await PublicClientApp.AcquireTokenInteractive(scopes)
                                                  .ExecuteAsync()
                                                  .ConfigureAwait(false);
            }

            return AuthResult.AccessToken;
        }

        private void FileNameFlyout_Closed(object sender, object e)
        {
            if (FileNameTextBox.Text == "")
            {
                FileNameTextBox.Text = "新建 SketchNotes 笔记本";
            }
        }

        private async void BackToWindowBtn_Click(object sender, RoutedEventArgs e)
        {
            var preferences = ViewModePreferences.CreateDefault(ApplicationViewMode.Default);
            await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.Default, preferences);

            App.IsCompactOverlay = false;
        }

        private void FullSketchBtn_Click(object sender, RoutedEventArgs e)
        {
            ResizeGrid.Height = MainTabView.ActualHeight - 140;
        }

        private void OpenInNewTabBtn_Click(object sender, RoutedEventArgs e)
        {

        }

        private void MinimizeSketchBtn_Click(object sender, RoutedEventArgs e)
        {
            SketchGrid.Visibility = Visibility.Collapsed;
            MiniSketch.Visibility = Visibility.Visible;
            IsMiniSketch = true;
        }

        private void ClearSketchBtn_Click(object sender, RoutedEventArgs e)
        {
            SketchInkCanvas.InkPresenter.StrokeContainer.Clear();
        }

        private void ResizeGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RefreshSketch();
        }

        private void ShowSketch_Click(object sender, RoutedEventArgs e)
        {
            SketchGrid.Visibility = Visibility.Visible;
            MiniSketch.Visibility = Visibility.Collapsed;
            IsMiniSketch = false;
        }

        private void SearchBtn_Click(object sender, RoutedEventArgs e)
        {

        }

        /// <summary>
        /// Sign out the current user
        /// </summary>
        //private async void SignOutButton_Click(object sender, RoutedEventArgs e)
        //{
        //    IEnumerable<IAccount> accounts = await PublicClientApp.GetAccountsAsync().ConfigureAwait(false);
        //    IAccount firstAccount = accounts.FirstOrDefault();

        //    try
        //    {
        //                    ApplicationDataContainer container = ApplicationData.Current.LocalSettings;
        //container.Values["IsSignedIn"] = "False";

        //        await PublicClientApp.RemoveAsync(firstAccount).ConfigureAwait(false);
        //        await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
        //        {
        //            ResultText.Text = "User has signed out";
        //            this.CallGraphButton.Visibility = Visibility.Visible;
        //            this.SignOutButton.Visibility = Visibility.Collapsed;
        //        });
        //    }
        //    catch (MsalException ex)
        //    {
        //        ResultText.Text = $"Error signing out user: {ex.Message}";
        //    }
        //}

        /// <summary>
        /// Display basic information contained in the token. Needs to be called from the UI thread.
        /// </summary>
        //private void DisplayBasicTokenInfo(AuthenticationResult authResult)
        //{
        //    TokenInfoText.Text = "";
        //    if (authResult != null)
        //    {
        //        TokenInfoText.Text += $"User Name: {authResult.Account.Username}" + Environment.NewLine;
        //        TokenInfoText.Text += $"Token Expires: {authResult.ExpiresOn.ToLocalTime()}" + Environment.NewLine;
        //    }
        //}
    }
}