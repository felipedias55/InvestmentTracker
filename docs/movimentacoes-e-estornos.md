# Movimentações e estornos auditáveis

## Fluxo de uso

Use **Movimentações** para depósitos, retiradas, transferências internas e ajustes de saldo. Compras/vendas e recebimentos de proventos continuam nas telas próprias e entram automaticamente no mesmo histórico. Não registre novamente os aportes gerados por compras.

| Operação | Saldo | Histórico de aportes/retiradas |
|---|---|---|
| Depósito | Acrescenta o valor ao saldo escolhido | Registra aporte |
| Retirada | Desconta o valor disponível | Registra retirada |
| Transferência | Desconta da origem e credita no destino | Não gera aporte nem retirada |
| Ajuste | Define o novo total informado, com motivo obrigatório | Não representa entrada/saída de dinheiro |
| Compra/venda | Mantém as regras de posição e origem/destino existentes | Movimento externo somente quando há dinheiro novo ou retirada |
| Provento | Atualiza o acumulado e, opcionalmente, o saldo | Não gera aporte |

Transferências nesta versão são entre saldos da mesma carteira e da mesma moeda. Conversão cambial e transferências entre carteiras ficam para uma evolução própria. Para depósitos/retiradas em moeda estrangeira, informe o equivalente histórico na moeda-base; na data de hoje o serviço de câmbio pode preenchê-lo. O equivalente é preservado no registro.

O cadastro de patrimônio externo informa o **saldo inicial**. A edição posterior altera nome e descrição; valores passam a ser alterados em Movimentações e a moeda não pode ser trocada. Saldos referenciados pelo histórico não podem ser excluídos. Posições ainda podem receber atualização manual de valores/proventos, que também registra os valores antes e depois como ajuste de posição.

## Como estornar

1. Localize o lançamento em **Movimentações**.
2. Selecione **Estornar #número** e informe o motivo.
3. Confirme. O sistema restaura os efeitos registrados e cria um novo lançamento ligado ao original.

O original permanece imutável, identificado como estornado. O histórico exibe saldos antes/depois e, para posições, quantidade, custo e proventos. O estorno tem identificador próprio, motivo e data/hora de registro. Sem autenticação, não há identificação de um usuário individual responsável.

O estorno comum é registrado na data atual de São Paulo. Não reabre nem recalcula fotografias anteriores. Se a fotografia do mês corrente precisar refletir a correção, substitua-a explicitamente após conferir os valores. O [lote de correção retroativa](correcoes-atrasadas.md) tem fluxo próprio: simula, confirma e anula os originais nas suas datas efetivas, com vínculos auditáveis e reabertura dos períodos afetados. A data/hora de gravação identifica quando o lote foi aplicado.

Um aporte estornado reduz os aportes; uma retirada estornada reduz as retiradas. Isso não cria dinheiro novo na categoria oposta. Quando o original pertence a outro mês, a correção aparece no mês do estorno e o total líquido daquela categoria pode ficar negativo. A moeda e o equivalente originais são preservados. As comparações históricas continuam sendo variação patrimonial descontada dos fluxos, não rentabilidade percentual.

## Proteções

- Motivo obrigatório; registros originais nunca são apagados pelo estorno.
- Reenvio com o mesmo identificador e conteúdo não aplica a operação duas vezes.
- Solicitações simultâneas são serializadas por carteira, junto com compras, vendas, proventos e fotografias.
- Movimentações posteriores que afetaram a mesma posição ou saldo precisam ser estornadas primeiro. Lançamentos independentes não bloqueiam.
- Se os valores atuais diferirem dos valores registrados após a operação, o estorno é recusado; não sobrescreve ajustes desconhecidos.
- Não há estorno de estorno. Para corrigir novamente, registre a operação correta.
- Saldo insuficiente, referência de outra carteira, transferência para o mesmo saldo ou moeda diferente são recusados sem gravação parcial.
- Os movimentos gerados no histórico de aportes/retiradas não podem ser editados ou removidos separadamente.

## Registros anteriores à atualização

A migration inclui compras, vendas, proventos e movimentos históricos existentes no índice unificado, sem alterar valores da carteira ou inventar saldos anteriores. Compras e proventos anteriores à auditoria **não possuem estorno automático**, pois não há informação suficiente para recuperar os valores exatos. A tela indica essa limitação. Use ajustes justificados quando necessário.

Registros históricos manuais de aporte/retirada podem ser estornados sem alterar saldos, porque nunca os alteraram. O formulário antigo fica recolhido em Evolução e fechamentos e deve ser usado somente para histórico antigo, já refletido nos saldos. Novos fluxos de dinheiro devem usar Movimentações.

## Arquitetura e atualização

- Application: `IMovementService`, `MovementService`, contratos específicos e regras de movimentação/estorno.
- Domain: `FinancialMovement`, `MovementEffect` e vínculo/classificação em `PortfolioCashFlow`.
- Infrastructure: repositório transacional, captura dos valores efetivamente alterados e configurações EF.
- API: `GET/POST /api/portfolios/{portfolioId}/movements` e `POST /api/portfolios/{portfolioId}/movements/{id}/reversal`.
- Frontend: página standalone Angular; mesma arquitetura do Visual Studio e namespaces C# com chaves.

Faça backup validado antes de aplicar as migrations `AuditableMovements` e `ReversalCashFlowClassification`. Em outra instalação:

```powershell
dotnet ef database update --project InvestmentTracker.Infrastructure --startup-project InvestmentTracker.Api
```

As migrations contêm estrutura e transformação genérica de registros existentes, sem valores financeiros pessoais embutidos. Rollback que descartaria auditoria ou classificação de estorno é bloqueado. Backups e arquivos do banco permanecem fora do Git.

Os testes incluem conservação de saldo, precisão do custo na venda, dependências, concorrência, reenvios, motivos obrigatórios, moedas, fotografias congeladas e atualização de uma base anterior com preservação dos registros. Os E2E cobrem os novos fluxos em desktop e celular emulado.

## Validação da entrega em 11/09/2026

135 testes unitários C#, 101 de integração, 52 Angular e 6 E2E aprovados; build de produção concluído. Migrations aplicadas à instalação local após backup restaurado e validado. A comparação dos registros antes/depois confirmou preservação de posições, saldos, operações, recebimentos, fluxos originais e fotografias. Nenhuma movimentação de teste foi inserida na carteira real.

Conversões de proventos são registros auditáveis sem alteração de saldo. Para corrigir, registre outra versão em Proventos; não são estornadas como uma operação financeira. Veja [câmbio histórico](recebimento-de-proventos.md).
