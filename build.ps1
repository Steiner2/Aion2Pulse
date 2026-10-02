param(
    [switch]$Test,
    [switch]$Publish,
    [switch]$Offline,
    [string]$PublishFolder = 'artifacts\Aion2Pulse-0.3.1'
)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$portableSdk = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$dotnetCommand = if (Test-Path -LiteralPath $portableSdk) { $portableSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
Push-Location (Join-Path $projectRoot 'meter')
try {
    $offlineArguments = if ($Offline) { @('--ignore-failed-sources', '-p:NuGetAudit=false') } else { @() }
    & $dotnetCommand build Aion2Flow.slnx -c Release @offlineArguments
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    if ($Test) {
        & $dotnetCommand test --project tests/Aion2Flow.UnitTests/Aion2Flow.UnitTests.csproj -c Release --no-build --no-restore --max-parallel-test-modules 1
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    }
    if ($Publish) {
        $publishDirectory = Join-Path $projectRoot $PublishFolder
        & $dotnetCommand publish src\Aion2Flow\Aion2Flow.csproj -c Release -r win-x64 --self-contained true -p:PublishAot=false -o $publishDirectory @offlineArguments
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
        Copy-Item -LiteralPath 'LICENSE.txt' -Destination $publishDirectory
        Copy-Item -LiteralPath 'UPSTREAM.md' -Destination $publishDirectory
        $defaultSettingsPath = Join-Path $publishDirectory 'settings.json'
        if (-not (Test-Path -LiteralPath $defaultSettingsPath)) {
            Copy-Item -LiteralPath 'config\default-settings.json' -Destination $defaultSettingsPath
        }
        Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $publishDirectory 'START-HERE.md')
        $manifest = Get-ChildItem -LiteralPath $publishDirectory -File | ForEach-Object {
            [pscustomobject]@{ Name = $_.Name; Length = $_.Length; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
        }
        $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectRoot 'artifacts\pulse-publish-manifest.json')
        $packageItems = Get-ChildItem -LiteralPath $publishDirectory | Where-Object { $_.Name -notin 'history', 'logs' }
        Compress-Archive -Path $packageItems.FullName -DestinationPath (Join-Path $projectRoot 'artifacts\Aion2Pulse-win-x64.zip') -Force
    }
} finally {
    Pop-Location
}
