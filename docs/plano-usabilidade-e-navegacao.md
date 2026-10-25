# Plano de ação — consistência, navegação e organização das telas

Data: 22/10/2026.
Status: marcos 1–7 concluídos em 23/10/2026, com revisão integrada, 118 testes de interface e 44 cenários de apresentação aprovados. Rodada encerrada com as limitações explícitas registradas no marco 7, incluindo validações físicas/nativas ainda pendentes.

## Objetivo e contexto

A migração para Angular Material e os temas claro/escuro estão registrados em [Entregas de layout](entregas-layout.md). Esta nova rodada trata da consistência de uso entre telas, da preservação de contexto e da organização das ações. Não substitui o histórico da migração.

Problemas relatados pelo usuário:

- Botões distribuídos de maneiras diferentes entre telas.
- Edição representada ora por lápis, ora por “Editar”, ora por texto com nome do registro.
- Setas de reorganização apertadas nos gráficos.
- Alça/prévia/sombra do arraste aparentemente desalinhada; causa ainda precisa ser diagnosticada.
- Preferência por Editar e Excluir no final à direita dos cadastros.
- Proximidade entre ações de atualização e exclusão a avaliar conforme seu significado.

Decisões da discussão: manter as setas; separar controles de organização dos controles de consulta; padronizar áreas de ação; preservar contexto ao navegar e salvar. “Recarregar dados”, “Buscar cotações” e “Salvar alterações” terão nomes e posições distintos. Exclusão não será aproximada de recarregamento somente por ambas serem ações.

O usuário autorizou os marcos 1–7 sequencialmente em 23/10/2026. O inventário abaixo preserva o levantamento inicial; os registros ao final distinguem o que foi aplicado, validado e o que permanece limitado. A conclusão desta rodada não declara aprovadas as verificações físicas/nativas não executadas.

## Regras permanentes

- Preservar cálculos, precisão decimal, formato brasileiro, moedas, auditoria, idempotência, confirmações financeiras e limites gratuitos de cotações.
- Não alterar dados reais ou criar movimentos ao navegar, filtrar, reorganizar ou marcar itens do simulador.
- Manter Material e os ícones locais; evitar outra biblioteca visual ou componentes genéricos sem necessidade concreta.
- Preservar tema, privacidade dos valores e preferências de organização existentes. Evoluções de formato devem manter compatibilidade ou oferecer recuperação segura.
- Validar desktop e celular, temas claro e escuro, teclado, foco e toque. Não depender exclusivamente de hover, cor ou arraste.
- Não persistir rascunhos financeiros, valores ou textos sensíveis em URL/localStorage por conveniência. Persistência de contexto não significa salvar formulários automaticamente.
- Não adicionar confirmação para alterações puramente visuais ou para fechar formulários sem mudanças.
- Manter C# com namespaces por chaves se alguma alteração de backend se tornar necessária; esta rodada tem foco na interface e não pressupõe mudanças financeiras ou migrations.

## Ordem e dependências

| Marco | Entrega | Prioridade | Complexidade | Dependência |
| --- | --- | --- | --- | --- |
| 1 | Inventário e padrão de ações | Essencial | Média | Nenhuma |
| 2 | Cadastros e ações por registro | Alta | Média | 1 |
| 3 | Formulários, confirmação e feedback | Alta | Alta | 1–2 |
| 4 | Contexto e navegação entre telas | Alta | Alta | 1–3 |
| 5 | Navegação interna, filtros e escopo | Alta | Média/alta | 1 e 4 |
| 6 | Organização dos gráficos e arraste | Alta | Alta | 1; integrar com 4–5 |
| 7 | Revisão integrada e documentação | Essencial | Média | 1–6 |

A ordem privilegia regras compartilhadas antes da aplicação em telas. Cada marco deve produzir uma entrega revisável e registrar validações e limitações. Não considerar um marco concluído apenas porque compila.

## Marco 1 — inventário e contrato visual de ações

### Escopo

Revisar Dashboard, Carteira/cotações, Compras e vendas/eventos, Proventos, Movimentações/correções, Evolução/fechamentos, Simulador/estratégias, Metas, Patrimônio externo, Ativos e cadastros auxiliares.

Para cada tela, mapear: tarefa principal, ações de página, filtros, ações por registro, ações destrutivas, edição/modal, estado de carregamento e destino/retorno dos links. Registrar divergências observadas, sem presumir que todos os problemas ocorrem em todas as páginas.

### Padrão proposto

| Contexto | Organização |
| --- | --- |
| Cabeçalho | Título à esquerda; ação principal à direita; secundárias agrupadas sem competir com a principal. |
| Filtros | Busca/seletores, Aplicar filtros quando necessário e Limpar filtros, em sequência estável. |
| Registro | Ações no final à direita; Editar antes de Excluir; separação visual da ação destrutiva. |
| Formulário | Cancelar e Salvar/Registrar no rodapé à direita, nessa ordem; exclusão fora desse par. |
| Gráfico em consulta | Título, opções de consulta e recolher/expandir. |
| Gráfico em organização | Faixa própria com alça, setas e tamanho; não comprimir esses controles no título. |

Vocabulário: “Novo ativo”, “Registrar compra”, “Registrar recebimento”, “Editar”, “Ver detalhes”, “Salvar alterações”, “Recarregar dados”, “Buscar cotações”, “Excluir”, “Estornar”. Não renomear estorno como exclusão. Se confirmar uma operação tiver efeito específico, usar esse efeito no verbo.

Usar ícone + “Editar” nas ações comuns. Nomes acessíveis podem identificar o registro sem alongar o texto visível. Controles só com ícone ficam restritos a ações compactas reconhecíveis, com nome acessível e ajuda por foco/toque. Não esconder ações frequentes apenas para economizar espaço.

### Aceite e validação

- Matriz de telas e ações registrada no documento, com exceções justificadas.
- Uma ação principal por contexto, sem confundir ação da página com ação de um registro.
- Ordem de ações consistente também no celular e por teclado.
- Escolher um cadastro representativo como referência antes de propagar o padrão.

## Marco 2 — cadastros e ações por registro

### Escopo e execução

Aplicar primeiro em Ativos e cadastros auxiliares, depois Patrimônio externo e ações de registro na Carteira. Alinhar áreas de ações à direita sem depender do comprimento do nome ou da quantidade de conteúdo. Em cartões, colocar no rodapé; em tabelas, usar coluna de ações. No celular, permitir uma linha própria sem sobrepor dados ou reduzir excessivamente alvos de toque.

Editar e Excluir devem manter ordem e aparência. Ações complementares raras podem ficar em menu; consultar, comprar, vender ou registrar proventos precisam preservar seu contexto e não serão agrupados automaticamente como simples edição.

Exclusões continuam confirmadas, identificando o registro e exibindo bloqueios por vínculo. Patrimônio externo mantém ajuste auditável de saldo separado da edição cadastral.

### Aceite e validação

- Nomes longos e textos de erro não deslocam ações para posições imprevisíveis.
- Sem rolagem horizontal da página em viewport estreita; tabelas podem ter região própria quando necessário.
- Após editar ou excluir, filtros e página válida permanecem; página vazia após exclusão é ajustada.
- Verificar registros com/sem permissão de exclusão, primeiro/último item, lista vazia e busca sem resultados.
- Confirmar que o payload e as regras cadastrais não mudaram por causa da apresentação.

## Marco 3 — formulários, confirmação e feedback

### Escopo e execução

Revisar os painéis de cadastro/edição e os fluxos de compras, vendas, proventos, movimentos, correções, fotografias e estratégias.

- Rodapé com Cancelar e ação de conclusão. Em formulários longos, avaliar rodapé fixo dentro do painel, garantindo que não cubra campos, erros ou conteúdo ao abrir o teclado móvel.
- Mostrar “Salvando…”, “Registrando…” ou “Consultando…” na ação em andamento. Bloquear duplicidade; não bloquear controles sem necessidade.
- Após falha, manter valores preenchidos e permitir nova tentativa conforme as regras atuais. Resultados incertos de operações financeiras devem seguir o tratamento idempotente existente.
- Após sucesso, fechar quando adequado, devolver foco ao acionador ou sucessor lógico e destacar brevemente o registro atualizado.
- Avisar ao fechar, trocar carteira ou navegar para fora de formulário com mudanças efetivas não salvas. Cancelar a saída preserva formulário e contexto; confirmar descarta explicitamente.
- Formulário intocado ou salvo não deve disparar aviso. Escape, botão fechar, Cancelar, links e voltar do navegador precisam de comportamento consistente. Para fechamento da aba, usar somente o mecanismo nativo suportado pelo navegador, sem prometer texto personalizado.

