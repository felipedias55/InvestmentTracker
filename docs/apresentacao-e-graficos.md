# Apresentação das informações e gráficos

Entrega de 03/10/2026, apenas na interface: sem migração nem alteração de dados financeiros.

## Dashboard

- Patrimônio total em destaque; cartões secundários para posições, custo e proventos.
- Referências de atualização dos dados das posições, câmbio e configuração das metas. A data da posição não comprova atualização da cotação de mercado.
- Atalhos para carteira, simulação de aporte, recebimentos e evolução.
- Formato selecionável por gráfico: rosca para distribuição, barras para comparar participação atual e meta. Países não possuem metas.
- Tipo, ordem e recolhimento persistem por carteira no navegador; restaurar gráficos também restaura os formatos.
- Filtros não recalculam percentuais sobre o subconjunto. A rosca mantém o denominador da carteira inteira, usando cinza para a participação não exibida. Dados indisponíveis não se tornam zero.

## Evolução

- Alternância entre linha e colunas do patrimônio, com moeda e agrupamento existentes.
- Linha interrompida em lacunas, moedas diferentes e fotografias reabertas/desatualizadas. As fotografias preservadas continuam disponíveis pelos botões abaixo.
- Cascata para um intervalo selecionado: patrimônio inicial, aportes líquidos, proventos retidos, valorização/outros efeitos e patrimônio final. Proventos distribuídos não pertencem ao patrimônio final; continuam na decomposição econômica já existente.
- A cascata só aparece quando todos os componentes da comparação estão disponíveis. O residual não representa exclusivamente oscilação de preços.

## Proventos e carteira

- Colunas mensais/anuais agregam os recebimentos filtrados; ranking horizontal no agrupamento por ativo.
- Uma série por moeda original, ou equivalentes históricos na moeda-base. Escalas separadas, sem somar moedas diferentes.
- Qualquer conversão faltante torna o total do grupo pendente. Estornos podem produzir barras negativas. Períodos sem registros não são inventados.
- Explicações secundárias em seções expansíveis; busca por ativo também junto à análise.
- Posições em cartões com categoria/setor e blocos de valores, preservando ações e filtros.

## Acessibilidade e privacidade

Gráficos mantêm tabelas de apoio, rótulos, valores com sinal e controles nativos de teclado. Séries longas permitem rolagem horizontal dentro do gráfico. Ocultar valores mascara valores monetários, eixos, tabelas e títulos dos gráficos; percentuais e formatos continuam visíveis. Campos de edição permanecem visíveis, conforme o funcionamento do modo de privacidade.

## Testes

`npm test -- --watch=false` valida lacunas, geometria de sinais/cascata, denominador dos filtros, conversões incompletas e ocultação de valores.

`npm run test:presentation` usa Playwright em desktop e Pixel 7 emulado, com todos os endpoints interceptados e dados fictícios. Não inicia API, não usa LocalDB e não substitui a suíte integrada `npm run test:e2e`. Capturas ficam em `test-results`, ignorado pelo Git.

## Grade da dashboard — 14/10/2026

A primeira etapa reorganiza os gráficos existentes de categoria, setor e país. Em Organizar dashboard, use a alça para arrastar no desktop ou as setas para mover pelo teclado/celular. O destino recebe destaque e os blocos se encaixam sem sobreposição. Compacto e Médio ocupam metade da largura, com alturas diferentes; Amplo ocupa a largura inteira. Conteúdos extensos têm rolagem interna no desktop. Recolher reduz o bloco ao cabeçalho, preservando o tamanho para quando for expandido.

Ordem, tamanho, formato e recolhimento ficam salvos por carteira neste navegador, com compatibilidade com preferências anteriores. Restaurar organização padrão reinicia essas opções. No celular, a ordem é preservada em uma coluna com altura natural, sem arraste. Filtros e referências ficam recolhidos inicialmente. Os novos indicadores por período, proventos e evolução na dashboard continuam destinados às próximas entregas; os totais atuais mantêm seus significados.

## Análises na grade — 14/10/2026

Três novos blocos integram a grade: evolução patrimonial, proventos por período e destaques dos ativos. Preferências antigas são preservadas e os novos blocos são acrescentados ao fim. Todos podem ser movidos, redimensionados e recolhidos.

Evolução permite selecionar mês/ano, filtrar ano e detalhar um fechamento com aportes líquidos no intervalo, valorização/outros efeitos, Dietz estimado e XIRR anualizada retornados pelo backend. Outra moeda, fotografia ausente, desatualizada ou reaberta impedem a comparação. O início e o fim do intervalo são explícitos: não se presume que toda comparação comece no primeiro dia do mês.

Proventos apresenta colunas mensais/anuais nas moedas originais ou em moeda-base com conversões históricas existentes. Estornos são descontados; valores incompletos não viram zero. O ranking apresenta até cinco altas e cinco quedas sobre o preço médio, excluindo posições sem quantidade ou base válida. Não corresponde à variação do período.

Falhas de carregamento de uma análise não bloqueiam o restante da dashboard e oferecem nova tentativa. Trocar de carteira cancela as requisições anteriores. A privacidade usa os mesmos componentes de valores/gráficos da aplicação. Resumo por período, comparação de recebimentos e movimentações recentes foram concluídos na etapa descrita abaixo.

## Resumo e acompanhamento — última entrega da dashboard

Acima da grade, Resumo por período permite escolher mês ou ano. Patrimônio refere-se à fotografia, enquanto aportes líquidos e proventos usam o período civil. Valorização/outros efeitos usa o intervalo entre fotografias informado na tela. Esses valores não são somados entre si; os cartões atuais da carteira permanecem identificados separadamente.

