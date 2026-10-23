#if WINDOWS
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using WinRT;
using ConsolePlayground.Cmd.Imoging;

namespace ConsolePlayground.Cmd.Imoging.Platform;

public sealed class WindowCapture : IDisposable, IImageSource {
    private readonly IDirect3DDevice device;
    private readonly GraphicsCaptureItem item;
    private Direct3D11CaptureFramePool? pool;
    private GraphicsCaptureSession? session;
    private Windows.Graphics.SizeInt32 lastSize;
    readonly object lockObzekt = new();
    SKBitmap? latest;
    Stopwatch stopwatch = new();
    ManualResetEventSlim frameEvent = new();
    volatile bool disposed;

    private readonly IntPtr hwnd;
    // 対象ウインドウが閉じられた
    bool closed;
    // ウインドウ生存確認の間隔
    static readonly TimeSpan closedPollInterval = TimeSpan.FromMilliseconds(100);

    public WindowCapture(IntPtr hwnd) {
        this.hwnd = hwnd;
        device = Interop.CreateDirect3DDevice();
        item = Interop.CreateItemForWindow(hwnd);
        lastSize = item.Size;
    }
    public WindowCapture(MonitorInfo mon)
    {
        this.hwnd = IntPtr.Zero;
        device = Interop.CreateDirect3DDevice();
        item = Interop.CreateItemForMonitor(mon.HMonitor);
        lastSize.Width = mon.MonitorArea.Right - mon.MonitorArea.Left;
        lastSize.Height = mon.MonitorArea.Bottom - mon.MonitorArea.Top;
    }

    public bool IsValid() => true;

    public void Start() {
        stopwatch.Start();
        pool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, lastSize);
        pool.FrameArrived += OnFrameArrived;

