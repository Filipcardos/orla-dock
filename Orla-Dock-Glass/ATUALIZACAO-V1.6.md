# Orla Dock Glass 1.6 — integração com a área útil do Windows

Base: versão 1.5, sem alterações no visual, calendário ou controles do sistema.

- As duas janelas visíveis deixam de usar `Topmost`.
- Uma única janela auxiliar transparente, sem ativação e sem captura de cliques, registra a faixa reservada pelo Orla Dock junto ao Shell com `SHAppBarMessage` (`ABM_NEW`, `ABM_QUERYPOS`, `ABM_SETPOS`, `ABM_REMOVE`).
- A faixa considera a posição, o tamanho selecionado e o DPI do monitor principal. A barra de tarefas original permanece habilitada; as duas barras ocupam áreas separadas. A reserva abrange toda a largura/altura da borda, ainda que o Orla Dock seja centralizado.
- Mudanças de posição, DPI, resolução e notificações do Shell solicitam novo posicionamento. A posição reservada não é reaplicada se não mudar.
- `ABN_FULLSCREENAPP` fecha os painéis abertos e rebaixa as duas janelas na ordem Z, sem ativá-las. Ao sair da tela cheia, volta à ordem Z normal, não Topmost.
- Ao sair pelo menu de contexto, a reserva é removida. Após reinício do Explorer, tenta registrar novamente.
- Caso o registro falhe, o dock usa a área útil normal, sem ficar sempre no topo. Nesse fallback não há garantia de reserva de espaço.

## Testes no Windows (pendentes)

1. Compilar e abrir, selecionar Inferior e Pequena. Maximizar Edge, Explorer e Terminal: a área de conteúdo não deve cobrir o dock nem ser coberta por ele.
2. Alternar Superior, Esquerda e Direita; confirmar que a faixa anterior deixa de ser reservada.
3. Alterar tamanho, maximizar/restaurar janelas e confirmar que os painéis e o calendário continuam acessíveis.
4. Abrir vídeo em tela cheia e sair; confirmar ordem Z e ausência de sobreposição.
5. Sair do dock pelo menu de contexto e maximizar novamente: o espaço antes reservado deve voltar a ficar disponível.
6. Testar escala 100%/125%, barra de tarefas nativa com ocultação automática e reinício do Explorer.

A API de AppBar não obriga aplicativos com gerenciamento próprio de janelas a respeitar a área útil. Esta versão continua voltada ao monitor principal. Não houve compilação ou execução no Windows neste ambiente.

Referência: https://learn.microsoft.com/pt-br/windows/win32/shell/application-desktop-toolbars
