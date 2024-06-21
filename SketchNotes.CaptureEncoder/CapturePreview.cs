using System;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.UI.Composition;

namespace SketchNotes.CaptureEncoder
{
    public sealed class CapturePreview : IDisposable
    {
        private GraphicsCaptureItem Item;
        private Direct3D11CaptureFramePool FramePool;
        private GraphicsCaptureSession Session;
        private SizeInt32 LastSize;
        private bool IncludeCursor = true;
        private IDirect3DDevice Device;
        private SharpDX.Direct3D11.Device D3DDevice;
        private SharpDX.DXGI.SwapChain1 SwapChain;

        public CapturePreview(IDirect3DDevice device, GraphicsCaptureItem item)
        {
            Item = item;
            Device = device;
            D3DDevice = Direct3D11Helpers.CreateSharpDXDevice(device);

            var dxgiDevice = D3DDevice.QueryInterface<SharpDX.DXGI.Device>();
            var adapter = dxgiDevice.GetParent<SharpDX.DXGI.Adapter>();
            var factory = adapter.GetParent<SharpDX.DXGI.Factory2>();

            var description = new SharpDX.DXGI.SwapChainDescription1
            {
                Width = item.Size.Width,
                Height = item.Size.Height,
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
            SwapChain = new SharpDX.DXGI.SwapChain1(factory, dxgiDevice, ref description);

            FramePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                    Device,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    item.Size);
            Session = FramePool.CreateCaptureSession(item);
            LastSize = item.Size;

            FramePool.FrameArrived += OnFrameArrived;
        }

        public GraphicsCaptureItem Target => Item;

        public bool IsCursorCaptureEnabled
        {
            get { return IncludeCursor; }
            set
            {
                if (IncludeCursor != value)
                {
                    IncludeCursor = value;
                    Session.IsCursorCaptureEnabled = IncludeCursor;
                }
            }
        }

        public void StartCapture()
        {
            Session.StartCapture();
        }

        public ICompositionSurface CreateSurface(Compositor compositor)
        {
            return compositor.CreateCompositionSurfaceForSwapChain(SwapChain);
        }

        public void Dispose()
        {
            Session?.Dispose();
            FramePool?.Dispose();
            SwapChain?.Dispose();
            SwapChain = null;
            FramePool = null;
            Session = null;
            Item = null;
        }

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            var newSize = false;

            using (var frame = sender.TryGetNextFrame())
            {
                if (frame.ContentSize.Width != LastSize.Width ||
                    frame.ContentSize.Height != LastSize.Height)
                {
                    // The thing we have been capturing has changed size.
                    // We need to resize our swap chain first, then blit the pixels.
                    // After we do that, retire the frame and then recreate our frame pool.
                    newSize = true;
                    LastSize = frame.ContentSize;
                    SwapChain.ResizeBuffers(
                        2,
                        LastSize.Width,
                        LastSize.Height,
                        SharpDX.DXGI.Format.B8G8R8A8_UNorm, 
                        SharpDX.DXGI.SwapChainFlags.None);
                }

                using (var sourceTexture = Direct3D11Helpers.CreateSharpDXTexture2D(frame.Surface))
                using (var backBuffer = SwapChain.GetBackBuffer<SharpDX.Direct3D11.Texture2D>(0))
                using (var renderTargetView = new SharpDX.Direct3D11.RenderTargetView(D3DDevice, backBuffer))
                {
                    D3DDevice.ImmediateContext.ClearRenderTargetView(renderTargetView, new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 1));
                    D3DDevice.ImmediateContext.CopyResource(sourceTexture, backBuffer);
                }

            }

            SwapChain.Present(1, SharpDX.DXGI.PresentFlags.None);

            if (newSize)
            {
                FramePool.Recreate(
                    Device,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    LastSize);
            }
        }
    }
}
