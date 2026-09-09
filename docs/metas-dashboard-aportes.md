# Metas, dashboard e simulador de aportes

## Uso

Após atualizar o banco, inicie API e Angular normalmente pelo Visual Studio ou pelos comandos do README.

1. Abra **Metas**, escolha a carteira e configure categorias ou setores. Digite percentuais brasileiros, por exemplo `33,3333`. Cada conjunto deve somar exatamente 100%. Clique em **Salvar conjunto de metas**.
2. Abra **Dashboard** para consultar o valor atual, o total investido, a quantidade de posições e as distribuições por categoria, setor e país. Todos os valores monetários usam a moeda-base da carteira, com apresentação brasileira.
3. Abra **Aportes**, escolha categoria ou setor e informe o aporte na moeda-base, por exemplo `1.234,50`. A simulação não grava posições nem registra compras.

A carteira selecionada nas telas de análise é mantida durante a navegação na sessão da SPA. O dashboard é a página inicial; a edição de posições continua em **Carteira**.

## Metas

- Armazenamento em `CategoryAllocationTarget` e `SectorAllocationTarget`, por carteira, com os relacionamentos existentes.
- O contrato da API e o banco usam frações: `0.333333` representa `33,3333%`. A interface recebe percentuais, com até quatro casas decimais; o banco usa `decimal(9,6)`.
- Cada conjunto precisa conter pelo menos uma linha, ter soma exata de `1`, IDs distintos e referências existentes. Cada fração está entre `0` e `1`.
- O envio substitui **todo** o conjunto daquela dimensão. A transação bloqueia a carteira antes de substituir as linhas, inclusive quando o conjunto ainda está vazio; duas gravações concorrentes nunca misturam partes de conjuntos diferentes. A última gravação completa prevalece.
- Linhas em branco ficam fora do conjunto. Depois de configurar metas, grupos omitidos são comparados com meta de zero. Uma linha com 0% explícito pode ser cadastrada.
- Sem um conjunto de metas, a distribuição atual continua visível, mas a meta e a diferença aparecem como não configuradas. O simulador exige um conjunto válido somando 100%.
- A exclusão de categorias/setores referenciados pelas metas retorna conflito, seguindo o padrão dos demais cadastros.

## Dashboard

A análise reutiliza o mesmo resumo de posições e câmbio da carteira. As classificações são carregadas junto com as posições, sem consultar novamente as posições para calcular as distribuições.

```text
Valor do grupo = soma dos valores atuais convertidos para a moeda-base
Percentual atual = valor do grupo / valor atual da carteira
Diferença = meta - percentual atual
```

A diferença na tela é expressa em pontos percentuais. Positivo significa abaixo da meta; negativo significa acima. Grupos com metas e sem posições também aparecem. Se o valor atual total for zero, os percentuais atuais são zero, evitando divisão por zero.

Se faltar qualquer taxa, os totais, percentuais e diferenças ficam indisponíveis e o aporte retorna conflito; não são usadas somas parciais. Metas e dados originais continuam disponíveis. Taxas antigas ou de fallback permitem a análise com avisos e datas de referência.

O dashboard agora apresenta ativos, patrimônio externo vinculado à carteira e patrimônio total, além dos proventos acumulados. As distribuições e os aportes continuam considerando somente as posições em ativos. Detalhes: [patrimônio externo e proventos](patrimonio-externo-e-proventos.md). O histórico manual está disponível em [Evolução e aportes](historico-da-carteira.md), separado do simulador. Não há cálculo de rentabilidade percentual histórica/cambial; o total investido convertido usa o câmbio de referência atual, como na tela Carteira.

## Fórmula do aporte

Regra fornecida pelo usuário em 08/09/2026:

```text
Peso ajustado = máximo(diferença, 0)
Soma dos pesos = soma dos pesos ajustados
Aporte sugerido = peso ajustado / soma dos pesos × aporte
```

Somente grupos abaixo da meta recebem valores. Categoria e setor são **simulações alternativas do mesmo aporte**, não dois orçamentos que devem ser somados. A distribuição é por grupo, não uma indicação de compra de ativos individuais.

