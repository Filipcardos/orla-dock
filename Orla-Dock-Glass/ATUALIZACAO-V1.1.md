# Orla Dock Glass 1.1
- Janela com transparência por pixel (WPF AllowsTransparency), fundo escuro com alfa 76–105/255, sem Acrylic cinza opaco. Não há desfoque real do desktop neste modo.
- Clique esquerdo Wi-Fi/Bluetooth abre painel flutuante. Alternância dentro do painel; clique direito continua abrindo configurações.
- Wi-Fi: varredura WinRT, intensidade do sinal, conexão com senha e desconexão. Depende das permissões e drivers. Redes empresariais/ocultas devem ser configuradas pelo Windows.
- Bluetooth: enumeração Classic e LE, tentativa de pareamento padrão; dispositivos já pareados e casos que exigem UI adicional usam configurações do Windows.
- Não foi compilado/testado no Windows nesta sessão. WPF transparente pode ter custo de composição; 144 Hz não garantidos.
- Feche a versão anterior antes de executar `dotnet build -c Release` e `dotnet run -c Release` nesta pasta.
