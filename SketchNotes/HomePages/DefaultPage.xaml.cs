using SketchNotes.TabPages;
using System;
using System.ComponentModel;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Imaging;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace SketchNotes.HomePages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class DefaultPage : Page
    {
        public DefaultPage()
        {
            InitializeComponent();
        }

        private void GetCachedSettings()
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;
            var homePageEnableBackground = container.Values["HomePageEnableBackground"];
            var homePageLiveBackground = container.Values["HomePageLiveBackground"];
            var homePageOpacityBackground = container.Values["HomePageOpacityBackground"];
            var webPageEnableBackground = container.Values["WebPageEnableBackground"];
            var webPageLiveBackground = container.Values["WebPageLiveBackground"];
            var webPageOpacityBackground = container.Values["WebPageOpacityBackground"];
            var theme = container.Values["Theme"];
            var enableElementSound = container.Values["EnableElementSound"];
            var enableSpatialAudio = container.Values["EnableSpatialAudio"];
            var enableAutoMove = container.Values["EnableAutoMove"];
            var autoMoveTime = container.Values["AutoMoveTime"];
            var autoMoveDistance = container.Values["AutoMoveDistance"];
            var autoMoveLength = container.Values["AutoMoveLength"];

            if ((string)homePageEnableBackground != "False")
            {
                HomePageEnableBackgroundToggleSwitch.IsOn = true;
            }

            if ((string)homePageLiveBackground != "False")
            {
                HomePageLiveBackgroundToggleSwitch.IsOn = true;
            }

            if (homePageOpacityBackground != null)
            {
                HomePageBackgroundSlider.Value = (double)homePageOpacityBackground;
            }
            else
            {
                HomePageBackgroundSlider.Value = 50;
            }

            if ((string)webPageEnableBackground != "False")
            {
                WebPageEnableBackgroundToggleSwitch.IsOn = true;
            }

            if ((string)webPageLiveBackground != "False")
            {
                WebPageLiveBackgroundToggleSwitch.IsOn = true;
            }

            if (webPageOpacityBackground != null)
            {
                WebPageBackgroundSlider.Value = (double)webPageOpacityBackground;
            }
            else
            {
                WebPageBackgroundSlider.Value = 50;
            }

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

            if ((string)enableElementSound != "True")
            {
                SoundToggleSwitch.IsOn = false;
            }
            else
            {
                SoundToggleSwitch.IsOn = true;
            }

            if ((string)enableSpatialAudio != "False")
            {
                SpatialAudioToggleSwitch.IsOn = true;
            }

            if((string)enableAutoMove != "False")
            {
                AutoMoveToggleSwitch.IsOn = true;
            }

            if (autoMoveDistance == null)
            {
                AutoMoveDistanceNumberBox.Value = 100;
            }
            else
            {
                AutoMoveDistanceNumberBox.Value = (double)autoMoveDistance;
            }

            if (autoMoveTime == null)
            {
                AutoMoveTimeNumberBox.Value = 0.5;
            }
            else
            {
                AutoMoveTimeNumberBox.Value = (double)autoMoveTime;
            }

            if (autoMoveLength == null)
            {
                AutoMoveLengthNumberBox.Value = 100;
            }
            else
            {
                AutoMoveLengthNumberBox.Value = (double)autoMoveLength;
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            GetCachedSettings();
        }

        private void HomePageLiveBackgroundToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;

            if (HomePageLiveBackgroundToggleSwitch.IsOn == true)
            {
                container.Values["HomePageLiveBackground"] = "True";

                // 避免闪烁。
                if (HomePage.InitialPage.MainMediaElement.Source == null)
                {
                    HomePage.InitialPage.MainMediaElement.Source = new Uri(
                        "ms-appx:///Assets/HomePage.mp4");
                }
            }
            else
            {
                container.Values["HomePageLiveBackground"] = "False";
                HomePage.InitialPage.MainMediaElement.Source = null;
            }
        }

        private void HomePageBackgroundSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;
            container.Values["HomePageOpacityBackground"] = HomePageBackgroundSlider.Value;
            HomePage.InitialPage.MainMediaElement.Opacity = HomePageBackgroundSlider.Value / 100;
        }

        private void HomePageEnableBackgroundToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;

            if (HomePageEnableBackgroundToggleSwitch.IsOn == true)
            {
                container.Values["HomePageEnableBackground"] = "True";

                // 避免闪烁。
                if (HomePage.InitialPage.MainMediaElement.PosterSource == null)
                {
                    HomePage.InitialPage.MainMediaElement.PosterSource = new BitmapImage(
                    new Uri("ms-appx:///Assets/HomePage.jpg"));
                }

                if (HomePageLiveBackgroundToggleSwitch.IsOn == true)
                {
                    HomePage.InitialPage.MainMediaElement.Source = new Uri(
                        "ms-appx:///Assets/HomePage.mp4");
                }

                HomePage.InitialPage.MainMediaElement.Visibility = Visibility.Visible;
            }
            else
            {
                container.Values["HomePageEnableBackground"] = "False";

                HomePage.InitialPage.MainMediaElement.Source = null;
                HomePage.InitialPage.MainMediaElement.PosterSource = null;
                HomePage.InitialPage.MainMediaElement.Visibility = Visibility.Collapsed;
            }
        }

        private void WebPageEnableBackgroundToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;

            if (WebPageEnableBackgroundToggleSwitch.IsOn == true)
            {
                container.Values["WebPageEnableBackground"] = "True";
            }
            else
            {
                container.Values["WebPageEnableBackground"] = "False";
            }
        }

        private void WebPageLiveBackgroundToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;

            if (WebPageLiveBackgroundToggleSwitch.IsOn == true)
            {
                container.Values["WebPageLiveBackground"] = "True";
            }
            else
            {
                container.Values["WebPageLiveBackground"] = "False";
            }
        }

        private void WebPageBackgroundSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;
            container.Values["WebPageOpacityBackground"] = WebPageBackgroundSlider.Value;
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

        private void SoundToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;

            if (SoundToggleSwitch.IsOn == true)
            {
                container.Values["EnableElementSound"] = "True";
                ElementSoundPlayer.State = ElementSoundPlayerState.On;

                if (SpatialAudioToggleSwitch.IsOn == true)
                {
                    ElementSoundPlayer.SpatialAudioMode = ElementSpatialAudioMode.On;
                }
            }
            else
            {
                container.Values["EnableElementSound"] = "False";
                ElementSoundPlayer.State = ElementSoundPlayerState.Off;
            }
        }

        private void SpatialAudioToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;

            if (SpatialAudioToggleSwitch.IsOn == true)
            {
                container.Values["EnableSpatialAudio"] = "True";
                ElementSoundPlayer.SpatialAudioMode = ElementSpatialAudioMode.On;
            }
            else
            {
                container.Values["EnableSpatialAudio"] = "False";
                ElementSoundPlayer.SpatialAudioMode = ElementSpatialAudioMode.Off;
            }
        }

        private void AutoMoveNumberBox_OnValueChanged(Microsoft.UI.Xaml.Controls.NumberBox sender, Microsoft.UI.Xaml.Controls.NumberBoxValueChangedEventArgs args)
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;

            if (double.IsNaN(sender.Value))
            {
                if (sender.Name == "AutoMoveTimeNumberBox")
                {
                    sender.Value = 0.5;
                    container.Values["AutoMoveTime"] = sender.Value;
                }
                else if (sender.Name == "AutoMoveDistanceNumberBox")
                {
                    sender.Value = 100;
                    container.Values["AutoMoveDistance"] = sender.Value;
                }
                else if (sender.Name == "AutoMoveLengthNumberBox")
                {
                    sender.Value = 100;
                    container.Values["AutoMoveLength"] = sender.Value;
                }
            }
            else
            {
                if (sender.Name == "AutoMoveTimeNumberBox")
                {
                    container.Values["AutoMoveTime"] = sender.Value;
                }
                else if (sender.Name == "AutoMoveDistanceNumberBox")
                {
                    container.Values["AutoMoveDistance"] = sender.Value;
                }
                else if (sender.Name == "AutoMoveLengthNumberBox")
                {
                    container.Values["AutoMoveLength"] = sender.Value;
                }
            }
        }

        private void AutoMoveToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ApplicationDataContainer container = ApplicationData.Current.LocalSettings;

            if (AutoMoveToggleSwitch.IsOn == true)
            {
                container.Values["EnableAutoMove"] = "True";
            }
            else
            {
                container.Values["EnableAutoMove"] = "False";
            }
        }
    }
}
