# Compile KSTS.dll directly with Roslyn against a local KSP install (no Visual Studio, NuGet or deploy.bat needed).
#   powershell -File build.ps1 [-KSP "D:\...\Kerbal Space Program"] [-Deploy]
param(
    [string]$KSP = $(if ($env:KSPDIR) { $env:KSPDIR } else { "D:\SteamLibrary\steamapps\common\Kerbal Space Program" }),
    [switch]$Deploy
)
$ErrorActionPreference = "Stop"
$src = Join-Path $PSScriptRoot "Source"
$out = Join-Path $PSScriptRoot "GameData\KSTS\Plugins\KSTS.dll"

$sdk = (dotnet --list-sdks | Select-Object -Last 1) -replace '^(\S+) \[(.+)\]$', '$2\$1'
$csc = Join-Path $sdk "Roslyn\bincore\csc.dll"
if (-not (Test-Path $csc)) { throw "csc.dll not found under $sdk" }

$managed = Join-Path $KSP "KSP_x64_Data\Managed"
$refs = @(Get-ChildItem $managed -Filter *.dll | Where-Object { $_.Name -match '^(Assembly|UnityEngine|mscorlib|System|netstandard)' } | ForEach-Object { $_.FullName })
$refs += @(
    "GameData\000_ClickThroughBlocker\Plugins\ClickThroughBlocker.dll",
    "GameData\001_ToolbarControl\Plugins\ToolbarControl.dll",
    "GameData\SpaceTuxLibrary\Plugins\KSP_Log.dll",
    "GameData\SpaceTuxLibrary\Plugins\SpaceTuxUtility.dll",
    "GameData\KIS\Plugins\KSPDev_Utils.2.6.dll"
) | ForEach-Object { Join-Path $KSP $_ }

[xml]$proj = Get-Content (Join-Path $src "KSTS.csproj")
$files = $proj.Project.ItemGroup.Compile.Include | Where-Object { $_ } | Select-Object -Unique | ForEach-Object { Join-Path $src $_ }

$rsp = Join-Path $env:TEMP "ksts-build.rsp"
@(
    "-nologo", "-target:library", "-nostdlib+", "-optimize+", "-debug:portable",
    "-langversion:12", "-nowarn:CS0618,CS0649,CS0169,CS0414,CS0162,CS0168,CS0219",
    "-define:TRACE", "-out:`"$out`""
) + ($refs | ForEach-Object { "-r:`"$_`"" }) + ($files | ForEach-Object { "`"$_`"" }) | Set-Content $rsp -Encoding utf8

dotnet $csc "@$rsp"
if ($LASTEXITCODE -ne 0) { throw "build failed" }
Write-Host "built -> $out"

if ($Deploy) {
    $dst = Join-Path $KSP "GameData\KSTS"
    Copy-Item (Join-Path $PSScriptRoot "GameData\KSTS\*") $dst -Recurse -Force
    Write-Host "deployed -> $dst"
}
