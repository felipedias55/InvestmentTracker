# Migração da interface e entregas de layout

Atualizado em 22/10/2026.

## Objetivo e situação

Padronizar componentes, reduzir rolagem, facilitar consulta e edição e apresentar explicações sob demanda. Preservar registros financeiros, cálculos, auditoria e funcionalidades existentes.

Este documento registra o plano discutido com o usuário. Após aprovação da opção visual A e realização do commit anterior pelo usuário, a primeira entrega foi autorizada e implementada. Os marcos de Movimentações/correções, Compras/vendas/eventos, Proventos, Evolução/fechamentos, Ativos/cadastros/patrimônio externo e Metas/estratégias/simulador também foram implementados. Dashboard/gráficos e revisão global também foram concluídos. O modo escuro também foi implementado.

Direção adotada: Angular Material como biblioteca de componentes, com tema próprio, e Carbon como referência de organização de telas densas em dados. Não instalar um segundo conjunto de componentes Carbon. Material e CDK 22.2.3 foram instalados após verificar compatibilidade com o Angular 22 do projeto.

Paleta aprovada pelo usuário: opção A, azul discreto nas ações e seleções, grafite na navegação, superfícies claras e logo dourado/prateado preservado. Modo escuro implementado na última entrega.

## Entregas em ordem

A prioridade indica importância; a ordem também considera dependências e reutilização. Os marcos de fundação/Carteira, Movimentações/correções, Compras/vendas/eventos, Proventos, Evolução/fechamentos, Ativos/cadastros/patrimônio externo e Metas/estratégias/simulador estão implementados. Os padrões foram propagados às telas e revisados globalmente. Gráficos próprios, tabelas de apoio e diálogo nativo compartilhado permanecem como componentes intencionais do produto, integrados à apresentação Material.

| Ordem | Entrega | Escopo | Prioridade | Dificuldade |
| --- | --- | --- | --- | --- |
| 1 | Fundação visual e técnica | Verificar compatibilidade com o Angular instalado; integrar a biblioteca; definir tipografia, espaçamentos, densidade, bordas, cores semânticas e estados. Preparar temas claro e escuro, implementando o escuro ao final. | Essencial | Alta |
| 2 | Ícones, botões e feedback | Uma família de ícones; substituir emojis e símbolos improvisados nos controles; padronizar ações principais, secundárias, destrutivas, carregamento, confirmações e mensagens. | Essencial | Média |
| 3 | Navegação | Header, logo, menu lateral recolhível, item ativo, títulos, ações de página e adaptação móvel. Preservar ocultação dos valores. | Alta | Média |
| 4 | Listagens e edição compartilhadas | Tabela, filtros, busca, paginação, ordenação, detalhes expansíveis, estados vazios e painel de edição. Preservar contexto após editar. | Essencial | Alta |
| 5 | Carteira e cotações | Tabela compacta no desktop, apresentação móvel adequada, comparação financeira, detalhes sob demanda e migração do painel de atualização em lote. | Muito alta | Alta |
| 6 | Movimentações e correções | Histórico paginado; resumo por lançamento; auditoria em detalhes; padronizar análise, estorno, simulação e confirmação de correções. | Muito alta | Muito alta |
| 7 | Compras, vendas e eventos | Separar consulta e registro; migrar seleção de ativos, campos monetários, taxas, eventos e confirmação. | Alta | Alta |
| 8 | Proventos | Separar recebimentos e análises; histórico paginado; formulário, conversões pendentes e detalhes padronizados. | Alta | Alta |
| 9 | Evolução e fechamentos | Organizar evolução, fotografias e fluxos históricos em áreas distintas; seleção de período e detalhes de fotografias. | Alta | Alta |
| 10 | Ativos, cadastros e patrimônio externo | Aplicar formulários, listagens, paginação e ações comuns; preservar ajuste manual de saldo. | Média | Média |
| 11 | Metas, estratégias e simulador | Organizar configuração, simulação e resultados; explicações de cálculo recolhíveis; padronizar regras, seletores e marcações temporárias. | Alta | Alta |
| 12 | Dashboard e gráficos | Alinhar cartões, filtros, legendas e controles; preservar arrastar, redimensionar, recolher e preferências existentes. | Média | Média/alta |
| 13 | Revisão global e remoção do legado | Retirar estilos/componentes substituídos; verificar consistência, teclado, foco, responsividade e fluxos completos. | Essencial | Alta |
| 14 | Modo escuro | Temas claro, escuro e sistema; persistir preferência; revisar contraste, gráficos, menus, diálogos e demais superfícies. | Posterior | Média/alta |

