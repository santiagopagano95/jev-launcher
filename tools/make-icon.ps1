# Generates src/JevLauncher.App/Assets/jev.ico and a preview PNG.
# Design: rounded square with the TypeSafe accent gradient (#F386A1 -> #D45BB6) and a white "J".
param(
    [string]$OutIco = "$PSScriptRoot\..\src\JevLauncher.App\Assets\jev.ico",
    [string]$PreviewPng = "$env:TEMP\jev-icon-preview.png"
)

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$fontPath = "$PSScriptRoot\..\src\JevLauncher.App\Fonts\Inter-SemiBold.ttf"
$pfc = New-Object System.Drawing.Text.PrivateFontCollection
$pfc.AddFontFile((Resolve-Path $fontPath).Path)
$family = $pfc.Families[0]

$accentFrom = [System.Drawing.Color]::FromArgb(255, 243, 134, 161)  # #F386A1
$accentTo   = [System.Drawing.Color]::FromArgb(255, 212, 91, 182)   # #D45BB6

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [float][Math]::Max(1, $size * 0.05)
    $rect = New-Object System.Drawing.RectangleF($pad, $pad, ($size - 2 * $pad), ($size - 2 * $pad))
    $radius = [float]($size * 0.26)
    $d = $radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $accentFrom, $accentTo, [float]0.0)
    $g.FillPath($brush, $path)

    $font = New-Object System.Drawing.Font($family, [float]($size * 0.72), [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $textRect = New-Object System.Drawing.RectangleF(0, [float]($size * 0.02), $size, $size)
    $g.DrawString('J', $font, [System.Drawing.Brushes]::White, $textRect, $sf)

    $font.Dispose(); $brush.Dispose(); $path.Dispose(); $g.Dispose()
    return $bmp
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$entries = @()
foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $entries += , @($s, $ms.ToArray())
    if ($s -eq 256) { $bmp.Save($PreviewPng, [System.Drawing.Imaging.ImageFormat]::Png) }
    $ms.Dispose(); $bmp.Dispose()
}

$dir = Split-Path -Parent $OutIco
New-Item -ItemType Directory -Force -Path $dir | Out-Null

$fs = [System.IO.File]::Create($OutIco)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$entries.Count)
$offset = 6 + 16 * $entries.Count
foreach ($e in $entries) {
    $s = [int]$e[0]; $bytes = [byte[]]$e[1]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim)
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$bytes.Length); $bw.Write([UInt32]$offset)
    $offset += $bytes.Length
}
foreach ($e in $entries) { $bw.Write([byte[]]$e[1]) }
$bw.Flush(); $bw.Dispose(); $fs.Dispose()

Write-Host "icon  : $OutIco"
Write-Host "preview: $PreviewPng"
