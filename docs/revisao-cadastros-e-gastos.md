# Revisões de produto registradas em 30/10/2026

Solicitações do usuário para análise futura. Este registro não define uma implementação nem autoriza migração/unificação automática dos dados existentes.

## Reduzir cadastros manuais

Problema percebido: há muitos cadastros separados — **Patrimônio Externo, Ativos, Tipos de Ativo, Países, Moedas, Categorias e Setores**. Avaliar quais podem ser unificados na experiência, quais realmente precisam de entidades distintas e quais informações podem vir automaticamente de fontes confiáveis.

Pontos a revisar:

- Mapear o esforço atual para cadastrar e manter um investimento, identificando duplicações, telas e campos desnecessários.
- Examinar a relação entre Ativos, Tipos de Ativo, Categorias e Setores. Nomes parecidos não comprovam equivalência: tipo de instrumento, agrupamento de estratégia e setor têm responsabilidades próprias hoje.
- Avaliar Patrimônio Externo em relação aos ativos e saldos internos, preservando seu significado, a identidade das contas e os vínculos de operações existentes. Não juntar saldos nem criar posições automaticamente.
- Avaliar Países e Moedas como dados de referência disponíveis por padrão, com manutenção excepcional, em vez de exigir cadastro manual no fluxo comum.
- Avaliar busca/preenchimento de ativos por ticker e revisão dos dados sugeridos: fonte, cobertura Brasil/EUA, limites dos provedores e tratamento de divergências. Não sobrescrever classificações ou dados confirmados silenciosamente.
- Separar simplificação da navegação de unificação do modelo: reunir telas pode resolver parte do esforço sem remover entidades necessárias.

Antes de implementar, apresentar proposta de fluxo, campos manuais/automáticos, compatibilidade com os cadastros atuais e plano de migração auditável. Preservar IDs/vínculos, revisões de estratégias, metas, histórico e fotografias; não apagar cadastros para reduzir a quantidade de telas.

## Análise de gastos pessoais como extra independente

Desejo registrado: analisar gastos pessoais, embora isso amplie o escopo além de investimentos. O usuário ainda não definiu como o módulo deverá funcionar.

Requisito firme: **ser independente dos investimentos, sem afetar carteira e dashboard de investimentos**. Gastos não devem gerar movimentações financeiras da carteira, diminuir Patrimônio Externo, criar fluxos de aporte/retirada, alterar rentabilidade ou marcar fotografias como desatualizadas. Nenhuma integração automática entre os módulos deve ser presumida.

Para definição futura, estudar:

- Quais perguntas a análise deve responder: gastos por período, categoria, recorrência e comparação mensal.
- Como os dados entram: registro manual, importação de arquivo ou outra opção a escolher; não existe fonte/formato aprovado.
- Se haverá receitas, contas, cartões, parcelas, transferências e orçamento; esses itens são perguntas de escopo, não requisitos já decididos.
- Categorias próprias de gastos, sem confundi-las com categorias/setores de investimento.
- Área de navegação, contratos, serviços e dados próprios, com relatórios exclusivos e isolamento verificável por testes.
- Privacidade, correção de registros e identificação de importações duplicadas, conforme o fluxo escolhido.

Próxima ação: desenhar uma proposta pequena de uso e confirmar o escopo funcional antes de implementar o módulo. O registro não introduz tabelas, endpoints, telas ou importações nesta entrega de limites de concentração.
