using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage.Streams;
using Windows.UI.Composition;

namespace SketchNotes.CaptureEncoder
{
    public sealed class Encoder : IDisposable
    {
        private IDirect3DDevice Device;
        private SharpDX.Direct3D11.Device D3DDevice;
        private GraphicsCaptureItem CaptureItem;
        private CaptureFrameWait FrameGenerator;
        private VideoStreamDescriptor VideoDescriptor;
        private MediaStreamSource MediaStreamSource;
        private MediaTranscoder Transcoder;
        private bool IsRecording;
        private bool IsPreviewing = false;
        private object PreviewLock;
        private EncoderPreview Preview;
        private bool IsClosed = false;

        public Encoder(IDirect3DDevice device, GraphicsCaptureItem item)
        {
            Device = device;
            D3DDevice = Direct3D11Helpers.CreateSharpDXDevice(device);
            CaptureItem = item;
            IsRecording = false;
            PreviewLock = new object();

            CreateMediaObjects();
        }

        public IAsyncAction EncodeAsync(IRandomAccessStream stream, uint width, uint height, uint bitrateInBps, uint frameRate, bool includeCursor)
        {
            return EncodeInternalAsync(stream, width, height, bitrateInBps, frameRate, includeCursor).AsAsyncAction();
        }

        private async Task EncodeInternalAsync(IRandomAccessStream stream, uint width, uint height, uint bitrateInBps, uint frameRate, bool includeCursor)
        {
            if (!IsRecording)
            {
                IsRecording = true;

                FrameGenerator = new CaptureFrameWait(
                    Device,
                    CaptureItem,
                    CaptureItem.Size,
                    includeCursor);

                using (FrameGenerator)
                {
                    var encodingProfile = new MediaEncodingProfile();
                    encodingProfile.Container.Subtype = "MPEG4";
                    encodingProfile.Video.Subtype = "H264";
                    encodingProfile.Video.Width = width;
                    encodingProfile.Video.Height = height;
                    encodingProfile.Video.Bitrate = bitrateInBps;
                    encodingProfile.Video.FrameRate.Numerator = frameRate;
                    encodingProfile.Video.FrameRate.Denominator = 1;
                    encodingProfile.Video.PixelAspectRatio.Numerator = 1;
                    encodingProfile.Video.PixelAspectRatio.Denominator = 1;
                    var transcode = await Transcoder.PrepareMediaStreamSourceTranscodeAsync(MediaStreamSource, stream, encodingProfile);

                    await transcode.TranscodeAsync();
                }
            }
        }

        public ICompositionSurface CreatePreviewSurface(Compositor compositor)
        {
            if (!IsPreviewing)
            {
                lock (PreviewLock)
                {
                    if (!IsPreviewing)
                    {
                        Preview = new EncoderPreview(D3DDevice);
                        IsPreviewing = true;
                    }
                }
            }

            return Preview.CreateCompositionSurface(compositor);
        }

        public void Dispose()
        {
            if (IsClosed)
            {
                return;
            }
            IsClosed = true;

            if (!IsRecording)
            {
                DisposeInternal();
            }

            IsRecording = false;            
        }

        private void DisposeInternal()
        {
            FrameGenerator.Dispose();
            Preview?.Dispose();
        }

        private void CreateMediaObjects()
        {
            // Create our encoding profile based on the size of the item
            int width = CaptureItem.Size.Width;
            int height = CaptureItem.Size.Height;

            // Describe our input: uncompressed BGRA8 buffers
            var videoProperties = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Bgra8, (uint)width, (uint)height);
            VideoDescriptor = new VideoStreamDescriptor(videoProperties);

            // Create our MediaStreamSource
            MediaStreamSource = new MediaStreamSource(VideoDescriptor);
            MediaStreamSource.BufferTime = TimeSpan.FromSeconds(0);
            MediaStreamSource.Starting += OnMediaStreamSourceStarting;
            MediaStreamSource.SampleRequested += OnMediaStreamSourceSampleRequested;

            // Create our transcoder
            Transcoder = new MediaTranscoder();
            Transcoder.HardwareAccelerationEnabled = true;
        }

