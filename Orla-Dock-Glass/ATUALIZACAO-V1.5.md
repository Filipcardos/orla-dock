# Orla Dock Glass 1.5

Atualização da configuração inicial, baseada no layout aprovado. Mantém o código da barra, do calendário e dos painéis da 1.4.

- Janela sem moldura branca, com título personalizado, botão Fechar, área de arraste e rodapé com Cancelar/Concluir.
- Desktop Acrylic solicitado via DWM (Windows 11 build 22621 ou superior). A disponibilidade e aparência são controladas pelo Windows; caso a API falhe, usa gradiente escuro de contraste legível. Não depende de APIs não documentadas.
- Controles de escolha, seleção de 4/5 redes, inicialização e slider com visual personalizado e sem componentes brancos padrão.
- Preserva posição, tamanho, limite, opacidade e inicialização no mesmo arquivo de preferências. Opacidade de 0% continua válida para a barra, mas não torna a configuração inicial invisível.
- Janela limitada à área de trabalho primária, com rolagem se necessário.

Validação: estrutura ZIP e XML conferidas; não compilado ou executado neste ambiente Linux. Testar no Windows 11, incluindo DPI, transparência do sistema, tamanho e teclado. Nenhuma alteração nas funcionalidades de detecção de apps, calendário ou rádio.
