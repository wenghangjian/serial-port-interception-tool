param(
    [string]$Runtime = "win-x64",
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src/SerialMitmProxy.App/SerialMitmProxy.App.csproj"
$selfContained = -not $FrameworkDependent.IsPresent
$outputName = if ($selfContained) { "$Runtime-self-contained" } else { "$Runtime" }
$output = Join-Path $root "release/$outputName"

New-Item -ItemType Directory -Force -Path $output | Out-Null

dotnet publish $project `
  -c Release `
  -r $Runtime `
  --self-contained $selfContained `
  /p:PublishSingleFile=false `
  /p:PublishTrimmed=false `
  -o $output

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Copy-Item (Join-Path $root "config/serialmitmproxy.template.json") (Join-Path $output "serialmitmproxy.template.json") -Force
Write-Host "Published to $output (SelfContained=$selfContained)"
