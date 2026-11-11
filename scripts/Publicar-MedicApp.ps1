[CmdletBinding()]
param(
    [string]$ProfilePath,
    [uri]$SiteUrl = 'https://mwzsystems-001-site9.etempurl.com/',
    [switch]$InitializeCalendar,
    [switch]$PrepareOnly
)

$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskProject = Join-Path $taskRoot 'MedicApp\MedicApp.csproj'
if (!(Test-Path -LiteralPath $taskProject)) { throw 'Ejecuta este script desde la carpeta scripts del proyecto MedicApp.' }
if ($SiteUrl.Scheme -ne 'https' -or $SiteUrl.Host -ne 'mwzsystems-001-site9.etempurl.com') { throw 'Esta entrega corresponde exclusivamente al sitio site9 de MedicApp.' }
if (!$PrepareOnly -and !$ProfilePath) { throw 'Indica -ProfilePath con el perfil Web Deploy propio de MedicApp/site9.' }

$taskSecretPath = Join-Path $env:APPDATA 'Microsoft\UserSecrets\MedicApp-Local\secrets.json'
$taskSecrets = Get-Content -LiteralPath $taskSecretPath -Raw | ConvertFrom-Json
$taskConnection = $taskSecrets.'ConnectionStrings:Workforce'
if (!$taskConnection) { $taskConnection = $taskSecrets.ConnectionStrings.Workforce }
if (!$taskConnection) { throw 'Falta la conexión privada de MedicApp.' }
$taskConnectionBuilder = [System.Data.Common.DbConnectionStringBuilder]::new()
$taskConnectionBuilder.set_ConnectionString($taskConnection)
$taskSqlServer = if ($taskConnectionBuilder.ContainsKey('Server')) { $taskConnectionBuilder['Server'] } else { $taskConnectionBuilder['Data Source'] }
$taskSqlDatabase = if ($taskConnectionBuilder.ContainsKey('Database')) { $taskConnectionBuilder['Database'] } else { $taskConnectionBuilder['Initial Catalog'] }
if ($taskSqlServer -ne 'sql1002.site4now.net' -or $taskSqlDatabase -ne 'db_acaa35_trainify') { throw 'La conexión no corresponde a la base común autorizada.' }
$taskDeploySql = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($taskConnection)
# Conexion del sitio (USUARIOS_SQL_POR_APLICACION.md, en Trainify): ConnectionStrings:Workforce es la de despliegue y solo se usa
# desde este equipo. El sitio recibe ConnectionStrings:WorkforceSite, con un usuario SQL propio y de permisos minimos.
$taskSiteConnection = $taskSecrets.'ConnectionStrings:WorkforceSite'; if (!$taskSiteConnection -and $taskSecrets) { $taskSiteConnection = $taskSecrets.ConnectionStrings.WorkforceSite }
if ($taskSiteConnection) {
    $taskSiteSql = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($taskSiteConnection)
    if ($taskSiteSql.DataSource -ne $taskSqlServer -or $taskSiteSql.InitialCatalog -ne $taskSqlDatabase) { throw 'La conexion del sitio apunta a otra base.' }
    if ($taskSiteSql.UserID -eq $taskDeploySql.UserID) { throw 'La conexion del sitio debe usar un usuario distinto del de despliegue.' }
    Write-Output 'CONEXION_SITIO_SEPARADA'
} else {
    $taskSiteConnection = $taskConnection
    Write-Warning 'El sitio seguira usando el usuario de despliegue. Configura su usuario con scripts\Configurar-ConexionSitio.ps1.'
}