A detecção de alterações deve comparar estado relevante com o inicial: formatação monetária e carregamento automático não devem criar falsas mudanças. Preferências visuais e marcas temporárias não devem receber o mesmo bloqueio de um lançamento financeiro.

### Aceite e validação

- Não perder alterações sem aviso nos caminhos de saída previstos.
- Confirmar descarte e desistir do descarte funcionam sem modais empilhados ou foco perdido.
- Duplo clique não duplica operações; erro não apaga formulário; conclusão não deixa bloqueio de saída ativo.
- Foco permanece no diálogo aberto e retorna corretamente ao fechar; erros são anunciados e alcançáveis.
- Validar formulário curto, longo, inválido, preenchido, alterado e revertido ao valor inicial.

## Marco 4 — continuidade entre telas

### Escopo e execução

Preservar, por carteira e tela, filtros aplicados, ordenação, página, aba e posição de rolagem quando fizer sentido. Diferenciar filtros em edição dos efetivamente aplicados. Inventariar primeiro o que já é preservado para não duplicar mecanismos.

Definir política explícita de estado: parâmetros de URL para seleções compartilháveis não sensíveis; estado da navegação/sessão para retorno; preferências duráveis apenas quando forem escolhas visuais. Não colocar dados financeiros de formulário ou busca sensível na URL. Carteira selecionada deve continuar coerente entre as páginas.

Links contextuais, como Carteira → Compra/Provento, devem levar carteira e ativo e oferecer retorno à origem. O retorno deve restaurar contexto depois que os dados e o item estiverem disponíveis, sem rolar para posição inexistente. Acesso direto sem origem deve ter destino padrão seguro.

Troca de carteira invalida estados incompatíveis e cancela consultas antigas. Remoção de ativo, mudança na lista e alteração do número de páginas devem ter fallback previsível. Voltar/avançar do navegador precisam funcionar sem ciclos de navegação.

### Aceite e validação

- Sair de uma lista filtrada na página 2, consultar outra tela e voltar restaura contexto compatível.
- Compra/provento aberto por ativo mantém seleção; acesso direto funciona sem estado anterior.
- Trocar carteira não reaproveita ativo, filtro específico ou resposta assíncrona de outra carteira.
- Recarregar dados não dispara consulta externa de cotação e não cria operações.
- Identificar no documento quais estados sobrevivem a navegação, recarga e fechamento do navegador.

## Marco 5 — navegação dentro da página, filtros e escopo

### Escopo e execução

Revisar páginas longas e densas, especialmente Carteira, Movimentações, Proventos, Evolução/fechamentos, Simulador e Dashboard.

- Manter abas existentes quando representam tarefas distintas; usar links internos para seções da mesma tarefa. Não criar abas apenas para esconder volume de texto.
- Identificar título, carteira atual e item ativo do menu de forma consistente.
- Resumir filtros ativos e quantidade de resultados; oferecer Limpar filtros sem apagar outras preferências.
- Diferenciar carregamento, ausência de cadastro, ausência de resultado e falha de consulta. Explicar a próxima ação possível em cada caso.
- Identificar totais da carteira inteira versus resultados filtrados. Gráficos e resumos não devem passar a somar apenas a página visível.
- Buscar cotações informa quantidade/escopo antes de executar e preserva o conjunto capturado para revisão; paginação não muda silenciosamente esse conjunto.
- Manter ajuda complementar recolhida; falhas, pendências cambiais e consequências financeiras continuam visíveis.
- Fixar apenas elementos necessários: header para navegação e, quando útil, rodapé de formulário. Evitar empilhar barras fixas que consumam a tela do celular.

### Aceite e validação

- Usuário identifica em qual carteira/tarefa está e o que uma ação afetará.
- Filtro sem resultado oferece recuperação; limpar filtros restaura o conjunto esperado.
- Paginação, totais globais e escopo de atualização permanecem corretos.
- Mudança de aba não descarta edição silenciosamente; retorno respeita o marco 4.
- Verificar tela estreita, zoom de 200%, conteúdo longo e teclado móvel quando disponível; registrar limitações da emulação.

## Marco 6 — reorganização dos gráficos com setas preservadas

### Escopo e execução

Diagnosticar o desalinhamento observando a alça, o elemento arrastável, a imagem de arraste e o destino. Verificar diferenças entre borda, padding, tamanho do cartão e coordenadas do ponteiro antes de escolher a correção.

Criar faixa exclusiva de organização visível somente nesse modo:

- Alça de arraste com área de interação definida.
- Botões Subir e Descer, mantidos explicitamente e com espaço suficiente.
- Controle de tamanho em posição consistente.
- No celular, faixa em linha própria; setas são alternativa completa ao arraste.

Preservar tamanhos compacto/médio/amplo, recolhimento e preferências por carteira. Definir ordem linear como referência das setas e da leitura por teclado; grade visual respeita essa ordem mesmo com cartões de tamanhos distintos. Não prometer uma troca geométrica de célula que contradiga a ordem salva.

Primeiro item desabilita Subir; último desabilita Descer. Ao mover, manter foco no controle do mesmo gráfico, anunciar sua nova posição e trazê-lo à vista apenas quando necessário. Arrastar não deve selecionar texto nem acionar filtros/recolhimento acidentalmente.

A prévia deve acompanhar o ponteiro sem deslocamento inesperado, e o destino deve ser distinguível da sombra. Cancelamento do arraste não altera a ordem. Restaurar organização padrão mantém comportamento documentado e não altera dados financeiros.

### Aceite e validação

- Setas acessíveis por teclado e toque, sem sobreposição com título, formato ou tamanho.
- Ordem persistida após recarga e compatível com preferências anteriores.
- Foco permanece no gráfico movido; anúncio acessível informa o resultado.
- Validar todos os tamanhos, recolhidos/expandidos, primeiro/último gráfico, rolagem da página e menu lateral aberto/fechado.
- Conferir visualmente alça, prévia e destino em desktop; em celular, comprovar reorganização completa pelas setas.
- Nenhuma requisição financeira ou mudança de valores ao reorganizar.

## Marco 7 — revisão integrada e encerramento

### Execução

Percorrer jornadas completas com dados fictícios, cobrindo os marcos anteriores sem depender da base real:

1. Consultar carteira filtrada → abrir registro → editar → salvar → continuar no mesmo contexto.
2. Abrir compra ou recebimento a partir de ativo → conferir contexto → cancelar/registrar → retornar.
3. Alterar formulário → tentar sair → desistir do descarte → concluir; repetir confirmando descarte.
4. Filtrar histórico → paginar → consultar auditoria → voltar → trocar carteira.
5. Simular aporte → consultar outra tela → retornar conforme política das marcas temporárias, sem transformar marcação em lançamento.
6. Organizar Dashboard por setas e arraste → alterar tamanho/recolher → recarregar → confirmar preferências.

### Critérios de encerramento

- Critérios de cada marco atendidos ou limitação explícita registrada; não encerrar com regressão conhecida de dados, contexto ou navegação.
- Build de produção e testes relevantes aprovados. Executar testes financeiros/backend apenas se comportamento ou contrato correspondente for alterado.
- Capturas de telas críticas conferidas em temas claro/escuro e desktop/celular, com revisão de foco, rótulos e estados habilitado/desabilitado.
- Privacidade também preservada em menus, detalhes, gráficos e mensagens.
- Não aumentar limites de pacote apenas para esconder avisos. Registrar impacto de dependências e tamanho inicial, hoje com aviso conhecido próximo de 522 kB.
- Atualizar este documento e documentação de uso afetada. Não realizar commit automaticamente.

## Fora do escopo desta rodada

Novas estratégias, métricas financeiras, provedores pagos, automação de backup, base de demonstração, mudanças no modelo financeiro e redesenho da identidade aprovada. Otimização ampla de desempenho pode ser planejada separadamente; regressões introduzidas pela própria rodada devem ser resolvidas nela.

## Acompanhamento

