param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$publishDir = Join-Path $repoRoot "artifacts\publish\client"
$distDir = Join-Path $repoRoot "artifacts\dist"
$wixObjDir = Join-Path $repoRoot "artifacts\wix\client"
$outputMsi = Join-Path $distDir "TenderAssistant.Client.Setup.msi"

New-Item -ItemType Directory -Force $publishDir, $distDir, $wixObjDir | Out-Null

dotnet publish (Join-Path $repoRoot "src\TenderAssistant.Client\TenderAssistant.Client.csproj") `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    --output $publishDir

wix build (Join-Path $PSScriptRoot "Product.wxs") `
    -arch x64 `
    -ext WixToolset.UI.wixext `
    -ext WixToolset.Util.wixext `
    -d "ProjectDir=$PSScriptRoot\" `
    -d "PublishDir=$publishDir" `
    -intermediateFolder $wixObjDir `
    -pdbtype none `
    -out $outputMsi

Write-Host "MSI created: $outputMsi"
