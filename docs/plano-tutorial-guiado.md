# Plano de tutorial guiado — 06/11/2026

Status: planejado, não implementado. Iniciativa independente do [chatbot](plano-chatbot.md), priorizada antes dele. Integra a fase de orientação do [plano técnico](plano-melhorias-tecnicas.md).

## Experiência proposta

Ajuda acessível pelo menu e por ações contextuais, com roteiros curtos por tarefa. Ao iniciar, escurecer o restante da interface e destacar o elemento explicado, com título, descrição curta, próximo/anterior, progresso e sair. Permitir iniciar novamente e pular; não obrigar tutorial na primeira visita.

Primeiros roteiros:

1. Entender carteira, saldo/patrimônio externo e dashboard.
2. Ajustar saldo externo: onde fica Movimentações, por que informar o saldo final e quando usar depósito/retirada.
3. Cadastrar ativo, imagem e posição inicial; distinguir cadastro de compra.
4. Registrar compra/venda/provento, sem executar operação durante a explicação.
5. Consultar cotações e conferir consumo/fonte; diferenciar de recarregar dados.
6. Criar fotografias, interpretar datas e comparar benchmarks, incluindo indisponibilidades.

## Primeira entrega e expansão

Primeira entrega: infraestrutura do tutorial e roteiro **ajustar saldo externo**, respondendo à dúvida real registrada. Os demais roteiros ficam para expansão após validar esse padrão.

Etapas devem apontar para identificadores semânticos estáveis, sem depender de coordenadas fixas, posição na lista, nomes pessoais ou índices frágeis do DOM. Explicar a intenção e o efeito da ação, não detalhes internos do software.

Não realizar compras, ajustes, exclusões, chamadas de provedores ou salvamentos para avançar no tutorial. Caminhar até um formulário deve manter proteção de rascunhos; não limpar ou preencher valores existentes automaticamente. Se não houver carteira/saldo/elemento necessário, explicar o pré-requisito e oferecer encerramento ou caminho seguro.

## Integração técnica

Serviço e componente compartilhados de apresentação; roteiros declarativos separados por recurso e conteúdo versionado. Reutilizar a camada de overlay disponível no projeto ou solução nativa compatível, a decidir após protótipo. Não escolher nova biblioteca só para desenhar uma área escurecida.

Validar especialmente a integração com `<dialog>`: overlays externos podem ficar abaixo da camada do diálogo. Tratar rolagem, mudança de tamanho, menu móvel, alvo removido e carregamento assíncrono. Não deixar máscaras soltas após navegar ou fechar o diálogo.

Teclado, Escape, foco visível, retorno ao elemento de origem, descrição acessível e respeito a movimento reduzido fazem parte do próprio componente. Oferecer instruções textuais equivalentes: a explicação não pode depender apenas do destaque visual. Isso não reabre a auditoria formal de acessibilidade anteriormente encerrada.

Conclusão/preferências podem ficar localmente com versão do roteiro, sem criar registros financeiros. Não haverá custo de tokens de IA; o tutorial só poderá carregar os dados locais necessários à própria tela, sem consultar provedores para demonstração.

## Aceite

- Executar e sair em qualquer etapa sem gravar operações ou perder rascunhos.
- Explicar ajuste total versus depósito/retirada usando os nomes reais da interface.
- Funcionar em desktop/celular e claro/escuro, inclusive dentro de diálogos e após rolagem.
- Tratar ausência de dados/alvo, troca de carteira e navegação sem travar a interface.
- Testar cancelamento, foco e ausência de chamadas financeiras/provedores; cenário visual e jornada de uso controlada.

Não inclui gamificação, conclusão obrigatória, rastreamento externo de comportamento ou execução de operações financeiras pelo tutorial.
