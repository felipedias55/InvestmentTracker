# Análise do projeto — 25/10/2026

Decisão de escopo em **28/10/2026**: o usuário encerrou as conferências adicionais de acessibilidade/aparelho físico e a investigação de consistência sob concorrência/escala, considerando o uso pessoal por uma única pessoa. As referências a esses trabalhos abaixo preservam o diagnóstico histórico e não representam pendências atuais. Não foram executadas novas auditorias para esse encerramento.

Atualização em **26/10/2026**: o achado 0 foi resolvido. A suíte E2E foi adaptada aos menus, formulários e detalhes atuais, preservando as verificações financeiras. Os 12 cenários passaram em execução completa, em desktop e celular emulado; a base temporária foi removida. Os resultados e recomendações abaixo registram o diagnóstico original de 25/10; as demais pendências permanecem abertas. Veja [validação E2E atualizada](testes-e2e.md).

Segunda entrega em **26/10/2026**: os achados 1 e 2 foram tratados pela [otimização do carregamento](otimizacao-carregamento.md): pacote inicial de 455,76 kB, sem ampliar budgets, e logo publicado de 20.451 bytes. O original foi preservado fora da pasta pública; o menu de temas passa a carregar na primeira interação. As demais recomendações do diagnóstico original continuam abertas.

Terceira entrega em **27/10/2026**: o achado 3 foi tratado pelo [contexto analítico por carteira](contexto-analitico-dashboard.md), com filtros recuperados durante a sessão, tratamento de dados indisponíveis e contenção de gráficos extensos no celular. Os achados 4 e 5 também foram tratados: README e planejamento agora distinguem o cache persistente dos EUA do cache em memória do Brasil, e os registros históricos indicam as entregas posteriores. As verificações físicas e a auditoria formal de acessibilidade continuam pendentes.

Quarta entrega em **28/10/2026**: implementação local do achado 6 em [qualidade e CI](qualidade-e-ci.md), com ESLint, formatação de configurações, remoção de dependência sem uso, SDK/Node declarados e workflow preparado. Foram aprovados 565 testes/cenários locais, incluindo a repetição simultânea das suítes com diretórios de artefatos separados. A primeira execução do workflow nos runners GitHub ainda está pendente do envio das alterações. Os registros abaixo conservam o diagnóstico original.

## Escopo e conclusão

Revisão da estrutura, documentação, composição da API, persistência, carteira, alocação/estratégias, cotações, histórico, contexto de navegação e ferramentas de qualidade, acompanhada da execução das suítes existentes. Trata-se de revisão técnica e funcional por amostragem dos fluxos críticos; não equivale a auditoria formal de segurança, acessibilidade ou reconciliação dos dados pessoais.

O projeto tem um núcleo consistente para uso pessoal: camadas separadas, cálculos na Application, operações transacionais, restrições relacionais, controle de concorrência nas posições, identificação de solicitações financeiras, correções auditáveis e fotografias versionadas. A migração visual e o refinamento do Dashboard estão concluídos conforme os registros atuais. As próximas entregas devem consolidar desempenho, contexto de navegação, documentação e verificação operacional, antes de ampliar funcionalidades.

Nenhuma mudança funcional foi feita nesta análise. As verificações de banco utilizam as bases temporárias geradas pelas suítes, sem utilizar a carteira pessoal. Não foram consultados provedores com chaves pessoais nem executado backup da base oficial.

## Achados confirmados

### 0. E2E com API real desatualizado após o layout — prioridade alta

A execução de `npm run test:e2e` chegou à API e criou carteira/ativos/posições na base temporária, mas o primeiro cenário falhou por timeout ao procurar o botão exato `Atualizar cotações em lote`. A interface atual apresenta `Atualizar cotações dos N resultados filtrados`. O problema foi confirmado comparando `e2e/portfolio.spec.ts:18` com `portfolio-page.html`.

