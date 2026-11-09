# Cadastro assistido e saldos

Etapas 3 e 4 da [revisão de cadastros](proposta-simplificacao-cadastros.md), implementadas em 03/11/2026. Complementam a [organização dos cadastros e imagens](cadastros-e-imagens.md).

## Como usar

Em **Ativos → Novo ativo** ou **Editar**, abra **Buscar por ticker e revisar sugestões**. Escolha Brasil ou EUA, informe o ticker e faça a busca. Resultados não são selecionados nem gravados automaticamente. Escolha o instrumento correto e confira a fonte, a moeda e as diferenças.

A comparação mostra o valor atual e o sugerido. Marque os campos que deseja aplicar; campos já preenchidos começam desmarcados. Setor começa sempre desmarcado, inclusive se estiver vazio. A categoria da estratégia permanece no formulário, sob decisão do usuário. País não é inferido da bolsa. Tipo e setor só oferecem um vínculo quando há correspondência única no catálogo; equivalências ambíguas ou indisponíveis precisam de seleção manual.

**Buscar imagem sugerida** prepara uma prévia no padrão compartilhado. A imagem atual permanece até marcar **Aplicar imagem sugerida**, aplicar os campos e salvar o ativo. A opção manual de arquivo continua disponível. Se o formulário for editado durante a revisão, diferenças selecionadas que ficaram desatualizadas precisam ser revistas novamente.

Aplicar sugestões altera apenas o rascunho. O botão **Salvar ativo** confirma metadados, mapeamento e imagem na mesma transação. Descartar sugestões ou fechar o editor não grava essas alterações. A proteção dos formulários continua ativa.

## Mercados e referências

O ativo pode armazenar mercado (`BR`/`US`) e símbolo de consulta independentemente do ticker local e do país do emissor. Cotações e diagnóstico de cobertura usam esse mapeamento quando presente; o resultado é apresentado com o ticker local. Sem mapeamento explícito, o comportamento anterior continua disponível. Brasil exige BRL e EUA exige USD; a restrição existente de mudança de moeda para ativos com posições permanece válida.

Em **Referências** e no editor de Ativos, **Disponibilizar países e moedas BR/US** adiciona Brasil, Estados Unidos, BRL e USD quando ausentes. A carga é transacional e idempotente. Reconhece aliases conhecidos de país para evitar criar equivalentes, mas não funde IDs, renomeia registros, altera símbolos personalizados, cria carteiras ou aplica classificações aos ativos. Opções ambíguas continuam exigindo revisão.

## Fontes, cobertura e consumo

