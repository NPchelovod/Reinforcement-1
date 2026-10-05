param([Parameter(Mandatory = $true)][string]$TargetPath)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
# Authenticode signing remains a PostBuildEvent in Reinforcement.csproj.
# Keep the PFX and its password outside the source/output package.
$signTool = $env:EC_BIM_SIGNTOOL_PATH
if ([string]::IsNullOrWhiteSpace($signTool)) {
    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($command) { $signTool = $command.Source }
}
if ([string]::IsNullOrWhiteSpace($signTool)) {
    $sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (Test-Path -LiteralPath $sdkRoot) {
        $signTool = Get-ChildItem -LiteralPath $sdkRoot -Directory |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName 'x64\signtool.exe' } |
            Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    }
}
if ([string]::IsNullOrWhiteSpace($signTool) -or !(Test-Path -LiteralPath $signTool)) {
    throw 'SignTool not found. Install Windows SDK or set EC_BIM_SIGNTOOL_PATH to signtool.exe.'
}
if (!(Test-Path -LiteralPath $TargetPath -PathType Leaf)) { throw 'Signing target DLL not found.' }
$signArgs = @('sign', '/fd', 'SHA256')
if (![string]::IsNullOrWhiteSpace($env:EC_BIM_SIGN_CERT_THUMBPRINT)) {
    $signArgs += @('/sha1', $env:EC_BIM_SIGN_CERT_THUMBPRINT, '/s', 'My')
} else {
    $pfxPath = $env:EC_BIM_SIGN_PFX_PATH
    if ([string]::IsNullOrWhiteSpace($pfxPath)) { $pfxPath = 'C:\Certs\MyRevitPlugin.pfx' }
    if (!(Test-Path -LiteralPath $pfxPath -PathType Leaf)) {
        throw 'PFX not found. Set EC_BIM_SIGN_PFX_PATH and EC_BIM_SIGN_PFX_PASSWORD before starting Visual Studio/MSBuild.'
    }
    $signArgs += @('/f', $pfxPath)
    if (![string]::IsNullOrEmpty($env:EC_BIM_SIGN_PFX_PASSWORD)) {
        $signArgs += @('/p', $env:EC_BIM_SIGN_PFX_PASSWORD)
    }
}
$timestampUrl = $env:EC_BIM_SIGN_TIMESTAMP_URL
if ([string]::IsNullOrWhiteSpace($timestampUrl)) { $timestampUrl = 'http://timestamp.digicert.com' }
$signArgs += @('/tr', $timestampUrl, '/td', 'SHA256', $TargetPath)
# Never print $signArgs: the PFX password may be present in this array.
& $signTool @signArgs
$signExitCode = $LASTEXITCODE
$signArgs = $null
if ($signExitCode -ne 0) { throw "SignTool failed (exit $signExitCode). The build is not a signed release." }
$signature = Get-AuthenticodeSignature -LiteralPath $TargetPath
if (!$signature.SignerCertificate -or $signature.Status -eq 'NotSigned' -or $signature.Status -eq 'HashMismatch') {
    throw 'Authenticode signature missing or corrupted after signing.'
}
Write-Host ('Signed DLL; certificate: ' + $signature.SignerCertificate.Thumbprint)
if ($signature.Status -ne 'Valid') {
    Write-Warning ('The DLL is signed, but Windows trust verification returned: ' + $signature.Status + '. No certificates were installed into trusted stores.')
}

