# Orla Dock

## Glass Edition v1.6 — Windows 11

A nova implementação em C# / .NET 8 / WPF está em [Orla-Dock-Glass/](Orla-Dock-Glass/). Inclui dock Glass com ícones dinâmicos, posicionamento nas quatro bordas, calendário, controles do sistema e reserva de espaço com a API AppBar.

- [Código e instruções de compilação](Orla-Dock-Glass/README.md)
- [Alterações da v1.6](Orla-Dock-Glass/ATUALIZACAO-V1.6.md)

```powershell
cd Orla-Dock-Glass
dotnet restore
dotnet build -c Release
dotnet run -c Release --no-build
```

A v1.6 é distribuída como código-fonte; compilação e testes específicos da AppBar no Windows ainda pendentes. A versão original em C e seu executável permanecem disponíveis abaixo.

---

# Orla Dock 0.1.0

Dock portátil com visual próprio para Windows 10 e 11 x64. A interface é feita em C e
Win32. O executável usa as bibliotecas que acompanham essas versões do Windows e roda
com as permissões da sua conta.

## Download

Os arquivos da [versão v0.1.0](https://github.com/Filipcardos/orla-dock/releases/tag/v0.1.0) estão na pasta [downloads/v0.1.0](downloads/v0.1.0).

- [OrlaDock.exe](downloads/v0.1.0/OrlaDock.exe), executável portátil.
- [OrlaDock-0.1.0.zip](downloads/v0.1.0/OrlaDock-0.1.0.zip), pacote com o executável e o código-fonte.

## Como usar

1. Abra `OrlaDock.exe` para mostrar o dock no monitor principal.
2. Clique no ícone de um aplicativo para alternar para ele. Se ele já estiver ativo, o
   mesmo clique minimiza a janela. O botão de reticências mostra as janelas que
   excedem o espaço do dock.
3. Clique na marca à esquerda para mudar de tema, alternar entre topo e parte inferior
   ou fechar. O botão ao lado abre o Explorador de Arquivos. O atalho
   `Ctrl + Alt + O` também abre o menu quando a combinação está disponível.
4. Para usar mais espaço da tela, escolha **Configurações da barra do Windows** no menu
   do Orla. Ative **Ocultar automaticamente a barra de tarefas** nas configurações do
   sistema. O Windows guarda essa preferência. Desative-a no mesmo lugar quando
   quiser manter a barra do Windows à vista novamente.

O aplicativo guarda o tema e a posição em
`%LOCALAPPDATA%\OrlaDock\preferencias.ini`. Feche o Orla e remova essa pasta se quiser
apagar as preferências. O executável é portátil e esta compilação vem sem assinatura
digital.

## Arquitetura e validação

O Orla mantém um processo nativo e uma fila de mensagens do Windows. As mudanças
nas janelas são recebidas por `SetWinEventHook` e agrupadas antes de atualizar a lista.
O relógio agenda a próxima atualização para a virada do minuto. A interface é
desenhada quando solicitada, sem animação contínua, motor de navegador ou rede.

O LumaBar foi estudado pela página oficial e pela documentação pública. Os padrões
aproveitados para este protótipo foram o dock independente, a escolha de posição e
a combinação de atalhos com informação breve. O visual e o código do Orla foram
desenvolvidos para este projeto. A primeira versão concentra-se na troca de janelas,
nos temas locais e no relógio.

A compilação x64 passou com avisos tratados como erros e análise estática.
O teste no Wine verificou a abertura do dock, a atualização da lista com a abertura
e o fechamento de janelas, a troca da janela ativa por clique, a mudança do dock para
o topo pelo menu e o encerramento. Validação em Windows real, incluindo
compatibilidade com jogos e medição de CPU e memória, está pendente.

## Como compilar

Adicione o diretório `bin` da distribuição [LLVM-MinGW](https://github.com/mstorsjo/llvm-mingw)
ao PATH. No Windows, execute `compilar.cmd` junto do código-fonte, do manifesto e do ícone.
O script gera outro `OrlaDock.exe` na mesma pasta.

## Referências

- [Visão geral do LumaBar](https://lumabar.xyz/#overview)
- [Documentação pública do LumaBar](https://github.com/Acadbek/lumabar)
- [SetWinEventHook no Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook)
- [Design de aplicativos do Windows](https://learn.microsoft.com/en-us/windows/apps/design/)
