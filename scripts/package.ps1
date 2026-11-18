# package.ps1 —— 组装免安装目录，可选带模型一起打 zip
# 用法：
#   scripts\package.ps1                          # 只有程序和运行时（约 90MB）
#   scripts\package.ps1 -IncludeModel -Zip       # 连模型一起打包（约 1.1GB）
param(
    [string]$Configuration = 'Release',
    [string]$Version = '1.0.0',
    [switch]$IncludeModel,
    [switch]$Zip
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$dist = Join-Path $root 'dist\translateAI-win64'
$build = Join-Path $root "src\translateAI\bin\$Configuration"

# 1) 编译
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

# 4) 运行时：优先 Vulkan 构建（没有显卡时自动退回 CPU）
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

# 只保留 llama-server.exe，其余命令行工具删掉
Get-ChildItem $runtimeDst -Filter *.exe | Where-Object { $_.Name -ne 'llama-server.exe' } | Remove-Item -Force

# 5) 模型（可选）
$modelRel = ''
if ($IncludeModel) {
    $srcModels = Join-Path $root 'models'
    $pick = $null

    $cfgPath = Join-Path $root 'config.json'
    if (Test-Path $cfgPath) {
        $cfg = Get-Content $cfgPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($cfg.model) {
            $candidate = Join-Path $root $cfg.model
            if (Test-Path $candidate) { $pick = Get-Item $candidate }
        }
    }

    if (-not $pick) {
        $pick = Get-ChildItem $srcModels -Filter *.gguf -ErrorAction SilentlyContinue |
                Sort-Object Length -Descending | Select-Object -First 1
    }

    if (-not $pick) { throw '-IncludeModel 指定了，但 models\ 里没有 .gguf' }

    Copy-Item $pick.FullName (Join-Path $dist 'models')
    $modelRel = 'models\' + $pick.Name
    Write-Host ("model   : {0}  ({1:N0} MB)" -f $pick.Name, ($pick.Length / 1MB))
}

# 6) 说明文件
@'
把 .gguf 模型放进这个文件夹，程序会在「设置」里自动列出。
Drop .gguf files here; they show up in Settings > Model.
'@ | Set-Content (Join-Path $dist 'models\README.txt') -Encoding UTF8

Copy-Item (Join-Path $root 'docs\THIRD-PARTY-NOTICES.txt') $dist

@'
translateAI —— 本地 AI 翻译（免安装）
=====================================

怎么用
------
1. 解压到任意目录（不要放在需要管理员权限的路径下）
2. 双击 translateAI.exe
3. 在任意程序里选中文字，按 Ctrl+P，译文会出现在鼠标旁边
4. Esc 或点击别处关闭悬浮窗；托盘图标右键可以退出

首次使用
--------
首次翻译时会把模型加载进内存，需要 5–10 秒（取决于模型大小和机器）。
之后每次翻译在 0.3–2 秒之间。

模型
----
- 本包 models 文件夹里如果已经有 .gguf，直接就能用
- 想换模型：把别的 .gguf 丢进 models，然后在「设置」里选择
- 建议 1B–2B 参数、Q4 量化；显存小于 6GB 或纯 CPU 时优先选更小的模型

常见问题
--------
- 按 Ctrl+P 没反应：可能被别的软件占用了这个热键，去「设置」里换一个
- 提示"没取到选中文字"：某些程序（如部分 Electron 应用）不支持模拟复制，
  可以先用 Ctrl+C 复制，再用托盘菜单「翻译剪贴板」
- 提示 xxx 注册失败：热键被占用，换一个即可

隐私
----
全部推理在本机完成，不联网、不上传任何内容。

许可证
------
本程序源码见 GitHub 仓库；随包分发的第三方组件见 THIRD-PARTY-NOTICES.txt。
模型权重遵循各自的许可证，不适用本程序的许可证。
'@ | Set-Content (Join-Path $dist '使用说明.txt') -Encoding UTF8

# 7) 默认配置
$config = [ordered]@{
    model   = $modelRel
    port    = 8787
    ctx     = 4096
    threads = 0
    hotkey  = 'Ctrl+P'
    lang    = 'zh'
}
$config | ConvertTo-Json | Set-Content (Join-Path $dist 'config.json') -Encoding UTF8

# 8) 清理任何跑测试留下的日志
Get-ChildItem $dist -Recurse -Include *.log,o.txt,e.txt -ErrorAction SilentlyContinue | Remove-Item -Force

# 9) 体积
$sizeMB = [math]::Round(((Get-ChildItem $dist -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
Write-Host ("packaged: $dist  ($sizeMB MB)")

if ($Zip) {
    # 带模型 / 不带模型用不同文件名，避免互相覆盖
    $suffix = if ($IncludeModel) { '' } else { '-lite' }
    $zipPath = Join-Path $root ("dist\translateAI-win64-v{0}{1}.zip" -f $Version, $suffix)
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Write-Host 'zipping ...'
    Compress-Archive -Path (Join-Path $dist '*') -DestinationPath $zipPath -CompressionLevel Fastest
    $zipMB = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
    Write-Host ("zip     : $zipPath  ($zipMB MB)")
}
