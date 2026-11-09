# Cadastros e imagens dos ativos

Primeira entrega implementada em 03/11/2026, a partir da [revisão dos sete cadastros](proposta-simplificacao-cadastros.md).

## Fluxo disponível

O menu reúne Tipos de Ativo, Categorias e Setores em **Classificações**, e Países e Moedas em **Referências**. As rotas anteriores continuam disponíveis. As entidades e seus IDs permanecem separados: instrumento, agrupamento da estratégia, atividade econômica, país e moeda têm responsabilidades diferentes.

No editor de Ativos, cada seletor permite criar uma opção sem abandonar o formulário. O cadastro auxiliar é salvo imediatamente e selecionado no ativo; nome, ticker, demais campos e imagem pendente são preservados. Cancelar o ativo posteriormente não apaga o cadastro auxiliar criado. Os formulários mantêm a proteção contra perda de alterações.

## Imagem opcional

O usuário seleciona uma imagem PNG, JPEG ou WebP de até 5 MB. O navegador preserva as proporções e centraliza a imagem em PNG de 128 × 128 pixels. A imagem só é aplicada ao salvar o ativo; pode ser removida ou a alteração pode ser desfeita antes de salvar.

### Ajuste de enquadramento — 04/11/2026

Após selecionar um arquivo, a prévia permite ampliar ou reduzir de 50% a 400%, posicionar horizontal e verticalmente apenas por arraste com mouse/toque; o zoom usa o controle Material. As proporções são preservadas; o quadrado da prévia define o recorte. **Redefinir enquadramento** retorna ao ajuste inicial, usando o arquivo original selecionado durante aquela edição, sem acumular recortes sucessivos.

Para uma imagem existente ou sugerida já aplicada ao formulário, **Ajustar enquadramento** abre os mesmos controles. Abrir a imagem não modifica o rascunho; ajustar gera um PNG pendente. **Desfazer alteração da imagem** retorna à imagem confirmada. Apenas **Salvar ativo** grava o resultado, com a versão esperada e as validações já existentes. O controle de zoom tem operação por teclado e o arraste fica restrito à prévia, preservando a rolagem no restante do formulário.

A reedição usa a imagem local de 128 × 128 que foi salva, não consulta provedores nem consome suas cotas. Para recuperar detalhes descartados por um recorte já confirmado, é necessário selecionar novamente o arquivo original: ele permanece apenas em memória durante a edição. A imagem decodificada é liberada ao trocar, remover, desfazer ou fechar o editor; respostas antigas não substituem uma escolha mais recente.

O PNG resultante continua usando o padrão de apresentação do catálogo, carteira e dashboard. Não há nova migration, alteração de contratos da API ou de dados financeiros.

Validação concluída com 178 testes Angular, oito cenários visuais de desktop/celular e claro/escuro, dois cenários E2E focados, lint e build de produção (456,83 kB iniciais). Os E2E verificaram os bytes do PNG salvo contra a prévia, reedição sem gravação antecipada, desfazer e preservação financeira. A base temporária foi removida pelo wrapper após a execução. As capturas e verificações visuais também cobrem zoom por teclado, arraste, redefinição e ausência de transbordamento horizontal.

Um componente compartilhado mostra imagem, nome e ticker no catálogo de ativos, nas posições da carteira e nos detalhes da posição. O espaço reservado é de 32 pixels nas listas e 40 nos detalhes, com fundo branco, borda e enquadramento uniforme nos dois temas. Imagens ausentes ou com falha usam as iniciais do ticker, mantendo nome e ticker legíveis.

As miniaturas exibem o PNG sem espaçamento interno adicional. O `padding` de 3 pixels que criava uma faixa branca além do enquadramento foi removido do componente compartilhado; a correção vale também para imagens já salvas, sem reprocessamento ou nova consulta aos provedores. O contorno externo discreto e as proporções continuam preservados.

A revisão para posicionamento somente por arraste e miniaturas sem margem adicional passou nos 178 testes Angular, oito cenários visuais e dois E2E focados, além de lint e build. Os cenários verificam que existe apenas o controle de zoom, que arrastar altera o recorte e que o PNG exibido é o salvo. A execução E2E usou conexão direta com o processo SQL já ativo após falha de descoberta do LocalDB; a base temporária foi removida e a configuração do aplicativo permaneceu preservada.

