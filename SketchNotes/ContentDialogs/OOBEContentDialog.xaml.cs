using System;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“内容对话框”项模板

namespace SketchNotes.ContentDialogs
{
    public sealed partial class OOBEContentDialog : ContentDialog
    {
        public OOBEContentDialog()
        {
            InitializeComponent();
        }

        private void GetCachedSettings()
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;
            var theme = container.Values["Theme"];

            if ((string)theme == "Light")
            {
                ThemeModeComboBox.SelectedIndex = 0;
            }
            else if ((string)theme == "Dark")
            {
                ThemeModeComboBox.SelectedIndex = 1;
            }
            else
            {
                ThemeModeComboBox.SelectedIndex = 2;
            }
        }

        private async void ContentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            SketchNotesLicenseMarkdownTextBlock.Text = await Windows.Storage.FileIO.ReadTextAsync(
                await Package.Current.InstalledLocation.GetFileAsync("Assets\\SketchNotesLicense.md"));
            GPLLicenseTextBlock.Text = await Windows.Storage.FileIO.ReadTextAsync(
                await Package.Current.InstalledLocation.GetFileAsync("Assets\\GPLLicense.txt"));

            GetCachedSettings();
        }

        private void MainFlipView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (MainFlipView.SelectedIndex)
            {
                case 0:
                    MainImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/SketchNotesLicense.png"));
                    break;
                case 1:
                    MainImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/MITLicense.png"));
                    break;
                case 2:
                    MainImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/Default.png"));
                    break;
                case 3:
                    MainImage.Source = null;
                    break;
            }

        }

        private void ThemeModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;
            var selectedTheme = ((ComboBoxItem)ThemeModeComboBox.SelectedItem)?.Tag?.ToString();

            if (selectedTheme != null)
            {
                App.RootTheme = App.GetEnum<ElementTheme>(selectedTheme);

                if (selectedTheme == "Light")
                {
                    container.Values["Theme"] = "Light";
                }
                else if (selectedTheme == "Dark")
                {
                    container.Values["Theme"] = "Dark";
                }
                else
                {
                    container.Values["Theme"] = "Default";
                }
            }
        }

        private void SignInBtn_Click(object sender, RoutedEventArgs e)
        {
            MainPage.RootPage.SignIn();
        }

        private void FinishBtn_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }
    }
}
