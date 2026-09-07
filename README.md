# Investment Tracker

Aplicação pessoal de investimentos em .NET 10, SQL Server e Angular. Abra `InvestmentTracker.slnx` no Visual Studio; a estrutura dos projetos foi preservada.

## Cadastros disponíveis

Tipos de ativo, países, moedas, categorias, setores e ativos possuem listagem, criação, edição e exclusão pela API e pelo Angular. As classificações são obrigatórias nos ativos. País e moeda são independentes. Registros referenciados não podem ser excluídos.

Veja [regras, contratos e escopo desta etapa](docs/cadastros.md).

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

Carteira/posições, patrimônio externo, metas e seus cálculos, dashboard e aportes continuam como etapas seguintes. A fórmula de aporte requer os critérios da planilha original. Docker, CI/CD, cloud e uma suíte E2E automatizada também são evoluções previstas; esta entrega conclui a sequência de cadastros da seção 42.
