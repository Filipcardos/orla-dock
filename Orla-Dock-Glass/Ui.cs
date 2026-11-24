using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OrlaDock;
internal static class Ui
{
    internal static Brush Brush(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
    internal static TextBlock Text(string text,double size,bool muted=false) => new() { Text=text, FontFamily=new FontFamily("Segoe UI Variable, Segoe UI"), FontSize=size, Foreground=Brush(muted?"#BFC6CE":"#F4F6F8"), VerticalAlignment=VerticalAlignment.Center };
    internal static Button Button(object content,string label,double width,double height) => new() { Content=content, ToolTip=label, Width=width,Height=height, Margin=new Thickness(3,0,3,0), Style=(Style)Application.Current.FindResource("GlassButton"), HorizontalContentAlignment=HorizontalAlignment.Center, VerticalContentAlignment=VerticalAlignment.Center };
    internal static Viewbox Icon(string geometry,double size=19, bool fill=false)
    {
        var path = new System.Windows.Shapes.Path { Data=Geometry.Parse(geometry), Stroke=Brush("#EDF4FA"), StrokeThickness=1.7, StrokeStartLineCap=PenLineCap.Round, StrokeEndLineCap=PenLineCap.Round, StrokeLineJoin=PenLineJoin.Round, Fill=fill?Brush("#EDF4FA"):Brushes.Transparent };
        var canvas=new Canvas { Width=24,Height=24 }; canvas.Children.Add(path);
        return new Viewbox { Width=size, Height=size, Child=canvas, Stretch=Stretch.Uniform };
    }
    internal static FrameworkElement WindowsLogo()
    {
        var canvas=new Canvas { Width=24,Height=24 };
        var fill=new LinearGradientBrush(Color.FromArgb(150,250,253,255),Color.FromArgb(32,210,230,245),90);
        foreach(var p in new[]{(2,2),(13,2),(2,13),(13,13)})
        {
            var r=new Rectangle { Width=9,Height=9,RadiusX=1,RadiusY=1, Fill=fill,Stroke=Brush("#E0F5FAFF"),StrokeThickness=0.9 };
            Canvas.SetLeft(r,p.Item1);Canvas.SetTop(r,p.Item2);canvas.Children.Add(r);
        }
        return new Viewbox { Width=28,Height=28,Child=canvas };
    }
    internal static Border Divider() => new() { Width=1,Height=37, Background=Brush("#45FFFFFF"), Margin=new Thickness(9,0,9,0), VerticalAlignment=VerticalAlignment.Center };
    internal const string Wifi="M2,8 Q12,-1 22,8 M5,12 Q12,6 19,12 M8,16 Q12,12 16,16 M12,20 L12.1,20";
    internal const string Bluetooth="M12,2 L12,22 L18,16 L6,6 M12,2 L18,8 L6,18";
    internal const string Speaker="M3,9 L7,9 L12,5 L12,19 L7,15 L3,15 Z M16,8 Q21,12 16,16 M19,5 Q26,12 19,19";
    internal const string Mute="M3,9 L7,9 L12,5 L12,19 L7,15 L3,15 Z M16,9 L22,15 M22,9 L16,15";
    internal const string Sun="M16,12 A4,4 0 1 1 8,12 A4,4 0 1 1 16,12 M12,1 L12,4 M12,20 L12,23 M1,12 L4,12 M20,12 L23,12 M4,4 L6,6 M18,18 L20,20 M4,20 L6,18 M18,6 L20,4";
    internal const string Folder="M2,6 L9,6 L11,9 L22,9 L22,20 L2,20 Z";
    internal const string App="M3,4 L21,4 L21,20 L3,20 Z M3,8 L21,8";
}
