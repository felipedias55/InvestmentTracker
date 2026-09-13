# Fechamentos, análise de proventos e eventos

## Lançamentos atrasados e reabertura

Em **Evolução e fechamentos**, abra “Reabrir período para lançamentos atrasados”, informe a data inicial e um motivo. A reabertura é registrada no histórico unificado, sem alterar dinheiro. Todas as fotografias com data igual ou posterior são marcadas como reabertas e desatualizadas.

Uma operação com data anterior a uma fotografia fechada exige reabertura. O bloqueio não depende mais da última data de qualquer operação da carteira: operações em posições e saldos independentes podem ter datas diferentes. A validação ocorre dentro da mesma transação que grava os efeitos e mantém o bloqueio por carteira.

Se existem operações posteriores ativas na mesma posição ou saldo, é necessário estorná-las da mais recente para a mais antiga, registrar o lançamento atrasado e registrar novamente as posteriores na ordem correta. O sistema não aplica uma compra antiga por cima do custo médio de vendas posteriores. Registros antigos sem efeitos auditados bloqueiam conservadoramente o lançamento atrasado; seus saldos anteriores não são inventados. Lançamentos na mesma data seguem a ordem de registro.

**O estorno mantém sua data de execução**, inclusive quando serve para reorganizar lançamentos atrasados. Portanto, as séries de aportes e recebimentos conservam o histórico de correções: o mês de um relançamento pode ter valores brutos duplicados, compensados pelo estorno no mês da correção. Elas não devem ser interpretadas como uma reconstituição contábil retroativa. A trilha mostra o original, seu estorno e a nova operação.

Operações, estornos, registros históricos ou alterações de saldos posteriores à captura, com data efetiva abrangida pela fotografia, sinalizam que ela está desatualizada. Aportes históricos não alteram saldos, mas podem invalidar comparações. Inserir/remover saldos iniciais também invalida fotografias afetadas. Atualizações de preços de dias posteriores não tornam desatualizada uma fotografia antiga que representava corretamente seu próprio dia.

As comparações ficam suspensas quando uma das fotografias envolvidas está desatualizada. O gráfico mantém o valor registrado, com barras listradas e aviso. Nenhum valor passado é substituído pelos saldos de hoje.

## Versões do fechamento

“Atualizar fotografia deste mês” cria uma nova versão, preservando a anterior, inclusive seu documento completo, câmbio e instante de captura. A consulta da fotografia permite abrir versões anteriores. A nova captura encerra a reabertura do **mês atual**.

Meses passados reabertos permanecem disponíveis como documentos desatualizados. Não existe recálculo ou refotografia automática do passado, pois faltam as cotações, saldos e classificações históricos necessários. Também não se reconstrói uma versão que já havia sido sobrescrita antes desta implementação. A primeira versão disponível é a que existia na atualização.

## Taxas e custos de negociação

O campo de taxas e custos recebe o **total da operação**, na moeda do ativo, com até quatro casas decimais e valor não negativo.

- Compra: débito/aporte e acréscimo do custo = quantidade × preço, arredondado a quatro casas, + taxas.
- Venda: crédito/retirada = quantidade × preço, arredondado a quatro casas, − taxas. O recebimento líquido precisa ser positivo.
- A redução do custo na venda continua proporcional à quantidade, independentemente do preço vendido e da taxa.
- O valor de mercado da posição usa a quantidade remanescente e o preço unitário; taxas não viram cotação.
- O equivalente em moeda-base informado para um fluxo externo deve corresponder ao total com taxas. Reenvios com taxas diferentes e mesmo identificador são recusados.

Exemplo sintético: comprar 10 unidades a 10 com taxa 2,50 custa 102,50. Vender 2 a 12 com taxa 1 recebe 23 e deixa custo de 82 para as 8 unidades restantes. O estorno restaura os saldos e o custo exatos registrados antes da operação.

## Eventos na posição

Em **Compras e vendas**, abra o formulário de eventos. Todos exigem data, posição e comunicado/motivo; ficam em **Movimentações**, com efeitos antes/depois e estorno.

- Desdobramento: informar a quantidade total final, maior que a atual; preserva o custo total.
- Grupamento: informar a quantidade total final, menor que a atual; preserva o custo total.
- Bonificação: informar a quantidade adicional recebida e o custo total atribuído conforme o comunicado. Zero é aceito quando aplicável. Não há inferência de regra fiscal.

