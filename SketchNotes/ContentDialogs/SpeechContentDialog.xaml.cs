using Windows.UI.Xaml.Controls;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“内容对话框”项模板

namespace SketchNotes.ContentDialogs
{
    public sealed partial class SpeechContentDialog : ContentDialog
    {
        public SpeechContentDialog()
        {
            InitializeComponent();
        }

        public string SpeechText = "";
        public bool AddToExistence;

        private void MainTextBox_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
        {
            if (MainTextBox.Text != "")
            {
                IsPrimaryButtonEnabled = true;
            }
            else
            {
                IsPrimaryButtonEnabled = false;
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            SpeechText = MainTextBox.Text;
            AddToExistence = (bool)AddCheckBox.IsChecked;
        }
    }
}
