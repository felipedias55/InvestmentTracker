# Datas de atualização e backup local

## Datas

Posições e patrimônio externo possuem `UpdatedOn` (SQL `date`). A API atribui o dia de São Paulo ao criar ou salvar o registro, usando `TimeProvider`. Consultar dados ou atualizar o câmbio não altera essa data. Salvar novamente, mesmo sem mudar valores, confirma a atualização do registro. Essa informação indica a última gravação manual, não a data de uma cotação de mercado.

A migration `20260908182342_PositionAndExternalAssetUpdateDates` preenche os registros existentes com **08/09/2026**, conforme autorizado. Novos registros recebem a data corrente pelo serviço. Não há valor padrão fixo no banco.

A data aparece na carteira, no patrimônio externo e nos detalhes de novas fotografias. Fotografias anteriores permanecem inalteradas e mostram “Não informado nesta fotografia”.

Após atualizar o código, aplique migrations e reinicie a API e o frontend. Nesta instalação, a migration já foi aplicada em 08/09/2026.

## Backup completo

O script `scripts/Backup-Database.ps1` cria um arquivo `.bak` com todos os dados, incluindo datas, metas, câmbio, fotografias e movimentos. O nome é único; backups anteriores não são sobrescritos. Usa `COPY_ONLY` e `CHECKSUM`.

Além de `RESTORE VERIFYONLY`, o script restaura o backup em uma nova base de nome aleatório, executa `DBCC CHECKDB`, confere a presença de datas e remove somente essa base temporária. A base original não é substituída. Se uma restauração falhar parcialmente, pode ser necessário remover manualmente a base temporária identificada no erro, após conferir seu nome.

Execute no PowerShell, a partir da raiz do projeto, usando a conexão já configurada:

```powershell
./scripts/Backup-Database.ps1
```

Sem parâmetros, o script lê a conexão diretamente dos User Secrets do projeto no Windows e cria a pasta backups na raiz. Isso evita depender da saída do comando dotnet user-secrets e de sua compatibilidade com ConvertFrom-Json no Windows PowerShell. A conexão não é exibida. Os parâmetros -ConnectionString e -BackupDirectory continuam disponíveis para uso explícito; uma pasta explícita deve existir no host do SQL Server.

Requer SQL Server acessível e permissões de backup, criação/restauração e remoção da base temporária. A pasta precisa existir no host do SQL Server e permitir escrita à conta que o executa. O fluxo foi validado com Windows/LocalDB e arquivos SQL de dados e log.

Os backups são locais e estão ignorados pelo Git. Não há agendamento nem envio para serviços externos. Copie periodicamente um backup validado para outro dispositivo ou armazenamento privado; manter a única cópia no mesmo disco não protege contra falha desse disco.

Para recuperação, restaure o `.bak` em uma **nova base**, usando o assistente de restauração do SQL Server e novos caminhos para seus arquivos. Confira os dados antes de apontar a configuração da API para ela. Este script valida novos backups; não oferece uma ação de sobrescrever a base atual.

## Restaurar em outra máquina ou recuperar uma cópia

1. Instale os requisitos do projeto (.NET 10 e Node.js), SQL Server/LocalDB e SQL Server Management Studio (SSMS). Use preferencialmente a mesma versão do SQL Server da origem. Copie o projeto e o arquivo `.bak` separadamente: o backup contém dados pessoais e não deve entrar no Git.
2. Pare a API se estiver recuperando uma instalação existente. No SSMS, conecte à instância de destino; para LocalDB, use `(localdb)\MSSQLLocalDB` com autenticação Windows.
3. Em **Bancos de Dados → Restaurar Banco de Dados**, selecione **Origem → Dispositivo** e adicione o `.bak`. Informe um nome novo no destino, por exemplo `InvestmentTracker_Restaurado`.
4. Na página **Arquivos**, ajuste os caminhos dos arquivos de dados e log para pastas existentes no destino. Use nomes de arquivos que não coincidam com arquivos de outro banco. Confirme a restauração. A conta do SQL Server precisa ter acesso ao backup e às pastas de destino.
5. Na raiz do projeto, configure a conexão para o banco restaurado (adapte o servidor se não usar LocalDB):

```powershell
dotnet user-secrets set "ConnectionStrings:InvestmentTracker" "Server=(localdb)\MSSQLLocalDB;Database=InvestmentTracker_Restaurado;Trusted_Connection=True;TrustServerCertificate=True" --project InvestmentTracker.Api
```

6. Com uma versão do código compatível com o backup, aplique migrations pendentes se o código for mais recente:

```powershell
dotnet ef database update --project InvestmentTracker.Infrastructure --startup-project InvestmentTracker.Api
```

Se a ferramenta ainda não estiver instalada, a versão correspondente às dependências atuais é:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.11
```

7. Os cadastros já estão no backup; não é necessário executar `SeedCatalogs`. Execute `npm ci` na pasta `frontend/InvestmentTracker.Web` no primeiro uso. Abra a solução no Visual Studio, inicie o projeto e confira os dados. Para execução pelo terminal, consulte o README.

O mesmo procedimento permite recuperar uma cópia na máquina original, preservando o banco anterior. Restaurar recupera o estado na data do backup, sem mesclar lançamentos posteriores. User Secrets não são transportados pelo Git nem pelo backup do banco; configure-os novamente no destino. O script de backup valida a restauração, mas não substitui o banco em uso.

Referência: [restaurar para outro local no SQL Server](https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/restore-a-database-to-a-new-location-sql-server).

## Validação da entrega original de datas e backup

- 133 testes unitários do backend e 76 testes de integração aprovados.
- 37 testes Angular aprovados e build de produção concluído.
- Teste de integração cobre mudança de dia em São Paulo, criação, consulta, edição e rejeição de gravação inválida.
- Backup real após migration restaurado e verificado com sucesso em base temporária.
