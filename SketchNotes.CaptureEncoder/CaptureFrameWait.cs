using System;
using System.Threading;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace SketchNotes.CaptureEncoder
{
    public sealed class SurfaceWithInfo : IDisposable
    {
        public IDirect3DSurface Surface { get; internal set; }
        public TimeSpan SystemRelativeTime { get; internal set; }

        public void Dispose()
        {
            Surface?.Dispose();
            Surface = null;
        }
    }

    class MultithreadLock : IDisposable
    {
        private SharpDX.Direct3D11.Multithread MainMultithread;

        public MultithreadLock(SharpDX.Direct3D11.Multithread multithread)
        {
            MainMultithread = multithread;
            MainMultithread?.Enter();
        }

        public void Dispose()
        {
            MainMultithread?.Leave();
            MainMultithread = null;
        }
    }

    public sealed class CaptureFrameWait : IDisposable
    {
        private IDirect3DDevice Device;
        private SharpDX.Direct3D11.Device D3DDevice;
        private SharpDX.Direct3D11.Multithread MainMultithread;
        private SharpDX.Direct3D11.Texture2D ComposeTexture;
        private SharpDX.Direct3D11.RenderTargetView ComposeRenderTargetView;
        private ManualResetEvent[] Events;
        private ManualResetEvent FrameEvent;
        private ManualResetEvent ClosedEvent;
        private Direct3D11CaptureFrame CurrentFrame;
        private GraphicsCaptureItem Item;
        private GraphicsCaptureSession Session;
        private Direct3D11CaptureFramePool FramePool;

        public CaptureFrameWait(
            IDirect3DDevice device,
            GraphicsCaptureItem item,
            SizeInt32 size,
            bool includeCursor)
        {
            Device = device;
            D3DDevice = Direct3D11Helpers.CreateSharpDXDevice(device);
            MainMultithread = D3DDevice.QueryInterface<SharpDX.Direct3D11.Multithread>();
            MainMultithread.SetMultithreadProtected(true);
            Item = item;
            FrameEvent = new ManualResetEvent(false);
            ClosedEvent = new ManualResetEvent(false);
            Events = new[] { ClosedEvent, FrameEvent };

            InitializeComposeTexture(size);
            InitializeCapture(size, includeCursor);
        }

        private void InitializeCapture(SizeInt32 size, bool includeCursor)
        {
            Item.Closed += OnClosed;
            FramePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                Device,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                1,
                size);
            FramePool.FrameArrived += OnFrameArrived;
            Session = FramePool.CreateCaptureSession(Item);

            if (!includeCursor)
            {
                Session.IsCursorCaptureEnabled = includeCursor;
            }
            Session.StartCapture();
        }

        private void InitializeComposeTexture(SizeInt32 size)
        {
            var description = new SharpDX.Direct3D11.Texture2DDescription
            {
                Width = size.Width,
                Height = size.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                SampleDescription = new SharpDX.DXGI.SampleDescription()
                {
                    Count = 1,
                    Quality = 0
                },
                Usage = SharpDX.Direct3D11.ResourceUsage.Default,
                BindFlags = SharpDX.Direct3D11.BindFlags.ShaderResource | SharpDX.Direct3D11.BindFlags.RenderTarget,
                CpuAccessFlags = SharpDX.Direct3D11.CpuAccessFlags.None,
                OptionFlags = SharpDX.Direct3D11.ResourceOptionFlags.None
            };
            ComposeTexture = new SharpDX.Direct3D11.Texture2D(D3DDevice, description);
            ComposeRenderTargetView = new SharpDX.Direct3D11.RenderTargetView(D3DDevice, ComposeTexture);
        }

        private void SetResult(Direct3D11CaptureFrame frame)
        {
            CurrentFrame = frame;
            FrameEvent.Set();
        }

        private void Stop()
        {
            ClosedEvent.Set();
        }

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            SetResult(sender.TryGetNextFrame());
        }

        private void OnClosed(GraphicsCaptureItem sender, object args)
        {
            Stop();
        }

        private void Cleanup()
        {
            FramePool?.Dispose();
            Session?.Dispose();

            if (Item != null)
            {
                Item.Closed -= OnClosed;
            }

            Item = null;
            Device = null;
            D3DDevice = null;
            ComposeTexture?.Dispose();
            ComposeTexture = null;
            ComposeRenderTargetView?.Dispose();
            ComposeRenderTargetView = null;
            CurrentFrame?.Dispose();
        }

        public SurfaceWithInfo WaitForNewFrame()
        {
            // Let's get a fresh one.
            CurrentFrame?.Dispose();
            FrameEvent.Reset();

            var signaledEvent = Events[WaitHandle.WaitAny(Events)];

            if (signaledEvent == ClosedEvent)
            {
                Cleanup();
                return null;
            }

            var result = new SurfaceWithInfo
            {
                SystemRelativeTime = CurrentFrame.SystemRelativeTime
            };
            using (var multithreadLock = new MultithreadLock(MainMultithread))
            using (var sourceTexture = Direct3D11Helpers.CreateSharpDXTexture2D(CurrentFrame.Surface))
            {
                D3DDevice.ImmediateContext.ClearRenderTargetView(ComposeRenderTargetView, new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 1));

                var width = Math.Clamp(CurrentFrame.ContentSize.Width, 0, CurrentFrame.Surface.Description.Width);
                var height = Math.Clamp(CurrentFrame.ContentSize.Height, 0, CurrentFrame.Surface.Description.Height);
                var region = new SharpDX.Direct3D11.ResourceRegion(0, 0, 0, width, height, 1);
                D3DDevice.ImmediateContext.CopySubresourceRegion(sourceTexture, 0, region, ComposeTexture, 0);

                var description = sourceTexture.Description;
                description.Usage = SharpDX.Direct3D11.ResourceUsage.Default;
                description.BindFlags = SharpDX.Direct3D11.BindFlags.ShaderResource | SharpDX.Direct3D11.BindFlags.RenderTarget;
                description.CpuAccessFlags = SharpDX.Direct3D11.CpuAccessFlags.None;
                description.OptionFlags = SharpDX.Direct3D11.ResourceOptionFlags.None;

                using (var copyTexture = new SharpDX.Direct3D11.Texture2D(D3DDevice, description))
                {
                    D3DDevice.ImmediateContext.CopyResource(ComposeTexture, copyTexture);
                    result.Surface = Direct3D11Helpers.CreateDirect3DSurfaceFromSharpDXTexture(copyTexture);
                }
            }

            return result;
        }

        public void Dispose()
        {
            Stop();
            Cleanup();
        }
    }
}
