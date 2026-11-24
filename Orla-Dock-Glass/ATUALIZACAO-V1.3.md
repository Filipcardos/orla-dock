# Orla Dock Glass v1.3

- Remove Explorador e Edge fixos e o limite de dois aplicativos adicionais.
- Exibe aplicativos com janelas detectadas, inclusive minimizadas; atualiza a cada 500 ms. Processos em segundo plano sem janela não aparecem. Uma entrada por caminho de executável.
- Preserva ordem dos ícones existentes e acrescenta novos à direita ou abaixo. Quando a área disponível acaba, use a roda do mouse sobre os ícones.
- Ícones obtidos da janela ou do executável via APIs do Windows. Se não conseguir extrair, exibe o nome do aplicativo em texto, sem desenho genérico, e tenta novamente periodicamente. Alguns aplicativos empacotados, elevados e janelas especiais podem não ser detectados ou apresentar o ícone retornado pelo sistema.
- Mantém opções da v1.2, logo do Windows, relógio e controles.

## Compilar

Na pasta do projeto: `dotnet restore`, `dotnet build -c Release`, `dotnet run -c Release`.

## Teste no Windows

Abra e feche Edge, Explorador, Terminal e outros programas; minimize e restaure; confira se o ícone aparece e desaparece, sem duplicação por janela. Verifique as quatro bordas e a rolagem com muitos apps.

Código revisado estaticamente; compilação e execução Windows ainda não verificadas.
