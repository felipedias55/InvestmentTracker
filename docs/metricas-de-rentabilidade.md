# Métricas de rentabilidade

Em **Evolução e fechamentos**, a tabela de rentabilidade acompanha os filtros de mês/ano. Usa o patrimônio total (ativos e patrimônio externo) de duas fotografias, seus equivalentes históricos e os movimentos entre as datas reais exibidas. Não altera saldos, registros ou fotografias e não requer migração.

## Escopo e convenções

- O saldo da fotografia inicial é o capital inicial; não se tenta reconstruir o retorno anterior ao primeiro registro.
- Fluxos na data inicial já pertencem ao saldo inicial. Incluem-se os posteriores a ela até a data final, inclusive. Adota-se fim do dia, pois os movimentos têm data sem horário.
- Aporte tem sinal positivo; retirada e provento recebido fora da carteira têm sinal negativo na perspectiva da carteira. Estornos invertem o sinal original na data efetiva registrada. Compras/vendas com saldo interno e transferências internas não são aportes novos.
- Proventos retidos já compõem o patrimônio e não são somados novamente. Proventos distribuídos entram como pagamentos ao investidor.
- Valores estrangeiros usam equivalentes históricos registrados, nunca o câmbio atual. Para proventos, usa-se a última revisão auditada na moeda das fotografias.
- A visão anual usa a última fotografia do ano e a última do ano anterior; não soma taxas mensais. O intervalo pode ser parcial ou ultrapassar 365 dias. A visão mensal exige uma fotografia no mês anterior.

## Dietz modificado — estimativa do intervalo

`R = (Vfinal − Vinicial − ΣF) / (Vinicial + Σ(w × F))`

`w = dias entre o fluxo e a fotografia final / dias totais entre fotografias`.

Exemplo sintético: patrimônio inicial 1.000, aporte de 500 no meio de um intervalo de 30 dias e patrimônio final 1.600: `(1600 − 1000 − 500) / (1000 + 0,5 × 500) = 8%`.

Não é TWR exato: faltam avaliações em cada movimentação externa. Fluxos grandes e volatilidade elevada podem afastar a estimativa do retorno exato. O percentual não é anualizado.

## XIRR — taxa anualizada ponderada pelo dinheiro

Resolve `Σ(Ci / (1+r)^(dias_i/365)) = 0`, com capital inicial e aportes negativos; patrimônio final, retiradas e distribuições positivos. Agrupa valores da mesma data antes de resolver.

A implementação usa bisseção em `log(1+r)`, com normalização numérica. Por conservadorismo, só calcula fluxos com uma mudança de sinal, do negativo para o positivo: assim não escolhe arbitrariamente entre possíveis raízes. Padrões não convencionais ficam sem XIRR mesmo quando poderiam admitir uma solução. O domínio suportado é maior que −99,9999% e menor que 1.000.000% ao ano. Uma perda total pode aparecer como −100% em Dietz, mas não terá XIRR neste domínio.

XIRR anualizada em períodos curtos pode ser muito elevada e não representa previsão nem o ganho do mês. Não somar taxas mensais ou comparar a taxa anualizada diretamente com Dietz do mês.

## Qualidade dos dados

Ambas as taxas ficam indisponíveis quando faltam fotografias, a moeda-base difere, as fotografias estão reabertas/desatualizadas, o intervalo é inválido, o patrimônio inicial não é positivo ou faltam conversões históricas. Dietz exige capital ponderado positivo. A tela explica os bloqueios e avisa sobre câmbio anterior/alternativo.

O sistema não consegue provar que o usuário registrou todos os fluxos. Aportes omitidos, cotações antigas ou ajustes de saldo podem distorcer a rentabilidade. Ajustes manuais que não foram classificados como aporte contam como resultado; o residual monetário continua incluindo câmbio, custos e outros efeitos. As métricas não são uma certificação de desempenho nem calculam separadamente retorno líquido de todos os impostos pessoais.

## Verificação

Testes cobrem ganhos, perdas e retorno zero; datas de fronteira; ponderação de aportes; proventos retidos/distribuídos sem duplicação; estornos; conversões faltantes; capital inválido; fotografias reabertas/desatualizadas; fluxos não convencionais e estabilidade em intervalos longos. A integração verifica a resposta HTTP a partir de fotografias persistidas e a suspensão após invalidá-las. O teste da interface verifica percentuais brasileiros, datas e motivos de indisponibilidade.

Referências: [GIPS / CFA Institute — metodologia e Dietz](https://www.gipsstandards.org/wp-content/uploads/2021/03/gips_standards_handbook_for_asset_owners.pdf), [Microsoft — XIRR e base de 365 dias](https://support.microsoft.com/en-us/excel/functions/xirr-function).