O comparativo recolhível de proventos mostra o período civil imediatamente anterior, diferença monetária e variação percentual quando o valor anterior é positivo. Períodos em andamento são identificados como parciais; ausência de registros e conversões incompletas não são interpretadas como zero. As somas/subtrações de apresentação preservam casas decimais; regras de rentabilidade e valorização continuam no backend. Troca de carteira e recarga limpam os resultados anteriores, e erros de histórico/proventos são informados separadamente.

Movimentações recentes integra a grade e começa recolhido, inclusive na migração de preferências antigas. Ao expandir, consulta os cinco registros mais recentes por data da operação, com desempate pelo instante de criação e ID. A lista preserva identificação de estorno e substituição; ajustes administrativos não aparecem como entradas de dinheiro. A consulta atual usa o endpoint existente de listagem e seleciona cinco na interface; paginação no servidor pode ser considerada se o histórico crescer muito. Recolher cancela consulta pendente e reabrir consulta novamente.

Validação automatizada: resumo parcial, virada de mês/ano, moeda incompatível, fotografia desatualizada, conversões pendentes, denominador nulo/zero, valores negativos, precisão decimal, privacidade, movimentações recentes e organização em desktop/celular com dados fictícios. Não houve alteração de dados financeiros ou migration.

## Controles de organização — 23/10/2026

Organizar dashboard abre uma faixa própria em cada gráfico, com posição na ordem, alça, Subir, Descer e Tamanho. Formato e Recolher/Expandir continuam junto ao título. As setas seguem a ordem de leitura salva, mesmo quando a grade combina larguras diferentes; não representam troca de células. No celular, use as setas, com tamanho na linha seguinte.

Ao mover, o gráfico anuncia a nova posição e mantém o foco no seu controle; ao atingir uma extremidade, o foco passa para a outra seta habilitada. A prévia do arraste contém o título; o destino tem contorno e texto Soltar aqui. Esc cancela sem alterar a ordem. Preferências anteriores por carteira e Restaurar organização padrão mantêm o mesmo formato e comportamento. As alturas aumentam temporariamente no modo de organização para acomodar seus controles.

Validação e limites, incluindo a conferência visual ainda pendente da imagem nativa de arraste no sistema operacional, estão no [registro do marco 6](plano-usabilidade-e-navegacao.md#registro-do-marco-6--23102026).

## Distribuições compactas — 23/10/2026

No Dashboard, categorias, setores e países apresentam até cinco itens na legenda/lista de barras. As roscas mantêm todas as fatias dos filtros atuais; o limite é apenas da legenda. Valores monetários deixam a legenda e as barras usam um marcador para a meta. Nomes completos, valores, metas e diferenças podem ser consultados em Ver detalhes, num painel separado, com todos os grupos filtrados e a mesma privacidade dos valores. Esc fecha e devolve o foco.

Filtros não mudam o denominador da carteira. O formato escolhido e as preferências de organização foram preservados. O simulador e os detalhes históricos mantêm suas tabelas anteriores. Alturas gerais da grade e os outros blocos serão tratados nas próximas etapas do [refinamento do Dashboard](refinamento-dashboard.md).

## Evolução resumida no Dashboard — 25/10/2026

O cartão de Evolução patrimonial mantém os filtros, a linha e os avisos de revisão. Use Ver detalhes da evolução para selecionar um fechamento e consultar intervalo, aportes líquidos, valorização/outros efeitos, Dietz, XIRR, notas e tabela dos períodos. O painel fecha por Esc ou Fechar detalhes e devolve o foco. Histórico completo leva à tela de Evolução com os métodos de cálculo existentes.

Essa separação reduz o conteúdo vertical do cartão; lacunas e comparações inválidas continuam explícitas. Os valores não foram recalculados, e os demais gráficos mantêm suas tabelas de apoio anteriores. Alturas da grade e limitação de períodos continuam em etapas próprias do [refinamento](refinamento-dashboard.md).

## Destaques visuais e janela de proventos — 25/10/2026

Destaques dos ativos resume três altas e três quedas sobre o preço médio, com barras na mesma escala e percentuais com sinal. Ver ranking completo reúne todos os ativos elegíveis e suas cotações unitárias na moeda original. A variação não representa retorno com proventos nem desempenho mensal.

Proventos permite selecionar uma moeda original ou equivalentes na moeda-base, mostrando um gráfico por vez. A janela inicial tem cinco períodos com registros; Anteriores/Seguintes percorrem o histórico e a opção de 12 períodos amplia a janela. Ver detalhes dos proventos oferece a tabela completa da moeda, filtro de ano e frequência mensal/anual. Períodos ausentes não viram zero e pendências fora da janela continuam avisadas. Detalhes ficam fora dos cartões e preservam privacidade e foco. Ver [refinamento do Dashboard](refinamento-dashboard.md).

## Alturas naturais e movimentações recentes — 25/10/2026

A grade do Dashboard acompanha a altura do conteúdo e preserva a ordem de leitura, sem rolagem vertical interna nos cartões. Compacto reduz espaços e gráficos, Médio mantém o tamanho regular e Amplo ocupa toda a largura no desktop. Os gráficos temporais acompanham a largura disponível. Recolhimento e escolhas salvas por carteira continuam preservados; no celular, a grade tem uma coluna. Linhas com dois cartões de alturas diferentes ainda podem deixar espaço abaixo do menor.

Movimentações recentes mantém cinco registros com tipo, data, nome abreviado, valor quando aplicável e vínculos de estorno/substituição. Selecione um registro para consultar descrição, nomes e efeitos completos no painel separado, com privacidade e retorno de foco. Para auditoria atualizada e ações, use Movimentações. Rolagem horizontal de séries extensas e tabelas completas continua disponível. Ver [refinamento do Dashboard](refinamento-dashboard.md).
