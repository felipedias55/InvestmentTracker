# Qualidade e integração contínua — 28/10/2026

## Ferramentas e versões

O projeto passa a declarar o SDK .NET em `global.json`: 10.0.401, aceitando patches posteriores da mesma faixa 10.0.4xx e recusando versões de prévia. A versão Node de referência está em `.nvmrc` (24.19.0); o package.json conserva npm 12.0.2. O CI usa essas referências.

ESLint 10 já estava presente, mas sem configuração. A configuração plana `eslint.config.mjs` combina ESLint, typescript-eslint e angular-eslint, incluindo TypeScript de produção, testes, E2E, configurações TS e templates Angular externos ou inline. Dependências de análise ficam em devDependencies, com versões resolvidas no lockfile. `jest-editor-support`, sem uso nos imports do projeto, foi removido junto de suas dependências exclusivas.

## Verificações locais

No diretório `frontend/InvestmentTracker.Web`:

```powershell
npm ci
npm run lint
npm run format:check
npm test -- --watch=false
npm run build
npm run test:presentation
npm run test:e2e
```

`test:e2e` exige Windows/LocalDB, conforme [testes de ponta a ponta](testes-e2e.md). Na raiz, as etapas .NET equivalentes ao CI são:

```powershell
dotnet test tests/InvestmentTracker.UnitTests --configuration Release
dotnet test tests/InvestmentTracker.IntegrationTests --configuration Release
```

A integração usa `ConnectionStrings:TestDatabase` e substitui o nome por uma base própria. O E2E cria outra base exclusiva pelo wrapper. Nenhum comando deve apontar os testes para a carteira pessoal.

## Escopo da análise e da formatação

- `npm run lint` reprova erros e avisos. Usa regras recomendadas, incluindo variáveis/imports não utilizados, expressões sem efeito e literais numéricos que perdem precisão. O build Angular continua responsável pela verificação de tipos e compilação dos templates; esta primeira configuração ESLint não exige análise com informações de tipos.
- `any` permanece permitido apenas nos testes e fixtures E2E. A exigência de migrar injeção por construtor para `inject` foi desativada: ambos continuam suportados e a migração não pertence a esta entrega.
- Comparações `value == null` nos templates continuam permitidas de propósito, cobrindo tanto null quanto undefined nos DTOs. Outras comparações continuam sujeitas à regra de igualdade estrita.
- `npm run format:check` verifica arquivos JSON/MJS/CJS de configuração do frontend, `.prettierrc`, `global.json` e os workflows YAML. `npm run format` formata esse mesmo conjunto. Não foi aplicada reformatação em massa de TS/HTML/CSS existentes, nem esse comando declara que esses fontes seguem integralmente o estilo Prettier. Fins de linha locais são preservados para permitir uso em Windows e Linux.
- Templates usam o conjunto recomendado Angular. Isso não representa auditoria formal de acessibilidade nem habilita automaticamente todas as regras de acessibilidade.

## Correções encontradas pela primeira execução

Foram removidos um import e um argumento de callback não utilizados, e o marcador temporário do simulador passou a usar um if explícito no lugar de uma expressão condicional sem resultado consumido.

O editor de estratégias comparava valores fixos com o literal `999999999999999.99`, que não é representável exatamente como Number em JavaScript. A verificação agora preserva a string decimal e valida até 15 dígitos inteiros e duas casas, correspondendo ao limite do backend. O teste aceita o máximo exato, rejeita 10^15 e uma terceira casa decimal e conserva a string enviada para a API. Percentuais mantêm o limite de 100%.

Os testes jsdom recebem mocks explícitos de scrollTo e confirm (recusa por padrão). Casos de descarte continuam configurando suas próprias respostas. Não há supressão genérica de console ou de erros; os testes de navegador continuam exercitando APIs reais.

## Workflow preparado

`.github/workflows/quality.yml` é executado em push, pull request e acionamento manual, com dois jobs:

| Job | Ambiente | Verificações |
| --- | --- | --- |
| Frontend | Ubuntu 24.04 | npm ci, lint, formatação de configurações, testes Angular, build de produção e apresentação Playwright |
| Backend e dados | Windows 2025 / LocalDB | SDK fixado, testes unitários .NET, integração SQL em base temporária e E2E com API real em outra base temporária |

O workflow usa somente permissão de leitura do repositório, cache de download npm, limites de duração e artefatos de testes com retenção de sete dias. Não recebe conexão de produção, chaves de cotações ou dados pessoais. Não há publicação/deploy.

As suítes Playwright agora isolam suas saídas em `test-results/presentation/` e `test-results/e2e/`. A primeira execução simultânea revelou colisão dos diretórios temporários de trace; separar outputDir evita que a limpeza de uma suíte remova os arquivos da outra. As portas 65455 e 65454 também permanecem separadas.

O workflow foi preparado e sua sintaxe YAML conferida localmente. A primeira execução nos runners do GitHub ainda depende do envio das alterações ao repositório e deve ser acompanhada para confirmar as particularidades dessas imagens. Nesta entrega não houve push, alteração de proteção de branch ou execução remota de Actions.

## Referências de configuração

- [Compatibilidade do typescript-eslint](https://typescript-eslint.io/users/dependency-versions/).
- [Configuração plana e templates inline no angular-eslint](https://github.com/angular-eslint/angular-eslint/blob/main/docs/CONFIGURING_ESLINT.md).
- [setup-dotnet e global.json](https://github.com/actions/setup-dotnet).
- [Ferramentas da imagem Windows 2025](https://github.com/actions/runner-images/blob/main/images/windows/Windows2025-Readme.md).

## Validação desta entrega

Validação local final em **28/10/2026**:

| Verificação | Resultado |
| --- | --- |
| Instalação limpa do lockfile | `npm ci --ignore-scripts --no-audit --no-fund` aprovado; não é auditoria de vulnerabilidades |
| ESLint | Sem erros ou avisos |
| Prettier / sintaxe YAML | Configurações e workflow aprovados |
| .NET Release | 214 unitários e 149 de integração aprovados |
| Angular | 130 testes aprovados em 26 arquivos, sem os avisos jsdom de confirm/scrollTo |
| Apresentação | 60 cenários aprovados em 2,6 minutos |
| E2E com API real | 12 cenários aprovados em 1,2 minuto; remoção da base temporária confirmada |
| Build Angular | Aprovado, pacote inicial de 455,77 kB, abaixo do orçamento preservado de 500 kB |

Total: **565 testes/cenários aprovados**. A repetição das duas suítes de navegador ocorreu simultaneamente, com diretórios independentes, confirmando a correção da colisão dos traces. Os testes com banco usaram somente bases temporárias. O CI hospedado continua aguardando sua primeira execução no GitHub.
