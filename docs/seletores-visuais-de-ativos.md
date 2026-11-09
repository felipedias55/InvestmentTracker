# Seletores visuais de ativos — 05/11/2026

Compras/vendas, cadastro de saldo inicial da posição, recebimentos, eventos societários e correções atrasadas usam um seletor compartilhado com imagem, nome, ticker e moeda. A identidade mantém a imagem à esquerda e o enquadramento já usado no catálogo, carteira e dashboard, sem espaçamento interno adicional.

## Uso

No formulário, **Selecionar ativo** abre a busca. Quando já há seleção, **Trocar ativo** permite pesquisar outro instrumento. A busca ignora diferenças de maiúsculas e acentos, filtra os dados do catálogo já carregados e apresenta até 30 resultados por vez, com contagem e orientação para refinar buscas maiores.

A identificação do ativo atual permanece visível enquanto o usuário pesquisa, inclusive quando a busca não encontra resultados. Somente escolher um resultado altera o ID do formulário. Fechar a busca mantém a seleção e os valores preenchidos; **Limpar seleção do ativo** zera apenas a escolha. Nos campos obrigatórios será necessário selecionar novamente; na consulta de impactos, a posição adicional continua opcional e pode ficar vazia.

O botão do resultado contém imagem, nome, código e moeda. Imagem ausente ou indisponível usa a alternativa local do ticker. A mesma identificação fica no resumo do ativo escolhido. Não há busca por metadados, logotipos ou cotações nos provedores durante essa seleção: são usados o catálogo existente e o endpoint local de imagens.

## Teclado e layout

O seletor expande dentro do painel de edição existente, evitando sobreposições externas ao diálogo modal nativo. Os resultados têm rolagem própria e altura limitada, com nomes que podem quebrar linha em telas pequenas.

- Abrir a busca direciona o foco para o campo de pesquisa.
- Tab permite percorrer os controles e resultados; Enter/Espaço ativam os botões.
- Setas para cima/baixo percorrem os resultados.
- Enter no campo escolhe o resultado quando a busca tem uma única correspondência; com várias, direciona o foco para a primeira, sem selecionar silenciosamente.
- Escape fecha somente a busca e retorna o foco ao botão de seleção. O formulário permanece aberto.

## Preservação dos fluxos

O componente implementa `ControlValueAccessor` e comunica somente o ID numérico ao `FormControl` existente. Pesquisa não é valor de ativo e não grava operação ou posição. Carregamento assíncrono, seleção inicial por link/contexto, formulário desabilitado e redefinição do formulário continuam suportados. As regras e cálculos financeiros permanecem nos serviços atuais.

Em compras/vendas, quantidade, preço, taxas, data e o ativo escolhido são preservados durante a pesquisa. A origem/destino do dinheiro e o equivalente em moeda-base são reiniciados apenas quando o ID efetivamente muda, mantendo a regra anterior para evitar saldos de outra moeda. Escolher novamente o mesmo ativo não apaga esses campos.

Em cadastro de saldo inicial, a lista continua excluindo ativos que já têm posição naquela carteira. Na edição de uma posição, o controle do ativo permanece desabilitado: o seletor mostra sua identidade, mas não permite trocar ou limpar o ID. Quantidades, valores, moedas e vínculos usam os mesmos contratos e validações anteriores.

Em recebimentos, a lista é restrita às posições da carteira e inclui as zeradas para proventos após venda. O valor usa `assetId`, preservando o contrato de recebimentos. Trocar efetivamente o ativo limpa destino e equivalente em moeda-base; pesquisar ou escolher novamente o mesmo ativo preserva esses campos e o valor informado.

Em eventos societários e na consulta de impactos, a lista usa o ID da **posição**, enquanto a imagem usa o ID do **ativo**. Esses identificadores podem ser diferentes. A moeda vem da posição já carregada; a quantidade atual continua visível no formulário de eventos. Nenhuma conversão de posição em ativo é feita no envio da operação.

Na execução de correções, o seletor usa o catálogo de ativos existente, mantendo o `assetId` da operação e as regras de equivalência e invalidação da simulação. Apenas uma mudança efetiva do valor invalida o resultado; abrir/fechar a busca e selecionar novamente o mesmo ID preservam a simulação. Na consulta de impactos, limpar uma posição escolhida invalida a consulta anterior. Os controles respeitam o bloqueio durante a gravação.

## Escopo e continuidade

Não há migration nem mudanças de contratos da API. A padronização cobre os formulários de seleção individual de ativos/posições existentes: compras/vendas, inclusão/edição de posições, recebimentos, eventos societários, consulta de impactos e execução de correções atrasadas. Seletores de contas/saldos e filtros por texto mantêm sua finalidade própria. A atualização manual de imagens segue disponível conforme a escolha do usuário; atualização em lote de imagens continua adiada.

O [mapeamento revisável de setores retornados pelas fontes](revisao-de-setores.md) foi implementado em 05/11/2026. O módulo de gastos permanece independente e requer proposta funcional antes de implementação.

## Validação da primeira entrega

- 183 testes Angular aprovados, incluindo seleção explícita, pesquisa, formulário desabilitado, carregamento assíncrono e preservação dos valores da operação.
- 88 cenários de apresentação validados em desktop/celular e temas claro/escuro: 84 passaram na rodada completa; os quatro restantes passaram após atualizar as verificações do seletor antigo para o novo componente.
- 28 cenários E2E aprovados, incluindo cadastro de posição e compra com o ID escolhido. A base temporária dos testes foi removida; a base pessoal não recebeu migrations nesta entrega.
- Lint, verificação de formatação e build de produção aprovados. Bundle inicial de 456,83 kB, abaixo do limite de 500 kB.
- Prévias visuais inspecionadas; pesquisa, imagens e identificação do ativo não exigem consultas aos provedores externos.

## Validação da ampliação dos seletores

- 192 testes Angular aprovados na execução final isolada. A rodada simultânea com as suítes de navegador teve quatro timeouts, resolvidos ao repetir sem essa concorrência e sem aumentar os limites.
- 96 cenários visuais validados: 94 na rodada completa, com repetição dos quatro projetos de revisão de setor após corrigir a confirmação que permanecia marcada ao trocar o setor proposto. Os quatro projetos dos novos seletores também passaram novamente nessa repetição focada de oito cenários.
- 28 cenários E2E validados: 26 na rodada completa e os dois restantes após atualizar as seleções de recebimentos que ainda usavam o campo antigo. As duas bases temporárias foram removidas pelo script; o wrapper focado temporário também foi removido.
- Cobertura específica de posições zeradas em recebimentos, IDs distintos de posição/ativo, imagem pelo ID correto, envio explícito, moeda, preservação de rascunhos e invalidação de consultas/simulações somente quando o valor muda. Evidências visuais inspecionadas.
- Lint, build de produção e verificação de diferenças aprovados. Pacote inicial de 456,83 kB, abaixo do limite preservado de 500 kB. Sem nova migration, alteração de contrato da API ou consulta real aos provedores.

Ideias de investimento, teses e diário de decisões foram retirados dos planos por solicitação do usuário nesta rodada, com a decisão registrada no README, nas próximas etapas e nos documentos históricos relacionados. Benchmarks e análise independente de gastos mantêm seus escopos próprios.
