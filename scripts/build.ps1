# build.ps1 —— 编译 Release 版
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$proj = Join-Path $root 'src\translateAI\translateAI.csproj'

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' 2>$null
        if ($found) { return ($found | Select-Object -First 1) }
    }

    foreach ($p in @(
        'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe',
        'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe',
        'D:\APP\Microsoft Visual Studio\2017\MSBuild\15.0\Bin\MSBuild.exe'
    )) {
        if (Test-Path $p) { return $p }
    }

    throw 'MSBuild.exe not found. Install Visual Studio or the Build Tools.'
}

$msbuild = Find-MSBuild
Write-Host "MSBuild : $msbuild"

& $msbuild $proj "/p:Configuration=$Configuration" /v:minimal /nologo

if ($LASTEXITCODE -ne 0) { throw "build failed ($LASTEXITCODE)" }

$exe = Join-Path $root "src\translateAI\bin\$Configuration\translateAI.exe"
Write-Host "OK      : $exe"
