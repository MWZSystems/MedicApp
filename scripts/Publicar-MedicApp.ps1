[CmdletBinding()]
param(
    [string]$ProfilePath,
    [uri]$SiteUrl = 'https://mwzsystems-001-site9.etempurl.com/',
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
    $taskProduction = @{ ConnectionStrings = @{ Workforce = $taskConnection }; AllowedHosts = $SiteUrl.Host; Medical = @{ DataDirectory = 'App_Data' } }
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
        Add-Type -AssemblyName System.Security.Cryptography.ProtectedData
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
        $taskLine = $_.ToString().Replace($taskPassword, '[REDACTED]').Replace($taskConnection, '[REDACTED]')
        if ($taskLine -notmatch '^Info: Adding (file|directory)') { Write-Output $taskLine }
    }
    if ($LASTEXITCODE -ne 0) { throw 'Web Deploy no terminó correctamente.' }
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
    $taskPassword = $null; $taskConnection = $null; $taskSecrets = $null; $taskProduction = $null; $taskDestination = $null; $taskArguments = $null
}
