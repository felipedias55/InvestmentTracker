# Investment Tracker

Aplicação pessoal de investimentos em .NET 10, SQL Server e Angular. Abra `InvestmentTracker.slnx` no Visual Studio; a estrutura dos projetos foi preservada.

## Cadastros disponíveis

Tipos de ativo, países, moedas, categorias, setores e ativos possuem listagem, criação, edição e exclusão pela API e pelo Angular. As classificações são obrigatórias nos ativos. País e moeda são independentes. Registros referenciados não podem ser excluídos.

Veja [regras, contratos e escopo desta etapa](docs/cadastros.md).

## Carteiras, posições e câmbio

A página inicial é o **Dashboard**; a gestão das posições fica em **Carteira**. É possível criar e editar carteiras, definir a moeda-base de cada uma, adicionar/editar/remover posições e alternar entre moeda original e moeda-base. Os totais são sempre convertidos; a visualização original apresenta subtotais separados por moeda.

O provedor é [Frankfurter v2](https://frankfurter.dev/): gratuito, sem chave de API. O backend consulta cotações diárias e mantém cache persistente no SQL Server. Nenhum valor de investimento é enviado ao provedor. Sem cotação disponível, os totais convertidos ficam indisponíveis, sem ocultar os valores originais.

**Ao atualizar uma instalação existente, aplique a nova migration antes de iniciar a API:**

```powershell
dotnet ef database update --project InvestmentTracker.Infrastructure --startup-project InvestmentTracker.Api
```

O comando de carga inicial abaixo também aplica as migrations e cria a **Carteira Principal** em BRL se ainda não houver carteiras. A configuração `Portfolio:DefaultCurrencyCode` em `appsettings.json` define o padrão de novas carteiras quando nenhuma moeda for informada. Alterar esse padrão não modifica carteiras existentes.

Detalhes de precisão, cache, indisponibilidade e endpoints: [carteira e câmbio](docs/carteira-e-cambio.md).

## Metas, dashboard e aportes

As telas **Metas**, **Dashboard** e **Aportes** estão disponíveis no menu. Configure conjuntos de metas por categoria e setor somando 100%, acompanhe as distribuições na moeda-base e simule o aporte usando as diferenças positivas, conforme a fórmula da planilha. A simulação não altera posições. Valores e percentuais usam o padrão brasileiro.

Para atualizar uma base já cadastrada, aplique a nova migration:

```powershell
dotnet ef database update --project InvestmentTracker.Infrastructure --startup-project InvestmentTracker.Api
```

Regras, exemplos, endpoints e validação: [metas, dashboard e aportes](docs/metas-dashboard-aportes.md).

## Patrimônio externo e proventos

O menu **Patrimônio externo** permite cadastrar valores por carteira com moeda própria. O dashboard consolida ativos + patrimônio externo. A tela **Carteira** possui proventos acumulados por posição e o total no topo; proventos não são somados automaticamente ao patrimônio.

Aplique a migration `PortfolioExternalAssetsAndIncome` com o comando de atualização acima e reinicie a aplicação. Os proventos das posições existentes começam em zero. Regras e detalhes: [patrimônio externo e proventos](docs/patrimonio-externo-e-proventos.md).

## Evolução e histórico de aportes

A tela **Evolução e aportes** registra fotografias mensais manuais, apresenta gráficos e comparações mês a mês/ano a ano e permite consultar a composição preservada de cada mês. Aportes e retiradas reais são registrados separadamente, sem alterar automaticamente as posições.

Aplique a migration `PortfolioHistory`, confira seus saldos e registre a primeira fotografia. Somente a fotografia do mês corrente pode ser substituída, com confirmação. O histórico começa com dados efetivamente registrados; meses anteriores não são preenchidos automaticamente.

Uso, fórmulas, moedas, datas e endpoints: [histórico da carteira](docs/historico-da-carteira.md).

## Executar localmente

Pré-requisitos: SDK .NET 10, SQL Server acessível (ou LocalDB no Windows), Node.js compatível com o Angular instalado e npm. Use as versões do `package-lock.json` com `npm ci`.

Configure a conexão da API em User Secrets (substitua o exemplo pelo servidor local):

```powershell
dotnet user-secrets set "ConnectionStrings:InvestmentTracker" "Server=(localdb)\MSSQLLocalDB;Database=InvestmentTracker;Trusted_Connection=True;TrustServerCertificate=True" --project InvestmentTracker.Api
```

Para aplicar as migrations existentes e inserir os cadastros iniciais, execute **uma vez, explicitamente**:

```powershell
dotnet run --project InvestmentTracker.Api -- --SeedCatalogs=true
```

O comando termina após a carga. Pode ser repetido: procura pelos nomes/códigos únicos, preserva IDs e dados existentes e insere somente o que falta. A carga não é executada automaticamente ao iniciar a API. Nenhum ativo ou valor financeiro pessoal é inventado.

Inicie a API e o frontend em terminais separados:

```powershell
dotnet run --project InvestmentTracker.Api --launch-profile https
```

```powershell
cd frontend/InvestmentTracker.Web
npm ci
npm start
```

Abra `http://127.0.0.1:65453`. O proxy encaminha `/api` para `https://localhost:7097`. O certificado de desenvolvimento pode ser confiado com `dotnet dev-certs https --trust`. A configuração `secure: false` do proxy é apenas para desenvolvimento local.

No Visual Studio, os mesmos projetos podem ser configurados como projetos de inicialização. O frontend continua no projeto `.esproj` existente. O OpenAPI está em `https://localhost:7097/openapi/v1.json` no ambiente Development.

## Testes

```powershell
dotnet test
# Ou somente os unitários:
dotnet test tests/InvestmentTracker.UnitTests
```

Configure uma conexão com um servidor SQL Server cujo usuário possa criar e remover bancos **de teste**:

```powershell
dotnet user-secrets set "ConnectionStrings:TestDatabase" "Server=(localdb)\MSSQLLocalDB;Database=InvestmentTrackerTests;Trusted_Connection=True;TrustServerCertificate=True" --project tests/InvestmentTracker.IntegrationTests
dotnet test tests/InvestmentTracker.IntegrationTests
```

Alternativamente use `ConnectionStrings__TestDatabase` no ambiente. Os testes substituem o nome configurado por `InvestmentTracker_Tests_<guid>`, aplicam migrations, limpam somente os dados dessa base entre testes e removem essa base ao terminar. Não apagam o banco indicado na conexão de origem.

```powershell
cd frontend/InvestmentTracker.Web
npm test -- --watch=false
npm run build
```

Os testes Angular usam Vitest, Reactive Forms e o backend HTTP de teste. Os testes de integração C# usam a API real com `WebApplicationFactory`, EF Core e SQL Server.

## Próximas etapas da especificação

Cadastros, carteiras/posições, conversão de moedas, patrimônio externo por carteira, proventos acumulados, metas, dashboard, simulador de aportes e histórico mensal/anual com movimentos realizados estão implementados. A suíte E2E já valida os fluxos principais de compras, vendas, reinvestimento e fotografias em desktop e celular emulado. Docker, CI/CD e publicação permanecem adiados.

## Datas de atualização e backup

Posições e patrimônio externo exibem a data da última gravação manual. A carga inicial utiliza 08/09/2026; as próximas gravações recebem o dia corrente de São Paulo. Novas fotografias preservam as datas.

O script `scripts/Backup-Database.ps1` gera backup completo e valida a restauração em uma base temporária. Veja [datas, execução e recuperação](docs/datas-e-backup.md).

## Compras e vendas automáticas

Use **Compras e vendas** para informar data, ativo, quantidade e preço unitário. A posição e o movimento do dinheiro são gravados juntos. Dinheiro novo gera aporte; venda com saída gera retirada. Usar um saldo de patrimônio externo permite reinvestir sem duplicar aportes.

Os registros existentes continuam como saldo inicial. O formulário manual da carteira fica reservado a saldo inicial, correções e atualização de valores/proventos. Novas operações reais devem ser registradas em Compras e vendas.

Aplique a migration `PortfolioTrades` e reinicie API e frontend. Regras, limites, arquitetura e análise das próximas etapas: [compras e vendas](docs/compras-e-vendas.md).

## Revisão em desktop e celular

O menu lateral pode ser aberto/recolhido pelo botão fixo no topo. No celular, a navegação abre sobre o conteúdo e fecha após escolher uma página. Edição e confirmação de exclusão em cadastros, ativos, posições e patrimônio externo abrem em janela, preservando o formulário e evitando voltar ao topo. Cadastros e ativos possuem busca por nome/código (ativos também por classificação).

Para acessar pelo celular na mesma rede privada, mantenha a API iniciada e execute no diretório do frontend:

```powershell
npm run start:lan
```

Abra `http://IP_DO_COMPUTADOR:65453` no celular. O proxy encaminha a API. Identificadores de operações usam `crypto.getRandomValues`, compatível com HTTP na rede local; não dependem de `crypto.randomUUID`. Para acesso apenas no computador, continue usando `npm start`.

A API não abre mais o navegador automaticamente ao iniciar pelo Visual Studio. Não há nova migration nesta revisão de interface.

Além dos testes Angular, a suíte Playwright agora exercita menu, edição de ativo e operações financeiras com API e banco reais, em uma base temporária. A revisão em aparelhos físicos continua complementar à emulação. Veja [execução, cobertura e diagnóstico dos testes E2E](docs/testes-e2e.md).

## Recebimentos de proventos

Use **Proventos** para registrar ativo, data, valor líquido e destino do recebimento. O acumulado da posição é atualizado automaticamente; um saldo de patrimônio externo pode receber o crédito para reinvestimento, sem gerar aporte. Os acumulados anteriores permanecem como saldo inicial. Veja [regras, histórico e migration](docs/recebimento-de-proventos.md).
