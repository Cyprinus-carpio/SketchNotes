using System;
using System.Reflection;
using System.Text.RegularExpressions;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace SketchNotes
{
    /// <summary>
    /// 提供特定于应用程序的行为，以补充默认的应用程序类。
    /// </summary>
    sealed partial class App : Application
    {
        /// <summary>
        /// 初始化单一实例应用程序对象。这是执行的创作代码的第一行，
        /// 已执行，逻辑上等同于 main() 或 WinMain()。
        /// </summary>
        public App()
        {
            InitializeComponent();

            Suspending += OnSuspending;
            UnhandledException += OnUnhandledException;
        }

        public static Guid NoteBookGuid = Guid.NewGuid();
        public static bool IsCompactOverlay = false;
        public static string LaunchInfo;

        public static ElementTheme RootTheme
        {
            get
            {
                if (Window.Current.Content is FrameworkElement rootElement)
                {
                    return rootElement.RequestedTheme;
                }

                return ElementTheme.Default;
            }
            set
            {
                if (Window.Current.Content is FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = value;
                }
            }
        }

        public static TEnum GetEnum<TEnum>(string text) where TEnum : struct
        {
            if (!typeof(TEnum).GetTypeInfo().IsEnum)
            {
                throw new InvalidOperationException("Generic parameter 'TEnum' must be an enum.");
            }

            return (TEnum)Enum.Parse(typeof(TEnum), text);
        }

        public static bool IsContentDialogOpen(Window window = null)
        {
            // By default use the current window
            if (window == null)
            {
                window = Window.Current;
            }

            // Get all open popups in the window. A ContentDialog is a popup.
            var popups = VisualTreeHelper.GetOpenPopups(window);

            foreach (var popup in popups)
            {
                if (popup.Child is ContentDialog)
                {
                    // A content dialog is open
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 在应用程序由最终用户正常启动时进行调用。
        /// 将在启动应用程序以打开特定文件等情况下使用。
        /// </summary>
        /// <param name="e">有关启动请求和过程的详细信息。</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // 不要在窗口已包含内容时重复应用程序初始化，
            // 只需确保窗口处于活动状态
            if (rootFrame == null)
            {
                // 创建要充当导航上下文的框架，并导航到第一页
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: 从之前挂起的应用程序加载状态
                }

                // 将框架放在当前窗口中
                Window.Current.Content = rootFrame;
            }

            GetCachedSettings();

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // 当导航堆栈尚未还原时，导航到第一页，
                    // 并通过将所需信息作为导航参数传入来配置
                    // 参数
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }

                // 确保当前窗口处于活动状态
                Window.Current.Activate();

                Windows.UI.Core.Preview.SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += async (sender, args) =>
                {
                    var deferral = args.GetDeferral();

                    if (IsContentDialogOpen())
                    {
                        MainPage.RootPage.SendNotification("需要关闭对话框",
                            "关闭对话框以尝试退出 SketchNotes。",
                            Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error);

                        args.Handled = true;
                    }
                    else
                    {
                        var dialog = new ContentDialog
                        {
                            Title = "SketchNotes",
                            Content = "是否要关闭所有标签页？",
                            PrimaryButtonText = "取消",
                            SecondaryButtonText = "全部关闭",
                        };

                        var result = await dialog.ShowAsync();

                        if (result == ContentDialogResult.Primary)
                        {
                            args.Handled = true;
                        }
                    }

                    deferral.Complete();
                };
                //ApplicationDataContainer container = ApplicationData.Current.LocalSettings;
                //string oobe = container.Values["OOBE"] as string;
                //if (oobe == "False")
                //{
                //    string creatNewTab = container.Values["CreatNewTab"] as string;
                //    if (creatNewTab == "True")
                //    {
                //        rootFrame.Navigate(typeof(MainPage), "0", new DrillInNavigationTransitionInfo());
                //    }
                //    else if (creatNewTab == "False")
                //    {
                //        rootFrame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
                //    }
                //    else
                //    {
                //        rootFrame.Navigate(typeof(MainPage),null,null);
                //    }
                //}
                //else
                //{
                //    rootFrame.Navigate(typeof(MainPage), "2", new DrillInNavigationTransitionInfo());
                //}
            }
        }

        /// <summary>
        /// 导航到特定页失败时调用
        /// </summary>
        ///<param name="sender">导航失败的框架</param>
        ///<param name="e">有关导航失败的详细信息</param>
        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// 在将要挂起应用程序执行时调用。  在不知道应用程序
        /// 无需知道应用程序会被终止还是会恢复，
        /// 并让内存内容保持不变。
        /// </summary>
        /// <param name="sender">挂起的请求的源。</param>
        /// <param name="e">有关挂起请求的详细信息。</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: 保存应用程序状态并停止任何后台活动
            deferral.Complete();
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
            }

            EnsureWindow(args);
            Window.Current.Activate();
            base.OnActivated(args);
        }

        private void EnsureWindow(IActivatedEventArgs args = null)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            GetCachedSettings();

            if (args.Kind == ActivationKind.Protocol)
            {
                try
                {
                    string uri = ((ProtocolActivatedEventArgs)args).Uri?.AbsoluteUri;
                    LaunchInfo = uri;
                    CreatNewTab(uri);
                }
                catch (Exception ex)
                {
                    LaunchInfo = ex.ToString();
                    CreatNewTab("");
                }
            }
            else
            {
                rootFrame.Navigate(typeof(MainPage));
            }
        }

        private void GetCachedSettings()
        {
            Frame rootFrame = Window.Current.Content as Frame;

            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;
            var theme = (string)container.Values["Theme"];
            var enableElementSound = (string)container.Values["EnableElementSound"];
            var enableSpatialAudio = (string)container.Values["EnableSpatialAudio"];

            if (theme == "Light")
            {
                rootFrame.RequestedTheme = ElementTheme.Light;
            }
            else if (theme == "Dark")
            {
                rootFrame.RequestedTheme = ElementTheme.Dark;
            }
            else
            {
                rootFrame.RequestedTheme = ElementTheme.Default;
            }

            if (enableElementSound == "True")
            {
                ElementSoundPlayer.State = ElementSoundPlayerState.On;

                if (enableSpatialAudio == "True")
                {
                    ElementSoundPlayer.SpatialAudioMode = ElementSpatialAudioMode.On;
                }
            }
        }

        public static void CreatNewTab(string uri)
        {
            Frame rootFrame = Window.Current.Content as Frame;
            ApplicationView view = ApplicationView.GetForCurrentView();
            Match match;

            match = Regex.Match(uri, @"://(.*)/(.*)");

            if (match.Success)
            {
                string method = match.Groups[1]?.ToString();
                string type = match.Groups[2]?.ToString();

                if (method == "new")
                {
                    if (type == "note")
                    {
                        rootFrame.Navigate(typeof(MainPage), "0");
                    }
                    else if (type == "web")
                    {
                        rootFrame.Navigate(typeof(MainPage), "1");
                    }
                    else if (type == "map")
                    {
                        rootFrame.Navigate(typeof(MainPage), "2");
                    }
                    else if (type == "capture")
                    {
                        rootFrame.Navigate(typeof(MainPage), "3");
                    }
                    else if (type == "timer")
                    {
                        rootFrame.Navigate(typeof(MainPage), "4");
                    }
                    else if (type == "oobe")
                    {
                        rootFrame.Navigate(typeof(MainPage), "5");
                    }
                    else if (type == "")
                    {
                        rootFrame.Navigate(typeof(MainPage));
                    }
                    else
                    {
                        rootFrame.Navigate(typeof(MainPage), "-1");
                    }
                }
                else if (method == "")
                {
                    rootFrame.Navigate(typeof(MainPage));
                }
                else
                {
                    rootFrame.Navigate(typeof(MainPage), "-1");
                }
            }
            else
            {
                rootFrame.Navigate(typeof(MainPage), "-1");
            }
        }

        private async void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            var dialog = new MessageDialog("SketchNotes 不稳定，自动备份可能不会按预期工作，请立即保存已有工作，并重启 SketchNotes。", "内部错误");
            dialog.Commands.Add(new UICommand("关闭"));
            dialog.Commands.Add(new UICommand("复制到剪贴板"));
            dialog.DefaultCommandIndex = 0;
            dialog.CancelCommandIndex = 1;
            var result = await dialog.ShowAsync();

            if (result.Label == "复制到剪贴板")
            {
                DataPackage dataPackage = new DataPackage();
                dataPackage.SetText(e.Message);
                Clipboard.SetContent(dataPackage);
            }
        }
    }
}
