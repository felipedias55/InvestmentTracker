# Classificação e elegibilidade do planejamento

Primeira ampliação do planejamento de aportes, implementada em 30/10/2026.

## Cadastros explícitos

Em **Tipos de ativo**, escolha **Classificação do instrumento**. A escolha vale para todos os ativos vinculados ao tipo:

| Classificação | Elegibilidade |
| --- | --- |
| Outros instrumentos (`general`) | Combinações existentes com quantidade positiva podem receber sugestões. |
| FII (`fii`) / REIT (`reit`) | Só recebe sugestões se o setor estiver explicitamente marcado como imobiliário. |
| Revisar classificação (`unclassified`) | Não recebe sugestões até a classificação ser conferida. |

Em **Setores**, use **Setor imobiliário para planejamento**. Os cálculos não consultam os nomes do tipo, categoria ou setor para decidir compatibilidade. Renomear um cadastro preserva sua classificação. Tipos novos começam como Outros instrumentos: um tipo chamado “FII” não se torna FII automaticamente.

Uma combinação incompatível pode existir no cadastro, mas não recebe sugestão automática. Alterar a classificação afeta próximas simulações, sem regravar posições, metas, operações ou fotografias antigas. A classificação não seleciona empresas nem verifica a qualidade de um investimento.

## Bloqueios na estratégia

Em **Simular aporte → Configurar estratégia**, habilite a estratégia e abra **Elegibilidade por categoria e setor**. Marque **Bloquear aportes** nas combinações desejadas. A lista tem busca e paginação; filtros e páginas não apagam bloqueios. Pares salvos que deixaram de estar disponíveis continuam identificados para permitir removê-los.

| Regra | Reservas explícitas | Restante e sua base |
| --- | --- | --- |
| Excluir categoria da base do restante | A categoria ainda pode receber sua reserva. | Categoria fica fora. |
| Bloquear combinação categoria/setor | Combinação não recebe reserva. | Combinação fica fora. |
| Tipo sem classificação ou FII/REIT em setor não imobiliário | Posição não é elegível. | Posição fica fora da base elegível da estratégia. |

Uma categoria pode ter uma combinação bloqueada e outra permitida. A reserva é dividida entre as combinações restantes elegíveis. Sem destino elegível, a reserva fica **não distribuída**, sem ser transferida silenciosamente para outras regras. Se nenhuma posição for elegível para o restante, ele também fica não distribuído, inclusive no modo metas. Nos outros casos do modo metas, continuam sendo necessárias metas válidas nos grupos remanescentes.

Reservas distribuídas + restante distribuído + valor não distribuído conservam o aporte informado. Patrimônio e gráficos continuam mostrando a carteira inteira. A compatibilidade estruturada também vale para o plano conjunto tradicional, sem estratégia, mantendo sua análise pelas metas da carteira inteira.

**Salvar como estratégia padrão** cria uma revisão com os bloqueios. Simular pode usar campos ainda não salvos. Alterar a estratégia invalida a simulação e suas marcações temporárias. Configurações antigas sem `excludedCombinations` continuam válidas, sem bloqueios implícitos. Nenhuma compra ou venda é executada.

## Migration e atualização

A migration **20261030111431_StructuredPlanningClassification** adiciona `AssetType.InstrumentClass` e `Sector.IsRealEstate`, com restrição dos códigos de classificação no banco. Não modifica posições, saldos, metas, operações ou fotografias.

No preenchimento inicial, apenas nomes canônicos são reconhecidos: FII/FIIs, REIT/REITs, Ação/Ações e Stock/Stocks. Imobiliário/Real Estate recebem a marcação de setor. Tipos personalizados não reconhecidos ficam como Revisar classificação e setores personalizados começam sem marcação. Confira esses cadastros após atualizar. Essa correspondência ocorre somente na migration; os cálculos não repetem a inferência por nomes. O seed de bases novas já informa os campos estruturados.

Pare a API, use o backup manual habitual e, na raiz do projeto, execute:

```powershell
dotnet ef database update --project InvestmentTracker.Infrastructure --startup-project InvestmentTracker.Api
```

Reinicie a API e confira **Tipos de ativo** e **Setores**. A migration foi aplicada somente em bancos temporários de testes nesta entrega. O downgrade remove os novos campos e suas classificações; não é um mecanismo para desfazer simulações.

## Arquitetura e contratos

Domain contém os códigos e a compatibilidade de instrumento/setor. Application valida e calcula; Infrastructure persiste o schema e as revisões. Angular apresenta cadastros e regras com Material, painel de edição, busca e paginação existentes.

Os endpoints de tipos e setores expõem os campos novos; atualizações que os omitem preservam os valores existentes. `PositionDto` informa `instrumentClass` e `isRealEstateSector` com os dados atuais. `StrategyConfig.excludedCombinations` armazena pares `{ categoryId, sectorId }`, limitados a 500 pares distintos. Referências inválidas são recusadas. O JSON versionado da estratégia preserva revisões anteriores.

Objetivos e metas de patrimônio/renda permanecem para outras etapas. [Planejado versus realizado](planejado-versus-realizado.md) foi implementado em 03/11/2026. [Prioridades e sugestões de setor/moeda](prioridades-e-sugestoes-de-aporte.md) foram implementadas em 30/10/2026. Os [limites de concentração](limites-de-concentracao.md) foram implementados posteriormente em 30/10/2026. Não há preferências automáticas entre ativos nem valores reais pré-configurados.

## Validação

241 testes unitários .NET, 172 de integração, 137 Angular, 64 de apresentação e 14 E2E aprovados: **628 no total**. Lint, formatação de configurações e build aprovados; pacote inicial de 455,77 kB, abaixo do limite de 500 kB.

Os testes verificam independência dos nomes, campos omitidos em clientes antigos, bloqueios parciais/totais, conservação de centavos, preservação dos cadastros/valores financeiros, revisões e referências inválidas. A migration foi exercitada com tipos legados canônicos/personalizados e uma posição cujos valores, quantidade, proventos e data permaneceram iguais. O modelo foi conferido sem mudanças pendentes.

O E2E cadastra a classificação e o setor pela interface, salva um bloqueio, recarrega a página e o remove, em desktop/celular emulado. A apresentação cobre os dois temas. Integração e E2E usaram bases temporárias; a base E2E foi removida ao concluir.
