# Finanças pessoais — planejamento consolidado

Decisões consolidadas em **06/11/2026**. Status: **somente planejamento**. Este documento não representa implementação, criação de projetos, alteração do banco ou autorização para executar todas as fases automaticamente. Substitui, para este tema, a indefinição do registro inicial em [revisão de cadastros e gastos](revisao-cadastros-e-gastos.md).

## 1. Objetivo principal

Saber quanto se gasta por mês, em quê se gasta e quais compromissos ainda precisam ser pagos. O usuário quer avaliar pessoalmente quais gastos fazem sentido e quanto pode destinar a investimentos sem precisar resgatar dinheiro posteriormente para pagar contas.

Houve meses em que uma estimativa incorreta levou ao uso de recursos de investimentos, especialmente cofrinhos com liquidez, para cobrir despesas. O módulo deve tornar esse risco visível antes da decisão, sem decidir pelo usuário ou movimentar investimentos automaticamente.

As análises e decisões serão feitas pelo usuário. Não haverá integração financeira inicial com a carteira, chatbot decisor ou execução automática de aportes.

## 2. Organização e execução definidas

- Manter **o mesmo repositório e a mesma solution** do InvestmentTracker.
- Criar Finanças pessoais como **módulo separado, com projetos `.csproj` próprios**, dentro dessa solution. Esses projetos serão bibliotecas, não servidores adicionais.
- Continuar iniciando **uma única aplicação backend: `InvestmentTracker.Api`**, pelo Visual Studio.
- Manter **um único frontend Angular**, iniciado pelo `ng serve`, com uma área própria de Finanças pessoais.
- Não exigir outro Visual Studio, segunda API ou segunda aplicação Angular em execução.
- Nomes e quantidade exata dos projetos serão definidos no desenho técnico; não estão decididos neste documento.

Direção: monólito modular, separado nas regras e nos dados e compartilhado na execução. Uma nova solution ou aplicação independente não será criada nesta primeira versão.

## 3. Limites entre os módulos

Finanças pessoais terá contas, cartões, lançamentos, categorias de gasto, compromissos e orçamentos próprios. Não reutilizar entidades de investimento só porque ambas envolvem valores monetários.

Compartilhamento permitido quando fizer sentido: navegação, componentes visuais, formatação, mecanismos de ajuda e infraestrutura técnica. Compartilhar a interface não significa unificar saldos ou significados financeiros.

Inicialmente:

- Uma despesa não cria retirada na carteira, reduz patrimônio externo ou modifica posições.
- Uma receita pessoal não cria aporte nos investimentos.
- Movimentações pessoais não alteram dashboard de investimentos, metas, rentabilidade, fluxos ou fotografias.
- Uma destinação para investimentos registrada aqui não executa uma compra nem registra um aporte no outro módulo.
- Um módulo não altera diretamente as tabelas do outro; comunicação futura deverá usar contratos explícitos.
- Investimentos deve continuar funcionando independentemente do uso do módulo pessoal.

Pode ser usado o mesmo banco, mantendo tabelas e persistência próprias. A organização física definitiva — schemas, DbContexts, migrations e compartilhamento da infraestrutura — ainda depende do desenho técnico. Não presumir compartilhamento de entidades ou acesso cruzado por usar o mesmo banco.

## 4. Identidade visual própria

**Decisão explícita do usuário:** embora compartilhe o Angular, Finanças pessoais terá **coloração diferente** da área de investimentos, deixando clara a separação entre os contextos.

Aplicar uma identidade cromática contextual, por exemplo em acentos, cabeçalhos, navegação e seleção de área, mantendo o padrão de componentes, tipografia e interação do projeto. A paleta exata ainda será escolhida; não foi aprovada nenhuma cor específica.

Requisitos:

- Compatibilidade com temas claro, escuro e sistema existentes.
- Identificação textual persistente de qual área está aberta; não depender somente da cor.
- Ao trocar de área, aplicar o tema contextual sem deixar estilos vazarem para a outra.
- Preservar significados de erro, aviso, sucesso e entrada/saída; cores da identidade não podem confundir esses estados.
- Manter privacidade dos valores, legibilidade, foco, comportamento móvel e componentes compartilhados.

## 5. Mecanismos comuns, sem duplicar funcionalidades

Casos semelhantes compartilharão mecanismos parametrizados, com formulários e nomes adequados ao uso:

