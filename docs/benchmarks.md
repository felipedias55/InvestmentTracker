# Benchmarks — primeira entrega

Comparação sob demanda em **Evolução e fechamentos → Evolução**, com CDI, IPCA, Ibovespa e S&P 500. O intervalo é escolhido entre os meses/anos já exibidos no histórico; o botão **Comparar benchmarks** consulta as fontes somente após uma ação explícita.

## Método e universo

A carteira usa o **Dietz modificado estimado** que o backend já calcula para o patrimônio total de duas fotografias, incluindo saldos e patrimônio externo. A métrica incorpora os fluxos históricos registrados, conforme [métricas de rentabilidade](metricas-de-rentabilidade.md). Não se compara a XIRR anualizada com o retorno mensal de um índice.

Cada referência mostra a variação no intervalo real entre as fotografias. A diferença é `Dietz da carteira − retorno da referência`, apresentada em **pontos percentuais**. Exemplo sintético: carteira 10%, referência 5% → diferença de 5 p.p. Não representa alfa, recomendação ou uma simulação de investimento com os mesmos aportes/retiradas da carteira. Não há encadeamento de Dietz mensal, soma de retornos ou projeção de patrimônio com benchmark nesta entrega.

| Referência | Fonte | Método |
| --- | --- | --- |
| CDI | BCB/SGS 12 | Produto de `1 + taxa diária / 100`, para observações posteriores à fotografia inicial e até a final, inclusive. Referência bruta de 100% do CDI, sem impostos ou custos. |
| IPCA | BCB/SGS 433 | Produto das variações mensais, incluindo deflação, dos meses entre os fechamentos. Somente em BRL e entre fins de mês; todos os meses precisam estar publicados. Não se rateia inflação mensal por dia. |
| Ibovespa | brapi/B3, `^BVSP` | Razão dos níveis de fechamento diário final/inicial menos um. Não substitui o índice por ETF. Cobertura depende do plano e do intervalo liberado pela brapi. |
| S&P 500 | FRED/S&P Dow Jones Indices, `SP500` | Razão dos níveis de fechamento final/inicial menos um. A série é de preços, sem dividendos reinvestidos. A fonte disponibiliza histórico diário limitado e pode revisar os dados. |

Índices e câmbio usam a última observação disponível até cada fotografia, com tolerância máxima de sete dias e sem buscar um preço futuro. Datas efetivas são apresentadas. A série CDI não é preenchida artificialmente e lacunas superiores a sete dias suspendem o resultado; não existe um calendário local que certifique todos os feriados ou a completude da fonte. Série vazia, dados duplicados, níveis inválidos, falta de cobertura e estouro numérico não viram retorno zero.

## Moeda e qualidade do histórico

A primeira entrega suporta fotografias em **BRL ou USD**. Referências em outra moeda são convertidas com USD/BRL histórico do BCB/SGS 1: índice em USD para BRL multiplica pelo fator do câmbio; índice em BRL para USD divide pelo fator. Não usa o câmbio atual. As datas do câmbio são informadas no diagnóstico da referência convertida.

O IPCA permanece uma medida de inflação em BRL; não é transformado em inflação dos EUA. Em fotografias USD, aparece indisponível com explicação. Datas parciais e meses ainda não publicados também suspendem o IPCA, preservando as demais referências.

Fotografias inválidas, reabertas/desatualizadas, moedas incompatíveis, conversões históricas ausentes ou Dietz indisponível suspendem a comparação antes de consultar os provedores. Alertas de câmbio anterior/alternativo das fotografias são preservados. O intervalo solicitado deve existir no histórico, começar a partir de 2000 e ter até cinco anos. Não é possível fornecer retornos da carteira pelo cliente para substituir o cálculo do backend.

## Consumo, cache e falhas

- Abrir o histórico, navegar ou mudar filtros não consulta fontes externas. O usuário precisa clicar novamente para o novo contexto.
- Ibovespa compartilha a chave brapi, as tentativas, o bloqueio entre processos e as pausas já usadas nas cotações/cadastro assistido. A proteção é de 15.000 tentativas em 31 dias; a tentativa é persistida antes de enviar, inclusive em erros. HTTP 429 impede novas consultas durante a pausa. Não ativa planos pagos.
- CDI, IPCA e USD/BRL usam BCB público; S&P 500 usa CSV público do FRED. Essas consultas não entram nos contadores brapi/Alpha Vantage. Não há chamada à Alpha Vantage para benchmarks.
- Resultados de fontes públicas ficam em memória por 24 horas, em até 80 janelas; reiniciar a API perde esse cache. Erros ficam cinco minutos em cache para evitar repetições imediatas. O documento de Ibovespa também usa o cache persistente compartilhado, com validade de 24 horas e limite de 200 documentos.
- Uma falha de fonte ou de câmbio mantém as outras referências disponíveis; não inventa observações nem estende uma série antiga até hoje. O período, moeda, fonte, consulta/cache e motivos de indisponibilidade ficam visíveis. Alterar carteira, agrupamento ou período invalida resultados e cancela solicitações em andamento.
- Há URLs fixas, sem redirecionamentos ou URLs fornecidas pelo cliente, limites de leitura de 1 MiB, cancelamento e timeout. Logs de transporte usam código, categoria e tipo da falha, sem chave, corpo externo ou URL com credenciais.
- FRED tenta primeiro o cliente HTTP gerenciado. Em falha de conexão/transporte, usa o `curl` do sistema com HTTPS e validação normal de certificado, argumentos separados sem shell, URL fixa, sem redirecionamentos e limites de tempo/tamanho. Não usa essa alternativa após recusas HTTP (por exemplo, 403/429). No Windows usa o executável do diretório do sistema; em outros ambientes, `curl` precisa estar disponível caso essa alternativa seja necessária. Se ambos falharem, a referência fica indisponível; não há substituição por outra série.

