using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;

namespace OrlaDock;
public enum DockEdge { Top, Bottom, Left, Right }
public enum DockSize { Small, Medium, Large }
public sealed class DockSettings
{
    public DockEdge Edge {get;set;}=DockEdge.Bottom;
    public bool AutoStart {get;set;}
    public DockSize Size {get;set;}=DockSize.Small;
    internal double Scale => Size switch { DockSize.Small=>42.0/72, DockSize.Medium=>56.0/72, _=>1.0 };
    public int NetworkLimit {get;set;}=5;
    public double GlassOpacity {get;set;}=0.22;
    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OrlaDock","settings-v12.json");
    internal static DockSettings? Load(){try{var s=JsonSerializer.Deserialize<DockSettings>(File.ReadAllText(FilePath));if(s is null)return null;if(!Enum.IsDefined(s.Edge))s.Edge=DockEdge.Bottom;if(!Enum.IsDefined(s.Size))s.Size=DockSize.Small;s.NetworkLimit=Math.Clamp(s.NetworkLimit,4,5);s.GlassOpacity=double.IsFinite(s.GlassOpacity)?Math.Clamp(s.GlassOpacity,0,0.65):0.22;return s;}catch{return null;}}
    internal void Save(){Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);var tmp=FilePath+".tmp";File.WriteAllText(tmp,JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));File.Move(tmp,FilePath,true);}
    internal void ApplyStartup(){using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");if(!AutoStart){key.DeleteValue("OrlaDockGlass",false);return;}var exe=Environment.ProcessPath;if(string.IsNullOrWhiteSpace(exe)||!File.Exists(exe)||!string.Equals(Path.GetFileNameWithoutExtension(exe),"OrlaDock",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Execute o OrlaDock.exe publicado ou compilado em uma pasta permanente antes de habilitar a inicialização.");key.SetValue("OrlaDockGlass",$"\"{exe}\" --startup",RegistryValueKind.String);}
}
internal sealed class SettingsWindow : Window
{
    internal DockSettings? Result {get;private set;}
    internal SettingsWindow(DockSettings current)
    {
        Title="Orla Dock · Configuração inicial";
        Width=468; MaxWidth=Math.Max(320,SystemParameters.WorkArea.Width-24); MaxHeight=Math.Max(240,SystemParameters.WorkArea.Height-24);
        SizeToContent=SizeToContent.Height; ResizeMode=ResizeMode.NoResize;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;
        WindowStyle=WindowStyle.None; AllowsTransparency=false; Background=Brushes.Transparent;
        Foreground=Brushes.White; FontFamily=new FontFamily("Segoe UI Variable, Segoe UI");
        UseLayoutRounding=true; SnapsToDevicePixels=true;
        var frame=new Border{CornerRadius=new CornerRadius(14),BorderThickness=new Thickness(1),
            BorderBrush=new LinearGradientBrush(Color.FromArgb(150,255,255,255),Color.FromArgb(45,255,255,255),90),
            Background=new LinearGradientBrush(Color.FromRgb(39,47,54),Color.FromRgb(22,28,34),90)};
        Content=frame;
        SourceInitialized+=(_,_)=>{
            if(SettingsGlass.TryApply(this))frame.Background=new LinearGradientBrush(Color.FromArgb(40,255,255,255),Color.FromArgb(48,10,17,23),90);
            else if(SystemParameters.HighContrast)frame.Background=Brushes.Black;
        };
        var layout=new DockPanel{LastChildFill=true};frame.Child=layout;
        var header=new Grid{Height=48,Margin=new Thickness(16,2,8,0)};
        header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        var drag=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Background=Brushes.Transparent};
        var logo=Ui.WindowsLogo();logo.Width=22;logo.Height=22;logo.Margin=new Thickness(0,0,9,0);drag.Children.Add(logo);
        var headerText=Ui.Text("Orla Dock  ·  Configuração inicial",13);headerText.FontWeight=FontWeights.SemiBold;drag.Children.Add(headerText);
        drag.MouseLeftButtonDown+=(_,e)=>{if(e.LeftButton==System.Windows.Input.MouseButtonState.Pressed){try{DragMove();}catch(InvalidOperationException){}}};
        header.Children.Add(drag);
        var close=Ui.Button(Ui.Icon("M6,6 L18,18 M18,6 L6,18",15),"Fechar",32,30);close.Background=Brushes.Transparent;close.BorderBrush=Brushes.Transparent;close.Click+=(_,_)=>DialogResult=false;
        System.Windows.Automation.AutomationProperties.SetName(close,"Fechar configuração");Grid.SetColumn(close,1);header.Children.Add(close);
        DockPanel.SetDock(header,Dock.Top);layout.Children.Add(header);
        var footer=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(18,12,18,16)};
        var cancel=Ui.Button("Cancelar","Cancelar",104,36);cancel.IsCancel=true;
        var apply=Ui.Button("Concluir","Salvar e aplicar",120,36);apply.IsDefault=true;apply.Background=Ui.Brush("#30FFFFFF");apply.BorderBrush=Ui.Brush("#70FFFFFF");
        footer.Children.Add(cancel);footer.Children.Add(apply);DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);
        var root=new StackPanel{Margin=new Thickness(20,2,20,0)};
        layout.Children.Add(new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Content=root,Focusable=false});
        var intro=Ui.Text("Defina as preferências antes de aplicar a barra.",13,true);intro.TextWrapping=TextWrapping.Wrap;root.Children.Add(intro);
        void Line(){root.Children.Add(new Border{Height=1,Background=Ui.Brush("#30FFFFFF"),Margin=new Thickness(0,13,0,11)});}
        void Label(string text){var t=Ui.Text(text,13);t.FontWeight=FontWeights.SemiBold;t.Margin=new Thickness(0,0,0,8);root.Children.Add(t);}
        RadioButton[] Choices(string group,string[] names,int selected,string[]? icons=null)
        {
            var row=new UniformGrid{Columns=names.Length};var choices=new RadioButton[names.Length];
            for(int i=0;i<names.Length;i++){
                var content=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};
                if(icons is not null){var icon=Ui.Icon(icons[i],25);icon.Margin=new Thickness(0,0,0,3);content.Children.Add(icon);}
                var text=Ui.Text(names[i],12);text.TextAlignment=TextAlignment.Center;content.Children.Add(text);
                var radio=new RadioButton{GroupName=group,Content=content,IsChecked=i==selected,Height=icons is null?36:58,Margin=new Thickness(2,0,2,0),Style=(Style)Application.Current.FindResource("SettingsChoice")};
                System.Windows.Automation.AutomationProperties.SetName(radio,names[i]);choices[i]=radio;row.Children.Add(radio);
            }
            root.Children.Add(row);return choices;
        }
        Line();Label("Posição da barra");
        var edges=Choices("DockEdge",new[]{"Superior","Inferior","Esquerda","Direita"},(int)current.Edge,new[]{
            "M2,4 L22,4 L22,20 L2,20 Z M5,7 L19,7", "M2,4 L22,4 L22,20 L2,20 Z M5,17 L19,17",
            "M4,2 L20,2 L20,22 L4,22 Z M7,5 L7,19", "M4,2 L20,2 L20,22 L4,22 Z M17,5 L17,19"});
        Line();Label("Tamanho da barra");var sizes=Choices("DockSize",new[]{"Pequena","Média","Grande"},(int)current.Size);
        var sizeNote=Ui.Text("",12,true);sizeNote.Margin=new Thickness(2,6,0,0);sizeNote.TextWrapping=TextWrapping.Wrap;root.Children.Add(sizeNote);
        void UpdateSizeNote(){int i=Array.FindIndex(sizes,r=>r.IsChecked==true);sizeNote.Text=i switch{1=>"56 DIPs · equilíbrio entre espaço e legibilidade",2=>"72 DIPs · ícones e controles maiores",_=>"42 DIPs · próximo à altura da barra padrão"};}
        foreach(var radio in sizes)radio.Checked+=(_,_)=>UpdateSizeNote();UpdateSizeNote();
        Line();Label("Redes e dispositivos por painel");var count=Choices("NetworkLimit",new[]{"4 dispositivos","5 dispositivos"},current.NetworkLimit==4?0:1);
        Line();var opacityHeader=new DockPanel{Margin=new Thickness(0,0,0,4)};
        var value=Ui.Text($"{current.GlassOpacity*100:0}%",13);DockPanel.SetDock(value,Dock.Right);opacityHeader.Children.Add(value);
        var opacityTitle=Ui.Text("Opacidade do vidro",13);opacityTitle.FontWeight=FontWeights.SemiBold;opacityHeader.Children.Add(opacityTitle);root.Children.Add(opacityHeader);
        var opacity=new Slider{Minimum=0,Maximum=65,Value=current.GlassOpacity*100,TickFrequency=1,IsSnapToTickEnabled=false,Height=26,Style=(Style)Application.Current.FindResource("GlassSlider")};root.Children.Add(opacity);
        opacity.ValueChanged+=(_,_)=>value.Text=$"{opacity.Value:0}%";
        var opacityNote=Ui.Text("0% = transparente · 65% = mais escuro",11,true);root.Children.Add(opacityNote);
        Line();var startup=new CheckBox{Content="Iniciar automaticamente ao entrar no Windows",IsChecked=current.AutoStart,Style=(Style)Application.Current.FindResource("SettingsCheck"),Foreground=Brushes.White,Margin=new Thickness(0,0,0,8)};root.Children.Add(startup);
        var note=Ui.Text("Mantenha o executável em uma pasta permanente. O Orla Dock não substitui a barra de tarefas do Windows.",11,true);note.TextWrapping=TextWrapping.Wrap;root.Children.Add(note);
        cancel.Click+=(_,_)=>DialogResult=false;
        apply.Click+=(_,_)=>{
            var next=new DockSettings{Edge=(DockEdge)Math.Max(0,Array.FindIndex(edges,r=>r.IsChecked==true)),Size=(DockSize)Math.Max(0,Array.FindIndex(sizes,r=>r.IsChecked==true)),NetworkLimit=count[0].IsChecked==true?4:5,GlassOpacity=opacity.Value/100,AutoStart=startup.IsChecked==true};
            try{next.ApplyStartup();next.Save();Result=next;DialogResult=true;}catch(Exception ex){MessageBox.Show(this,ex.Message,"Não foi possível salvar",MessageBoxButton.OK,MessageBoxImage.Warning);}
        };
    }
}
internal static class PopupPosition
{
    internal static PlacementMode Inward => App.Settings.Edge switch {DockEdge.Bottom=>PlacementMode.Top,DockEdge.Left=>PlacementMode.Right,DockEdge.Right=>PlacementMode.Left,_=>PlacementMode.Bottom};
    internal static void Apply(Popup popup){popup.Placement=Inward;popup.HorizontalOffset=App.Settings.Edge switch{DockEdge.Left=>8,DockEdge.Right=>-8,_=>0};popup.VerticalOffset=App.Settings.Edge switch{DockEdge.Top=>8,DockEdge.Bottom=>-8,_=>0};}
}
