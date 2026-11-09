# Proposta: cadastro de investimentos e identidade visual dos ativos

Revisão inicial em 03/11/2026, incluindo a solicitação de imagens para ações, FIIs e REITs. Este documento entrega a análise do modelo existente e uma proposta de implementação. As etapas 1 e 2 foram implementadas em 03/11/2026: veja [cadastros e imagens](cadastros-e-imagens.md). As etapas 3 e 4 também foram implementadas: veja [cadastro assistido, cobertura e saldos](cadastro-assistido-e-saldos.md). A cobertura depende dos dados liberados por cada fonte.

## Conclusão da revisão

O principal problema é o esforço de preencher e manter informações em telas separadas. A proposta é centralizar o fluxo em **Ativos**, tornar as classificações e referências acessórias a esse fluxo e oferecer preenchimento assistido com revisão. Os IDs e as responsabilidades financeiras das entidades podem ser preservados.

Não foi encontrada equivalência suficiente para fundir tipos, categorias e setores: hoje eles participam de decisões diferentes. País e moeda também são independentes. Patrimônio Externo representa saldos/valores associados à carteira e pode financiar operações, enquanto Ativo é o instrumento compartilhado entre carteiras.

## O que fazer com cada cadastro

| Cadastro atual | Responsabilidade encontrada | Tratamento proposto |
| --- | --- | --- |
| Ativos | Identifica o instrumento e reúne cinco referências obrigatórias. | Ser a entrada principal de cadastro, com busca por ticker, revisão dos dados sugeridos e identidade visual. |
| Tipos de Ativo | Define o instrumento; `InstrumentClass` participa da elegibilidade de aportes e da compatibilidade FII/REIT. | Manter a entidade. Apresentar como **Tipo de instrumento**, com explicação curta e edição acessível no fluxo de classificações. |
| Categorias | Agrupamento escolhido para a estratégia; possui metas e participa de reservas, exclusões, prioridades e limites. | Manter separado do tipo, com rótulo **Categoria da estratégia** e exemplos. A escolha continua sendo do usuário. |
| Setores | Segmento econômico; possui metas próprias e marcação imobiliária usada pelo planejamento. | Manter a entidade. Sugerir correspondências de fontes externas, mas exigir revisão quando houver ambiguidade. |
| Países | Classificação geográfica do ativo. | Tratar como referência pronta para selecionar; manutenção secundária. Não inferir moeda ou mercado apenas pelo país. |
| Moedas | Unidade monetária do instrumento e das operações. | Tratar como referência pronta para selecionar, preservando manutenção excepcional e as restrições atuais de alteração. |
| Patrimônio Externo | Valor por carteira e moeda, também usado como saldo de origem/destino em operações. | Manter independente de Ativos. Revisar o texto para esclarecer **Saldos e patrimônio externo**, distinguindo seu papel nas operações e no patrimônio total. |

O seed atual já inclui Brasil, Estados Unidos, BRL, USD, tipos e classificações iniciais. Isso não significa que toda instalação tenha executado o seed: o fluxo deve tratar referências ausentes. A carga complementar proposta deve ser idempotente, preservar cadastros personalizados e não renomear ou reclassificar registros existentes com base apenas no nome.

## Fluxo proposto para um novo ativo

1. Abrir **Ativos → Novo ativo** e escolher **Buscar por ticker** ou **Preencher manualmente**.
2. Na busca, selecionar explicitamente mercado e ticker. Exibir resultados com nome, ticker, moeda, identificação do mercado, imagem disponível e fonte. Não escolher automaticamente um resultado ambíguo.
3. Abrir o editor Material existente com os dados sugeridos. Identificação reúne ticker, nome, mercado e imagem; classificações reúnem tipo de instrumento, categoria da estratégia, setor, país e moeda.
4. O usuário revisa as sugestões. Dados ausentes ficam visivelmente pendentes; categorias não são preenchidas silenciosamente com uma decisão de estratégia.
5. Se faltar uma classificação, permitir cadastrá-la em um painel contextual e retornar ao ativo com o formulário preservado. Evitar navegação que obrigue a reiniciar o cadastro.
6. Salvar somente o ativo confirmado. Adicioná-lo à carteira ou registrar uma compra continuam sendo ações explícitas dos fluxos existentes.

