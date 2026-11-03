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
4. Clique em Buscar cotações.
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

## Mapeamento explícito de símbolos

Implementado em 28/10/2026. Quando o código usado pelo provedor difere do ticker cadastrado, é possível configurar uma correspondência local por mercado, sem alterar o cadastro ou o histórico:

```powershell
dotnet user-secrets set "Quotes:Symbols:US:TICKER_CADASTRADO" "SIMBOLO_DO_PROVEDOR" --project InvestmentTracker.Api
```

O comando é um modelo: substitua ambos os códigos por símbolos conferidos do **mesmo instrumento, classe e mercado**. Para Brasil, use `BR` no lugar de `US`. Reinicie a API após configurar. Para remover uma correspondência, use `dotnet user-secrets remove "Quotes:Symbols:US:TICKER_CADASTRADO" --project InvestmentTracker.Api` e reinicie.

Não há correspondências predefinidas, busca por semelhança, conversão automática de ponto/hífen nem migração automática de tickers antigos. Renomes decorrentes de eventos societários exigem conferir o cadastro e o histórico; o mapeamento não reconstrói eventos. A configuração não comprova equivalência econômica entre dois códigos.

Sem mapeamento, o ticker cadastrado continua sendo enviado diretamente. Com mapeamento, a resposta externa deve coincidir exatamente com o símbolo configurado; a brapi continua recusando `changed: true`. A resposta interna mantém o ticker cadastrado para a validação da posição, e a mensagem de revisão informa os dois símbolos. País, moeda, preço e data continuam com as mesmas validações.

O símbolo configurado aceita letras maiúsculas, números, ponto e hífen, até 20 caracteres. Configuração vazia ou inválida retorna `invalid-mapping` antes de acessar a rede ou consumir a proteção local de consultas. O cache usa o símbolo efetivamente consultado: alterar a correspondência não reutiliza o preço do símbolo anterior. Correspondências para o mesmo símbolo compartilham o cache e o controle de consumo existentes.

