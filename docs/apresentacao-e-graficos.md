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
