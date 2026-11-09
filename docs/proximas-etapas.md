# Próximas etapas

Lista atualizada em 06/11/2026. As prioridades abaixo orientam as próximas entregas; não representam autorização para implementar todos os itens de uma vez.

## Rodada técnica e orientação de uso

O [plano consolidado de Finanças pessoais](plano-financas-pessoais.md) registra o escopo definido com o usuário: projetos próprios na mesma solution, uma API e um Angular, coloração distinta, entradas manuais, compromissos e calendário, saldos/projeções por conta e avisos antes de destinar dinheiro a investimentos. Não há integração financeira inicial nem implementação autorizada por esse registro.

A [revisão técnica e plano de melhorias](plano-melhorias-tecnicas.md) registra achados do código, hipóteses a medir, prioridades e critérios de aceite para desempenho, UI/UX, padronização e possíveis erros. A próxima execução recomendada é estabelecer o baseline e caracterizar cobertura/cache dos benchmarks, sem refatoração em massa.

Foram abertos dois planos independentes: [tutorial guiado](plano-tutorial-guiado.md), priorizado primeiro, e [chatbot de ajuda](plano-chatbot.md), posterior e opcional. Ambos estão planejados, sem implementação ou ativação de APIs de IA nesta rodada. O primeiro roteiro proposto é ajustar saldo externo.

## Prioridades

Decisão de escopo em **05/11/2026**: ideias de investimento, teses e diário de decisões foram retirados dos planos por solicitação do usuário. Não permanecem como pendências ou entregas adiadas. Benchmarks mantêm seu escopo próprio.

Decisão de escopo em **28/10/2026**: o usuário encerrou, para o uso pessoal e individual atual, os itens de validação adicional de acessibilidade/aparelho físico e de investigação de consistência sob concorrência/escala. Esses trabalhos não devem ser reabertos como pendências do uso atual; o encerramento é uma decisão de escopo, sem afirmar que auditorias formais ou testes adicionais foram executados. Podem ser reavaliados se o público, volume ou modo de distribuição mudar.

Em 28/10/2026, foi preparada a [automação de qualidade e CI](qualidade-e-ci.md): lint TypeScript/templates, formatação de configurações, SDK/Node de referência, dependência de editor removida e jobs para frontend/apresentação e backend/SQL/E2E. As etapas passaram localmente; o usuário confirmou posteriormente o envio das alterações e o sucesso da primeira execução no GitHub, encerrando essa pendência. Artefatos Playwright foram isolados por suíte, e a validação do limite de valores fixos no editor de estratégias deixou de usar um literal Number sem precisão suficiente.

Refinamento adicional do Dashboard iniciado em 23/10/2026: [quatro etapas concluídas](refinamento-dashboard.md), incluindo distribuições compactas, evolução resumida, ranking visual, proventos com uma moeda/janela de períodos, movimentações com detalhes separados e grade com altura conforme o conteúdo. Rodada concluída em 25/10/2026 com build, 125 testes unitários e 56 cenários de apresentação validados. Rolagem horizontal de séries extensas e tabelas completas permanece disponível. Não há outra etapa definida nesta rodada; as conferências adicionais foram encerradas pela decisão de escopo acima.

A migração de interface registrada em [Entregas de layout](entregas-layout.md) foi concluída em 22/10/2026, incluindo Dashboard/gráficos, revisão global e temas claro/escuro/sistema. A rodada do [Plano de usabilidade e navegação](plano-usabilidade-e-navegacao.md) teve os marcos 1–7 concluídos em 23/10/2026: ações consistentes, proteção de formulários, contexto em memória, navegação interna, escopo de filtros e organização dos gráficos. A revisão integrada final aprovou build, 118 testes de interface e 44 cenários de apresentação em desktop/celular e claro/escuro, com evidências e limitações registradas. Não há novo marco de layout definido nesta rodada.

As conferências adicionais de toque, teclado virtual, zoom real de 200%, imagem nativa do arraste e auditoria formal de contraste/leitor de tela deixaram de ser pendências para o uso atual, por decisão do usuário. O [contexto analítico do Dashboard](contexto-analitico-dashboard.md) foi implementado em 27/10/2026: Evolução e Proventos recuperam filtros por carteira ao navegar, com contexto apenas em memória. A entrega também contém os gráficos extensos na largura da coluna móvel. A [otimização de carregamento de 26/10/2026](otimizacao-carregamento.md) reduziu o pacote inicial para 455,76 kB, abaixo do limite preservado de 500 kB, e o logo publicado para 20.451 bytes; o build de 27/10 registra 455,77 kB. Os testes E2E também foram atualizados para o layout atual, com 12 cenários aprovados em 26/10/2026. Novos problemas do uso real devem ser registrados com tela, passos e impacto; as demais prioridades funcionais abaixo mantêm seu escopo próprio.

