using System.Management;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using Windows.Devices.Radios;

namespace OrlaDock;
internal sealed class ControlsView : Grid
{
    private readonly Button wifi, bluetooth, sound, sun, battery;
    private readonly Slider slider;
    private readonly Grid controls, level;
    private readonly WrapPanel buttons;
    private readonly Border divider;
    private bool verticalLayout;
    private readonly TextBlock percent=Ui.Text("—",12);
    private readonly Rectangle batteryFill=new(){Height=10,Width=0,RadiusX=2,RadiusY=2,Fill=Ui.Brush("#D6E1E9")};
    private readonly Viewbox modeIcon;
    private readonly DispatcherTimer timer, writeTimer;
    private readonly MMDeviceEnumerator? audio;
    private bool updating, refreshing, brightnessMode, brightnessAvailable, radioBusy, writing;
    private bool volumeAvailable;
    private RadioAccessStatus? radioAccess;
    private Radio? wifiRadio, bluetoothRadio;
    private int lastBrightness=50;
    private int? pendingBrightness;
    private int tick;
    internal ControlsView()
    {
        ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(186)});ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1)});ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        controls=new Grid{VerticalAlignment=VerticalAlignment.Center};controls.RowDefinitions.Add(new RowDefinition{Height=new GridLength(33)});controls.RowDefinitions.Add(new RowDefinition{Height=new GridLength(24)});
        buttons=new WrapPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};
        wifi=Ui.Button(Ui.Icon(Ui.Wifi),"Wi-Fi: redes disponíveis / botão direito: configurações",38,30);
        bluetooth=Ui.Button(Ui.Icon(Ui.Bluetooth),"Bluetooth: dispositivos / botão direito: configurações",38,30);
        sound=Ui.Button(Ui.Icon(Ui.Speaker),"Volume / botão direito: silenciar",38,30);
        sun=Ui.Button(Ui.Icon(Ui.Sun),"Brilho da tela",38,30);
        foreach(var b in new[]{wifi,bluetooth,sound,sun}) buttons.Children.Add(b);controls.Children.Add(buttons);
        level=new Grid{Margin=new Thickness(6,0,6,0)};level.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(23)});level.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        modeIcon=Ui.Icon(Ui.Speaker,15);level.Children.Add(modeIcon);
        slider=new Slider{Minimum=0,Maximum=100,SmallChange=1,LargeChange=5,Style=(Style)Application.Current.FindResource("GlassSlider"),VerticalAlignment=VerticalAlignment.Center,IsMoveToPointEnabled=true,ToolTip="Volume"};Grid.SetColumn(slider,1);level.Children.Add(slider);Grid.SetRow(level,1);controls.Children.Add(level);Children.Add(controls);
        divider=new Border{Background=Ui.Brush("#45FFFFFF"),Width=1,Height=42,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(divider,1);Children.Add(divider);
        var batteryCanvas=new Canvas{Width=30,Height=20};var outline=new Rectangle{Width=26,Height=15,RadiusX=3,RadiusY=3,Stroke=Ui.Brush("#E8F0F6"),StrokeThickness=1.6};Canvas.SetTop(outline,2);batteryCanvas.Children.Add(outline);Canvas.SetLeft(batteryFill,3);Canvas.SetTop(batteryFill,4.5);batteryCanvas.Children.Add(batteryFill);var cap=new Rectangle{Width=3,Height=6,RadiusX=1,RadiusY=1,Fill=Ui.Brush("#E8F0F6")};Canvas.SetLeft(cap,27);Canvas.SetTop(cap,6.5);batteryCanvas.Children.Add(cap);
        var batteryPanel=new StackPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};batteryPanel.Children.Add(batteryCanvas);percent.TextAlignment=TextAlignment.Center;batteryPanel.Children.Add(percent);
        battery=Ui.Button(batteryPanel,"Bateria e energia",63,53);battery.Background=Brushes.Transparent;battery.BorderBrush=Brushes.Transparent;Grid.SetColumn(battery,2);Children.Add(battery);battery.Click+=(_,_)=>NativeApps.Open("ms-settings:powersleep");
        try{audio=new MMDeviceEnumerator();}catch{}
        wifi.Click+=(_,_)=>ShowRadioPanel(wifi,RadioKind.WiFi);bluetooth.Click+=(_,_)=>ShowRadioPanel(bluetooth,RadioKind.Bluetooth);
        wifi.MouseRightButtonUp+=(_,e)=>{e.Handled=true;NativeApps.Open("ms-settings:network-wifi");};bluetooth.MouseRightButtonUp+=(_,e)=>{e.Handled=true;NativeApps.Open("ms-settings:bluetooth");};
        sound.Click+=(_,_)=>SetMode(false);sound.MouseRightButtonUp+=(_,e)=>{e.Handled=true;ToggleMute();};sun.Click+=(_,_)=>SetMode(true);sun.MouseRightButtonUp+=(_,e)=>{e.Handled=true;NativeApps.Open("ms-settings:display");};
        writeTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(90)};writeTimer.Tick+=async(_,_)=>{writeTimer.Stop();await ApplyBrightness();};
        slider.ValueChanged+=(_,_)=>{if(updating)return;if(brightnessMode){pendingBrightness=(int)Math.Round(slider.Value);writeTimer.Stop();writeTimer.Start();}else SetVolume((float)(slider.Value/100));};
        timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};timer.Tick+=async(_,_)=>await Refresh();
        Loaded+=async(_,_)=>{await Refresh();timer.Start();};Unloaded+=(_,_)=>{timer.Stop();writeTimer.Stop();audio?.Dispose();};
        SetMode(false);UpdateBattery();
    }
    internal void ClosePanel()=>panel?.Close();
    internal void SetVertical(bool vertical)
    {
        if(verticalLayout==vertical)return;verticalLayout=vertical;
        ColumnDefinitions.Clear();RowDefinitions.Clear();
        controls.RowDefinitions[0].Height=new GridLength(vertical?60:33);
        buttons.Width=vertical?88:176;buttons.HorizontalAlignment=HorizontalAlignment.Center;
        if(vertical){ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});RowDefinitions.Add(new RowDefinition{Height=new GridLength(88)});RowDefinitions.Add(new RowDefinition{Height=new GridLength(1)});RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});Grid.SetColumn(controls,0);Grid.SetRow(controls,0);Grid.SetColumn(divider,0);Grid.SetRow(divider,1);divider.Width=70;divider.Height=1;Grid.SetColumn(battery,0);Grid.SetRow(battery,2);}
        else{ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(186)});ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1)});ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});Grid.SetRow(controls,0);Grid.SetColumn(controls,0);Grid.SetRow(divider,0);Grid.SetColumn(divider,1);divider.Width=1;divider.Height=42;Grid.SetRow(battery,0);Grid.SetColumn(battery,2);}
    }
    private void SetMode(bool bright)
    {
        brightnessMode=bright;var next=Ui.Icon(bright?Ui.Sun:Ui.Speaker,15);var child=next.Child;next.Child=null;modeIcon.Child=child;
        slider.ToolTip=bright?"Brilho":"Volume";sound.Background=Ui.Brush(bright?"#16FFFFFF":"#30FFFFFF");sun.Background=Ui.Brush(bright?"#30FFFFFF":"#16FFFFFF");
        if(bright&&!brightnessAvailable){slider.IsEnabled=false;NativeApps.Open("ms-settings:display");return;} if(bright){slider.IsEnabled=true;SetLevel(lastBrightness);}else UpdateAudio();
    }
    private void SetLevel(double value){updating=true;try{if(!slider.IsMouseCaptureWithin)slider.Value=Math.Clamp(value,0,100);}finally{updating=false;}}
    private void UpdateAudio()
    {
        try{using var endpoint=audio?.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);if(endpoint is null)throw new InvalidOperationException();var vol=endpoint.AudioEndpointVolume;volumeAvailable=true;sound.Content=Ui.Icon(vol.Mute?Ui.Mute:Ui.Speaker);if(!brightnessMode){slider.IsEnabled=true;SetLevel(vol.MasterVolumeLevelScalar*100);}}catch{volumeAvailable=false;if(!brightnessMode)slider.IsEnabled=false;}
    }
    private void SetVolume(float volume){if(!volumeAvailable)return;try{using var d=audio?.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);if(d is not null){d.AudioEndpointVolume.MasterVolumeLevelScalar=Math.Clamp(volume,0,1);if(volume>0)d.AudioEndpointVolume.Mute=false;}}catch{UpdateAudio();}}
    private void ToggleMute(){try{using var d=audio?.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);if(d is not null)d.AudioEndpointVolume.Mute=!d.AudioEndpointVolume.Mute;}catch{}UpdateAudio();}
    private async Task Refresh()
    {
        if(refreshing)return;refreshing=true;
        try{UpdateAudio();UpdateBattery();if(!radioBusy)await ReadRadios();if(tick++%3==0&&!writing&&pendingBrightness is null){int? brightness=await Task.Run(ReadBrightness);brightnessAvailable=brightness.HasValue;if(brightness.HasValue)lastBrightness=brightness.Value;if(brightnessMode){slider.IsEnabled=brightnessAvailable;SetLevel(lastBrightness);}}}finally{refreshing=false;}
    }
    private async Task ReadRadios()
    {
        try{var radios=await Radio.GetRadiosAsync();wifiRadio=radios.FirstOrDefault(r=>r.Kind==RadioKind.WiFi);bluetoothRadio=radios.FirstOrDefault(r=>r.Kind==RadioKind.Bluetooth);}catch{wifiRadio=null;bluetoothRadio=null;}
        RadioAppearance(wifi,wifiRadio,"Wi-Fi");RadioAppearance(bluetooth,bluetoothRadio,"Bluetooth");
    }
    private static void RadioAppearance(Button b,Radio? radio,string label){bool on=radio?.State==RadioState.On;b.Background=Ui.Brush(on?"#33FFFFFF":"#0DFFFFFF");b.Opacity=on?1:0.65;b.ToolTip=$"{label}: {(radio is null?"não disponível":radio.State.ToString())} — clique para abrir painel; botão direito para configurações";}
    private RadioPanel? panel;
    private void ShowRadioPanel(Button anchor, RadioKind kind)
    {
        if(panel?.IsOpen==true && panel.Kind==kind){panel.Close();return;}
        panel?.Close();
        panel=new RadioPanel(anchor,kind,()=>ToggleRadio(kind),()=>kind==RadioKind.WiFi?wifiRadio:bluetoothRadio);
        panel.Open();
    }
    private async Task ToggleRadio(RadioKind kind)
    {
        if(radioBusy)return;radioBusy=true;var setting=kind==RadioKind.WiFi?"ms-settings:network-wifi":"ms-settings:bluetooth";
        try{radioAccess??=await Radio.RequestAccessAsync();var r=kind==RadioKind.WiFi?wifiRadio:bluetoothRadio;if(radioAccess!=RadioAccessStatus.Allowed||r is null){NativeApps.Open(setting);return;}var result=await r.SetStateAsync(r.State==RadioState.On?RadioState.Off:RadioState.On);if(result!=RadioAccessStatus.Allowed)NativeApps.Open(setting);}catch{NativeApps.Open(setting);}finally{await ReadRadios();radioBusy=false;}
    }
    private void UpdateBattery(){if(!GetSystemPowerStatus(out var status)||status.BatteryLifePercent==255||(status.BatteryFlag&128)!=0){percent.Text="—";batteryFill.Width=0;battery.ToolTip="Sem bateria detectada";return;}int value=status.BatteryLifePercent;percent.Text=$"{value}%";batteryFill.Width=20*value/100.0;battery.ToolTip=$"Bateria {value}% — {(status.ACLineStatus==1?"conectado à energia":"na bateria")}";}
    private static int? ReadBrightness(){try{using var search=new ManagementObjectSearcher(@"root\WMI","SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active = TRUE");using var result=search.Get();foreach(ManagementObject item in result){using(item)return Convert.ToInt32(item["CurrentBrightness"]);}}catch{}return null;}
    private async Task ApplyBrightness(){if(writing||pendingBrightness is null)return;int value=pendingBrightness.Value;pendingBrightness=null;writing=true;try{bool ok=await Task.Run(()=>WriteBrightness(value));if(ok)lastBrightness=value;else{brightnessAvailable=false;if(brightnessMode)slider.IsEnabled=false;}}finally{writing=false;if(pendingBrightness.HasValue)writeTimer.Start();}}
    private static bool WriteBrightness(int value){try{using var search=new ManagementObjectSearcher(@"root\WMI","SELECT * FROM WmiMonitorBrightnessMethods WHERE Active = TRUE");using var result=search.Get();bool any=false;foreach(ManagementObject item in result){using(item){item.InvokeMethod("WmiSetBrightness",new object[]{1u,(byte)Math.Clamp(value,0,100)});any=true;}}return any;}catch{return false;}}
    [StructLayout(LayoutKind.Sequential)]private struct PowerStatus{public byte ACLineStatus,BatteryFlag,BatteryLifePercent,SystemStatusFlag;public uint BatteryLifeTime,BatteryFullLifeTime;}
    [DllImport("kernel32.dll")]private static extern bool GetSystemPowerStatus(out PowerStatus status);
}
