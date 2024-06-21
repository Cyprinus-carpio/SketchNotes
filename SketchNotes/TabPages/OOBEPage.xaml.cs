using SketchNotes.ContentDialogs;
using System;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace SketchNotes.TabPages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class OOBEPage : Page
    {
        public OOBEPage()
        {
            InitializeComponent();
            Window.Current.CoreWindow.SizeChanged += CoreWindow_SizeChanged;
        }

        private void CoreWindow_SizeChanged(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.WindowSizeChangedEventArgs args)
        {
            if (App.IsCompactOverlay)
            {
                MainStackPanel.Margin = new Thickness(56, 0, 0, 56);
            }
            else
            {
                MainStackPanel.Margin = new Thickness(56, 0, 0, 156);
            }
        }

        private async void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            MainStackPanel.Visibility = Visibility.Collapsed;
            MainMediaElement.Source = null;
            
            OOBEContentDialog dialog = new OOBEContentDialog();
            await dialog.ShowAsync();
            MainPage.RootPage.CloseCurrentPage();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            MainMediaElement.PosterSource = new BitmapImage(
                new Uri("ms-appx:///Assets/OOBEPage.jpg"));

            if (MainToggleSwitch.IsOn == true)
            {
                MainMediaElement.Source = new Uri("ms-appx:///Assets/OOBEPage.mp4");
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            MainMediaElement.PosterSource = null;
            MainMediaElement.Source = null;
        }

        private void MainToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (MainToggleSwitch.IsOn == true)
            {
                MainMediaElement.Source = new Uri("ms-appx:///Assets/OOBEPage.mp4");
            }
            else
            {
                MainMediaElement.Source = null;
            }
        }
    }
}
