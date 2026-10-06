# Como funciona o simulador de aportes

O simulador transforma as metas cadastradas em uma sugestão de distribuição do próximo aporte. A escolha de setor e categoria depende das diferenças para as metas e das combinações existentes na carteira. Ele não avalia empresas, preços atrativos, perspectivas de mercado ou rentabilidade esperada.

## 1. O que aparece na tela

- **Categorias e setores:** duas análises do mesmo aporte, lado a lado no desktop e empilhadas no celular.
- **Gráficos:** participação atual e meta de cada grupo, usando os dados carregados da carteira. Eles não representam a distribuição projetada depois do aporte.
- **Distribuição sugerida:** orçamento calculado para cada categoria e para cada setor.
- **Plano conjunto:** cruzamento dos dois orçamentos, indicando combinações como “Financeiro → Ações BR”.
- **Moeda original:** equivalente estimado do orçamento na moeda dos ativos elegíveis.
- **Investido:** marcação temporária para acompanhar a execução.

As duas análises não são aportes diferentes. Se você informar R$ 1.000,00, terá uma visão por categoria e outra por setor sobre esses mesmos R$ 1.000,00. Não some os dois resultados.

## 2. Como são calculados os orçamentos

O cálculo é feito separadamente para categorias e setores.

A participação atual é o valor atual do grupo, convertido para a moeda-base, dividido pelo valor atual total das posições da carteira. Patrimônio externo não entra nessa distribuição. Quando o total é zero e a conversão está disponível, a participação atual é zero.

Para cada grupo:

~~~text
Diferença = Meta − Participação atual
Peso ajustado = máximo(Diferença, 0)
Aporte sugerido = Peso ajustado ÷ Soma dos pesos × Aporte informado
~~~

Um grupo na meta ou acima dela recebe peso zero. Quanto maior a diferença positiva, maior seu orçamento proporcional.

Exemplo fictício para um aporte de R$ 1.000,00:

| Categoria | Atual | Meta | Diferença | Peso | Sugestão |
| --- | ---: | ---: | ---: | ---: | ---: |
| Ações BR | 20% | 30% | +10 p.p. | 10 | R$ 400,00 |
| Ações EUA | 20% | 30% | +10 p.p. | 10 | R$ 400,00 |
| REITs | 15% | 20% | +5 p.p. | 5 | R$ 200,00 |
| FIIs | 45% | 20% | −25 p.p. | 0 | R$ 0,00 |

A soma dos pesos positivos é 25. Ações BR recebem 10 ÷ 25 × 1.000 = R$ 400,00. O mesmo procedimento produz os orçamentos dos setores.

### Arredondamento

Os valores são inicialmente arredondados para baixo a centavos. Os centavos restantes vão para os grupos com maiores frações descartadas. Se as frações empatarem, vence o menor ID do grupo.

Assim, havendo pesos positivos, a soma da análise fecha exatamente com o aporte. Sem pesos positivos, todo o valor fica sem distribuição.

## 3. Como o plano escolhe “em qual categoria investir naquele setor”

O plano usa os dois resultados anteriores como limites:

1. Cada categoria pode receber, no máximo, seu aporte sugerido.
2. Cada setor pode receber, no máximo, seu aporte sugerido.
3. Uma combinação só é permitida se houver uma posição com **quantidade positiva** naquela categoria e naquele setor.
4. O objetivo é distribuir o maior valor possível respeitando esses limites.

Por exemplo, suponha que os orçamentos dos setores sejam Financeiro R$ 400,00, Tecnologia R$ 400,00 e Imobiliário R$ 200,00. Se as posições elegíveis oferecerem as combinações abaixo, o plano poderá ser:

| Setor | Categoria | Valor na moeda-base |
| --- | --- | ---: |
| Financeiro | Ações BR | R$ 400,00 |
| Tecnologia | Ações EUA | R$ 400,00 |
| Imobiliário | REITs | R$ 200,00 |

REITs receberam o orçamento imobiliário porque essa combinação existe e tem orçamento disponível nas duas dimensões. Isso não significa que o simulador tenha concluído que REITs são melhores que FIIs. No exemplo, FIIs já estavam acima da meta e receberam orçamento zero.

O nome “Ações BR” ou “Ações EUA” vem do cadastro de categorias. O plano não cria essas classificações a partir do ticker, do país ou da moeda. Cadastros corretos são necessários para que a indicação tenha o significado esperado.

### Regra de FII e REIT

Na implementação atual, nomes de categoria ou tipo contendo as palavras FII, FIIs, REIT ou REITs são reconhecidos como restritos ao setor imobiliário. Essas posições só participam se o nome do setor contiver “imobili” ou “real estate”, sem distinção entre maiúsculas e minúsculas.

Essa validação usa nomes, não um atributo estruturado de classificação. Uma nomenclatura diferente pode não ser reconhecida. Por isso, mantenha categorias, tipos e setores coerentes; o simulador não corrige cadastros automaticamente.

### Quando existem várias escolhas possíveis

O algoritmo maximiza o valor distribuído. Entre soluções com o mesmo total, não existe preferência econômica por Brasil, EUA, FII ou REIT.

Categorias e setores são percorridos em ordem crescente de seus IDs. Essa ordem torna o resultado reproduzível com as mesmas entradas, mas não significa prioridade financeira. Renomear um grupo não muda seu ID; excluir e recriar cadastros pode mudar o desempate.