| Ordem | Entrega | Escopo pendente |
| --- | --- | --- |
| 1 | Adequar o simulador à arquitetura | Concluído em 07/10/2026: cálculo em Application, resposta conjunta e Angular restrito à apresentação/marcações. Investigação adicional de concorrência/escala encerrada por decisão de escopo em 28/10/2026. |
| 2 | Cotações automáticas | Fase concluída em 28/10/2026: mapeamento explícito por mercado, cache persistente de ambos os provedores, pausas, proteção local brapi de 15.000 tentativas/31 dias e Alpha Vantage de 25/24h, informativo de consumo e diagnóstico repetível. Conferência real: 27/27 ativos disponíveis (12 brapi e 15 Alpha Vantage), sem alterações financeiras. Ver [resultados e limites](cotacoes-automaticas.md). Novos ativos/falhas futuras são manutenção, sem garantia permanente de cobertura. |
| 3 | Ampliar correções atrasadas | Ampliação implementada em 28/10/2026: inserir/corrigir desdobramento, grupamento e bonificação; reaplicar ajustes auditados de posição e cotações em lote, inclusive de várias posições. Proporções explícitas e intenção dos ajustes preservadas em correções posteriores. Ver [regras e limites](correcoes-atrasadas.md). Saldos iniciais, registros sem auditoria suficiente e tratamento de frações não são reconstruídos automaticamente. Edição direta retroativa de ajustes de posição não integra esta ampliação. |
| 4 | Refinar o planejamento | Exclusão de categorias, reservas, classificação estruturada de FII/REIT e bloqueios por combinação já disponíveis. Em 30/10/2026 foram adicionados [limites de concentração](limites-de-concentracao.md) por categoria/setor, com corte de sugestões e diagnóstico do valor não distribuído, sem vendas ou alterações financeiras. [Prioridades e sugestões de setor/moeda](prioridades-e-sugestoes-de-aporte.md) também estão disponíveis. [Planejado versus realizado](planejado-versus-realizado.md) foi implementado em 03/11/2026, com planos preservados e vínculos de compras auditáveis. [Objetivos com prazo e metas de patrimônio/renda](objetivos-de-planejamento.md) foram implementados em 03/11/2026, com cenários sem rentabilidade e renda histórica. [Faixas específicas por categoria/setor](faixas-por-grupo.md) também estão disponíveis, com diagnósticos preservados nos planos. [Benchmarks implementados em 05/11/2026](benchmarks.md), com comparação sob demanda de CDI, IPCA, Ibovespa e S&P 500 nos intervalos das fotografias, em BRL/USD e com limites explícitos de cobertura. |
| 5 | Base de demonstração | Dados fictícios coerentes, histórico, identificação visual e criação/restauração isolada da base oficial. |
| 6 | Distribuição e publicação | CI implementado, com primeira execução hospedada confirmada pelo usuário. Docker/Compose, Testcontainers, homologação, CD e infraestrutura de hospedagem continuam pendentes. Autenticação/autorização antes de exposição pública. |
| 7 | Observabilidade | Diagnóstico da API e conectividade do banco implementados em 28/10/2026, com liveness/readiness, timeout e respostas sem detalhes internos; ver [diagnóstico operacional](diagnostico-operacional.md). Logs centralizados, métricas, traces e monitoramento contínuo permanecem futuros. |
| 8 | Simplificar cadastros manuais | Revisar Patrimônio Externo, Ativos, Tipos de Ativo, Países, Moedas, Categorias e Setores; avaliar unificação na experiência/modelo e dados automáticos, sem presumir equivalência nem remover vínculos. Revisão inicial concluída: [proposta de fluxo, organização e compatibilidade](proposta-simplificacao-cadastros.md). Primeira entrega implementada: [Classificações/Referências e criação contextual no editor de Ativos](cadastros-e-imagens.md). [Cadastro assistido por ticker, imagens sugeridas, referências BR/US e revisão de Saldos e patrimônio externo implementados](cadastro-assistido-e-saldos.md). Cobertura automática permanece condicionada aos provedores; sem fusão de entidades ou alterações financeiras. Solicitação original em [revisão de produto](revisao-cadastros-e-gastos.md). |
| 9 | Análise de gastos pessoais | Planejamento consolidado em 06/11/2026: [Finanças pessoais](plano-financas-pessoais.md). Módulo com projetos próprios na mesma solution, uma API/Angular e coloração própria. Calendário, compromissos, entradas manuais, disponibilidade por conta e alertas de destinação; sem integração financeira inicial com investimentos. Ainda não implementado. |
| 10 | Imagens para identificar ativos | Primeira entrega implementada em 03/11/2026: [imagem opcional selecionada pelo usuário](cadastros-e-imagens.md), ao lado do nome no catálogo, nas posições e nos detalhes, com componente compartilhado, cache e alternativa local. [Busca assistida e imagens sugeridas implementadas](cadastro-assistido-e-saldos.md), com revisão antes de salvar e seleção manual quando a fonte não oferece imagem compatível. [Seletores visuais de ativos em compras/vendas e posições implementados em 05/11/2026](seletores-visuais-de-ativos.md). Seletores ampliados para recebimentos, eventos societários e correções atrasadas em 05/11/2026. Expansão da cobertura das fontes permanece como evolução futura. Ver [proposta](proposta-simplificacao-cadastros.md) e [escopo registrado](revisao-cadastros-e-gastos.md#imagens-para-identificar-ações-fiis-e-reits). |

Continuidade dos cadastros em 05/11/2026: [revisão e equivalências de setores](revisao-de-setores.md) implementadas no cadastro assistido, com confirmação explícita, diagnóstico de ambiguidades e escolha manual de setor existente. Não há reclassificação em lote. A próxima etapa funcional desta sequência é preparar a proposta do módulo independente de gastos; sua implementação continua condicionada à definição desse escopo.

## Melhorias técnicas transversais

- Encapsular alterações das entidades para reduzir setters públicos indiscriminados, conforme a especificação.
- Manter namespaces C# com chaves e a organização atual da solução do Visual Studio.
- Evoluir testes e documentação junto de cada entrega.
- As análises usam o mesmo dashboard no backend. Investigação adicional de concorrência/escala encerrada para o uso individual atual; reavaliar se esse cenário mudar.
- Registrar e priorizar problemas de usabilidade encontrados durante o uso. Melhorar nomes e localização dos botões para distinguir recarregar dados salvos de consultar novas cotações. O informativo opcional de fonte/data/horário já existe no painel de consulta/revisão, mas não nos cartões da carteira.
- TWR exato permanece dependente de avaliações em cada fluxo externo; Dietz estimado e XIRR já existem. Não é requisito do MVP original.

## Adiados ou condicionais

- Backup automático: adiado por decisão do usuário. Backup manual e validação de restauração já atendem ao uso atual.
- Cloud, Key Vault e autenticação multiusuário: condicionados à forma de distribuição/publicação.
- Importação de corretoras: citada como fora do MVP, sem escopo definido.
- Aplicativo nativo, microserviços e IA: possibilidades mencionadas na especificação, sem compromisso de implementação.

## O que já está disponível

Cadastros e carteiras, moeda-base e câmbio, patrimônio externo, metas, dashboard, simulador integrado, cotações manuais ou consultadas por brapi/Alpha Vantage em lote, informativo opcional de horários, indicador de variação sobre o preço médio, compras/vendas com custos, recebimentos de proventos, movimentações unificadas, estornos auditáveis, eventos societários suportados, fotografias versionadas, evolução mensal/anual, métricas de rentabilidade, correções atrasadas nos casos suportados, privacidade dos valores, gráficos, identidade visual e backup manual validado.

## Como classificar o estágio atual

O núcleo funcional do MVP original está coberto e é utilizado com dados reais. O projeto já avançou para uma versão funcional pós-MVP em evolução: várias funcionalidades que eram futuras na especificação foram entregues.

Isso não equivale a afirmar que o sistema esteja pronto para operação pública ou comercial. Preparação operacional, segurança de acesso e infraestrutura de publicação continuam com escopo próprio. As pendências futuras não devem ampliar retroativamente o conceito de mínimo necessário para validar o uso pessoal.

## Estratégias flexíveis — primeira entrega implementada em 15/10/2026

Manter metas como uma opção, permitindo futuramente combinar regras de valor fixo, percentual do aporte, distribuição por metas, divisão igual e distribuição manual. Configurações poderão definir ativos/grupos elegíveis, exclusões da base das metas, pausas, limites de concentração, prioridades e destino dos proventos. Percentual do dinheiro novo deve ser distinto de percentual desejado do patrimônio.

Exemplo motivador: reservar um valor fixo para FIIs/REITs, excluí-los da base das metas da etapa seguinte e distribuir o restante pelas metas dos demais investimentos. Mostrar reservas, justificativas e saldo não distribuído; conflitos e orçamento insuficiente devem ser explícitos.

Etapas propostas: (1) regras básicas e ajustes manuais; (2) prioridades, faixas, limites e planejado versus realizado; (3) objetivos, metas de renda e benchmarks. Permitir salvar configurações e escolhê-las na simulação, sem alterar o histórico. A primeira etapa de modelos e regras de aporte foi autorizada e implementada posteriormente: veja [estratégias de investimento](estrategias-de-investimento.md). Limites de concentração e [prioridades com sugestões de setor/moeda](prioridades-e-sugestoes-de-aporte.md) já estão disponíveis. [Planejado versus realizado](planejado-versus-realizado.md) foi implementado em 03/11/2026. [Objetivos de patrimônio e renda com prazo](objetivos-de-planejamento.md) também estão disponíveis. [Faixas por grupo](faixas-por-grupo.md) foram implementadas. [Benchmarks implementados](benchmarks.md): comparação de referência na Evolução, sem alteração financeira ou simulação dos mesmos fluxos.
