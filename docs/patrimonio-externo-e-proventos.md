# Patrimônio externo e proventos

## Uso

- **Patrimônio externo** no menu, também acessível pela página Carteira: selecione a carteira, informe nome, moeda, valor e descrição opcional. É possível criar, editar e remover registros. A remoção exige confirmação na tela.
- Valores externos são digitados na moeda selecionada, no padrão brasileiro, com até quatro casas decimais. A moeda-base da carteira é pré-selecionada ao criar. Na edição, o formulário sempre mostra o valor original, mesmo que a lista esteja convertida. Trocar a moeda do registro exige informar o valor correspondente; a troca não converte automaticamente o valor digitado.
- A lista alterna entre moeda original e moeda-base. O total externo sempre é convertido para a base; valores de moedas distintas não são somados diretamente.
- **Carteira**: cada posição possui o campo **Proventos acumulados**. Informe o total recebido naquela posição, na moeda original do ativo. O total convertido de proventos aparece no início da página e no dashboard; a lista e os subtotais originais também exibem proventos.
- **Dashboard**: mostra ativos da carteira, patrimônio externo, patrimônio total e proventos. A seleção da carteira é compartilhada com as telas de análise e patrimônio externo durante a navegação.

## Significado dos valores

```text
Patrimônio total = valor atual das posições + valor atual do patrimônio externo
Total de proventos = soma dos proventos das posições, convertidos para a moeda-base
```

Proventos são um total acumulado manual, não um saldo de caixa nem um histórico de pagamentos. As fotografias mensais agora preservam esses totais no [histórico da carteira](historico-da-carteira.md). Não há calendário individual de pagamentos, importação ou reinvestimento automático. Remover uma posição também remove seus proventos do total acompanhado.

Os proventos não são adicionados automaticamente ao patrimônio: podem já ter sido reinvestidos ou constar de um saldo registrado. Se desejar acompanhar dinheiro recebido e ainda disponível, registre seu saldo em patrimônio externo, evitando duplicar um valor já incluído em outro registro.

O patrimônio externo pertence à carteira selecionada, mas permanece fora da distribuição estratégica dos ativos. Metas, percentuais por categoria/setor/país e simulações de aporte continuam usando apenas o valor atual das posições. O patrimônio externo não possui essas classificações.

## Moedas e indisponibilidade

Cada registro externo tem moeda própria. A conversão reutiliza o serviço Frankfurter/cache existente, com datas, avisos de dados anteriores e fallback. O total de proventos usa o mesmo câmbio das demais informações da posição; não representa uma reconstrução do câmbio histórico na data do recebimento.

Se faltar câmbio externo, o total externo e o patrimônio total ficam indisponíveis. A análise das posições e o simulador continuam funcionando se houver câmbio para os ativos. Se faltar câmbio de uma posição, os totais das posições, inclusive proventos, ficam indisponíveis; os valores originais permanecem acessíveis. Nunca é exibido um patrimônio total parcial como completo.

Códigos de moeda usados pelo patrimônio externo também são protegidos contra alteração e exclusão, como já ocorria com ativos e carteiras.

## Arquitetura e API

A estrutura existente do Visual Studio e os namespaces com chaves foram mantidos. A feature `ExternalAssets` possui DTOs, interfaces e serviço na Application, repositório EF na Infrastructure e Controller na API. Não há regras de cálculo financeiro no Angular. A validação e os erros usam os padrões existentes de InputValidationException, ResourceConflictException e ProblemDetails.

| Método | Caminho | Resultado |
|---|---|---|
| GET | `/api/portfolios/{portfolioId}/external-assets` | Resumo com itens, total convertido e estado do câmbio |
| POST | `/api/portfolios/{portfolioId}/external-assets` | Criar; 201 com ID e Location para a coleção |
| PUT | `/api/portfolios/{portfolioId}/external-assets/{id}` | Atualizar; 204 |
| DELETE | `/api/portfolios/{portfolioId}/external-assets/{id}` | Remover; 204 |

Todas as consultas, alterações e remoções são limitadas à carteira da rota. Um ID de outra carteira retorna 404. Nomes têm até 200 caracteres; descrições, até 500; valores são não negativos, com até 15 dígitos inteiros e quatro decimais. Não há unicidade artificial de nome.

Exemplo de criação (substitua o ID de moeda):

```json
{"name":"Reserva","currencyId":1,"value":"1234.5678","description":"Saldo disponível"}
```

Na API de posições, `income` representa proventos. Na criação, omitir o campo ou enviar null resulta em zero. Na edição, omissão/null preserva o valor já salvo, permitindo compatibilidade com clientes anteriores; enviar `"0"` zera explicitamente os proventos. São aplicados os mesmos limites e precisão dos demais valores monetários das posições.

As respostas passam a incluir:

- posição: `income` e `baseIncome`;
- subtotais originais: `income`;
- resumo de carteira: `totalIncome`;
- dashboard: `externalAssets` e `totalWealth`.

Decimais financeiros são serializados como strings; a API também aceita números JSON.

## Migration

A migration `PortfolioExternalAssetsAndIncome`:

- acrescenta `PortfolioAsset.Income`, decimal(19,4), inicializado em zero para posições existentes;
- acrescenta carteira e moeda obrigatórias ao patrimônio externo, com FKs Restrict;
- ajusta o valor externo para decimal(19,4) e valida valores não negativos;
- mantém os valores já cadastrados nas posições.

A tabela ExternalAsset existia sem carteira/moeda, mas ainda não tinha fluxo de cadastro. Se houver registros legados inseridos diretamente nessa tabela, a migration aborta antes de alterar dados: é necessário definir explicitamente o vínculo e a moeda de cada registro e preparar o mapeamento da migration. Não são atribuídos carteira ou moeda presumidos a valores financeiros. O rollback também é bloqueado se descartaria vínculos externos ou proventos preenchidos.

Para atualizar o banco de desenvolvimento:

```powershell
dotnet ef database update --project InvestmentTracker.Infrastructure --startup-project InvestmentTracker.Api
```

Reinicie API e Angular após atualizar. Não é necessário refazer os cadastros ou preencher proventos imediatamente; eles começam em zero. A implementação não executou migrations no banco pessoal.

## Verificação desta entrega

Build da API e do Angular, suíte existente e verificações de integração dos novos fluxos em SQL Server temporário isolado. Foram verificados criação/edição/remoção de patrimônio externo, precisão, isolamento de carteiras, conversão, ausência de câmbio externo sem bloquear aportes, proteção de moedas e proventos sem duplicar o patrimônio. Os dados reais do usuário não foram usados nessas verificações.

Uma suíte de ponta a ponta no navegador, preparação para publicação e comparação manual com a planilha continuam adiadas, conforme solicitado.
