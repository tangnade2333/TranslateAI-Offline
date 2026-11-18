# package.ps1 —— 组装可分发目录 dist\translateAI-win64\
param(
    [string]$Configuration = 'Release',
    [switch]$Zip
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$dist = Join-Path $root 'dist\translateAI-win64'
$build = Join-Path $root "src\translateAI\bin\$Configuration"

# 1) 先编译
& (Join-Path $root 'scripts\build.ps1') -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { throw 'build failed' }

# 2) 清空输出目录
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Force $dist | Out-Null
New-Item -ItemType Directory -Force (Join-Path $dist 'models') | Out-Null

# 3) 主程序
Copy-Item (Join-Path $build 'translateAI.exe') $dist
if (Test-Path (Join-Path $build 'translateAI.exe.config')) {
    Copy-Item (Join-Path $build 'translateAI.exe.config') $dist
}

# 4) 运行时：优先 Vulkan 构建（没有显卡时它自己退回 CPU）
$runtimeSrc = Join-Path $root 'runtime-vulkan'
if (-not (Test-Path (Join-Path $runtimeSrc 'llama-server.exe'))) {
    $runtimeSrc = Join-Path $root 'runtime'
}
if (-not (Test-Path (Join-Path $runtimeSrc 'llama-server.exe'))) {
    throw 'runtime\ 或 runtime-vulkan\ 里没有 llama-server.exe'
}

$runtimeDst = Join-Path $dist 'runtime'
New-Item -ItemType Directory -Force $runtimeDst | Out-Null
Copy-Item (Join-Path $runtimeSrc '*') $runtimeDst -Recurse -Force

# 只留 llama-server.exe，其余工具程序删掉，省体积
Get-ChildItem $runtimeDst -Filter *.exe | Where-Object { $_.Name -ne 'llama-server.exe' } | Remove-Item -Force

# 5) 说明与默认配置
@'
把 .gguf 模型放进这个文件夹，程序会在设置里自动列出。
Drop .gguf files here; they show up in Settings > Model.
'@ | Set-Content (Join-Path $dist 'models\README.txt') -Encoding UTF8

$config = [ordered]@{
    model   = ''
    port    = 8787
    ctx     = 4096
    threads = 0
    hotkey  = 'Ctrl+P'
    lang    = 'zh'
}
$config | ConvertTo-Json | Set-Content (Join-Path $dist 'config.json') -Encoding UTF8

# 6) 体积
$sizeMB = [math]::Round(((Get-ChildItem $dist -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
Write-Host "packaged: $dist  ($sizeMB MB, 不含模型)"

if ($Zip) {
    $zipPath = Join-Path $root 'dist\translateAI-win64.zip'
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path $dist -DestinationPath $zipPath
    Write-Host "zip     : $zipPath"
}
