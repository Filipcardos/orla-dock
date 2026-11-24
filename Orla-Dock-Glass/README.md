> Versão 1.6: consulte ATUALIZACAO-V1.6.md. O dock agora reserva espaço na área útil do monitor principal e não é sempre visível sobre outras janelas.

> Versão 1.4: consulte ATUALIZACAO-V1.4.md. A descrição atual abaixo substitui detalhes anteriores.

> Versão 1.3: consulte ATUALIZACAO-V1.3.md.

> Versão 1.2: consulte ATUALIZACAO-V1.2.md para instalação e novos recursos.

# Orla Dock — Glass Edition

Implementação independente para Windows 11 x64. Não substitui nem altera automaticamente o projeto anterior no Codex. A arquitetura C# / .NET 8 / WPF foi escolhida por oferecer integração nativa com o desktop e manter uma base pequena.

## Interface

- Dois módulos flutuantes, na borda escolhida do monitor principal, com tamanho pequeno (42 DIPs), médio (56 DIPs) ou grande (72 DIPs), selecionado na configuração inicial.
- Transparência por pixel com gradiente neutro e bordas sutis; opacidade do dock ajustável. O calendário possui um gradiente translúcido próprio para legibilidade, sem desfoque real do desktop.
- Reflexos neutros, sem luz neon, logotipo vetorial do Windows em vidro, sem texto da marca.
- Ícones dos executáveis via Shell Image Factory (solicitação de até 128 px; tamanho real depende dos recursos do aplicativo). Ícones vetoriais nos controles.
- Transições de 140–180 ms com `Timeline.DesiredFrameRate=144` como objetivo. A taxa real depende de WPF, GPU, DWM, carga e monitor. Não há promessa de 144 FPS medidos.
- DPI PerMonitorV2, espaçamento em unidades independentes de DPI. O posicionamento usa a área útil do monitor primário.

## Funcionalidades e uso

| Elemento | Ação |
|---|---|
| Windows | Abre o menu Iniciar via tecla Windows |
| Aplicativos abertos | Exibe uma entrada por executável com janela detectada, incluindo minimizadas; sem atalhos fixos. Clique ativa a janela; botão direito abre nova janela. |
| Relógio | Hora/data reais, pt-BR; clique abre calendário Glass alinhado ao relógio, com navegação por mês |
| Wi-Fi e Bluetooth | Painéis com até 4 ou 5 redes ou dispositivos, controle de rádio e conexão quando autorizado; fallback para Configurações. Botão direito abre Configurações diretamente |
| Volume | Seleciona controle deslizante de volume real via Core Audio; botão direito alterna mudo |
| Brilho | Seleciona controle de brilho via WMI para tela integrada compatível; fallback para Configurações |
| Bateria | Percentual e estado AC reais via GetSystemPowerStatus; clique abre Energia |
| Sair | Botão direito em uma área livre do módulo, depois Sair |

Não desativa a barra de tarefas do Windows. A opção de inicialização automática utiliza a chave Run do usuário atual e não exige elevação. Sem monitoramento de arquivos pessoais ou telemetria implementada no aplicativo.

## Compilar e executar

Pré-requisitos: Windows 11 x64, SDK .NET 8 instalado e acesso ao NuGet no primeiro restore. O SDK Windows é resolvido pelo target framework .NET. Dependências: NAudio 2.2.1 e System.Management 8.0.0.

No PowerShell, dentro desta pasta:

```powershell
dotnet restore .\OrlaDock.csproj
dotnet build .\OrlaDock.csproj -c Release
dotnet run --project .\OrlaDock.csproj -c Release
```

Para gerar versão autossuficiente (pasta com o EXE e DLLs):

```powershell
dotnet publish .\OrlaDock.csproj -c Release -r win-x64 --self-contained true -o .\publish
```

O executável estará em `publish\OrlaDock.exe` se o publish terminar com sucesso. Não foi incluído um EXE pré-compilado.

## Checklist de validação no Windows

- [ ] `dotnet restore`, `dotnet build` e `dotnet publish` sem erros.
- [ ] Ativar/desativar transparência do Windows e conferir contraste, bordas e fundo.
- [ ] Testar hover e clique a 144 Hz; medir frametimes com ferramenta de profiling adequada. Meta por quadro: ~6,94 ms. Comparar uso de CPU/GPU ocioso e durante animação.
- [ ] Testar escala de 100%, 125% e 150%, Full HD e diferentes resoluções.
- [ ] Abrir e fechar Explorador, Edge e outros aplicativos; conferir indicadores e ativação de janela.
- [ ] Alterar volume pelo dock e pelo Windows; testar mudo e troca de dispositivo de saída.
- [ ] Testar Wi-Fi e Bluetooth em hardware real, incluindo permissões negadas. Desligar Wi-Fi pode desconectar a sessão atual; testar localmente.
- [ ] Testar brilho da tela integrada, suspensão/retomada e percentual de bateria.
- [ ] Confirmar encerramento pelo menu de contexto.

## Limitações conhecidas

Este pacote foi produzido em ambiente Linux sem SDK .NET/Windows; não foi compilado nem executado em Windows. O XAML e o XML foram verificados estruturalmente. Esta versão usa polling para estados e janelas (1–6 segundos), e não animações dirigidas por polling. A compatibilidade de Acrylic, controle de rádios e WMI depende do sistema e dos drivers; monitores externos podem não aceitar brilho via WMI. Aplicativos UWP/empacotados ou de processos protegidos podem não ser listados. A ativação de janelas está sujeita às regras de foreground do Windows. A janela principal pode ter contorno menos arredondado que o mockup, pois usa o recorte nativo do DWM. Não é um substituto completo da barra de tarefas.

## Referência visual

Veja `design-reference.png` (mockup aprovado como direção visual; os elementos do aplicativo são controles nativos, não uma imagem de fundo).

## Referências técnicas

- https://learn.microsoft.com/windows/win32/api/dwmapi/ne-dwmapi-dwm_systembackdrop_type
- https://learn.microsoft.com/uwp/api/windows.devices.radios.radio
- https://learn.microsoft.com/uwp/api/windows.devices.radios.radio.requestaccessasync
- https://github.com/naudio/NAudio
