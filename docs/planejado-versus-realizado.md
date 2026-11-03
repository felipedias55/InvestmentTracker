# Aportes: planejado versus realizado

Implementado em 03/11/2026. Acesse **Simular aporte → Planejado × realizado**.

## Como usar

1. Simule um aporte com as metas atuais ou com uma estratégia. Clique em **Salvar plano de aporte**, informe nome e período de execução e confirme em **Salvar e acompanhar**. O período começa e termina no dia atual por padrão; ajuste-o para acompanhar um mês ou outro intervalo.
2. Registre suas compras normalmente em **Compras e vendas**. No acompanhamento, clique em **Atualizar execução** e vincule cada compra à linha correspondente do plano.
3. Consulte planejado, realizado e diferença por linha, além do total. Para voltar depois, escolha a carteira, use **Abrir acompanhamento** e selecione um plano salvo.

Salvar preserva as sugestões, os valores na moeda-base, as alternativas de moeda/câmbio, nomes/classificações dos grupos, prioridades e a configuração da estratégia usada. Metas atuais podem mudar sem reescrever os valores planejados. O servidor recalcula e confere uma impressão da simulação antes de salvar: se a distribuição ou suas informações mudaram, uma nova simulação é exigida. Salvar novamente a mesma solicitação após falha de comunicação retorna o plano já criado; reutilizar o identificador com outros dados é recusado.

O plano é uma cópia imutável da simulação, sem executar compras. As marcações temporárias **investido** do simulador não contam como realizado. As listas de planos e compras têm paginação; linhas e histórico de vínculos são apresentados em páginas de dez itens.

## O que conta como realizado

- Somente compras registradas, ativas e explicitamente vinculadas. Vendas, proventos, depósitos, ajustes de posição e cotações não contam como execução de um aporte.
- Uma compra inteira pertence a uma única linha de um único plano. Não há divisão automática entre reservas, restante ou planos. É possível ultrapassar o valor planejado; o excesso fica explícito.
- A data da compra deve estar dentro do período inclusivo. Categoria e setor atuais do ativo no momento do vínculo e a moeda registrada da compra precisam corresponder à linha preservada. Compras incompatíveis do período aparecem sem ação de vínculo. A preferência de moeda da estratégia é uma indicação; alternativas elegíveis preservadas também podem ser executadas.
- O realizado usa o custo da compra, incluindo taxas, e não seu valor de mercado atual. Na mesma moeda usa o total registrado. Em outra moeda usa o equivalente histórico do fluxo associado, quando registrado na moeda do plano. Compras com saldo externo podem não ter esse fluxo: nesse caso é obrigatório informar o equivalente histórico total, com até quatro casas decimais. A origem da conversão fica identificada. Não se consulta câmbio atual para completar o acompanhamento.
- A classificação não é reavaliada para reescrever vínculos já feitos. Renomear ou reclassificar um ativo não muda retroativamente a linha escolhida. Correções financeiras geram substitutos: o vínculo anterior permanece visível, deixa de contar quando estornado e o substituto deve ser vinculado explicitamente.
- **Remover vínculo** preserva a compra e registra a data da remoção no histórico. A mesma compra ativa pode então ser vinculada novamente. Estornar uma compra também a retira automaticamente do realizado na próxima consulta/atualização, preservando o vínculo para conferência.

Diferença = planejado − realizado, com precisão decimal. Negativo significa excesso. O orçamento não distribuído permanece identificado, mas não é uma linha de compra. O plano fica pendente, parcial, concluído ou acima do planejado; só conclui quando todas as linhas estão atendidas. Excesso em uma linha não compensa automaticamente a falta em outra. Planos totalmente não distribuídos são identificados como **Sem distribuição**.

## Arquitetura e isolamento

Domain contém `SavedContributionPlan` e `ContributionPurchaseLink`. Application valida requisições, calcula progresso e define a escolha do equivalente. Infrastructure persiste a cópia JSON e vínculos auditáveis, consulta compras e estornos e serializa mutações por carteira. A restrição única filtrada de compra impede dupla contagem. API expõe `/api/portfolios/{portfolioId}/contribution-plans`, detalhes, candidatos e inclusão/remoção de vínculos. Angular apresenta valores calculados no servidor, com Material, painel de edição, proteção de formulários e privacidade dos valores.

Salvar planos e gerenciar vínculos não alteram posições, preços, saldos, Patrimônio Externo, movimentações, fluxos, proventos, rentabilidade, metas ou fotografias. As operações financeiras continuam ocorrendo exclusivamente nos fluxos existentes. Não há alocação automática de compras por período ou importação de corretoras nesta entrega.

## Atualização do banco

A migration **20261103130333_ContributionPlanTracking** adiciona somente as duas tabelas de acompanhamento, índices e restrições. Não contém preenchimento de dados pessoais. Foi preparada para aplicação pelo usuário; os testes usam bases temporárias.

Com a API parada e o backup manual feito, execute na raiz do projeto:

```powershell
dotnet ef database update --project InvestmentTracker.Infrastructure --startup-project InvestmentTracker.Api
```

O comando também aplica migrations anteriores ainda pendentes. Nenhuma migration foi aplicada ao banco pessoal durante esta implementação.

## Validação

Os testes cobrem conservação da cópia, solicitação repetida/conflitante, impressão alterada, período, paginação, isolamento por carteira, compras incompatíveis, dupla contagem, remoção auditável, estorno, custos com taxas, câmbio histórico/manual, precisão e conclusão por linha. Testes de navegador verificam salvar, vincular, atualizar, estornar e reabrir o plano; apresentação cobre desktop/celular nos temas claro/escuro e ocultação de valores.

Resultado local: **688 testes aprovados** — 278 unitários .NET, 176 de integração, 148 Angular, 68 de apresentação e 18 E2E. Lint, formatação e build aprovados; pacote inicial mantido em 455,77 kB. A suíte Angular foi repetida sem concorrência com as suítes pesadas após um timeout de um teste de cotações; os 148 testes passaram sem alteração do timeout. Imagens do vínculo no celular e da execução no tema escuro foram conferidas. A base E2E temporária foi removida ao concluir.
