param([string]$SqlInstance = '(localdb)\MSSQLLocalDB')

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$databaseName = 'InvestmentTracker_E2E_' + [Guid]::NewGuid().ToString('N')
$previousConnection = $env:ConnectionStrings__InvestmentTracker
$previousDatabase = $env:INVESTMENT_E2E_DATABASE
$connection = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$connection['Data Source'] = $SqlInstance
$connection['Initial Catalog'] = $databaseName
$connection['Integrated Security'] = $true
$connection['TrustServerCertificate'] = $true
$connection['Connect Timeout'] = 15
$testExitCode = 1
try {
    $env:ConnectionStrings__InvestmentTracker = $connection.ConnectionString
    $env:INVESTMENT_E2E_DATABASE = $databaseName
    Write-Host "Base temporária: $databaseName"
    Push-Location $projectRoot
    try {
        & dotnet run --project InvestmentTracker.Api --no-launch-profile -- --SeedCatalogs true --Logging:LogLevel:Default Warning
        if ($LASTEXITCODE -ne 0) { throw 'Não foi possível preparar a base temporária.' }
    } finally { Pop-Location }
    Push-Location (Join-Path $projectRoot 'frontend/InvestmentTracker.Web')
    try {
        & npx.cmd playwright test
        $testExitCode = $LASTEXITCODE
    } finally { Pop-Location }
} finally {
    $env:ConnectionStrings__InvestmentTracker = $previousConnection
    $env:INVESTMENT_E2E_DATABASE = $previousDatabase
    # The target is generated here, never taken from a user connection string.
    if ($databaseName -notmatch '^InvestmentTracker_E2E_[a-f0-9]{32}$') {
        throw 'Nome de base temporária inválido; limpeza recusada.'
    }
    $connection['Initial Catalog'] = 'master'
    $sql = New-Object System.Data.SqlClient.SqlConnection($connection.ConnectionString)
    try {
        $sql.Open()
        $command = $sql.CreateCommand()
        $command.CommandText = "IF DB_ID(@name) IS NOT NULL BEGIN ALTER DATABASE [$databaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$databaseName]; END"
        $null = $command.Parameters.AddWithValue('@name', $databaseName)
        $null = $command.ExecuteNonQuery()
        Write-Host "Base temporária removida: $databaseName"
    } finally { $sql.Dispose() }
}
exit $testExitCode
