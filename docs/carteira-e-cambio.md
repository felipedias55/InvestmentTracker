# Carteira, posições e câmbio

## Decisões implementadas

- Moeda-base por carteira (`Portfolio.BaseCurrencyId`), independente da moeda dos ativos.
- BRL como padrão inicial do site, configurável em `Portfolio:DefaultCurrencyCode`. A API utiliza o padrão quando `BaseCurrencyId` é omitido na criação; a tela pré-seleciona essa moeda.
- Carteira Principal em BRL criada pela carga explícita (`--SeedCatalogs=true`), somente se ainda não existir nenhuma carteira.
- Valores das posições são totais na moeda original do ativo; não são preços por unidade. Quantidade, investido e valor atual são não negativos.
- Alternância de exibição entre original e base; os totais da carteira sempre usam a base. Na opção original, os subtotais são separados por moeda.
- Editar a moeda-base recalcula a apresentação, sem regravar ou reconverter valores originais.
- O ativo de uma posição não pode ser trocado durante a edição. A moeda de um ativo com posições também não pode ser trocada. Códigos de moedas referenciadas por ativos ou carteiras são protegidos contra alteração; nomes e símbolos podem ser editados.

## API escolhida: Frankfurter v2

Documentação oficial: https://frankfurter.dev/

Consulta utilizada: `GET https://api.frankfurter.dev/v2/rate/USD/BRL` (substituindo o par conforme necessário).

Resposta verificada:

```json
{"date":"2026-09-07","base":"USD","quote":"BRL","rate":5.1211}
```

Essa resposta ilustra o contrato; a taxa não está fixa no código. São enviadas apenas as siglas das moedas, nunca carteiras, posições ou valores.

Segundo a documentação consultada em 07/09/2026, o serviço público é gratuito, não exige chave e não tem cotas diárias/mensais. Há limitação contra abuso e os dados subjacentes seguem os termos de seus provedores. São cotações diárias de referência, não preços de negociação, câmbio em tempo real ou valores com tarifas/spread. Usamos as taxas combinadas padrão da API v2; não há filtro de banco central específico.

## Conversão e cache

`Valor na base = valor original × taxa(original → base)`.

A taxa é resolvida uma vez por moeda na consulta da carteira. Para pares iguais, usa-se 1 sem acesso externo. O cache na tabela `ExchangeRate` guarda par, taxa, data de referência e horário UTC da consulta bem-sucedida. Não são armazenados totais convertidos de carteira.

- Atualização sob demanda, na primeira consulta do dia UTC para cada par; não há tarefa agendada quando o site não é acessado.
- Timeout de 10 segundos por chamada. Após falha, nova tentativa somente depois de 15 minutos nessa instância, evitando chamadas repetidas em cada carregamento.
- Reiniciar a API preserva as taxas do banco; o controle temporário de novas tentativas é reiniciado.
- Há coordenação por par dentro da instância e gravação atômica do cache no SQL Server para suportar consultas simultâneas.
- A data de referência pode ser anterior ao dia da consulta, por exemplo em fins de semana/feriados. A tela informa essa situação.
- Se o provedor falha, retorna dados inválidos ou não suporta o par, usa-se a última taxa armazenada com aviso de fallback. Sem taxa armazenada, o total convertido fica `null`/indisponível; nunca é usado 1 como substituto de uma taxa desconhecida.
- Se faltar taxa para qualquer posição, o total consolidado completo fica indisponível. As linhas que puderem ser convertidas e todos os valores originais permanecem disponíveis.
- A data do câmbio aparece na linha de cada posição estrangeira. Trocar o seletor de exibição não faz nova consulta.

O custo investido também é convertido pela taxa diária para fins de apresentação. Não se reconstrói câmbio histórico de aportes, rentabilidade cambial, impostos ou transações nesta etapa.

## Endpoints

| Método | Caminho | Resultado |
|---|---|---|
| GET | `/api/portfolios/defaults` | Moeda padrão do site |
| GET | `/api/portfolios` | Lista de carteiras e suas moedas-base |
| POST | `/api/portfolios` | Criação, 201 com Location |
| PUT | `/api/portfolios/{id}` | Edição do nome, descrição e moeda-base |
| GET | `/api/portfolios/{id}` | Carteira, posições, subtotais originais, totais convertidos e estado das taxas |
| GET | `/api/portfolios/{id}/assets` | Mesmo resumo completo das posições |
| POST | `/api/portfolios/{id}/assets` | Adicionar posição, 201 com ID |
| PUT | `/api/portfolios/{id}/assets/{positionId}` | Editar valores da posição, 204 |
| DELETE | `/api/portfolios/{id}/assets/{positionId}` | Remover posição, 204 |

Entradas inválidas usam 400, recursos inexistentes 404 e conflitos 409, no ProblemDetails já utilizado pelo projeto. Uma posição é identificada dentro de sua carteira: usar seu ID em outra carteira retorna 404. Não foi adicionada exclusão de carteiras; as posições devem ser removidas explicitamente.

## Precisão e migrations

- `Quantity`: decimal(19,6), até 13 inteiros e 6 casas decimais.
- `InvestedAmount` e `CurrentValue`: decimal(19,4), até 15 inteiros e 4 casas decimais.
- Taxa: decimal(28,12).
- Nos DTOs financeiros, valores decimais são serializados como strings para não perder precisão ao editar pelo JavaScript. A API aceita também números JSON. As entradas e a exibição usam o padrão brasileiro: ponto nos milhares e vírgula nos decimais (ex.: `1.234,5`).
- Os cálculos usam decimal no backend. A tela arredonda a apresentação monetária para duas casas; o formulário preserva a precisão original.
- A migration `PortfolioBaseCurrencyAndExchangeRates` adiciona a FK de moeda-base, cache, precisão e constraint de valores não negativos. Carteiras existentes recebem BRL, preservando seus IDs e valores.
- Se dados existentes forem negativos, excederem os novos limites ou perderem casas decimais de quantidade, a migration aborta com mensagem; não arredonda os dados silenciosamente. O rollback também verifica a perda de precisão.

A API não aplica migrations automaticamente na inicialização normal. Execute o comando do README no banco de desenvolvimento antes de iniciar a nova versão. A validação automática usa bancos temporários isolados; não migra a base pessoal durante os testes.

## Testes

Os unitários cobrem conversão antes da soma, agrupamento por moeda, carteira vazia, precisão, duplicidade e ausência de cotação. Os de integração cobrem API/EF/SQL Server, troca da base sem alterar originais, cache persistente/fallback, migração de carteiras antigas e proteção da moeda de posições existentes. Os testes do cliente Frankfurter usam respostas HTTP controladas; a suíte não depende da disponibilidade da API pública. Foi feita também uma consulta pública real para verificar o contrato.

Os testes Angular cobrem o seletor de exibição, totais indisponíveis, data/fallback, padrão BRL, edição na moeda original e preservação da precisão no payload.

## Etapas futuras

Metas, distribuições por categoria/setor/país, dashboard da carteira e aporte sugerido foram implementados na etapa seguinte: [regras e utilização](metas-dashboard-aportes.md). Patrimônio externo por carteira, consolidação patrimonial e proventos acumulados também estão disponíveis: [regras e utilização](patrimonio-externo-e-proventos.md).