A primeira entrega oferecia seleção manual. A [busca assistida com imagens sugeridas](cadastro-assistido-e-saldos.md) foi implementada posteriormente. Exibir as imagens já confirmadas continua sem consumir consultas dos provedores.

O Dashboard também exibe a imagem de 32 pixels **à esquerda do ticker** nas maiores altas e quedas e no ranking completo, conforme o padrão dos ativos e a orientação mais recente do usuário. Essa variante reutiliza a imagem, o enquadramento e a alternativa local do componente compartilhado. A ordenação, os percentuais, a escala das barras e a privacidade dos valores permanecem preservados; não exige outra migration.

Esse complemento passou nos 170 testes Angular, lint e build, e em oito cenários de apresentação do ranking/cadastro em desktop/celular e claro/escuro. A verificação da posição foi atualizada para a imagem à esquerda do código; também são cobertos a identidade pelo ID do ativo, a falha de carregamento e o ranking completo.

A correção da ordem passou nos 174 testes Angular atuais, nos oito cenários de apresentação focados, lint e build, sem necessidade de nova migration.

## Persistência e compatibilidade

A imagem fica na tabela `AssetImage`, separada das consultas de catálogo/posição, com limite de 256 KB. O backend valida estrutura PNG, dimensões, CRC e descompressão limitada. O ativo contém apenas a versão SHA-256; o endpoint de imagem usa URL versionada, cache privado e ETag/304. Apenas imagens locais da aplicação são exibidas.

Metadados e imagem são salvos na mesma transação. Clientes que omitem o campo de alteração da imagem preservam a imagem atual. Substituir/remover exige a versão esperada; uma alteração concorrente retorna conflito, enquanto repetir o mesmo conteúdo é seguro. Excluir o ativo remove a imagem associada, respeitando as restrições financeiras existentes. Backups do banco passam a incluir as imagens.

O trabalho não altera operações, quantidades, custos, proventos, metas, estratégias ou fotografias. A imagem pertence ao ativo e é compartilhada entre suas posições.

## Migration

Migration aditiva: `20261103191958_AssetImages`. Não foi aplicada ao banco pessoal. Para atualizar, na raiz do projeto:

```powershell
dotnet ef database update --project InvestmentTracker.Infrastructure --startup-project InvestmentTracker.Api
```

## Validação

301 testes unitários do backend, 183 de integração e 170 testes Angular aprovados. A integração cobre armazenamento, versão/cache, rejeição de dados inválidos, conflitos, remoção e preservação financeira. Os testes de navegador cobrem criação contextual, preservação do formulário, normalização de imagem e identidade na carteira em desktop/celular. As integrações usam bancos descartáveis, sem migrar a base pessoal.

Na máquina local, a execução simultânea das suítes excedeu a memória disponível. O Angular passou com dois workers e limite de heap de 4 GB; a suíte de apresentação aprovou 79 de 80 cenários na repetição, e os oito cenários de cadastros passaram em execução isolada, incluindo o cenário restante. Os seletores E2E foram atualizados para a navegação por Classificações. Essas repetições mantiveram as verificações e os limites de tempo existentes.

Os 24 cenários E2E foram aprovados: 22 na rodada completa e os dois cenários de classificações na repetição após corrigir o seletor do link. As bases temporárias foram removidas pelos respectivos wrappers. O tratamento final de erro de compressão também passou nos 301 testes unitários e nos dois testes de integração específicos de imagens.

Lint, formatação de configurações e build de produção aprovados. O build final registra 456,64 kB iniciais, abaixo do limite de 500 kB.

## Continuação da revisão

- [Busca por ticker com mercado explícito, revisão de metadados/imagens e cotas compartilhadas implementada](cadastro-assistido-e-saldos.md).
- Referências BR/US prontas para seleção implementadas, preservando cadastros existentes.
- Revisão de Saldos e patrimônio externo implementada, preservando contas, saldos e histórico.
- Resultados visuais disponíveis na busca assistida; [seletores padronizados](seletores-visuais-de-ativos.md) disponíveis em compras/vendas, posições, recebimentos, eventos societários e correções atrasadas.

O módulo de gastos continua separado e requer definição funcional própria.