Primeiro marco proposto: etapas 1 a 4 aplicadas à Carteira (etapa 5). Revisar o resultado em desktop e celular antes de propagar o padrão. A biblioteca não substitui as regras financeiras nem exige trocar a tecnologia dos gráficos.

## Regras de componentes e interação

- Uma ação visualmente principal por contexto, com verbo específico: Salvar, Simular, Registrar compra.
- Ações secundárias com menor destaque; ações destrutivas identificadas pelo texto, aparência e confirmação da consequência.
- Uma família de ícones com significado consistente. Controles apenas com ícone precisam de nome acessível e ajuda disponível por teclado e toque.
- Rótulos persistentes nos campos, indicação consistente de obrigatoriedade e erros junto do campo.
- Preservar formato brasileiro, precisão decimal, moeda explícita e cálculos sem perda de precisão.
- Alinhar números para comparação; ocultação de valores também vale para gráficos, detalhes e novos componentes.
- Edição sem perder filtros, ordenação, página e posição na lista quando possível. No celular, adaptar o painel sem comprimir uma tabela inteira.
- Mensagens breves para sucesso; erros que exijam ação permanecem disponíveis. Não depender somente de cor.
- Padronizar estados de carregamento, vazio, nenhum resultado para o filtro, erro e dados parcialmente disponíveis.
- Não criar wrappers para cada componente da biblioteca: compartilhar padrões de produto efetivamente repetidos.

## Informações visíveis e ajuda sob demanda

Sempre visíveis: dados principais, moeda, status, erros, pendências que afetem a interpretação e consequências relevantes antes de confirmar.

Ajuda curta: controle de informação acessível por foco, clique e toque. Não depender apenas de hover ou do atributo title.

Explicações longas: “Como foi calculado”, “Entenda esta operação” ou “Ver detalhes”. Não transformar páginas de uso diário em documentação permanente.

Exemplos: a metodologia das taxas pode ficar na ajuda, mas o total debitado continua visível. A auditoria completa fica em detalhes, mas o status de estornado permanece na linha. Não esconder bloqueios, conversões pendentes ou impacto de correções atrás de ajuda genérica.

## Paginação, filtros e ações em lote

- Opções iniciais propostas: 10, 25 e 50 registros por página.
- Filtrar e ordenar o conjunto completo antes de paginar.
- Alteração de filtros retorna à primeira página; edição preserva a página sempre que possível. Se ela deixar de existir, ajustar para uma página válida.
- Totais e gráficos mantêm seu escopo financeiro correto; não somar apenas a página visível.
- Históricos crescentes de movimentos, operações e proventos precisam de paginação na API. Paginação apenas visual não resolve o carregamento integral.
- Paginação de API deve ter ordenação estável, com desempate por identificador, e respostas suficientes para navegar e informar o total.
- Distinguir explicitamente “resultados filtrados”, “página atual” e eventual seleção manual.
- Cotações: preservar a seleção capturada ao abrir o painel, somente quantidades positivas e campos vazios. Não restringir silenciosamente a consulta à página atual após introduzir paginação.
- Editar ou navegar não pode disparar consulta externa de cotação. Preservar cache, limites, endpoints gratuitos e confirmação antes de gravar.
- Marcas temporárias do simulador continuam temporárias e seguem seu comportamento documentado.

## Identidade e cores — opção A aprovada