| Mecanismo | Aplicações | Parâmetros relevantes |
| --- | --- | --- |
| Lançamento avulso | Compra única, receita eventual, Pix recebido, presente/doação | Direção, valor, conta, categoria, datas e situação |
| Recorrência | Assinatura mensal, anual ou outra despesa periódica | Frequência, intervalo, início, término, vencimento, pausa/cancelamento |
| Sequência limitada | Compra parcelada | Valor total, número de parcelas, primeira cobrança, parcela atual e término |
| Alteração com vigência | Aumento/redução de assinatura ou outro compromisso | Novo valor, vigência e alcance da alteração |
| Realização e correção | Pagamento, recebimento, pagamento parcial, estorno e reembolso | Valor/data efetivos, saldo pendente e vínculo com o registro original |

Conceito orientador: uma definição do compromisso origina ocorrências previstas; cada ocorrência registra sua realização. Calendário, relatórios e projeções usam a mesma base, evitando cálculos paralelos por tipo de despesa.

Não unificar comportamentos distintos à força: parcelas precisam fechar um valor total e terminar; uma recorrência pode não ter término; transferências não são renda/gasto; pagamento de fatura não duplica a compra.

### Entradas: decisão da primeira versão

**Entradas serão registradas manualmente**, pelo valor efetivamente recebido. Isso inclui salário líquido, horas extras, pagamentos recebidos, Pix, presentes e doações. Não calcular salário bruto/líquido, benefícios, descontos, reajustes ou verbas de demissão.

A hipótese anterior de salário recorrente automático não faz parte do escopo inicial. Uma nova entrada não altera as seguintes. A projeção não presume que o próximo salário ou outro recebimento chegará antes de ser registrado.

Pix é meio de movimentação, não necessariamente categoria/origem da renda. Permitir descrever a natureza do recebimento sem obrigar categorias excessivas.

### Despesas recorrentes e reajustes

Suportar mensal, anual e intervalos parametrizados. Uma alteração deve indicar se vale somente para uma ocorrência ou para esta e as próximas, com vigência clara.

Aumento, redução, pausa e cancelamento afetam o futuro conforme sua vigência, sem reescrever cobranças realizadas. Exemplo: assinatura de R$ 30 que passa a R$ 40 em julho mantém os pagamentos anteriores com R$ 30. Definir regra explícita para ocorrências futuras já geradas ou editadas individualmente antes de aplicar alterações em lote.

### Compras avulsas e parceladas

Compra avulsa gera uma única despesa. Parcelamento identifica cada ocorrência como parcela X de Y e termina na última. A soma das parcelas precisa coincidir com o total; diferenças de centavos devem ser distribuídas por regra determinística e apresentada ao usuário.

## 6. Histórico, datas e situações

Separar previsto de realizado. Valor, vencimento e data efetiva podem diferir. Prever pendente, parcialmente realizado, realizado e cancelado, com tratamento de atraso derivado das datas/saldo pendente.

- Distinguir competência, vencimento e data efetiva de pagamento/recebimento.
- Ao realizar um compromisso, atualizar sua realização; não criar outra despesa independente para o mesmo pagamento.
- Valores realizados e sua história devem ser preservados. Correções guardam vínculo, valor anterior e motivo, sem sobrescrever silenciosamente o passado.
- Estorno/reembolso não apaga o gasto original e precisa refletir corretamente o dinheiro devolvido.
- Compromissos vencidos não desaparecem ao mudar o mês; pagamentos parciais mantêm o restante pendente.
- Definir regras para dia 29/30/31 em meses menores e para fins de semana/feriados. Datas efetivas sempre refletem o que ocorreu, sem deslocamento silencioso.

## 7. Contas, cartões e recursos disponíveis

O saldo precisa ser controlado **por origem**, não apenas somado em um total utilizável.

| Instrumento | Tratamento |
| --- | --- |
| Conta, carteira ou cartão pré-pago | Saldo próprio disponível, com saldo inicial e data de início |
| Cartão de débito | Meio de pagamento vinculado à conta; não cria um segundo saldo do mesmo dinheiro |
| Cartão de crédito | Limite, fechamento, vencimento e faturas; limite não é dinheiro disponível para investir |

Compras no crédito e suas parcelas devem compor as cobranças corretas. Pagar a fatura reduz o saldo da conta de pagamento, sem contar novamente as compras como gasto. A regra detalhada do ciclo de fatura será desenhada antes de implementar.

