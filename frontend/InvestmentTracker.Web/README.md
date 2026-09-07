# InvestmentTracker.Web

Frontend Angular da solução `InvestmentTracker.slnx`. Cadastro de tipos, países, moedas, categorias, setores e ativos com Reactive Forms e rotas carregadas sob demanda.

```powershell
npm ci
npm start
```

Endereço: `http://127.0.0.1:65453`. Inicie também `InvestmentTracker.Api` com o perfil `https`; o proxy encaminha as chamadas `/api` para `https://localhost:7097`.

```powershell
npm test -- --watch=false
npm run build
```

Os testes usam Vitest e HttpTestingController. O build de produção é gerado em `dist/InvestmentTracker.Web/browser`. O proxy de desenvolvimento não acompanha o build: em produção, o servidor deve encaminhar `/api` à API e as demais rotas da SPA ao `index.html`.

Consulte o [README da solução](../../README.md) para configuração do banco, carga inicial e testes do backend.