Decisão: superfícies neutras, texto grafite e azul discreto para ações. Preservar dourado e prata no logo, sem gradientes metálicos em botões, tabelas ou grandes áreas.

Referências da paleta aplicada ao tema claro:

| Papel | Tema claro proposto |
| --- | --- |
| Fundo | Cinza frio muito claro `#F5F7FA` |
| Superfície | Branco `#FFFFFF` |
| Texto principal | Grafite `#20252D` |
| Texto secundário | Cinza `#526071` |
| Ação principal | Azul discreto `#315E99` |
| Identidade pontual | Dourado fosco `#B69A55` |
| Separadores | Cinza claro `#DDE3EB` |

A alternativa B, grafite e dourado pontual, não foi escolhida. Evitar dourado como cor universal de texto, foco e seleção.

O prata pode ser representado pelos neutros e divisórias, sem efeito metálico. Verde/vermelho continuam semânticos para variação e estados, acompanhados de texto/ícone. Não reutilizar dourado de marca como único indicador de aviso.

Mapear a paleta para os papéis do tema e validar contraste em cada combinação e estado, sem sobrescritas isoladas por página.

## Modo escuro

Entregar ao final, preparando desde a primeira etapa cores semânticas para fundo, superfície, texto, borda, ação, foco, seleção, erro, aviso e sucesso.

Preferência entre Claro, Escuro e Sistema, persistida localmente sem exigir alteração de registros financeiros. Em Sistema, acompanhar a preferência do dispositivo.

Não inverter cores automaticamente. Revisar logo, gráficos, eixos, tooltips, dropdowns, diálogos, estados desabilitados e foco. Gráficos devem continuar distinguíveis e respeitar a ocultação de valores em ambos os temas.

## Validação e preservação funcional

- Testar a cada entrega; não adiar toda a verificação para a etapa 13.
- Prioridade de regressão: formatos monetários, seleção em lote, idempotência, correções/estornos, contexto após edição e totais independentes da paginação.
- Verificar teclado, foco ao abrir/fechar painéis e diálogos, nomes acessíveis e uso por toque.
- Conferir desktop e celular com dados fictícios, inclusive listas grandes, nomes longos e mensagens de erro.
- Testar filtros/ordenação/paginação juntos, inclusive após edição e exclusão.
- Preservar arquitetura atual, namespaces C# com chaves, regras de cálculo, auditoria e padrões de teste.
- Não alterar registros financeiros para adequar o layout. Eventuais mudanças estruturais de API/banco devem ser identificadas explicitamente no escopo da entrega.
- Sem compras de bibliotecas, templates, serviços ou planos nesta migração.

## Como manter este plano

Ao concluir cada entrega, registrar data, arquivos/documentação relevantes, verificações realizadas e pendências. Só marcar concluído depois da implementação e validação. Decisões de paleta e biblioteca devem deixar explícito o que foi aprovado e o que continua proposto.

## Registro do primeiro marco — 20/10/2026

- Biblioteca Material/CDK instalada; tema claro centralizado em `src/material-theme.scss`, preparado com papéis semânticos. Sem seletor de modo escuro nesta entrega.
- Header e navegação em grafite, seleção azul e ícones vetoriais locais consistentes, sem carregar fonte de ícones externa. Ocultação de valores e navegação móvel preservadas.
- Carteira com tabela Material no desktop e linhas reorganizadas em cartões no celular. Valores investidos, atuais e variação continuam visíveis; preço médio, cotação unitária, proventos e câmbio podem ser consultados nos detalhes.
- Paginação reutilizável em português, com 10 itens por padrão e opções 25/50. Filtros e ordenação precedem a paginação; totais continuam da carteira inteira. A paginação da Carteira é local, pois sua resposta ainda inclui o resumo e as posições completas. Paginação de históricos na API pertence às próximas entregas.
- Edição e saldo inicial abrem sob demanda; ações auxiliares ficam em menu Material. Campos da Carteira e do lote usam Material com seletores nativos, preservando decimais brasileiros e precisão.
- A estrutura de diálogo nativo compartilhada foi preservada nesta transição, com fechamento Material e restauração de foco. Não há uma migração global de todos os diálogos, formulários ou navegação para novos componentes. Reavaliar essa estrutura ao migrar fluxos de correção, sem abrir overlays fora da camada modal.
- Consulta de cotações mantém todos os resultados filtrados ao abrir o painel, independentemente da página, e continua exigindo revisão/confirmação. Nenhuma alteração de API financeira, migration ou dado real.
- Textos explicativos da Carteira foram recolhidos; falhas e pendências relevantes permanecem visíveis.
- Validação: 97 testes da interface e 6 cenários de apresentação aprovados (desktop/celular, HTTP simulado). Inclui paginação, preservação de filtros e totais, foco, atualização de lote além da página visível, gráficos e simulador. Compilação de produção validada.