O cenário seguinte e outros trechos também conservam o fluxo anterior: procuram `Nova carteira` sem abrir `Mais ações`, preenchem o cadastro de ativo sem abrir `Novo ativo` e esperam o formulário de operações permanentemente visível. Essas incompatibilidades adicionais foram identificadas por leitura; não foram todas executadas até a falha. A execução foi interrompida após diagnosticar a primeira falha para evitar repetir esperas sobre controles antigos. O script pai concluiu a remoção da base temporária, confirmada na saída.

Impacto: os 56 testes de apresentação com APIs fictícias continuam passando, mas a suíte real não comprova atualmente as jornadas financeiras completas no navegador após a migração. Isso é uma regressão da automação confirmada, não uma demonstração de erro nos cálculos.

Recomendação: atualizar a suíte E2E para os menus/painéis atuais, preferindo seletores por papel e nome com escopo adequado, e preservar todas as verificações de saldos, proventos, auditoria, filtros e fotografias. Reexecutar os 12 cenários em desktop e celular na base isolada. Essa deve ser a primeira entrega técnica antes de novas funcionalidades.

### 1. Desempenho: pacote inicial acima do orçamento — prioridade média

O build de produção passa, mas gera 524,92 kB iniciais para orçamento de aviso de 500 kB: excesso de 24,92 kB, aproximadamente 5%. A transferência estimada pelo build é 131,11 kB; o tamanho bruto não deve ser confundido com o tráfego comprimido. As páginas já usam carregamento sob demanda em `app.routes.ts`.

Recomendação: gerar estatísticas do build, identificar o peso dos imports compartilhados e da estrutura inicial e reduzir o necessário para voltar ao orçamento, preservando os limites. Não há evidência nesta revisão de que remover uma dependência específica, isoladamente, resolva o aviso.

Referências: `frontend/InvestmentTracker.Web/angular.json`, `src/app/app.routes.ts` e saída de `npm run build`.

### 2. Logo maior que o necessário — prioridade média

`public/fd-logo.png` possui 808.065 bytes, aproximadamente 789 KiB. O navegador emite NG0913 por dimensões intrínsecas muito superiores às de exibição. O arquivo é copiado como asset e não compõe os 524,92 kB do pacote inicial: são oportunidades distintas de redução de carga.

Recomendação: criar uma versão otimizada na resolução de exibição, considerando telas de alta densidade, preservando transparência e identidade visual. Validar a aparência nos dois temas e conferir a redução de bytes e o desaparecimento do aviso.

### 3. Contexto analítico do Dashboard incompleto — prioridade média

`dashboard-page.ts` recupera busca, status e ordenação usando `rememberContext`. Em `dashboard-analysis.ts`, ano, período, moeda e janela de proventos permanecem em signals locais. A saída da rota pode perder essas escolhas, embora outras consultas recuperem seu estado.

Recomendação: incorporar os filtros analíticos ao contexto em memória por carteira. Preservar a decisão atual de reiniciar o contexto ao recarregar a aplicação; não persistir rascunhos financeiros por consequência dessa melhoria. Validar ida/volta de rota, troca de carteira e indisponibilidade da moeda selecionada.

Referências: `src/app/shared/page-context.ts`, `features/dashboard/dashboard-page.ts` e `dashboard-analysis.ts`.

### 4. Documentação de cotações diverge da implementação — prioridade média

O README descreve cache de cotações apenas em memória e o planejamento ainda apresenta persistência de cache/consumo como evolução futura. Entretanto, `FreeAssetQuoteProvider` e `QuoteRequestState` já persistem, para os EUA, cache de 12 horas, tentativas em janela de 24 horas, próxima chamada e bloqueios informados pelo provedor. O arquivo é separado por hash da chave e há exclusão entre processos por arquivo de lock. O cache brasileiro continua em memória.

Recomendação: atualizar README e planejamento para distinguir BR e EUA e apontar para `docs/cotacoes-automaticas.md`, que já descreve corretamente a pasta configurável `Quotes:StateDirectory` e as falhas de acesso ao estado. Não implementar novamente a persistência já existente. Não foi validado o limite comercial vigente do provedor; esta análise descreve o comportamento do código.

### 5. Documentação histórica pode ser interpretada como pendência atual — prioridade baixa