        private void OnMediaStreamSourceSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            if (IsRecording && !IsClosed)
            {
                try
                {
                    using (var frame = FrameGenerator.WaitForNewFrame())
                    {
                        if (frame == null)
                        {
                            args.Request.Sample = null;
                            DisposeInternal();
                            return;
                        }

                        if (IsPreviewing)
                        {
                            lock (PreviewLock)
                            {
                                Preview.PresentSurface(frame.Surface);
                            }
                        }

                        var timeStamp = frame.SystemRelativeTime;
                        var sample = MediaStreamSample.CreateFromDirect3D11Surface(frame.Surface, timeStamp);
                        args.Request.Sample = sample;
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    Debug.WriteLine(e.StackTrace);
                    Debug.WriteLine(e);
                    args.Request.Sample = null;
                    DisposeInternal();
                }
            }
            else
            {
                args.Request.Sample = null;
                DisposeInternal();
            }
        }

        private void OnMediaStreamSourceStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            using (var frame = FrameGenerator.WaitForNewFrame())
            {
                args.Request.SetActualStartPosition(frame.SystemRelativeTime);
            }
        }

        private class EncoderPreview : IDisposable
        {
            public EncoderPreview(SharpDX.Direct3D11.Device device)
            {
                _d3dDevice = device;

                var dxgiDevice = _d3dDevice.QueryInterface<SharpDX.DXGI.Device>();
                var adapter = dxgiDevice.GetParent<SharpDX.DXGI.Adapter>();
                var factory = adapter.GetParent<SharpDX.DXGI.Factory2>();

                var description = new SharpDX.DXGI.SwapChainDescription1
                {
                    Width = 1,
                    Height = 1,
                    Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                    Usage = SharpDX.DXGI.Usage.RenderTargetOutput,
                    SampleDescription = new SharpDX.DXGI.SampleDescription()
                    {
                        Count = 1,
                        Quality = 0
                    },
                    BufferCount = 2,
                    Scaling = SharpDX.DXGI.Scaling.Stretch,
                    SwapEffect = SharpDX.DXGI.SwapEffect.FlipSequential,
                    AlphaMode = SharpDX.DXGI.AlphaMode.Premultiplied
                };
                var swapChain = new SharpDX.DXGI.SwapChain1(factory, dxgiDevice, ref description);

                using (var backBuffer = swapChain.GetBackBuffer<SharpDX.Direct3D11.Texture2D>(0))
                using (var renderTargetView = new SharpDX.Direct3D11.RenderTargetView(_d3dDevice, backBuffer))
                {
                    _d3dDevice.ImmediateContext.ClearRenderTargetView(renderTargetView, new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 0));
                }

                _swapChain = swapChain;
            }

            public ICompositionSurface CreateCompositionSurface(Compositor compositor)
            {
                return compositor.CreateCompositionSurfaceForSwapChain(_swapChain);
            }

            public void PresentSurface(IDirect3DSurface surface)
            {
                using (var sourceTexture = Direct3D11Helpers.CreateSharpDXTexture2D(surface))
                {
                    if (!_isSwapChainSized)
                    {
                        var description = sourceTexture.Description;

                        _swapChain.ResizeBuffers(
                            2,
                            description.Width,
                            description.Height,
                            SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                            SharpDX.DXGI.SwapChainFlags.None);

                        _isSwapChainSized = true;
                    }

                    using (var backBuffer = _swapChain.GetBackBuffer<SharpDX.Direct3D11.Texture2D>(0))
                    using (var renderTargetView = new SharpDX.Direct3D11.RenderTargetView(_d3dDevice, backBuffer))
                    {
                        _d3dDevice.ImmediateContext.ClearRenderTargetView(renderTargetView, new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 1));
                        _d3dDevice.ImmediateContext.CopyResource(sourceTexture, backBuffer);
                    }
                }

                _swapChain.Present(1, SharpDX.DXGI.PresentFlags.None);
            }

            public void Dispose()
            {
                _swapChain.Dispose();
                _d3dDevice.Dispose();
            }

            private SharpDX.Direct3D11.Device _d3dDevice;
            private SharpDX.DXGI.SwapChain1 _swapChain;

            private bool _isSwapChainSized = false;
        }
    }

    public struct SizeUInt32
    {
        public uint Width;
        public uint Height;
    }

    // Presets are made to match MediaEncodingProfile for ease of use
    public static class EncoderPresets
    {
        public static SizeUInt32[] Resolutions => new SizeUInt32[]
        {
            new SizeUInt32() { Width = 1280, Height = 720 },
            new SizeUInt32() { Width = 1920, Height = 1080 },
            new SizeUInt32() { Width = 3840, Height = 2160 },
            new SizeUInt32() { Width = 7680, Height = 4320 }
        };

        public static uint[] Bitrates => new uint[]
        {
            9000000,
            18000000,
            36000000,
            72000000,
        };

        public static uint[] FrameRates => new uint[]
        {
            24,
            30,
            60
        };
    }

}