## Registro do segundo marco — 20/10/2026

- Movimentações com lista compacta e responsiva, busca por número/descrição/referência, filtros de tipo, status e período. Aplicar filtros retorna à primeira página; recarregar após uma operação preserva a página quando possível.
- Paginação real na API: `GET /api/portfolios/{id}/movements/page`, com 10/25/50 itens, total filtrado e ordenação por data decrescente, desempate por identificador decrescente. Uma página que deixou de existir é ajustada para a última válida.
- Auditoria carregada sob demanda por `GET /api/portfolios/{id}/movements/{movementId}`. O status de estornado permanece na lista; efeitos antes/depois, vínculos, bloqueios e ações ficam em Ver detalhes.
- Formulário de movimentação abre em painel modal. Análise, simulação, motivo de estorno e confirmação usam controles Material contornados, com rótulos visíveis. A estrutura nativa de diálogo foi preservada; seletores nativos evitam problemas com overlays fora da camada modal.
- Consultas anteriores são canceladas ao trocar página/filtros/carteira ou fechar detalhes. Os filtros em edição só passam a valer ao aplicar. Textos auxiliares de movimentação e estorno ficam recolhidos; impactos e bloqueios de correções permanecem explícitos.
- Endpoint integral anterior preservado para consumidores existentes. Algoritmos de correção continuam consultando o histórico completo; paginação não altera cálculos, estornos, idempotência ou transações. Não houve migration nem alteração na base real.
- Validação: 19 testes de integração de movimentações em banco isolado, 100 testes da interface, 2 cenários de navegador (desktop e celular, HTTP fictício) e compilação de produção aprovados. Capturas conferidas para rótulos, responsividade e auditoria.

## Registro do terceiro marco — 20/10/2026

- Compras e vendas separa consulta e registro. Compra, venda e evento societário abrem em painel sob demanda, com controles Material contornados e rótulos persistentes. Links da Carteira com ativo/tipo continuam abrindo o formulário correspondente.
- Busca por código/nome reduz as opções no seletor de ativos e preserva o ativo selecionado. Quantidade disponível, moeda, taxas, total estimado e origem/destino do dinheiro continuam acessíveis antes da confirmação.
- Histórico paginado na API por `GET /api/portfolios/{id}/trades/page`, com 10/25/50 itens, filtros de ativo, tipo e período e ordenação estável por data/identificador decrescentes. Filtros são aplicados antes da paginação e voltam à primeira página; recarregar preserva a página válida.
- Lista responsiva com quantidade e total; preço unitário, taxas, destino/origem e referência de auditoria nos detalhes. Operações estornadas são identificadas explicitamente, mesmo quando o estorno está fora da página. Eventos societários continuam consultáveis no histórico unificado.
- Ajuda de taxas, reinvestimento e efeitos na posição recolhida. Consequências dos eventos e regras de equivalente histórico continuam visíveis no formulário. Formatação brasileira, precisão dos valores enviados, idempotência e regras financeiras preservadas.
- Nenhuma migration nem modificação dos dados reais. O endpoint integral anterior permanece disponível para consumidores existentes.
- Validação: 102 testes da interface, 28 testes de integração de operações/movimentações em banco isolado, 2 cenários de navegador em desktop/celular e build de produção aprovados. A execução de integração usou o endereço do processo LocalDB ativo somente no ambiente do comando, sem mudar connection strings do projeto.

