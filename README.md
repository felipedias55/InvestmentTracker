# Investment Tracker

O **Investment Tracker** é uma aplicação web para acompanhar uma carteira pessoal de investimentos no Brasil e nos Estados Unidos. Reúne posições, patrimônio externo, compras e vendas, proventos, planejamento de aportes e evolução patrimonial, preservando o histórico das operações e das correções.

Criado a partir de uma planilha, o projeto evoluiu para uma aplicação full stack com cálculos financeiros no backend, persistência relacional e interface responsiva. O foco atual é o uso local/privado: o núcleo do MVP já está em uso e as entregas seguintes ampliam análise, automação e usabilidade. Ainda não há autenticação ou preparação completa para publicação pública.

Documentação revisada em **28/10/2026**.

## Stack e arquitetura

| Área | Tecnologias e abordagem atuais |
| --- | --- |
| Backend | C# 14 / .NET 10, ASP.NET Core Web API com controllers, injeção de dependência e tratamento de erros com Problem Details |
| Persistência | Entity Framework Core 10, SQL Server e migrations; LocalDB como opção de desenvolvimento no Windows |
| Frontend | Angular 22, TypeScript 6, componentes standalone, Signals, RxJS 7.8 e Reactive Forms |
| Interface | Angular Material/CDK 22 na navegação e telas, incluindo Dashboard, Carteira, Movimentações/correções, Compras/vendas/eventos, Proventos, Evolução/fechamentos, Ativos/cadastros/patrimônio externo e Metas/estratégias/simulador; tema azul próprio, gráficos próprios, layout responsivo, formatação brasileira, moeda original/base e ocultação de valores. |
| Integrações | Frankfurter v2 para câmbio; brapi para cotações brasileiras; Alpha Vantage para fechamento de ativos dos EUA |
| Testes .NET | xUnit, Moq, ASP.NET Core MVC Testing e Coverlet; integração com SQL Server em bancos temporários |
| Testes web | Vitest e jsdom; Playwright para fluxos E2E e apresentação em desktop/celular emulado |
| Ferramentas | Solução Visual Studio .slnx, frontend integrado por .esproj, npm/package-lock e scripts PowerShell |
| Configuração local | .NET User Secrets para conexão e chaves; nenhuma chave ou base com dados pessoais deve ser versionada |

As versões principais acima refletem os projetos e o package.json; as versões exatas das dependências web estão no package-lock.json.

A solução mantém quatro camadas:

- **Domain:** entidades e conceitos do domínio, sem referência às demais camadas.
- **Application:** casos de uso, contratos, DTOs, validações e cálculos, incluindo planejamento de aportes e métricas.
- **Infrastructure:** EF Core, mapeamentos, migrations, repositórios e integração HTTP com provedores externos.
- **API:** endpoints e composição das dependências. O Angular consome a API e apresenta os resultados.

Operações financeiras suportadas utilizam transações, validações de concorrência e identificação de solicitações para evitar duplicidade. Correções preservam vínculos de auditoria. Essa organização em camadas não pressupõe microserviços ou um domínio totalmente encapsulado: o refinamento das entidades ainda consta no planejamento.

Abra InvestmentTracker.slnx no Visual Studio. A organização atual da solução e os namespaces C# com chaves são mantidos.

## Funcionalidades atuais

- Cadastros: tipos, países, moedas, categorias, setores e ativos; referências em uso são protegidas.
- Carteiras com moeda-base própria, posições e patrimônio externo; valores originais ou convertidos, no padrão brasileiro.
- Dashboard com distribuições, metas, filtros e gráficos recolhíveis/reordenáveis; interface responsiva com controles Material, destaques em azul discreto, logo original, temas claro/escuro/sistema e controle de privacidade dos valores.
- Estratégias com modelos de metas, aportes regulares, buy and hold, renda, núcleo/complementos, faixas, imobiliário fixo e personalizada; reservas, exclusões da base do restante e configurações versionadas por carteira.
- Metas por categoria e setor somando 100%; simulador lado a lado com gráficos, combinação de setor/categoria já presente na carteira, valores na moeda correspondente e marcações visuais de itens já atendidos. O cálculo ocorre no backend e a simulação não altera saldos.
- Compras/vendas com taxas, reinvestimento, proventos e movimentos de dinheiro, com atualização automática dos saldos.
- Histórico unificado, estorno auditável, desdobramento, grupamento e bonificação.
- Prévia de dependências e fotografias afetadas por correções ou lançamentos atrasados, sem alterar saldos.
- Simulação e aplicação atômica de correções retroativas em operações suportadas, com estornos e substitutos auditáveis.
- Fotografias mensais versionadas, comparação mensal/anual e reabertura controlada.
- Proventos mensais/anuais por ativo, nas moedas originais ou na moeda-base, com conversão histórica e correções auditáveis.

