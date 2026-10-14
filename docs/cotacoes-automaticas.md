# Cotações automáticas — primeira integração

Implementada em 07/10/2026. Consulta sob demanda no painel de cotações em lote, com revisão e confirmação explícita antes de alterar a carteira. Não há tarefa agendada nem compra de plano.

## Provedores escolhidos

| Mercado | Provedor | Modalidade usada |
| --- | --- | --- |
| Brasil | brapi | Endpoint v2 stocks/quote, um ticker por chamada, autenticação Bearer. Plano gratuito documenta 15.000 requisições/mês, uma consulta simultânea e atraso aproximado de 30 minutos. |
| EUA | Alpha Vantage | GLOBAL_QUOTE sem entitlement: preço de fechamento diário, um ticker por chamada; plano gratuito documenta até 25 requisições/dia. |

Os termos/limites podem mudar. A brapi documenta cotações de ações, FIIs e outros instrumentos, mas a cobertura de cada ticker na conta gratuita precisa ser confirmada na prática. Para EUA, o preço não é intradiário. Não usamos endpoints premium nem ativamos assinaturas ou upgrades.

Fontes consultadas:
- [Plano gratuito brapi](https://brapi.dev/faq/api-e-gratis-mesmo).
- [Cotação v2 e resolução de tickers](https://brapi.dev/docs/acoes/cotacao).
- [Limites Alpha Vantage](https://www.alphavantage.co/support/).
- [GLOBAL_QUOTE](https://www.alphavantage.co/documentation/#latestprice).

## Configuração

Crie as chaves nos sites oficiais, escolhendo somente os planos gratuitos. Não coloque as chaves no Angular, Git ou mensagens do chat. Configure localmente na raiz do projeto:

~~~powershell
dotnet user-secrets set "Quotes:BrapiToken" "SUA_CHAVE_GRATUITA" --project InvestmentTracker.Api
dotnet user-secrets set "Quotes:AlphaVantageKey" "SUA_CHAVE_GRATUITA" --project InvestmentTracker.Api
~~~

Reinicie a API. Sem chave, a tela informa qual provedor falta configurar e não tenta consultar aquele mercado. Cada provedor funciona independentemente do outro.

## Uso

1. Abra Carteira e filtre as posições desejadas.
2. Entre em Atualizar cotações em lote.
3. Deixe vazios os preços que deseja buscar.
4. Clique em Consultar cotações gratuitas.
5. Confira preço, fonte, data e mensagens de cada ativo.
6. Ajuste ou apague preços que não quiser aplicar.
7. Revise e confirme o lote.

A consulta preenche somente campos vazios, não substitui preenchimentos manuais e não salva posições. Falhas ficam com preço vazio. Quantidades, custos investidos e proventos continuam preservados na aplicação do lote.

Se alterar manualmente um preço consultado, sua identificação de fonte/data é removida para não atribuir ao provedor um valor editado.

## Validações e rastreabilidade

- O backend resolve os IDs exclusivamente dentro da carteira indicada.
- Seleção de 1 a 200 posições distintas.
- País cadastrado Brasil + BRL usa brapi; Estados Unidos + USD usa Alpha Vantage. Outras combinações ficam sem suporte, sem inferência automática pelo ticker.
- Símbolos permitidos: letras maiúsculas, números, ponto e hífen, até 20 caracteres.
- Ticker retornado precisa coincidir exatamente; substituição automática de ticker pela brapi é recusada.
- Moeda precisa coincidir. Na Alpha Vantage, USD decorre do escopo restrito a ativos cadastrados nos EUA; o endpoint GLOBAL_QUOTE não retorna um campo de moeda.
- Preço deve ser positivo, ter até quatro casas decimais e produzir total dentro do limite da posição.
- Data é obrigatória, não pode ser futura e deve estar nos últimos sete dias corridos. Essa janela não é um calendário de pregões.
- Dados inválidos, falhas HTTP, falta de chave ou limite gratuito não viram preço zero.
- A data de mercado é exibida antes de aplicar. UpdatedOn continua sendo a data da atualização da posição, não a data da cotação.
- Fonte/data do preço confirmado seguem no RequestPayload da movimentação auditável existente. São contexto do preenchimento confirmado, não uma assinatura criptográfica do provedor.
- A aplicação usa a mesma transação, idempotência e checagem de concorrência das cotações manuais. Fotografias afetadas seguem as regras existentes.

## Cache e consumo

Há um coordenador único por processo da API, com consultas sequenciais. Resultados válidos ficam em memória por 30 minutos para brapi e 12 horas para Alpha Vantage. Falhas ficam por dois minutos para evitar repetição imediata.

Alpha Vantage possui também um teto local de 25 tentativas por dia UTC por processo, intervalo mínimo entre chamadas e interrupção após uma resposta de limite. O cache e o contador são reiniciados ao reiniciar a API. Isso não renova a cota real do provedor; chamadas externas ou outras instâncias também podem consumir a chave. O limite oficial sempre prevalece.

Não há controle persistente da cota mensal brapi nesta primeira etapa. Ao atingir limites, a aplicação informa a falha e mantém a alternativa manual, sem ativar cobranças.

## Privacidade e arquitetura

Somente o ticker e a chave necessária são enviados ao provedor. Valores, quantidades e identificadores da carteira não são enviados. Chaves permanecem no backend; logs do HttpClient de cotações estão desabilitados para não registrar a chave Alpha Vantage presente na URL. Erros externos são substituídos por mensagens locais.

Application coordena a consulta e valida compatibilidade com a posição. Infrastructure implementa HTTP, cache e limites. API expõe POST /api/portfolios/{id}/quotes/preview. Angular apresenta/preenche e solicita confirmação. A gravação usa PUT /api/portfolios/{id}/quotes.

## Validação e pendências

Testes automatizados usam respostas HTTP controladas e bancos temporários. Após a primeira entrega, as chaves foram configuradas localmente, houve consulta real à brapi e o usuário confirmou o funcionamento do fluxo de consulta e gravação. Isso não comprova cobertura de todos os tickers nem consistência de todos os campos retornados pelos provedores.

Próximos passos: ampliar a verificação de tickers reais, inconsistências de dados e eventuais bloqueios do plano gratuito; depois avaliar cache persistente, controle de consumo entre reinícios e mapeamento explícito de instrumentos/símbolos.

Não exige migration.

### Informativo de origem da consulta
Na consulta e revisão do lote, o ícone ⓘ revela a fonte e os horários ao passar o mouse, focar pelo teclado ou tocar no celular. Os horários são apresentados em Brasília. O horário de mercado da brapi é preservado; na Alpha Vantage é exibida apenas a data do fechamento, sem inventar um horário. O horário de consulta permanece o original quando a resposta é reutilizada do cache. Falhas de consulta continuam visíveis, sem depender do informativo.

Esses detalhes referem-se à cotação consultada no painel, antes de confirmar o lote. A carteira continua com o preço salvo até a confirmação; esta alteração não recupera horários ausentes de registros antigos.

