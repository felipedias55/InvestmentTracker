# Revisão técnica e plano de evolução — 06/11/2026

## Objetivo e alcance desta revisão

Melhorar confiabilidade, desempenho percebido, compreensão dos fluxos e manutenção do FDInvestment Tracker. Esta entrega é uma análise inicial por leitura do código e um plano de execução; não implementa as mudanças propostas nem afirma que todo o projeto foi auditado dinamicamente.

Foram examinados rotas e componentes compartilhados Angular, formulários e histórico, configuração de lint/formatação e CI, consultas de ativos/posições/histórico, composição do dashboard, entidades financeiras e integração/cálculo dos benchmarks. Não foram consultadas APIs externas, dados da base pessoal ou executadas migrations. Não foram repetidos testes/build nesta revisão documental. As contagens de testes das entregas anteriores são históricas, não novas evidências desta análise.

Há muitas alterações anteriores ainda presentes na árvore de trabalho. Elas foram preservadas, sem staging, commits ou reorganização em massa.

Decisões anteriores continuam válidas: ideias, teses e diário foram retirados dos planos; backup automático está adiado; auditoria formal adicional de acessibilidade/aparelho físico e investigação de concorrência/escala não voltam a ser pendências do uso individual. Autenticação e infraestrutura pública continuam condicionadas à publicação. Os testes de teclado e foco dos novos componentes permanecem parte da qualidade normal de cada alteração.

## O que já é uma boa base

- Rotas Angular carregam páginas sob demanda (`app.routes.ts`); não há motivo identificado para substituir essa arquitetura.
- O resumo de histórico projeta metadados das fotografias sem carregar `DashboardJson` (`HistoryRepository.GetSnapshotsAsync`). Não propor novamente essa otimização como se estivesse ausente.
- Consultas de posições já usam `AsNoTracking`; há paginação de históricos, formulários protegidos e seletores visuais compartilhados.
- Dinheiro e cálculos financeiros permanecem no backend com decimal; conversões numéricas para apresentação/gráficos não são, por si só, bugs financeiros.
- CI executa lint, formatação de configurações, testes, build, apresentação, integração SQL e E2E (`.github/workflows/quality.yml`). Isso já existe e deve ser preservado.
- Imagens locais têm versões e alternativa visual; provedores têm proteções de consumo. Novas melhorias devem preservar essas garantias.

## Achados e hipóteses priorizadas

“Observado” significa confirmado na implementação lida; não implica lentidão ou erro financeiro comprovado. “A verificar” exige reprodução ou medição antes de alterar comportamento.

| ID | Prioridade | Evidência / situação | Trabalho proposto e validação |
| --- | --- | --- | --- |
| T01 | P1 | Observado: `BenchmarkProvider` mantém por 24h séries não vazias sem erro; `BenchmarkService` pode descobrir depois que falta um mês/uma extremidade. | Testar fonte parcialmente publicada que fica completa depois. Diferenciar validade da resposta e cobertura do intervalo; definir TTL/reconsulta apropriados e exibir quando consultar novamente. Não invalidar globalmente caches nem aumentar consumo sem necessidade. |
| T02 | P1 | Observado: CDI usa composição dos dias recebidos e bloqueia lacunas acima de sete dias; não tem calendário local. Uma ausência menor pode passar; intervalo só de fim de semana pode ficar indisponível. | Caracterizar essas fronteiras com exemplos sintéticos e documentação da série. Definir tratamento de feriados/dias sem observação antes de mudar o cálculo. Nunca preencher automaticamente uma taxa faltante com zero. É limitação de cobertura conhecida, não demonstração de retorno real incorreto. |
| T03 | P1 | Observado: `FredCsvFallback` inicia processo, drena stderr em tarefa separada e encerra o processo no finally. Testes de provedor simulam o transporte, sem cobrir todo o ciclo de vida do subprocesso. | Testar cancelamento, timeout, excesso de saída, executável ausente e término durante leitura; garantir observação de todas as tarefas e ausência de processos órfãos. Testes locais controlados, sem rede real. Preservar certificados, URL fixa e respeito a recusas HTTP. |
| T04 | P1 | Observado: dúvidas de uso incluem ajustar saldo externo; a ação está em Movimentações e não no cadastro desse saldo. | Conferir descoberta dessa ação e distinção entre ajuste total, depósito e retirada. Acrescentar ajuda contextual/atalho com contexto correto, se necessário. Testar que só navegar para a ação não grava nem altera valores. |
| T05 | P2 | Observado: `AssetRepository.GetAllAsync` retorna entidades com tracking e relações; posições carregam cinco relações. Não há medição atual de custo. | Medir consultas, materialização e bytes. Separar leitura de edição e avaliar projeção/AsNoTracking no caminho de listagem. Não trocar tracking em métodos usados para gravação sem conferir seus consumidores. |
| T06 | P2 | Observado: renderizar até 30 resultados no seletor não evita carregar o catálogo inteiro; filtro normaliza e ordena os dados em memória. | Medir catálogo representativo antes de mudar. Se relevante, normalizar/ordenar uma vez por atualização de catálogo; avaliar busca paginada no servidor só se a medição justificar. Preservar seleção, imagens e elegibilidade. |
| T07 | P2 | Observado: dashboard compõe resumo, metas de categoria/setor e patrimônio externo; histórico continua lendo fluxos para calcular retornos. | Instrumentar requisições e consultas por ação, procurando repetição e payload desnecessário. Não paralelizar operações EF no mesmo DbContext. Não paginar a base dos cálculos junto da página de exibição. |
| T08 | P2 | Observado: histórico tem template extenso; editor de estratégias mistura estado, template e estilos em arquivo com centenas de linhas. | Extrair componentes por responsabilidade, preservando containers de carregamento e regras no backend. Começar por unidades com contratos claros; tamanho isolado não exige reescrita. |
| T09 | P2 | Observado: Prettier verifica configurações, não todo TS/HTML/CSS; convivem arquivos compactados em linhas longas e arquivos formatados. | Definir convenção e adoção gradual por arquivo alterado. Separar commits de formatação de alterações funcionais; não formatar o repositório inteiro de uma vez. Avaliar verificação C# correspondente sem alterar namespaces com chaves. |
| T10 | P2 | Observado: entidades como `PortfolioAsset` expõem setters públicos; parte da proteção está em serviços/repositórios. | Encapsular primeiro uma operação financeira e suas invariantes, com testes de comportamento existentes. Evitar migração de esquema e alteração de contratos por estética. Verificar ajuste de posição, estorno e correção retroativa antes de expandir. |
| T11 | P2 | Observado: status de fonte/cache, carregamento, recarregamento e erros aparecem de formas diferentes entre fluxos. | Inventariar mensagens e adotar padrão: o que ocorreu, efeito nos dados e próxima ação. Distinguir recarregar dados locais de consultar um provedor. Manter tabelas/cartões, estados vazios, privacidade, temas e foco dos diálogos. |
| T12 | P2 | A verificar: custo real das suítes e estabilidade no ambiente atual. Houve timeouts nas execuções anteriores com tarefas pesadas simultâneas. | Medir tempo por suíte e cenário, repetir falhas focadas e separar gargalo do ambiente de instabilidade do teste. Não mascarar falhas aumentando todos os timeouts ou habilitando retries indiscriminados. |
| T13 | P3 | A verificar: dependências vulneráveis/desatualizadas, índices SQL, tarefas de fundo e retenção de dados de diagnóstico. | Fazer conferência específica antes de classificar risco; versões antigas ou ausência de índice não provam vulnerabilidade/gargalo. Não atualizar dependências principais em lote. Não confundir diagnóstico local com observabilidade de produção. |