- Consulta de cotações por mercado, preenchimento manual ou automático em lote e revisão antes de salvar; informações opcionais de fonte/data/horário no painel.
- Indicador de valorização/desvalorização em relação ao preço médio, com percentual e sinalização visual; não representa retorno total com proventos.
- Evolução com Dietz estimado e XIRR anualizada, condicionados à qualidade e disponibilidade do histórico.
- Backup manual com validação de restauração e instruções para levar os dados a outra máquina.

A dashboard reúne resumo mensal/anual com comparação de proventos, evolução, destaques sobre o preço médio e movimentações recentes. Os gráficos e a lista de movimentações podem ser movidos, redimensionados e recolhidos, com preferências por carteira salvas no navegador. Períodos parciais e referências incompletas são sinalizados; valores atuais e históricos permanecem distintos.

## Instalação nova

Pré-requisitos: SDK .NET na faixa indicada em `global.json` (10.0.401 ou patch posterior da mesma faixa), SQL Server acessível (ou LocalDB no Windows), Node.js de referência em `.nvmrc` e npm indicado no package.json. Use o `package-lock.json` com `npm ci`. O frontend continua no projeto `.esproj` do Visual Studio.

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

A tela de Movimentações oferece paginação na API, busca e filtros por tipo, status e período. Use **Ver detalhes** para consultar a auditoria, estornar ou corrigir um lançamento. **Movimentar saldo** abre o formulário de depósito, retirada, transferência ou ajuste, sem ocupar permanentemente o histórico.

Compras e vendas também possui histórico paginado, filtros por ativo/tipo/período e indicação de estornos. Os botões **Registrar compra**, **Registrar venda** e **Registrar evento societário** abrem os formulários sob demanda. Preço unitário, taxas e origem/destino ficam disponíveis em **Ver detalhes**.

Proventos separa **Recebimentos** de **Análises**, com filtros independentes. O histórico é paginado e permite localizar conversões pendentes na moeda-base atual. Registro e revisão do câmbio abrem sob demanda; a análise mantém os totais históricos completos, independentemente da página consultada.

Evolução e fechamentos separa **Evolução**, **Fotografias** e **Aportes/retiradas**. Fotografias têm lista mensal paginada e detalhes com versões anteriores em painel; fechar/reabrir períodos exige confirmação. Aportes e retiradas usam paginação na API e filtros de ano/tipo. Os gráficos e métricas consideram o histórico completo, independentemente da página da lista. Registros históricos antigos não alteram saldos.

Ativos e cadastros auxiliares têm busca, paginação e formulários Material sob demanda. Ativos podem ser filtrados por categoria/setor. Patrimônio externo inclui busca e filtro de moeda, mantendo o total integral da carteira; ajustes de saldo continuam auditáveis em Movimentações. A paginação dessas listas é local, sem limitar os totais nem as opções de referência.

Metas e regras de estratégia têm busca e paginação sem limitar o conjunto salvo ou seus totais. A configuração da estratégia abre em painel; o simulador destaca primeiro o plano conjunto e mantém gráficos por categoria/setor lado a lado no desktop. Marcações de investido são temporárias e não registram operações.

**Rotina:** use Compras e vendas para novas operações; Proventos para recebimentos; Movimentações para depósitos, retiradas, transferências, ajustes e estornos. Uma compra com dinheiro novo gera aporte; reinvestir saldo existente não gera outro aporte. Atualize os preços e registre/atualize a fotografia do mês.

A edição manual das posições atende a saldos iniciais, ajustes e atualização de valores. Ela não substitui o registro de novas compras/vendas. A edição de patrimônio externo altera nome/descrição; mudanças de saldo usam Movimentações.

Filtros, ordenação, página e rolagem das consultas são recuperados ao navegar na mesma sessão, separados por tela e carteira. Recarregar a página reinicia esse contexto. Comprar/Vender/Registrar recebimento nos detalhes de uma posição leva a carteira e o ativo; **Voltar à Carteira** recupera a consulta anterior. Trocar de carteira usa o contexto da carteira de destino.

