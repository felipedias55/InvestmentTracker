# InvestmentTracker.Web

Frontend Angular da solução `InvestmentTracker.slnx`. Cadastro de tipos, países, moedas, categorias, setores e ativos com Reactive Forms e rotas carregadas sob demanda.

```powershell
npm ci
npm start
```

Endereço: `http://127.0.0.1:65453`. Inicie também `InvestmentTracker.Api` com o perfil `https`; o proxy encaminha as chamadas `/api` para `https://localhost:7097`.

```powershell
npm run lint
npm run format:check
npm test -- --watch=false
npm run build
```

Os testes usam Vitest e HttpTestingController. O build de produção é gerado em `dist/InvestmentTracker.Web/browser`. O proxy de desenvolvimento não acompanha o build: em produção, o servidor deve encaminhar `/api` à API e as demais rotas da SPA ao `index.html`.

Consulte o [README da solução](../../README.md) para configuração do banco, carga inicial e testes do backend.

Use o Node de referência da `.nvmrc` da raiz e o npm indicado no package.json. A análise estática cobre TypeScript e templates Angular; o comando de formatação cobre configurações, SDK e workflow. Os scripts `lint:fix` e `format` aplicam as correções nesses respectivos escopos. A apresentação e o E2E têm artefatos separados em `test-results/presentation/` e `test-results/e2e/`. Consulte [qualidade e CI](../../docs/qualidade-e-ci.md) para comandos completos e limites da validação.
