# verify.ps1 —— 证明「这个包在这台机器上能把模型跑起来」
# 用法：
#   powershell -ExecutionPolicy Bypass -File verify.ps1
#   powershell -ExecutionPolicy Bypass -File verify.ps1 -Model "models\xxx.gguf"

param(
    [string]$Model,
    [string]$Text = "The quick brown fox jumps over the lazy dog."
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $root

$cfg = Get-Content (Join-Path $root 'config.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$port = [int]$cfg.port

function Resolve-Model($c) {
    if ($Model) {
        $p = Join-Path $root $Model
        if (Test-Path $p) { return $p }
        throw "指定的模型不存在：$Model"
    }
    if ($c.model) {
        $p = Join-Path $root $c.model
        if (Test-Path $p) { return $p }
        Write-Host "[!] config 指定的模型不存在：$($c.model)，回退到目录里第一个" -ForegroundColor Yellow
    }
    $first = Get-ChildItem (Join-Path $root 'models') -Filter *.gguf -File -ErrorAction SilentlyContinue |
             Sort-Object Name | Select-Object -First 1
    if ($first) { return $first.FullName }
    return $null
}

$modelPath = Resolve-Model $cfg
if (-not $modelPath) {
    Write-Host ""
    Write-Host "  [X] models\ 里没有 .gguf" -ForegroundColor Red
    Write-Host "      把模型文件放进 models\ 再跑一次："
    Write-Host "      powershell -ExecutionPolicy Bypass -File verify.ps1"
    Write-Host ""
    exit 1
}

$sizeMB = [math]::Round((Get-Item $modelPath).Length / 1MB, 1)
Write-Host ("model : " + (Split-Path $modelPath -Leaf) + "  (" + $sizeMB + " MB)")
Write-Host "print : 启动服务并等待就绪..."

$logOut = Join-Path $root '_cache\server.out.log'
$logErr = Join-Path $root '_cache\server.err.log'
New-Item -ItemType Directory -Force -Path (Join-Path $root '_cache') | Out-Null

$srvArgs = @('--model', $modelPath, '--host', '127.0.0.1', '--port', $port,
             '--ctx-size', [int]$cfg.ctx, '--no-webui')
if ([int]$cfg.threads -gt 0) { $srvArgs += @('--threads', [int]$cfg.threads) }
if ($cfg.extra_args) { $srvArgs += ($cfg.extra_args -split '\s+') }

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$proc = Start-Process -FilePath (Join-Path $root 'runtime\llama-server.exe') `
                      -ArgumentList $srvArgs -WorkingDirectory $root -WindowStyle Hidden `
                      -RedirectStandardOutput $logOut -RedirectStandardError $logErr -PassThru

$url = "http://127.0.0.1:$port/health"
$ok = $false
for ($i = 0; $i -lt 600; $i++) {
    Start-Sleep -Milliseconds 250
    if ($proc.HasExited) { break }
    try {
        $r = Invoke-RestMethod -Uri $url -TimeoutSec 2
        if ($r.status -eq 'ok') { $ok = $true; break }
    } catch { }
}
$loadMs = [int]$sw.Elapsed.TotalMilliseconds

if (-not $ok) {
    Write-Host ("  [X] 服务没能就绪（{0} ms），进程退出码：{1}" -f $loadMs, $proc.ExitCode) -ForegroundColor Red
    if (Test-Path $logErr) { Write-Host "--- server.err.log ---" -ForegroundColor DarkGray; Get-Content $logErr -Tail 20 }
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    exit 1
}

Write-Host ("  [OK] 就绪，加载耗时 {0} ms" -f $loadMs) -ForegroundColor Green

# 走 /completion（原始 prompt），不依赖模型自带的聊天模板
$prompt = "将以下文本翻译为中文，注意只需要输出翻译后的结果，不要额外解释：`n`n$Text"
$body = @{
    prompt       = $prompt
    n_predict    = 128
    temperature  = 0.7
    top_k        = 20
    top_p        = 0.6
    repeat_penalty = 1.05
} | ConvertTo-Json -Depth 5

try {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
    $sw2 = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-RestMethod -Uri "http://127.0.0.1:$port/completion" -Method Post `
            -Body $bytes -ContentType 'application/json; charset=utf-8' -TimeoutSec 300
    $sw2.Stop()

    $txt = $resp.content
    if (-not $txt) { $txt = $resp.choices[0].text }

    $predN  = $resp.timings.predicted_n
    $predMs = $resp.timings.predicted_ms
    $tps    = if ($predMs -gt 0) { [math]::Round($predN / ($predMs / 1000), 1) } else { 0 }

    Write-Host ""
    Write-Host "  原文 : $Text"
    Write-Host "  译文 : $txt"
    Write-Host ""
    Write-Host ("  首字/总耗时 : {0} ms" -f [int]$sw2.Elapsed.TotalMilliseconds)
    Write-Host ("  生成速度    : {0} tok/s  ({1} tokens)" -f $tps, $predN)
    Write-Host ""
    Write-Host "  [PASS] 模型可用" -ForegroundColor Green
}
catch {
    Write-Host ("  [X] 推理请求失败：" + $_.Exception.Message) -ForegroundColor Red
    if (Test-Path $logErr) { Get-Content $logErr -Tail 20 }
    $code = 1
}
finally {
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
}

if ($code) { exit $code }