`docs/revisao-funcional.md` ainda lista XIRR e automação de correções atrasadas como pendentes, enquanto implementações posteriores já entregaram XIRR e parte das correções. `docs/refinamento-dashboard.md` conserva frases de etapas intermediárias indicando etapa 4 pendente, seguida da conclusão dessa etapa.

Recomendação: identificar claramente registros históricos e colocar no início o estado atual e os links para as entregas posteriores. Manter a trilha de decisões sem transformar anotações antigas em backlog duplicado.

### 6. Ferramentas de qualidade sem fluxo completo — prioridade média

O frontend declara ESLint e Prettier, mas não oferece scripts de lint/formatação; não foi encontrada configuração ESLint no inventário do repositório. `jest-editor-support` está em dependencies com versão `*` e não apareceu nos imports de `src`. Não foi encontrado workflow de CI nem `global.json`.

Recomendação: confirmar a necessidade de `jest-editor-support` e removê-lo ou classificá-lo como ferramenta de desenvolvimento se aplicável; definir scripts e configuração de análise estática; fixar a política de SDK .NET. O lockfile já melhora a reprodução via `npm ci`: o wildcard não implica atualização automática em cada execução. Introduzir CI para build e testes, com SQL temporário isolado na etapa de integração. A seleção de versões e auditoria de vulnerabilidades exigem verificação própria; não foram feitas nesta revisão.

### 7. Endpoint de saúde superficial — prioridade baixa no uso local

`HealthController` retorna `status = ok` sem consultar dependências. Isso informa que a API respondeu, mas não comprova disponibilidade do SQL Server após a inicialização. `DatabaseSchemaGuard` já verifica modelo e migrations ao iniciar, o que é uma proteção complementar.

Recomendação: distinguir liveness de readiness e verificar banco na readiness, sem chamar provedores pagos/limitados em cada probe. Elevar a prioridade quando houver hospedagem ou monitoramento automatizado.

### 8. CORS conserva origem antiga — prioridade baixa

`Program.cs` permite `http://localhost:4200`; a instalação documentada usa frontend em `127.0.0.1:65453` e proxy. O proxy atual torna esse desencontro irrelevante para o fluxo normal, mas a configuração fica ambígua para acesso direto à API ou distribuição futura.

Recomendação: documentar o uso do proxy e tornar origens configuráveis quando houver acesso entre origens. Evitar ampliação genérica de CORS. A aplicação continua sem autenticação/autorização; essa é uma condição conhecida e bloqueadora para exposição pública, não uma nova exigência para o uso pessoal atual.

## Riscos que exigem validação direcionada

### Consistência das leituras — prioridade média, maior com concorrência

`AllocationService` monta resumo, metas por categoria/setor e patrimônio externo em consultas sucessivas, sem um recorte transacional único. Uma escrita concorrente pode produzir uma análise com partes de instantes diferentes. Isso não demonstra corrupção das gravações; operações críticas já usam transações e bloqueios. A fotografia possui proteção própria em `HistoryRepository.CaptureAsync`.

Próximo passo: reproduzir leitura versus escrita concorrente e definir a consistência necessária. Se preciso, obter dados locais em um snapshot curto. Evitar manter bloqueios do SQL durante chamadas externas de câmbio.

### Escala de histórico e consultas — prioridade baixa no volume pessoal

Há paginação de listas financeiras, mas métodos analíticos ainda carregam conjuntos completos, como `HistoryRepository.GetCashFlowsAsync` e `PortfolioRepository.GetPositionsAsync`. Isso pode ser adequado ao tamanho atual da carteira; paginação visual não limita automaticamente custo de análise.

Próximo passo: medir com histórico fictício volumoso, examinar planos e só então selecionar agregações no banco, consultas por intervalo e índices. Preservar a regra de que totais analíticos não dependem da página da lista.

### Datas e relógio — prioridade baixa

Serviços já usam `TimeProvider` e data de negócio em São Paulo; ainda existem chamadas diretas a `DateTime.UtcNow`, por exemplo em criação de carteira e gravação de movimento de ajuste. Não foi identificado defeito de data por esse motivo, mas a mistura limita testes determinísticos.