O valor de mercado total é preservado até atualizar a cotação real após o evento. Não se cria valorização fictícia multiplicando a quantidade nova pela cotação anterior. Nenhum desses eventos gera aporte, retirada ou provento em dinheiro. Quantidades aceitam seis casas decimais, inclusive frações custodiadas; uma venda de frações deve ser lançada separadamente. O sistema não calcula liquidação de frações, impostos ou eventos de incorporação/cisão.

## Análise de proventos e evolução

**Proventos** apresenta recebidos, estornados e líquido após estornos, por mês, ano e ativo. Filtros: ativo, ano e moeda. O agrupamento por ativo usa sua identidade, mesmo que o ticker tenha sido renomeado. Valores de moedas diferentes ficam separados. Estornos entram no período da correção e podem produzir saldo mensal negativo. Os acumulados iniciais sem data de recebimento não são distribuídos artificialmente pelos meses.

Na evolução, entre duas fotografias válidas na mesma moeda-base:

```
Aportes líquidos = aportes − retiradas (com estornos)
Variação patrimonial = patrimônio final − patrimônio inicial
Valorização e outros efeitos = variação patrimonial − aportes líquidos − proventos retidos
Resultado econômico = variação patrimonial − aportes líquidos + proventos recebidos fora da carteira
```

O intervalo começa depois da data da fotografia anterior e termina na data da atual. Proventos retidos já estão no patrimônio; somá-los novamente duplicaria rendimentos. O residual inclui preços, câmbio, taxas e ajustes manuais, portanto não é uma medição isolada de oscilação de preços. Comparações anuais usam a última fotografia de cada ano, não a soma de saldos mensais. Os recebimentos por ano, por sua vez, abrangem o ano civil.

Não usamos câmbio atual para inventar equivalentes históricos de proventos: a decomposição fica indisponível quando há recebimentos em outra moeda no intervalo. Mudança da moeda-base, fluxo externo sem equivalente ou fotografias desatualizadas também limita as comparações.

**Métricas definidas, ainda sem percentual fictício:** o resultado econômico acima é monetário. TWR exige avaliações nos fluxos externos para neutralizar aportes; XIRR exige uma série completa de fluxos datados, saldo inicial e patrimônio final, além de tratar casos sem solução única. Fotografias mensais e saldos iniciais incompletos não garantem essas condições. Essas taxas não são exibidas como aproximações exatas nesta etapa.

## Arquitetura e atualização

Mantidos os projetos da solution, namespaces com chaves, DTOs de valores decimais como strings, validações da Application e transações serializáveis da Infrastructure. `IncomeAnalysisService` centraliza os agrupamentos; `HistoryCalculator` calcula a decomposição; `MovementRecorder` verifica dependências e fechamento antes de persistir os efeitos. Eventos e reaberturas reutilizam o registro auditável e a idempotência.

Migration `ClosingsFeesAndSnapshotVersions`: taxas com valor inicial zero; campos de estado e versão nas fotografias; versões anteriores em JSON. Os valores financeiros existentes não são recalculados. Fotografias que comprovadamente receberam movimentos abrangidos após a captura são sinalizadas. A reversão da migration é bloqueada quando apagaria taxas ou versões auditáveis.

Menu: Dashboard, Carteira, Compras e vendas, Proventos, Movimentações, Evolução e fechamentos, Simular aporte, Metas, Patrimônio externo, Ativos e demais cadastros.

## Validação em 13/09/2026

138 testes unitários C#, 105 de integração, 55 Angular e 6 E2E aprovados (304 no total). O E2E percorreu desktop e celular emulado, incluindo taxas, evento, reabertura, versões e agrupamento anual de recebimentos. Build Angular de produção aprovado; EF sem alterações de modelo pendentes.

A migration foi aplicada ao banco local após backup com restauração de teste e `DBCC CHECKDB`. A comparação em memória antes/depois confirmou a preservação de posições, patrimônio externo, negociações, proventos, fluxos, movimentos auditáveis e conteúdo financeiro das fotografias. Nenhuma operação de teste foi inserida no banco pessoal. Backups permanecem ignorados pelo Git; nenhum commit foi realizado. Reinicie API e frontend no Visual Studio.
