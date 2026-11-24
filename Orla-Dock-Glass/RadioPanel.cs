using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Windows.Devices.Radios;
using Windows.Devices.WiFi;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth;
using Windows.Security.Credentials;
using Windows.Networking.Connectivity;

namespace OrlaDock;
internal sealed class RadioPanel
{
    internal RadioKind Kind { get; }
    private readonly Popup popup;
    private readonly StackPanel list = new();
    private readonly TextBlock status=Ui.Text("",12);
    private readonly Button toggle, refresh;
    private readonly Func<Task> toggleRadio;
    private readonly Func<Radio?> radio;
    private readonly System.Windows.Threading.Dispatcher dispatcher;
    private readonly Dictionary<string,DeviceInformation> devices=new();
    private readonly List<DeviceWatcher> watchers=new();
    private bool busy;
    private int generation;
    private string Settings => Kind==RadioKind.WiFi?"ms-settings:network-wifi":"ms-settings:bluetooth";
    internal bool IsOpen => popup.IsOpen;
    internal RadioPanel(Button anchor,RadioKind kind,Func<Task> toggleRadio,Func<Radio?> radio)
    {
        Kind=kind;this.toggleRadio=toggleRadio;this.radio=radio;dispatcher=anchor.Dispatcher;
        var root=new StackPanel();
        var title=Ui.Text(kind==RadioKind.WiFi?"Wi-Fi":"Bluetooth",19);title.FontWeight=FontWeights.SemiBold;
        var header=new DockPanel{Margin=new Thickness(0,0,0,12)};
        toggle=Ui.Button("Alternar","Ativar ou desativar",94,30);DockPanel.SetDock(toggle,Dock.Right);header.Children.Add(toggle);header.Children.Add(title);root.Children.Add(header);
        toggle.Click+=async(_,_)=>{if(busy)return;await toggleRadio();await Load();};
        status.TextWrapping=TextWrapping.Wrap;status.Margin=new Thickness(0,0,0,9);root.Children.Add(status);
        root.Children.Add(new ScrollViewer{MaxHeight=270,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=list});
        var footer=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,12,0,0)};
        refresh=Ui.Button("Atualizar","Buscar novamente",91,30);refresh.Click+=async(_,_)=>await Load();footer.Children.Add(refresh);
        var settings=Ui.Button("Configurações do Windows","Abrir controles nativos",178,30);settings.Click+=(_,_)=>{Close();NativeApps.Open(Settings);};footer.Children.Add(settings);root.Children.Add(footer);
        var frame=new Border{Width=310,Padding=new Thickness(14),CornerRadius=new CornerRadius(19),BorderThickness=new Thickness(1),BorderBrush=Ui.Brush("#66FFFFFF"),Background=Ui.Brush("#EB151B23"),Child=root};
        popup=new Popup{PlacementTarget=anchor,Placement=PlacementMode.Bottom,VerticalOffset=12,HorizontalOffset=-100,AllowsTransparency=true,StaysOpen=false,Child=frame,PopupAnimation=PopupAnimation.Fade};
        PopupPosition.Apply(popup);
        popup.Closed+=(_,_)=>{generation++;StopWatchers();};
    }
    internal async void Open(){popup.IsOpen=true;await Load();}
    internal void Close()=>popup.IsOpen=false;
    private void StopWatchers(){foreach(var w in watchers){try{if(w.Status==DeviceWatcherStatus.Started||w.Status==DeviceWatcherStatus.EnumerationCompleted)w.Stop();}catch{}}watchers.Clear();}
    private async Task Load()
    {
        if(busy||!IsOpen)return;busy=true;refresh.IsEnabled=false;int stamp=++generation;StopWatchers();devices.Clear();list.Children.Clear();
        toggle.Content=radio()?.State==RadioState.On?"Desativar":"Ativar";
        status.Text="Buscando…";
        try{
            if(radio()?.State==RadioState.Off){status.Text="Desativado. Ative para buscar.";return;}
            if(Kind==RadioKind.WiFi)await LoadWifi(stamp);else LoadBluetooth(stamp);
        }catch(Exception){if(stamp==generation)status.Text="A busca não está disponível. Verifique permissões e configurações do Windows.";}
        finally{busy=false;refresh.IsEnabled=true;}
    }
    private async Task LoadWifi(int stamp)
    {
        var access=await WiFiAdapter.RequestAccessAsync();
        if(stamp!=generation)return;
        if(access!=WiFiAccessStatus.Allowed){status.Text="Permissão de Wi-Fi indisponível. O Windows pode exigir acesso à localização para listar redes.";var location=Ui.Button("Permissões de localização","Abrir privacidade do Windows",250,32);location.Click+=(_,_)=>{Close();NativeApps.Open("ms-settings:privacy-location");};list.Children.Add(location);return;}
        var adapters=await WiFiAdapter.FindAllAdaptersAsync();
        if(stamp!=generation)return;
        if(adapters.Count==0){status.Text="Nenhum adaptador Wi-Fi encontrado.";return;}
        var adapter=adapters[0];await adapter.ScanAsync();if(stamp!=generation)return;
        string? connected=(await adapter.NetworkAdapter.GetConnectedProfileAsync())?.ProfileName;
        if(stamp!=generation)return;
        var networks=adapter.NetworkReport.AvailableNetworks.Where(n=>!string.IsNullOrWhiteSpace(n.Ssid)).GroupBy(n=>n.Ssid).Select(g=>g.OrderByDescending(n=>n.SignalBars).First()).OrderByDescending(n=>n.SignalBars).ThenByDescending(n=>n.Ssid==connected).Take(App.Settings.NetworkLimit).ToList();
        status.Text=networks.Count==0?"Nenhuma rede encontrada.":"Redes com melhor sinal. Selecione para conectar.";
        foreach(var network in networks){var row=Row(network.Ssid,network.Ssid==connected?"Conectado":$"Sinal: {network.SignalBars}/5 · {network.SecuritySettings.NetworkAuthenticationType}");list.Children.Add(row);row.Click+=(_,_)=>{if(network.Ssid==connected){adapter.Disconnect();_ = Load();}else ShowConnect(adapter,network);};}
    }
    private void ShowConnect(WiFiAdapter adapter,WiFiAvailableNetwork network)
    {
        list.Children.Clear();status.Text=$"Conectar a {network.Ssid}";
        var password=new PasswordBox{Margin=new Thickness(0,5,0,8),Padding=new Thickness(6)};
        bool open=network.SecuritySettings.NetworkEncryptionType==NetworkEncryptionType.None;
        if(!open){list.Children.Add(Ui.Text("Senha da rede",12));list.Children.Add(password);}
        var connect=Ui.Button("Conectar","Conectar à rede",130,32);list.Children.Add(connect);
        connect.Click+=async(_,_)=>{connect.IsEnabled=false;try{var result=open?await adapter.ConnectAsync(network,WiFiReconnectionKind.Automatic):await adapter.ConnectAsync(network,WiFiReconnectionKind.Automatic,new PasswordCredential{Password=password.Password});password.Clear();status.Text=result.ConnectionStatus==WiFiConnectionStatus.Success?"Conectado.":$"Não foi possível conectar: {result.ConnectionStatus}";if(result.ConnectionStatus==WiFiConnectionStatus.Success)await Load();}catch{status.Text="Falha na conexão. Use as configurações do Windows.";}finally{connect.IsEnabled=true;}};
    }
    private void LoadBluetooth(int stamp)
    {
        status.Text="Buscando dispositivos próximos e pareados…";
        foreach(var selector in new[]{BluetoothDevice.GetDeviceSelectorFromPairingState(true),BluetoothDevice.GetDeviceSelectorFromPairingState(false),BluetoothLEDevice.GetDeviceSelectorFromPairingState(true),BluetoothLEDevice.GetDeviceSelectorFromPairingState(false)}){
            var watcher=DeviceInformation.CreateWatcher(selector,new[]{"System.Devices.Aep.SignalStrength","System.Devices.Aep.IsConnected","System.Devices.Aep.DeviceAddress"},DeviceInformationKind.AssociationEndpoint);
            watcher.Added+=(w,d)=>dispatcher.BeginInvoke(new Action(()=>{if(stamp!=generation)return;devices[d.Id]=d;RenderBluetooth();}));
            watcher.Updated+=(w,d)=>dispatcher.BeginInvoke(new Action(()=>{if(stamp!=generation)return;if(devices.TryGetValue(d.Id,out var item)){item.Update(d);RenderBluetooth();}}));
            watcher.Removed+=(w,d)=>dispatcher.BeginInvoke(new Action(()=>{if(stamp!=generation)return;devices.Remove(d.Id);RenderBluetooth();}));
            watcher.EnumerationCompleted+=(w,e)=>dispatcher.BeginInvoke(new Action(()=>{if(stamp==generation)RenderBluetooth();}));
            watchers.Add(watcher);watcher.Start();
        }
    }
    private static int? Signal(DeviceInformation d){try{if(d.Properties.TryGetValue("System.Devices.Aep.SignalStrength",out var v)&&v is not null)return Convert.ToInt32(v);}catch{}return null;}
    private static string DeviceKey(DeviceInformation d)=>d.Properties.TryGetValue("System.Devices.Aep.DeviceAddress",out var v)&&v is string address&&!string.IsNullOrWhiteSpace(address)?address:d.Id;
    private void RenderBluetooth()
    {
        list.Children.Clear();var items=devices.Values.Where(d=>!string.IsNullOrWhiteSpace(d.Name)).GroupBy(DeviceKey,StringComparer.OrdinalIgnoreCase).Select(g=>g.OrderByDescending(d=>Signal(d)??int.MinValue).ThenByDescending(d=>d.Pairing.IsPaired).First()).OrderByDescending(d=>Signal(d)??int.MinValue).ThenByDescending(d=>d.Pairing.IsPaired).ThenBy(d=>d.Name).Take(App.Settings.NetworkLimit).ToList();
        status.Text=items.Count==0?"Aguardando dispositivos detectáveis…":"Melhor sinal disponível primeiro; sem sinal informado, prioriza pareados.";
        foreach(var device in items){var row=Row(device.Name,(device.Pairing.IsPaired?"Pareado · gerenciar":"Disponível · parear")+(Signal(device) is int db?$" · {db} dBm":""));list.Children.Add(row);row.Click+=async(_,_)=>{if(device.Pairing.IsPaired){Close();NativeApps.Open(Settings);return;}row.IsEnabled=false;try{var result=await device.Pairing.PairAsync();if(IsOpen)status.Text=$"Pareamento: {result.Status}";}catch{if(IsOpen)status.Text="Use Configurações para concluir o pareamento.";}finally{row.IsEnabled=true;}};}
    }
    private static Button Row(string name,string description)
    {
        var stack=new StackPanel{Margin=new Thickness(4)};var primary=Ui.Text(name,14);primary.TextTrimming=TextTrimming.CharacterEllipsis;stack.Children.Add(primary);stack.Children.Add(Ui.Text(description,11,true));
        var button=Ui.Button(stack,name,270,48);button.Margin=new Thickness(0,3,0,3);return button;
    }
}