## Registro do quarto marco — 21/10/2026

- Proventos separado em Recebimentos e Análises, preservando o total acumulado da carteira. O formulário de novo recebimento abre sob demanda; links com ativo selecionado continuam abrindo esse formulário.
- Histórico paginado por `GET /api/portfolios/{id}/income/page`, com 10/25/50 itens, busca por ativo, período e filtro de conversões pendentes. Filtros são aplicados antes de paginar; ordenação por data e identificador decrescentes. A página é preservada ao recarregar quando válida.
- Pendência avaliada na moeda-base atual da carteira, independentemente da base original do recebimento. Recebimentos na mesma moeda não são pendentes. Conversões para outras moedas não ocultam uma pendência na base atual.
- Lista responsiva com valor original, equivalente e indicação de estorno; destino, observação, fonte/data de referência e versões ficam nos detalhes. Completar/revisar conversão usa formulário Material e preserva motivo obrigatório, versão esperada, precisão e idempotência.
- Filtros de análise independentes da lista. Os gráficos e totais históricos continuam usando a análise completa, sem somar somente a página visível. Conversões faltantes continuam indicadas, sem apresentar soma parcial como total.
- Biblioteca Material aplicada aos formulários, seletores e botões. Ajuda sobre gráficos e recebimentos antigos recolhida; consequências e pendências relevantes permanecem visíveis. Sem migrations ou alterações na base real.
- Validação: 103 testes da interface, 14 testes de integração de proventos em banco isolado, 4 cenários de navegador (fluxos de Proventos e gráficos em desktop/celular) e builds da API/interface aprovados. LocalDB foi iniciado para executar os testes; configuração do projeto preservada.

## Registro do quinto marco — 21/10/2026

- Evolução e fechamentos separado em Evolução, Fotografias e Aportes/retiradas. Período/moeda e agrupamento mensal/anual usam controles Material; metodologia e explicações complementares ficam recolhidas. Bloqueios de rentabilidade, lacunas e fotografias desatualizadas permanecem indicados.
- Fotografias com paginação local dos metadados mensais (10/25/50), ordenados do mais recente ao mais antigo. Os gráficos continuam considerando todos os períodos selecionados. A consulta detalhada carrega somente ao abrir e usa painel amplo, com seletor que permite ir às versões anteriores e retornar à atual.
- Registro/atualização de fotografia, reabertura e remoção de histórico preservam a confirmação e os impactos explícitos. Formulários Material abrem sob demanda, mantendo precisão monetária e motivo de reabertura. Consultas de detalhes são canceladas ao fechar ou trocar a carteira.
- Aportes/retiradas paginados na API por `GET /api/portfolios/{id}/history/cash-flows/page`, com filtros de ano/tipo, 10/25/50 itens e ordenação por data e identificador decrescentes. Uma página inexistente é ajustada à última válida. Registros ligados a operações/movimentações mantêm seus vínculos de consulta/estorno.
- A tela solicita `GET /history?includeCashFlows=false` para não transferir a lista integral. O valor padrão continua sendo `true` para consumidores existentes. Os cálculos de evolução e rentabilidade continuam usando todos os fluxos no servidor; a paginação não altera os resultados. Filtros e páginas válidas são preservados ao recarregar; consultas antigas são canceladas.
- Validação: 106 testes da interface, 12 testes de integração do histórico em base isolada, 4 cenários de navegador (histórico e gráficos em desktop/celular) e builds da API/interface aprovados. Capturas revisadas. O comando de integração utilizou o endereço do LocalDB ativo somente em seu ambiente, sem alterar configurações persistidas. Sem migrations ou alterações em dados reais.

## Registro do sexto marco — 21/10/2026

