using SketchNotes.CaptureEncoder;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Storage;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace SketchNotes.TabPages
{
    class RecordingOptions
    {
        public GraphicsCaptureItem Target { get; }
        public CaptureEncoder.SizeUInt32 Resolution { get; }
        public uint Bitrate { get; }
        public uint FrameRate { get; }
        public bool IncludeCursor { get; }

        public RecordingOptions(GraphicsCaptureItem target, CaptureEncoder.SizeUInt32 resolution, uint bitrate, uint frameRate, bool includeCursor)
        {
            Target = target;
            Resolution = resolution;
            Bitrate = bitrate;
            FrameRate = frameRate;
            IncludeCursor = includeCursor;
        }
    }


    public sealed partial class RecordingPage : Page
    {
        private IDirect3DDevice MainDevice;
        private Encoder Encoder;
        private CompositionSurfaceBrush PreviewBrush;

        enum RecordingState
        {
            Recording,
            Done,
            Interrupted,
            Failed
        }

        public RecordingPage()
        {
            InitializeComponent();

            Window.Current.CoreWindow.SizeChanged += CoreWindow_SizeChanged;

            MainDevice = D3DDeviceManager.Device;
            SubToolBar.Translation = new Vector3(0, 0, 32);

            var compositor = Window.Current.Compositor;
            var visual = compositor.CreateSpriteVisual();
            visual.RelativeSizeAdjustment = Vector2.One;
            visual.Size = new Vector2(-24.0f, -24.0f);
            PreviewBrush = compositor.CreateSurfaceBrush();
            visual.Brush = PreviewBrush;
            ElementCompositionPreview.SetElementChildVisual(PreviewGrid, visual);
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            var options = (RecordingOptions)e.Parameter;
            await StartRecordingAsync(options);
        }

        private void CoreWindow_SizeChanged(CoreWindow sender, WindowSizeChangedEventArgs args)
        {
            if (App.IsCompactOverlay == true)
            {
                SubToolBar.Margin = new Thickness(0, 0, 24, 24);
                PreviewGrid.Margin = new Thickness(24, 24, 0, 0);
            }
            else
            {
                SubToolBar.Margin = new Thickness(0, 0, 24, 124);
                PreviewGrid.Margin = new Thickness(24, 24, 0, 100);
            }
        }

        private async Task StartRecordingAsync(RecordingOptions options)
        {
            // Encoders generally like even numbers.
            var width = EnsureEven(options.Resolution.Width);
            var height = EnsureEven(options.Resolution.Height);

            // Find a place to put our vidoe for now.
            var folder = ApplicationData.Current.TemporaryFolder;
            var file = await folder.CreateFileAsync(Guid.NewGuid() + ".tmp");

            // Kick off the encoding.
            try
            {
                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                using (Encoder = new Encoder(MainDevice, options.Target))
                {
                    var surface = Encoder.CreatePreviewSurface(Window.Current.Compositor);
                    PreviewBrush.Surface = surface;

                    // 避免意外。
                    StopRecordingBtn.IsEnabled = true;
                    MainPage.RootPage.SendNotification("捕获已开始",
                        "已成功创建录制任务。",
                        Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success);

                    await Encoder.EncodeAsync(
                        stream,
                        width, height, options.Bitrate,
                        options.FrameRate,
                        options.IncludeCursor);
                }
            }
            catch (Exception ex)
            {
                MainPage.RootPage.SendNotification("无法捕获",
                    ex.ToString(),
                    Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error);
                MainPage.RootPage.CloseCurrentPage();

                return;
            }

            //At this point the encoding has finished, let the user preview the file.
            Frame.Navigate(typeof(SaveVideoPage), file, new DrillInNavigationTransitionInfo());
        }

        private uint EnsureEven(uint number)
        {
            if (number % 2 == 0)
            {
                return number;
            }
            else
            {
                return number + 1;
            }
        }

        private void StopRecordingBtn_Click(object sender, RoutedEventArgs e)
        {
            Encoder?.Dispose();
        }
    }
}