Cada despesa e fatura deve indicar a conta de pagamento, quando conhecida. Compromissos sem origem definida precisam ficar visíveis como pendência: não podem ser considerados automaticamente cobertos por qualquer conta.

Transferências entre contas próprias alteram origem e destino sem gerar receita/despesa. Não presumir transferências só porque o consolidado tem dinheiro suficiente. Realocar dinheiro não cria disponibilidade adicional nem elimina reservas e obrigações já assumidas.

Incluir conferência com o saldo real e correções rastreáveis, distinguindo ajuste, renda e despesa. O saldo inicial precisa de data para não representar movimentações anteriores como receitas do período.

## 8. Tipos de saída e categorias

Separar **destino/natureza do dinheiro** de **categoria de consumo**:

- Despesa de consumo: moradia, alimentação, transporte, assinaturas etc.
- Destinação para investimentos: reduz o dinheiro disponível nas contas pessoais, sem contar como consumo e sem integração inicial com a carteira.
- Transferência entre contas próprias: não é receita nem despesa no consolidado.
- Ajustes, estornos e reembolsos: tratamento próprio e rastreável, sem mascarar renda/gasto recorrente.

Recorrente, avulso e parcelado são características do compromisso, não substitutos das categorias de gasto. Evitar somar transferências, compras e quitação de fatura como se fossem despesas diferentes.

Um resgate de investimento usado para cobrir despesas poderá ser informado manualmente como origem extraordinária de recursos, separado da renda recorrente. Não haverá lançamento automático no módulo de investimentos. A apresentação deve evidenciar quando o mês dependeu desse recurso.

## 9. Calendário e totais

**Requisito confirmado:** visão de calendário para localizar exatamente as cobranças, com totais do dia, mês e ano.

Duas perspectivas complementares:

1. Agenda prevista: vencimentos, parcelas, cobranças recorrentes, compromissos pendentes e atrasados. Não incluir salário futuro presumido.
2. Realizado: datas e valores efetivos de pagamentos e recebimentos.

Ao selecionar um dia, exibir seus lançamentos e totais. Separar receitas, despesas, destinações para investimentos e transferências, bem como previsto, realizado e pendente; não somar estados do mesmo compromisso como gastos adicionais.

Distinguir “gasto por competência” de “saída de dinheiro por data de pagamento”. Por exemplo, uma compra hoje com fatura no mês seguinte afeta essas visões de formas diferentes. A tela deve informar o critério do total exibido.

Permitir análise por conta/cartão e consolidada, com navegação mensal/anual e compreensão de compromissos futuros. A disposição exata das visões será definida no desenho de UI.

## 10. Orçamento, reservas e análise pessoal

Comparar orçamento por categoria com o realizado e manter a parcela ainda necessária para despesas variáveis, como alimentação e transporte. Considerar despesas futuras, não apenas as já pagas.

Prever reserva para despesas anuais e uma margem de segurança definida pelo usuário. Reservar dinheiro não é gerar uma nova despesa: o pagamento correspondente não pode ser descontado duas vezes.

Análises pretendidas: gastos por categoria, maiores despesas, evolução mensal/anual, compromissos e diferença em relação ao orçamento. Classificações opcionais como essencial, discricionário ou “quero revisar” são possibilidades de apoio, não julgamento automático do sistema nem campos obrigatórios já aprovados.

O usuário avaliará quais gastos fazem sentido. Não gerar recomendações de compra/venda, cortes obrigatórios ou decisões financeiras automáticas.

## 11. Simulação de uma destinação para investimentos

Antes de registrar a saída, escolher sua **conta de origem** e verificar:

1. A origem possui saldo para aquela saída?
2. Depois dela, conseguirá cobrir seus compromissos nas datas previstas?
3. O planejamento consolidado preservará compromissos, orçamento variável, reservas e margem de segurança?

Exemplo: conta A tem R$ 500, conta B tem R$ 1.000. Destinar R$ 800 a partir da conta A não é possível apenas porque existem R$ 1.500 no total. É necessário reduzir a saída ou registrar uma transferência real antes.

O conceito do cálculo é:

**Recursos disponíveis − compromissos pendentes − orçamento variável ainda necessário − reservas − margem de segurança = disponibilidade estimada antes de investir.**

