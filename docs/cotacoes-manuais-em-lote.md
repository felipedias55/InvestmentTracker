# Atualização manual de cotações em lote

Na **Carteira**, aplique busca/filtros e clique em **Atualizar cotações em lote**.

1. Preencha os preços unitários na moeda original de cada ativo, no padrão brasileiro e com até quatro casas decimais. Posições zeradas ficam fora da edição.
2. Deixe em branco as posições que não deseja atualizar. São aceitas até 200 cotações por lote.
3. Selecione **Revisar cotações**. Confira preço unitário, valor total anterior e novo valor total.
4. Confirme o lote. A carteira mantém os filtros, a moeda de exibição e a posição da lista.

O novo valor da posição é `quantidade × cotação unitária`, arredondado para quatro casas pelo critério de empate para o par (o mesmo usado no backend). A prévia usa aritmética inteira para preservar a precisão decimal. O formulário comum de ajuste de posição continua trabalhando com valor total; este modo recebe **preço unitário**.

## Regras e auditoria

- Altera somente valor atual e data de atualização das posições selecionadas. Quantidades, custos, proventos e saldos externos são preservados.
- Não gera compra, venda nem aporte. Registra um ajuste de posições em Movimentações, com os efeitos antes/depois e os preços informados no payload de auditoria.
- A data é a data corrente em São Paulo. Não oferece lançamento de cotação retroativa. Fotografias dessa data ou posteriores ficam sinalizadas para revisão, sem substituir seus valores.
- O endpoint `PUT /api/portfolios/{id}/quotes` valida o lote e grava em uma transação. Posição removida, moeda, quantidade, valor ou data divergente desde a revisão resultam em conflito, sem salvar parte do lote.
- Uma solicitação repetida com o mesmo identificador e conteúdo não gera outro registro. Se houver falha de comunicação, a tela mantém a confirmação para repetir a mesma solicitação; os campos ficam protegidos enquanto o resultado é incerto.
- Os testes verificam idempotência, concorrência, rollback após a primeira gravação, precisão, conversões/moedas, fotografias e edição em desktop/celular com dados sintéticos.

Não exige migração. A integração futura com APIs pode reutilizar a separação entre cotação unitária e atualização da posição, preservando o fluxo manual.
