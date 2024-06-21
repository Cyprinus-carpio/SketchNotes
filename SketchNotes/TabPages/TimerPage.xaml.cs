using Microsoft.Toolkit.Uwp.Notifications;
using SketchNotes.ContentDialogs;
using System;
using Windows.Foundation.Metadata;
using Windows.System.Profile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace SketchNotes.TabPages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class TimerPage : Page
    {
        public TimerPage()
        {
            InitializeComponent();

            CountdownTimer.Tick += CountdownTimer_Tick;
            StopwatchTimer.Tick += StopwatchTimer_Tick;

            CountdownTimer.Interval = TimeSpan.FromMilliseconds(1);
            StopwatchTimer.Interval = TimeSpan.FromMilliseconds(1);
        }

        private DispatcherTimer CountdownTimer = new DispatcherTimer();
        private DispatcherTimer StopwatchTimer = new DispatcherTimer();
        private DateTime CountdownStartTime;
        private TimeSpan CountdownPauseDurationTime;
        private DateTime StopwatchStartTime;
        private TimeSpan StopwatchPauseDurationTime;
        private bool Alarm;

        private class Mark
        {
            public string Time { get; set; }
            public string Type { get; set; }
        }

        private void EndCountdownTimer()
        {
            StopwatchPivotItem.IsEnabled = true;
            SetCountdownBtn.IsEnabled = true;
            StartCountdownBtn.IsEnabled = true;
            PauseCountdownBtn.IsEnabled = false;
            EndCountdownBtn.IsEnabled = false;
            MarkCountdownBtn.IsEnabled = false;
            CountdownTimer.Stop();
            CountdownPauseDurationTime = new TimeSpan(0, 0, 0, 0, 0);
        }

        private void AddMark(string time, string type)
        {
            Mark mark = new Mark()
            {
                Time = time,
                Type = type
            };

            MainListView.Items.Add(mark);

            DetailMark.Visibility = Visibility.Visible;
            MiniMark.Visibility = Visibility.Collapsed;
        }

        private void CountdownTimer_Tick(object sender, object e)
        {
            var duration = DateTime.Now - CountdownStartTime + CountdownPauseDurationTime;
            MainRadialGauge.Value = MainRadialGauge.Maximum - duration.TotalMilliseconds / 1000;
        }

        private void StopwatchTimer_Tick(object sender, object e)
        {
            var duration = DateTime.Now - StopwatchStartTime + StopwatchPauseDurationTime;
            TimeSpan time = TimeSpan.FromMilliseconds(duration.TotalMilliseconds);
            StopwatchTextBlock.Text = time.ToString("hh\\:mm\\:ss\\.fff");
        }

        private void MainRadialGauge_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            TimeSpan time = TimeSpan.FromSeconds(MainRadialGauge.Value);
            DetailTimeTextBlock.Text = time.ToString("hh\\:mm\\:ss\\.fff");

            if (MainRadialGauge.Value == 0)
            {
                EndCountdownTimer();

                if (Alarm)
                {
                    var toast = new ToastContentBuilder();
                    toast.AddInlineImage(new Uri("ms-appx:///Assets/TimerAlarm.png"));
                    toast.AddText("计时器时间到");
                    toast.AddText(MainRadialGauge.Maximum + " 秒已结束");
                    toast.SetToastDuration(ToastDuration.Long);

                    bool supportsCustomAudio = true;

                    // If we're running on Desktop before Version 1511, do NOT include custom audio
                    // since it was not supported until Version 1511, and would result in a silent toast.
                    if (AnalyticsInfo.VersionInfo.DeviceFamily.Equals("Windows.Desktop")
                        && !ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 2))
                    {
                        supportsCustomAudio = false;
                    }

                    if (supportsCustomAudio)
                    {
                        toast.AddAudio(new Uri("ms-appx:///Assets/AlarmRingtone.m4a"), true);
                    }

                    toast.Show();
                }
            }
        }

        private async void SetCountdownBtn_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            SetCountdownContentDialog dialog = new SetCountdownContentDialog();
            await dialog.ShowAsync();

            if (dialog.Seconds != 0)
            {
                MainRadialGauge.Maximum = dialog.Seconds;
                MainRadialGauge.Value = dialog.Seconds;
                MainRadialGauge.TickSpacing = (int)dialog.Seconds / 15;
                StartCountdownBtn.IsEnabled = true;
                Alarm = dialog.Alarm;
            }
        }

        private void StartCountdownBtn_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            StopwatchPivotItem.IsEnabled = false;
            SetCountdownBtn.IsEnabled = false;
            StartCountdownBtn.IsEnabled = false;
            PauseCountdownBtn.IsEnabled = true;
            EndCountdownBtn.IsEnabled = true;
            MarkCountdownBtn.IsEnabled = true;

            if (MainRadialGauge.Value == 0)
            {
                MainRadialGauge.Value = MainRadialGauge.Maximum;
            }

            CountdownStartTime = DateTime.Now;
            CountdownTimer.Start();
        }

        private void EndCountdownBtn_Click(object sender, RoutedEventArgs e)
        {
            EndCountdownTimer();
            MainRadialGauge.Value = MainRadialGauge.Maximum;
        }

        private void PauseCountdownBtn_Click(object sender, RoutedEventArgs e)
        {
            CountdownPauseDurationTime += DateTime.Now - CountdownStartTime;
            PauseCountdownBtn.IsEnabled = false;
            StartCountdownBtn.IsEnabled = true;
            CountdownTimer.Stop();
        }

        private void MarkCountdownBtn_Click(object sender, RoutedEventArgs e)
        {
            AddMark(DetailTimeTextBlock.Text, "倒计时 " + MainRadialGauge.Maximum + " 秒");
        }

        private void ClearMarkBtn_Click(object sender, RoutedEventArgs e)
        {
            MainListView.Items.Clear();
        }

        private void HideMarkBtn_Click(object sender, RoutedEventArgs e)
        {
            DetailMark.Visibility = Visibility.Collapsed;
            MiniMark.Visibility = Visibility.Visible;
        }

        private void ShowMark_Click(object sender, RoutedEventArgs e)
        {
            DetailMark.Visibility = Visibility.Visible;
            MiniMark.Visibility = Visibility.Collapsed;
        }

        private void StartStopwatchBtn_Click(object sender, RoutedEventArgs e)
        {
            CountdownPivotItem.IsEnabled = false;
            StartStopwatchBtn.IsEnabled = false;
            PauseStopwatchBtn.IsEnabled = true;
            ResetStopwatchBtn.IsEnabled = true;
            MarkStopwatchBtn.IsEnabled = true;

            StopwatchStartTime = DateTime.Now;
            StopwatchTimer.Start();
        }

        private void ResetStopwatchBtn_Click(object sender, RoutedEventArgs e)
        {
            StopwatchStartTime = DateTime.Now;
            StopwatchPauseDurationTime = new TimeSpan(0, 0, 0, 0, 0);
            StopwatchTextBlock.Text = "00:00:00.000";
        }

        private void PauseStopwatchBtn_Click(object sender, RoutedEventArgs e)
        {
            StopwatchPauseDurationTime += DateTime.Now - StopwatchStartTime;
            PauseStopwatchBtn.IsEnabled = false;
            StartStopwatchBtn.IsEnabled = true;
            CountdownPivotItem.IsEnabled = true;
            StopwatchTimer.Stop();
        }

        private void MarkStopwatchBtn_Click(object sender, RoutedEventArgs e)
        {
            AddMark(StopwatchTextBlock.Text, "停表");
        }
    }
}
