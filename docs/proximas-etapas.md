# Próximas etapas

Lista atualizada em 15/10/2026. As prioridades abaixo orientam as próximas entregas; não representam autorização para implementar todos os itens de uma vez.

## Prioridades

A revisão de interface tem um plano próprio em [Entregas de layout](entregas-layout.md), registrado em 20/10/2026, com ordem, dificuldade, padrões de componentes, paginação e modo escuro. A paleta nova ainda está em discussão; o documento não indica implementação concluída.

| Ordem | Entrega | Escopo pendente |
| --- | --- | --- |
| 1 | Adequar o simulador à arquitetura | Concluído em 07/10/2026: cálculo em Application, resposta conjunta e Angular restrito à apresentação/marcações. Isolamento transacional das leituras pode ser aprofundado posteriormente. |
| 2 | Cotações automáticas | Primeira integração implementada: brapi e Alpha Vantage, consulta sob demanda, revisão e aplicação auditável. Chaves configuradas no ambiente local e fluxo validado pelo usuário; ampliar a conferência de cobertura dos ativos e de inconsistências do provedor; cache/controle de consumo persistentes e mapeamento explícito de símbolos são evoluções futuras. |
| 3 | Ampliar correções atrasadas | Revisar os casos bloqueados por eventos societários, ajustes de posição e histórico/auditoria insuficientes. Não reconstruir dados ausentes sem informação confiável. |
| 4 | Refinar o planejamento | Permitir excluir grupos (como imobiliário) da base das metas, mantendo-os no patrimônio e reservando seu aporte fixo separadamente; diferenciar exclusão de meta 0%. Disponível no simulador de estratégias para exclusão por categoria; ver documentação de estratégias. Classificação estruturada de FII/REIT, substituindo identificação por nomes; avaliar preferências para combinações setor/categoria. |
| 5 | Ideias de investimento | Cadastro e acompanhamento de estados: Ideia, Em análise, Aguardando, Descartada, Aprovada e Executada. |
| 6 | Reflexões do investidor | Diário de decisões com possibilidade de vínculo às fotografias da carteira. |
| 7 | Base de demonstração | Dados fictícios coerentes, histórico, identificação visual e criação/restauração isolada da base oficial. |
| 8 | Distribuição e publicação | Docker/Compose, CI, Testcontainers, homologação, CD e infraestrutura de hospedagem. Autenticação/autorização antes de exposição pública. |
| 9 | Observabilidade | Evoluir o endpoint simples de saúde para verificações de dependências; logs centralizados, métricas, traces e monitoramento da API/banco. |

## Melhorias técnicas transversais

- Encapsular alterações das entidades para reduzir setters públicos indiscriminados, conforme a especificação.
- Manter namespaces C# com chaves e a organização atual da solução do Visual Studio.
- Evoluir testes e documentação junto de cada entrega.
- As análises agora usam o mesmo dashboard no backend; avaliar isolamento de leitura caso passe a haver uso concorrente.
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

Etapas propostas: (1) regras básicas e ajustes manuais; (2) prioridades, faixas, limites e planejado versus realizado; (3) objetivos, metas de renda, teses e benchmarks. Permitir salvar configurações e escolhê-las na simulação, sem alterar o histórico. A primeira etapa de modelos e regras de aporte foi autorizada e implementada posteriormente: veja [estratégias de investimento](estrategias-de-investimento.md). Metas patrimoniais, renda desejada, objetivos, limites de concentração e acompanhamento planejado versus realizado continuam pendentes.
