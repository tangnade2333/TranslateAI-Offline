param(
    [string]$Runtime = 'runtime-vulkan',
    [string]$Model = '',
    [int]$Ngl = 99
)

$ErrorActionPreference = 'Stop'
$root = 'D:\Project\AI_Project\translateAI'
$rt   = Join-Path $root (Join-Path $Runtime 'llama-server.exe')

if ($Model -eq '') {
    $m = Get-ChildItem (Join-Path $root 'models') -Filter *.gguf | Select-Object -First 1
}
else {
    $m = Get-Item (Join-Path $root ('models\' + $Model))
}

$port = 8787
$cases = Get-Content (Join-Path $root '_cache\cases.json') -Raw -Encoding UTF8 | ConvertFrom-Json

$srvArgs = @('--model', $m.FullName, '--host', '127.0.0.1', '--port', $port,
             '--ctx-size', 2048, '--no-webui', '-t', 8, '-ngl', $Ngl)

$p = Start-Process -FilePath $rt -ArgumentList $srvArgs -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $root '_cache\chat.out.log') `
        -RedirectStandardError  (Join-Path $root '_cache\chat.err.log')

$h = $null
for ($i = 0; $i -lt 240; $i++) {
    Start-Sleep -Milliseconds 250
    try { $h = Invoke-RestMethod "http://127.0.0.1:$port/health" -TimeoutSec 2; if ($h.status -eq 'ok') { break } } catch { }
}
if (-not $h -or $h.status -ne 'ok') {
    Write-Host ('[' + $m.Name + '] SERVER FAILED')
    Get-Content (Join-Path $root '_cache\chat.err.log') -Tail 8 | ForEach-Object { Write-Host ('  ' + $_) }
    exit 1
}

Write-Host ('=== ' + $m.Name + '   runtime=' + $Runtime + ' ===')

foreach ($c in $cases) {
    $body = @{
        messages    = @(@{ role = 'user'; content = $c.prompt })
        temperature = 0.7
        top_k       = 20
        top_p       = 0.6
        max_tokens  = 256
        stream      = $false
    } | ConvertTo-Json -Depth 6

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $r = Invoke-RestMethod -Uri "http://127.0.0.1:$port/v1/chat/completions" -Method Post `
         -Body $bytes -ContentType 'application/json; charset=utf-8' -TimeoutSec 300
    $sw.Stop()

    $txt = $r.choices[0].message.content
    $ct  = $r.usage.completion_tokens
    $ms  = [int]$sw.Elapsed.TotalMilliseconds

    Write-Host ('[' + $c.name + '] ' + $ms + 'ms / ' + $ct + 'tok')
    Write-Host ('  ' + $txt.Trim())
}

Stop-Process -Id $p.Id -Force
Write-Host ''