        session = pool.CreateCaptureSession(item);
        session.IsCursorCaptureEnabled = false;
        // 黄色い枠消し(GraphicsCaptureAccess.RequestAccessAsync&manifestが必要でめんどいので保留)
        //await GraphicsCaptureAccess.RequestAccessAsync(GraphicsCaptureAccessKind.Borderless);
        // session.IsBorderRequired = false;
        session.StartCapture();
    }

    public void Stop() {
        session?.Dispose(); session = null;
        if (pool is not null) { pool.FrameArrived -= OnFrameArrived; pool.Dispose(); pool = null; }
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args) {
        if (disposed) {
            return;
        }
        try {
            using var frame = sender.TryGetNextFrame();
            if (frame is null) {
                return;
            }

            // ここでGPUからCPUに転送
            using SoftwareBitmap sb = SoftwareBitmap
                .CreateCopyFromSurfaceAsync(frame.Surface)
                .AsTask().GetAwaiter().GetResult();

            int w = sb.PixelWidth, h = sb.PixelHeight;
            var pixels = new byte[w * h * 4];
            sb.CopyToBuffer(pixels.AsBuffer());

            var bmp = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
            Marshal.Copy(pixels, 0, bmp.GetPixels(), pixels.Length);

            var cs = frame.ContentSize;
            if (cs.Width != lastSize.Width || cs.Height != lastSize.Height) {
                lastSize = cs;
                // ウインドウサイズが変わったのでフレームプールを作り直し
                sender.Recreate(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, cs);
            }
            lock (lockObzekt) {
                if (disposed) {
                    bmp.Dispose();
                    return;
                }
                latest?.Dispose();
                latest = bmp;
                frameEvent.Set();
            }
        }
        catch (Exception ex) {
            // 破棄中に飛んできたコールバックの失敗は握りつぶす
            if (!disposed) {
                Console.Error.WriteLine($"capture error: {ex.Message}");
            }
        }
    }

    const double fixed60 = 60.0;

    public double FrameRate => fixed60;
    public TimeSpan Duration => stopwatch.Elapsed;
    public bool Next(CancellationToken token) {
        while (!token.IsCancellationRequested) {
            lock (lockObzekt) {
                if (latest != null) {
                    return true;
                }
                frameEvent.Reset();
            }
            if (closed) {
                return false;
            }
            if (!frameEvent.Wait(closedPollInterval, token)
                && hwnd != IntPtr.Zero && !Interop.IsWindow(hwnd)) {
                closed = true;
            }
        }
        return false;
    }

    public T Frame<T>(FrameDelegaty<T> delegaty) {
        SKBitmap? bitmap;
        lock (lockObzekt) {
            bitmap = latest;
            latest = null;
        }
        using (bitmap) {
            return delegaty(bitmap);
        }
    }

    public void Dispose() {
        Stop();
        lock (lockObzekt) {
            disposed = true;
            latest?.Dispose();
            latest = null;
        }
        device.Dispose();
        frameEvent.Dispose();
    }

    #region D3DとCOM関連

    private static class Interop
    {
        private const int SlotCreateForWindow = 3;
        private const int SlotCreateForMonitor = 4;
        private const string GraphicsCaptureItemClassName = "Windows.Graphics.Capture.GraphicsCaptureItem";
        private static readonly Guid IID_IGraphicsCaptureItemInterop = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");

        [DllImport("combase.dll")]
        private static extern int WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)] string sourceString, uint length, out IntPtr hstring);

        [DllImport("combase.dll")]
        private static extern int WindowsDeleteString(IntPtr hstring);

        [DllImport("combase.dll")]
        private static extern int RoGetActivationFactory(IntPtr activatableClassId, in Guid iid, out IntPtr factory);

        [DllImport("d3d11.dll")]
        private static extern int D3D11CreateDevice(
            IntPtr adapter, int driverType, IntPtr software, uint flags,
            IntPtr featureLevels, uint featureLevelCount, uint sdkVersion,
            out IntPtr device, out int featureLevel, out IntPtr immediateContext);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("d3d11.dll")]
        private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

        private const int D3D_DRIVER_TYPE_HARDWARE = 1;
        private const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;
        private const uint D3D11_SDK_VERSION = 7;

        private static readonly Guid IID_IDXGIDevice = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
        private static readonly Guid IID_GraphicsCaptureItem = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

        public static GraphicsCaptureItem CreateItemForWindow(IntPtr hwnd) => CreateItem(SlotCreateForWindow, hwnd);

        public static GraphicsCaptureItem CreateItemForMonitor(IntPtr monitor) => CreateItem(SlotCreateForMonitor, monitor);

        private static unsafe GraphicsCaptureItem CreateItem(int slot, IntPtr handle)
        {
            Marshal.ThrowExceptionForHR(WindowsCreateString(
                GraphicsCaptureItemClassName, (uint)GraphicsCaptureItemClassName.Length, out var className));
            IntPtr factory = IntPtr.Zero, ptr = IntPtr.Zero;
            try {
                // 若干面倒くせえけど、Native AOT対策でVtblから直で関数ポインターを取る
                Marshal.ThrowExceptionForHR(RoGetActivationFactory(className, in IID_IGraphicsCaptureItemInterop, out factory));
                var vtbl = *(IntPtr**)factory;
                var create = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Guid*, IntPtr*, int>)vtbl[slot];
                var iid = IID_GraphicsCaptureItem;
                Marshal.ThrowExceptionForHR(create(factory, handle, &iid, &ptr));
                return GraphicsCaptureItem.FromAbi(ptr);
            }
            finally {
                if (ptr != IntPtr.Zero) {
                    Marshal.Release(ptr);
                }
                if (factory != IntPtr.Zero) {
                    Marshal.Release(factory);
                }
                WindowsDeleteString(className);
            }
        }

        public static IDirect3DDevice CreateDirect3DDevice()
        {
            Marshal.ThrowExceptionForHR(D3D11CreateDevice(
                IntPtr.Zero, D3D_DRIVER_TYPE_HARDWARE, IntPtr.Zero, D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                IntPtr.Zero, 0, D3D11_SDK_VERSION, out var d3dDevice, out _, out var context));

            IntPtr dxgiDevice = IntPtr.Zero, abi = IntPtr.Zero;
            try {
                var iid = IID_IDXGIDevice;
                Marshal.ThrowExceptionForHR(Marshal.QueryInterface(d3dDevice, in iid, out dxgiDevice));
                Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out abi));
                return MarshalInterface<IDirect3DDevice>.FromAbi(abi);
            }
            finally {
                if (abi != IntPtr.Zero) Marshal.Release(abi);
                if (dxgiDevice != IntPtr.Zero) Marshal.Release(dxgiDevice);
                Marshal.Release(context);
                Marshal.Release(d3dDevice);
            }
        }
    }
    #endregion
}

#endif