Esse conceito exige alocação sem sobreposição: despesas pagas já estão refletidas no saldo; orçamento considera o restante ainda necessário; parcelas/faturas/reservas não entram duas vezes. A fórmula detalhada e os horizontes serão especificados com exemplos antes de implementar.

Avaliar a projeção **por conta e por data**, inclusive o menor saldo previsto no período, além do consolidado. Um mês pode terminar positivo e ainda faltar dinheiro antes de uma cobrança. Verificar compromissos anuais e parcelas futuras pertinentes, sem dar a entender que uma sobra mensal é livre de todas as obrigações futuras.

Avisos propostos:

- Saldo insuficiente na origem.
- Saída compromete a margem de segurança.
- Saída deixará recursos insuficientes para compromissos registrados, indicando quando/quanto.
- Projeção incompleta por compromisso sem conta ou informação necessária.

Alertas de planejamento inicialmente orientam a decisão, sem bloqueio obrigatório de orçamento. Isso não equivale a autorizar saldo negativo: a regra de validação de saldo/cheque especial ainda será definida. Não consumir automaticamente recursos de outras contas ou investimentos para aprovar a saída.

A projeção inicial usa dinheiro já registrado, sem assumir entradas futuras. Mostrar essa condição, o horizonte e a completude dos dados. O resultado é estimativa baseada nos registros, não garantia de que todas as despesas foram informadas.

## 12. Crescimento e integração futura

O módulo deve poder crescer, integrar-se ou ser extraído sem que essa mudança seja obrigatória agora. Preservar contratos, IDs, responsabilidade pelos dados e histórico.

Uma integração futura poderá relacionar uma destinação pessoal ao aporte efetivo nos investimentos, evitando duplicidade e distinguindo destinação, transferência e compra. Não criar esse vínculo automaticamente na primeira versão.

Separar depois é mais viável com módulos independentes; ainda exigirá trabalho de API/comunicação, dados, configuração, execução e tratamento de falhas. Integrar aplicações separadas também exigiria esses contratos. Compartilhar entidades/tabelas sem limites torna ambas as mudanças mais difíceis. A estrutura escolhida preserva opções, sem prometer extração sem custo.

## 13. Relação com outros planos

- [Melhorias técnicas](plano-melhorias-tecnicas.md): o desenho modular e a preservação dos contratos devem orientar o desenvolvimento.
- [Tutorial guiado](plano-tutorial-guiado.md): pode explicar este módulo futuramente; não é requisito para que os cálculos funcionem.
- [Chatbot](plano-chatbot.md): iniciativa distinta e opcional; não decide orçamento nem movimenta dinheiro. O tutorial continua priorizado antes do chatbot.

Não reintroduzir ideias, teses ou diário de investimentos, retirados anteriormente dos planos.

## 14. Pontos ainda a detalhar, sem decisões presumidas

- Nomes/referências dos projetos e organização de persistência/migrations.
- Paleta de cores e navegação entre áreas.
- Horizonte e alocação exatos do orçamento, reservas e projeções por conta.
- Regras de datas, faturas, antecipação de parcelas e exceções às recorrências.
- Política para saldo negativo/cheque especial e para correções de registros realizados.
- Necessidade de múltiplas moedas no módulo pessoal: não está definida e não deve herdar conversões dos investimentos implicitamente.
- Ordem e conteúdo exato das entregas. Importações e integrações externas não integram o mínimo acordado.

## 15. Critérios gerais para a futura implementação

- Manter a rotina de execução: uma API e um Angular.
- Evidenciar qual módulo está ativo, com coloração própria e identificação textual, respeitando os temas existentes.
- Provar que lançamentos pessoais não modificam dados de investimentos.
- Preservar histórico ao reajustar, cancelar, corrigir e realizar compromissos.
- Não duplicar despesas entre previsão, pagamento, cartão/fatura, transferência e reserva.
- Validar saldo por origem e projeção por data, não só consolidado.
- Manter entradas manuais e ausência de receita futura presumida.
- Testar parcelas/centavos, virada de mês/ano, recorrências anuais, atrasos, pagamentos parciais, estornos e avisos com exemplos verificáveis.
- Seguir arquitetura, precisão financeira, componentes de UI e testes do projeto; não aplicar migrations na base pessoal sem uma etapa própria autorizada.

Nenhuma dessas funcionalidades foi implementada por este documento.
