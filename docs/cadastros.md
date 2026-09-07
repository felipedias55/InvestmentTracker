# Cadastros — primeiro fluxo da especificação

Implementação das seções 8.1–8.6, 17, 18, 19 e da primeira sequência da seção 42 do documento inicial. A especificação orienta os requisitos; os nomes dos projetos, a solução `.slnx`, o `InvestmentTrackerDbContext` e a organização existente do Visual Studio são mantidos.

## Arquitetura e convenções

- API → contratos/serviços da Application → repositórios específicos implementados na Infrastructure → EF Core/SQL Server.
- Domain independente de EF Core e ASP.NET Core; constantes de tamanho compartilhadas com os mapeamentos.
- Cada cadastro tem seus próprios DTOs, interface de serviço, interface de repositório, serviço, repositório e controller. Não foi introduzido Generic Repository, CQRS ou MediatR.
- Namespaces C# com chaves, configurados também em `.editorconfig`.
- DTOs são records; controllers tratam HTTP, serviços concentram validação e orquestração; consultas e persistência recebem CancellationToken.
- Erros de entrada e conflito usam exceções específicas da Application, traduzidas globalmente em ProblemDetails. O campo `message` continua disponível por compatibilidade.
- O índice único do banco continua sendo a proteção final contra concorrência. A infraestrutura traduz violações SQL específicas sem expor detalhes do banco.
- A atualização usa entidades rastreadas pelo contexto. `SaveChangesAsync` confirma a operação; consultas de listagem usam `AsNoTracking`.

## Contratos

| Recurso | Caminho | Campos de criação/edição |
|---|---|---|
| Tipo | `/api/asset-types` | `name` |
| País | `/api/countries` | `name` |
| Moeda | `/api/currencies` | `code`, `name`, `symbol` opcional |
| Categoria | `/api/asset-categories` | `name` |
| Setor | `/api/sectors` | `name` |
| Ativo | `/api/assets` | `ticker`, `name`, `assetTypeId`, `countryId`, `currencyId`, `assetCategoryId`, `sectorId` |

Todos oferecem GET da coleção, GET por `/{id}`, POST, PUT `/{id}` e DELETE `/{id}`. Criação retorna 201 com Location; edição retorna 200; exclusão retorna 204; registro inexistente retorna 404. Entradas inválidas retornam 400 e conflitos retornam 409.

### Validações

- Nomes dos cadastros de apoio: obrigatórios, normalizados com Trim, até 100 caracteres. Países/categorias/setores/tipos têm nome único conforme a comparação configurada no SQL Server.
- Moeda: código obrigatório, três letras A–Z, normalizado para maiúsculas; código único. Nome obrigatório, até 100 caracteres; símbolo opcional, até 10 caracteres; vazio vira null. Não restringimos o cadastro a BRL/USD.
- Ativo: ticker obrigatório, até 20 caracteres, Trim e maiúsculas, único; nome obrigatório, até 200 caracteres. As cinco referências devem existir. O servidor atribui CreatedAt em UTC na criação e preserva a data na edição.
- País não determina moeda: um ativo classificado no Brasil pode usar USD.
- Exclusões respeitam as FKs existentes, incluindo categorias/setores usados por metas e ativos usados em posições. Uma referência removida entre validação e gravação também resulta em conflito.

## Banco e carga inicial

As tabelas, FKs e índices necessários já constavam na migration inicial; esta etapa não altera o schema. A carga é explícita (`--SeedCatalogs=true`), transacional e identificada pelas chaves naturais únicas, sem sobrescrever registros existentes. Inclui Ação/FII/REIT, Brasil/Estados Unidos, BRL/USD, quatro categorias e oito setores descritos na especificação.

Nenhuma carteira, posição, patrimônio externo ou meta é criada nesta etapa. As diferenças do modelo financeiro inicial (precisão decimal, constraints de valores e metas) deverão ser tratadas junto às respectivas funcionalidades, com migrations próprias.

## Interface

Rotas Angular independentes para os seis cadastros, carregadas sob demanda. Cadastros de apoio compartilham apresentação; ativos têm formulário específico com seleção das cinco referências. O backend mantém a autoridade das regras de negócio.

As telas incluem estados de carregamento e vazio, erros de conexão/validação/conflito, confirmação de exclusão, bloqueio durante gravação e preservação dos dados do formulário quando ocorre erro. As telas se adaptam a desktop e celular. Códigos e IDs do banco não são apresentados como rótulos de classificação.

## Verificação

- Unitários: validação, normalização, duplicidade, ausência de registro e referências, preservação de data e ausência de gravação em operações inválidas.
- Integração: CRUD HTTP, SQL Server real, duplicidade na gravação, exclusão com dependências, referência inválida, seed repetível e aplicação das migrations.
- Angular/Vitest: navegação, validação dos formulários, payloads HTTP, estados vazios e erros, edição, confirmação de exclusão e vínculos para cadastros ausentes.
- Inspeção manual no navegador: carregamento pela API local, navegação e formulário de moedas em viewport mobile.

Os testes de integração usam banco temporário exclusivo, mantêm o padrão xUnit/WebApplicationFactory e não dependem de dados existentes na base de desenvolvimento.