Não há tentativa de espalhar o orçamento igualmente por todas as combinações. Uma solução pode concentrar um setor em uma categoria mesmo que outra distribuição também seja válida.

### Por que ele pode mudar uma escolha feita durante o cálculo

O algoritmo utilizado é de **fluxo máximo**, com caminhos residuais. Isso permite desfazer uma atribuição intermediária para acomodar outra combinação e distribuir mais do aporte.

Exemplo com R$ 100,00:

- Categorias A e B têm R$ 50,00 cada.
- Setores X e Y têm R$ 50,00 cada.
- A pode investir em X ou Y; B só pode investir em X.

Se A recebesse X primeiro e essa escolha fosse definitiva, B ficaria sem destino. O algoritmo consegue reorganizar para A → Y e B → X, distribuindo os R$ 100,00.

O cálculo conjunto usa centavos inteiros com BigInt, evitando perda de centavos por aproximação de números decimais.

## 4. Por que pode sobrar dinheiro

O plano não excede os orçamentos para forçar o uso de todo o aporte.

Exemplo: Ações BR têm orçamento de R$ 600,00 e Ações EUA de R$ 400,00. Financeiro tem R$ 300,00 e Tecnologia R$ 700,00. Se só existirem BR → Financeiro e EUA → Tecnologia, o máximo compatível será R$ 700,00:

- R$ 300,00 em BR → Financeiro;
- R$ 400,00 em EUA → Tecnologia;
- R$ 300,00 sem distribuição compatível.

Também pode sobrar valor quando não existem pesos positivos, quando faltam posições elegíveis ou quando uma combinação é excluída pela regra imobiliária.

Uma categoria com meta, mas sem posição positiva, pode receber orçamento na análise individual e continuar sem destino no plano conjunto. O simulador não inventa um ativo para preencher essa lacuna.

## 5. Como é mostrado o valor na moeda correta

O orçamento é calculado na moeda-base da carteira. Para mostrar o equivalente na moeda original, usa-se a taxa disponível nas posições:

~~~text
Valor na moeda original = Valor na moeda-base ÷ Taxa original→base
~~~

Exemplo: orçamento de R$ 400,00 e taxa de 5,00 BRL por USD resultam em USD 80,00.

A conversão:

- usa a referência de câmbio carregada na carteira, não uma taxa escolhida pelo plano;
- informa a data e sinaliza cotação anterior ou de fallback;
- arredonda para duas casas decimais; em meio centavo, arredonda para cima;
- não inclui corretagem, impostos, spread ou restrições de lotes;
- fica indisponível se a taxa for ausente, inválida, zero ou conflitante entre as posições usadas.

Na moeda-base, a taxa é 1. Se a combinação oferecer várias moedas, cada valor mostrado é uma alternativa equivalente para o **mesmo orçamento**. Não são parcelas para somar. A escolha da moeda depende do ativo que você decidir comprar.

## 6. Como usar durante os investimentos

1. Atualize os valores das posições e confira as referências de câmbio.
2. Abra o simulador e informe o aporte na moeda-base.
3. Confira categorias, setores e o plano conjunto.
4. Use as marcações “investido” para acompanhar o que já executou.
5. Registre as compras reais em **Compras e vendas**.
6. Quando quiser recalcular com os novos saldos, use **Atualizar carteira e limpar simulação**.

As marcações são independentes: marcar uma linha do plano conjunto não marca automaticamente sua categoria ou setor. Elas não reduzem o aporte restante, não recalculam sugestões e não geram movimentações.

Nada dessas marcações é salvo. São apagadas ao alterar o aporte, simular novamente, trocar ou atualizar a carteira, recarregar ou sair da página.

## 7. Limites importantes para interpretar o resultado

- As metas de cada dimensão precisam somar 100%. Uma dimensão disponível pode ser exibida mesmo se a outra estiver sem configuração ou falhar; o plano conjunto depende das duas.
- O cálculo usa a diferença percentual atual. Não resolve a composição final incluindo o aporte no denominador, portanto não garante atingir exatamente as metas depois das compras.
- O cruzamento não utiliza metas específicas para cada par setor/categoria, pois essas metas conjuntas não são cadastradas.
- O plano não escolhe ticker, quantidade ou preço de execução.
- Os gráficos e as taxas refletem os dados carregados na página. Alterações feitas em outra tela exigem atualização.
- As duas análises são consultadas separadamente. Não há uma transação única abrangendo o carregamento da carteira e ambas as simulações; evite alterar posições durante o cálculo e atualize os dados em caso de dúvida.

## Referências de implementação

- [Fórmula individual e distribuição de centavos](../InvestmentTracker.Application/Allocation/Services/ContributionCalculator.cs).
- [Participações atuais e metas](../InvestmentTracker.Application/Allocation/Services/AllocationService.cs).
- [Plano conjunto e conversão de moedas](../frontend/InvestmentTracker.Web/src/app/features/allocation/contribution-plan.ts).
- [Estado da tela e marcações temporárias](../frontend/InvestmentTracker.Web/src/app/features/allocation/contributions-page.ts).
- [Testes do algoritmo conjunto](../frontend/InvestmentTracker.Web/src/app/features/allocation/contribution-plan.spec.ts).
- [Documentação de metas, dashboard e API](metas-dashboard-aportes.md).