Tratamentos adicionais:

- Sem pesos positivos: todos os aportes sugeridos são zero e o valor aparece como **sem distribuição**.
- Carteira vazia com metas: os percentuais atuais são zero e a primeira contribuição segue as metas.
- Aporte zero é permitido. Valores negativos, acima de 15 inteiros ou com mais de duas casas decimais são rejeitados.
- Os valores são truncados para centavos; os centavos restantes vão para as maiores frações, com desempate pelo ID do grupo. Assim, a soma das sugestões corresponde exatamente ao aporte quando há pesos positivos.
- Os cálculos são feitos com `decimal` na Application, em `ContributionCalculator`. Os resultados são derivados e não são persistidos. Valores financeiros e percentuais são strings decimais na resposta JSON para preservar a precisão; a API também aceita números JSON.
- Esta é a regra da planilha: utiliza a diferença percentual **antes** do aporte. Não é um otimizador que promete atingir todas as metas após um aporte grande.

Exemplo: percentuais atuais 20%, 30%, 50%; metas 40%, 40%, 20%. Os pesos são 20%, 10%, 0%. Com aporte de 300,00, as sugestões são 200,00, 100,00 e 0,00.

## API

| Método | Caminho | Comportamento |
|---|---|---|
| GET | `/api/portfolios/{id}/category-targets` | Conjunto salvo de categorias |
| PUT | `/api/portfolios/{id}/category-targets` | Substitui o conjunto, 204 |
| GET | `/api/portfolios/{id}/sector-targets` | Conjunto salvo de setores |
| PUT | `/api/portfolios/{id}/sector-targets` | Substitui o conjunto, 204 |
| GET | `/api/portfolios/{id}/dashboard` | Resumo da carteira e distribuições |
| GET | `/api/portfolios/{id}/allocation` | Distribuições e comparação com metas |
| POST | `/api/portfolios/{id}/contribution-analysis` | Simulação sem modificar posições |

Exemplo de PUT (substitua os IDs pelos cadastros existentes):

```json
{"targets":[{"groupId":1,"targetPercentage":"0.3"},{"groupId":2,"targetPercentage":"0.7"}]}
```

Exemplo de simulação:

```json
{"dimension":"category","amount":"1234.50"}
```

`dimension` aceita `category` ou `sector`. Carteira inexistente retorna 404; validação retorna 400; referências alteradas concorrentemente ou ausência de câmbio na simulação retornam 409. Os erros seguem o ProblemDetails central existente.

## Banco e execução

A migration `AllocationTargetConstraints` amplia a precisão das metas, acrescenta limites individuais e troca a exclusão em cascata da carteira por Restrict. Não cria metas, modifica posições ou insere ativos. Dados preexistentes fora dos limites causam falha da migration, sem correção silenciosa. O rollback também é bloqueado se reduzir a precisão das metas existentes.

Aplique no banco de desenvolvimento antes de usar as novas telas:

```powershell
dotnet ef database update --project InvestmentTracker.Infrastructure --startup-project InvestmentTracker.Api
```

A inicialização normal da API não aplica migrations automaticamente. A implementação foi validada em bancos temporários isolados; os testes não atualizam o banco pessoal.

## Validação

- Unitários: soma, precisão, duplicidade e referências das metas; distribuições; grupos sem posições; carteira vazia; taxas ausentes; fórmula da planilha; ausência de pesos; valores extremos e preservação de centavos.
- Integração: HTTP, persistência real em SQL Server e migrations; edição completa; isolamento entre carteiras e dimensões; concorrência; FK/constraint; moedas diferentes; ausência de câmbio; preservação das posições após simular.
- Angular: edição brasileira de percentuais e valores; envio exato; erros sem apagar formulário; troca de carteira/dimensão e cancelamento de consultas antigas; tabelas e totais indisponíveis; invalidação do resultado quando a entrada muda.

Execute `dotnet test`, `npm test -- --watch=false` e `npm run build` no diretório Angular, conforme o README. A suíte usa câmbio controlado, sem depender da API pública.
