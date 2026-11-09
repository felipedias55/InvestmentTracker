# Plano de chatbot de ajuda — 06/11/2026

Status: planejado, não implementado. Projeto separado do [tutorial guiado](plano-tutorial-guiado.md), que terá prioridade. A escolha de provedor/modelo, implementação e ativação de custos não fazem parte desta análise.

## Objetivo

Responder dúvidas sobre o uso real do FDInvestment Tracker: onde ajustar saldo, diferença entre cadastro e operação, como consultar cotações, por que um benchmark está indisponível e como interpretar as mensagens. Aproveitar a mesma base revisada de ajuda e roteiros do tutorial.

Primeira versão deve explicar o sistema, com links para a tela correta e para o tutorial pertinente. Não será consultor de investimentos, executor de compras/vendas ou modificador de saldos.

## Etapas

1. **Base de ajuda:** perguntas frequentes, termos e procedimentos versionados junto do projeto. Fazer busca e ajuda contextual úteis mesmo sem IA. Definir responsáveis pela atualização quando a interface mudar.
2. **Protótipo conversacional:** avaliar, com perguntas reais, se busca textual basta ou se um modelo melhora a resposta. Comparar acerto, clareza, latência e custo; escolher tecnologia somente nessa etapa.
3. **Chatbot opcional de documentação:** responder com base no conteúdo recuperado, citar a origem e assumir quando não houver resposta. Oferecer passos verificáveis e navegação segura, em vez de inventar botões ou regras.
4. **Contexto pessoal, se autorizado futuramente:** avaliar separadamente se precisa de carteira, valores ou histórico. A primeira versão não os envia ao provedor. Dados mascarados na interface não podem ser expostos implicitamente no chat.

## Limites e arquitetura

Se houver API de IA, chamadas devem sair do backend, com segredo fora do frontend, cancelamento, limites de entrada/saída, histórico de conversa limitado e controle de consumo. Não usar o servidor da API para navegar em URLs arbitrárias sugeridas pelo modelo. Tratar conteúdo recuperado e respostas como dados, sem lhes dar autoridade para executar instruções.

Sem ferramentas de gravação financeira, acesso direto ao banco ou chamadas automáticas de cotações. A versão inicial oferece links e explicações. Uma eventual execução de ações precisaria de outro plano, validação determinística e autorização própria; não está incluída.

Custos de IA, caso adotada, serão independentes das franquias brapi/Alpha Vantage. Antes da ativação, definir orçamento, limite por sessão, indicação de consumo e comportamento ao atingir o limite. Não prometer gratuidade nem recomendar modelo/preço sem pesquisa atualizada nessa etapa.

Definir política de privacidade e retenção antes de enviar conversas externamente. Evitar logs de perguntas completas, dados pessoais e respostas contendo patrimônio por padrão; registrar apenas o diagnóstico necessário. Conversas não devem persistir automaticamente sem uma decisão de produto.

## Avaliação e aceite

Conjunto de perguntas de referência: ajustar saldo externo; depósito versus ajuste; cadastro versus compra; cotas das APIs; fotografia desatualizada; IPCA ausente; fechamento parcial; significado de Dietz versus XIRR. Incluir perguntas ambíguas, fora do escopo e tentativas de induzir regras falsas.

Aceite: respostas aderentes ao sistema e à documentação, origem verificável, recusa de execução financeira, nenhum envio de dados pessoais por padrão, indisponibilidade tratada com alternativa de ajuda local, consumo limitado e ausência de consultas de mercado disparadas pela conversa.

Não iniciar por agentes autônomos, acesso a todos os dados, recomendações personalizadas ou treinamento de modelo próprio. Primeiro provar utilidade e precisão para dúvidas de uso.