- Ativos, cadastros auxiliares (tipos, países, moedas, categorias e setores) e patrimônio externo usam formulários, seletores e botões Material. Criação e edição abrem em painel modal; exclusões mantêm confirmação e erros de vínculos visíveis.
- Paginação local reutilizável de 10/25/50 registros. A API continua fornecendo os cadastros completos para consulta e referências; não houve mudança nos endpoints. Busca/filtros e ordenação alfabética antecedem a paginação. Filtros e página válida são preservados após salvar; exclusão ajusta páginas vazias. Consultas anteriores são canceladas ao recarregar.
- Ativos com filtros específicos de categoria e setor, além da busca por código/nome/classificações. Classificações e data de cadastro consultadas sob demanda. Cadastros auxiliares mantêm busca sem distinção de acentos e regras específicas de moeda.
- Patrimônio externo com busca e filtro de moeda; descrição e referências cambiais nos detalhes. Total da carteira independente dos filtros e da paginação, com indisponibilidade e uso de câmbio anterior explícitos. Moeda original/base e ocultação de valores preservadas.
- Ajuste manual de saldo permanece em Movimentações, com auditoria. Edição de cadastro mantém saldo/moeda bloqueados; saldo inicial aceita decimais brasileiros sem perda de precisão. Troca de carteira limpa o contexto da lista e cancela consultas antigas.
- Validação: 111 testes da interface, 2 cenários de navegador (desktop/celular, HTTP fictício) e build de produção aprovados. Capturas revisadas. Sem alterações de API, migrations ou dados reais neste marco.

## Registro do sétimo marco — 21/10/2026

- Metas de categoria e setor com campos Material, busca e paginação local de 10/25/50. O total considera o conjunto inteiro; filtros e páginas não removem percentuais do payload nem da validação. Grupos em branco e regra de 100% preservados.
- Estratégia ativa resumida na tela do simulador. Configuração em painel sob demanda, com modelos existentes, busca/paginação das categorias, reservas, exclusões e tolerância. Fechar mantém os campos da sessão; salvar como padrão continua criando uma revisão. Configuração incompleta mantém aviso visível e impede simulação.
- Seletores nativos dentro do diálogo evitam sobreposição fora da camada modal, inclusive o tamanho da página de regras. Formulários, botões e marcações temporárias usam Material. Filtrar regras não altera os valores de outras categorias.
- Plano conjunto aparece antes das comparações para facilitar a execução dos aportes. Categorias e setores permanecem lado a lado no desktop e empilhados no celular, com gráficos da carteira completa. Ajuda de cálculo, modelos e marcações sob demanda; valores não distribuídos, conversões e avisos continuam explícitos.
- Preservados: cálculos no backend, precisão de percentuais, moedas originais/equivalentes, privacidade, revisões da estratégia e limpeza das marcas ao alterar aporte/configuração ou refazer simulação. Nenhuma operação financeira é registrada pelo simulador.
- Validação: 113 testes da interface, fluxos de metas e simulador em desktop/celular e build de produção. Sem mudanças de API, migrations ou dados reais.

## Registro do oitavo marco — 22/10/2026

- Dashboard com filtros de grupos, formato/tamanho dos gráficos, período e exibição de proventos em campos Material contornados. Botões Material e ícones locais para arrastar e mover; rótulos acessíveis mantidos.
- Patrimônio total destacado em azul discreto, alinhado à identidade aprovada. Explicações de metas e leitura dos gráficos sob demanda; alertas de câmbio, conversões pendentes e fotografias inválidas continuam visíveis.
- Organização por arraste ou setas, tamanhos, recolhimento, tipos de gráfico e preferências por carteira preservados. No celular, os blocos continuam empilhados e podem ser reordenados pelas setas. Tabelas de apoio e privacidade preservadas.
- Cálculos, consultas financeiras e dados reais não foram alterados. Validação: 113 testes da interface, cenários de apresentação em desktop/celular com HTTP fictício e compilação de produção. Capturas conferidas.

## Registro do nono marco — 22/10/2026

