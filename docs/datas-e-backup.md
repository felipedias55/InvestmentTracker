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
$backupSecrets = dotnet user-secrets list --project InvestmentTracker.Api --json | ConvertFrom-Json
$backupFolder = Join-Path (Get-Location) 'backups'
New-Item -ItemType Directory -Force -Path $backupFolder | Out-Null
./scripts/Backup-Database.ps1 -ConnectionString $backupSecrets.'ConnectionStrings:InvestmentTracker' -BackupDirectory $backupFolder
```

Requer SQL Server acessível e permissões de backup, criação/restauração e remoção da base temporária. A pasta precisa existir no host do SQL Server e permitir escrita à conta que o executa. O fluxo foi validado com Windows/LocalDB e arquivos SQL de dados e log.

Os backups são locais e estão ignorados pelo Git. Não há agendamento nem envio para serviços externos. Copie periodicamente um backup validado para outro dispositivo ou armazenamento privado; manter a única cópia no mesmo disco não protege contra falha desse disco.

Para recuperação, restaure o `.bak` em uma **nova base**, usando o assistente de restauração do SQL Server e novos caminhos para seus arquivos. Confira os dados antes de apontar a configuração da API para ela. Este script valida novos backups; não oferece uma ação de sobrescrever a base atual.

## Validação desta entrega

- 133 testes unitários do backend e 76 testes de integração aprovados.
- 37 testes Angular aprovados e build de produção concluído.
- Teste de integração cobre mudança de dia em São Paulo, criação, consulta, edição e rejeição de gravação inválida.
- Backup real após migration restaurado e verificado com sucesso em base temporária.