Ao editar um ativo existente, buscar dados deve apresentar diferenças por campo e permitir escolher quais aplicar. Campos confirmados, imagens personalizadas e classificações próprias não serão substituídos automaticamente. A restrição atual que impede trocar a moeda de um ativo com posições permanece aplicável.

## Organização das telas e navegação

Proposta para reduzir a dispersão no menu:

- **Ativos**: catálogo principal, busca, criação/edição e identidade visual.
- **Classificações**: acesso conjunto a Tipos de instrumento, Categorias da estratégia e Setores, preservando busca, paginação e edição próprias.
- **Referências**: Países e Moedas, como manutenção secundária.
- **Saldos e patrimônio externo**: continua junto das funções financeiras da carteira.

As rotas atuais devem continuar funcionando, inclusive links de páginas e testes existentes. A reorganização visual pode ser entregue sem renomear tabelas nem fundir registros. Novos painéis contextuais reutilizam os serviços e validações dos catálogos, sem criar um segundo caminho de regras no frontend.

## Quais dados podem ser sugeridos

| Informação | Proposta de preenchimento | Confirmação necessária |
| --- | --- | --- |
| Ticker e nome | Sugestão do resultado da busca, com mercado e fonte. | Selecionar o instrumento correto; manter edição permitida pelo domínio. |
| Moeda | Sugestão do instrumento consultado. | Conferir compatibilidade; nunca deduzir apenas de país ou categoria. |
| País | Sugestão quando a fonte definir o significado de seu campo geográfico. | Distinguir país do emissor de mercado de negociação; divergências ficam pendentes. |
| Tipo de instrumento | Mapear códigos documentados da fonte para os tipos estruturados. | Não deduzir FII/REIT por texto solto; casos não mapeados precisam de escolha. |
| Setor | Propor correspondência com o catálogo existente. | Não criar setores duplicados a partir de rótulos externos ou alterar a marcação imobiliária silenciosamente. |
| Categoria da estratégia | Seleção manual, podendo oferecer a última escolha como conveniência explicitamente identificada. | Sempre decisão do usuário, sem equiparar categoria a tipo ou país. |
| Imagem | Sugerir um logotipo identificado pela fonte e ligado ao instrumento selecionado. | Permitir substituir, remover e preservar personalizações. Ausência não bloqueia cadastro. |

## Imagens: padrão e locais escolhidos

Implementar um componente compartilhado de identificação, com imagem à esquerda, nome ao lado e ticker em informação secundária. Proposta inicial de moldura: 32 × 32 px em listas e 40 × 40 px no cabeçalho do editor/detalhes, com o mesmo formato de cantos e espaçamento. Usar enquadramento que preserve a proporção do logotipo, fundo neutro legível nos dois temas e espaço reservado durante o carregamento.

Locais da primeira implementação:

1. Nome do ativo na lista de posições da carteira.
2. Nome do ativo nos cartões do catálogo de Ativos e no cabeçalho de sua edição.
3. Resultados da busca assistida por ticker, quando esse fluxo estiver disponível.

Os seletores nativos atuais não suportam imagens de forma consistente. Sua troca por uma busca visual deve ocorrer somente nos pontos selecionados, preservando teclado, seleção e formulário, em vez de tentar inserir imagens dentro de opções nativas. Gráficos, históricos completos e todas as ocorrências de ticker não receberão imagens indiscriminadamente.

Usar o logotipo real do emissor ou fundo quando corretamente identificado; não gerar imagens que possam ser confundidas com sua marca. Para ausência ou falha, apresentar uma alternativa local consistente baseada no ticker. Manter sempre nome/ticker em texto, com imagem complementar sem anúncio duplicado para leitores de tela. A falha de uma imagem não pode bloquear consulta, cálculo ou edição.

## Fontes externas verificadas