- Seletor compartilhado de carteira e confirmação de correção migrados para Material, preservando seleção, evento de confirmação e bloqueio durante aplicação. Atalho para o conteúdo também padronizado.
- Removidos estilos globais antigos de campos/botões, tokens de gradiente metálico sem uso, layout de duas colunas substituído e regras redundantes de navegação/dashboard. Campos Material passam a controlar sua geometria, sem resets compensatórios; rótulos globais não interferem nos rótulos Material.
- Foco visível em regiões roláveis e conteúdo principal. Novo teste real de teclado cobre atalho para conteúdo, abertura do menu, ciclo de foco móvel e fechamento/restauração com Escape. Diálogos nativos e barras interativas dos gráficos são mantidos intencionalmente.
- Linhas e barras do histórico alinhadas ao azul; valores negativos nos gráficos usam vermelho semântico. Paleta categórica continua distinguindo grupos e tabelas de apoio continuam disponíveis.
- Validação: 113 testes de interface, 20 cenários de navegador em desktop/celular com HTTP fictício e build de produção aprovados. Capturas de formulários conferidas. Nenhuma mudança de backend, migration ou dado real. Os avisos conhecidos sobre tamanho do logo no navegador e scrollTo no ambiente de teste não impedem os testes.

## Registro do décimo marco — 22/10/2026

- Menu “Escolher tema” no header com Claro, Escuro e Sistema. Sistema é o padrão inicial e acompanha mudanças do dispositivo. A preferência explícita é preservada em `investment-tracker.theme` no navegador; indisponibilidade de armazenamento não impede a troca durante a sessão.
- Tema Material escuro aplicado também aos menus e demais overlays. Superfícies, diálogos, formulários, tabelas, paginação, alertas, indicadores de variação e gráficos usam papéis compartilhados de cor. O logo original é preservado.
- Testes de apresentação passam a executar em quatro configurações: desktop/celular nos temas claro/escuro. Cenário específico verifica persistência após recarregar, escolha explícita, reação ao sistema e retorno de foco do menu.
- Sem alterações financeiras, migrations ou acesso à base real. A aplicação do tema ocorre na inicialização da interface; a preferência é local ao navegador.

Validação do décimo marco: 113 testes da interface e 44 cenários de navegador aprovados; capturas de dashboard, formulário e menu conferidas. Build de produção aprovado com aviso de tamanho inicial (520,68 kB, limite de alerta de 500 kB); o limite não foi aumentado.

As etapas planejadas de migração estão implementadas. Próxima atividade: receber e corrigir os problemas observados no uso real.


## Ajustes após uso — gráficos e botões

- Roscas com paleta fria de azuis, azul-petróleo, lavanda e neutros, com variações próprias por tema. Cor de cada grupo continua estável pelo identificador, inclusive ao filtrar ou ordenar.
- Botões habilitados com texto e contorno na cor de ação e peso 600. Removida a opacidade global adicional dos desabilitados; estes usam cores neutras específicas, inclusive nas ações principais e destrutivas.
- Build e oito cenários de apresentação de dashboard/carteira aprovados nos dois temas, em desktop e celular. Aviso de pacote inicial permanece (aproximadamente 522 kB).

## Rodada de usabilidade — encerramento em 23/10/2026

Os sete marcos do [plano de usabilidade e navegação](plano-usabilidade-e-navegacao.md) foram concluídos com revisão integrada e limitações explícitas: ações por registro, proteção de alterações, retorno ao contexto, filtros/escopo e faixa própria para organizar gráficos. Validação final: build aprovado, 118 testes de interface e 44 cenários de apresentação aprovados em desktop/celular e claro/escuro. Pacote inicial de 524,92 kB; aviso mantido sem aumentar budgets. Evidências finais em `docs/evidencias/usabilidade-marco-7/`.

Aparelho físico, teclado virtual, zoom real, prévia nativa do arraste e auditoria formal de acessibilidade permanecem fora da comprovação automatizada; os detalhes e limites de cobertura estão no registro do marco 7. Esta conclusão não altera o escopo financeiro nem representa preparação para publicação pública.
