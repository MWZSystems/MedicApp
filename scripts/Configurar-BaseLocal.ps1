[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\MedicApp\MedicApp.csproj'
$serverName = Read-Host 'Servidor SQL'
$databaseName = Read-Host 'Base de datos compartida'
$loginName = Read-Host 'Cuenta SQL propia de MedicApp'
$passwordValue = Read-Host 'Contraseña SQL' -AsSecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($passwordValue)
try {
    $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
    $builder['Server'] = $serverName
    $builder['Database'] = $databaseName
    $builder['User ID'] = $loginName
    $builder['Password'] = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    $builder['Encrypt'] = 'True'
    $builder['TrustServerCertificate'] = 'False'
    $payload = @{ 'ConnectionStrings:Workforce' = $builder.get_ConnectionString() } | ConvertTo-Json -Compress
    $payload | dotnet user-secrets set --project $project
    if ($LASTEXITCODE -ne 0) { throw 'No fue posible guardar la configuración local.' }
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    $payload = $null
    $builder = $null
    $passwordValue.Dispose()
}
Write-Host 'Configuración guardada fuera del repositorio. No se instaló ni modificó la base.'
