# Refinamento do Dashboard — densidade e detalhes

Rodada iniciada em 23/10/2026 após a conclusão dos sete marcos de usabilidade. O problema relatado é o excesso de texto junto aos gráficos, que aumenta cartões e cria rolagem interna. As quatro etapas foram autorizadas sequencialmente e concluídas.

As seções abaixo preservam o registro cronológico de cada etapa; frases sobre próxima etapa ou limitações intermediárias referem-se àquele momento. Atualização em **27/10/2026**: filtros analíticos por carteira e contenção de cartões extensos na coluna móvel foram tratados na entrega de [contexto analítico](contexto-analitico-dashboard.md). A otimização de bundle/logo também foi entregue posteriormente, conforme [carregamento](otimizacao-carregamento.md). Para pendências atuais, consulte [próximas etapas](proximas-etapas.md).

| Etapa | Escopo | Estado |
| --- | --- | --- |
| 1 | Distribuições por categoria, setor e país: legendas, barras e detalhes | Implementada em 23/10/2026 |
| 2 | Evolução patrimonial: separar gráfico e detalhamento do fechamento | Implementada em 25/10/2026 |
| 3 | Destaques e proventos: ranking visual, moeda e janela de períodos | Implementada em 25/10/2026 |
| 4 | Movimentações recentes e grade: lista compacta, alturas e rolagens | Implementada em 25/10/2026 |

## Etapa 1 — distribuições

- Roscas no Dashboard têm diâmetro menor e legenda com nome/percentual, sem repetir valores monetários. Nomes extensos são abreviados visualmente; o texto completo permanece no DOM e na tabela de detalhes.
- Barras usam linhas menores, com participação atual e marcador de meta. O valor textual da meta, valores monetários e diferenças permanecem na tabela; meta ausente é identificada no resumo quando o grupo está visível.
- Os resumos mostram até cinco grupos na ordem escolhida pelos filtros/ordenação. O limite é informado com a quantidade total e o botão Ver detalhes. Não é necessariamente um ranking: ao ordenar por nome, são os cinco primeiros nomes.
- A rosca continua desenhando **todos os grupos filtrados**; apenas sua legenda é limitada. As barras mostram os cinco primeiros grupos. Não houve agregação em Outros nem mudança automática do formato escolhido. As cores existentes e o denominador da carteira inteira são preservados; grupos ocultados por filtros não são redistribuídos nos percentuais.
- Ver detalhes abre o painel compartilhado com todos os grupos dos filtros atuais, nomes completos, valores, participações e, para categorias/setores, metas e diferenças em pontos percentuais. Países continua sem metas. A tabela não cresce dentro do cartão. No celular, pode rolar horizontalmente em sua própria região, com orientação e acesso por teclado, para não quebrar valores monetários em várias linhas.
- Esc, botão de fechamento e Fechar detalhes encerram o painel e devolvem foco ao acionador. Privacidade monetária continua aplicada na tabela. Dados indisponíveis permanecem identificados; a versão compacta também informa sua quantidade, inclusive quando estiverem além dos cinco itens do resumo.
- Implementação optativa em `allocation-table.ts`/`allocation-donut.ts`, ativada somente pelo Dashboard. Simulador e detalhes históricos mantêm a apresentação completa anterior. Sem nova biblioteca, backend, contratos financeiros ou acesso a dados reais.

## Validação

- Build de produção aprovado: pacote inicial de **524,92 kB**, mantendo o aviso de 500 kB, sem aumento de budgets nem aviso de CSS de componente.
- **120 testes unitários em 26 arquivos aprovados**. Novos casos verificam limite do resumo versus tabela completa, metas ausentes, privacidade, denominador da rosca, manutenção de todas as fatias e rejeição de participações desconhecidas. Testes de Dashboard passaram a abrir o painel antes de consultar sua tabela.
- **48 cenários de apresentação aprovados**, com APIs fictícias, desktop/celular e temas claro/escuro. Foram acrescentados quatro cenários de distribuições com dez grupos e nomes longos, limite de legenda, filtros, detalhe completo, foco e privacidade. A conferência visual corrigiu a largura da legenda móvel e a quebra de valores na tabela.
- Após ajustes finais de espaçamento, **oito cenários de gráficos repetidos e aprovados**, incluindo asserção de ausência de rolagem vertical no cartão compacto de barras com cinco itens visíveis. Após orientação/região acessível da tabela, os quatro cenários de distribuições foram repetidos. Configuração temporária na porta 65456, removida ao terminar.
- Evidências em `docs/evidencias/dashboard-distribuicoes/`: rosca, barras e painel completo nas quatro configurações. Exemplo: [rosca desktop](evidencias/dashboard-distribuicoes/distribution-donut-desktop.png), [barras desktop](evidencias/dashboard-distribuicoes/distribution-bars-desktop.png) e [detalhes no celular](evidencias/dashboard-distribuicoes/distribution-details-celular.png).