- Brasil: a [listagem de tickers da brapi](https://brapi.dev/swagger/latest.json) fornece identificação, moeda, subtipo, setor e endereço de logotipo. O adaptador retorna até 20 resultados e usa o subtipo documentado para distinguir FII; não infere país do emissor.
- EUA: a [busca de símbolos da Alpha Vantage](https://www.alphavantage.co/documentation/#symbolsearch) fornece nome, símbolo e moeda. Somente resultados de negociação nos Estados Unidos em USD são oferecidos. A região de negociação não define país do emissor; `Equity` não distingue ações de REITs, portanto a classificação continua manual. A consulta de [logotipo](https://www.alphavantage.co/documentation/#company-logo) é separada e opcional.

Conferência real, com as chaves já configuradas, em 03/11/2026: **PETR4** teve resultados e imagem SVG disponíveis; **MSFT** teve nome, ticker e moeda disponíveis, enquanto os campos de logotipo vieram vazios. O perfil `OVERVIEW` foi recusado pelo plano atual; por isso o fluxo usa `SYMBOL_SEARCH` e não depende de perfil completo. Não houve consulta em lote a todos os ativos, ativação de serviço pago ou alteração do banco pessoal. Esses resultados não garantem cobertura permanente ou de outros ativos.

Busca, imagem de dados e cotações compartilham os mesmos arquivos de estado, bloqueio entre processos, tentativas, pausas e espaçamento do provedor. A proteção local é de 15.000 tentativas/31 dias para brapi e 25/24h para Alpha Vantage. Ela não representa uma consulta à fatura do provedor. Tentativas são persistidas antes da chamada, inclusive quando a resposta falha. Avisos externos são traduzidos sem retornar mensagens cruas ou chaves.

Documentos de busca/logotipo ficam em cache local por 24 horas, limitado a 200 entradas por arquivo. A seleção de um resultado brasileiro reutiliza os dados já buscados para obter seu logotipo. Imagens preparadas têm cache em memória de até 32 entradas/24h; depois de confirmar o ativo, a imagem fica no banco conforme a entrega anterior. Exibir linhas do catálogo, carteira ou dashboard não consome consultas dos provedores.

Downloads de imagens aceitam somente HTTPS, hosts e caminhos previstos dos provedores, sem redirecionamentos, credenciais ou parâmetros arbitrários. Há limites de tamanho e tempo. PNG, JPEG e WebP passam pela normalização de 128 × 128 no navegador; SVG passa antes por um subconjunto restrito de elementos e atributos, sem scripts, imagens externas ou DTD. A persistência continua aceitando apenas PNG validado. Arquivos incompatíveis resultam na alternativa local ou na seleção manual, preservando a imagem confirmada.

## Saldos e patrimônio externo

A navegação passa a usar **Saldos e patrimônio externo**, mantendo `/external-assets` e todos os IDs e contratos financeiros. A tela distingue o cadastro de conta/reserva/item patrimonial do cadastro de instrumentos em Ativos e aponta para Movimentações quando a intenção é depositar, retirar, transferir ou corrigir.

O formulário esclarece que o valor cadastrado é inicial e entra no patrimônio total. Na edição, saldo e moeda continuam protegidos; nome e descrição podem ser ajustados. Não houve fusão com Ativos, transferência de saldos, criação automática de posições ou reclassificação de dados existentes. O módulo de gastos continua independente e requer proposta funcional própria.

## API e migration

- `GET /api/asset-discovery?market=BR&query=PETR`: consulta sem gravação financeira.
- `GET /api/asset-discovery/image?market=BR&symbol=PETR4`: sugestão de imagem; não aceita URL do cliente.
- `POST /api/reference-defaults`: adiciona somente referências ausentes.
- `listing` opcional no cadastro do ativo: `{ market, symbol }`; omitir preserva o mapeamento existente. `{ market: "", symbol: "" }` retorna à compatibilidade anterior.

Migration aditiva: **`20261104002557_AssetListing`**, com duas colunas opcionais. O identificador usa data UTC; a entrega foi realizada em 03/11 no fuso de São Paulo. Não foi aplicada ao banco pessoal. Para aplicar as migrations pendentes, na raiz:

```powershell
dotnet ef database update --project InvestmentTracker.Infrastructure --startup-project InvestmentTracker.Api
```

Diagnóstico opcional e explícito de um ticker, sem acessar/migrar o banco pessoal:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$report = Join-Path (Get-Location) 'local-reports/discovery-check.json'
dotnet run --project InvestmentTracker.Api --no-launch-profile -- --AssetDiscoveryCheck true --AssetDiscoveryMarket BR --AssetDiscoverySymbol PETR4 --AssetDiscoveryOutput $report
```

A pasta de saída deve existir. O comando consulta busca e, quando há correspondência exata, imagem; respeita o cache e a cota compartilhada. O relatório contém resultados e estado da imagem, sem chave, URL externa ou dados binários. A conferência pode consumir consultas do plano.

## Validação e limites

Testes cobrem busca sem gravação, cotas compartilhadas entre instâncias, dados inválidos ou excessivos, falhas de imagem, aquisição restrita de SVG, preservação de campos e imagens confirmados, mapeamento explícito, compatibilidade de clientes antigos, referências idempotentes e manutenção dos valores financeiros após salvar sugestões.

Passaram 302 testes unitários do backend, 198 de integração e 174 Angular, além dos 84 cenários de apresentação. Os 26 cenários E2E foram validados: 25 na rodada completa e repetição dos dois cenários de cadastro contextual após corrigir a busca do ativo na lista paginada. A base E2E inicial e a de repetição foram removidas pelos wrappers; o script temporário de execução focada também foi removido. O ajuste móvel da comparação passou nos quatro cenários focados em desktop/celular e claro/escuro.

Lint, formatação e build de produção aprovados; pacote inicial abaixo do limite preservado de 500 kB. As conferências de dados reais foram somente de leitura dos provedores e gravação do controle local de consultas/relatório. Não foi aplicada migration nem executada carga de referências no banco pessoal.

[Seletores visuais de ativos em compras/vendas e posições](seletores-visuais-de-ativos.md) e [revisão de setores com equivalências de nomes](revisao-de-setores.md) foram implementados em 05/11/2026. Setores são sempre confirmados explicitamente; ambiguidades e dados ausentes podem ser resolvidos escolhendo um setor do catálogo para revisar. Os seletores também foram ampliados para recebimentos, eventos societários e correções atrasadas. Cobertura adicional de provedores pode ser evoluída posteriormente. Não há importação automática de todos os cadastros nem preenchimento silencioso de campos ausentes.
