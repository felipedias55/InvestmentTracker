# Contexto analítico do Dashboard — 27/10/2026

## Comportamento

Evolução e Proventos recuperam escolhas ao sair do Dashboard e voltar pela navegação interna ou pelo histórico do navegador. As escolhas são separadas por carteira e por análise:

- Evolução: frequência mensal/anual, ano e fechamento selecionado nos detalhes.
- Proventos: frequência, ano, moeda original ou moeda-base, tamanho da janela (5/12) e deslocamento para períodos anteriores.

Os painéis de detalhes voltam fechados. Trocar de carteira recupera as escolhas dessa carteira, usando padrões se ainda não houver contexto. Recarregar dados salvos preserva o contexto; recarregar a página ou abrir outra sessão reinicia os filtros. Ordem, formatos e recolhimento dos cartões continuam com as preferências já existentes no navegador.

O contexto usa o serviço PageContexts em memória, sem localStorage, alteração de saldos ou novas chamadas de escrita. A restauração de rolagem continua sob responsabilidade da página principal; o contexto analítico armazena somente as escolhas de consulta. Não há recuperação de rascunhos financeiros.

## Atualização dos dados e falhas

O contexto é salvo antes de trocar de carteira ou recarregar a análise e ao destruir o componente. Só é substituído após uma carga válida: falhas e requisições canceladas não apagam a última seleção carregada.

Na restauração, um ano ausente volta a Todos; uma moeda original ausente segue a escolha disponível da carteira; um fechamento ausente usa a última referência disponível nos filtros. A janela é limitada à quantidade atual de pontos, sem preencher períodos ausentes. Seu deslocamento é relativo ao fim da série, portanto novos recebimentos podem mudar quais referências ocupam a mesma janela. Se ano ou moeda deixar de existir, a janela volta ao trecho mais recente da seleção válida.

Mudar frequência, ano, moeda ou tamanho da janela durante o uso continua voltando ao trecho mais recente. Estornos negativos, conversões pendentes, precisão e cálculo dos totais permanecem com as regras anteriores.

## Ajuste de interface móvel

O teste com 12 períodos revelou que os cartões da coluna móvel herdavam o alinhamento lateral inicial da grade de desktop. Conteúdo extenso podia alargar o cartão e atrapalhar o acionamento dos controles. A coluna móvel agora estica os cartões até a largura disponível; séries extensas mantêm sua rolagem horizontal dentro do gráfico. O teste confere ausência de transbordamento da página e usa cliques normais, sem forçar ações sob elementos sobrepostos.

Os seletores de ano e tamanho de janela também passaram a marcar explicitamente a opção selecionada, para que o valor visível acompanhe o contexto restaurado após a chegada dos dados.

## Verificação e limites

Os testes de regressão cobrem navegação, troca de carteira, recarga de dados, reinício após recarga da página, isolamento entre análises, fechamento de painéis, erros/cancelamentos e perda de moeda/ano/parte da série. O novo percurso de apresentação usa apenas GETs interceptados e roda em desktop/celular e claro/escuro.

Validação final em **27/10/2026**:

- Build de produção aprovado, com pacote inicial de **455,77 kB**, abaixo dos 500 kB, sem mudar budgets. Dashboard sob demanda: **71,32 kB**.
- **129 testes Angular** aprovados em 26 arquivos, incluindo quatro novos casos de contexto e regressões dos valores visíveis dos seletores.
- **60 cenários de apresentação** aprovados em execução completa de 2,8 minutos: 56 anteriores e o novo percurso nas quatro configurações. Todos os acessos à API no novo percurso são GETs fictícios.
- Capturas do cartão de proventos após restauração conferidas em celular claro e desktop escuro; os percursos incluem fechamento de diálogo por Escape e os testes anteriores de teclado/menu/foco.
- Documentação consolidada: README e planejamento distinguem persistência de cotações BR/EUA; a revisão funcional de setembro e os registros intermediários do Dashboard foram identificados como históricos, com links para o estado posterior.

Isso não equivale a auditoria formal de acessibilidade ou validação física: leitor de tela, contraste completo, zoom real de 200%, teclado virtual, toque e imagem nativa de arraste continuam exigindo conferência própria, conforme o [plano de usabilidade](plano-usabilidade-e-navegacao.md). Nenhuma base pessoal ou provedor de cotações foi acessado nesta rodada.