$taskStage = Join-Path $env:LOCALAPPDATA ('MedicApp\Publish\' + [Guid]::NewGuid().ToString('N'))
$taskPrivateFile = Join-Path $taskStage 'appsettings.Production.json'
$taskPassword = $null
$taskPasswordBytes = $null
try {
    & dotnet publish $taskProject -c Release --no-restore --nologo -p:UseAppHost=false -p:AspNetCoreHostingModel=OutOfProcess -o $taskStage
    if ($LASTEXITCODE -ne 0) { throw 'No se generó el paquete de publicación.' }
    foreach ($taskRequired in @('MedicApp.dll','MedicApp.runtimeconfig.json','web.config','wwwroot\css\medicapp.css')) {
        if (!(Test-Path -LiteralPath (Join-Path $taskStage $taskRequired))) { throw ('Falta un archivo requerido: ' + $taskRequired) }
    }
    $taskWebConfig = [xml](Get-Content -LiteralPath (Join-Path $taskStage 'web.config') -Raw)
    $taskCore = $taskWebConfig.configuration.location.'system.webServer'.aspNetCore
    if ($taskCore.processPath -ne 'dotnet' -or $taskCore.arguments -notmatch 'MedicApp\.dll' -or $taskCore.hostingModel -ne 'outofprocess') { throw 'El web.config no corresponde al arranque IIS esperado.' }
    if (Test-Path -LiteralPath (Join-Path $taskStage 'App_Data\Keys')) { throw 'El paquete no debe contener claves locales de sesión.' }
    if ($InitializeCalendar) {
        $taskCalendarPath = Join-Path $taskStage 'App_Data\Configuration'
        New-Item -ItemType Directory -Path $taskCalendarPath -Force | Out-Null
        @{Medical=@{BookingHorizonDays=7;Open24Hours=$true;OpeningTime='08:00';ClosingTime='18:00';DefaultSlotMinutes=30;DemoBookingEnabled=$true}} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskCalendarPath 'medical.json') -Encoding UTF8
    }
    $taskProduction = @{ ConnectionStrings = @{ Workforce = $taskSiteConnection }; AllowedHosts = $SiteUrl.Host; Medical = @{ DataDirectory = 'App_Data' } }
    $taskProduction | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $taskPrivateFile -Encoding UTF8
    if ($PrepareOnly) {
        Write-Output ('PAQUETE_PREPARADO: ' + $taskStage)
        Write-Output 'IIS_WEB_CONFIG_VERIFICADO; HOST_PRODUCCION_CONFIGURADO; CONEXION_INCLUIDA_FUERA_DEL_REPOSITORIO'
        return
    }

    $taskXml = [xml](Get-Content -LiteralPath $ProfilePath -Raw)
    if ($taskXml.publishData) {
        $taskCandidates = @($taskXml.publishData.publishProfile | Where-Object { $_.publishMethod -eq 'MSDeploy' -and $_.msdeploySite -eq 'mwzsystems-001-site9' })
        if ($taskCandidates.Count -ne 1) { throw 'El archivo debe contener un perfil MSDeploy único para site9.' }
        $taskProfile = $taskCandidates[0]
        $taskIisSite = [string]$taskProfile.msdeploySite
        $taskEndpoint = [string]$taskProfile.publishUrl
        $taskUserName = [string]$taskProfile.userName
        $taskPassword = [string]$taskProfile.userPWD
    }
    else {
        $taskProfile = $taskXml.Project.PropertyGroup
        if ($taskProfile.WebPublishMethod -ne 'MSDeploy') { throw 'Se requiere un perfil Web Deploy, no FTP.' }
        $taskIisSite = [string]$taskProfile.DeployIisAppPath
        $taskEndpoint = [string]$taskProfile.MSDeployServiceURL
        $taskUserName = [string]$taskProfile.UserName
        # PowerShell 7 la incluye en ProtectedData; Windows PowerShell 5.1 en System.Security.
        try { Add-Type -AssemblyName System.Security.Cryptography.ProtectedData } catch { Add-Type -AssemblyName System.Security }
        $taskEncrypted = ([xml](Get-Content -LiteralPath ($ProfilePath + '.user') -Raw)).Project.PropertyGroup.EncryptedPassword
        $taskPasswordBytes = [Security.Cryptography.ProtectedData]::Unprotect([Convert]::FromBase64String($taskEncrypted), $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
        $taskPassword = [Text.Encoding]::Unicode.GetString($taskPasswordBytes).TrimEnd([char]0)
    }
    if ($taskIisSite -ne 'mwzsystems-001-site9') { throw 'No se permite publicar MedicApp en otro sitio ni subcarpeta.' }
    if ($taskEndpoint -notmatch '^https?://') { $taskEndpoint = 'https://' + $taskEndpoint }
    $taskService = [uri]$taskEndpoint
    if ($taskService.Scheme -ne 'https' -or !$taskService.Host.EndsWith('.site4now.net')) { throw 'Revisar el endpoint HTTPS del proveedor de hosting.' }
    if (!$taskPassword -or !$taskUserName) { throw 'El perfil no contiene credenciales utilizables.' }
    $taskTool = Join-Path $env:ProgramFiles 'IIS\Microsoft Web Deploy V3\msdeploy.exe'
    if (!(Test-Path -LiteralPath $taskTool)) { throw 'Falta Microsoft Web Deploy V3.' }
    $taskDestination = "-dest:iisApp='$taskIisSite',computerName='$taskEndpoint',userName='$taskUserName',password='$taskPassword',authType=Basic"
    $taskArguments = @('-verb:sync', "-source:iisApp='$taskStage'", $taskDestination, '-enableRule:DoNotDeleteRule', '-enableRule:AppOffline', '-retryAttempts:2', '-retryInterval:3000')
    & $taskTool @taskArguments 2>&1 | ForEach-Object {
        $taskLine = $_.ToString().Replace($taskPassword, '[REDACTED]').Replace($taskConnection, '[REDACTED]').Replace($taskSiteConnection, '[REDACTED]')
        if ($taskLine -notmatch '^Info: Adding (file|directory)') { Write-Output $taskLine }
    }
    if ($LASTEXITCODE -ne 0) { throw 'Web Deploy no terminó correctamente.' }
    # La sincronización no borra archivos sobrantes. Lo retirado del proyecto se quita aquí, por ruta fija y solo si el sitio aún lo sirve.
    $taskRetired = @(@{ Probe = '/lib/insove/style.css'; Path = 'wwwroot/lib/insove' })
    foreach ($taskItem in $taskRetired) {
        $taskStillServed = $false
        try { $taskStillServed = (Invoke-WebRequest ([uri]::new($SiteUrl, $taskItem.Probe)) -Method Head -UseBasicParsing -TimeoutSec 30).StatusCode -eq 200 } catch { $taskStillServed = $false }
        if (!$taskStillServed) { continue }
        $taskDelete = @('-verb:delete', "-dest:contentPath='$taskIisSite/$($taskItem.Path)',computerName='$taskEndpoint',userName='$taskUserName',password='$taskPassword',authType=Basic", '-retryAttempts:2', '-retryInterval:3000')
        & $taskTool @taskDelete 2>&1 | ForEach-Object { Write-Output $_.ToString().Replace($taskPassword, '[REDACTED]') }
        if ($LASTEXITCODE -ne 0) { throw ('No se pudo retirar del sitio: ' + $taskItem.Path) }
        Write-Output ('RETIRADO_DEL_SITIO: ' + $taskItem.Path)
        $taskDelete = $null
    }
    $taskHealth = Invoke-WebRequest ([uri]::new($SiteUrl, '/health')) -UseBasicParsing -TimeoutSec 40
    if ($taskHealth.StatusCode -ne 200 -or $taskHealth.Content.Trim() -ne 'Healthy') { throw 'El sitio no confirmó conexión saludable a la base de datos.' }
    $taskLogin = Invoke-WebRequest ([uri]::new($SiteUrl, '/Account/Login')) -UseBasicParsing -TimeoutSec 30
    if ($taskLogin.StatusCode -ne 200 -or $taskLogin.Content -notmatch 'name="Identifier"' -or $taskLogin.Content -notmatch 'MedicApp') { throw 'No se verificó la pantalla de acceso de MedicApp.' }
    $taskRootPage = Invoke-WebRequest $SiteUrl -UseBasicParsing -TimeoutSec 30
    if ($taskRootPage.StatusCode -ne 200 -or $taskRootPage.Content -notmatch 'MedicApp' -or $taskRootPage.Content -match 'temporary homepage') { throw 'La raíz todavía no abre MedicApp.' }
    Write-Output ('MEDICAPP_PUBLICADO_Y_VERIFICADO: ' + $SiteUrl.AbsoluteUri)
}
finally {
    if (Test-Path -LiteralPath $taskPrivateFile) { Remove-Item -LiteralPath $taskPrivateFile -Force }
    if ($taskPasswordBytes) { [Array]::Clear($taskPasswordBytes, 0, $taskPasswordBytes.Length) }
    $taskPassword = $null; $taskConnection = $null; $taskSiteConnection = $null; $taskDeploySql = $null; $taskSecrets = $null; $taskProduction = $null; $taskDestination = $null; $taskArguments = $null
}
