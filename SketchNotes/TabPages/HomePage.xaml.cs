using SketchNotes.HomePages;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using MUXC = Microsoft.UI.Xaml.Controls;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace SketchNotes.TabPages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();

            Window.Current.CoreWindow.SizeChanged += CoreWindow_SizeChanged;

            InitialPage = this;
        }

        public int NavigationTarget = 0;
        public static HomePage InitialPage;

        private void GetCachedSettings()
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;
            var enableBackground = container.Values["HomePageEnableBackground"];
            var liveBackground = container.Values["HomePageLiveBackground"];
            var opacityBackground = container.Values["HomePageOpacityBackground"];

            if ((string)enableBackground != "False")
            {
                MainMediaElement.PosterSource = new BitmapImage(
                    new Uri("ms-appx:///Assets/HomePage.jpg"));

                if ((string)liveBackground != "False")
                {
                    MainMediaElement.Source = new Uri("ms-appx:///Assets/HomePage.mp4");
                }

                if (opacityBackground != null)
                {
                    MainMediaElement.Opacity = (double)opacityBackground / 100;
                }

                MainMediaElement.Visibility = Visibility.Visible;
            }
            else
            {
                MainMediaElement.Visibility = Visibility.Collapsed;
            }
        }

        private void CoreWindow_SizeChanged(CoreWindow sender, WindowSizeChangedEventArgs args)
        {
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            if (App.IsCompactOverlay == true)
            {
                ContentFrame.Margin = new Thickness(0, 48, 0, 0);
            }
            else
            {
                ContentFrame.Margin = new Thickness(0, 48, 0, 100);
            }
        }

        private void ContentFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        // List of ValueTuple holding the Navigation Tag and the relative Navigation Page
        private readonly List<(string Tag, Type Page)> pages = new List<(string Tag, Type Page)>
        {
            ("Home", typeof(StartPage)),
            ("Default", typeof(DefaultPage)),
            //("Extensions", typeof(ExtensionsPage)),
            ("Recovery", typeof(StoragePage)),
            ("Advance", typeof(AdvancePage)),
            ("About", typeof(AboutPage))
        };

        private void MainNavigationView_Loaded(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigated += On_Navigated;

            MainNavigationView.SelectedItem = MainNavigationView.MenuItems[0];

            GetCachedSettings();
            MainNavigationView_Navigate("Home",
                new Windows.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());

            SystemNavigationManager.GetForCurrentView().BackRequested += System_BackRequested;
        }

        private void MainNavigationView_ItemInvoked(MUXC.NavigationView sender, MUXC.NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer != null)
            {
                var navItemTag = args.InvokedItemContainer.Tag.ToString();
                MainNavigationView_Navigate(navItemTag, args.RecommendedNavigationTransitionInfo);
            }
        }

        public void MainNavigationView_Navigate(string navItemTag, Windows.UI.Xaml.Media.Animation.NavigationTransitionInfo transitionInfo)
        {
            Type page = null;
            var item = pages.FirstOrDefault(p => p.Tag.Equals(navItemTag));
            page = item.Page;

            var preNavPageType = ContentFrame.CurrentSourcePageType;

            if (!(page is null) && !Equals(preNavPageType, page))
            {
                ContentFrame.Navigate(page, null, transitionInfo);
            }
        }

        private void MainNavigationView_BackRequested(MUXC.NavigationView sender, MUXC.NavigationViewBackRequestedEventArgs args)
        {
            TryGoBack();
        }

        private void System_BackRequested(object sender, BackRequestedEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = TryGoBack();
            }
        }

        private bool TryGoBack()
        {
            if (!ContentFrame.CanGoBack)
                return false;

            if (MainNavigationView.IsPaneOpen && (
                MainNavigationView.DisplayMode == MUXC.NavigationViewDisplayMode.Compact ||
                MainNavigationView.DisplayMode == MUXC.NavigationViewDisplayMode.Minimal))
                return false;

            ContentFrame.GoBack();
            return true;
        }

        private void On_Navigated(object sender, NavigationEventArgs e)
        {
            MainNavigationView.IsBackEnabled = ContentFrame.CanGoBack;

            var item = pages.FirstOrDefault(p => p.Page == e.SourcePageType);
            MainNavigationView.SelectedItem =
                MainNavigationView.MenuItems.OfType<MUXC.NavigationViewItem>().First(n => n.Tag.Equals(item.Tag));
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            MainMediaElement.Source = null;
            MainMediaElement.PosterSource = null;
        }
    }
}
