param(
    [string]$Runtime = 'runtime-vulkan',
    [int]$Ngl = 99,
    [string]$CasesFile = '_cache\plamo-cases.json',
    [double]$Temp = 0.3
)

$ErrorActionPreference = 'Stop'
$root = 'D:\Project\AI_Project\translateAI'
$rt   = Join-Path $root (Join-Path $Runtime 'llama-server.exe')
$m    = Get-Item (Join-Path $root 'models\plamo-3-translate-0.8b-Q4_K_M.gguf')
$port = 8787
$cases = Get-Content (Join-Path $root $CasesFile) -Raw -Encoding UTF8 | ConvertFrom-Json

$srvArgs = @('--model', $m.FullName, '--host', '127.0.0.1', '--port', $port,
             '--ctx-size', 2048, '--no-webui', '-t', 8, '-ngl', $Ngl)

$p = Start-Process -FilePath $rt -ArgumentList $srvArgs -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $root '_cache\plamo.out.log') `
        -RedirectStandardError  (Join-Path $root '_cache\plamo.err.log')

$h = $null
for ($i = 0; $i -lt 240; $i++) {
    Start-Sleep -Milliseconds 250
    try { $h = Invoke-RestMethod "http://127.0.0.1:$port/health" -TimeoutSec 2; if ($h.status -eq 'ok') { break } } catch { }
}
if (-not $h -or $h.status -ne 'ok') {
    Write-Host 'SERVER FAILED'
    Get-Content (Join-Path $root '_cache\plamo.err.log') -Tail 10 | ForEach-Object { Write-Host ('  ' + $_) }
    exit 1
}

Write-Host ('=== ' + $m.Name + '   runtime=' + $Runtime + ' ===')

foreach ($c in $cases) {
    if ($c.system) {
        $msgs = @(
            @{ role = 'system'; content = $c.system },
            @{ role = 'user';   content = $c.user }
        )
    }
    else {
        $msgs = @(@{ role = 'user'; content = $c.user })
    }

    $body = @{
        messages    = $msgs
        temperature = $Temp
        max_tokens  = 256
        stream      = $false
    } | ConvertTo-Json -Depth 8

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $r = Invoke-RestMethod -Uri "http://127.0.0.1:$port/v1/chat/completions" -Method Post `
             -Body $bytes -ContentType 'application/json; charset=utf-8' -TimeoutSec 300
        $sw.Stop()
        $txt = $r.choices[0].message.content
        $ct  = $r.usage.completion_tokens
        Write-Host ('[' + $c.name + '] ' + [int]$sw.Elapsed.TotalMilliseconds + 'ms / ' + $ct + 'tok')
        Write-Host ('  ' + $txt.Trim())
    }
    catch {
        Write-Host ('[' + $c.name + '] REQUEST FAILED: ' + $_.Exception.Message)
        try {
            $resp = $_.Exception.Response
            if ($resp) {
                $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
                Write-Host ('  body: ' + $reader.ReadToEnd())
            }
        } catch { }
    }
}

Stop-Process -Id $p.Id -Force

