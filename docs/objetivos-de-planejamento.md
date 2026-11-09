# Objetivos de patrimônio e renda com prazo

Entrega de 03/11/2026. Em **Metas → Objetivos de patrimônio e renda → Abrir objetivos**, cadastre um valor desejado, um prazo e o tipo do objetivo. A seção possui busca, filtros de tipo/situação, paginação, edição e revisões. Os objetivos são independentes das metas percentuais de categoria/setor e das estratégias de distribuição de aportes.

## Objetivo de patrimônio

A referência atual é o valor de todas as posições da carteira na moeda-base. Não inclui Patrimônio Externo, nem exige que os ativos sejam elegíveis para receber aportes. Câmbio indisponível bloqueia o progresso; câmbio anterior é sinalizado.

O cálculo apresenta valor atual, valor que falta e progresso limitado de 0% a 100%. A quantidade de parcelas mensais inclui o mês atual e o mês do prazo. Exemplo: em novembro, um objetivo para janeiro possui três parcelas, independentemente do dia escolhido dentro dos meses. Um prazo no próprio dia possui uma parcela; um prazo anterior à data atual não possui parcelas futuras.

**Aporte mensal necessário sem rentabilidade = arredondamento para cima, em centavos, de (valor que falta ÷ parcelas).** Se o objetivo já foi atingido, o aporte necessário é zero. Se o prazo passou e ainda falta dinheiro, o aporte necessário fica indisponível, sem divisão por zero.

O campo **Aporte mensal para o cenário** permite avaliar **valor atual + aporte mensal × parcelas**. O cenário considera rentabilidade zero, sem inflação, custos futuros ou retiradas. Ele não representa promessa de desempenho ou execução automática. Cenários futuros não são apresentados para prazos encerrados. Não se presume que registrar um objetivo gere uma obrigação de aporte.

## Objetivo de renda mensal

A referência é a média mensal dos proventos líquidos registrados nos **12 meses completos anteriores**, divididos sempre por 12. Em 03/11/2026, a janela é 01/11/2025 a 31/10/2026. O mês atual fica fora, evitando comparar um mês incompleto com meses completos. Meses sem registros contam como zero; a qualidade da referência depende do histórico registrado pelo usuário.

São usados os recebimentos e os estornos da análise de Proventos, nas datas em que estão registrados. Um estorno fora da janela não muda o líquido daquela janela; ele participará da janela correspondente à sua data. Correções seguem os lançamentos e substitutos já auditados pelo sistema. Somente proventos datados entram na média: totais acumulados informados manualmente nas posições não substituem um histórico de recebimentos.

Recebimentos em outra moeda usam o último equivalente histórico registrado para a moeda-base; não se usa câmbio atual. Se algum lançamento da janela estiver sem conversão, a média inteira fica indisponível e a tela orienta revisar Proventos. Nenhuma soma parcial é exibida como renda total. O líquido pode ser negativo quando houver estornos de recebimentos de fora da janela; nesse caso a referência negativa é preservada e o progresso visual fica em zero.

Não há rendimento presumido, estimativa de dividendos futuros ou conversão de renda desejada em capital necessário. Aporte mensal e projeção de patrimônio pertencem exclusivamente ao objetivo de patrimônio.

## Estados, revisão e moeda

- O progresso considera a referência atual: **Em andamento**, **Atingido**, **Prazo encerrado**, **Base indisponível** ou **Moeda alterada**. Atingido significa que a referência atual atende ao valor, sem certificar que isso ocorreu até a data-limite. O progresso pode diminuir com mudanças no mercado ou nos registros.
- Cada objetivo usa a carteira inteira. Eles não segregam ativos ou dinheiro; aportes necessários de objetivos que representam o mesmo patrimônio não devem ser somados automaticamente.
- Salvar uma edição cria uma revisão, preservando nome, tipo, moeda, valor, prazo, aporte mensal do cenário e situação anteriores. O histórico mostra configurações, não fotografias do progresso antigo. Não há restauração automática de uma revisão; alterações podem ser feitas por uma nova edição.
- **Arquivar** cria uma revisão e retira o objetivo da lista de ativos. Na mesma moeda, desmarcar **Arquivado** na edição o reativa. Nenhum objetivo ou histórico é apagado.
- A moeda fica preservada por objetivo. Novos objetivos usam a moeda-base atual. Se a carteira mudar de moeda, os objetivos antigos não serão convertidos nem comparados com números de outra moeda. Podem ser arquivados sem alterar seus valores; para usar a moeda atual, crie um novo objetivo. Não há conversão implícita de metas ou cenários.
- A revisão esperada impede sobrescrever uma edição antiga. Repetir a mesma tentativa após resposta incerta retorna a revisão criada, sem duplicação. Alterações conflitantes pedem recarga.

## Arquitetura, efeitos e atualização

Domain contém `PlanningGoalRevision`; Application valida os dados, calcula os objetivos e reutiliza as análises existentes de carteira e Proventos. Infrastructure persiste revisões e filtra/pagina as versões atuais por carteira. API expõe GET/POST `/api/portfolios/{id}/planning-goals` e GET `/{goalId}/revisions`. Angular apresenta os valores do servidor, com painéis Material, proteção de formulários, privacidade de valores e paginação.

Salvar, arquivar e consultar objetivos não executam operações financeiras nem alteram posições, saldos, cotações, Patrimônio Externo, metas percentuais, estratégias, planos salvos, movimentações, fluxos ou fotografias. Consultar patrimônio reutiliza a política existente de câmbio/cache. A análise de renda não adiciona consultas a provedores de câmbio.

A migration **20261103132654_PlanningGoals** adiciona somente a tabela de revisões dos objetivos, índices e restrições; não preenche dados pessoais. Com a API parada e o backup manual feito, execute na raiz:

```powershell
dotnet ef database update --project InvestmentTracker.Infrastructure --startup-project InvestmentTracker.Api
```

O comando também aplica migrations anteriores pendentes. O banco pessoal não foi atualizado durante esta implementação. Faixas adicionais por grupo e benchmarks ficaram fora desta entrega. Teses foram retiradas do planejamento por decisão do usuário em 05/11/2026.

## Validação

Os testes verificam prazos inclusivos, centavos, valores máximos, estados, renda com meses vazios, janela fechada e ano bissexto, conversões revisadas/ausentes, estornos, isolamento entre carteiras, moeda alterada, revisões, conflitos, filtros e preservação dos dados financeiros. Os fluxos de navegador cobrem patrimônio e renda, edição, arquivo/reativação, histórico, recarga, privacidade, desktop/celular e temas claro/escuro.

Resultado local: **712 testes aprovados** — 286 unitários .NET, 179 de integração, 155 Angular, 72 de apresentação e 20 E2E. Lint, formatação e build aprovados; pacote inicial de 455,84 kB, abaixo do limite de 500 kB. Imagens do editor no celular e dos objetivos no tema escuro foram conferidas. Integração e E2E usaram bancos temporários; a base E2E foi removida ao concluir.