## Limites e continuação

As alturas salvas e a geometria da grade não foram redefinidas. A redução de texto permite usar cartões compactos em mais situações, mas não promete eliminar toda rolagem com avisos, fontes ampliadas ou no modo de organização. Ajuste geral de alturas pertence à etapa 4. A tabela completa pode exigir rolagem, agora fora do cartão.

Com muitos grupos, a rosca ainda pode ter segmentos pequenos e cores repetidas da paleta existente; o formato Barras e a tabela continuam disponíveis. A primeira etapa preserva a escolha de formato em vez de trocá-la automaticamente. Agrupamento em Outros pode ser avaliado depois, sem confundi-lo com grupos filtrados.

Validação móvel emulada; aparelho físico, teclado virtual e auditoria formal de acessibilidade continuam fora da comprovação. Nenhum dado financeiro foi gravado e nenhum commit foi realizado. Próxima etapa: Evolução patrimonial.

## Etapa 2 — evolução patrimonial — 25/10/2026

- O cartão mantém frequência mensal/anual, ano, gráfico e aviso de fechamentos que precisam de revisão. A seleção do fechamento, intervalo de comparação, aportes líquidos, valorização/outros efeitos, Dietz, XIRR e notas saíram do corpo do cartão.
- Ver detalhes da evolução abre o painel compartilhado. Nele ficam o fechamento selecionado, os indicadores, as explicações e a tabela completa dos pontos do gráfico. Histórico completo continua acessível diretamente pelo cartão; os métodos de cálculo permanecem na tela de Evolução.
- A tabela de apoio não se expande mais dentro do cartão da evolução. `MoneyChart.showDataTable` é optativo e permanece verdadeiro por padrão nas outras telas. A descrição acessível do gráfico indica que seus dados estão nos detalhes quando a tabela local é desativada.
- O fechamento mostrado no seletor corresponde ao usado nos indicadores, inclusive ao abrir o painel pela primeira vez. A revisão visual identificou a seleção nativa do primeiro item enquanto o estado usava o mais recente; o vínculo explícito da opção selecionada corrige essa divergência.
- Preservados os intervalos entre fotografias e todos os valores retornados pela API. Dados indisponíveis não viram zero; lacunas, moeda incompatível, fechamento reaberto ou desatualizado continuam interrompendo a linha. Métricas de um fechamento inválido continuam bloqueadas. Não há mistura entre fluxos do período civil e fluxos entre fotografias.
- Esc e Fechar detalhes devolvem foco ao acionador. No celular, os indicadores ficam em uma coluna. Valores monetários do painel/tabela respeitam a privacidade. Troca de carteira/recarga da análise fecha o painel anterior. Ausência de fotografias desabilita a abertura de detalhes.
- Sem novas dependências, gravações, alterações de backend ou cálculos financeiros. As alturas da grade e a janela horizontal de períodos permanecem para as etapas seguintes. Destaques, Proventos e Movimentações não foram redesenhados nesta entrega.

### Validação da etapa 2

- Build final aprovado: **524,92 kB** de pacote inicial, mantendo o aviso de 500 kB sem ampliar budgets. Nenhuma dependência adicionada; a rota Dashboard passa de aproximadamente 54,67 para 56,96 kB no pacote carregado sob demanda.
- **121 testes unitários em 26 arquivos aprovados**. O novo caso confere painel separado, texto acessível do gráfico, seleção inicial do fechamento, privacidade, notas, métricas indisponíveis e limpeza do painel ao trocar carteira. A primeira execução teve falha de leitura de arquivo no ambiente (`UNKNOWN: unknown error, read`); a repetição completa passou, sem alterar o arquivo afetado de correções.
- **48 cenários de apresentação aprovados** em desktop/celular e claro/escuro. A jornada de gráficos verifica ausência de métricas/tabela no cartão, abertura dos detalhes, intervalo, seleção de fechamento inválido e válido, tabela de três períodos com lacuna, privacidade e restauração de foco. Após corrigir o seletor, os quatro cenários de gráficos foram repetidos e aprovados.
- Oito capturas finais em `docs/evidencias/dashboard-evolucao/`, com cartão e painel nas quatro configurações. Conferidos cartão [desktop](evidencias/dashboard-evolucao/evolution-card-desktop.png), painel [desktop escuro](evidencias/dashboard-evolucao/evolution-details-desktop-escuro.png) e [celular](evidencias/dashboard-evolucao/evolution-details-celular.png), incluindo a coerência do seletor com os indicadores.
- APIs inteiramente fictícias; sem backend, banco real ou provedor. Configuração temporária na porta 65456 removida. Permanecem as limitações de aparelho físico, teclado virtual e acessibilidade formal, além dos avisos conhecidos de logo e APIs nativas do jsdom. Sem commit.