A documentação oficial da [brapi](https://brapi.dev/docs/openapi) descreve imagens nos dados de ativos brasileiros, inclusive ações e FIIs, e campos de identificação que podem auxiliar o cadastro. É uma candidata para sugestões no Brasil, sem assumir cobertura completa ou liberação de todos os dados no plano atual.

A [Alpha Vantage](https://www.alphavantage.co/documentation/#company-logo) documenta `COMPANY_LOGO` para logotipos de companhias negociadas em bolsa. Isso justifica avaliar a fonte para ações e REITs dos EUA, mas não comprova cobertura por ativo nem disponibilidade na chave do usuário. O endpoint de [perfil da companhia](https://www.alphavantage.co/documentation/#company-overview) também precisa ter seu acesso e custo de cota verificados antes de entrar no preenchimento assistido.

Nesta revisão foram consultadas somente as documentações públicas. Não foram usadas chaves pessoais, realizadas consultas em lote aos ativos do usuário ou contratados serviços. A disponibilidade real, as condições de uso/armazenamento das imagens e o consumo dos endpoints novos serão verificados durante a implementação dos adaptadores. Uma fonte indisponível deve resultar em preenchimento manual e alternativa visual, não na ativação automática de um serviço pago.

## Ajustes de arquitetura necessários

- **Identidade de consulta**: hoje a consulta de preço deduz BR/US de nomes de país e moeda, e o ticker é único globalmente. Para busca automática confiável, definir mercado e símbolo de consulta explicitamente, reaproveitando os mapeamentos existentes. Não mudar essa unicidade nem reidentificar ativos como efeito colateral da busca; conflitos devem ser apresentados.
- **Sugestões em Application**: contratos próprios para resultados e diferenças de cadastro, com fonte e campos ausentes. A busca não cria ativos, posições ou classificações.
- **Provedores em Infrastructure**: adaptadores para metadados/imagens, com cache e consumo coordenado com o controle de consultas já existente. Endpoints novos não podem ignorar as pausas e cotas do provedor.
- **Identidade visual**: acrescentar dados opcionais de imagem e procedência e definir armazenamento local/cache antes de expor URLs. Aquisição deve aceitar somente formatos e origens previstos, limitar conteúdo e falhar para a alternativa visual. Evitar um proxy que baixe qualquer endereço arbitrário fornecido pelo cliente.
- **Entrega das imagens**: o navegador não deve receber chaves dos provedores. Carregar imagens de tamanho adequado, com cache e dimensões fixas; não consumir uma consulta de dados do provedor a cada renderização de linha.
- **Referências e classificações**: os serviços atuais continuam responsáveis pelas regras e pela validação dos vínculos. A UI centraliza a experiência, não os cálculos ou as decisões financeiras.

## Sequência recomendada de implementação

| Etapa | Entrega concreta | Critério de conclusão |
| --- | --- | --- |
| 1 | Organizar Classificações/Referências e melhorar o editor de Ativos. | Menos navegação para cadastrar; formulários preservados; rotas e IDs mantidos; referências faltantes tratadas explicitamente. |
| 2 | Base de identidade visual, imagem opcional/personalizada e componente compartilhado na carteira e catálogo. | Mesmo padrão nos pontos escolhidos; alternativa para imagem ausente; personalização preservada; sem dependência de uma consulta externa para exibir cada linha. |
| 3 | **Implementada** — Busca por ticker com mercado explícito e metadados/imagens sugeridos. | Revisão antes de salvar, fonte identificada, cotas/cache respeitados e funcionamento manual disponível em falhas. |
| 4 | **Implementada** — Revisão de Saldos e patrimônio externo. | Nomes e fluxo esclarecem valor patrimonial e uso em operações, preservando contas, saldos e histórico. |

A etapa de imagens pode aproveitar a base de cadastro antes da integração automática. Sua disponibilização não precisa esperar cobertura integral dos provedores para todos os ativos.

## Compatibilidade, migrações e validação

Preservar IDs de ativos e classificações, metas, regras/revisões de estratégias, planos salvos, objetivos, compras/vendas, proventos, movimentações e fotografias. Mudanças de navegação não exigem migração; campos persistidos de imagem/mercado, se adotados, devem ter migration aditiva e compatibilidade com clientes antigos. Não executar deduplicação automática nem alterar posições como parte da simplificação.

Na implementação, testar sugestões sem gravação, referências ausentes, divergências, manutenção de dados confirmados, preservação de formulário, cadastro contextual, imagens ausentes/com falha e personalizações. Validar consultas e cotas com provedores simulados, revisão de campos, manutenção dos vínculos e independência dos valores financeiros. Conferir os locais selecionados em desktop/celular e nos temas claro/escuro, com testes proporcionais a cada entrega.

A revisão inicial foi documental. A primeira implementação posterior está registrada em [cadastros e imagens](cadastros-e-imagens.md), com migration aditiva ainda não aplicada ao banco pessoal.