| Marco | Status | Evidência de conclusão |
| --- | --- | --- |
| 1 — Inventário e padrão | Concluído em 23/10/2026 | Inventário, contrato, capturas revisadas e 44 cenários de apresentação aprovados; limites registrados abaixo |
| 2 — Cadastros | Concluído em 23/10/2026 | Ações por registro padronizadas; testes, build e apresentação aprovados; registro abaixo |
| 3 — Formulários e feedback | Implementado em 23/10/2026 | Proteção de rascunhos, rodapés, processamento e foco; validações e limites abaixo |
| 4 — Contexto entre telas | Implementado em 23/10/2026 | Contexto em memória por carteira/tela, links contextuais e retorno; 117 testes e 44 cenários aprovados |
| 5 — Navegação interna | Implementado em 23/10/2026 | Filtros aplicados, escopo, estados e atalhos internos; validação registrada abaixo |
| 6 — Organização dos gráficos | Implementado em 23/10/2026 | Faixa própria, setas, foco e prévia explícita; 118 testes e 44 cenários aprovados; limites no registro |
| 7 — Revisão integrada | Concluído em 23/10/2026 com limitações registradas | Seis jornadas revisadas, privacidade ampliada nos testes, 118 testes e 44 cenários aprovados; evidências e documentação atualizadas |

Ao concluir cada marco, registrar data, telas/arquivos envolvidos, decisões e exceções, testes executados, evidências visuais, limitações e próximo marco. Novos problemas relatados pelo usuário entram no marco pertinente, com seu impacto indicado, sem apagar o histórico das decisões.

## Registro do marco 1 — 23/10/2026

### Contexto, método e limites

Especificação inicial de 21/08/2026 lida como contexto externo ao repositório (`C:/Users/felip/Downloads/especificacao.md`), junto do README e dos planos atuais. Dela permanecem relevantes a apresentação mobile-first, consulta dos dados também por tabelas e cálculos fora do Angular. O desenho inicial do MVP não substitui as entregas posteriores registradas no projeto. Nenhuma instrução do arquivo externo foi tratada como nova autorização de execução.

Inventário feito nos templates, componentes e estilos atuais, incluindo alterações locais já existentes. Raiz das evidências: `frontend/InvestmentTracker.Web/src/app/`. Rotas conferidas em `app.routes.ts`; cadastros auxiliares em `features/catalogs/catalog.models.ts`. Não houve acesso à base real, execução de operações, alteração de frontend/backend ou commit nesta etapa.

As tabelas descrevem evidências estáticas. Alinhamento efetivo, contraste, toque, sequência real de foco e causa do desalinhamento do arraste exigem inspeção no navegador. Não foram considerados aprovados por leitura do código.

### Matriz de telas e ações observadas

| Tela / evidência em `features/` | Tarefa e ações de página atuais | Filtros / consulta | Ações por registro, edição e destrutivas | Carregamento e links / retorno |
| --- | --- | --- | --- | --- |
| Dashboard — `dashboard/dashboard-page.html`, `.ts`, `.css` | Consultar patrimônio; Atualizar dashboard; Organizar dashboard / Concluir organização; restaurar padrão | Grupos, período, formato; Limpar filtros; organização mistura alça, setas, tamanho e consulta no cabeçalho dos blocos | Recolher/expandir, mover e redimensionar; sem exclusão financeira | `loading`; links para Carteira, Proventos, Metas, Simulador, Evolução e Patrimônio externo. Preferências de gráficos salvas por carteira; retorno à origem não explicitado nesses links |
| Carteira — `portfolio/portfolio-page.html`, `.ts` | Recarregar dados; Mais ações para criar/editar carteira e navegar; registrar compra/venda; cadastrar saldo inicial | Busca/classificações/ordenação, paginação, moeda de exibição; abrir lote com todos os resultados filtrados | Detalhes e edição por ícone na tabela; nos detalhes, Comprar, Vender, Editar com ticker e remoção. Formulários e confirmação em painel | `loading`/`saving`; links de compra/venda levam `assetId` e `kind`; não incluem origem de retorno. Salvar antecede Cancelar nos formulários |
| Lote de cotações — `portfolio/quote-batch-panel.ts` | Consultar cotações gratuitas; Revisar; Confirmar atualização do lote | Conjunto capturado; preços manuais e informações de fonte/horário | Voltar à edição e Cancelar; descarte de campos preenchidos tem tratamento próprio | `fetching`/`saving`, comunicação incerta e nova tentativa tratados. Consulta externa, revisão e gravação são etapas distintas; preservar esse fluxo |
| Compras/vendas/eventos — `trades/trades-page.html`, `.ts` | Registrar compra como principal; venda e evento secundários; Atualizar saldos e histórico | Ativo, tipo e período; Filtrar / Limpar filtros; paginação | Detalhes no histórico; evento e compra/venda em painéis; estorno encaminhado a Movimentações | `loading`/`saving`, texto Registrando na operação; lê ativo/tipo da URL. Links para carteira, movimentos e evolução são genéricos; rodapés não têm par Cancelar/conclusão uniforme |
| Proventos — `income/income-page.html`, `.ts` | Novo recebimento; Atualizar recebimentos; Recebimentos / Análises compartilham faixa com ação de registro | Histórico por ativo/período/conversão; análise com filtros próprios; paginação | Detalhes, registro e revisão de conversão; estorno em Movimentações | `loading`/`saving`; lê ativo da URL para abrir formulário. Links genéricos para carteira, movimentos e evolução; preservar separação dos totais de análise e lista |
| Movimentações/correções — `movements/movements-page.html`, `.ts`, `correction-panel.html` | Movimentar saldo; Registrar operação atrasada; Analisar lançamento atrasado; Atualizar movimentações | Busca, tipo, status, período; Filtrar / Limpar filtros; paginação | Ver detalhes; Estornar, Analisar correção e Corrigir conforme elegibilidade; painéis de diagnóstico/execução/confirmacão | Carregamento de página, detalhe e análise separados; `saving`; estorno tem motivo e confirmação. Links para operações, proventos, evolução e patrimônio externo; não padronizar estorno como edição/exclusão |
| Evolução/fechamentos — `history/history-page.html`, `.ts` | Atualizar histórico; Evolução / Fotografias / Aportes e retiradas; registrar/atualizar fotografia; reabrir período; registrar histórico antigo | Ano, moeda, mês/ano, intervalo; ano/tipo dos fluxos; paginação de fotos e fluxos | Detalhes e versões de fotografias; editar/remover fluxo avulso; vínculos de operações levam à consulta/estorno correspondente | `loading`/`saving`, erro de fluxos e Tentar novamente; confirmações específicas de fotografia/reabertura/remoção. Links para movimentos/operações são genéricos, mesmo com identificador no texto |
| Simulador — `allocation/contributions-page.html`, `.ts` | Simular aporte; Atualizar carteira e limpar simulação; Configurar estratégia | Valor do aporte; resultados por categoria/setor; marcações temporárias | Marcar investido sem lançamento; sem Editar/Excluir por resultado | `loading`/`saving`, Calculando; links para metas e operações. Atualizar tem efeito adicional explícito de limpar; marcas são limpas também ao sair, conforme ajuda atual |
| Estratégias — `allocation/strategy-editor.ts` | Configurar estratégia; salvar como padrão; Concluir configuração | Modelo/configuração salva; busca/paginação de categorias nas regras | Editar regras na sessão e salvar revisão; fechar mantém campos da sessão | `loading`/`saving`, erros/aviso; painel amplo. Concluir configuração não equivale a salvar revisão nem registrar investimento |
| Metas — `allocation/targets-page.html`, `.ts` | Salvar metas do contexto; alternar categoria/setor | Busca/paginação dos grupos; total e payload abrangem todos os grupos | Edição percentual na própria página, sem exclusão por linha | `loading`/`saving`, Tentar novamente; link para Simulador. Troca de dimensão precisa entrar na revisão de alterações não salvas |
| Ativos — `assets/assets-page.html`, `.ts` | Novo ativo; Atualizar na seção de registros | Busca, categoria e setor; Limpar filtros; paginação local | Editar e Excluir no rodapé dos cartões; nomes acessíveis por registro; formulários e confirmação em painel | `loading`/`saving`, Salvando e mensagens de resultado; referências faltantes levam ao cadastro auxiliar. Salvar ativo vem antes de Cancelar; edição ainda sem ícone local junto do texto |
| Tipos, Países, Moedas, Categorias e Setores — `catalogs/catalog-page.html`, `.ts` | Novo cadastro; Atualizar dentro da lista | Busca imediata e paginação local; contagem de resultados; não há botão Limpar busca | Editar e Excluir na última coluna, rótulos acessíveis; moeda inclui código/símbolo; confirmação informa bloqueio por uso | `loading`/`saving`, Salvando, sucesso/erro; sem navegação contextual própria. Salvar cadastro vem antes de Cancelar edição |
| Patrimônio externo — `external-assets/external-assets-page.html`, `.ts` | Novo patrimônio e Atualizar na mesma faixa | Busca/moeda, exibição original/base, paginação; total integral separado | Editar e Remover com nome visível em botões dentro dos cartões; editar não altera saldo/moeda; confirmação em painel | `loading`/`saving`; links para Movimentações e Dashboard. Salvar vem antes de Cancelar; ação de saldo permanece fora da edição cadastral |

