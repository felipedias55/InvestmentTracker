# Correções atrasadas — diagnóstico, simulação e aplicação

## Diagnóstico de dependências

Em **Movimentações**, use **Analisar correção #N** para um registro existente ou **Analisar lançamento atrasado** para uma operação esquecida. Informe a data pretendida e as referências envolvidas. Uma correção existente já inclui a posição e os saldos registrados no original; se a correção envolver novas referências, selecione-as também. Transferências podem indicar dois saldos.

A consulta lista movimentos relacionados, dependências indiretas e fotografias que precisam de revisão. Exibe alertas para estornos, registros legados sem efeitos suficientes e ajustes absolutos. A ordem de conferência considera a ordem de gravação usada pelas regras atuais de dependência; não autoriza estornos nem constitui um plano executável.

O diagnóstico **não executa correções** nem grava movimentos. Para calcular novos valores e aplicar, use o fluxo abaixo. Os fluxos normais de registro e estorno mantêm suas proteções.

## Simular e aplicar

1. Em **Movimentações**, use **Corrigir #N** em uma operação ativa ou **Registrar operação atrasada** para uma operação esquecida.
2. Informe os dados completos corretos: tipo, data, ativo e/ou saldos, quantidade/preço/taxas ou valor. Informe um motivo.
3. Para fluxos de dinheiro ou proventos em moeda estrangeira, informe o equivalente histórico na moeda-base atual. Alterar data, ativo, quantidade, preço, taxas ou valor limpa o equivalente para nova conferência. Não há consulta ao câmbio atual nesse fluxo.
4. Clique em **Simular correção**. Confira as quantidades, custos, valores e proventos antes/depois, as fotografias afetadas e os avisos. Uma simulação bloqueada informa a causa e não oferece confirmação.
5. Marque que conferiu os impactos e clique em **Confirmar correção**. O sistema revalida a simulação e aplica o lote inteiro em uma transação.

São suportadas compras, vendas, recebimentos de proventos, depósitos, retiradas, transferências na mesma moeda, ajustes de saldo e, desde 28/10/2026, desdobramentos, grupamentos e bonificações auditados. Uma compra pode criar a posição de um ativo já cadastrado. Ajustes absolutos preservam o total informado pelo ajuste, não a diferença antiga; a simulação avisa quando isso ocorre.

### Eventos societários e ajustes posteriores

Para inserir um evento atrasado, selecione **Desdobramento**, **Grupamento** ou **Bonificação** no mesmo painel Material de correção. Eventos ativos também oferecem **Corrigir #N** nos detalhes do histórico.

- Desdobramento/grupamento: informe **Novas unidades** e **Unidades antigas de referência**, conforme o comunicado. Por exemplo, 2 e 1 significam duas novas unidades para cada antiga. O backend aplica a proporção à quantidade reconstruída na data, preservando custo total, valor de mercado e proventos. A direção precisa coincidir com o tipo do evento.
- Eventos antigos gravados com quantidade final só são reaplicados quando payload e efeitos auditados são consistentes. A proporção é recuperada das quantidades antes/depois e aparece nos avisos da simulação para conferência. Eventos substitutos passam a guardar a proporção explícita.
- Bonificação: informe a quantidade adicional e o custo total adicional na moeda do ativo. Não há criação de saldo em dinheiro nem de provento. Nos relançamentos, preservam-se esses valores declarados, sem presumir uma porcentagem ausente do registro original.
- Frações que excedam seis decimais, quantidade acima do limite, proporção inválida ou auditoria incompatível bloqueiam a simulação inteira. Cálculos de proporção usam inteiros escalados, evitando arredondamento silencioso. Não há tratamento automático de leilão/liquidação de frações, impostos ou novos tipos de evento.

A cadeia também pode atravessar ajustes auditados de posição e cotações em lote, inclusive de várias posições. Ajustes manuais mantêm como totais os campos que alteraram originalmente; campos não alterados acompanham o recálculo. Cotações em lote mantêm o **preço unitário confirmado**, a identificação de origem disponível e recalculam o valor pela nova quantidade com a regra de arredondamento já utilizada nas cotações. Os dados de intenção são preservados no payload dos substitutos para correções posteriores, inclusive se um ajuste se tornar numericamente neutro.

