# Evolução da carteira e histórico de aportes

## Como começar

1. Aplique a migration `PortfolioHistory` e reinicie a aplicação.
2. Confira as posições, proventos e patrimônio externo com seus valores atuais.
3. Abra **Evolução e aportes**, selecione a carteira e clique em **Registrar fotografia**. Confirme após verificar os saldos.
4. Registre os aportes e retiradas reais com data, moeda, valor e observação. Os registros não alteram automaticamente os saldos.
5. Faça uma fotografia nos meses seguintes. A tela apresentará gráficos e comparação mensal e anual; clique em uma barra ou em **Ver fotografia** para consultar aquela composição.

```powershell
dotnet ef database update --project InvestmentTracker.Infrastructure --startup-project InvestmentTracker.Api
```

Se a API estiver em execução no Visual Studio, pare a depuração antes do comando para liberar os arquivos de compilação. A implementação foi validada em Release e em bancos de teste isolados; não atualizou o banco pessoal.

## Fotografias manuais

Cada carteira tem no máximo uma fotografia por mês. O mês e a data vêm do servidor no fuso **America/Sao_Paulo**; o instante UTC de captura também é guardado. Não é possível escolher um mês passado para fotografar saldos atuais nem criar fotografias futuras. A primeira fotografia é o ponto de partida real. Meses anteriores não são reconstruídos.

Durante o mês corrente, **Atualizar fotografia deste mês** permite substituir explicitamente a fotografia após confirmação. O ID é mantido; a versão anterior desse mês é substituída. Depois que o mês passa, o sistema não oferece alteração ou remoção daquela fotografia. Não existe fechamento agendado nesta implementação.

Uma fotografia preserva:

- nome e moeda-base da carteira;
- ativos, quantidades, nomes, tickers, classificações, valores originais e valores convertidos;
- custo investido e proventos acumulados;
- patrimônio externo, moedas e descrições;
- categorias/setores e metas vigentes;
- distribuições por categoria, setor e país;
- taxas de câmbio, datas e indicação de cotação anterior/fallback.

Alterar ou remover um ativo, renomear um cadastro, editar uma meta ou mudar os valores atuais não modifica o conteúdo das fotografias anteriores. A consulta de uma fotografia não busca câmbio novo. Os totais consolidados do índice histórico são guardados com quatro casas decimais; o documento completo conserva os valores e taxas do resumo original. A interface apresenta dinheiro com duas casas e padrão brasileiro.

O fechamento lê os dados e persiste a fotografia numa transação serializável, com coordenação por carteira. Capturas simultâneas não criam dois registros no mesmo mês. Sem câmbio suficiente para consolidar todo o patrimônio, a gravação é recusada; taxas anteriores/fallback podem ser utilizadas e ficam identificadas.

## Aportes e retiradas

Aporte significa dinheiro novo entrando na carteira; retirada significa dinheiro saindo. O escopo inclui o patrimônio externo vinculado à carteira. Transferências internas, compra/venda usando dinheiro que já estava na carteira e proventos não devem ser registrados como novos aportes.

Os registros são manuais e independentes do simulador. O simulador sugere uma distribuição; esta tela registra movimentos efetivamente realizados. Registrar, corrigir ou remover um movimento não compra ativos, movimenta saldos ou regrava fotografias.

- Data entre 01/01/1900 e hoje, conforme o fuso acima; registros anteriores à primeira fotografia são permitidos quando o usuário conhece os fatos.
- Valor maior que zero, até 15 inteiros e quatro casas decimais.
- Moeda existente e observação opcional de até 500 caracteres.
- Na mesma moeda-base, o valor equivalente é o próprio valor original.
- Em outra moeda, é possível informar o equivalente efetivo na moeda-base na data do movimento. O sistema **não usa o câmbio de hoje para preencher o passado**. Sem esse dado, o registro original permanece válido, mas comparações dependentes da conversão ficam indisponíveis.
- A moeda-base usada para registrar o equivalente fica preservada no movimento, mesmo que a carteira troque sua base depois. A edição informa valores nessa mesma base histórica.
- Exclusões e correções recalculam os indicadores derivados. As fotografias de saldos permanecem inalteradas.

A moeda original referenciada por movimentos não pode ser excluída ou ter seu código alterado.

## Leitura da evolução

```text
Patrimônio = valor dos ativos + patrimônio externo
Variação = patrimônio da fotografia final − patrimônio da fotografia inicial
Movimento líquido = aportes − retiradas entre as fotografias
Variação sem movimentos = variação − movimento líquido
```

Exemplo: patrimônio passa de 1.000 para 1.300, com aporte de 100 e retirada de 50 entre as fotografias. A variação bruta é 300; a variação descontando os movimentos é 250.

