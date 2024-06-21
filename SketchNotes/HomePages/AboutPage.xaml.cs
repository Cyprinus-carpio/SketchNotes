using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace SketchNotes.HomePages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class AboutPage : Page
    {
        public AboutPage()
        {
            InitializeComponent();
        }

        public string Version
        {
            get
            {
                var version = Windows.ApplicationModel.Package.Current.Id.Version;
                return string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
            }
        }

        private async void SamaSettingsCard_Click(object sender, RoutedEventArgs e)
        {
            var uri = new Uri("https://www.pixiv.net/users/5094445");
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        private async void MicrosoftSettingsCard_Click(object sender, RoutedEventArgs e)
        {
            var uri = new Uri("https://www.microsoft.com/servicesagreement");
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        private async void HoYoverseSettingsCard_Click(object sender, RoutedEventArgs e)
        {
            var uri = new Uri("https://www.hoyoverse.com/");
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }
    }
}
