# Recebimento de proventos

Em **Proventos**, selecione a carteira, o ativo, a data e o valor líquido total recebido na moeda do ativo. A observação permite identificar dividendos, JCP, rendimentos de fundos ou outros pagamentos. Use vírgula nos decimais.

O recebimento aumenta automaticamente os proventos acumulados da posição. Quantidade, custo investido e valor atual do ativo permanecem iguais. Posições zeradas também podem receber pagamentos após uma venda.

## Destino do dinheiro

- **Saldo de patrimônio externo:** credita o valor no saldo escolhido, que deve pertencer à mesma carteira e usar a mesma moeda do ativo. Para reinvestir, use esse saldo como origem em Compras e vendas.
- **Fora dos saldos da carteira:** registra o recebimento e aumenta o acumulado de proventos, sem creditar dinheiro no patrimônio acompanhado.

Nenhuma das opções cria aporte ou retirada. O acumulado de proventos continua separado do patrimônio total; somente o dinheiro creditado em um saldo entra no patrimônio, evitando dupla contagem.

## Histórico e limites

O histórico preserva data, ticker, moeda, valor, destino e observação. Os valores acumulados já cadastrados continuam como saldo inicial, sem inventar recebimentos antigos. Registre somente valores ainda não incluídos nesse acumulado.

Os recebimentos devem seguir a ordem cronológica: não podem preceder a última compra, venda, recebimento ou fotografia. Datas futuras são recusadas. Fotografias existentes não são recalculadas; uma fotografia do mês corrente pode ser substituída explicitamente após o recebimento.

Recebimentos confirmados não possuem edição ou estorno nesta versão, assim como as compras e vendas. O campo manual de proventos na carteira permanece disponível para saldo inicial e ajustes; não deve ser preenchido novamente com o valor de um recebimento já registrado. Uma posição com recebimentos vinculados não pode ser excluída, preservando o histórico.

## Arquitetura e instalação

- API: `GET/POST /api/portfolios/{portfolioId}/income`.
- Application: `IncomeService`, DTOs e interfaces.
- Domain: `IncomeReceipt`.
- Infrastructure: configuração EF, migration `20260909135902_IncomeReceipts` e repositório transacional.
- Frontend: página Angular standalone com seleção de carteira, saldo e histórico.

O registro, o acumulado e o crédito no saldo são gravados na mesma transação. O bloqueio de carteira é compartilhado com compras e vendas. O identificador da solicitação impede duplicação em reenvios; reutilizá-lo com outro conteúdo produz conflito.

Em outra instalação, faça backup e aplique a migration antes de usar a página:

```powershell
dotnet ef database update --project InvestmentTracker.Infrastructure --startup-project InvestmentTracker.Api
```

Os testes cobrem recebimentos em posição zerada, saldo inicial preservado, crédito opcional, valores inválidos, referências incorretas, idempotência, concorrência e fotografias. A suíte E2E também registra recebimentos em desktop e celular emulado.