### Divergências e encaminhamento

| Evidência concreta | Decisão / marco responsável |
| --- | --- |
| Releitura dos dados aparece como Atualizar, Atualizar dashboard, Atualizar histórico, Atualizar recebimentos e Atualizar movimentações | Adotar Recarregar dados para releitura; preservar texto que explicita efeito adicional no simulador. Aplicar nos marcos 2 e 5 |
| Carteira combina lápis sem texto e Editar com ticker; patrimônio externo inclui nome completo; cadastros usam Editar textual | Editar com ícone local e texto visível estável; nome do registro no rótulo acessível. Marco 2 |
| Ações de cartões externos usam `.actions`; o estilo compartilhado alinha `.card-footer`, que esses cartões não usam | Aplicar região de rodapé explícita e alinhamento previsível; conferir nomes longos e celular. Marco 2 |
| Salvar precede Cancelar em cadastros/Carteira/histórico; diversos painéis financeiros dependem do fechamento no topo | Ordem Cancelar → conclusão no DOM e no visual. Marco 3; não corrigir só com CSS `order` |
| `shared/edit-panel.ts` bloqueia fechamento quando ocupado e restaura foco, mas não compara alterações do formulário | Aproveitar mecanismo existente e inventariar saída com alterações; lote de cotações já tem proteção própria. Marco 3, sem duplicar confirmações |
| `PortfolioSelection` mantém seleção em serviço; links de operação aceitam ativo/tipo; filtros/páginas são sinais dos componentes | Reutilizar seleção e preservar apenas contexto seguro; não confundir manutenção ao recarregar dados com restauração após destruir a página. Marco 4 |
| Organização dos gráficos e consulta compartilham cabeçalho; setas são compactas | Separar faixa de organização; manter setas. Causa do deslocamento de arraste ainda não determinada. Marco 6 |
| Proventos mistura navegação Recebimentos/Análises com ação Novo recebimento; ações de página ocupam regiões distintas entre telas | Navegação de seções e comandos devem ter áreas distintas. Marcos 2 e 5 |

### Contrato de ações para aplicação

1. Cabeçalho: título e identificação da tarefa à esquerda; ação principal à direita; secundárias ao lado com menor destaque. Carteira selecionada fica em região de contexto anterior aos filtros. Páginas de consulta podem não ter comando principal; não promover Recarregar dados só para preencher esse lugar.
2. Filtros: campos em ordem de leitura, Aplicar filtros quando a consulta for explícita, depois Limpar filtros. Em busca imediata, não criar Aplicar artificialmente. Limpar atua apenas nos filtros daquele contexto. Consulta externa de cotações fica fora da faixa de filtros, em área própria com escopo informado.
3. Registros: coluna final à direita em tabelas; rodapé à direita em cartões. Editar com ícone local + texto, seguido de Excluir com separação e aparência destrutiva. Nome longo fica no conteúdo e no nome acessível, sem aumentar o texto do botão. Ver detalhes antecede edição quando houver; comprar/vender/receber têm grupo contextual próprio.
4. Formulários: Cancelar seguido de Salvar alterações na edição, ou verbo específico de criação/registro. A ação de conclusão é a única principal dentro do painel; no processamento, usa Salvando…, Registrando… ou Consultando…, conforme o efeito. Exclusão fica fora desse par. Confirmação destrutiva: Cancelar, depois Excluir/Estornar/Reabrir período, com consequência e alvo visíveis.
5. Gráficos: consulta contém título, seletores e recolher/expandir. Organização contém alça, Subir, Descer e tamanho, em faixa própria; na tela estreita, linha exclusiva. Ordem linear é a referência da leitura e persistência. Primeiro/último desabilitam os movimentos impossíveis.
6. Celular: preservar sequência de ações; permitir quebra de linha ou faixa própria, sem sobreposição ou compressão dos alvos. A ação principal do cabeçalho pode ocupar a linha seguinte. Tabelas extensas rolam apenas em sua região. Não esconder Editar/Excluir em hover.
7. Teclado e foco: ordem visual deve acompanhar o DOM, sem `tabindex` positivo nem inversão por CSS; filtros precedem lista e paginação; ações do registro seguem a ordem acima. Ao fechar painel, devolver foco ao acionador ou sucessor válido; ao mover gráfico, mantê-lo no controle do mesmo gráfico e anunciar posição. Ícones isolados exigem nome acessível e ajuda também por foco/toque.

Vocabulário de referência: Novo ativo; Novo tipo de ativo; Novo país; Nova moeda; Nova categoria; Novo setor; Novo patrimônio; Registrar compra; Registrar venda; Registrar recebimento; Registrar evento societário; Ver detalhes; Editar; Salvar alterações; Recarregar dados; Buscar cotações; Excluir; Estornar. Na criação, usar Salvar cadastro/Salvar ativo quando adequado. Revisão e confirmação do lote continuam explícitas: Buscar cotações → Revisar cotações → Confirmar atualização do lote.

### Cadastro de referência e exceções

**Ativos será a referência do marco 2**: reúne busca, dois filtros, nomes longos, classificação, paginação, edição modal e exclusão bloqueável por vínculos. O resultado será aplicado primeiro ali e conferido antes da propagação aos cinco cadastros auxiliares. Moedas será a conferência adicional por possuir código e símbolo.

Esboço do alvo, ainda sem implementação:

```text
Ativos                                      [Novo ativo]
[Busca] [Categoria] [Setor] [Limpar filtros]
Registros / contagem                    [Recarregar dados]
Código | Nome | Classificação |            Ações
...    | ...  | ...           | [ícone Editar]  [Excluir]
Paginação

Painel de edição: campos ...   [Cancelar] [Salvar alterações]
```

Exceções justificadas:

- Metas têm salvamento coletivo; não criar Editar/Excluir por linha nem salvar somente a página visível.
- Estratégia distingue Concluir configuração na sessão de Salvar como padrão com revisão. Não trocar ambos por Salvar alterações sem distinguir seus efeitos.
- Simular aporte permanece a principal do simulador; marcas e organização visual não recebem confirmação financeira. A política de limpeza atual precisa ficar explícita ao preservar contexto.
- Atualizar fotografia grava uma versão, Reabrir período tem consequências históricas e Estornar preserva auditoria. Não são Recarregar dados ou Excluir.
- Remover posição/patrimônio exige conferir a semântica e os bloqueios antes de substituir o verbo por Excluir. O padrão visual não altera o contrato financeiro.
- Novo/editar carteira pode permanecer em Mais ações; a tarefa diária da Carteira não é administrar seus metadados.
- Diálogo nativo compartilhado e seletores nativos dentro dele são intencionais; este contrato não exige substituição por outra biblioteca/modal.

### Validação, pendências e próximo passo

- Conferidos estaticamente: cobertura das rotas atuais, cinco cadastros auxiliares, ações de página/registro, filtros, painéis, estados e destinos de links nas evidências acima.
- Registrados: contrato, vocabulário, cadastro de referência, exceções e encaminhamento por marco.
- Conferência complementar concluída abaixo: capturas de desktop/celular nos dois temas e suíte de apresentação com teclado. O marco 1 encerra o levantamento; a conformidade das telas com o contrato será validada durante sua aplicação.
- Build de produção não repetido: somente documentação e evidências alteradas. A suíte de apresentação foi executada na continuação abaixo. O aviso de pacote próximo de 522 kB continua no histórico, sem mudança de limite.
- Próximo marco: aplicação inicial em Ativos, seguida de Moedas e demais cadastros. Os marcos 2–7 não foram iniciados por este registro.

### Continuação — conferência visual e teclado em 23/10/2026

Executada a suíte existente `e2e/presentation.spec.ts`: **44 cenários aprovados em 1,2 minuto**, nas configurações desktop (1440 × 1000), Pixel 7 emulado, desktop escuro e celular escuro. Todas as requisições `/api/**` foram interceptadas com respostas fictícias; não foi iniciado backend nem utilizado banco real.

