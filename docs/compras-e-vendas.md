# Compras e vendas automáticas

## Uso

Acesse **Compras e vendas** pelo menu ou pelos atalhos Comprar/Vender de uma posição. Escolha a carteira, o ativo, a data, a quantidade e o preço por unidade, sempre na moeda do ativo e com números no padrão brasileiro.

Ao confirmar, uma única transação do banco registra a operação, atualiza a posição e movimenta o dinheiro. Não é necessário cadastrar o aporte ou retirada novamente no histórico.

- Compra com **dinheiro novo**: cria a posição, se necessário, aumenta quantidade e custo e gera um aporte automaticamente.
- Compra com **saldo de patrimônio externo**: reduz esse saldo, aumenta a posição e registra a compra, sem gerar um aporte externo.
- Venda com **retirada da carteira**: reduz a posição e gera uma retirada automaticamente.
- Venda para **saldo de patrimônio externo**: credita o saldo escolhido para reinvestimento; registra a venda sem retirada externa.

O saldo usado para reinvestir precisa ser da mesma carteira e moeda do ativo, e representar dinheiro efetivamente disponível. Uma compra é rejeitada se o saldo for insuficiente. Não há conversão automática entre contas de moedas diferentes.

## Cálculo

Total da operação = quantidade × preço unitário, arredondado a quatro casas com `AwayFromZero`. Quantidades aceitam seis casas. Os cálculos persistidos usam `decimal` no backend; a estimativa visual não é usada para gravação.

Na compra, o custo remanescente recebe o total comprado. Na venda, o custo remanescente é reduzido proporcionalmente à quantidade (custo médio). O total vendido é o fluxo de dinheiro, não a redução do custo.

Exemplo: 20 unidades com custo total de 500; vender 5 por 40 gera 200 de venda e deixa 15 unidades com custo de 375. O valor atual da posição é recalculado pelo preço da operação: 15 × 40 = 600.

A venda total deixa quantidade, custo e valor atual zerados. A posição e seus proventos acumulados são preservados. Vendas superiores à quantidade disponível são recusadas.

## Datas e câmbio

A data da operação pode ser até hoje e não pode anteceder a última operação ou fotografia da carteira. O histórico não é reconstruído retroativamente. Fotografias já salvas não são alteradas por uma operação; atualize a do mês corrente quando desejar um novo fechamento.

`UpdatedOn` recebe o dia da gravação em São Paulo. A data efetiva da negociação fica no registro da operação.

Movimentos externos na moeda-base usam o próprio total como equivalente. Em moeda estrangeira, pode ser informado o equivalente total real na moeda-base. Para operações de hoje, na ausência desse valor, utiliza-se a cotação disponível no serviço de câmbio. Para outra data, ou sem taxa disponível, o equivalente deve ser informado e a operação inteira é recusada até isso acontecer. O valor equivalente é congelado no movimento; não é recalculado nas consultas.

## Dados existentes e correções

Posições e movimentos anteriores são preservados, sem gerar compras fictícias. O formulário da carteira continua disponível para **saldo inicial, ajustes e atualização de valores/proventos**, sem gerar transações implícitas. Use Compras e vendas para novas operações reais.

Os movimentos gerados têm vínculo com a operação e não podem ser editados ou apagados isoladamente. O histórico manual permanece para outros fluxos e registros antigos. Não duplique nele os movimentos automáticos.

Nesta versão, não existe edição, cancelamento ou estorno de operações confirmadas. Confira os dados antes de confirmar. Correções auditáveis de operações, taxas de negociação, eventos corporativos e importação de corretoras são evoluções posteriores. Não há cálculo fiscal ou integração que execute ordens em corretoras.

## Arquitetura e persistência

- API: `GET/POST /api/portfolios/{portfolioId}/trades`.
- Application: `ITradeService`, `TradeService`, DTOs e contrato específico de repositório.
- Domain: `PortfolioTrade` e vínculo opcional em `PortfolioCashFlow`.
- Infrastructure: configuração EF e `TradeRepository`, que coordena a transação serializável com bloqueio da carteira.
- Frontend: página standalone Angular, com atalhos na carteira e ligação ao histórico.

Foi mantida a solution do Visual Studio, a separação em camadas e namespaces C# com chaves. A migration `PortfolioTrades` apenas cria a estrutura de operações e o vínculo opcional; não transforma registros existentes em operações.

O identificador da solicitação evita duplicação em reenvios. Reutilizar o identificador com dados diferentes produz conflito. Operações simultâneas na mesma carteira são serializadas. Valores financeiros de posições e saldo externo possuem verificação otimista de concorrência para impedir que uma gravação manual em andamento sobrescreva uma operação.

## Validação e próximos passos

A suíte cobre compra em posição nova e existente, custo na venda parcial e total, proventos preservados, reinvestimento, saldo insuficiente, carteira/moeda incorretas, repetição de requisições, concorrência, valores fracionários, limites, câmbio histórico, proteção de movimentos gerados e preservação de fotografias. Os testes Angular cobrem envio com precisão brasileira, prevenção de duplo envio e reenvio com o mesmo identificador.

A suíte [E2E no navegador](testes-e2e.md) foi executada em desktop e celular emulado, com API real e uma base criada do zero pelas migrations. Para a homologação, continuam a conferência final dos dados com a planilha, a revisão em aparelhos físicos e a validação da instalação em outra máquina seguindo o README. Docker, CI/CD e publicação permanecem adiados. O cancelamento/estorno auditável de operações é a próxima melhoria funcional recomendada para este fluxo.

## Resultado da entrega em 08/09/2026

Migration `20260908231723_PortfolioTrades` aplicada à instalação local. Backups completos anteriores e posteriores à migration foram restaurados em bases temporárias e passaram pelo `DBCC CHECKDB`.

Validação final: 133 testes unitários do backend, 84 de integração e 40 do frontend aprovados, além do build Angular de produção. Reinicie API e frontend no Visual Studio para utilizar a versão atualizada. Não foram inseridas operações de teste na carteira real.
