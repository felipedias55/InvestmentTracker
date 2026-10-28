# Otimização do carregamento — 26/10/2026

## Resultado

| Medida | Antes | Depois |
| --- | --- | --- |
| Pacote inicial Angular, tamanho bruto | 524,92 kB | 455,76 kB |
| Transferência inicial estimada pelo build | 131,11 kB | 114,61 kB |
| PNG publicado do logo | 808.065 bytes, 1254 × 1254 px | 20.451 bytes, 132 × 132 px |
| Limite de aviso do pacote inicial | 500 kB | 500 kB, preservado |

Redução de aproximadamente 13,2% no pacote inicial e 97,5% no arquivo do logo. O PNG é um asset separado e não está incluído no tamanho dos bundles. Os valores de transferência são estimativas do build, não tempos medidos nem o total de uma rota completa: ao abrir o Dashboard, seus módulos também são carregados.

## Implementação

As estatísticas de `npm run build -- --stats-json` identificaram menu Material e CDK Overlay entre os módulos trazidos à inicialização pela seleção de temas. O seletor passou para `shared/theme-picker.ts`, carregado por `@defer` no primeiro acionamento de Escolher tema. O primeiro clique ou acionamento pelo teclado carrega e abre o menu; os seguintes usam o componente já carregado. O menu mantém as opções, semântica e controle de foco do Material.

O serviço Theme permanece inicializado na estrutura principal: a preferência salva e o tema do sistema são aplicados antes de abrir o seletor. A mudança adia o código de interação do menu, não a aplicação do tema. A primeira abertura pode aguardar a transferência do módulo; as páginas que também usam menus Material podem já ter carregado parte do código compartilhado. Não foi acrescentado prefetch automático para o seletor.

O logo publicado foi reamostrado do original com transparência, em resolução que atende até três vezes os 44 pixels de exibição do cabeçalho. A arte original permanece em `frontend/InvestmentTracker.Web/design/fd-logo-original.png`, fora dos assets publicados. `scripts/Optimize-BrandLogo.ps1` reproduz a versão pequena no Windows usando System.Drawing, sem dependências novas e sem redesenhar a marca. Cabeçalho e favicon conservam o caminho `fd-logo.png`.

Os budgets de produção não foram alterados. O build final deixou de emitir o aviso de pacote inicial acima de 500 kB.

## Validação

- Build de produção com estatísticas aprovado: 455,76 kB iniciais, sem aviso de orçamento.
- 125 testes Angular aprovados em 26 arquivos.
- 56 cenários de apresentação aprovados em execução completa de 2,2 minutos: desktop/celular emulado e temas claro/escuro. O percurso de tema verifica primeiro acionamento por clique, primeiro acionamento por seta para baixo após recarga, preferência persistida, acompanhamento do sistema e retorno do foco com Escape.
- Capturas de cabeçalho e menu conferidas visualmente em desktop/celular; nenhuma emissão de NG0913 foi observada na execução final. Conferidos também o logo no tema claro e a presença apenas da versão pequena no diretório de publicação.
- Script de otimização executado, produzindo novamente o PNG de 20.451 bytes.
- Hash Git do original preservado em design idêntico ao PNG original versionado: `566e29ba0eaf450f4cbc0a492506f95371339f7a`.

As mensagens conhecidas do jsdom sobre APIs de navegador permanecem. Esta entrega não altera cálculos financeiros, API, banco ou dependências. Aparelho físico, auditoria formal de acessibilidade e medição de carregamento em rede real permanecem fora desta comprovação.