Esse indicador **não é rentabilidade percentual** nem cálculo de TWR/XIRR. Pode refletir oscilação dos ativos, câmbio, atualização manual de saldos, proventos refletidos no patrimônio e movimentos não registrados. Os proventos acumulados são mostrados separadamente e não são adicionados novamente ao patrimônio.

### Datas e limites da comparação

A fotografia representa os saldos até sua data local, inclusive. Portanto, a comparação usa movimentos com data **posterior à fotografia inicial e até a fotografia final, inclusive**. Movimentos do dia da primeira fotografia são considerados já refletidos nela. Como os movimentos têm data sem horário, atualize os saldos incluindo os movimentos do dia antes de fotografar.

As colunas “Aportes do período” e “Retiradas do período” usam o mês/ano calendário inteiro, até os movimentos registrados. Já a variação ajustada usa o intervalo entre as datas reais de captura, que pode ser diferente se o fechamento aconteceu antes do último dia do mês.

- Mês a mês: compara com a fotografia do mês imediatamente anterior. Se faltar esse mês, não é criado um saldo zero nem uma comparação implícita com um mês distante.
- Ano a ano: utiliza a última fotografia disponível em cada ano e compara com a última do ano anterior. Não soma patrimônios mensais. As datas reais são mostradas; a última fotografia pode não ser de dezembro.
- Sem fotografia: o patrimônio fica em branco, e o gráfico apresenta uma lacuna.
- Sem fotografia anterior: não há variação calculada.
- Fotografias em moedas-base diferentes: valores históricos são preservados, mas não há subtração direta entre moedas. O gráfico separa as moedas pelo seletor.
- Se um movimento não tiver equivalente na moeda da comparação, a variação bruta continua disponível e a variação ajustada fica indisponível. Se a moeda original do movimento já for a moeda da fotografia, usa-se o valor original.

Registrar movimentos antigos melhora o histórico de aportes, mas não permite deduzir os saldos, quantidades ou cotações de meses sem fotografia. O sistema não inventa esses dados.

## API e persistência

| Método | Caminho relativo a `/api/portfolios/{portfolioId}/history` | Resultado |
|---|---|---|
| GET | `/` | Períodos mensais/anuais e movimentos da carteira |
| POST | `/snapshots` | Fotografia do mês corrente; 201 com Location |
| PUT | `/snapshots/current` | Substituição explícita do mês corrente; 200 |
| GET | `/snapshots/{id}` | Conteúdo congelado da fotografia |
| POST | `/cash-flows` | Novo aporte/retirada; 201 |
| PUT | `/cash-flows/{id}` | Corrigir movimento; 204 |
| DELETE | `/cash-flows/{id}` | Remover movimento; 204 |

Exemplo de aporte em moeda estrangeira, com equivalente conhecido (substitua o ID da moeda):

```json
{"date":"2026-09-08","kind":"contribution","currencyId":2,"amount":"100.1234","baseAmount":"510.50","notes":"Aporte realizado"}
```

`kind` aceita `contribution` ou `withdrawal`. Todos os IDs são limitados à carteira da rota. Referência inexistente retorna 404; entrada inválida retorna 400; fotografia duplicada, fechamento sem câmbio ou conflito concorrente retornam 409, usando ProblemDetails.

Novas tabelas:

- `PortfolioCashFlow`: movimentos com valor original, equivalente informado e datas de criação/edição.
- `PortfolioSnapshot`: índice mensal, totais e documento JSON versionado (`PayloadVersion = 1`) contendo o dashboard capturado. O JSON evita depender dos cadastros vivos para reconstruir a fotografia; valores derivados são persistidos aqui porque representam evidência histórica.

As tabelas têm FKs Restrict, índice único carteira/mês, precisão decimal e constraints. A migration é aditiva, sem fotografias ou aportes inventados. O rollback é bloqueado se apagaria histórico já preenchido. A arquitetura da solução, repositórios específicos, serviços Application, DTOs e namespaces com chaves foram mantidos.

## Verificação

Testes unitários cobrem os cálculos mensais/anuais, intervalos de datas, aportes e retiradas, lacunas, moeda-base diferente e equivalente desconhecido. Integração com SQL Server isolado cobre conteúdo congelado, atualização somente do mês corrente, concorrência, escopo entre carteiras, correções de movimentos, fuso e bloqueio de fotografias parciais. Os testes Angular cobrem gráficos, abertura de fotografia, confirmação, edição brasileira, cancelamento de consultas antigas e filtros.

A etapa de publicação e uma suíte de ponta a ponta no navegador continuam adiadas. O usuário pode começar a acompanhar com a primeira fotografia depois de aplicar a migration.
