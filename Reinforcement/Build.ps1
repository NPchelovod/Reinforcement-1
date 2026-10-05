param(
    [string]$RevitInstallDir = "$env:ProgramW6432\Autodesk\Revit 2024",
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [ValidateSet('x64', 'AnyCPU')][string]$Platform = 'x64',
    [string]$MSBuildPath,
    [string[]]$AdditionalMSBuildArguments = @(),
    [switch]$AllowUnsignedBuild
)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath (Join-Path $RevitInstallDir 'RevitAPI.dll'))) { throw 'Не найден Revit 2024. Укажите -RevitInstallDir.' }
$solutionRoot = Split-Path -Parent $PSScriptRoot
$tools = Join-Path $solutionRoot '.build-tools'
New-Item -ItemType Directory -Path $tools -Force | Out-Null
$nuget = Join-Path $tools 'nuget.exe'
if (!(Test-Path -LiteralPath $nuget)) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest 'https://dist.nuget.org/win-x86-commandline/v6.12.1/nuget.exe' -OutFile $nuget -UseBasicParsing
}
& $nuget restore (Join-Path $PSScriptRoot 'packages.config') -PackagesDirectory (Join-Path $solutionRoot 'packages') -NonInteractive
if ($LASTEXITCODE -ne 0) { throw 'Не восстановлены зависимости NuGet. Распакуйте комплект в короткий путь, например C:\EC_BIM.' }
$msbuildCommand = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
$msbuild = if ($MSBuildPath) { $MSBuildPath } elseif ($msbuildCommand) { $msbuildCommand.Source } else { $null }
if (!$msbuild) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    }
}
if (!$msbuild) { throw 'Нужна Visual Studio 2022 с компонентом разработки классических приложений .NET и Developer Pack .NET Framework 4.8.' }
$signPlugin = if ($AllowUnsignedBuild) { 'false' } else { 'true' }
& $msbuild (Join-Path $PSScriptRoot 'Reinforcement.csproj') /t:Rebuild "/p:Configuration=$Configuration" "/p:Platform=$Platform" "/p:SignPlugin=$signPlugin" "/p:RevitInstallDir=$RevitInstallDir" @AdditionalMSBuildArguments /nologo /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Сборка завершилась с ошибками.' }
Write-Output (Join-Path $PSScriptRoot "bin\$Configuration\Reinforcement.dll")