A primeira execução não avançou na inicialização e a porta 65455 ficou ocupada. A execução concluída utilizou cópia temporária da configuração na porta 65456, sem mudar cenários ou expectativas, com `NG_CLI_ANALYTICS=false`. A configuração temporária foi removida e a execução anterior interrompida. Comando da execução concluída: `npx playwright test --config=playwright.audit.config.ts` (cópia de `playwright.presentation.config.ts`, alterando somente a porta).

Cobertura relevante: filtro/paginação e salvamento no mesmo contexto em Ativos; bloqueio de exclusão por vínculo em Setores; saldo/moeda preservados na edição externa; totais independentes dos filtros; Escape e restauração de foco em painéis; atalho ao conteúdo; abertura/fechamento do menu por teclado; ciclo de foco e isolamento do menu móvel; persistência dos temas e preferências dos gráficos. Os testes de teclado não constituem auditoria completa de acessibilidade nem de toda a sequência Tab de cada formulário.

Capturas revisadas e preservadas em `docs/evidencias/usabilidade-marco-1/`:

| Evidência | Observação / encaminhamento |
| --- | --- |
| [Editor de Ativos desktop claro](evidencias/usabilidade-marco-1/ativo-editor-desktop.png) e [escuro](evidencias/usabilidade-marco-1/ativo-editor-desktop-escuro.png) | Campos e foco visíveis; formulário coloca Salvar antes de Cancelar e alinha o par à esquerda. Alterar ordem e posição no marco 3. Ações do cartão ao fundo já ficam à direita, mas sem ícone junto de Editar: marco 2 |
| [Editor de Ativos celular claro](evidencias/usabilidade-marco-1/ativo-editor-celular.png) e [escuro](evidencias/usabilidade-marco-1/ativo-editor-celular-escuro.png) | Painel cabe na largura emulada, com campos empilhados e ações visíveis. A mesma ordem divergente permanece; conferir teclado virtual e formulário longo no marco 3 |
| [Patrimônio externo desktop](evidencias/usabilidade-marco-1/patrimonio-desktop.png) e [celular](evidencias/usabilidade-marco-1/patrimonio-celular.png) | Editar/Remover usam o nome do registro e ficam à esquerda; no celular quebram em linhas. Aplicar rodapé e rótulos estáveis no marco 2. Total integral continua separado da lista filtrada |

Também conferida a captura de Dashboard gerada pela suíte: recarga com rótulo Atualizar dashboard e controles de consulta nos cabeçalhos. O teste confirma reorganização por arraste no desktop e por setas no celular, mas não comprova alinhamento da prévia com o ponteiro; diagnóstico reservado ao marco 6. Capturas de página inteira com header fixo podem mostrar a barra na posição correspondente à rolagem no momento da captura; isso não basta para atribuir sobreposição ao layout em uso.

Limitações para as entregas seguintes: sem aparelho físico, teclado virtual ou zoom real de 200% nesta execução; sem conferência visual dedicada de cada cadastro auxiliar (compartilham template); sem medição formal de contraste. Nomes longos, erros extensos, ordem Tab de formulários e foco após mover gráficos devem ser verificados ao aplicar os respectivos marcos. Aviso Angular conhecido de logo com resolução maior que o tamanho exibido continua presente.

Encerramento do marco 1: matriz e contrato registrados, Ativos escolhido como referência, divergências confirmadas por amostra visual e navegação por teclado exercitada. Nenhuma conformidade futura foi antecipada: a interface ainda precisa receber os ajustes dos marcos seguintes. Sem alteração de código de produto, dados ou commit.

## Registro do marco 2 — 23/10/2026

- Ativos como referência: Editar com ícone local e texto, seguido de Excluir; rodapé à direita e alinhado ao fim do cartão independentemente do conteúdo. Cinco cadastros auxiliares usam o mesmo padrão na última coluna, alinhada à direita, com quebra das ações em tela estreita e rolagem restrita à tabela.
- Patrimônio externo: rodapé próprio à direita, botões com texto estável Editar/Remover e nome do item no rótulo acessível. Mantido Remover, conforme a exceção semântica do marco 1; saldo e moeda continuam fora da edição cadastral.
- Carteira: lápis isolado substituído por ícone + Editar. Nos detalhes, Comprar/Vender têm faixa contextual separada do rodapé Editar/Remover. Nenhum novo comando de exclusão foi introduzido na tabela.
- Separação das destrutivas por espaço, texto e cor semântica Material; estados desabilitados continuam com os papéis próprios. Alvos das ações cadastrais com altura mínima de 44 px. Releitura nos cadastros e patrimônio passa a Recarregar dados.
- Arquivos envolvidos: templates/componentes de `features/assets`, `features/catalogs`, `features/external-assets`, template/CSS de `features/portfolio`, `shared/registration-list.css`, estilos globais, tema Material e cenário de apresentação. Sem alteração de métodos de gravação, payloads, cálculos, auditoria ou regras de bloqueio.
- Validação: 113 testes de interface aprovados; 12 cenários de apresentação (cadastros, carteira e teclado × quatro configurações) aprovados. Após ajustes finais de cor/CSS, repetidos os oito cenários de cadastros/carteira. Cenário ampliado para conferir edição de moeda, Escape/restauração de foco e capturas das listas. Build de produção aprovado, mantendo aviso inicial de 522,53 kB; sem aumento de limites ou aviso de CSS de componente.
- Evidências: `docs/evidencias/usabilidade-marco-2/`, com listas de Ativos, Moedas e Patrimônio externo em desktop/celular claro/escuro. Conferidas amostras de alinhamento, quebra de ações e rótulos; no celular, a tabela de Moedas pode rolar internamente. Testes usam API interceptada e dados fictícios.
- Preservados filtros/página após edição, bloqueio por vínculo, precisão e total integral externo. Limites: sem aparelho físico; não houve nova matriz visual exaustiva de todos os comprimentos de nomes/erros nem de todas as combinações de exclusão. Quebra de nomes e ajuste de página válida continuam usando os mecanismos existentes, cobertos parcialmente pelas suítes atuais.
- Próximo marco: formulários, confirmação e feedback. A ordem Cancelar/Salvar dos formulários não foi alterada nesta entrega. Sem backend, migrations, dados reais ou commit.

## Registro do marco 3 — 23/10/2026

### Entrega

- Rodapés de cadastros, carteira, operações, recebimentos/conversões e histórico usam Cancelar antes da conclusão, à direita, acompanhando a ordem do DOM. Incluído Cancelar em formulários que dependiam apenas do fechamento no topo. Confirmações de exclusão/fotografia também colocam Cancelar primeiro. Estratégia mantém Concluir configuração, pois fechar preserva seus campos na sessão, e salvar como padrão tem efeito distinto.
- Textos de processamento para salvar posição/carteira/conversão/histórico, registrar evento, reabrir período e estornar. Mantidos bloqueios de envio duplicado e identificadores de solicitação existentes, sem mudar regras financeiras nem o tratamento de comunicação incerta.
- `shared/unsaved-changes.ts` compara os formulários reativos com a referência inicial, em memória. Considera valores revertidos e equivalências de formatação decimal nos campos monetários; carregamento/reset de formulário intocado atualiza a referência. Salvar metas marca o formulário como salvo e libera a proteção.
- `shared/unsaved-registry.ts` centraliza a confirmação nativa de descarte para fechamento de painel, Cancelar, navegação pelo Router (incluindo voltar/avançar) e troca de carteira. Metas também protege a troca de dimensão. Recusar a saída mantém o formulário. Durante processamento, a saída é bloqueada. Nenhum rascunho é salvo na URL ou no armazenamento do navegador.
- A rolagem do conteúdo só volta ao início após `NavigationEnd`, não ao clicar em um link cuja saída pode ser recusada. A restauração de posições de origem permanece no marco 4.
- `shared/draft-state.ts` cobre editores por Signals: motivo de estorno, estratégia e lote de cotações. A estratégia registra alterações para saída da página/troca de carteira, mas concluir/fechar configuração mantém os campos da sessão. O lote preserva sua confirmação própria de fechamento e a revisão/idempotência existentes; o registro compartilhado protege a navegação externa ao painel.
- Fechar a aba usa apenas `beforeunload` nativo, sujeito às regras do navegador. Não há texto personalizado garantido. Filtros, organização visual e marcas do simulador não são rascunhos financeiros.
- Painel compartilhado continua devolvendo foco ao acionador; se ele saiu do DOM, usa o conteúdo principal como alternativa. Erros do painel recebem foco e anúncio. Ativos e cadastros auxiliares têm destaque breve do registro editado; animação respeita redução de movimento. A Carteira mantém seu destaque existente.