No Dashboard, Evolução e Proventos também recuperam seus filtros por carteira: frequência, ano, fechamento selecionado, moeda e tamanho/posição da janela de proventos. Recarregar dados salvos mantém essas escolhas; recarregar a página reinicia o contexto. Os detalhes voltam fechados. Anos ou moedas ausentes nos dados atualizados usam uma seleção disponível, sem inventar valores.

Formulários alterados pedem confirmação ao cancelar, fechar ou sair; recusar mantém os campos. Durante processamento, a saída é bloqueada. Formulários intocados e alterações apenas visuais não pedem confirmação. Rascunhos financeiros não são recuperados após recarga. No simulador, resultados e marcas de investido são temporários e também desaparecem ao sair da tela.

Em **Organizar dashboard**, cada gráfico oferece alça, **Subir**, **Descer** e tamanho em uma faixa própria. As setas seguem a ordem de leitura e funcionam no celular e por teclado; Esc cancela o arraste. Ordem, formato, tamanho e recolhimento ficam salvos por carteira neste navegador. **Restaurar organização padrão** reinicia essas preferências. A organização não altera valores financeiros. Veja o [registro de usabilidade e suas limitações de validação](docs/plano-usabilidade-e-navegacao.md).

## Atualização de cotações

Na página **Carteira**, filtre as posições e use **Atualizar cotações em lote → Buscar cotações → Revisar → Confirmar atualização do lote**. Só são consultados campos vazios de posições com quantidade positiva.

