# Revisão de setores no cadastro assistido

Implementada em 05/11/2026, como continuidade do [cadastro assistido](cadastro-assistido-e-saldos.md).

## Como usar

No editor de Ativos, busque o ticker e selecione o resultado. A revisão mantém o texto original do setor informado pela fonte e explica se houve correspondência pelo nome, equivalência conhecida, ambiguidade, ausência de correspondência ou ausência de informação.

O campo **Setor para revisar** permite escolher um setor já cadastrado. Uma correspondência única prepara a comparação; duas ou mais correspondências exigem escolha manual, mesmo se uma tiver exatamente o nome da fonte. **Manter setor atual** remove somente a proposta de setor.

A sugestão de setor começa sempre desmarcada, inclusive em cadastros novos. Escolher outro setor também desmarca a aplicação anterior. Para alterar o rascunho, marque **Aplicar sugestão de Setor** e clique em **Aplicar campos selecionados**. **Salvar ativo** confirma a alteração no banco. Descartar sugestões remove a revisão pendente; alterações já aplicadas ao formulário permanecem no rascunho, conforme o comportamento dos demais campos.

Se o setor atual for editado durante a revisão, será necessário conferir os campos novamente. Setores removidos ou renomeados no catálogo carregado não podem ser aplicados usando uma proposta desatualizada.

## Equivalências e limites

As equivalências ficam em uma função pura do recurso de cadastro assistido, separada da interface e dos cálculos financeiros. A resolução normaliza espaços, maiúsculas e acentos e compara nomes completos; não usa aproximação por substring.

| Nomes da fonte | Nomes locais reconhecidos |
| --- | --- |
| Technology / Information Technology | Tecnologia / Tecnologia da Informação |
| Financial Services / Financials | Serviços Financeiros / Financeiro / Finanças |
| Healthcare / Health Care | Saúde |
| Consumer Cyclical / Consumer Discretionary | Consumo Cíclico |
| Consumer Defensive / Consumer Staples | Consumo Não Cíclico |
| Basic Materials / Materials | Materiais Básicos |
| Industrials / Industrial | Bens Industriais |
| Real Estate | Imobiliário |
| Communication Services | Serviços de Comunicação / Comunicações |
| Utilities | Utilidade Pública / Utilidades Públicas |
| Energy | Energia |

Essa tabela sugere equivalências de nomes amplos, não impõe uma taxonomia. A escolha final continua dependendo da classificação pessoal. Não há inferência de setor a partir de bancos, subsetores, tipos de fundo, ticker, país ou moeda. Utilities não é convertido em Energia. A opção estruturada de setor imobiliário não é alterada automaticamente.

Os setores precisam existir no catálogo. Não há criação, fusão ou renomeação automática, nem persistência de equivalências pessoais. Casos desconhecidos podem ser resolvidos manualmente na própria revisão ou pelo cadastro contextual de setores já disponível no editor.

A fonte dos EUA usada pelo projeto pode não informar setor; esta entrega não acrescenta consultas de perfil nem depende de plano pago. As equivalências e a escolha local não consomem consultas externas. A busca por ticker mantém as cotas e o cache existentes.

## Efeito nos dados

Não há migration, alteração de contrato ou mudança nos serviços de cálculo. A categoria da estratégia permanece independente. Confirmar um setor muda a classificação do ativo e, consequentemente, os agrupamentos por setor e a avaliação das metas que usam essa classificação. Não cria operações, altera quantidades, preços, custos, saldos ou proventos. Não há reclassificação em lote dos ativos existentes.

## Validação

189 testes Angular aprovados, incluindo equivalências, nomes normalizados, ambiguidades, ausência de setor, aplicação explícita e revisão invalidada por alteração do catálogo. Os oito cenários de apresentação do cadastro assistido passaram em desktop/celular e temas claro/escuro após corrigir a sincronização inicial do seletor. Lint e build de produção aprovados; pacote inicial de 456,83 kB, abaixo do limite de 500 kB.

Os 28 cenários E2E passaram. A revisão de setor foi confirmada em desktop e celular: antes de salvar, o banco conserva o setor anterior; depois de salvar, a posição recebe a nova classificação, preservando quantidades, custos, valores e proventos e sem criar movimentações. A base temporária foi removida pelo script de testes. Não houve migration na base pessoal ou consulta real aos provedores nesta validação.
