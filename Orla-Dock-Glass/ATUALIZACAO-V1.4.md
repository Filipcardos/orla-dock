# Orla Dock Glass 1.4

- Calendário personalizado, sem componente Calendar padrão do WPF (elimina a ambiguidade CS0104 da v1.3).
- Grade com semanas de domingo a sábado, navegação mensal, seleção de datas e botão Hoje. Fundo translúcido, sem área branca, borda e destaque da data atual.
- Popup com posicionamento personalizado centralizado no relógio e abertura para dentro da tela. WPF pode ajustar a posição próximo às bordas. Fecha ao mudar o conjunto de apps ou a resolução para evitar desalinhamento.
- Escolha de tamanho na configuração inicial do executável: pequena (42 DIPs), média (56 DIPs), grande (72 DIPs). A escala afeta ambos os módulos; os popups mantêm tamanho legível. Nas laterais, a largura do módulo é proporcional. O tamanho físico depende da escala DPI.
- Preferências anteriores preservadas no arquivo settings-v12.json; tamanho padrão pequeno caso não exista preferência de tamanho. A inicialização automática continua opcional e não mostra a configuração a cada login. Ao abrir manualmente, a configuração é apresentada. Se já estiver aberto, use o menu de contexto para reabrir a janela de configuração.
- Dois módulos mantidos, com espaçamento reduzido; ícones dos aplicativos abertos e controles existentes preservados.
- Transparência por pixel, não blur real do desktop. Animação do calendário com alvo de 144 Hz (não medido).

## Executar

Na pasta que contém OrlaDock.csproj: `dotnet restore`, `dotnet build -c Release`, `dotnet run -c Release --no-build`.

Para publicar: `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish`.

## Verificações pendentes no Windows

1. Compilar e executar. Selecionar cada tamanho nas quatro posições, em 100% e 125% de DPI.
2. Abrir calendário, navegar entre meses, selecionar data, retornar a Hoje; verificar alinhamento ao relógio e fechamento externo.
3. Abrir/minimizar/fechar apps e verificar ícones; testar Wi-Fi, Bluetooth, áudio e bateria.
4. Confirmar inicialização automática a partir de uma pasta permanente e sem duplicação de processos.

Revisão estática e integridade ZIP verificadas. Não foi possível compilar nem executar neste ambiente Linux sem .NET SDK; não inclui EXE pré-compilado.
