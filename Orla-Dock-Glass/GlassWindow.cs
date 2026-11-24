using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OrlaDock;
internal sealed class GlassWindow : Window
{
    private readonly Border frame;
    internal UIElement ContentView { get; }
    internal GlassWindow(UIElement content)
    {
        ContentView = content;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; Topmost = false; ShowActivated = false;
        AllowsTransparency = true; Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.Manual;
        frame = new Border { CornerRadius = new CornerRadius(19), BorderThickness = new Thickness(1), Padding = new Thickness(9,6,9,6), Child = content, SnapsToDevicePixels = true, UseLayoutRounding = true };
        frame.BorderBrush = new LinearGradientBrush(Color.FromArgb(105,255,255,255), Color.FromArgb(35,255,255,255), 90);
        Content = frame;
        SourceInitialized += (_, _) => ApplyGlass();
        Loaded += (_, _) => { if (SystemParameters.ClientAreaAnimation) { var animation = new DoubleAnimation(0.3,1,TimeSpan.FromMilliseconds(180)); Timeline.SetDesiredFrameRate(animation,144); frame.BeginAnimation(OpacityProperty, animation); } };
        var menu = new ContextMenu();
        var settings = new MenuItem { Header = "Configurações do Orla Dock" }; settings.Click += (_,_) => ((App)Application.Current).Configure(); menu.Items.Add(settings);
        var exit = new MenuItem { Header = "Sair do Orla Dock" }; exit.Click += (_,_) => Application.Current.Shutdown(); menu.Items.Add(exit); ContextMenu = menu;
    }
    internal void ApplyScale(double scale)
    {
        if(frame.LayoutTransform is not ScaleTransform existing || existing.ScaleX!=scale)frame.LayoutTransform=new ScaleTransform(scale,scale);
    }
    internal void ApplyGlass()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        var src = HwndSource.FromHwnd(handle); if (src?.CompositionTarget != null) src.CompositionTarget.BackgroundColor = Colors.Transparent;
        frame.Background = SystemParameters.HighContrast ? SystemColors.WindowBrush : new LinearGradientBrush(Color.FromArgb((byte)(App.Settings.GlassOpacity*255),16,22,28),Color.FromArgb((byte)(Math.Min(1,App.Settings.GlassOpacity*1.15)*255),9,13,18),90);
    }
}
