# Investment Tracker

Aplicação pessoal de investimentos em .NET 10, SQL Server e Angular. Abra `InvestmentTracker.slnx` no Visual Studio. Mantidos os projetos existentes, as camadas API/Application/Domain/Infrastructure e namespaces C# com chaves.

## Funcionalidades atuais

- Cadastros: tipos, países, moedas, categorias, setores e ativos; referências em uso são protegidas.
- Carteiras com moeda-base própria, posições e patrimônio externo; valores originais ou convertidos, no padrão brasileiro.
- Dashboard com distribuições, metas, filtros e gráficos recolhíveis/reordenáveis.
- Metas por categoria e setor somando 100%; simulador de aportes sem alteração dos saldos.
- Compras/vendas com taxas, reinvestimento, proventos e movimentos de dinheiro, com atualização automática dos saldos.
- Histórico unificado, estorno auditável, desdobramento, grupamento e bonificação.
- Fotografias mensais versionadas, comparação mensal/anual e reabertura controlada.
- Proventos mensais/anuais por ativo, nas moedas originais ou na moeda-base, com conversão histórica e correções auditáveis.

## Instalação nova

Pré-requisitos: SDK .NET 10, SQL Server acessível (ou LocalDB no Windows), Node.js compatível com o Angular instalado e npm. Use o `package-lock.json` com `npm ci`. O frontend continua no projeto `.esproj` do Visual Studio.

Na raiz, configure a conexão em User Secrets; adapte o exemplo ao seu servidor:

```powershell
dotnet user-secrets set "ConnectionStrings:InvestmentTracker" "Server=(localdb)\MSSQLLocalDB;Database=InvestmentTracker;Trusted_Connection=True;TrustServerCertificate=True" --project InvestmentTracker.Api
dotnet run --project InvestmentTracker.Api -- --SeedCatalogs=true
```

A carga é explícita, aplica migrations e cria somente os cadastros iniciais e a Carteira Principal. Pode ser repetida sem duplicar os cadastros e sem inventar ativos ou valores pessoais. Não ocorre automaticamente ao iniciar a API.

Inicie a API e, em outro terminal, o frontend:

```powershell
dotnet run --project InvestmentTracker.Api --launch-profile https
```

```powershell
cd frontend/InvestmentTracker.Web
npm ci
npm start
```

Abra `http://127.0.0.1:65453`. O proxy encaminha `/api` para `https://localhost:7097`. Se necessário, confie no certificado local com `dotnet dev-certs https --trust`. A opção `secure: false` do proxy é exclusiva do desenvolvimento. O OpenAPI fica em `/openapi/v1.json` no ambiente Development. A API não abre o navegador automaticamente.

## Atualização de uma instalação existente

1. Pare a depuração/API, gere e valide um backup conforme [datas e backup](docs/datas-e-backup.md).
2. Aplique **todas as migrations pendentes**, sem recriar o banco:

```powershell
dotnet ef database update --project InvestmentTracker.Infrastructure --startup-project InvestmentTracker.Api
```

3. Reinicie API e frontend. Após alteração de dependências, execute `npm ci`.

A API verifica o modelo e as migrations na inicialização. Se o modelo tiver alteração sem migration, é necessário gerá-la e revisá-la durante o desenvolvimento. Se o banco estiver atrasado, a inicialização informa o comando de atualização. Não há aplicação silenciosa de migrations durante o uso normal.

A migration `IncomeHistoricalConversions` cria `IncomeConversion` e os campos de contexto no recebimento. Sem ela, o código novo pode produzir `Invalid object name 'IncomeConversion'`. Registros antigos permanecem sem equivalentes inventados; complemente-os pela página Proventos.

## Primeiro uso e rotina

**Instalação vazia:** cadastre os ativos e informe as posições e saldos iniciais que já possui. Configure metas. Confira o Dashboard e registre a primeira fotografia. Não registre compras antigas que já estejam incluídas no saldo inicial.