### Validação e evidências

- Build de produção aprovado; pacote inicial de 523,23 kB, com aviso de 500 kB já existente, sem alterar limites. A proteção foi separada das dependências de formulários para não levar o módulo reativo ao carregamento inicial das rotas.
- 116 testes de interface aprovados, incluindo três cenários novos de comparação/reversão monetária, descarte recusado/aceito e bloqueio durante salvamento/liberação após salvar.
- 44 cenários de apresentação aprovados em desktop/celular e temas claro/escuro. Cenário de Ativos exercita Escape com descarte recusado e confirma que o texto preenchido permanece. Os demais cenários aceitam diálogos de descarte para continuar suas jornadas existentes. Após os ajustes finais de lote/rodapés/destaque, 16 cenários afetados foram repetidos e aprovados.
- O teste de fotografia que clicava no primeiro botão foi ajustado para identificar a ação principal, pois o primeiro agora é Cancelar. Conferência visual do editor de Ativos preservada em `docs/evidencias/usabilidade-marco-3/`, nas quatro configurações.
- Validação exclusivamente com HTTP fictício; sem backend, migrations, uso de base real ou commit. Avisos conhecidos de logo e APIs nativas não implementadas no jsdom permanecem sem impedir as verificações.

### Decisões e limites

- Rodapé continua no fluxo do painel, sem fixação adicional: evita cobrir campos, mensagens e conteúdo em tela estreita. Teclado físico/virtual em aparelho real não foi conferido; emulação não comprova seu comportamento.
- A confirmação de descarte usa o diálogo nativo do navegador; não empilha outro diálogo Angular sobre o painel existente. Seu aspecto visual é controlado pelo navegador.
- As suítes não comprovam cada combinação de voltar/avançar, erro financeiro incerto ou troca de carteira com cada editor aberto. A revisão integrada deve ampliar essas jornadas; proteção é compartilhada, mas cobertura de navegador dedicada nesta entrega é a recusa de descarte em Ativos. Destaque visual específico do patrimônio externo e dos lançamentos financeiros continua limitado às mensagens de sucesso atuais.
- Próximo marco: preservar/restaurar contexto entre telas. A proteção de saída entregue aqui não implementa persistência de filtros, página ou rolagem após destruir um componente.

## Registro do marco 4 — 23/10/2026

### Contexto e política de estado

`shared/page-context.ts` mantém uma lista explícita dos estados de consulta de cada tela. Os contextos são separados por tela/carteira; Ativos e cadastros auxiliares são globais, com chave própria por cadastro. Nenhum componente nem formulário financeiro é mantido vivo para recuperar uma lista.

| Estado | Navegação na mesma sessão | Recarregar a página | Fechar o navegador |
| --- | --- | --- | --- |
| Filtros, busca, ordenação, página/tamanho, seção e rolagem registrados abaixo | Restaurados por tela/carteira; dados são consultados novamente | Reiniciados | Reiniciados |
| Filtros dos históricos ainda em edição | Mantidos separadamente dos aplicados, apenas em memória | Reiniciados | Reiniciados |
| Carteira selecionada | Serviço compartilhado existente; links contextuais podem selecionar uma carteira válida explicitamente | Usa `portfolioId` do link, se válido; caso contrário, padrão seguro | Não há nova preferência durável de carteira |
| `portfolioId`, `assetId` e tipo de compra/venda em links | URL identifica a seleção; não contém valores ou texto de formulário | Continuam no link; ativo inexistente volta à seleção com mensagem | Apenas se o próprio navegador reabrir o endereço |
| Tema, privacidade e organização dos gráficos | Mecanismos anteriores preservados | Preferências anteriores preservadas conforme seus mecanismos | Nenhuma mudança nessa política |
| Rascunhos financeiros, configuração não salva e resultado/marcas do simulador | Não entram no contexto; proteção de saída segue o marco 3. Simulador mantém a limpeza ao sair | Não recuperados | Não recuperados |

Não foi introduzido `localStorage` ou `sessionStorage` para buscas ou rascunhos. A escolha por memória evita manter textos sensíveis depois da recarga; parâmetros de URL ficam limitados às seleções dos links existentes e ao identificador da carteira.

### Aplicação nas telas

- Carteira: busca/classificações/moeda/período de atualização/ordenação, modo de valores, tamanho e índice de página. Ativos: busca, categoria/setor e paginação. Cadastros: busca e paginação por tipo. Patrimônio externo: busca, moeda, modo de exibição e paginação por carteira.
- Compras/vendas e Movimentações: filtros aplicados, filtros em edição e paginação. Proventos acrescenta Recebimentos/Análises e filtros da análise. Histórico preserva seção, agrupamento, ano/moeda/período de composição e filtros/paginação dos fluxos e fotografias. Metas preserva dimensão, busca e paginação, sem percentuais editados. Dashboard preserva busca/status/ordenação; organização continua no mecanismo durável existente.
- Captura antes da desmontagem no guard de navegação, somente depois de a proteção de alterações permitir a saída. A restauração aguarda os carregamentos da tela e o próximo render; limita a posição de rolagem ao tamanho atual do documento. A restauração nativa de rolagem foi colocada em modo manual para não disputar a posição com a aplicação.
- Troca de carteira salva o contexto anterior e recupera somente o da carteira de destino, ou padrões para uma carteira ainda não consultada. Página local é limitada ao conjunto atual; históricos mantêm o ajuste de página válida da API. Consultas e detalhes antigos continuam usando os cancelamentos existentes.
- Seletores nativos com opções carregadas depois do estado agora refletem explicitamente a opção selecionada, para não mostrar campo vazio com filtro ainda aplicado.
- Carteira → Comprar/Vender/Registrar recebimento leva `portfolioId` e `assetId`. Compra/provento oferece Voltar à Carteira com a carteira atual. Sem origem, Carteira é o destino seguro. O ativo do link é consumido na abertura inicial; trocar de carteira não reabre o formulário com o ativo anterior. Ativo que deixou de existir é limpo e gera mensagem para escolher outro.
- Recarregar dados continua apenas consultando os endpoints existentes. O contexto não consulta provedores de cotação nem cria operações. Não há backend, migrations ou alteração de dados reais nesta entrega.

### Validação e limites

- Teste unitário novo desmonta/recria o contexto, verifica separação por carteira e comprova que um campo de rascunho financeiro não é recuperado. Teste de Metas ajustado para o comportamento deliberado de carteira nova iniciar na dimensão padrão, cancelando a consulta da anterior.
- Cenários de navegador ampliados: lista de Ativos filtrada/página 2 → Moedas → voltar restaura filtro, página e rolagem; Carteira/página 2 → compra/recebimento com ativo → cancelar → retornar; troca de carteira sem reaproveitar o ativo e recuperação da página da primeira carteira. APIs inteiramente fictícias.
- Limites: recuperação é da tela de consulta, sem reabrir detalhes/painéis que já haviam sido fechados. Não persiste rascunhos nem resultados do simulador. Subcomponentes analíticos do Dashboard mantêm seus próprios controles e não foram incluídos integralmente no contexto desta rodada. Sem aparelho físico; emulação não verifica teclado virtual nem restauração de abas pelo navegador.
- Próximo marco: navegação interna, filtros e escopo. Capturas e resultados finais da execução registrados na conclusão desta entrega.

Conclusão da validação do marco 4: **117 testes de interface e 44 cenários de apresentação aprovados**, incluindo os percursos ampliados nos quatro projetos (desktop/celular claro/escuro). Build de produção aprovado com pacote inicial de 524,54 kB; aviso conhecido de 500 kB mantido, sem aumentar limites. Capturas finais da Carteira preservadas em `docs/evidencias/usabilidade-marco-4/`; verificações de contexto constam nas asserções dos cenários. A configuração temporária de apresentação usou porta 65456 e foi removida. Sem commit.

## Registro do marco 5 — 23/10/2026

### Navegação e organização