P1 significa primeira rodada recomendada, não incidente crítico ou perda de dados comprovada. Nenhuma nova perda financeira foi reproduzida nesta revisão.

## Sequência de execução

### Fase 0 — estabelecer medidas

Registrar versão/estado da árvore, inventário de telas/endpoints, tempos de carregamento e requisições de Dashboard, Carteira, Ativos e Histórico. Medir primeira visita e retorno à tela separadamente. Usar dados fictícios e tamanhos comparáveis ao uso pessoal, sem teste de escala reaberto por suposição. Registrar tamanho de bundle/payload e duração das suítes.

Aceite: relatório reproduzível com máquina, conjunto de dados e método, sem metas arbitrárias de milissegundos ou ganho percentual inventado. Baseline de build/testes da árvore atual, sem confundir resultados históricos com execução nova.

### Fase 1 — confiabilidade e clareza de dados externos

Tratar T01–T03, cada um com caso reproduzível e teste que falharia antes da correção. Conferir invalidação ao mudar contexto, frescor, falhas parciais e consumo. Mudanças em cálculos exigem especificação de fronteiras e revisão de resultados conhecidos.

Aceite: comparação permanece fiel a datas/moeda, nenhuma gravação financeira, consumo explicado, cancelamento limpo e cobertura incompleta sem retorno inventado.

### Fase 2 — desempenho medido e padronização incremental

Executar T05–T10 conforme o baseline. Um fluxo por entrega, com comparação antes/depois e sem cache que oculte dados financeiros alterados. Manter regras financeiras em Application e persistência/HTTP em Infrastructure. Separar correção funcional, otimização e formatação para permitir revisão.

Aceite: gargalo medido reduzido sem mudar resultados e sem regressão de carregamento/edição. Se não houver ganho relevante, documentar e evitar complexidade adicional.

### Fase 3 — orientação e UI/UX

Executar T04 e T11 e iniciar o [plano de tutorial guiado](plano-tutorial-guiado.md). A ajuda deve explicar ações e consequências, sem transformar toda tela em documentação nem alterar operações para demonstrá-las.

Aceite: usuário consegue localizar o ajuste do saldo e distinguir ajuste, aporte/retirada e atualização de cotação. Validar desktop/celular, temas, navegação por teclado e cancelamento do tutorial como parte da entrega.

### Fase 4 — assistência conversacional opcional

Executar o [plano separado do chatbot](plano-chatbot.md), após existir uma base de ajuda revisada e reutilizável. A escolha do tutorial primeiro é de produto: responde às dúvidas atuais com passos verificáveis, sem necessidade de IA ou dados pessoais.

## Regra de conclusão por entrega

- Registrar problema, evidência, escopo e critério de aceite antes de implementar.
- Executar testes proporcionais: cálculo no backend; persistência/contratos na integração; interação no Angular; apresentação quando houver mudança visual; E2E para jornadas financeiras afetadas. Não repetir toda suíte após cada ajuste de texto.
- Informar o que mudou, o resultado medido, testes executados e limitações. Migrations só quando necessárias e nunca aplicadas automaticamente à base pessoal.
- Atualizar este plano e as próximas etapas; mensagens de commit em inglês.

Primeira execução recomendada: **Fase 0 e caracterização de T01**, antes de otimizações ou refatorações amplas. Este documento autoriza organização do trabalho, não trata todas as propostas como já implementadas.
