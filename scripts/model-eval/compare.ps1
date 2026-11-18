$ErrorActionPreference = 'Continue'
$root = 'D:\Project\AI_Project\translateAI'
$models = Get-ChildItem (Join-Path $root 'models') -Filter *.gguf | Sort-Object Name
$backends = @(
    @{ Name = 'CPU   '; Dir = 'runtime' },
    @{ Name = 'Vulkan'; Dir = 'runtime-vulkan' }
)

foreach ($m in $models) {
    Write-Host ('=== ' + $m.Name + '   ' + [math]::Round($m.Length / 1MB) + ' MB ===')
    foreach ($b in $backends) {
        $exe = Join-Path $root (Join-Path $b.Dir 'llama-bench.exe')
        $out = & $exe -m $m.FullName -p 32 -n 32 -r 1 2>&1 | Out-String
        $rows = ($out -split "`n") | Where-Object { $_ -match '\|\s*(pp32|tg32)\s*\|' }
        if ($rows.Count -eq 0) {
            Write-Host ('  ' + $b.Name + ' : FAILED')
            ($out -split "`n") | Where-Object { $_ -match 'error|failed|unknown|unsupported|invalid' } |
                Select-Object -First 2 | ForEach-Object { Write-Host ('      ' + $_.Trim()) }
        }
        else {
            foreach ($r in $rows) {
                $c = ($r -split '\|')
                $test = $c[6].Trim()
                $toks = $c[7].Trim()
                Write-Host ('  ' + $b.Name + ' : ' + $test.PadRight(5) + ' ' + $toks)
            }
        }
    }
    Write-Host ''
}