- Carteira e Dashboard oferecem atalhos para posições/filtros e análises, respectivamente. Simulador oferece Aporte, Plano (quando disponível) e Comparações. `shared/section-link.ts` move foco e rolagem ao destino sem mudar URL, destruir a tela ou gerar operação; modificadores de abertura de link continuam disponíveis. Não foram criadas abas para esconder conteúdo de uma mesma tarefa.
- Proventos mantém Recebimentos/Análises, agora em uma faixa de navegação separada de Registrar recebimento. Evolução mantém suas três visões distintas. A identificação da carteira pelo seletor e o item ativo do menu foram preservados.
- Releitura em Dashboard, Movimentações, Compras/vendas, Proventos e Histórico passa a Recarregar dados. O simulador mantém o rótulo que explicita a limpeza da simulação, pois essa ação tem efeito adicional.

### Filtros, estados e escopo

- `shared/filter-summary.ts` exibe os filtros efetivamente aplicados em Compras/vendas, Movimentações e Recebimentos, incluindo datas em formato brasileiro. Quando os campos em edição divergem da consulta, informa que é necessário Aplicar filtros. Limpar filtros aplicados recupera o conjunto sem apagar outras preferências.
- Históricos exibem a quantidade do conjunto filtrado e deixam explícito que a paginação muda somente os registros mostrados. Proventos distingue total acumulado da carteira inteira, histórico paginado e análise histórica filtrada. Histórico informa que gráficos/métricas consideram o período completo, não apenas a página de registros.
- Ausência de registros sem filtros oferece a ação de registro; busca sem resultado orienta limpar filtros. Falhas nas listas oferecem Tentar novamente, separado do carregamento. A análise de Proventos tem limpeza própria de busca/ano/moeda, preservando modo de valores e agrupamento. Aportes/retiradas tem resumo de ano/tipo/quantidade e limpeza de seus filtros.
- Carteira continua mostrando contagem e filtros ativos, com mensagem de recuperação quando não há posições correspondentes. Dashboard mantém o denominador da carteira inteira e as contagens dos grupos nos gráficos. Simulador identifica as comparações da carteira inteira; reservas, base elegível, valores não distribuídos e pendências continuam visíveis.
- Lote de cotações informa antes de executar: quantidade capturada pelos filtros, quantidade com posição positiva e número de campos vazios que serão enviados. Buscar cotações é distinto de Recarregar dados e da confirmação de gravação. A consulta usa os mesmos identificadores exibidos no escopo e considera o limite de 200 campos vazios; posições já preenchidas e zeradas não são consultadas.
- Seleção capturada e revisão foram preservadas. Não há cálculo baseado somente na página visível nem alteração de regra financeira, endpoints ou limites dos provedores. O rodapé do lote deixa de ser fixo no celular para não cobrir campos com controles/mensagens extensos. Nenhuma nova barra fixa foi adicionada.

### Validação e limites

- Cenário de Movimentações verifica que digitar filtro não altera o resumo aplicado até Aplicar filtros. Carteira testa navegação interna por teclado, foco no título, URL sem fragmento e ausência de rolagem horizontal da página em tela estreita. O teste de menu foi delimitado ao menu lateral, pois agora existem outras regiões de navegação.
- Lote tem conferência da quantidade anunciada antes da chamada externa e teste de que campo manual preenchido não entra na solicitação. Suítes seguem com APIs fictícias; não houve acesso à base real ou chamadas reais a provedores.
- Em desktop, a verificação adicional usa viewport de 720 × 500, equivalente em espaço disponível a 1440 × 1000 com zoom de 200%. É uma verificação de reflow, não comprova zoom real do navegador. Tabelas podem rolar em sua própria região. Temas claro/escuro e celular são conferidos na suíte de apresentação; aparelho físico/teclado virtual continuam fora da emulação.
- README e instruções de cotações atualizados com Buscar cotações. Próximo marco: organização dos gráficos e diagnóstico do arraste. Sem migrations, alteração de dados reais ou commit.

Conclusão da validação: 117 testes de interface e **44 cenários de apresentação aprovados** em desktop/celular e claro/escuro. Build de produção aprovado com pacote inicial de 524,92 kB; aviso existente mantido sem aumento de limites. Capturas revisadas de Proventos e navegação interna da Carteira preservadas em `docs/evidencias/usabilidade-marco-5/`. A configuração temporária da suíte (porta 65456) foi removida. Zoom real de 200% e teclado virtual permanecem como limitações explícitas, sem serem declarados validados pela emulação.

## Registro do marco 6 — 23/10/2026

### Diagnóstico e entrega

- Antes da alteração, alça, tamanho e setas dividiam o cabeçalho com título, formato e recolhimento. A captura [antes no desktop](evidencias/usabilidade-marco-6/antes-desktop.png) confirma a disposição apertada. Foram coletados retângulos da alça, cabeçalho e cartão, além de padding, borda e rolagem no cenário de apresentação.
- No código, o elemento arrastável era somente o botão da alça, enquanto a classe de opacidade era aplicada ao cartão inteiro. Não havia imagem nem coordenadas explícitas em `setDragImage`. Isso deixava a representação do arraste a cargo do navegador; não comprova, por si só, um desvio específico causado pelo padding. A prévia nativa do sistema operacional não aparece na captura da página, portanto não foi declarado um deslocamento medido dessa imagem.
- Criada faixa exclusiva de organização antes do cabeçalho: posição na ordem, alça Material de 48 px, Subir, Descer e Tamanho. Só aparece no modo de organização. Título, Formato e Recolher/Expandir permanecem na faixa de consulta. No celular, setas ocupam linha própria e tamanho fica abaixo; a alça continua oculta.
- A prévia agora contém somente o título do gráfico, com largura definida, cores do tema e borda. `setDragImage` recebe coordenadas relativas ao ponto pressionado dentro da alça, limitadas às dimensões da prévia; não usa o canto/padding do cartão como origem. A imagem auxiliar é removida após a captura pelo navegador e também no encerramento/destruição. O cartão de origem fica translúcido; o destino tem contorno tracejado e texto Soltar aqui.
- Setas e arraste preservam a ordem linear existente. A ajuda explica que as setas seguem a leitura e não prometem troca geométrica de células. Não há preenchimento denso da grade que reordene a leitura visual. Primeiro Subir e último Descer ficam desabilitados.
- Após mover, o foco permanece no controle do mesmo gráfico, com anúncio da nova posição. Se a seta utilizada se tornou desabilitada na extremidade, o foco vai para a outra seta desse gráfico. A rolagem só é solicitada quando o controle fica fora da área visível. Esc e encerramento do arraste limpam os indicadores; cancelar mantém a ordem salva.
- Tamanhos compacto/médio/amplo, recolhimento, formatos, restauração padrão e armazenamento `investment-tracker.dashboard.v1.<carteira>` foram preservados. No modo de organização, as alturas reservam espaço para a nova faixa, inclusive quando recolhido; ao concluir, volta a geometria de consulta. Não houve alteração de cálculos, payloads financeiros, backend ou dados reais.

### Validação e evidências

- Build de produção aprovado, pacote inicial de 524,92 kB. Permanece apenas o aviso conhecido do limite inicial de 500 kB; sem aumentar limites nem aviso de CSS de componente.
- **118 testes de interface aprovados**. O teste novo confere a imagem de título, coordenadas relativas à alça, limpeza da prévia, destino e cancelamento sem persistir nova ordem. Testes existentes cobrem tamanhos, compatibilidade com preferências antigas/corrompidas e isolamento por carteira. O teste de ambiente usa `matchMedia` simulado, pois jsdom não fornece essa API.
- **44 cenários de apresentação aprovados** em desktop/celular e temas claro/escuro. Cenário do Dashboard ampliado para conferir faixa separada, primeiro/último controles, cancelamento com Esc no desktop, arraste de cartão recolhido, foco nas setas do celular e persistência de tamanho/recolhimento após recarga.
- Após ampliar a conferência dos três tamanhos no mesmo gráfico e das setas por teclado com menu lateral recolhido, os **quatro cenários de gráficos foram repetidos e aprovados**. Asserção geométrica verifica que a faixa de organização termina antes do cabeçalho. Todos os endpoints são interceptados com dados fictícios; as interações visuais não chamam gravações financeiras.
- Capturas revisadas: [desktop claro](evidencias/usabilidade-marco-6/depois-desktop.png), [desktop escuro](evidencias/usabilidade-marco-6/depois-desktop-escuro.png), [celular claro](evidencias/usabilidade-marco-6/depois-celular.png), [celular escuro](evidencias/usabilidade-marco-6/depois-celular-escuro.png), menu recolhido [claro](evidencias/usabilidade-marco-6/menu-recolhido-desktop.png) e [escuro](evidencias/usabilidade-marco-6/menu-recolhido-desktop-escuro.png). Evidências em `docs/evidencias/usabilidade-marco-6/`.
- Configuração temporária Playwright usou a porta 65456 e foi removida. Avisos conhecidos de logo e APIs nativas ausentes no jsdom permanecem. Sem commit.

