# Integração ao Orla Dock existente no Codex

Use este pacote como implementação de referência, não como substituição automática.

1. Dentro de `/workspace/output`, identifique a stack, árvore de arquivos, branch, alterações não commitadas e funcionalidades existentes. Não apague ou sobrescreva o projeto.
2. Compare os componentes da implementação de referência com a arquitetura atual. Se a stack já for WPF, integre os estilos, serviços e componentes, preservando contratos. Se for outra stack, reimplemente a aparência e use as APIs nativas equivalentes, sem migrar sem necessidade.
3. Preserve recursos que já funcionam. Remova neon, brilho colorido e tiles vazios. Mantenha os dois módulos, glass neutro e logotipo vetorial do Windows sem texto.
4. Integre estados reais de Wi-Fi, Bluetooth, volume, brilho e bateria. Use fallback para ajustes quando a plataforma negar acesso. Não simule estados.
5. Use ícones do sistema/aplicativos em alta resolução, suporte a DPI e animações de transformação/opacidade, sem reconstrução de layout por frame. Meta 144 Hz, não um FPS artificialmente fixado.
6. Rode os testes e compile com a stack original. Valide em Windows real ou explicite a impossibilidade. Crie uma branch separada e registre o que mudou e o que falta.
