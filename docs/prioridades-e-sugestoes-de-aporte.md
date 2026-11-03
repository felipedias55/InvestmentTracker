# Prioridades e sugestões de aporte

Entrega de 30/10/2026. Em **Simular aporte → Estratégia de investimento**, ative **Atender regras por prioridade**. Números menores são atendidos primeiro: de 1 a 1000, com padrão 100. A preferência de moeda pode ser usada mesmo sem ativar prioridades.

## Orçamento e limites

- As reservas continuam usando a moeda-base da carteira. Com prioridades ativas, o orçamento atende primeiro as reservas de menor número. Reservas empatadas dividem o orçamento disponível proporcionalmente ao valor solicitado, com distribuição exata dos centavos. Exemplo: duas reservas de 80, com prioridades 1 e 5, recebem orçamentos de 80 e 20 quando o aporte é 100.
- Sem ativar a opção, configurações antigas mantêm a recusa quando as reservas ultrapassam o aporte.
- A prioridade do grupo categoria/setor ordena sugestões, centavos de empate e disputas sob limites. Ela não substitui a divisão igual das reservas nem os pesos das metas.
- Sob limites de concentração, o cálculo conserva o maior volume viável para as capacidades calculadas e favorece as prioridades entre essas distribuições. Reservas precedem o restante; depois entram prioridade da reserva e prioridade do grupo. O mecanismo conservador de convergência dos limites continua aplicável.
- Elegibilidade, bloqueios e limites continuam obrigatórios. Uma prioridade alta não libera um grupo bloqueado ou sem ativos elegíveis. Valores reservados que não puderem ser distribuídos ficam não distribuídos; não são automaticamente transferidos para outra reserva.
- **Atendimento das prioridades** apresenta solicitado, orçamento e sugestão final. O orçamento pode ser maior que a sugestão após bloqueios ou limites. O restante é calculado depois dos orçamentos reservados.

## Setor, real e dólar

Cada linha informa categoria, setor, valor na moeda-base, alternativas em moeda original e os ativos elegíveis de cada alternativa. O servidor indica a moeda quando existe uma única alternativa com câmbio e equivalente positivos, ou quando a preferência salva do grupo está disponível.

Quando houver BRL e USD no mesmo grupo, escolha **Moeda preferida neste grupo**. Se a preferência estiver indisponível, o plano explica o motivo e mantém as alternativas para revisão, sem trocar a preferência silenciosamente. Sem preferência e com várias alternativas válidas, o plano pede uma escolha.

Assim, uma linha pode indicar **Tecnologia em dólar (USD)** ou **Financeiro em real (BRL)** conforme os ativos elegíveis e a estratégia. A indicação usa sua alocação e suas preferências; não faz previsão de câmbio ou avaliação de empresas. Os tickers apresentados são candidatos elegíveis, sem definição de quantidade a comprar. Moeda do ativo não equivale a país ou exposição econômica.

Os valores BRL/USD são alternativas equivalentes do mesmo aporte: não devem ser somados. Conversões mantêm as regras existentes de câmbio, sem novas consultas externas introduzidas por esta funcionalidade.

## Compatibilidade e arquitetura

Application valida prioridades, calcula orçamentos, aplica concentração e enriquece as sugestões. Infrastructure preserva as preferências no JSON das revisões existentes; Angular apenas apresenta os resultados e edita a configuração. Valores monetários continuam decimais serializados como strings.

Campos novos são opcionais para clientes antigos. Não há nova migration nesta entrega. Simular e salvar estratégias não alteram posições, movimentações, proventos, fotografias, metas ou Patrimônio Externo.

Os testes cobrem orçamento insuficiente, empates e centavos, precisão decimal, limites cruzados, prioridades de reserva/grupo, preferências disponíveis/indisponíveis, compatibilidade do JSON antigo, revisões e preservação dos dados financeiros. A interface é exercitada em desktop/celular e temas claro/escuro.

Validação local: **666 testes aprovados** — 271 unitários .NET, 172 de integração, 143 Angular, 64 de apresentação e 16 E2E. Lint, formatação e build aprovados; pacote inicial de 455,77 kB. Imagens do plano móvel e do editor escuro foram conferidas. Integração e E2E usaram bancos temporários; a base E2E foi removida ao concluir. O banco pessoal não foi alterado.