### Limitações e próximo marco

A automação comprova as coordenadas fornecidas à API de prévia e o resultado funcional de arrastar/cancelar; não comprova a composição visual da imagem nativa sob o ponteiro em cada navegador/sistema operacional. Essa conferência visual continua pendente, assim como toque em aparelho físico. Pixel 7 é emulado; reordenação por setas e foco foram validados nesse ambiente. O menu móvel aberto isola o conteúdo, portanto organizar exige fechá-lo, conforme o comportamento existente.

Marco 6 implementado com essas limitações explícitas. Próximo marco: revisão integrada das jornadas e documentação (7).

## Registro do marco 7 — 23/10/2026

### Revisão integrada

Ampliados os cenários existentes de `e2e/presentation.spec.ts`, mantendo dados fictícios e interceptação de todos os endpoints. Esta entrega altera testes e documentação; não adiciona dependências, muda contratos financeiros ou modifica código do produto. Alterações locais de outras entregas foram preservadas, sem commit.

| Jornada prevista | Resultado conferido |
| --- | --- |
| Carteira filtrada → editar → salvar → continuar | Busca TEST e página 2 preservadas após salvar a posição TEST10. Quantidade brasileira 12,345678 enviada como string decimal 12.345678; valor atual original mantido. Registro atualizado destacado e painel fechado. Ativos também mantém categoria/página após salvar e ao ir a Moedas e voltar |
| Posição → compra/recebimento → cancelar → retornar | Ativo TEST10 e carteira chegam ao formulário; cancelar e Voltar à Carteira recuperam a página 2. Trocar carteira não reaproveita o ativo da anterior. Jornada cobre cancelamento; não registra compra/provento real |
| Formulário alterado → recusar descarte → salvar; repetir aceitando descarte | Ativos mantém o nome preenchido ao recusar Escape e permite salvar. Nova edição é cancelada com descarte aceito; reabrir confirma o último nome salvo, sem recuperar o texto descartado |
| Histórico filtrado → paginar → auditoria → voltar → trocar carteira | Movimentações aplica busca, acessa a página 2 e abre a auditoria do lançamento #13. Após ir a Moedas e voltar, recupera busca/página. Carteira 2 inicia sem filtro; retornar à primeira recupera seu contexto. Filtros em edição/aplicados continuam distintos |
| Simular → marcar investido → sair → retornar | Ida a Moedas e retorno limpam resultado, marca e valor do aporte, conforme a política de estado temporário. A lista de chamadas de escrita não aumenta nessa navegação; nenhuma marca vira operação. O POST de cálculo da simulação continua sendo consulta de cálculo |
| Organizar Dashboard → tamanho/recolher → recarregar | Arraste e cancelamento no desktop; setas no celular e por teclado com menu desktop recolhido; três tamanhos, extremidades, foco e preferências após recarga. Mantida a compatibilidade por carteira coberta nos testes unitários |

Privacidade ampliada: detalhes da posição e da auditoria mascaram valores monetários; menu Mais ações não mostra os valores de posição. A mensagem de posição salva não inclui montantes. Gráficos, totais e plano do simulador mantêm as verificações existentes. Quantidades e percentuais seguem a política anterior, distinta da ocultação monetária; os campos de edição continuam visíveis.

O teste de privacidade foi ajustado para ativá-la antes de abrir o diálogo, pois o modal bloqueia o header corretamente. O teste de organização agora aguarda a devolução do foco ao botão do menu antes de começar outra sequência de teclado, além de confirmar a nova ordem antes de usar a seta seguinte. As falhas dessas primeiras execuções não foram contabilizadas como aprovação; a execução final fica registrada na conclusão abaixo.

### Evidências e documentação

Capturas preservadas em `docs/evidencias/usabilidade-marco-7/`, com seis amostras nas quatro configurações (desktop 1440 × 1000 e Pixel 7 emulado, claro/escuro): contexto da posição salva, detalhes privados da posição, auditoria privada, retorno ao simulador, editor de Ativos e faixa de organização. Conferidas amostras de alinhamento, quebra dos botões, distinção de ações, foco visível e valores mascarados.

| Amostra | Claro | Escuro |
| --- | --- | --- |
| Posição salva no contexto desktop | [Captura](evidencias/usabilidade-marco-7/portfolio-saved-context-desktop.png) | [Captura](evidencias/usabilidade-marco-7/portfolio-saved-context-desktop-escuro.png) |
| Detalhes privados no celular | [Captura](evidencias/usabilidade-marco-7/portfolio-private-details-celular.png) | [Captura](evidencias/usabilidade-marco-7/portfolio-private-details-celular-escuro.png) |
| Auditoria privada no celular | [Captura](evidencias/usabilidade-marco-7/movement-private-audit-celular.png) | [Captura](evidencias/usabilidade-marco-7/movement-private-audit-celular-escuro.png) |
| Retorno ao simulador no celular | [Captura](evidencias/usabilidade-marco-7/simulation-return-celular.png) | [Captura](evidencias/usabilidade-marco-7/simulation-return-celular-escuro.png) |
| Formulário de referência desktop | [Captura](evidencias/usabilidade-marco-7/asset-editor-desktop.png) | [Captura](evidencias/usabilidade-marco-7/asset-editor-desktop-escuro.png) |

README atualizado com contexto da sessão, links de operação, descarte, marcas temporárias, organização do Dashboard e distinção entre apresentação fictícia e E2E com API. Corrigida também a descrição antiga de migração parcial do Dashboard. A tabela de acompanhamento deste plano passa a refletir as entregas; os registros anteriores permanecem históricos.

### Limites que permanecem

- Sem aparelho físico, teclado virtual, zoom real de 200% ou conferência visual da prévia nativa de arraste no sistema operacional. O reflow em viewport reduzida, as coordenadas da API de prévia e as ações de navegador estão cobertos, mas não substituem essas verificações.
- Sem auditoria formal de contraste/leitor de tela nem verificação exaustiva de toda sequência Tab, nomes/erros longos, falhas de rede e combinação de rascunho com voltar/avançar em cada formulário. A cobertura de descarte no navegador usa Ativos e cancelamentos de outros painéis; comparação monetária e bloqueio durante processamento têm testes unitários compartilhados.
- Contexto é em memória, sem reabrir painéis/detalhes fechados. Subcomponentes analíticos do Dashboard não recuperam integralmente seus filtros ao sair; essa limitação do marco 4 permanece. Destaques específicos de patrimônio externo/lançamentos continuam limitados ao feedback existente.
- Avisos conhecidos de logo maior que o tamanho exibido e APIs nativas ausentes no jsdom permanecem. Otimização ampla de pacote/logo tem escopo próprio. Não foi aumentada a configuração de budgets.
- Não foram executados testes com API/banco real nem testes financeiros/backend nesta entrega, pois comportamento e contratos correspondentes não mudaram. As validações de interface não substituem a suíte financeira ou testes com provedores reais.

### Conclusão

- `npm run build`: aprovado; pacote inicial de **524,92 kB** (transferência estimada de 131,14 kB), sem mudança em relação ao marco 6. Aviso de 500 kB mantido, sem ampliar budgets. Nenhuma dependência adicionada no marco 7; os impactos anteriores permanecem registrados por entrega.
- `npm test -- --watch=false`: **25 arquivos e 118 testes aprovados**.
- `npx playwright test --config=playwright.audit.config.ts`: **44 cenários aprovados em 1,6 minuto**, com todos os ajustes finais de teste. Configuração temporária equivalente à de apresentação, alterando somente a porta 65455 para 65456; removida após a execução.
- 24 capturas finais preservadas e amostras revisadas nas quatro configurações. Nenhum backend iniciado, dado real acessado, provedor consultado ou commit realizado nesta entrega.

Marco 7 concluído e rodada de usabilidade encerrada com as limitações acima. O acompanhamento posterior deve validar aparelho físico, teclado virtual, zoom real e prévia nativa do arraste, além de registrar problemas encontrados no uso. Não há marco 8 definido por este plano; novas funcionalidades e infraestrutura continuam na lista própria de próximas etapas.
