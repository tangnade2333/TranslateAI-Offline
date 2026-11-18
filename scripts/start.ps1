# start.ps1 —— 按 config.json 拉起本地翻译服务（脚本放在 scripts\ 下，根目录是它的上一层）
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $root

$cfg = Get-Content (Join-Path $root 'config.json') -Raw -Encoding UTF8 | ConvertFrom-Json

function Resolve-Model($c) {
    if ($c.model) {
        $p = Join-Path $root $c.model
        if (Test-Path $p) { return $p }
        Write-Host "[!] config 指定的模型不存在：$($c.model)" -ForegroundColor Yellow
    }
    $first = Get-ChildItem (Join-Path $root 'models') -Filter *.gguf -File -ErrorAction SilentlyContinue |
             Sort-Object Name | Select-Object -First 1
    if ($first) { return $first.FullName }
    return $null
}

$model = Resolve-Model $cfg
if (-not $model) {
    Write-Host "models\ 里没有 .gguf 模型。把一个 gguf 放进去再启动。" -ForegroundColor Red
    exit 1
}

$srvArgs = @('--model', $model, '--host', '127.0.0.1', '--port', [int]$cfg.port, '--ctx-size', [int]$cfg.ctx)
if ([int]$cfg.threads -gt 0) { $srvArgs += @('--threads', [int]$cfg.threads) }
if ($cfg.extra_args) { $srvArgs += ($cfg.extra_args -split '\s+') }

Write-Host ("model : " + (Split-Path $model -Leaf))
Write-Host ("port  : " + $cfg.port)

Start-Process -FilePath (Join-Path $root 'runtime\llama-server.exe') `
              -ArgumentList $srvArgs -WorkingDirectory $root -WindowStyle Minimized

$url = "http://127.0.0.1:$($cfg.port)/health"
$ok = $false
for ($i = 0; $i -lt 240; $i++) {
    Start-Sleep -Milliseconds 500
    try {
        $r = Invoke-RestMethod -Uri $url -TimeoutSec 2
        if ($r.status -eq 'ok') { $ok = $true; break }
    } catch { }
}

if (-not $ok) {
    Write-Host "服务没起来：检查端口占用、模型是否被当前构建支持。" -ForegroundColor Red
    exit 1
}

Write-Host ("ready : http://127.0.0.1:" + $cfg.port) -ForegroundColor Green
Start-Process ("http://127.0.0.1:" + $cfg.port + "/")