O ajuste de posição não virou um novo formulário de edição retroativa: a alteração direta continua na Carteira. Nesta entrega ele é suportado como etapa da cadeia, enquanto eventos podem ser inseridos e corrigidos no painel. Registros sem efeitos suficientes, payload de cotação incompatível, referências removidas e saldos iniciais continuam exigindo conferência manual.

Originais permanecem imutáveis. O lote registra motivo e instante de execução, estornos vinculados aos originais e novos lançamentos vinculados ao lote e aos registros substituídos. Não é necessário executar estornos manualmente para os casos suportados.

**Datas:** o estorno comum continua com a data de hoje. No lote retroativo, o estorno recebe a data efetiva do original, anulando seu efeito naquele período; a data/hora de gravação registra quando a correção ocorreu. O substituto recebe a data correta informada. As séries líquidas são corrigidas; totais brutos recebidos e estornados continuam mostrando a trilha de auditoria.

Os períodos afetados são reabertos e as fotografias marcadas como desatualizadas. Conteúdo, câmbio congelado e versões anteriores não são modificados. Não se reconstrói fotografia histórica com preços de hoje.

## Proteções e limites

- A simulação não grava nada. O token identifica os dados simulados e o estado da carteira; qualquer mudança relevante exige nova simulação.
- A aplicação usa o mesmo bloqueio de carteira das operações normais e transação serializável. Uma falha intermediária desfaz todo o lote. Repetir a mesma solicitação após falha de rede retorna o resultado anterior sem duplicar operações; reutilizar o identificador com outros dados é recusado.
- A cadeia é desfeita pela ordem inversa de registro e reaplicada por data e ordem original. Um novo lançamento entra antes dos existentes na mesma data, pois não existe horário efetivo das operações.
- Vendas sem quantidade disponível, saídas sem saldo, excesso de precisão/limites numéricos, referências removidas ou de outra carteira e divergência entre saldos atuais e efeitos auditados bloqueiam a aplicação.
- Saldos iniciais e registros legados sem efeitos suficientes não são reinterpretados automaticamente. Eventos e ajustes de posição só são reaplicados com os dados e a auditoria compatíveis descritos acima. Uma cadeia bloqueada não gera aplicação parcial nem valores presumidos para completar histórico ausente.
- Operações já estornadas não podem ser corrigidas novamente; selecione o substituto ativo. O cabeçalho do lote não possui um estorno próprio.
- Equivalentes dos fluxos e versões de conversão dos proventos reaplicados são preservados. A operação corrigida usa os dados confirmados pelo usuário. Uma mudança de moeda-base pode exigir informar outro equivalente.
- A reconstituição parte dos efeitos auditados disponíveis. Preços atuais seguem a mesma regra das operações normais (último preço de operação na sequência recalculada); não há busca de cotações históricas nem cálculo de impostos.

## Regras da análise

- A data inicial é a mais antiga entre a data original e a pretendida. Para lançamento novo, é a data informada.
- A análise é conservadora: considera o dia completo porque não existe horário efetivo de cada operação, além de dependências pela ordem de registro.
- Compartilhar posição ou saldo estabelece dependência potencial. O conjunto é expandido até incluir as referências conectadas por outros movimentos; identificadores de posição e saldo são tratados separadamente.
- Registros de reabertura e conversão de proventos não movimentam saldos e não entram nessa cadeia. Seus fluxos próprios permanecem disponíveis.
- Originais estornados e seus estornos permanecem visíveis para auditoria; não são sugeridos para novo estorno. Efeitos desconhecidos geram aviso de análise incompleta, sem deduzir valores.
- Fotografias desde a data potencialmente afetada são listadas sem alterar conteúdo, versões ou indicadores. A necessidade de reabertura acompanha a regra de fechamento posterior à data da operação; uma fotografia do mesmo dia também exige conferência.
- Alterar o formulário, fechar a prévia ou recarregar a carteira descarta o resultado. A consulta não reserva a carteira: se outra sessão registrar algo, consulte novamente.

## Arquitetura e API

`POST /api/portfolios/{portfolioId}/movements/correction-preview` recebe `date` e, opcionalmente, `movementId`, `positionId`, `cashAssetId` e `destinationId`. Para uma operação nova, é necessário ao menos uma posição ou saldo. Todas as referências são validadas dentro da carteira. Apesar de usar POST para receber o formulário, o endpoint só consulta dados.

