# Builds assets\parklecture.ico from the character logo: a blue tile with magnifier/lecture frame around the face.
# Sizes up to 64px are stored as 32-bit DIBs (readable by .NET Framework Icon APIs); 128/256px are PNG.
param([string]$PreviewDirectory)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$logo = [System.Drawing.Image]::FromFile((Join-Path $root 'assets\parkjuim-logo.png'))

function New-RoundRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); return $p
}

function New-IconFrame([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'; $g.Clear([System.Drawing.Color]::Transparent)
    $small = $s -le 24
    # Deep vibrant blue tile with a soft top-to-bottom gradient.
    $inset = [single]($s * 0.03); $tile = New-RoundRect $inset $inset ($s - 2 * $inset) ($s - 2 * $inset) ([single]($s * 0.22))
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.PointF(0, 0)), (New-Object System.Drawing.PointF(0, $s)), [System.Drawing.Color]::FromArgb(255, 38, 132, 255), [System.Drawing.Color]::FromArgb(255, 0, 88, 224))
    $g.FillPath($grad, $tile)
    # Character face in a white-ringed magnifier circle
    $faceRatio = if ($small) { 0.74 } else { 0.58 }
    $fd = [single]($s * $faceRatio)
    $fx = [single](($s - $fd) / 2 - ($s * 0.04))
    $fy = [single](($s - $fd) / 2 - ($s * 0.04))
    $ring = [single][Math]::Max(1.5, $s * 0.045)
    # Magnifier handle at bottom-right
    if ($s -gt 16) {
        $handlePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, [single][Math]::Max(2.0, $s * 0.085))
        $handlePen.StartCap = 'Round'; $handlePen.EndCap = 'Round'
        $cx = $fx + $fd * 0.82; $cy = $fy + $fd * 0.82
        $hx = [single]($s * 0.84); $hy = [single]($s * 0.84)
        $g.DrawLine($handlePen, $cx, $cy, $hx, $hy)
        $handlePen.Dispose()
    }
    $g.FillEllipse([System.Drawing.Brushes]::White, $fx - $ring, $fy - $ring, $fd + 2 * $ring, $fd + 2 * $ring)
    $clip = New-Object System.Drawing.Drawing2D.GraphicsPath; $clip.AddEllipse($fx, $fy, $fd, $fd)
    $g.SetClip($clip)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 226, 238, 255))), $fx, $fy, $fd, $fd)
    $src = New-Object System.Drawing.RectangleF(237, 150, 780, 780)
    $g.DrawImage($logo, (New-Object System.Drawing.RectangleF($fx, $fy, $fd, $fd)), $src, [System.Drawing.GraphicsUnit]::Pixel)
    $g.ResetClip()
    # Small yellow laser/pen dot badge at top-right on larger sizes
    if ($s -ge 32) {
        $dotD = [single]($s * 0.16)
        $dx = [single]($s * 0.72); $dy = [single]($s * 0.12)
        $g.FillEllipse((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 214, 10))), $dx, $dy, $dotD, $dotD)
    }
    $g.Dispose(); return $bmp
}

function Get-DibBytes([System.Drawing.Bitmap]$bmp) {
    $s = $bmp.Width; $ms = New-Object System.IO.MemoryStream; $w = New-Object System.IO.BinaryWriter($ms)
    $w.Write([int]40); $w.Write([int]$s); $w.Write([int]($s * 2)); $w.Write([int16]1); $w.Write([int16]32)
    $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0)
    for ($y = $s - 1; $y -ge 0; $y--) { for ($x = 0; $x -lt $s; $x++) { $c = $bmp.GetPixel($x, $y); $w.Write([byte]$c.B); $w.Write([byte]$c.G); $w.Write([byte]$c.R); $w.Write([byte]$c.A) } }
    $maskRow = [int]([Math]::Ceiling($s / 32.0) * 4)
    for ($y = $s - 1; $y -ge 0; $y--) { $row = New-Object byte[] $maskRow; for ($x = 0; $x -lt $s; $x++) { if ($bmp.GetPixel($x, $y).A -eq 0) { $row[$x -shr 3] = $row[$x -shr 3] -bor (0x80 -shr ($x -band 7)) } }; $w.Write($row) }
    $w.Flush(); return , $ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$images = @()
foreach ($s in $sizes) {
    $bmp = New-IconFrame $s
    if ($PreviewDirectory) { New-Item -ItemType Directory -Force $PreviewDirectory | Out-Null; $bmp.Save((Join-Path $PreviewDirectory "icon-$s.png")) }
    if ($s -ge 128) { $ms = New-Object System.IO.MemoryStream; $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $images += , $ms.ToArray() }
    else { $images += , (Get-DibBytes $bmp) }
    $bmp.Dispose()
}
$logo.Dispose()
$out = New-Object System.IO.MemoryStream; $w = New-Object System.IO.BinaryWriter($out)
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0); $w.Write([int16]1); $w.Write([int16]32)
    $w.Write([int]$images[$i].Length); $w.Write([int]$offset); $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write([byte[]]$img) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $root 'assets\parklecture.ico'), $out.ToArray())
Write-Output "Wrote assets\parklecture.ico ($($sizes -join ', ') px)"
