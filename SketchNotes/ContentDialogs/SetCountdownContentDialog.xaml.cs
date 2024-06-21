using Windows.UI.Xaml.Controls;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“内容对话框”项模板

namespace SketchNotes.ContentDialogs
{
    public sealed partial class SetCountdownContentDialog : ContentDialog
    {
        public SetCountdownContentDialog()
        {
            InitializeComponent();
        }

        public double Seconds = 0;
        public bool Alarm;

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Seconds = HoursNumberBox.Value * 3600 + MinutesNumberBox.Value * 60 + SecondsNumberBox.Value;
            Alarm = (bool)AlarmCheckBox.IsChecked;
        }

        private void NumberBox_OnValueChanged(Microsoft.UI.Xaml.Controls.NumberBox sender, Microsoft.UI.Xaml.Controls.NumberBoxValueChangedEventArgs args)
        {
            if (HoursNumberBox != null &&
                MinutesNumberBox != null &&
                SecondsNumberBox != null)
            {
                if (double.IsNaN(sender.Value))
                {
                    if (sender.Name == "HoursNumberBox")
                    {
                        sender.Value = 0;
                    }
                    else if (sender.Name == "MinutesNumberBox")
                    {
                        sender.Value = 0;
                    }
                    else if (sender.Name == "SecondsNumberBox")
                    {
                        sender.Value = 0;
                    }
                }
                else
                {
                    if (HoursNumberBox.Value == 0 &&
                        MinutesNumberBox.Value == 0 &&
                        SecondsNumberBox.Value == 0)
                    {
                        IsPrimaryButtonEnabled = false;
                    }
                    else
                    {
                        IsPrimaryButtonEnabled = true;
                    }
                }
            }
        }
    }
}