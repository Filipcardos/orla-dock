using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Text;
using System.IO;

namespace OrlaDock;
internal sealed class DockView : StackPanel
{
    private readonly StackPanel applications = new() { Orientation=Orientation.Horizontal, VerticalAlignment=VerticalAlignment.Center };
    private readonly ScrollViewer appScroll = new() { HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden, VerticalScrollBarVisibility=ScrollBarVisibility.Disabled, Focusable=false, PanningMode=PanningMode.Both };
    private readonly TextBlock clock = Ui.Text("",22), date = Ui.Text("",11,true);
    private readonly Dictionary<string,ImageSource?> icons = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(Border Dot, Button Button, string Path)> tiles = new();
    private readonly DispatcherTimer timer;
    private readonly Border divider;
    private Popup? calendarPopup;
    internal double DesiredDockHeight => 18+55+VisibleTileCount(true)*51+12+48;
    private string signature = "";
    private int ticks;
    private bool retryMissingIcons;
    internal double DesiredDockWidth => 18+55+VisibleTileCount(false)*55+19+85+12;
    private int VisibleTileCount(bool vertical) => Math.Min(tiles.Count, Math.Max(1, (int)Math.Floor((vertical ? (SystemParameters.WorkArea.Height-16)/App.Settings.Scale-309 : (SystemParameters.WorkArea.Width-16)/App.Settings.Scale-481)/(vertical?51:55))));
    internal event EventHandler? DockWidthChanged;
    internal DockView()
    {
        Orientation=Orientation.Horizontal; VerticalAlignment=VerticalAlignment.Center; HorizontalAlignment=HorizontalAlignment.Center;
        var start=Ui.Button(Ui.WindowsLogo(),"Menu Iniciar",49,51); start.Click+=(_,_)=>NativeApps.StartMenu(); Children.Add(start);
        appScroll.Content=applications; Children.Add(appScroll); divider=Ui.Divider(); Children.Add(divider);
        appScroll.PreviewMouseWheel+=(_,e)=>{if(Orientation==Orientation.Horizontal && appScroll.ExtentWidth>appScroll.ViewportWidth){appScroll.ScrollToHorizontalOffset(appScroll.HorizontalOffset-e.Delta/120.0*55);e.Handled=true;}else if(Orientation==Orientation.Vertical && appScroll.ExtentHeight>appScroll.ViewportHeight){appScroll.ScrollToVerticalOffset(appScroll.VerticalOffset-e.Delta/120.0*51);e.Handled=true;}};
        clock.FontWeight=FontWeights.SemiBold; clock.TextAlignment=TextAlignment.Center;date.TextAlignment=TextAlignment.Center;
        var timePanel=new StackPanel { Width=85, VerticalAlignment=VerticalAlignment.Center }; timePanel.Children.Add(clock);timePanel.Children.Add(date);
        var clockButton=Ui.Button(timePanel,"Data e hora",85,51);clockButton.Margin=new Thickness(0);clockButton.Background=Ui.Brush("#09FFFFFF"); clockButton.Click+=(_,_)=>ShowCalendar(clockButton);Children.Add(clockButton);
        UpdateClock(); UpdateApps();
        timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(500) };
        timer.Tick+=(_,_)=>{ UpdateClock(); ++ticks; UpdateApps(); }; timer.Start();
        Unloaded+=(_,_)=>timer.Stop();
    }
    internal void SetVertical(bool vertical)
    {
        var orientation=vertical?Orientation.Vertical:Orientation.Horizontal;
        if(Orientation!=orientation){appScroll.ScrollToHorizontalOffset(0);appScroll.ScrollToVerticalOffset(0);}
        appScroll.HorizontalScrollBarVisibility=vertical?ScrollBarVisibility.Disabled:ScrollBarVisibility.Hidden;
        appScroll.VerticalScrollBarVisibility=vertical?ScrollBarVisibility.Hidden:ScrollBarVisibility.Disabled;
        Orientation=applications.Orientation=orientation;applications.HorizontalAlignment=HorizontalAlignment.Center;
        divider.HorizontalAlignment=HorizontalAlignment.Center;divider.Width=vertical?55:1;divider.Height=vertical?1:37;divider.Margin=vertical?new Thickness(0,5,0,5):new Thickness(9,0,9,0);
        UpdateViewport();
    }
    internal void CloseCalendar(){if(calendarPopup is not null)calendarPopup.IsOpen=false;}
    private void ShowCalendar(Button anchor)
    {
        if(calendarPopup?.IsOpen==true){CloseCalendar();return;}
        var widget=new GlassCalendar();
        calendarPopup=new Popup{PlacementTarget=anchor,AllowsTransparency=true,StaysOpen=false,PopupAnimation=PopupAnimation.None,Child=widget,Placement=PlacementMode.Custom};
        calendarPopup.CustomPopupPlacementCallback=(popupSize,targetSize,offset)=>GlassCalendar.Place(popupSize,targetSize,App.Settings.Edge);
        calendarPopup.Opened+=(_,_)=>widget.AnimateIn();calendarPopup.IsOpen=true;
    }
    private void UpdateClock()
    {
        var now=DateTime.Now; var culture=CultureInfo.GetCultureInfo("pt-BR");clock.Text=now.ToString("HH:mm",culture);date.Text=now.ToString("ddd, dd/MM",culture).Replace(".","");
    }
    private void UpdateApps()
    {
        var windows=NativeApps.Windows();
        var openPaths=windows.Select(w=>w.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        // Preserve existing tile order; append newly opened applications.
        var paths=tiles.Select(t=>t.Path).Where(p=>openPaths.Contains(p,StringComparer.OrdinalIgnoreCase)).ToList();
        paths.AddRange(openPaths.Where(p=>!paths.Contains(p,StringComparer.OrdinalIgnoreCase)));
        string next=string.Join("|",paths);
        if(next!=signature || (retryMissingIcons && ticks%20==0))
        {
            if(next!=signature)CloseCalendar();
            signature=next; retryMissingIcons=false; applications.Children.Clear();tiles.Clear();
            foreach(var path in paths) AddTile(path,windows.First(w=>string.Equals(w.Path,path,StringComparison.OrdinalIgnoreCase)).Handle);
            UpdateViewport(); DockWidthChanged?.Invoke(this,EventArgs.Empty);
        }
        UpdateActive(windows);
    }
    private void UpdateViewport()
    {
        bool vertical=Orientation==Orientation.Vertical;
        appScroll.Width=vertical?double.NaN:VisibleTileCount(false)*55;
        appScroll.Height=vertical?VisibleTileCount(true)*51:double.NaN;
        appScroll.Visibility=tiles.Count==0?Visibility.Collapsed:Visibility.Visible;
    }
    private void AddTile(string path,IntPtr hwnd)
    {
        string title=Path.GetFileNameWithoutExtension(path);try { title=FileVersionInfo.GetVersionInfo(path).FileDescription??title; } catch {}
        if(!icons.TryGetValue(path,out var image)||image is null) { image=NativeApps.Icon(path,hwnd);if(image is not null)icons[path]=image; }
        var holder=new Grid { Width=39,Height=45 };holder.RowDefinitions.Add(new RowDefinition { Height=new GridLength(35) });holder.RowDefinitions.Add(new RowDefinition { Height=new GridLength(10) });
        // If the original icon is inaccessible, use the application name, never a generic drawing.
        if(image is null) retryMissingIcons=true;
        FrameworkElement icon=image is not null ? new Image { Source=image,Width=34,Height=34,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,Stretch=Stretch.Uniform } : new TextBlock { Text=title, FontSize=9, Foreground=Ui.Brush("#F4F6F8"), TextTrimming=TextTrimming.CharacterEllipsis, Width=39, TextAlignment=TextAlignment.Center, VerticalAlignment=VerticalAlignment.Center };
        RenderOptions.SetBitmapScalingMode(icon,BitmapScalingMode.HighQuality); holder.Children.Add(icon);
        var dot=new Border { Width=11,Height=3,CornerRadius=new CornerRadius(2),Background=Ui.Brush("#A4AFB9"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center };Grid.SetRow(dot,1);holder.Children.Add(dot);
        var button=Ui.Button(holder,title,49,51);button.Click+=(_,_)=>{var running=NativeApps.Windows().FirstOrDefault(w=>string.Equals(w.Path,path,StringComparison.OrdinalIgnoreCase));if(running is not null) NativeApps.Activate(running.Handle); else UpdateApps();};
        var menu=new ContextMenu();var open=new MenuItem { Header="Abrir nova janela" };open.Click+=(_,_)=>NativeApps.Open(path);menu.Items.Add(open);button.ContextMenu=menu;
        applications.Children.Add(button);tiles.Add((dot,button,path));
    }
    private void UpdateActive(List<NativeApps.AppWindow>? windows=null)
    {
        windows??=NativeApps.Windows();var active=NativeApps.GetForegroundWindow();
        foreach(var t in tiles)
        {
            bool running=windows.Any(w=>string.Equals(w.Path,t.Path,StringComparison.OrdinalIgnoreCase));bool selected=windows.Any(w=>w.Handle==active&&string.Equals(w.Path,t.Path,StringComparison.OrdinalIgnoreCase));
            t.Dot.Opacity=running?1:0;t.Dot.Background=Ui.Brush(selected?"#F1F5F8":"#87929B"); t.Button.Background=Ui.Brush(selected?"#20FFFFFF":"#0BFFFFFF");
        }
    }
}
internal static class NativeApps
{
    internal sealed record AppWindow(IntPtr Handle,string Path);
    internal static void Open(string path) {try {Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch(Exception e){MessageBox.Show(e.Message,"Orla Dock");}}
    internal static void StartMenu(){keybd_event(0x5B,0,0,UIntPtr.Zero);keybd_event(0x5B,0,2,UIntPtr.Zero);}
    internal static void Activate(IntPtr hwnd){ if(IsIconic(hwnd))ShowWindowAsync(hwnd,9);else ShowWindowAsync(hwnd,5);if(!SetForegroundWindow(hwnd)) FlashWindow(hwnd,true); }
    internal static List<AppWindow> Windows()
    {
        var results=new List<AppWindow>(); EnumWindows((hwnd,_)=>{
            if(!IsWindowVisible(hwnd)||GetWindowTextLength(hwnd)==0)return true;
            long style=GetWindowLongPtr(hwnd,-20).ToInt64();
            bool appWindow=(style&0x40000)!=0;
            if(!appWindow && ((style&0x80)!=0 || GetWindow(hwnd,4)!=IntPtr.Zero))return true;
            var cls=new StringBuilder(256);GetClassName(hwnd,cls,cls.Capacity);if(cls.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")return true;
            if(DwmGetWindowAttribute(hwnd,14,out int cloaked,4)==0&&cloaked!=0)return true;
            GetWindowThreadProcessId(hwnd,out uint pid);if(pid==Environment.ProcessId)return true;
            try{using var process=Process.GetProcessById((int)pid);var path=process.MainModule?.FileName;
                if(string.Equals(process.ProcessName,"ApplicationFrameHost",StringComparison.OrdinalIgnoreCase)){
                    path=null;EnumChildWindows(hwnd,(child,__)=>{GetWindowThreadProcessId(child,out uint childPid);if(childPid==0||childPid==pid)return true;try{using var hosted=Process.GetProcessById((int)childPid);var candidate=hosted.MainModule?.FileName;if(!string.IsNullOrEmpty(candidate)){path=candidate;return false;}}catch{}return true;},IntPtr.Zero);
                }
                if(!string.IsNullOrEmpty(path))results.Add(new AppWindow(hwnd,path));}catch{}
            return true;
        },IntPtr.Zero);return results;
    }
    internal static ImageSource? Icon(string path,IntPtr hwnd)
    {
        if(hwnd!=IntPtr.Zero){try{foreach(var type in new[]{1,2,0}){if(SendMessageTimeout(hwnd,0x007F,(UIntPtr)(uint)type,IntPtr.Zero,2,100,out var result)!=IntPtr.Zero&&result!=UIntPtr.Zero){var src=Imaging.CreateBitmapSourceFromHIcon(new IntPtr(unchecked((long)result.ToUInt64())),Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());if(src.PixelWidth>=32){src.Freeze();return src;}}}}catch{}}
        IShellItemImageFactory? factory=null;IntPtr bitmap=IntPtr.Zero;
        try{var iid=typeof(IShellItemImageFactory).GUID;int hr=SHCreateItemFromParsingName(path,IntPtr.Zero,ref iid,out factory);if(hr<0||factory is null)return ExtractExecutableIcon(path);hr=factory.GetImage(new SizeNative{cx=128,cy=128},0x4|0x1,out bitmap);if(hr<0||bitmap==IntPtr.Zero)return ExtractExecutableIcon(path);var source=Imaging.CreateBitmapSourceFromHBitmap(bitmap,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());source.Freeze();return source;}catch{return ExtractExecutableIcon(path);}finally{if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);if(factory is not null)Marshal.ReleaseComObject(factory);}
    }
    private static ImageSource? ExtractExecutableIcon(string path)
    {
        var large=new IntPtr[1];var small=new IntPtr[1];
        try{if(ExtractIconEx(path,0,large,small,1)==0)return null;var handle=large[0]!=IntPtr.Zero?large[0]:small[0];if(handle==IntPtr.Zero)return null;var image=Imaging.CreateBitmapSourceFromHIcon(handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());image.Freeze();return image;}catch{return null;}
        finally{if(large[0]!=IntPtr.Zero)DestroyIcon(large[0]);if(small[0]!=IntPtr.Zero&&small[0]!=large[0])DestroyIcon(small[0]);}
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern uint ExtractIconEx(string file,int index,IntPtr[] large,IntPtr[] small,uint count);
    [DllImport("user32.dll")]private static extern bool DestroyIcon(IntPtr icon);
    [ComImport,Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IShellItemImageFactory{[PreserveSig]int GetImage(SizeNative size,uint flags,out IntPtr hbitmap);}
    [StructLayout(LayoutKind.Sequential)]private struct SizeNative{public int cx,cy;}
    private delegate bool EnumWindowsProc(IntPtr hwnd,IntPtr lp);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr SendMessageTimeout(IntPtr hwnd,uint msg,UIntPtr wp,IntPtr lp,uint flags,uint timeout,out UIntPtr result);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]private static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
    [DllImport("user32.dll")]private static extern bool EnumWindows(EnumWindowsProc callback,IntPtr lp);
    [DllImport("user32.dll")]private static extern bool EnumChildWindows(IntPtr parent,EnumWindowsProc callback,IntPtr lp);
    [DllImport("user32.dll")]private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")]private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")]private static extern bool ShowWindowAsync(IntPtr hwnd,int command);
    [DllImport("user32.dll")]private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")]private static extern bool FlashWindow(IntPtr hwnd,bool invert);
    [DllImport("user32.dll")]private static extern IntPtr GetWindow(IntPtr hwnd,uint cmd);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetClassName(IntPtr hwnd,StringBuilder name,int max);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
    [DllImport("user32.dll")]internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]private static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
    [DllImport("dwmapi.dll")]private static extern int DwmGetWindowAttribute(IntPtr hwnd,int attr,out int value,int size);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern int SHCreateItemFromParsingName(string path,IntPtr bind,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out IShellItemImageFactory? item);
    [DllImport("gdi32.dll")]private static extern bool DeleteObject(IntPtr h);
}
