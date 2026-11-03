# Faixas de rebalanceamento por categoria e setor

Entrega de 03/11/2026. Em **Simular aporte → Configurar estratégia**, escolha **Rebalanceamento por faixas** e mantenha **Distribuir o restante → Metas de categoria e setor**.

## Configuração

A tolerância geral continua sendo o padrão. Abra **Faixas por categoria e setor** para definir exceções por grupo, com busca e paginação. Exemplo: Ações BR com 3 pontos percentuais, Ações EUA com 5 e FIIs com 2.

- Campo vazio: usa a tolerância geral.
- Zero: tolerância específica de 0 p.p.; não volta ao padrão.
- Valores permitidos: de 0 a 100 p.p., com duas casas decimais. São aceitas até 200 configurações distintas, identificadas pela dimensão e pelo ID do grupo. Categoria e setor podem ter o mesmo ID com tolerâncias diferentes.
- A configuração fica nas revisões da estratégia. Perfis antigos sem exceções continuam usando a tolerância geral.
- As faixas só são aplicadas ao restante por metas no modelo de faixas. Se o restante for igualitário ou reservado, a simulação informa que elas não foram aplicadas. Outros modelos mantêm seu cálculo próprio.

## Cálculo

As faixas usam a **base elegível atual do restante**, antes de adicionar reservas ou aportes simulados. Categorias excluídas, combinações bloqueadas e instrumentos inelegíveis continuam fora dessa base; metas remanescentes são normalizadas para 100% em cada dimensão.

Uma meta normalizada de 40% com tolerância de 3 p.p. produz faixa de 37% a 43%. Os extremos são limitados a 0% e 100%. As bordas pertencem à faixa.

Dentro da faixa, o grupo recebe peso zero no restante por metas. Abaixo dela, o peso é o desvio integral até a meta, mantendo o algoritmo anterior: não se usa apenas o excesso além da tolerância. Acima da faixa, não há peso positivo nem sugestão de venda. Os cálculos usam decimais sem arredondar o desvio para decidir a faixa; percentuais de diagnóstico são exibidos com até quatro casas decimais.

Categoria e setor continuam precisando encontrar combinações de posições existentes. Um grupo abaixo da faixa pode não receber nada se a outra dimensão não tiver peso, se não houver combinação compatível, se o orçamento restante for zero ou se os centavos não produzirem alocação. O saldo continua explicitamente não distribuído.

As faixas são critérios de ativação dos pesos, **não tetos sobre a composição após o aporte**. Um aporte grande pode ultrapassar uma faixa. Para impor tetos, use os [limites de concentração](limites-de-concentracao.md). Não há promessa de atingir simultaneamente todas as metas apenas com dinheiro novo.

Reservas explícitas têm regras próprias e podem receber valores dentro ou acima da faixa. Elegibilidade e limites continuam obrigatórios. Prioridades mantêm a ordem e os desempates já definidos; elas não substituem os pesos das metas. As alternativas em BRL/USD permanecem equivalentes do mesmo aporte.

## Diagnóstico e planos salvos

O simulador mostra a tolerância efetivamente usada (geral/específica), percentual atual, meta normalizada, faixa e estado: abaixo, dentro, acima, excluído do restante, sem posições elegíveis ou sem meta válida.

Cada linha também informa o valor do restante no plano conjunto, o valor após limites e as reservas do plano final, com a explicação correspondente. As análises de categorias e setores representam o mesmo dinheiro e não devem ser somadas. O relatório possui busca, filtro de dimensão, paginação e privacidade de valores.

Ao salvar o plano de aporte, os diagnósticos são preservados junto da configuração. Eles podem ser consultados em **Planejado × realizado → Regras preservadas no plano**. Edições posteriores da estratégia não reescrevem essa cópia. A conferência da simulação no salvamento inclui os diagnósticos, evitando salvar uma explicação alterada com uma impressão antiga, mesmo quando o valor final permaneça igual.

## Arquitetura e atualização

`StrategyConfig.bands` é uma coleção opcional de `{ dimension, groupId, tolerance }`. A tolerância no contrato é uma fração decimal serializada como string: 3 p.p. corresponde a `"0.03"`. A interface faz a conversão de apresentação, enquanto Application valida e decide os pesos, faixas e diagnósticos. Infrastructure valida referências e persiste o JSON das revisões existentes.

Não há migration adicional nesta entrega. Os diagnósticos novos são opcionais na cópia JSON dos planos; registros antigos continuam legíveis. Migrations de entregas anteriores, como a dos objetivos de planejamento, continuam necessárias caso ainda estejam pendentes.

Simular, salvar estratégias e preservar diagnósticos não alteram posições, saldos, movimentações, fluxos, proventos, fotografias ou metas cadastradas. Não foram introduzidas consultas adicionais a provedores de preços ou câmbio para calcular as faixas.

## Validação

Os testes cobrem tolerância específica/geral, zero explícito, bordas inclusivas, precisão, pesos integrais, exclusões, bloqueios, metas ausentes, orçamento esgotado, interação com reservas/prioridades/limites, referências inválidas, configurações antigas e conservação do aporte. Integração verifica revisão e restauração das estratégias, cópia dos diagnósticos e proteção da impressão no salvamento. Interface e navegador exercitam filtros, persistência, recarga, conflitos entre dimensões e privacidade em desktop/celular e temas claro/escuro.

Resultado local: **738 testes aprovados** — 297 unitários .NET, 181 de integração, 162 Angular, 76 de apresentação e 22 E2E. Lint, formatação e build aprovados; pacote inicial mantido em 455,84 kB. A conferência visual também identificou e corrigiu o seletor de modelo ao reabrir estratégias salvas; os 162 testes Angular, os quatro cenários visuais de faixas e os 22 E2E foram repetidos após essa correção. Imagens do editor escuro e do diagnóstico no celular foram conferidas. Integração e E2E usaram bancos temporários; as bases E2E foram removidas ao concluir. Nenhuma migration foi aplicada ao banco pessoal.