**Rotina:** use Compras e vendas para novas operações; Proventos para recebimentos; Movimentações para depósitos, retiradas, transferências, ajustes e estornos. Uma compra com dinheiro novo gera aporte; reinvestir saldo existente não gera outro aporte. Atualize os preços e registre/atualize a fotografia do mês.

A edição manual das posições atende a saldos iniciais, ajustes e atualização de valores. Ela não substitui o registro de novas compras/vendas. A edição de patrimônio externo altera nome/descrição; mudanças de saldo usam Movimentações.

## Câmbio

O provedor é [Frankfurter v2](https://frankfurter.dev/). Para os saldos atuais, existe cache persistente das taxas diárias. Para proventos, uma consulta com a data do recebimento produz uma conversão congelada. Moedas e datas são enviadas ao provedor; valores e identificadores pessoais ficam na aplicação.

O equivalente informado pelo usuário tem precedência. Na ausência de uma referência histórica adequada, o recebimento permanece registrado com conversão pendente. A complementação exige motivo, preserva versões anteriores e não credita novamente o saldo. Veja [recebimentos e câmbio histórico](docs/recebimento-de-proventos.md).

## Testes

Configure um servidor no qual seja permitido criar bancos temporários de teste:

```powershell
dotnet user-secrets set "ConnectionStrings:TestDatabase" "Server=(localdb)\MSSQLLocalDB;Database=InvestmentTrackerTests;Trusted_Connection=True;TrustServerCertificate=True" --project tests/InvestmentTracker.IntegrationTests
dotnet test tests/InvestmentTracker.UnitTests
dotnet test tests/InvestmentTracker.IntegrationTests
```

A suíte de integração substitui o nome da conexão por `InvestmentTracker_Tests_<guid>`, aplica migrations e remove somente sua base temporária. Os testes não utilizam dados da carteira pessoal. Também é possível executar `dotnet test` na solution.

No diretório do frontend:

```powershell
npm test -- --watch=false
npm run build
npm run test:e2e
```

O E2E usa Playwright, API real e banco temporário próprio, em desktop e celular emulado. Inclui carteira vazia, cadastros, operações, fechamentos, conversões auditáveis, metas e simulador. Veja [execução e diagnóstico](docs/testes-e2e.md).

## Acesso pelo celular

Com API iniciada e ambos os aparelhos na mesma rede privada:

```powershell
cd frontend/InvestmentTracker.Web
npm run start:lan
```

Abra `http://IP_DO_COMPUTADOR:65453`. O menu permanece acessível pelo botão fixo no header. A emulação automatizada não substitui a conferência no seu aparelho físico.

## Limitações e próximas etapas

- Fotografias passadas desatualizadas não são reconstruídas com valores de hoje. Versões sobrescritas antes da implementação do versionamento não podem ser recuperadas pelo sistema.
- Lançamentos atrasados com dependências ainda exigem estorno e relançamento ordenado; a automação guiada não faz parte desta entrega.
- Conversão histórica desconhecida não vira zero nem usa câmbio atual. Mudança de moeda-base pode exigir outro equivalente, sem apagar o anterior.
- O resultado econômico é monetário; TWR/XIRR dependem de histórico suficiente e não são calculados.
- Cotações automáticas dos ativos, ideias/reflexões, backup agendado, Docker, CI/CD e publicação continuam pendentes. O backup manual com validação de restauração já existe.
- Autenticação/autorização devem anteceder a exposição pública. O uso atual é local/privado.

## Documentação

- [Cadastros](docs/cadastros.md), [carteira e câmbio](docs/carteira-e-cambio.md).
- [Metas, dashboard e aportes](docs/metas-dashboard-aportes.md).
- [Compras e vendas](docs/compras-e-vendas.md), [movimentações e estornos](docs/movimentacoes-e-estornos.md).
- [Fechamentos, evolução e eventos](docs/fechamentos-proventos-e-eventos.md), [histórico](docs/historico-da-carteira.md).
- [Recebimentos e câmbio histórico](docs/recebimento-de-proventos.md).
- [Datas e backup](docs/datas-e-backup.md), [revisão funcional](docs/revisao-funcional.md).
