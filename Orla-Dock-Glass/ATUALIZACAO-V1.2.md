# Orla Dock Glass 1.2

- Configuração antes de mostrar a barra, salvo no perfil do usuário. A inicialização automática é opcional e usa HKCU Run; a execução com --startup respeita a configuração salva e não abre a tela de ajustes. Para alterar, clique com botão direito na superfície da barra e escolha Configurações.
- Quatro posições na área útil do monitor principal. Nas laterais, os ícones permanecem em pé e os controles são reorganizados em duas colunas. Os popups abrem em direção ao centro da tela.
- Opacidade ajustável de 0 a 65%, padrão 22%. Sem desfoque real do desktop.
- Wi-Fi: 4 ou 5 SSIDs únicos com maior quantidade de barras de sinal, usando o primeiro adaptador disponível. Bluetooth: 4 ou 5 dispositivos, ordenados por RSSI se o Windows disponibilizar; caso contrário, pareados primeiro. Dispositivos sem nome são omitidos.
- Calendário mensal navegável em português ao clicar no relógio, com botão Hoje.
- Ícones de janelas via WM_GETICON com timeout e fallback Shell 128 px. Nem todo aplicativo, especialmente apps empacotados, expõe um ícone ou caminho utilizável. A lista continua limitada aos atalhos Explorer/Edge e até dois aplicativos abertos da versão anterior.
- Não substitui nem oculta a barra de tarefas do Windows.

## Compilar e publicar

Em PowerShell, dentro de Orla-Dock-Glass:

```powershell
dotnet restore
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
.\publish\OrlaDock.exe
```

Para inicialização automática, mantenha a pasta publicada em local permanente e habilite a opção na janela de configuração. Para desabilitar, desmarque a opção e aplique antes de remover o aplicativo.

## Validação pendente no Windows

Este pacote contém código-fonte, não executável compilado. Verificar as quatro posições, abrir Wi-Fi/Bluetooth/calendário em cada posição, alternar opacidade, confirmar persistência e inicialização, conferir ícones e estabilidade de áudio/brilho. Wi-Fi pode exigir permissões de localização; Bluetooth depende do suporte do driver. A meta de 144 Hz não foi medida.
