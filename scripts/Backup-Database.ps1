param(
    [Parameter(Mandatory = $true)][string]$ConnectionString,
    [Parameter(Mandatory = $true)][string]$BackupDirectory
)
# The directory is on the SQL Server host and must already exist.
# Only the newly generated validation database may be removed by this script.
$ErrorActionPreference = 'Stop'
$builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($ConnectionString)
$sourceDatabase = $builder.InitialCatalog
if ([string]::IsNullOrWhiteSpace($sourceDatabase) -or $sourceDatabase -in @('master', 'model', 'msdb', 'tempdb')) {
    throw 'Informe uma base de aplicação na conexão.'
}
function SqlLiteral([string]$value) { return "N'" + $value.Replace("'", "''") + "'" }
function SqlIdentifier([string]$value) { return '[' + $value.Replace(']', ']]') + ']' }
$builder['Initial Catalog'] = 'master'
$connection = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
function Execute([string]$sql) {
    $command = $connection.CreateCommand()
    try { $command.CommandText = $sql; $command.CommandTimeout = 600; [void]$command.ExecuteNonQuery() }
    finally { $command.Dispose() }
}
function Query([string]$sql) {
    $command = $connection.CreateCommand()
    $adapter = [System.Data.SqlClient.SqlDataAdapter]::new($command)
    try {
        $command.CommandText = $sql; $command.CommandTimeout = 600
        $table = [System.Data.DataTable]::new()
        [void]$adapter.Fill($table)
        return ,$table
    } finally { $adapter.Dispose(); $command.Dispose() }
}
$validationDatabase = 'InvestmentTracker_RestoreCheck_' + [guid]::NewGuid().ToString('N')
$backupFile = Join-Path $BackupDirectory ('InvestmentTracker_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '_' + [guid]::NewGuid().ToString('N') + '.bak')
$backupLiteral = SqlLiteral $backupFile
$validationId = SqlIdentifier $validationDatabase
$created = $false
try {
    $connection.Open()
    Execute "BACKUP DATABASE $(SqlIdentifier $sourceDatabase) TO DISK = $backupLiteral WITH COPY_ONLY, CHECKSUM;"
    Execute "RESTORE VERIFYONLY FROM DISK = $backupLiteral WITH CHECKSUM;"
    $files = Query "RESTORE FILELISTONLY FROM DISK = $backupLiteral;"
    $directories = Query "SELECT physical_name, type FROM sys.master_files WHERE database_id = DB_ID($(SqlLiteral $sourceDatabase)) AND type IN (0, 1);"
    $dataDirectory = Split-Path ($directories.Rows | Where-Object { $_.type -eq 0 } | Select-Object -First 1).physical_name
    $logDirectory = Split-Path ($directories.Rows | Where-Object { $_.type -eq 1 } | Select-Object -First 1).physical_name
    $moves = @()
    foreach ($file in $files.Rows) {
        if ($file.Type -notin @('D', 'L')) { throw 'Validação automática suporta apenas arquivos SQL de dados e log.' }
        $directory = if ($file.Type -eq 'L') { $logDirectory } else { $dataDirectory }
        $destination = Join-Path $directory ($validationDatabase + '_' + $file.FileId + '.dat')
        $moves += "MOVE $(SqlLiteral $file.LogicalName) TO $(SqlLiteral $destination)"
    }
    Execute "IF DB_ID($(SqlLiteral $validationDatabase)) IS NOT NULL THROW 51000, 'Base de validação já existe.', 1;"
    Execute "RESTORE DATABASE $validationId FROM DISK = $backupLiteral WITH CHECKSUM, RECOVERY, $($moves -join ', ');"
    $created = $true
    $check = Query "DBCC CHECKDB ($validationId) WITH NO_INFOMSGS, TABLERESULTS;"
    if ($check.Rows.Count -gt 0) { throw 'A base restaurada apresentou problemas no CHECKDB.' }
    $dates = Query "SELECT 'PortfolioAsset' AS Entity, COUNT(*) AS Records, COUNT(UpdatedOn) AS DatedRecords FROM $validationId.dbo.PortfolioAsset UNION ALL SELECT 'ExternalAsset', COUNT(*), COUNT(UpdatedOn) FROM $validationId.dbo.ExternalAsset;"
    foreach ($row in $dates.Rows) {
        if ($row.Records -ne $row.DatedRecords) { throw 'Existem registros sem data na cópia restaurada.' }
    }
    [pscustomobject]@{ BackupFile = $backupFile; Database = $sourceDatabase; RestoredAndChecked = $true }
} finally {
    if ($created -and $connection.State -eq 'Open') {
        Execute "ALTER DATABASE $validationId SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE $validationId;"
    }
    $connection.Dispose()
}
