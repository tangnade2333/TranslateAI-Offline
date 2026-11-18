# make-icon.ps1 —— 把一张图标转成方形、多尺寸的 .ico
# 用法：powershell -ExecutionPolicy Bypass -File scripts\make-icon.ps1 `
#           -Source src\translateAI\resources\translateAI_64x64.ico `
#           -Output src\translateAI\resources\app.ico
#
# 注意：Windows 的 .ico 允许用 PNG 压缩单帧，System.Drawing.Icon.ToBitmap()
# 对这种文件会抛 "Requested range extends past the end of the array"，
# 所以这里手动解析 ICO 目录、按 PNG 解码。
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Output
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Load-BestIconImage([string]$path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    if ([BitConverter]::ToUInt16($bytes, 0) -ne 0 -or [BitConverter]::ToUInt16($bytes, 2) -ne 1) {
        throw "not an .ico file: $path"
    }

    $count = [BitConverter]::ToUInt16($bytes, 4)

    $best = $null
    for ($i = 0; $i -lt $count; $i++) {
        $o = 6 + 16 * $i
        $w = [int]$bytes[$o]
        if ($w -eq 0) { $w = 256 }
        $size = [BitConverter]::ToUInt32($bytes, $o + 8)
        $offset = [BitConverter]::ToUInt32($bytes, $o + 12)
        if ($null -eq $best -or $w -gt $best.Width) {
            $best = [pscustomobject]@{ Width = $w; Size = $size; Offset = $offset }
        }
    }

    if ($null -eq $best) { throw "empty icon: $path" }

    $data = New-Object byte[] $best.Size
    [Array]::Copy($bytes, $best.Offset, $data, 0, $best.Size)

    $isPng = ($data.Length -gt 8 -and $data[0] -eq 0x89 -and $data[1] -eq 0x50 -and
              $data[2] -eq 0x4E -and $data[3] -eq 0x47)

    if ($isPng) {
        $ms = New-Object System.IO.MemoryStream(, $data)
        return [System.Drawing.Image]::FromStream($ms)
    }

    $icon = New-Object System.Drawing.Icon($path)
    return $icon.ToBitmap()
}

$sizes = @(16, 32, 48, 64, 128, 256)

$srcImg = Load-BestIconImage (Resolve-Path $Source).Path
Write-Host ("source : {0}x{1}  ({2})" -f $srcImg.Width, $srcImg.Height, $srcImg.PixelFormat)

$pngs = @()

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # 等比缩放并居中，保持正方形画布
    $scale = [Math]::Min($s / $srcImg.Width, $s / $srcImg.Height)
    $w = [int][Math]::Round($srcImg.Width * $scale)
    $h = [int][Math]::Round($srcImg.Height * $scale)
    $x = [int](($s - $w) / 2)
    $y = [int](($s - $h) / 2)

    $g.DrawImage($srcImg, $x, $y, $w, $h)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()

    $pngs += ,($ms.ToArray())
    $ms.Dispose()
}

$fs = [System.IO.File]::Create($Output)
$bw = New-Object System.IO.BinaryWriter($fs)

$bw.Write([UInt16]0)                 # reserved
$bw.Write([UInt16]1)                 # 1 = icon
$bw.Write([UInt16]$sizes.Count)      # frame count

$offset = 6 + 16 * $sizes.Count

for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $data = $pngs[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }

    $bw.Write([byte]$dim)            # width
    $bw.Write([byte]$dim)            # height
    $bw.Write([byte]0)               # palette
    $bw.Write([byte]0)               # reserved
    $bw.Write([UInt16]1)             # planes
    $bw.Write([UInt16]32)            # bpp
    $bw.Write([UInt32]$data.Length)
    $bw.Write([UInt32]$offset)

    $offset += $data.Length
}

foreach ($d in $pngs) { $bw.Write($d) }

$bw.Flush()
$bw.Close()
$fs.Close()

$size = [math]::Round((Get-Item $Output).Length / 1KB, 1)
Write-Host ("output : {0}  ({1} KB, {2} frames)" -f $Output, $size, $sizes.Count)
