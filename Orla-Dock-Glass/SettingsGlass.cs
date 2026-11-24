using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace OrlaDock;

// Native Windows 11 Desktop Acrylic for the non-layered configuration window.
// The dock itself remains per-pixel transparent; the two mechanisms are separate.
internal static class SettingsGlass
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Margins { public int Left, Right, Top, Bottom; }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd,ref Margins margins);
    private static int Set(IntPtr hwnd,int attribute,int value)=>DwmSetWindowAttribute(hwnd,attribute,ref value,sizeof(int));
    internal static bool TryApply(Window window)
    {
        if(!OperatingSystem.IsWindowsVersionAtLeast(10,0,22621)||SystemParameters.HighContrast)return false;
        try
        {
            var hwnd=new WindowInteropHelper(window).Handle;
            if(hwnd==IntPtr.Zero)return false;
            var source=HwndSource.FromHwnd(hwnd);
            if(source?.CompositionTarget is null)return false;
            Set(hwnd,20,1); // Immersive dark mode
            Set(hwnd,33,2); // Round corners
            var margins=new Margins{Left=-1,Right=-1,Top=-1,Bottom=-1};
            if(DwmExtendFrameIntoClientArea(hwnd,ref margins)!=0)return false;
            if(Set(hwnd,38,3)!=0) // DWMSBT_TRANSIENTWINDOW = Desktop Acrylic
            {
                margins=new Margins();DwmExtendFrameIntoClientArea(hwnd,ref margins);return false;
            }
            source.CompositionTarget.BackgroundColor=Colors.Transparent;
            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or System.ComponentModel.Win32Exception) { return false; }
    }
}
