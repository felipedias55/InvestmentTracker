# Recebimentos e câmbio histórico de proventos

## Registro e dinheiro

Em **Proventos**, selecione carteira, ativo, data e valor líquido total na moeda do ativo. Use vírgula nos decimais. O recebimento aumenta o acumulado da posição; não altera quantidade, custo nem cotação. Posições zeradas podem receber pagamentos após uma venda.

Escolher um saldo de patrimônio externo credita o dinheiro na mesma carteira e moeda do ativo. “Fora dos saldos da carteira” apenas registra o rendimento. Nenhuma opção gera aporte ou retirada. O acumulado inicial já cadastrado não deve ser lançado novamente como recebimento.

Datas futuras são recusadas. Atrasos em períodos fechados seguem reabertura e verificação das dependências descritas em [fechamentos](fechamentos-proventos-e-eventos.md). A última operação em um ativo independente não impede, por si só, um recebimento anterior. Fotografias afetadas são marcadas como desatualizadas, preservando seus valores.

## Equivalente histórico

Para novos recebimentos:

1. Na mesma moeda-base, o equivalente é o valor original e a taxa é 1.
2. Em outra moeda, o equivalente líquido informado tem precedência. A taxa exibida é derivada desse valor; o equivalente com quatro casas decimais é o valor autoritativo.
3. Sem equivalente informado, o sistema consulta `GET /v2/rate/{origem}/{destino}?date=AAAA-MM-DD` no Frankfurter.
4. A referência deve ser positiva, corresponder às moedas pedidas e estar entre a data solicitada e os sete dias anteriores. A data retornada permanece visível. Uma referência futura ou mais antiga não é aceita.
5. Sem cotação adequada, incluindo falha de rede, o recebimento é salvo normalmente **com conversão pendente**. Nunca se consulta o endpoint de taxa atual para preencher o passado.

Uma consulta pública de USD/BRL para 07/09/2025 (domingo) confirmou a disponibilidade de uma referência datada. Isso não garante cobertura de todo par e toda data. A API pode fornecer referência do próprio dia ou anterior; preservamos a data que ela informa, sem afirmar que representa uma negociação intradiária. Documentação: [Frankfurter v2](https://frankfurter.dev/).

O registro congela a moeda-base, equivalente, taxa quando representável, data de referência, origem, instante de gravação e motivo. Não há atualização automática desses valores em consultas posteriores. O reenvio do recebimento não consulta novamente a cotação nem duplica o crédito.

## Complementação e correção auditável

Use **Conversão #id** na linha do recebimento. Escolha uma moeda cadastrada, informe o equivalente real ou deixe vazio para consultar o histórico, e descreva o motivo.

- Cada versão é imutável e numerada por recebimento/moeda. As anteriores continuam consultáveis.
- A versão esperada impede que duas correções concorrentes sobrescrevam uma à outra; reenvios com o mesmo identificador não duplicam versões.
- A complementação aparece em Movimentações como “Conversão de provento”. Ela não altera saldos, acumulados, aportes, valores originais ou conteúdo das fotografias.
- Equivalentes para moedas diferentes coexistem. Alterar a moeda-base da carteira não muda a moeda-base registrada no recebimento nem apaga conversões anteriores.
- É possível corrigir a conversão de um recebimento já estornado. Original e estorno usam a mesma última versão para cada moeda, evitando diferença artificial de câmbio.
- Corrigir somente o equivalente não exige estornar o recebimento financeiro. Uma correção da quantidade de dinheiro efetivamente recebida continua usando estorno e novo recebimento.

A migration não atribui o câmbio de hoje a registros antigos e não deduz qual era a moeda-base da carteira na época. Valores antigos na própria moeda podem ser usados diretamente; os demais precisam de complementação.

## Análises e fotografias

A página oferece moedas originais ou moeda-base, agrupamento mensal/anual/por ativo, e filtros de ativo, ano e moeda original. Um grupo com qualquer equivalente faltante mostra “Conversão pendente”, sem apresentar a soma parcial como total.

A evolução usa o equivalente histórico na moeda de cada fotografia. Complementações podem liberar ou atualizar comparações derivadas, mas não reescrevem a fotografia. Estornos aparecem na data da correção e usam o equivalente do recebimento, não uma taxa nova. Proventos retidos já integram o patrimônio; proventos recebidos fora dele entram somente no resultado econômico. Troca de moeda-base entre fotografias, lacunas ou fotografias desatualizadas continuam impedindo comparações incompatíveis.

## Arquitetura, instalação e diagnóstico

- `GET/POST /api/portfolios/{portfolioId}/income`: lista e registra recebimentos.
- `POST /api/portfolios/{portfolioId}/income/{id}/conversions`: complementa/corrige o equivalente.
- `GET /api/portfolios/{portfolioId}/income/analysis`: totais originais e convertidos, sem acesso à API de câmbio durante a consulta.
- Application: `IncomeService`, `IncomeConversionService`, `IncomeAnalysisService` e `HistoryCalculator`.
- Domain/Infrastructure: `IncomeReceipt`, `IncomeConversion`, configurações EF e migration `IncomeHistoricalConversions`.

O recebimento e seus saldos são gravados atomicamente; a conversão posterior e seu registro auditável também. Ambos usam o bloqueio serializável por carteira. Valores monetários usam decimal no servidor e strings nos DTOs.

Após backup validado, aplique as migrations e reinicie:

```powershell
dotnet ef database update --project InvestmentTracker.Infrastructure --startup-project InvestmentTracker.Api
```

O erro `Invalid object name 'IncomeConversion'` indica uma estrutura incompatível com o código. Nesta retomada, a causa foi a implementação interrompida antes da geração da migration. A inicialização agora detecta modelo sem migration e migrations pendentes, com orientação explícita; não aplica alterações no banco silenciosamente. Essa verificação não substitui a integridade física do banco caso tabelas sejam removidas manualmente.
