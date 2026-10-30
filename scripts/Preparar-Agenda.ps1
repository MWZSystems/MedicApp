[CmdletBinding()]
param([Parameter(Mandatory)][string]$AdminUserName, [string]$CalendarAdminUserName)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskSecretPath = Join-Path $env:APPDATA 'Microsoft\UserSecrets\MedicApp-Local\secrets.json'
$taskSecrets = Get-Content -LiteralPath $taskSecretPath -Raw | ConvertFrom-Json
$taskConnection = $taskSecrets.'ConnectionStrings:Workforce'
if (!$taskConnection) { $taskConnection = $taskSecrets.ConnectionStrings.Workforce }
if (!$taskConnection) { throw 'Falta la conexión privada de MedicApp.' }
$taskBuilder = [System.Data.Common.DbConnectionStringBuilder]::new()
$taskBuilder.set_ConnectionString($taskConnection)
$taskServer = if ($taskBuilder.ContainsKey('Server')) { $taskBuilder['Server'] } else { $taskBuilder['Data Source'] }
$taskDatabase = if ($taskBuilder.ContainsKey('Database')) { $taskBuilder['Database'] } else { $taskBuilder['Initial Catalog'] }
if ($taskServer -ne 'sql1002.site4now.net' -or $taskDatabase -ne 'db_acaa35_trainify') { throw 'Destino distinto de la base compartida autorizada.' }
$taskPreviousCalendarAdmin = $env:Bootstrap__CalendarAdminUserName
$taskPreviousActor = $env:Bootstrap__UserName
$taskPreviousConnection = $env:ConnectionStrings__Workforce
try {
    $env:Bootstrap__CalendarAdminUserName = $CalendarAdminUserName
    $env:Bootstrap__UserName = $AdminUserName
    $env:ConnectionStrings__Workforce = $taskConnection
    Push-Location (Join-Path $taskRoot 'MedicApp')
    try {
        & dotnet bin/Release/net10.0/MedicApp.dll --prepare-booking
        if ($LASTEXITCODE -ne 0) { throw 'No se completó la generación DEMO.' }
    } finally { Pop-Location }
} finally {
    $env:Bootstrap__CalendarAdminUserName = $taskPreviousCalendarAdmin
    $env:Bootstrap__UserName = $taskPreviousActor
    $env:ConnectionStrings__Workforce = $taskPreviousConnection
    $taskConnection = $null; $taskSecrets = $null; $taskBuilder = $null
}
