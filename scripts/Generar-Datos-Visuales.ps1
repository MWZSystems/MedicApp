[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$UserName,
    [Parameter(Mandatory)][string]$EmployeeNumber,
    [Parameter(Mandatory)][string]$ActorUserName,
    [Parameter(Mandatory)][ValidatePattern('^\d{4}-\d{2}-01$')][string]$FirstMonth,
    [Parameter(Mandatory)][string]$TimeZoneId
)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskSecretPath=Join-Path $env:APPDATA 'Microsoft\UserSecrets\MedicApp-Local\secrets.json'
$taskSecrets=Get-Content -LiteralPath $taskSecretPath -Raw | ConvertFrom-Json
$taskConnection=$taskSecrets.'ConnectionStrings:Workforce'
if(!$taskConnection){$taskConnection=$taskSecrets.ConnectionStrings.Workforce}
if(!$taskConnection){throw 'Falta la conexión privada de MedicApp.'}
$taskBuilder=[System.Data.Common.DbConnectionStringBuilder]::new();$taskBuilder.set_ConnectionString($taskConnection)
$taskServer=if($taskBuilder.ContainsKey('Server')){$taskBuilder['Server']}else{$taskBuilder['Data Source']}
$taskDatabase=if($taskBuilder.ContainsKey('Database')){$taskBuilder['Database']}else{$taskBuilder['Initial Catalog']}
if($taskServer -ne 'sql1002.site4now.net' -or $taskDatabase -ne 'db_acaa35_trainify'){throw 'Destino distinto de la base común autorizada.'}
$taskValues=@{'ConnectionStrings__Workforce'=$taskConnection;'VisualSamples__UserName'=$UserName;'VisualSamples__EmployeeNumber'=$EmployeeNumber;'VisualSamples__ActorName'=$ActorUserName;'VisualSamples__FirstMonth'=$FirstMonth;'VisualSamples__TimeZoneId'=$TimeZoneId}
$taskPrevious=@{}
try{
    foreach($taskKey in $taskValues.Keys){$taskPrevious[$taskKey]=[Environment]::GetEnvironmentVariable($taskKey,'Process');[Environment]::SetEnvironmentVariable($taskKey,$taskValues[$taskKey],'Process')}
    Push-Location (Join-Path $taskRoot 'MedicApp')
    try{
        & dotnet bin/Release/net10.0/MedicApp.dll --create-personal-samples
        if($LASTEXITCODE -ne 0){throw 'No se completó el lote visual; verifica la cuenta, actor, periodo y doctor DEMO.'}
    }finally{Pop-Location}
}finally{
    foreach($taskKey in $taskValues.Keys){[Environment]::SetEnvironmentVariable($taskKey,$taskPrevious[$taskKey],'Process')}
    $taskConnection=$null;$taskSecrets=$null;$taskBuilder=$null;$taskValues=$null
}
