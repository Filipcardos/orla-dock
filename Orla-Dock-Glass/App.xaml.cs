using System.Windows;
using Microsoft.Win32;

namespace OrlaDock;
public partial class App : Application
{
    internal static DockSettings Settings {get;private set;}=new();
    private GlassWindow? dock, controls;
    private System.Threading.Mutex? instance;
    private bool configuring, positioning;
    private AppBarReservation? reservation;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance=new System.Threading.Mutex(true,"Local\\OrlaDockGlass",out bool first);
        if(!first){if(!e.Args.Contains("--startup"))MessageBox.Show("O Orla Dock já está aberto. Clique com o botão direito na barra e escolha Configurações.","Orla Dock");Shutdown();return;}
        var saved=DockSettings.Load();Settings=saved??new();
        if(e.Args.Contains("--startup")){if(saved is null||!Settings.AutoStart){Shutdown();return;}}
        else if(!ConfigureInitial()){Shutdown();return;}
        var dockContent=new DockView();var controlsContent=new ControlsView();
        dock=new GlassWindow(dockContent);controls=new GlassWindow(controlsContent);
        MainWindow=dock;dockContent.DockWidthChanged+=(_,_)=>Position();
        SystemEvents.DisplaySettingsChanged+=OnDisplayChanged;SystemEvents.UserPreferenceChanged+=OnPreferenceChanged;
        try{reservation=new AppBarReservation(Position,OnFullscreenChanged);}catch{reservation=null;}
        Position();dock.Show();controls.Show();Position();
        if(reservation?.IsFullscreen==true)OnFullscreenChanged(true);
    }
    private bool ConfigureInitial(){var window=new SettingsWindow(Settings);if(window.ShowDialog()!=true||window.Result is null)return false;Settings=window.Result;return true;}
    internal void Configure(){if(configuring)return;configuring=true;try{((DockView?)dock?.ContentView)?.CloseCalendar();((ControlsView?)controls?.ContentView)?.ClosePanel();var window=new SettingsWindow(Settings);if(window.ShowDialog()==true&&window.Result is not null){Settings=window.Result;Position();dock?.ApplyGlass();controls?.ApplyGlass();}}finally{configuring=false;}}
    private void OnDisplayChanged(object? s,EventArgs e)=>Dispatcher.BeginInvoke(new Action(()=>{((DockView?)dock?.ContentView)?.CloseCalendar();((ControlsView?)controls?.ContentView)?.ClosePanel();Position();}));
    private void OnPreferenceChanged(object? s,UserPreferenceChangedEventArgs e)=>Dispatcher.BeginInvoke(new Action(()=>{dock?.ApplyGlass();controls?.ApplyGlass();Position();}));
    private void OnFullscreenChanged(bool fullscreen)
    {
        if(dock is null||controls is null)return;
        if(fullscreen){((DockView)dock.ContentView).CloseCalendar();((ControlsView)controls.ContentView).ClosePanel();}
        AppBarReservation.SetFullscreenZOrder(dock,fullscreen);AppBarReservation.SetFullscreenZOrder(controls,fullscreen);
    }
    private void Position()
    {
        if(dock is null||controls is null||positioning)return;
        positioning=true;try{
        Rect area=SystemParameters.WorkArea;var dv=(DockView)dock.ContentView;var cv=(ControlsView)controls.ContentView;
        bool vertical=Settings.Edge is DockEdge.Left or DockEdge.Right;dv.SetVertical(vertical);cv.SetVertical(vertical);
        double scale=Settings.Scale;dock.ApplyScale(scale);controls.ApplyScale(scale);double gap=8*scale,margin=8;
        area=reservation?.Reserve(Settings.Edge,(vertical?112:72)*scale+2*margin)??SystemParameters.WorkArea;
        if(vertical){dock.Width=controls.Width=112*scale;dock.Height=dv.DesiredDockHeight*scale;controls.Height=168*scale;double total=dock.Height+gap+controls.Height;double y=area.Top+Math.Max(margin,(area.Height-total)/2);dock.Top=y;controls.Top=y+dock.Height+gap;dock.Left=controls.Left=Settings.Edge==DockEdge.Left?area.Left+margin:area.Right-margin-dock.Width;}
        else {dock.Height=controls.Height=72*scale;controls.Width=284*scale;dock.Width=dv.DesiredDockWidth*scale;double total=dock.Width+gap+controls.Width;double x=area.Left+Math.Max(margin,(area.Width-total)/2);dock.Left=x;controls.Left=x+dock.Width+gap;dock.Top=controls.Top=Settings.Edge==DockEdge.Top?area.Top+margin:area.Bottom-margin-dock.Height;}
        }finally{positioning=false;}
    }
    protected override void OnExit(ExitEventArgs e){SystemEvents.DisplaySettingsChanged-=OnDisplayChanged;SystemEvents.UserPreferenceChanged-=OnPreferenceChanged;reservation?.Dispose();reservation=null;instance?.Dispose();base.OnExit(e);}
}
