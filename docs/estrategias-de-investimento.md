# Estratégias de investimento e planejamento de aportes

Primeira entrega em 15/10/2026. Acesse **Metas → Escolher estratégia e simular aportes**, ou **Simular aporte → Estratégia de investimento**.

## Modelos disponíveis

| Modelo | Comportamento inicial |
| --- | --- |
| Alocação por metas | Distribui o restante pelas diferenças positivas das metas de categoria e setor. |
| Aportes regulares | Divide o aporte informado igualmente entre combinações de categoria/setor elegíveis. Não agenda operações. |
| Buy and hold | Orientação de acumulação; novos aportes pelas metas, sem sugestão automática de vendas. Não impede o registro de uma venda decidida pelo usuário. |
| Foco em renda | Reservas nas categorias escolhidas pelo usuário; restante sem distribuição por padrão. Não seleciona ativos por dividend yield nem estima proventos futuros. |
| Núcleo e complementos | Percentuais do aporte configurados pelo usuário para categorias do núcleo/complementos. Exige pelo menos uma regra percentual. |
| Rebalanceamento por faixas | Como metas, mas diferenças dentro da tolerância em pontos percentuais recebem peso zero. Atua somente por novos aportes. |
| Imobiliário fixo + metas | Reservas fixas explícitas para categorias escolhidas, excluídas da base do restante. Exige pelo menos uma reserva fixa com exclusão. |
| Personalizada / híbrida | Combinação livre das regras suportadas. |

Modelos são pontos de partida editáveis, não recomendações universais ou algoritmos de seleção de empresas. Não existe identificação automática das categorias imobiliárias neste modelo: selecione FIIs/REITs explicitamente.

## Regras e cálculo

1. O aporte e os valores fixos usam a moeda-base da carteira.
2. Cada categoria pode ter uma reserva fixa ou um percentual do **aporte total**, com duas casas decimais. Percentual de aporte não é percentual de patrimônio.
3. Reservas são calculadas primeiro. Se ultrapassarem o aporte, a simulação é recusada; não há redução silenciosa.
4. Cada reserva é dividida igualmente entre combinações elegíveis de categoria/setor existentes, preservando centavos. Empates são resolvidos pelos IDs. Não é uma escolha de ativo dentro do grupo.
5. Categorias excluídas deixam de compor a base da distribuição do restante, mas continuam na carteira e podem receber a reserva explícita.
6. Para o restante, escolha metas, divisão igual entre combinações ou manter sem distribuição. A categoria reservada também participa do restante se não for excluída.
7. No modo metas, percentuais desejados dos grupos remanescentes são normalizados proporcionalmente para 100%. A base atual inclui somente posições de quantidade positiva não excluídas. São necessárias metas válidas de categoria e setor entre os elegíveis.
8. No modelo por faixas, tolerância de 5 significa cinco pontos percentuais. Diferença de até cinco pontos recebe peso zero; fora da faixa, usa-se a diferença integral, não apenas o excesso. Sem vendas, pode ser impossível chegar à faixa apenas com o aporte.
9. Sem combinações válidas para uma reserva/divisão, o valor fica não distribuído. No plano por metas, conflitos entre setor e categoria também podem gerar saldo não distribuído.
10. As alternativas em diferentes moedas são equivalentes do mesmo valor; não devem ser somadas. Mantêm as regras existentes de câmbio e compatibilidade de FII/REIT com setor imobiliário.

O plano final soma reservas, distribuição do restante e valor não distribuído, conservando o aporte. As análises de categoria/setor descrevem somente o restante. Os gráficos continuam representando a carteira completa; a base elegível é informada separadamente.

## Seu modelo imobiliário

Escolha **Imobiliário fixo + metas**, configure o valor fixo de FIIs e/ou REITs e marque **Excluir da base do restante** nas mesmas categorias. Não é necessário zerar as metas armazenadas dessas categorias: a simulação retira-as da base e normaliza as outras metas. Dividir a reserva entre FII e REIT é uma escolha explícita, usando uma regra para cada categoria.

Nenhum valor real foi pré-configurado pelo desenvolvimento. Se o aporte não cobrir as reservas, ajuste o orçamento ou as regras.

## Salvamento e efeitos

- Nomeie a configuração e use **Salvar como estratégia padrão**.
- A última configuração salva da carteira será carregada na próxima abertura. As demais continuam disponíveis no seletor.
- Alterar o modelo inicia uma nova configuração; editar uma configuração salva e salvar cria a próxima revisão.
- Revisões anteriores ficam preservadas na tabela PortfolioStrategyRevision. A tela mostra a revisão atual; navegação/restauração de revisões antigas ainda não está implementada.
- A revisão esperada impede sobrescrever silenciosamente uma edição concorrente. Se houver conflito ou resposta incerta, recarregue as configurações antes de tentar novamente.
- Simular usa os campos atuais, mesmo ainda não salvos, e não grava operações. Alterações invalidam a simulação anterior e as marcações visuais.
- Desmarcar **Usar estratégia nesta simulação** mantém o fluxo original por metas nessa sessão, sem apagar a configuração salva.
- Nenhum modelo altera preços, quantidades, saldos, proventos, fotografias ou metas cadastradas. Nenhum executa vendas ou compras.

## Arquitetura e atualização

Domain contém a entidade de revisão; Application valida configurações e calcula as sugestões; Infrastructure persiste revisões; API expõe GET/POST /api/portfolios/{id}/strategies. O endpoint contribution-plan aceita uma configuração opcional, mantendo compatibilidade com clientes que enviam apenas o aporte.

A migration **PortfolioStrategies** cria somente a tabela de revisões e suas restrições. Não contém dados pessoais nem muda metas existentes. Antes de usar, pare a API, faça o backup manual e aplique:

~~~powershell
dotnet ef database update --project InvestmentTracker.Infrastructure --startup-project InvestmentTracker.Api
~~~

A migration foi validada em banco temporário; não foi aplicada ao banco pessoal nesta entrega.

## Ainda fora desta etapa

Metas de patrimônio, renda desejada e objetivos com prazo; acompanhamento de aportes planejados versus realizados; agendamento; bloqueios por concentração; destinação automática de proventos; seleção de empresas por valor/crescimento/qualidade; benchmarks; previsão de composição após compras com quantidades e custos; editor/restauração de revisões anteriores. As estratégias atuais planejam o dinheiro novo por categoria/setor, sem prometer essas funções.
