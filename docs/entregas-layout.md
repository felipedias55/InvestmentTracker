# Migração da interface e entregas de layout

Atualizado em 20/10/2026.

## Objetivo e situação

Padronizar componentes, reduzir rolagem, facilitar consulta e edição e apresentar explicações sob demanda. Preservar registros financeiros, cálculos, auditoria e funcionalidades existentes.

Este documento registra o plano discutido com o usuário. Nenhuma entrega abaixo está concluída por ter sido documentada. A solicitação atual autoriza registrar o plano; não iniciar a migração.

Direção de trabalho da última proposta: Angular Material como biblioteca de componentes, com tema próprio, e Carbon como referência de organização de telas densas em dados. O usuário havia escolhido inicialmente customização do Carbon e depois concordou em migrar a biblioteca durante a comparação com Material. A escolha técnica de Angular Material foi explicitada na proposta seguinte; validar essa base no início da implementação, sem tratar a adoção de dois conjuntos de componentes como objetivo.

As cores douradas e prateadas atuais foram questionadas pelo usuário. A nova paleta é uma proposta pendente de validação, não uma decisão aprovada. Preservar o logo fornecido.

## Entregas em ordem

Todas estão pendentes. A prioridade indica importância; a ordem também considera dependências e reutilização.

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

## Identidade e proposta de cores — ainda não aprovada

Recomendação: superfícies neutras, texto grafite e azul discreto para ações. Preservar dourado e prata no logo e em detalhes pontuais, sem gradientes metálicos em botões, tabelas ou grandes áreas.

Paleta inicial para prototipação, não tema aplicado:

| Papel | Tema claro proposto |
| --- | --- |
| Fundo | Cinza frio muito claro `#F5F7FA` |
| Superfície | Branco `#FFFFFF` |
| Texto principal | Grafite `#20252D` |
| Texto secundário | Cinza `#526071` |
| Ação principal | Azul discreto `#315E99` |
| Identidade pontual | Dourado fosco `#B69A55` |
| Separadores | Cinza claro `#DDE3EB` |

Alternativa mais próxima da marca: grafite, branco e dourado muito pontual, com ações principais em grafite. Evitar dourado como cor universal de texto, foco e seleção.

O prata pode ser representado pelos neutros e divisórias, sem efeito metálico. Verde/vermelho continuam semânticos para variação e estados, acompanhados de texto/ícone. Não reutilizar dourado de marca como único indicador de aviso.

Os códigos são referências para discussão; validar contraste em cada combinação e estado. Ao adotar Material, mapear a paleta para os papéis do tema, sem sobrescritas isoladas por página.

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
