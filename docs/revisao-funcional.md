# Revisão funcional — 18/09/2026

## Escopo concluído

Revisão dos fluxos existentes, documentação de instalação/atualização e câmbio histórico dos proventos. Preservadas as camadas da solução, namespaces com chaves e os valores cadastrados. Backup agendado e automação de correções atrasadas permanecem para etapas seguintes.

## Fluxos verificados

- Carteira vazia, cadastros, posição inicial, compras, vendas com custos e reinvestimento.
- Depósitos, retiradas, transferências, ajustes, estornos e trilha de auditoria.
- Fotografias, reabertura e preservação das versões anteriores.
- Metas por categoria e setor: validação de 100%, diferenças positivas, arredondamento e valor não distribuído quando não há grupo abaixo da meta. Simular não altera saldos nem cria movimentos.
- Proventos: equivalente manual ou consulta histórica, persistência da cotação, repetição idempotente, indisponibilidade do provedor e conversões pendentes.
- Complementação de recebimentos antigos e correção por novas versões: motivo obrigatório, controle de concorrência, isolamento entre carteiras, moedas-base diferentes e preservação dos saldos e fotografias.
- Análise original/convertida e estorno usando o equivalente do recebimento; grupos incompletos aparecem pendentes, sem apresentar soma parcial como total.
- Navegação após rolagem e preferências dos gráficos em desktop e celular emulado.

O total acumulado da carteira inclui saldos iniciais e usa câmbio atual. A análise de recebimentos usa os equivalentes históricos registrados; os dois totais podem diferir. Os saldos iniciais não geram recebimentos retroativos fictícios.

## Validação automatizada

Execução nesta revisão: **322 testes aprovados** — 146 unitários C#, 110 de integração, 58 do frontend e 8 E2E (quatro cenários em desktop e celular emulado). Build de produção Angular validado. Os testes de integração e E2E usam bases temporárias e dados sintéticos.

A migração também foi testada a partir do esquema anterior, mantendo um recebimento legado sem inventar moeda-base ou cotação. Testes do cliente HTTP verificam a data solicitada e rejeitam referências futuras ou excessivamente antigas. A integração automatizada não depende da disponibilidade da API pública.

## Erro IncomeConversion

O erro `Invalid object name 'IncomeConversion'` ocorreu porque a implementação interrompida já consultava a nova entidade, mas sua migration ainda não havia sido concluída/aplicada. Nesta revisão foi criada e aplicada `20260918131542_IncomeHistoricalConversions`.

Antes da atualização local, foi criado um backup e validada sua restauração com verificação de integridade. Após a migration, os registros existentes de posições, patrimônio externo, operações, recebimentos, fluxos, fotografias, movimentos e efeitos foram comparados em memória e permaneceram iguais. Nenhum valor pessoal foi incluído nesta documentação.

A inicialização agora detecta alterações de modelo sem migration e migrations pendentes, informando o procedimento necessário. Essa proteção evita o mesmo tipo de desencontro em atualizações normais; não substitui backup nem detecta toda alteração manual feita diretamente no esquema. Consulte o [procedimento de atualização](../README.md).

## Limites da revisão

- O celular foi emulado; a conferência visual no aparelho físico continua a cargo do usuário.
- A criação do esquema foi exercitada em bancos temporários. A instalação completa em outro computador não foi executada.
- Fotografias congeladas não são recalculadas pela complementação de câmbio; a análise usa a versão vigente do equivalente na moeda consultada.
- Recebimentos antigos sem conversão devem ser complementados na página Proventos quando necessário. Nenhuma cotação atual foi utilizada para preencher o passado.
- TWR/XIRR, backup automático, automação dos lançamentos atrasados e publicação continuam pendentes.

Detalhes do fluxo e das regras: [recebimentos e câmbio histórico](recebimento-de-proventos.md).
