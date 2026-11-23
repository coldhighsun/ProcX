<#
.SYNOPSIS
    Builds the three ProcX release files: a framework-dependent zip, a self-contained zip and an Inno Setup installer.

.DESCRIPTION
    Output goes to artifacts/<version>/:
      ProcX-<version>-<rid>-framework-dependent.zip   needs the .NET Desktop Runtime installed
      ProcX-<version>-<rid>-self-contained.zip        runs without any runtime installed
      ProcX-<version>-<rid>-setup.exe                 installer (bundles the self-contained build, multi-file)
    Each is accompanied by a SHA256SUMS.txt.

.PARAMETER Runtime
    Runtime identifier: win-x64 (default) or win-arm64.

.PARAMETER Version
    Release version. Defaults to <Version> in Directory.Build.props.

.PARAMETER IsccPath
    Path to ISCC.exe of Inno Setup 7. Defaults to the winget per-user install, then PATH.

.PARAMETER SkipInstaller
    Build only the two zip files.
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64',
    [string]$Version,
    [string]$IsccPath,
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\ProcX.App\ProcX.App.csproj'

if (-not $Version) {
    $props = [xml](Get-Content (Join-Path $root 'Directory.Build.props'))
    $node = $props.SelectSingleNode('/Project/PropertyGroup/Version')
    if (-not $node) { throw 'No <Version> found in Directory.Build.props; pass -Version.' }
    $Version = $node.InnerText.Trim()
}
if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') {
    throw "Version '$Version' is not a valid semantic version."
}

$out = Join-Path $root "artifacts\$Version"
$work = Join-Path $root "artifacts\obj-$Version"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Path $out, $work | Out-Null

function Publish-Variant {
    param([string]$Name, [bool]$SelfContained, [bool]$SingleFile)

    $dir = Join-Path $work $Name
    Write-Host "== Publishing $Name ($Runtime) ==" -ForegroundColor Cyan

    $args = @(
        'publish', $project,
        '-c', 'Release',
        '-r', $Runtime,
        '-o', $dir,
        "-p:Version=$Version",
        "-p:SelfContained=$($SelfContained.ToString().ToLowerInvariant())",
        "-p:PublishSingleFile=$($SingleFile.ToString().ToLowerInvariant())",
        '-p:DebugType=none',
        '-p:DebugSymbols=false',
        # The XML documentation files are only useful at build time.
        '-p:GenerateDocumentationFile=false'
    )

    # A self-contained single file must extract everything on first run: WPF's DirectWriteForwarder.dll is a
    # mixed-mode assembly that cannot be loaded from inside the bundle (BadImageFormatException).
    if ($SelfContained -and $SingleFile) { $args += '-p:IncludeAllContentForSelfExtract=true' }

    # Output goes to the host, not the pipeline: this function's return value must be only the folder path.
    & dotnet @args | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $Name." }

    # Ship only what users need.
    Copy-Item (Join-Path $root 'LICENSE') $dir
    Copy-Item (Join-Path $root 'README.md') $dir
    return $dir
}

function New-ZipOf {
    param([string]$SourceDir, [string]$ZipPath)

    Compress-Archive -Path (Join-Path $SourceDir '*') -DestinationPath $ZipPath -CompressionLevel Optimal
    Write-Host ("   {0} ({1:N1} MB)" -f (Split-Path -Leaf $ZipPath), ((Get-Item $ZipPath).Length / 1MB))
}

$frameworkDependent = Publish-Variant -Name 'framework-dependent' -SelfContained $false -SingleFile $true
New-ZipOf $frameworkDependent (Join-Path $out "ProcX-$Version-$Runtime-framework-dependent.zip")

$selfContained = Publish-Variant -Name 'self-contained' -SelfContained $true -SingleFile $true
New-ZipOf $selfContained (Join-Path $out "ProcX-$Version-$Runtime-self-contained.zip")

if (-not $SkipInstaller) {
    if (-not $IsccPath) {
        $candidates = @(
            Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'
            Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'
            Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'
        )
        $IsccPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
        if (-not $IsccPath) {
            $found = Get-Command ISCC.exe -ErrorAction SilentlyContinue
            if ($found) { $IsccPath = $found.Source }
        }
    }
    if (-not $IsccPath -or -not (Test-Path $IsccPath)) {
        throw 'Inno Setup 7 (ISCC.exe) was not found. Install it (winget install JRSoftware.InnoSetup.7) or pass -IsccPath.'
    }

    # The installer ships the self-contained build as a folder rather than a single file: it starts in about
    # two seconds on first run, whereas the single file first extracts ~150 MB to %TEMP% (about 15 seconds).
    $installerSource = Publish-Variant -Name 'installer-payload' -SelfContained $true -SingleFile $false

    Write-Host "== Building installer with $IsccPath ==" -ForegroundColor Cyan
    $arch = if ($Runtime -eq 'win-arm64') { 'arm64' } else { 'x64compatible' }
    & $IsccPath (Join-Path $PSScriptRoot 'installer\ProcX.iss') `
        "/DAppVersion=$Version" `
        "/DSourceDir=$installerSource" `
        "/DOutputDir=$out" `
        "/DOutputBaseName=ProcX-$Version-$Runtime-setup" `
        "/DArchitecture=$arch" | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
}

Remove-Item $work -Recurse -Force

$sums = Get-ChildItem $out -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
}
Set-Content (Join-Path $out 'SHA256SUMS.txt') $sums -Encoding ascii

Write-Host ''
Write-Host "Release files in $out" -ForegroundColor Green
Get-ChildItem $out -File | ForEach-Object { Write-Host ("  {0,-55} {1,8:N1} MB" -f $_.Name, ($_.Length / 1MB)) }
