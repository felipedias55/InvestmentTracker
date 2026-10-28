# Testes de ponta a ponta

## Verificação de apresentação sem banco

Execute `npm run test:presentation` no diretório do frontend para verificar dashboard, evolução, proventos, filtros e privacidade em desktop e celular emulado. Essa suíte usa apenas dados fictícios e intercepta todas as chamadas da API; não depende do SQL Server. Usa a porta 65455 e gera capturas em `test-results/presentation`. Não substitui os testes integrados descritos abaixo.

Em **27/10/2026**, os **60 cenários de apresentação** passaram nas quatro configurações (desktop/celular, claro/escuro), incluindo recuperação dos filtros analíticos por carteira, reinício após recarga da página e contenção dos gráficos extensos no celular. Também passaram 129 testes Angular e o build de produção. Veja [contexto analítico do Dashboard](contexto-analitico-dashboard.md).

A suíte usa Playwright com Chromium, API real e SQL Server. Cada execução cria uma base `InvestmentTracker_E2E_<guid>`, aplica todas as migrations, executa o seed dos catálogos e remove a base ao terminar, inclusive quando um teste falha. A conexão da aplicação e os registros pessoais não são usados.

## Executar no Windows

Pré-requisitos: .NET 10, Node/npm e SQL Server LocalDB instalado, com permissão para criar e remover bases. Na primeira execução:

```powershell
cd frontend/InvestmentTracker.Web
npm ci
npx playwright install chromium
npm run test:e2e
```

O script inicia a API em `http://127.0.0.1:5053` e o frontend em `http://127.0.0.1:65454`. As portas devem estar livres: a suíte recusa reutilizar servidores existentes. O proxy E2E é separado do proxy usado normalmente no Visual Studio. Não é necessário iniciar a aplicação manualmente.

Se o LocalDB falhar ao iniciar, execute `SqlLocalDB.exe start MSSQLLocalDB` e tente novamente. Para outro servidor SQL com autenticação integrada, execute a partir da raiz:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-EndToEnd.ps1 -SqlInstance 'SERVIDOR\INSTANCIA'
```

O nome da base é sempre gerado pelo script; não há parâmetro para apontar os testes para a carteira real. O bloco de limpeza usa exclusivamente esse nome. Encerrar à força o PowerShell ou desligar o computador pode impedir a limpeza; nesse caso, o nome da base temporária é informado no início da execução.

## Cobertura atual

Os seis cenários abaixo rodam em desktop e em celular emulado (Pixel 7), totalizando 12 testes:

- Atualizar manualmente uma cotação em lote após filtrar a carteira, revisar e confirmar o valor com quatro casas decimais, preservar a posição não preenchida, quantidade, custo e proventos; consultar a variação nos detalhes e conferir o ajuste auditável sem novos aportes.
- Criar carteira vazia, cadastrar e editar ativo em janela, comprar com dinheiro novo, cadastrar saldo externo, vender para esse saldo, reinvestir, vender com retirada e rejeitar venda superior à quantidade disponível. Conferir posições, custo, dinheiro disponível e movimentos para detectar duplicação de aportes. Registrar fotografia, consultar o agrupamento anual, realizar nova compra, confirmar que a fotografia anterior não mudou e substituí-la explicitamente no mês corrente. Registrar proventos com crédito em saldo e fora da carteira, conferindo acumulado, ausência de novos aportes e fotografia preservada.
- Depositar, retirar, transferir entre saldos, ajustar e estornar, conferindo saldos e histórico sem duplicação de aportes. O cenário de operações também estorna proventos e confere a fotografia preservada.
- Recolher um gráfico, verificar persistência após recarregar, restaurar os gráficos e acessar Compras e vendas pelo menu após rolagem. Verificar o botão fixo e ausência de transbordamento horizontal nessa tela.
- Corrigir uma compra com venda dependente, exigir conferência explícita antes da aplicação, validar o recálculo de quantidade/custo/caixa e preservar os valores da fotografia anterior. Registrar um depósito atrasado e verificar novamente caixa, posição e fotografia.
- Começar com carteira vazia, rejeitar metas que não somam 100%, salvar metas de categoria/setor e simular aporte com centavos. Conferir sugestões, valor não distribuído e ausência de alterações nos saldos ou movimentações.

Os percursos acompanham os menus e painéis atuais: `Mais ações` para criar carteira, ações de registro para abrir formulários, detalhes do lançamento para estorno/correção e seções próprias de Fotografias, Recebimentos e Análises. Seletores de campos com nomes compartilhados entre filtro e formulário devem ficar restritos ao diálogo aberto. A restauração dos gráficos preserva o padrão atual, incluindo Movimentações inicialmente recolhido.

As operações usam BRL e valores fixos, sem dependência da disponibilidade da API de câmbio. Cada cenário possui sua própria carteira. Os testes existentes de unidade e integração continuam responsáveis pelos demais casos, como câmbio, precisão decimal, concorrência e comparações entre períodos. Esta suíte não substitui a revisão em aparelhos físicos, nem representa cobertura de todos os navegadores.

## Resultados e diagnóstico

Em **28/10/2026**, lint, formatação de configurações, build, 214 testes unitários .NET, 149 de integração, 130 Angular, 60 cenários de apresentação e 12 E2E foram aprovados localmente. Os artefatos foram separados em `test-results/presentation/` e `test-results/e2e/`, após detectar e corrigir colisões de traces entre execuções simultâneas. A remoção da base temporária E2E foi confirmada. O workflow de [qualidade e CI](qualidade-e-ci.md) está preparado, com primeira execução hospedada ainda a confirmar.

Validação em **26/10/2026**: **12 cenários aprovados** (seis desktop e seis celular emulado) em execução completa de 48,3 segundos, após atualizar os fluxos para o layout atual. A remoção da base temporária foi confirmada pelo script. Foram preservadas as verificações financeiras e de auditoria; não houve alteração do código da aplicação nesta entrega. O aviso conhecido NG0913 sobre o tamanho do logo permanece como pendência de desempenho.

Após a execução E2E, o relatório fica em `frontend/InvestmentTracker.Web/playwright-report/index.html`. Em falhas, `test-results/e2e` contém captura de tela e trace; a suíte de apresentação usa seu próprio diretório. Esses arquivos são ignorados pelo Git.

```powershell
# No diretório do frontend:
npx playwright show-report
```

Validação em 11/09/2026: seis testes E2E, 135 unitários C#, 101 de integração e 52 Angular aprovados; build Angular de produção concluído. Remoção da base temporária E2E confirmada.

Em 13/09/2026, o fluxo financeiro foi ampliado para taxas, desdobramento, reabertura com motivo, preservação de versões e análise anual de proventos. Os seis cenários passaram novamente nas configurações desktop e celular.
