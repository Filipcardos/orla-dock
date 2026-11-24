using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OrlaDock;

// Custom month grid: no dependency on the default white WPF Calendar template.
internal sealed class GlassCalendar : Border
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");
    private readonly TextBlock heading, monthLabel;
    private readonly UniformGrid days;
    private DateTime month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime selected = DateTime.Today;
    internal GlassCalendar()
    {
        Width=304; Padding=new Thickness(16); CornerRadius=new CornerRadius(20);
        BorderThickness=new Thickness(1);
        BorderBrush=new LinearGradientBrush(Color.FromArgb(140,255,255,255),Color.FromArgb(50,255,255,255),90);
        Background=SystemParameters.HighContrast?SystemColors.WindowBrush:new LinearGradientBrush(Color.FromArgb(202,28,33,39),Color.FromArgb(177,13,20,27),90);
        UseLayoutRounding=true; SnapsToDevicePixels=true;
        var root=new StackPanel(); Child=root;
        heading=Ui.Text("",18); heading.FontWeight=FontWeights.SemiBold; heading.TextWrapping=TextWrapping.Wrap;heading.Margin=new Thickness(0,0,0,14);root.Children.Add(heading);
        var nav=new Grid{Margin=new Thickness(0,0,0,10)};nav.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(32)});nav.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});nav.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(32)});
        var prev=Ui.Button(Ui.Icon("M15,5 L8,12 L15,19",14),"Mês anterior",30,32);prev.Margin=new Thickness(0);prev.Click+=(_,_)=>Move(-1);nav.Children.Add(prev);
        monthLabel=Ui.Text("",14);monthLabel.FontWeight=FontWeights.SemiBold;monthLabel.TextAlignment=TextAlignment.Center;
        var monthFrame=new Border{Background=Ui.Brush("#10FFFFFF"),CornerRadius=new CornerRadius(10),Margin=new Thickness(5,0,5,0),Child=monthLabel};Grid.SetColumn(monthFrame,1);nav.Children.Add(monthFrame);
        var next=Ui.Button(Ui.Icon("M9,5 L16,12 L9,19",14),"Próximo mês",30,32);next.Margin=new Thickness(0);next.Click+=(_,_)=>Move(1);Grid.SetColumn(next,2);nav.Children.Add(next);root.Children.Add(nav);
        var weekdays=new UniformGrid{Columns=7,Rows=1,Margin=new Thickness(0,0,0,3)};
        foreach(var name in new[]{"D","S","T","Q","Q","S","S"}){var t=Ui.Text(name,12);t.FontWeight=FontWeights.SemiBold;t.TextAlignment=TextAlignment.Center;weekdays.Children.Add(t);}root.Children.Add(weekdays);
        days=new UniformGrid{Columns=7,Rows=6};root.Children.Add(days);
        root.Children.Add(new Border{Height=1,Background=Ui.Brush("#32FFFFFF"),Margin=new Thickness(0,11,0,10)});
        var today=Ui.Button("Hoje","Voltar à data atual",124,34);today.HorizontalAlignment=HorizontalAlignment.Center;today.Click+=(_,_)=>{selected=DateTime.Today;month=new DateTime(selected.Year,selected.Month,1);RenderMonth();};root.Children.Add(today);
        RenderMonth();
    }
    private static string Capitalize(string text)=>Pt.TextInfo.ToUpper(text[..1])+text[1..];
    private void Move(int delta){try{var next=month.AddMonths(delta);if(next.Year<2||next.Year>9998)return;month=next;RenderMonth();}catch(ArgumentOutOfRangeException){}}
    private void RenderMonth()
    {
        heading.Text=Capitalize(selected.ToString("dddd, dd 'de' MMMM",Pt));
        monthLabel.Text=Capitalize(month.ToString("MMMM 'de' yyyy",Pt));
        days.Children.Clear();
        var start=month.AddDays(-(int)month.DayOfWeek);
        for(int i=0;i<42;i++){
            var day=start.AddDays(i);bool inMonth=day.Month==month.Month, isToday=day==DateTime.Today, isSelected=day==selected;
            var label=Ui.Text(day.Day.ToString(Pt),13,!inMonth);label.TextAlignment=TextAlignment.Center;if(!inMonth)label.Opacity=.65;
            if(isToday||isSelected)label.FontWeight=FontWeights.SemiBold;
            var button=Ui.Button(label,day.ToString("D",Pt),32,30);button.Margin=new Thickness(1);button.Background=isSelected?Ui.Brush("#52B3D7E7"):Brushes.Transparent;button.BorderBrush=isToday?Ui.Brush("#A0D6F3FF"):isSelected?Ui.Brush("#65FFFFFF"):Brushes.Transparent;
            System.Windows.Automation.AutomationProperties.SetName(button,day.ToString("D",Pt));
            button.Click+=(_,_)=>{selected=day;if(!inMonth)month=new DateTime(day.Year,day.Month,1);RenderMonth();};days.Children.Add(button);
        }
    }
    internal void AnimateIn()
    {
        if(!SystemParameters.ClientAreaAnimation)return;
        var fade=new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(150)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};Timeline.SetDesiredFrameRate(fade,144);BeginAnimation(OpacityProperty,fade);
    }
    internal static CustomPopupPlacement[] Place(Size popup,Size anchor,DockEdge edge)
    {
        const double gap=8;double x=(anchor.Width-popup.Width)/2,y=(anchor.Height-popup.Height)/2;
        var top=new CustomPopupPlacement(new Point(x,-popup.Height-gap),PopupPrimaryAxis.Horizontal);
        var bottom=new CustomPopupPlacement(new Point(x,anchor.Height+gap),PopupPrimaryAxis.Horizontal);
        var left=new CustomPopupPlacement(new Point(-popup.Width-gap,y),PopupPrimaryAxis.Vertical);
        var right=new CustomPopupPlacement(new Point(anchor.Width+gap,y),PopupPrimaryAxis.Vertical);
        return edge switch{DockEdge.Bottom=>new[]{top,bottom},DockEdge.Top=>new[]{bottom,top},DockEdge.Left=>new[]{right,left},_=>new[]{left,right}};
    }
}
