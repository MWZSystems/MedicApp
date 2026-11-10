# Guarda la conexion SQL del sitio de MedicApp (ConnectionStrings:WorkforceSite) en Secret Manager, fuera del repositorio.
# Es la que recibe el sitio al publicar; la de despliegue (ConnectionStrings:Workforce) sigue solo en este equipo.
# Requisitos: el usuario se crea antes en el panel de SmarterASP y se agrega a sus roles con
# database\seguridad\RolesPorAplicacion.sql del repositorio Trainify. Ver USUARIOS_SQL_POR_APLICACION.md en Trainify.
# El servidor, la base y el usuario no estan fijos: la base se toma de la conexion de despliegue de este equipo
# (o de -Server y -Database) y el usuario se escribe al ejecutar. Asi sirve para la base de otro cliente.
param([string]$Server, [string]$Database)
$ErrorActionPreference = 'Stop'
$taskServer = $null; $taskDatabase = $null
$taskProject = Join-Path $PSScriptRoot '..\MedicApp\MedicApp.csproj'
$taskSecretsPath = Join-Path $env:APPDATA ('Microsoft\UserSecrets\' + 'MedicApp-Local' + '\secrets.json')
$taskDeployUser = $null
if (Test-Path -LiteralPath $taskSecretsPath) {
    $taskSecrets = Get-Content -LiteralPath $taskSecretsPath -Raw | ConvertFrom-Json
    $taskDeploy = $taskSecrets.'ConnectionStrings:Workforce'; if (!$taskDeploy) { $taskDeploy = $taskSecrets.ConnectionStrings.Workforce }
    if ($taskDeploy) {
        $taskDeployBuilder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($taskDeploy)
        $taskDeployUser = $taskDeployBuilder.UserID; $taskServer = $taskDeployBuilder.DataSource; $taskDatabase = $taskDeployBuilder.InitialCatalog
        $taskDeployBuilder = $null
    }
    $taskSecrets = $null; $taskDeploy = $null
}
if ($Server) { $taskServer = $Server }
if ($Database) { $taskDatabase = $Database }
if (!$taskServer -or !$taskDatabase) { throw 'No se encontro la conexion de despliegue en este equipo. Indica -Server y -Database.' }
Write-Host ('Base: ' + $taskDatabase + ' en ' + $taskServer)
$taskUser = Read-Host 'Usuario SQL del sitio de MedicApp (por ejemplo complementos_app)'
if ([string]::IsNullOrWhiteSpace($taskUser)) { throw 'Es necesario un usuario.' }
if ($taskUser.Trim() -in @($taskDeployUser)) { throw 'El sitio debe usar un usuario distinto del de despliegue.' }
$taskPassword = Read-Host 'Contrasena de ese usuario' -AsSecureString
$taskPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($taskPassword)
try {
    $taskSql = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
    $taskSql['Data Source'] = $taskServer
    $taskSql['Initial Catalog'] = $taskDatabase
    $taskSql['User ID'] = $taskUser.Trim()
    $taskSql['Password'] = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($taskPointer)
    $taskSql['Encrypt'] = $true
    $taskSql['TrustServerCertificate'] = $true
    $taskSql['Persist Security Info'] = $false
    $taskSql['Connect Timeout'] = 15
    # Prueba la conexion, que el usuario no sea dueno de la base y que ya tenga sus roles app_.
    $taskConnection = [System.Data.SqlClient.SqlConnection]::new($taskSql.ConnectionString)
    try {
        $taskConnection.Open()
        $taskCommand = $taskConnection.CreateCommand()
        $taskCommand.CommandText = "SELECT IS_ROLEMEMBER('db_owner')"
        if ([int]$taskCommand.ExecuteScalar() -eq 1) { throw 'Ese usuario es dueno de la base (db_owner). Quitale ese rol antes de usarlo en el sitio.' }
        $taskCommand.CommandText = "SELECT COUNT(*) FROM sys.database_role_members m JOIN sys.database_principals r ON r.principal_id = m.role_principal_id WHERE m.member_principal_id = USER_ID() AND r.name LIKE 'app[_]%'"
        if ([int]$taskCommand.ExecuteScalar() -eq 0) { throw 'Ese usuario todavia no tiene roles app_ asignados. Asignalos antes de guardar la conexion.' }
    } finally { $taskConnection.Dispose() }
    @{ 'ConnectionStrings:WorkforceSite' = $taskSql.ConnectionString } | ConvertTo-Json -Compress | dotnet user-secrets set --project $taskProject | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo guardar la conexion.' }
    Write-Host 'Conexion del sitio guardada fuera de Git. La siguiente publicacion la usara. No copies la contrasena al chat.'
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($taskPointer)
    $taskSql = $null
    $taskPassword.Dispose()
}