## Arquitetura e API

- Application: seleção do intervalo a partir do histórico persistido, elegibilidade, composição de taxas, índices, conversão e diferenças.
- Infrastructure: HTTP e parsers BCB/FRED/brapi; cache e cota compartilhada.
- API: `GET /api/portfolios/{id}/history/benchmarks?mode=months&period=2026-02` (ou `mode=years&period=2026`).
- Angular: seleção, chamada explícita, formatação e exibição. Tabela em desktop e cartões no celular, nos temas existentes. Não recalcula rentabilidade.

Não há migration ou mudança dos contratos financeiros existentes. Consultar benchmarks não cria ativos, categorias, fluxos, compras/vendas, saldos ou fotografias, nem altera metas e planejamento. As comparações são leituras, não registros históricos auditados ou carteiras virtuais.

## Diagnóstico isolado

Conferência opcional de fontes sem abrir a base financeira, na raiz do projeto:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
New-Item -ItemType Directory -Path local-reports -Force | Out-Null
$report = Join-Path (Get-Location) 'local-reports/benchmark-check.json'
dotnet run --project InvestmentTracker.Api --no-launch-profile -- --BenchmarkCheck true --BenchmarkStart 2026-09-30 --BenchmarkEnd 2026-10-31 --BenchmarkOutput $report
```

`--BenchmarkCode SP500` limita o diagnóstico a uma série; também aceita CDI, IPCA, IBOV e USDBRL. O relatório contém cobertura, datas, fator, cache e mensagens, sem chaves ou dados financeiros. A pasta precisa existir e o caminho deve ser absoluto. Consultar IBOV pode consumir uma tentativa brapi; cache persistente válido é reutilizado.

## Referências

- [BCB/SGS — CDI, série 12](https://www3.bcb.gov.br/sgspub/consultarvalores/consultarValoresSeries.do?method=consultarSeries&series=12).
- [BCB/SGS — IPCA, série 433](https://www3.bcb.gov.br/sgspub/consultarvalores/consultarValoresSeries.do?method=consultarSeries&series=433).
- [BCB — dólar americano, venda, série 1](https://dadosabertos.bcb.gov.br/dataset/1-taxa-de-cambio---livre---dolar-americano-venda---diario).
- [brapi — histórico diário e limites do plano](https://brapi.dev/docs/acoes/historico). A antiga [série SGS 7](https://www3.bcb.gov.br/sgspub/consultarvalores/consultarValoresSeries.do?method=consultarSeries&series=7) termina em 2019 e não é usada como fonte atual de Ibovespa.
- [S&P 500 via FRED — metodologia, cobertura e atribuição](https://fred.stlouisfed.org/series/SP500).

## Validação da entrega

Passaram 314 testes unitários do backend, 25 testes focados de integração (histórico, API e adaptadores), 195 Angular e oito cenários visuais em desktop/celular e claro/escuro. A integração confirma o cálculo a partir das fotografias persistidas, a ausência de alterações financeiras e a suspensão da comparação após invalidar uma fotografia. Testes também cobrem composição diária/mensal, deflação, dados ausentes/duplicados, moeda, câmbio faltante, limites numéricos, cache, cota compartilhada, cancelamento e proibição de usar a alternativa de transporte após recusas HTTP.

Lint, build e verificação de diferenças aprovados. Pacote inicial de 456,83 kB, abaixo do limite preservado de 500 kB. A apresentação em cartões foi inspecionada no celular. Não houve migration ou acesso à base financeira pessoal.

Conferência real em 05/11/2026 para 30/09–31/10/2026: CDI, USD/BRL e Ibovespa tiveram cobertura; Ibovespa consumiu uma tentativa brapi e as repetições usaram cache persistente. O IPCA retornou observação de setembro, mas ainda não tinha outubro publicado, suspendendo a comparação daquele intervalo. O S&P 500 foi validado pela aplicação usando a alternativa HTTPS do sistema após resets do cliente gerenciado. Esses resultados são observações da entrega, sem garantia permanente de cobertura/tempo de publicação. Os relatórios locais ficam em `local-reports`, ignorado pelo Git, sem chaves ou dados financeiros.
