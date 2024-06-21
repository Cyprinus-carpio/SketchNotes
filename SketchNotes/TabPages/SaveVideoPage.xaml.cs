using Microsoft.Kiota.Abstractions;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace SketchNotes.TabPages
{
    public sealed partial class SaveVideoPage : Page
    {
        public SaveVideoPage()
        {
            InitializeComponent();

            Window.Current.CoreWindow.SizeChanged += CoreWindow_SizeChanged;
            SubToolBar.Translation = new Vector3(0, 0, 32);
        }

        private StorageFile PreviewFile;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            PreviewFile = (StorageFile)e.Parameter;
            PreviewMediaPlayerElement.Source = MediaSource.CreateFromStorageFile(PreviewFile);
        }

        private void CoreWindow_SizeChanged(CoreWindow sender, WindowSizeChangedEventArgs args)
        {
            if (App.IsCompactOverlay == true)
            {
                SubToolBar.Margin = new Thickness(0, 0, 24, 24);
                PreviewMediaPlayerElement.Margin = new Thickness(0, 0, 0, 0);
            }
            else
            {
                SubToolBar.Margin = new Thickness(0, 0, 24, 124);
                PreviewMediaPlayerElement.Margin = new Thickness(0, 0, 0, 100);
            }
        }

        private async Task<StorageFile> PickVideoAsync()
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.VideosLibrary,
                SuggestedFileName = "屏幕录制 " + DateTime.Now.ToString("yyyy-MM-dd HHmmss"),
                DefaultFileExtension = ".mp4"
            };
            picker.FileTypeChoices.Add("MP4", new List<string> { ".mp4" });
            var file = await picker.PickSaveFileAsync();

            return file;
        }

        private async void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            PreviewMediaPlayerElement.Source = null;
            await PreviewFile.DeleteAsync();
            MainPage.RootPage.CloseCurrentPage();
        }

        private async void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            // Ask the user where they'd like the video to live.
            var newFile = await PickVideoAsync();

            if (newFile == null)
            {
                // The user canceled.
                return;
            }

            // Move our video to its new home.
            PreviewMediaPlayerElement.Source = null;
            await PreviewFile.MoveAndReplaceAsync(newFile);

            MainPage.RootPage.SendNotification("捕获已保存",
                "已成功保存屏幕录制。",
                Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success);
            MainPage.RootPage.CloseCurrentPage();
        }
    }
}
