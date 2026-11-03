# Limites de concentração nas estratégias

Implementados em 30/10/2026. São tetos para sugestões por categoria e/ou setor; não executam ordens, mudam metas ou corrigem posições existentes.

## Configurar e conferir

Em **Simular aporte → Configurar estratégia**, habilite a estratégia e abra **Limites de concentração**. Busque a categoria ou setor e informe o percentual máximo, com até duas casas decimais. Vazio significa **sem limite**; `0` impede novos aportes no grupo e `100` não restringe sua concentração. O teto não retira posições da base.

Use **Salvar como estratégia padrão** para criar uma revisão, ou conclua e simule sem salvar. Confira o plano final, o valor não distribuído e o quadro de concentração: percentual atual/projetado e sugestão original/após limites. Alterações invalidam a simulação e suas marcações temporárias.

Tetos de categoria e setor valem simultaneamente. Não precisam somar 100% nem são normalizados como metas. Até 200 grupos distintos podem receber limite. Referências inexistentes, duplicadas ou percentuais fora de 0%–100% são recusados. Busca e paginação não removem configurações; grupos indisponíveis continuam identificados para permitir remover o limite.

## Base do percentual

A base considera **todas as posições da carteira na moeda-base**, inclusive grupos excluídos/bloqueados e posições inelegíveis, mais **somente o aporte efetivamente distribuído**. Patrimônio Externo e dinheiro não distribuído não entram nessa base. Câmbio indisponível continua impedindo simulações parciais.

Para cada grupo que receber dinheiro:

```text
(valor atual do grupo + aporte sugerido ao grupo)
------------------------------------------------ <= teto informado / 100
(valor atual de todas as posições + aporte distribuído)
```

Exemplo de conferência: posições somam 100, o grupo vale 50 e o teto é 60%. Se toda a sugestão de 100 for destinada a esse grupo, o máximo seguro é 25: `(50 + 25) / (100 + 25) = 60%`. Os outros 75 ficam sem distribuição. Contar os 100 completos na base permitiria sugerir mais que o teto real.

Um grupo inicialmente acima do teto pode continuar acima se não houver aporte suficiente para diluí-lo. Nesse caso não recebe dinheiro adicional e aparece um aviso, sem venda sugerida. Pode receber dinheiro se a distribuição completa o diluir o suficiente para respeitar o teto projetado. Sem valor atual/distribuído para formar uma base, o percentual aparece como **sem base**.

## Reservas, exclusões e valor não distribuído

- Classificação, bloqueios por combinação e exclusões continuam sendo aplicados antes dos tetos.
- Limites prevalecem sobre reservas e metas e podem reduzir as sugestões. O aporte original ainda precisa cobrir as reservas declaradas antes dessa etapa, como já exigido pela estratégia.
- O cálculo só reduz sugestões originalmente disponíveis. Não aumenta outra combinação além da sugestão anterior para aproveitar cortes. Dentro da mesma combinação, linhas de reserva existentes precedem as do restante.
- Não há promessa de uma distribuição única nem de maximizar o dinheiro investido. Se for usada uma solução conservadora para evitar convergência longa, o resultado avisa essa condição.
- Total distribuído + valor não distribuído continua igual ao aporte informado até o centavo.

Com tetos ativos, o **plano final** e o quadro de concentração substituem as análises separadas do restante, evitando apresentar valores sem corte como sugestões executáveis. Gráficos/comparações continuam mostrando a carteira inteira. Equivalentes em outras moedas acompanham o valor final reduzido e são alternativas do mesmo aporte, sem somar valores adicionais.

## Arquitetura e precisão

`StrategyConfig.concentrationLimits` guarda `{ dimension: "category" | "sector", groupId, maximumPercentage }`. Percentuais são decimais transmitidos como strings, por exemplo `"33.33"`. Configurações antigas sem a coleção continuam sem limites. Os dados ficam no JSON das revisões existentes: **não há nova migration nesta entrega**. A migration de classificação da etapa anterior ainda é necessária para instalações que não a aplicaram.

`ConcentrationCalculator`, em Application, usa fluxo em rede com capacidades inteiras em centavos para respeitar categoria, setor e sugestões originais por combinação. Recalcula a base quando há cortes. Um limite de iterações evita convergência longa perto de 100%; o caminho conservador preserva sugestões irrestritas e só admite margem comprovada para grupos limitados. A verificação final impede retornar aportes que violem tetos.

Valores convertidos não são arredondados previamente para criar margem: decimais são convertidos em inteiros de precisão arbitrária para calcular capacidades em centavos. Percentuais exibidos têm até quatro decimais; essa formatação não decide a validade. Infrastructure valida referências/persiste revisões e Angular apresenta os resultados calculados no backend.

## Revisões de produto relacionadas

Com a opção de prioridades ativa, reservas e grupos usam a ordem configurada nos desempates entre distribuições de mesmo volume viável. Os tetos continuam obrigatórios. Veja [prioridades e sugestões de aporte](prioridades-e-sugestoes-de-aporte.md).

Simplificação de cadastros manuais e análise independente de gastos ficaram registradas em [Revisão de cadastros e gastos](revisao-cadastros-e-gastos.md). São estudos futuros; não foram implementados nem misturados ao planejamento de investimentos nesta entrega.

## Validação local

**649 testes aprovados:** 259 unitários .NET, 172 de integração, 140 Angular, 64 de apresentação e 14 E2E. Lint, formatação de configurações e build aprovados; pacote inicial mantido em 455,77 kB, abaixo do limite de 500 kB.

Os novos testes cobrem tetos simultâneos, corte de reservas, base completa versus base elegível/Patrimônio Externo, saldo não distribuído, grupos já acima do teto, base zero, valores máximos, decimais convertidos abaixo de um centavo, caminho conservador e 120 combinações determinísticas de restrições. Integração verifica revisão/restauração dos limites e rejeição de referências inválidas sem alterar dados financeiros.

Angular e E2E conferem definir, salvar, recarregar, remover e invalidar a simulação ao mudar um teto. A apresentação foi exercitada em desktop/celular emulado e claro/escuro; imagens do resultado no celular e do editor escuro foram conferidas. Integração e E2E usaram bancos temporários, com remoção da base E2E ao concluir. Nenhuma estratégia foi aplicada nem posição alterada na carteira pessoal.
