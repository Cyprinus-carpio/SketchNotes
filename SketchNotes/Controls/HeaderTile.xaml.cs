using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

//https://go.microsoft.com/fwlink/?LinkId=234236 上介绍了“用户控件”项模板

namespace SketchNotes.Controls
{
    public sealed partial class HeaderTile : UserControl
    {
        public HeaderTile()
        {
            InitializeComponent();
        }

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(HeaderTile), new PropertyMetadata(null));

        public string Description
        {
            get { return (string)GetValue(DescriptionProperty); }
            set { SetValue(DescriptionProperty, value); }
        }

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register("Description", typeof(string), typeof(HeaderTile), new PropertyMetadata(null));

        public object Source
        {
            get { return GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(object), typeof(HeaderTile), new PropertyMetadata(null));

        public string TargetUri
        {
            get { return (string)GetValue(TargetUriProperty); }
            set { SetValue(TargetUriProperty, value); }
        }

        public static readonly DependencyProperty TargetUriProperty =
            DependencyProperty.Register("Target", typeof(string), typeof(HeaderTile), new PropertyMetadata(null));

        private void MainButton_Click(object sender, RoutedEventArgs e)
        {
            App.CreatNewTab(TargetUri);
        }
    }
}
