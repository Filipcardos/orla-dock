using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace OrlaDock;

// A separate, transparent HWND registers ONE shell appbar for both floating modules.
// The reservation is in physical pixels; WPF layout uses primary-monitor DIPs.
internal sealed class AppBarReservation : IDisposable
{
    private const uint ABM_NEW=0, ABM_REMOVE=1, ABM_QUERYPOS=2, ABM_SETPOS=3, ABM_WINDOWPOSCHANGED=9;
    private const int ABN_POSCHANGED=1, ABN_FULLSCREENAPP=2;
    private const int WM_WINDOWPOSCHANGED=0x47, WM_NCHITTEST=0x84, WM_DISPLAYCHANGE=0x7e, WM_DPICHANGED=0x02e0;
    private const uint SWP_NOSIZE=0x1, SWP_NOMOVE=0x2, SWP_NOZORDER=0x4, SWP_NOACTIVATE=0x10, SWP_SHOWWINDOW=0x40;
    private readonly HwndSource source;
    private readonly Action reposition;
    private readonly Action<bool> fullscreenChanged;
    private readonly uint callback, taskbarCreated;
    private bool registered, queued, disposed, hasPosition, moving;
    private NativeRect last;
    private uint lastEdge;
    internal bool IsFullscreen {get;private set;}

    internal AppBarReservation(Action reposition, Action<bool> fullscreenChanged)
    {
        this.reposition=reposition;this.fullscreenChanged=fullscreenChanged;
        callback=RegisterWindowMessage("OrlaDock.AppBar.Callback.v16");
        taskbarCreated=RegisterWindowMessage("TaskbarCreated");
        var p=new HwndSourceParameters("OrlaDock.AppBarReservation"){
            WindowStyle=unchecked((int)0x80000000), // WS_POPUP
            ExtendedWindowStyle=0x08000000|0x00000080|0x00000020, // NOACTIVATE, TOOLWINDOW, TRANSPARENT
            PositionX=0,PositionY=0,Width=1,Height=1,UsesPerPixelOpacity=true};
        source=new HwndSource(p);
        source.CompositionTarget.BackgroundColor=Colors.Transparent;
        source.RootVisual=new DrawingVisual();
        source.AddHook(WindowProc);
    }
    private AppBarData Data()=>new(){cbSize=(uint)Marshal.SizeOf<AppBarData>(),hWnd=source.Handle,uCallbackMessage=callback};
    private bool Register()
    {
        if(registered)return true;
        if(callback==0)return false;
        var data=Data();registered=SHAppBarMessage(ABM_NEW,ref data)!=UIntPtr.Zero;
        return registered;
    }
    internal Rect? Reserve(DockEdge edge,double thicknessDip)
    {
        if(disposed||!Register())return null;
        var monitor=MonitorFromPoint(new NativePoint(),1); // primary monitor
        var info=new MonitorInfo{cbSize=(uint)Marshal.SizeOf<MonitorInfo>()};
        if(monitor==IntPtr.Zero||!GetMonitorInfo(monitor,ref info)){Release();return null;}
        uint dpi=GetDpiForWindow(source.Handle);double scale=(dpi==0?96:dpi)/96.0;
        int thickness=Math.Max(1,(int)Math.Ceiling(thicknessDip*scale));
        uint nativeEdge=edge switch{DockEdge.Left=>0u,DockEdge.Top=>1u,DockEdge.Right=>2u,_=>3u};
        var d=Data();d.uEdge=nativeEdge;d.rc=info.rcMonitor;
        SHAppBarMessage(ABM_QUERYPOS,ref d);
        // QUERYPOS returns the available edge, not necessarily the requested thickness.
        switch(edge){case DockEdge.Left:d.rc.Right=d.rc.Left+thickness;break;case DockEdge.Right:d.rc.Left=d.rc.Right-thickness;break;case DockEdge.Top:d.rc.Bottom=d.rc.Top+thickness;break;default:d.rc.Top=d.rc.Bottom-thickness;break;}
        if(!hasPosition||lastEdge!=nativeEdge||!last.Equals(d.rc))
        {
            if(SHAppBarMessage(ABM_SETPOS,ref d)==UIntPtr.Zero){Release();return null;}
            last=d.rc;lastEdge=nativeEdge;hasPosition=true;
            moving=true;
            try{if(!SetWindowPos(source.Handle,IntPtr.Zero,last.Left,last.Top,last.Right-last.Left,last.Bottom-last.Top,SWP_NOACTIVATE|SWP_NOZORDER|SWP_SHOWWINDOW)){Release();return null;}}
            finally{moving=false;}
            var moved=Data();SHAppBarMessage(ABM_WINDOWPOSCHANGED,ref moved);
        }
        return new Rect(last.Left/scale,last.Top/scale,(last.Right-last.Left)/scale,(last.Bottom-last.Top)/scale);
    }
    private void QueuePosition()
    {
        if(queued||disposed)return;queued=true;
        source.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{queued=false;if(!disposed)reposition();}));
    }
    private IntPtr WindowProc(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(msg==WM_NCHITTEST){handled=true;return new IntPtr(-1);} // HTTRANSPARENT
        if((uint)msg==callback&&callback!=0){
            switch(wParam.ToInt32()){
                case ABN_POSCHANGED: QueuePosition();break;
                case ABN_FULLSCREENAPP: IsFullscreen=lParam!=IntPtr.Zero;fullscreenChanged(IsFullscreen);break;
            }
            handled=true;
        }else if((uint)msg==taskbarCreated&&taskbarCreated!=0){registered=false;hasPosition=false;IsFullscreen=false;fullscreenChanged(false);QueuePosition();}
        else if(msg==WM_DISPLAYCHANGE||msg==WM_DPICHANGED){hasPosition=false;QueuePosition();}
        else if(msg==WM_WINDOWPOSCHANGED&&registered&&!moving){var d=Data();SHAppBarMessage(ABM_WINDOWPOSCHANGED,ref d);}
        return IntPtr.Zero;
    }
    internal static void SetFullscreenZOrder(Window window,bool fullscreen)
    {
        IntPtr handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero)return;
        // HWND_BOTTOM on fullscreen, normal HWND_TOP (NOT topmost) when leaving.
        SetWindowPos(handle,fullscreen?new IntPtr(1):IntPtr.Zero,0,0,0,0,SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE);
    }
    private void Release(){if(registered){var data=Data();SHAppBarMessage(ABM_REMOVE,ref data);}registered=false;hasPosition=false;}
    public void Dispose(){if(disposed)return;disposed=true;Release();source.RemoveHook(WindowProc);source.Dispose();}

    [StructLayout(LayoutKind.Sequential)]private struct NativePoint{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]private struct NativeRect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]private struct MonitorInfo{public uint cbSize;public NativeRect rcMonitor,rcWork;public uint dwFlags;}
    [StructLayout(LayoutKind.Sequential)]private struct AppBarData{public uint cbSize;public IntPtr hWnd;public uint uCallbackMessage,uEdge;public NativeRect rc;public IntPtr lParam;}
    [DllImport("shell32.dll")]private static extern UIntPtr SHAppBarMessage(uint dwMessage,ref AppBarData pData);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")]private static extern IntPtr MonitorFromPoint(NativePoint point,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] [return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    [DllImport("user32.dll")]private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]private static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int cx,int cy,uint flags);
}
