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

São suportadas compras, vendas, recebimentos de proventos, depósitos, retiradas, transferências na mesma moeda e ajustes de saldo. Uma compra pode criar a posição de um ativo já cadastrado. Ajustes absolutos preservam o total informado pelo ajuste, não a diferença antiga; a simulação avisa quando isso ocorre.

Originais permanecem imutáveis. O lote registra motivo e instante de execução, estornos vinculados aos originais e novos lançamentos vinculados ao lote e aos registros substituídos. Não é necessário executar estornos manualmente para os casos suportados.

**Datas:** o estorno comum continua com a data de hoje. No lote retroativo, o estorno recebe a data efetiva do original, anulando seu efeito naquele período; a data/hora de gravação registra quando a correção ocorreu. O substituto recebe a data correta informada. As séries líquidas são corrigidas; totais brutos recebidos e estornados continuam mostrando a trilha de auditoria.

Os períodos afetados são reabertos e as fotografias marcadas como desatualizadas. Conteúdo, câmbio congelado e versões anteriores não são modificados. Não se reconstrói fotografia histórica com preços de hoje.

## Proteções e limites

- A simulação não grava nada. O token identifica os dados simulados e o estado da carteira; qualquer mudança relevante exige nova simulação.
- A aplicação usa o mesmo bloqueio de carteira das operações normais e transação serializável. Uma falha intermediária desfaz todo o lote. Repetir a mesma solicitação após falha de rede retorna o resultado anterior sem duplicar operações; reutilizar o identificador com outros dados é recusado.
- A cadeia é desfeita pela ordem inversa de registro e reaplicada por data e ordem original. Um novo lançamento entra antes dos existentes na mesma data, pois não existe horário efetivo das operações.
- Vendas sem quantidade disponível, saídas sem saldo, excesso de precisão/limites numéricos, referências removidas ou de outra carteira e divergência entre saldos atuais e efeitos auditados bloqueiam a aplicação.
- Saldos iniciais, ajustes de posição, eventos societários e registros legados sem efeitos suficientes não são reinterpretados automaticamente. Se entrarem na cadeia afetada, o sistema pede conferência manual. Proporções de desdobramento/grupamento e a intenção de ajustes de posição precisam ser definidas antes de automatizar esses casos.
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

Antes de atualizar outra instalação, gere backup e aplique migrations conforme [datas e backup](datas-e-backup.md).

## Validação da primeira etapa em 02/10/2026

153 testes unitários C#, 111 de integração e 60 do frontend aprovados. Build Angular de produção aprovado. Os oito cenários E2E passaram em desktop e celular emulado, incluindo consulta da prévia e comparação dos saldos/histórico antes e depois. Os testes existentes receberam esperas explícitas pelo formulário de setor e pela abertura modal na edição de ativo para evitar preenchimento antes de a interface estar pronta.

Integração e E2E usaram bancos temporários com dados sintéticos. Não houve migration nem alteração da base pessoal nesta entrega.

## Validação da segunda etapa em 02/10/2026

**349 testes aprovados:** 160 unitários C#, 116 de integração, 63 do frontend e dez cenários E2E em desktop/celular emulado. Build Angular de produção aprovado; modelo e migrations consistentes.

Os testes cobrem recálculo de compra seguida de venda, inserção de compra atrasada, proventos reaplicados sem duplicação líquida, equivalentes históricos obrigatórios, preservação de fotografias, confirmação explícita, repetição idempotente e rejeição de prévia desatualizada. Uma falha sintética após gravar o cabeçalho confirmou o rollback integral. Duas confirmações concorrentes produziram uma aplicação e um conflito.

A migration dos vínculos auditáveis foi aplicada no banco local depois de backup com restauração e integridade validadas. Os registros existentes de posições, saldos, operações, proventos/conversões, fluxos, fotografias, movimentos e efeitos foram comparados em memória e permaneceram iguais. Nenhuma correção foi executada na carteira pessoal; os testes financeiros usaram dados sintéticos em bancos temporários.
