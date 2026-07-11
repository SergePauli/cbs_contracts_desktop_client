param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Version = "1.0.1-beta",
    [string]$InnoSetupCompiler = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot "artifacts\publish\CbsContractsDesktopClient\$RuntimeIdentifier"
$installerDir = Join-Path $repoRoot "artifacts\installer"
$buildOutputDir = Join-Path $repoRoot "bin\$Configuration\net8.0-windows10.0.19041.0\$RuntimeIdentifier"
$projectPath = Join-Path $repoRoot "CbsContractsDesktopClient.csproj"
$innoScriptPath = Join-Path $repoRoot "installer\CbsContractsDesktopClient.iss"

function Resolve-InnoSetupCompiler {
    param(
        [string]$ConfiguredPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredPath)) {
        return $ConfiguredPath
    }

    $candidatePaths = @(
        (Join-Path $PSScriptRoot "ISCC.exe"),
        (Join-Path $repoRoot "ISCC.exe"),
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidatePath in $candidatePaths) {
        if (Test-Path -LiteralPath $candidatePath) {
            return $candidatePath
        }
    }

    return $candidatePaths[2]
}

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList
    )

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($ArgumentList -join ' ')"
    }
}

$resolvedInnoSetupCompiler = Resolve-InnoSetupCompiler $InnoSetupCompiler
if (-not (Test-Path -LiteralPath $resolvedInnoSetupCompiler)) {
    throw "Inno Setup compiler was not found: $resolvedInnoSetupCompiler"
}

Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $installerDir -Force | Out-Null

$buildArguments = @(
    "build",
    $projectPath,
    "--configuration",
    $Configuration,
    "--runtime",
    $RuntimeIdentifier,
    "--self-contained",
    "true",
    "-p:WindowsAppSDKSelfContained=true",
    "-p:PublishSingleFile=false"
)

Invoke-NativeCommand dotnet $buildArguments

if (-not (Test-Path -LiteralPath (Join-Path $buildOutputDir "CbsContractsDesktopClient.exe"))) {
    throw "Build output does not contain CbsContractsDesktopClient.exe: $buildOutputDir"
}

Get-ChildItem -LiteralPath $buildOutputDir -Force |
    Where-Object { $_.Name -ne "artifacts" } |
    Copy-Item -Destination $publishDir -Recurse -Force

$env:CBS_INSTALLER_VERSION = $Version
$env:CBS_INSTALLER_PUBLISH_DIR = $publishDir

try {
    Invoke-NativeCommand $resolvedInnoSetupCompiler @($innoScriptPath)
}
finally {
    Remove-Item Env:\CBS_INSTALLER_VERSION -ErrorAction SilentlyContinue
    Remove-Item Env:\CBS_INSTALLER_PUBLISH_DIR -ErrorAction SilentlyContinue
}

$setupPath = Join-Path $installerDir "CbsContractsDesktopClient-$Version-Setup.exe"
if (-not (Test-Path -LiteralPath $setupPath)) {
    throw "Installer output was not found after Inno Setup completed: $setupPath"
}

Write-Host "Installer created: $setupPath"