A cobertura real ainda depende do instrumento e do plano do provedor. Consulte os motivos retornados no painel e confirme o símbolo na [documentação Alpha Vantage](https://www.alphavantage.co/documentation/#symbolsearch) ou no [catálogo/documentação brapi](https://brapi.dev/docs/acoes/cotacao). Esta entrega foi validada com HTTP simulado, sem consumir franquias reais; não certifica cobertura de todos os ativos da carteira.

Validação local de 28/10/2026: 159 testes de integração aprovados, incluindo sete novos casos de mapeamento (EUA/Brasil, cache compartilhado, alteração de destino, configuração inválida e símbolo retornado incompatível). Nenhuma configuração de correspondência foi aplicada à carteira pessoal.

## Cache e consumo

Atualizado em 28/10/2026. Há um coordenador único por processo da API, com consultas sequenciais. A brapi mantém resultados em cache persistente por 30 minutos e falhas por dois minutos. HTTP 429 é registrado como pausa por chave, respeitando `Retry-After`, inclusive entre reinícios e para outros símbolos; não é tratado como falha isolada de um ticker. Na Alpha Vantage, cotações ficam em cache persistente por 12 horas; avisos e falhas não são confundidos com cotações.

A brapi utiliza arquivos `brapi-<hash-da-chave>.json` na mesma pasta de estado, separados dos arquivos existentes da Alpha Vantage, que são preservados. O bloqueio de arquivo e a escrita atômica também se aplicam à brapi. O cache preserva os horários originais de mercado e consulta; cache vencido não é oferecido como alternativa em falhas. Falhas de acesso ou arquivo inválido interrompem a consulta com `state-unavailable`. Não há migration nem configuração adicional para habilitar a persistência.

Validação local: 163 testes de integração aprovados, dos quais 27 exercitam os provedores com HTTP simulado. Os novos casos verificam reinício, expiração, horários preservados, pausas entre símbolos, cache curto de falhas, estado corrompido e separação entre provedores. Nenhuma franquia real foi consumida. A interpretação do HTTP 429 segue a [documentação de erros da brapi](https://brapi.dev/docs/troubleshooting), sem presumir que toda resposta 429 signifique esgotamento mensal.

Alpha Vantage possui proteção local de **25 tentativas em uma janela móvel de 24 horas**, por chave. Essa janela conservadora não presume o horário de renovação do provedor. Tentativas são salvas antes do envio, inclusive quando a conexão falha ou o navegador cancela. Reiniciar a API não apaga esse controle. Consultas são espaçadas em pelo menos 13 segundos; essa é uma precaução local, não garantia de franquia por minuto. Um lote americano pode levar alguns minutos.

O estado é salvo em `%LOCALAPPDATA%\InvestmentTracker\QuoteState`, fora do repositório. `Quotes:StateDirectory` permite definir outra pasta local gravável. O arquivo usa um hash da chave como nome e não contém a chave, posições ou saldos: apenas tickers, cotações, horários e controle de consultas. Escrita atômica e bloqueio de arquivo coordenam processos que compartilham a pasta. Não apague esses arquivos para tentar renovar a cota. Outras máquinas, pastas e aplicações que utilizem a mesma chave não compartilham esse controle, mas consomem a franquia real.

HTTP 429 gera pausa temporária, respeitando `Retry-After`; sem esse cabeçalho, a pausa é de um minuto. Avisos de frequência também geram pausa de um minuto. Somente uma mensagem identificada como limite diário gera bloqueio conservador de 24 horas a partir da resposta. Avisos desconhecidos não esgotam artificialmente o contador. Não são feitas tentativas automáticas adicionais: após o horário informado, consulte novamente para retomar os campos vazios.

O painel informa consultas enviadas, respostas reutilizadas do cache e resultados sem cotação. Cada ativo exibe uma frase com o motivo: pausa temporária, limite diário, chave recusada, restrição do plano gratuito, ticker não reconhecido, falta de dados, resposta inválida ou falha de comunicação. Falha no armazenamento local interrompe a consulta com uma mensagem própria, evitando ignorar silenciosamente a proteção de consumo. O controle local não é um extrato oficial da franquia.

A seleção é uma cópia dos filtros ativos ao abrir o painel. Somente posições dessa seleção, com quantidade positiva e campo de preço vazio, são enviadas para consulta. Para mudar a seleção, feche o painel, ajuste os filtros e abra novamente. Consultar não salva preços: revise e confirme o lote.

Desde 28/10/2026, há proteção local persistente da brapi: até **15.000 tentativas em janela móvel de 31 dias**, por chave. A janela é conservadora e não presume o dia de renovação da conta. Tentativas são registradas antes do envio, inclusive em falhas de rede, timeout ou cancelamento; cache não incrementa o contador. Ao atingir o limite, novas chamadas retornam `monthly-limit` com o próximo horário de liberação. Cotações ainda válidas em cache continuam disponíveis. O informativo da consulta apresenta tentativas, limite e janela, identificando esses números como proteção local, não como extrato oficial.

Esse controle começa nos registros locais desta versão: não recupera consumo anterior nem de outras aplicações, pastas ou máquinas. A quota real continua sendo determinada pelo provedor. Apagar o estado não renova a franquia real e remove a proteção local. Nenhum plano pago é ativado.

## Conferência de cobertura do cadastro

O modo de diagnóstico consulta uma vez cada ativo cadastrado, respeitando mapeamentos, cache e pausas/limites de ambos os provedores. Verifica país/moeda, símbolo retornado, preço e data, sem carregar posições ou alterar preços, quantidades e histórico. A adequação do valor total de uma posição continua sendo validada no painel antes de aplicar.

Execute na raiz do projeto, usando o perfil Development para carregar os User Secrets:

```powershell
New-Item -ItemType Directory -Path local-reports -Force | Out-Null
$report = Join-Path (Get-Location) 'local-reports/quote-coverage.json'
dotnet run --project InvestmentTracker.Api --configuration Release --launch-profile https -- --QuoteCoverage true --QuoteCoverageOutput $report --Logging:LogLevel:Default Warning --Logging:LogLevel:Microsoft Warning
```

O comando encerra após gerar o JSON, sem iniciar servidor HTTP e sem aplicar migrations. O relatório registra data/hora, identificador e ticker do ativo, provedor, status, motivo, data da cotação, uso de cache/rede e próximo horário de tentativa. Não contém chaves, preços, quantidades ou saldos. A pasta `local-reports/` está ignorada pelo Git porque os tickers ainda são dados do cadastro pessoal.

Uma indisponibilidade pontual, falta de chave ou cota esgotada não comprova ausência definitiva de cobertura. O relatório preserva esses motivos para distinguir problemas de configuração, restrições e respostas incompatíveis. Não corrige símbolos automaticamente, troca provedores ou ativa assinaturas. Repita somente quando necessário; consultas reais usam a franquia gratuita.

### Resultado de encerramento da fase — 28/10/2026

A conferência real do cadastro retornou **27 de 27 ativos com status `available`: 12 pela brapi e 15 pela Alpha Vantage**. Não foram detectadas incompatibilidades de símbolo, moeda, preço ou data nas validações executadas. Isso comprova o resultado nessa consulta, sem garantir disponibilidade futura, equivalência econômica de correspondências manuais ou exatidão independente do provedor. Na Alpha Vantage, a moeda continua decorrendo do mercado cadastrado, pois GLOBAL_QUOTE não a informa.

O relatório está em `local-reports/quote-coverage-2026-10-28.json`, ignorado pelo Git, com motivos e horários e sem preços, quantidades ou saldos. Nenhum preço ou cadastro foi alterado. Não foi necessário criar correspondências de símbolos para concluir essa conferência.

Encerrado o escopo desta fase: mapeamento explícito, cache persistente de ambos os provedores, pausas e proteção local de consumo, informativo no painel, diagnóstico repetível e conferência real do cadastro. Novos ativos e falhas futuras devem ser avaliados no uso; não permanecem como uma entrega de implementação indefinida desta fase.

Validação final: 220 testes unitários .NET, 165 de integração, 131 Angular, 60 de apresentação e 12 E2E aprovados (588 no total), além de lint, formatação de configurações e build. Pacote inicial mantido em 455,77 kB, abaixo do limite de 500 kB. Testes usam HTTP simulado e bases temporárias; a conferência de 27 ativos acima foi executada separadamente com os provedores reais.

## Privacidade e arquitetura

Somente o ticker e a chave necessária são enviados ao provedor. Valores, quantidades e identificadores da carteira não são enviados. Chaves permanecem no backend; logs do HttpClient de cotações estão desabilitados para não registrar a chave Alpha Vantage presente na URL. Erros externos são substituídos por mensagens locais.

Application coordena a consulta e valida compatibilidade com a posição. Infrastructure implementa HTTP, cache e limites. API expõe POST /api/portfolios/{id}/quotes/preview. Angular apresenta/preenche e solicita confirmação. A gravação usa PUT /api/portfolios/{id}/quotes.

## Validação e pendências

Testes automatizados usam respostas HTTP controladas e bancos temporários. Após a primeira entrega, as chaves foram configuradas localmente, houve consulta real à brapi e o usuário confirmou o funcionamento do fluxo de consulta e gravação. Isso não comprova cobertura de todos os tickers nem consistência de todos os campos retornados pelos provedores.

A correção de limites, o mapeamento explícito, a persistência brapi e a proteção local de consumo possuem testes com HTTP simulado. O diagnóstico de cobertura permite a conferência real do cadastro sem gravações financeiras. Motivos externos continuam disponíveis para orientar correções no uso; correspondências exigem equivalência confirmada dos instrumentos. Não há mudança automática de provedor, endpoint premium, plano ou assinatura.

Não exige migration.

### Informativo de origem da consulta
Na consulta e revisão do lote, o ícone ⓘ revela a fonte e os horários ao passar o mouse, focar pelo teclado ou tocar no celular. Os horários são apresentados em Brasília. O horário de mercado da brapi é preservado; na Alpha Vantage é exibida apenas a data do fechamento, sem inventar um horário. O horário de consulta permanece o original quando a resposta é reutilizada do cache. Falhas de consulta continuam visíveis, sem depender do informativo.

Esses detalhes referem-se à cotação consultada no painel, antes de confirmar o lote. A carteira continua com o preço salvo até a confirmação; esta alteração não recupera horários ausentes de registros antigos.