Próxima etapa: destaques dos ativos e proventos. A organização e as alturas da grade serão refinadas na etapa 4; cartões amplos ainda podem apresentar espaço livre, mesmo após retirar os detalhes.

## Etapa 3 — destaques e proventos — 25/10/2026

- Destaques apresenta até três altas e três quedas em barras horizontais com origem central, sinal e percentual. Os dois grupos usam a mesma escala absoluta; a cor não é a única indicação de direção. O preço unitário saiu do resumo.
- Ver ranking completo abre todos os ativos elegíveis ordenados, sem o corte anterior de cinco por grupo, com cotação unitária na moeda original e variação. Os critérios de elegibilidade, a ordenação e o desempate por ticker foram preservados. Variação zero não entra em altas/quedas. Não é a variação do mês nem retorno total com proventos.
- Proventos apresenta uma moeda por vez. O seletor distingue moeda original e equivalentes na moeda-base; não soma moedas originais entre si. A moeda-base original é preferida quando disponível; caso contrário, a primeira moeda disponível. Uma carteira sem dados mantém uma opção coerente com a moeda-base, sem criar valores.
- A janela inicial mostra os cinco períodos mais recentes com registros, com Anteriores/Seguintes e limites desabilitados. Há opção de 12 períodos; nessa opção, o gráfico pode rolar horizontalmente para manter os rótulos legíveis. Não são necessariamente cinco meses consecutivos: períodos sem registros continuam ausentes, sem zeros inventados.
- Trocar moeda, frequência, ano ou tamanho da janela recupera a janela mais recente. Ver detalhes dos proventos reúne as explicações, frequência mensal/anual, filtro de ano e tabela completa da moeda selecionada, incluindo pontos fora da janela do cartão. A escolha desses filtros também atualiza o gráfico ao fechar o painel.
- Valores negativos de estornos e totais incompletos permanecem sinalizados. O aviso de conversões pendentes considera o conjunto dos filtros, inclusive períodos fora da janela; recortar o gráfico não torna a análise completa. As agregações existentes não foram alteradas.
- Os dois painéis respeitam privacidade monetária, fechamento por Esc/Fechar detalhes e retorno de foco. Percentuais do ranking permanecem visíveis conforme a política anterior. A troca de carteira/recarga da análise fecha o painel anterior. Nenhuma escrita financeira, endpoint, provedor ou dependência nova.

### Validação da etapa 3

- Build final aprovado: pacote inicial de **524,92 kB**, com aviso de 500 kB mantido e sem aumentar budgets. A rota Dashboard carregada sob demanda passa de aproximadamente 56,96 para 64,96 kB; nenhuma biblioteca adicionada. Sem aviso de CSS de componente.
- **123 testes unitários em 26 arquivos aprovados**. Os dois casos novos conferem resumo versus ranking completo, escala compartilhada, privacidade dos preços, janelas de uma moeda, retorno aos períodos recentes, pendência fora da janela, rejeição de moeda inválida e carteira sem recebimentos.
- **52 cenários de apresentação aprovados** em desktop/celular e claro/escuro. Novo percurso com 12 posições e 14 períodos de proventos: resumo de seis líderes, ranking completo de 12, moedas BRL/USD/base, janelas de 5/12, navegação, tabela completa, estorno negativo, conversão indisponível, ano/frequência e privacidade. Requisições dessas interações permanecem somente de leitura e totalmente interceptadas.
- 16 capturas finais em `docs/evidencias/dashboard-destaques-proventos/`, com cartões e detalhes nas quatro configurações. Conferidos [ranking desktop](evidencias/dashboard-destaques-proventos/ranking-card-desktop.png), [ranking escuro](evidencias/dashboard-destaques-proventos/ranking-card-desktop-escuro.png), [proventos desktop](evidencias/dashboard-destaques-proventos/income-card-desktop.png), [proventos celular](evidencias/dashboard-destaques-proventos/income-card-celular.png) e painéis completos.
- Sem backend, banco real, provedor ou gravação financeira. Configuração temporária Playwright na porta 65456 removida. Permanecem os avisos conhecidos de logo e APIs ausentes no jsdom e as limitações de aparelho físico, teclado virtual e acessibilidade formal. Sem commit.

