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
