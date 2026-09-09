# Testes de ponta a ponta

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

Os dois cenários abaixo rodam em desktop e em celular emulado (Pixel 7), totalizando quatro testes:

- Criar carteira vazia, cadastrar e editar ativo em janela, comprar com dinheiro novo, cadastrar saldo externo, vender para esse saldo, reinvestir, vender com retirada e rejeitar venda superior à quantidade disponível. Conferir posições, custo, dinheiro disponível e movimentos para detectar duplicação de aportes. Registrar fotografia, consultar o agrupamento anual, realizar nova compra, confirmar que a fotografia anterior não mudou e substituí-la explicitamente no mês corrente. Registrar proventos com crédito em saldo e fora da carteira, conferindo acumulado, ausência de novos aportes e fotografia preservada.
- Recolher um gráfico, verificar persistência após recarregar, restaurar os gráficos e acessar Compras e vendas pelo menu após rolagem. Verificar o botão fixo e ausência de transbordamento horizontal nessa tela.

As operações usam BRL e valores fixos, sem dependência da disponibilidade da API de câmbio. Cada cenário possui sua própria carteira. Os testes existentes de unidade e integração continuam responsáveis pelos demais casos, como câmbio, precisão decimal, concorrência e comparações entre períodos. Esta suíte não substitui a revisão em aparelhos físicos, nem representa cobertura de todos os navegadores.

## Resultados e diagnóstico

Após a execução, o relatório fica em `frontend/InvestmentTracker.Web/playwright-report/index.html`. Em falhas, `test-results` contém captura de tela e trace. Esses arquivos são ignorados pelo Git.

```powershell
# No diretório do frontend:
npx playwright show-report
```

Validação em 09/09/2026: quatro testes E2E, 133 unitários C#, 93 de integração e 50 Angular aprovados; build Angular de produção concluído. Remoção da base temporária E2E confirmada.