A etapa 4 permanece pendente: movimentações recentes e adequação das alturas da grade. Os cartões ainda usam as alturas salvas; espaços livres ou rolagem em modo compacto/organização não foram resolvidos globalmente nesta etapa. Filtros analíticos continuam em memória do componente, sem nova persistência ao sair da tela.

## Etapa 4 — movimentações e altura da grade — 25/10/2026

- A grade passou de linhas fixas e rolagem vertical interna para altura conforme o conteúdo. Cartões recolhidos ocupam apenas o cabeçalho, e os cartões menores não são esticados até a altura do vizinho. A ordem visual segue a ordem de leitura; não há preenchimento denso que reordene cartões. Uma linha ainda pode deixar espaço sob seu cartão menor.
- Compacto reduz o espaçamento e o tamanho das roscas e dos gráficos temporais. Médio mantém a apresentação regular; Amplo ocupa as duas colunas no desktop. No celular, os cartões continuam em uma coluna. Organização, tamanhos, formatos e recolhimento usam a mesma chave e estrutura de preferências por carteira, sem apagar escolhas existentes.
- Gráficos temporais do Dashboard acompanham a largura disponível por ResizeObserver, liberado ao destruir o componente. O modo é optativo: as demais telas preservam a geometria anterior. Janelas com mais de cinco pontos ainda podem rolar horizontalmente para preservar rótulos, e tabelas/painéis completos mantêm sua rolagem quando necessária.
- Movimentações recentes mantém os cinco últimos registros, ordenados por data, criação e ID. Cada linha mostra tipo/ID, data, nome abreviado e valor apenas nos tipos que já representavam fluxos financeiros. Estornos e substituições permanecem explícitos. Dados completos são consultados em um painel separado, sem aumentar o cartão.
- O painel exibe descrição e nomes completos, vínculos de estorno/correção/operação e efeitos antes/depois, respeitando a precisão e privacidade existentes. É uma consulta dos dados já carregados; a tela Movimentações continua responsável por atualizar auditoria, estornar ou corrigir. Nenhum endpoint adicional nem ação financeira foi criado. Esc fecha o painel e devolve foco; troca de carteira/recarga fecha a seleção anterior.
- Sem dependências novas, alterações de backend, acesso a banco/provedor ou commit. Aparelho físico, teclado virtual e auditoria formal de acessibilidade continuam fora da comprovação.

### Validação da etapa 4

- Build de produção aprovado: pacote inicial de **524,92 kB**, mantendo o aviso de 500 kB e os budgets existentes. Dashboard carregado sob demanda: **69,49 kB**. Sem aviso de CSS de componente.
- **125 testes unitários em 26 arquivos aprovados**. Os novos casos verificam consulta dos efeitos completos, privacidade, fechamento por mudança de estado, exclusão de ajustes administrativos do valor de fluxo e geometria compacta com valores negativos.
- **56 cenários de apresentação validados** em desktop/celular e claro/escuro: 52 anteriores passaram na execução completa; os quatro novos passaram após corrigir o seletor do teste que também encontrava SVGs dos ícones de organização. O novo percurso confere os cinco registros, detalhes completos, precisão, privacidade, Esc/foco, três tamanhos, recolhimento persistido, ausência de sobreposição/rolagem vertical interna e largura intermediária de 720 px. A repetição final registra as capturas sem o cabeçalho fixo sobreposto na imagem da grade.
- Doze capturas em `docs/evidencias/dashboard-grade/`: grade, movimentações e painel nas quatro configurações. Exemplos: [grade desktop](evidencias/dashboard-grade/dashboard-natural-grid-desktop.png), [movimentações no celular escuro](evidencias/dashboard-grade/recent-movements-celular-escuro.png) e [consulta no celular](evidencias/dashboard-grade/recent-movement-details-celular.png).
- Todas as APIs interceptadas com dados fictícios, com requisições somente GET no novo percurso. Configuração temporária na porta 65456 removida ao concluir. Permanecem os avisos conhecidos de logo e APIs ausentes no jsdom. A rodada de quatro etapas está concluída, sem gravações financeiras e sem commit.