Próximo passo: uniformizar injeção de relógio nos pontos de auditoria e testar a virada UTC/São Paulo, mantendo distintos instante UTC, data da operação e data da cotação.

## Pendências funcionais reais

| Tema | Estado e próximo passo |
| --- | --- |
| Acessibilidade e uso móvel | Validar aparelho físico, teclado virtual, zoom real de 200%, contraste, leitor de tela e imagem nativa de arraste. Emulação automatizada não encerra esses itens. |
| Cotações | Conferir cobertura dos ativos e símbolos, tratamento de inconsistências e informação de fonte/data nos cartões. Persistência dos EUA já existe. |
| Correções retroativas | Ampliar cadeias com eventos societários, ajustes e auditoria suficiente. Casos sem dados confiáveis devem continuar bloqueados ou exigir conferência. |
| Estratégias | Modelos básicos e exclusões já estão implementados. Evoluções: concentração, prioridades mais amplas, planejado versus realizado e objetivos/renda. |
| Classificação imobiliária | O backlog menciona substituir identificação por nomes; o editor atual já pede seleção explícita de categorias. Antes de alterar o modelo, localizar o uso residual da heurística e definir a finalidade da classificação estruturada. |
| Rentabilidade | Dietz estimado e XIRR existem. TWR exato exige avaliações por fluxo externo; permanece evolução própria. |
| Ideias e reflexões | Funcionalidades novas de produto, sem necessidade de reabrir a rodada concluída de layout. |
| Demonstração e distribuição | Preparar base fictícia isolada; CI, empacotamento e autenticação conforme o destino de distribuição. |
| Backup | Procedimento manual com restauração e CHECKDB já existe. Automação permanece adiada por decisão do usuário. Esta revisão não repetiu a restauração. |

## Sequência recomendada

1. Atualizar os fluxos e seletores da suíte E2E real e aprovar novamente os 12 cenários, preservando as verificações financeiras.
2. Consolidar documentação atual, otimizar logo e investigar o pacote inicial. Entrega pequena, com resultado mensurável.
3. Completar contexto analítico do Dashboard e executar a conferência móvel/acessibilidade com registro reproduzível dos problemas.
4. Formalizar análise estática e execução automatizada das suítes; reduzir ruído dos mocks de navegador nos testes.
5. Validar consistência de leitura e escala com dados fictícios antes de alterar a arquitetura de consultas.
6. Retomar as funcionalidades conforme o uso real: cobertura de cotações, correções bloqueadas e planejamento. Ideias/diário podem seguir como entregas separadas.

## Verificações desta rodada

- Build Angular de produção: aprovado, com aviso de pacote inicial descrito acima.
- Testes unitários C#: 214 aprovados.
- Testes Angular/Vitest: 125 aprovados em 26 arquivos. Há mensagens do jsdom sobre `confirm`/`scrollTo`; não falharam assertions, mas convém fornecer mocks explícitos nos casos relevantes.
- As primeiras tentativas no ambiente restrito falharam por permissões de execução/arquivos temporários; a repetição com permissões adequadas aprovou as suítes unitárias. Essas falhas iniciais não foram classificadas como regressões da aplicação.
- Integração SQL: 149 testes aprovados, sem ignorados, em banco temporário próprio.
- Apresentação Playwright: 56 cenários aprovados em desktop/celular e claro/escuro; APIs fictícias.
- Total das quatro suítes concluídas: 544 testes/cenários aprovados.
- E2E Playwright com API real: primeira falha confirmada por seletor desatualizado; execução dos 12 cenários interrompida após diagnóstico. Não contabilizada como aprovada. Base temporária removida pelo script ao encerrar. Ver achado 0.

Os testes existentes não demonstram, por si só, ausência de todos os defeitos financeiros, de segurança ou de acessibilidade. Não foi feita medição de cobertura, auditoria de dependências, teste de carga ou comparação de saldos pessoais nesta rodada.