- Brasil + BRL utiliza **brapi**; Estados Unidos + USD utiliza **Alpha Vantage**. Não são chamadas as duas APIs para cada ativo.
- Brasil utiliza a cotação disponível com atraso; EUA utiliza fechamento diário. Não se trata de acompanhamento em tempo real.
- O ícone **ⓘ** aparece no painel após uma consulta bem-sucedida: mouse, foco pelo teclado ou toque revelam fonte e horários disponíveis, em Brasília. A Alpha Vantage não fornece horário de mercado nesse endpoint.
- **Recarregar dados** apenas relê os valores salvos. **Atualizar posições**, na dashboard, abre a carteira. Esses botões não consultam os provedores.
- A data de atualização da posição é a data da gravação; não equivale necessariamente à data de mercado da cotação.
- Na brapi, o cache fica em memória por 30 minutos. Para a Alpha Vantage, o cache de 12 horas e o controle local de tentativas/bloqueios persistem em disco por chave, inclusive após reiniciar a API. A pasta padrão é `%LOCALAPPDATA%\InvestmentTracker\QuoteState`; `Quotes:StateDirectory` permite alterá-la. Falhas de acesso ao estado são informadas e interrompem a consulta. Reiniciar ou apagar arquivos não renova a cota real do provedor. Consulte [cache e consumo](docs/cotacoes-automaticas.md#cache-e-consumo).
- Recebimentos de proventos são registrados pelo usuário e independem da atualização de preços. Não existe importação automática de dividendos recebidos.

As chaves devem ser configuradas nos User Secrets de cada instalação. Consulte [configuração, limites e validações das APIs](docs/cotacoes-automaticas.md). A atualização não ocorre em segundo plano e não é salva sem confirmação.

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
npm run test:presentation
npm run test:e2e
```

`npm run lint` verifica TypeScript e templates Angular, inclusive inline, sem aceitar avisos. `npm run format:check` verifica a formatação de configurações JSON/MJS/CJS e do workflow; não reformata em massa os fontes existentes. O workflow [Quality](.github/workflows/quality.yml) está preparado para executar essas verificações, build, testes Angular, apresentação, testes .NET, integração SQL e E2E em push/PR. Veja [qualidade e CI](docs/qualidade-e-ci.md) para escopo, versões e resultados locais; a primeira execução no GitHub ainda precisa ser confirmada após enviar as alterações.

O E2E usa Playwright, API real e banco temporário próprio, em desktop e celular emulado. Inclui carteira vazia, cadastros, operações, fechamentos, conversões auditáveis, metas e simulador. Veja [execução e diagnóstico](docs/testes-e2e.md).

A suíte `test:presentation` intercepta os endpoints com dados fictícios e executa jornadas de interface em desktop/celular e temas claro/escuro. Não inicia API nem usa banco de dados. Inclui contexto após salvar, descarte de alterações, auditoria, retorno do simulador, privacidade e organização dos gráficos; emulação não substitui aparelho físico nem valida a imagem nativa de arraste do sistema operacional.

## Acesso pelo celular

Com API iniciada e ambos os aparelhos na mesma rede privada:

```powershell
cd frontend/InvestmentTracker.Web
npm run start:lan
```

Abra `http://IP_DO_COMPUTADOR:65453`. O menu permanece acessível pelo botão fixo no header. A emulação automatizada não substitui a conferência no seu aparelho físico.

## Limitações e próximas etapas

- Fotografias passadas desatualizadas não são reconstruídas com valores de hoje. Versões sobrescritas antes da implementação do versionamento não podem ser recuperadas pelo sistema.
- Lançamentos atrasados e correções contam com diagnóstico, simulação financeira e aplicação em lote. Cadeias com eventos societários, ajustes de posição ou auditoria insuficiente continuam exigindo conferência manual.
- Conversão histórica desconhecida não vira zero nem usa câmbio atual. Mudança de moeda-base pode exigir outro equivalente, sem apagar o anterior.
- Evolução exibe Dietz estimado no intervalo e XIRR anualizada com limites explícitos e bloqueios por dados insuficientes. TWR exato continua pendente de avaliações a cada fluxo externo.
- Cotações sob demanda já foram utilizadas e confirmadas pelo usuário. Ainda há espaço para ampliar a verificação de cobertura, mapear símbolos explicitamente e persistir cache/controle de consumo da brapi. A persistência da Alpha Vantage já existe. Não há garantia de disponibilidade ou consistência de todos os dados externos.
- Meta de 0% impede aporte sugerido ao grupo, mas não retira seus ativos da base de cálculo. No simulador de estratégias, a exclusão por categoria permite retirar imobiliário da base do restante, mantendo-o no patrimônio.
- Ideias/reflexões, base de demonstração, Docker, CD, publicação e observabilidade ampliada permanecem no planejamento. O workflow de CI já foi preparado e validado localmente, com execução hospedada a confirmar. Backup automático foi adiado por decisão do usuário; o procedimento manual já atende ao uso atual.
- Autenticação/autorização devem anteceder a exposição pública. O uso atual é local/privado.

## Documentação

- [Próximas etapas e estágio atual do projeto](docs/proximas-etapas.md).

- [Cadastros](docs/cadastros.md), [carteira e câmbio](docs/carteira-e-cambio.md).
- [Estratégias de investimento e modelos](docs/estrategias-de-investimento.md).
- [Metas, dashboard e aportes](docs/metas-dashboard-aportes.md).
- [Simulador de aportes: como são escolhidos setor, categoria e moeda](docs/simulador-de-aportes.md).
- [Compras e vendas](docs/compras-e-vendas.md), [movimentações e estornos](docs/movimentacoes-e-estornos.md).
- [Correções atrasadas: diagnóstico, simulação e aplicação](docs/correcoes-atrasadas.md).
- [Fechamentos, evolução e eventos](docs/fechamentos-proventos-e-eventos.md), [histórico](docs/historico-da-carteira.md).
- [Métricas de rentabilidade: Dietz e XIRR](docs/metricas-de-rentabilidade.md).
- [Apresentação, gráficos e testes visuais](docs/apresentacao-e-graficos.md).
- [Otimização do carregamento e do logo](docs/otimizacao-carregamento.md).
- [Contexto analítico do Dashboard e navegação por carteira](docs/contexto-analitico-dashboard.md).
- [Análise estática, versões e integração contínua](docs/qualidade-e-ci.md).
- [Plano de migração da interface, padrões e modo escuro](docs/entregas-layout.md).
- [Plano de usabilidade e navegação: inventário e padronização das ações](docs/plano-usabilidade-e-navegacao.md).
- [Atualização manual de cotações em lote](docs/cotacoes-manuais-em-lote.md).
- [Cotações automáticas: provedores gratuitos e configuração](docs/cotacoes-automaticas.md).
- [Recebimentos e câmbio histórico](docs/recebimento-de-proventos.md).
- [Datas e backup](docs/datas-e-backup.md), [revisão funcional](docs/revisao-funcional.md).