O diagnóstico fica em Application e reutiliza interfaces de repositórios existentes. Não expõe o payload interno dos movimentos nem faz chamadas ao provedor de câmbio.

Endpoints da execução:

- `GET .../movements/{id}/correction-draft`: dados originais editáveis.
- `POST .../movements/correction-simulation`: motivo, original opcional e operação completa; retorna token, impactos e impedimentos.
- `POST .../movements/corrections`: `requestId`, token e a mesma entrada simulada; retorna o resultado e o identificador do lote.

`CorrectionCalculator` em Application calcula um plano em memória sem alterar entidades de origem. `CorrectionRepository` em Infrastructure lê o estado consistente, verifica idempotência/concorrência e persiste o plano. A migration `20261003002303_AuditableCorrectionLinks` adiciona `CorrectionId` e `ReplacesMovementId` em FinancialMovement; registros antigos recebem nulo. O downgrade é bloqueado quando já há correções auditadas.

A ampliação de 28/10/2026 mantém essa divisão e os mesmos endpoints. `CorrectionOperation` recebeu `ratioNumerator`, `ratioDenominator` e `cost` (decimais transmitidos como strings). Novos payloads auditáveis registram proporções de eventos e os campos/preços que os ajustes devem preservar. Não há alteração de schema nem nova migration nesta ampliação.

Antes de atualizar outra instalação, gere backup e aplique migrations conforme [datas e backup](datas-e-backup.md).

## Validação da ampliação em 28/10/2026

**611 testes aprovados:** 233 unitários C#, 169 de integração, 133 Angular, 64 de apresentação e 12 E2E. Lint, formatação de configurações e build Angular aprovados; pacote inicial mantido em 455,77 kB, abaixo do limite de 500 kB.

Novos testes cobrem proporções, direção do evento, frações incompatíveis, bonificações, payload de evento inconsistente, ajustes manuais versus preço unitário e preservação da intenção em correções sucessivas. A integração exercita compra, desdobramento, venda e cotação de várias posições, confirma idempotência e imutabilidade das fotografias e permite corrigir novamente o evento substituto. Os três eventos atrasados foram testados sem criação indevida de dinheiro, proventos ou fluxos.

O E2E real foi ampliado para inserir um desdobramento atrasado e corrigir sua proporção pela interface, em desktop e celular emulado. Quatro cenários adicionais de apresentação verificam formulário Material, campos condicionais, confirmação, ausência de rolagem horizontal e bloqueios em claro/escuro e desktop/celular. Imagens foram conferidas em celular claro e desktop escuro. Os cálculos permanecem em Application; Angular apresenta o formulário e os impactos retornados pelo backend.

Integração e E2E utilizaram bancos temporários; a base E2E foi removida ao concluir. Nenhuma correção foi aplicada à carteira pessoal, e esta ampliação não exige migration.

## Validação da primeira etapa em 02/10/2026

153 testes unitários C#, 111 de integração e 60 do frontend aprovados. Build Angular de produção aprovado. Os oito cenários E2E passaram em desktop e celular emulado, incluindo consulta da prévia e comparação dos saldos/histórico antes e depois. Os testes existentes receberam esperas explícitas pelo formulário de setor e pela abertura modal na edição de ativo para evitar preenchimento antes de a interface estar pronta.

Integração e E2E usaram bancos temporários com dados sintéticos. Não houve migration nem alteração da base pessoal nesta entrega.

## Validação da segunda etapa em 02/10/2026

**349 testes aprovados:** 160 unitários C#, 116 de integração, 63 do frontend e dez cenários E2E em desktop/celular emulado. Build Angular de produção aprovado; modelo e migrations consistentes.

Os testes cobrem recálculo de compra seguida de venda, inserção de compra atrasada, proventos reaplicados sem duplicação líquida, equivalentes históricos obrigatórios, preservação de fotografias, confirmação explícita, repetição idempotente e rejeição de prévia desatualizada. Uma falha sintética após gravar o cabeçalho confirmou o rollback integral. Duas confirmações concorrentes produziram uma aplicação e um conflito.

A migration dos vínculos auditáveis foi aplicada no banco local depois de backup com restauração e integridade validadas. Os registros existentes de posições, saldos, operações, proventos/conversões, fluxos, fotografias, movimentos e efeitos foram comparados em memória e permaneceram iguais. Nenhuma correção foi executada na carteira pessoal; os testes financeiros usaram dados sintéticos em bancos temporários.
