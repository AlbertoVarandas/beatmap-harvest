param([string]$Repo = (Split-Path $PSScriptRoot -Parent), [string]$Preview)
# Draws Beatmap Harvest's icon at every size (so small ones stay crisp) and writes app.ico plus PNGs.
# Pink rounded square (osu! pink), a white hit-circle ring, and a download arrow inside it.
#   powershell -File tools/make-icon.ps1 [-Preview sheet.png]
Add-Type -AssemblyName System.Drawing

function Draw([int]$size) {
  $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'; $g.CompositingQuality = 'HighQuality'
  $g.Clear([System.Drawing.Color]::Transparent)
  $s = [double]$size

  # Rounded square with a diagonal pink gradient.
  $inset = [Math]::Max(0.5, $s * 0.02)
  $r = $s * 0.22
  $rect = New-Object System.Drawing.RectangleF ([single]$inset), ([single]$inset), ([single]($s - 2 * $inset)), ([single]($s - 2 * $inset))
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = [single](2 * $r)
  $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
  $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
  $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
  $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
  $path.CloseFigure()
  $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 255, 122, 184)), ([System.Drawing.Color]::FromArgb(255, 196, 58, 128)), 45.0
  $g.FillPath($bg, $path)


  $white = [System.Drawing.Color]::White
  # Hit-circle ring.
  $ringWidth = [Math]::Max(1.6, $s * 0.075)
  $ringR = $s * 0.33
  $c = $s / 2
  $pen = New-Object System.Drawing.Pen $white, ([single]$ringWidth)
  $g.DrawEllipse($pen, [single]($c - $ringR), [single]($c - $ringR), [single](2 * $ringR), [single](2 * $ringR))

  # Download arrow: a shaft and a head, inside the ring.
  $shaftW = [Math]::Max(1.6, $s * 0.085)
  $top = $c - $s * 0.17
  $headTop = $c + $s * 0.0
  $tip = $c + $s * 0.17
  $headHalf = $s * 0.14
  $brush = New-Object System.Drawing.SolidBrush $white
  $g.FillRectangle($brush, [single]($c - $shaftW / 2), [single]$top, [single]$shaftW, [single]($headTop - $top + 1))
  $head = [System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF ([single]($c - $headHalf)), ([single]$headTop)),
    (New-Object System.Drawing.PointF ([single]($c + $headHalf)), ([single]$headTop)),
    (New-Object System.Drawing.PointF ([single]$c), ([single]$tip)))
  $g.FillPolygon($brush, $head)

  $g.Dispose()
  return $bmp
}

function PngBytes($bmp) {
  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  return ,$ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = foreach ($size in $sizes) { ,@($size, (PngBytes (Draw $size))) }

# ICO: 6-byte header, a 16-byte entry per image, then the PNG data (supported by Windows since Vista).
$ico = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $ico
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
  $size = $img[0]; $data = $img[1]
  $dim = if ($size -ge 256) { 0 } else { $size }
  $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
  $w.Write([UInt16]1); $w.Write([UInt16]32); $w.Write([UInt32]$data.Length); $w.Write([UInt32]$offset)
  $offset += $data.Length
}
foreach ($img in $images) { $w.Write([byte[]]$img[1]) }
$w.Flush()

$assets = Join-Path $Repo 'src\BeatmapHarvest.App\Assets'
New-Item -ItemType Directory -Force $assets | Out-Null
[IO.File]::WriteAllBytes((Join-Path $assets 'app.ico'), $ico.ToArray())
$img = Join-Path $Repo 'src\BeatmapHarvest.App\wwwroot\img'
New-Item -ItemType Directory -Force $img | Out-Null
[IO.File]::WriteAllBytes((Join-Path $img 'icon.png'), (PngBytes (Draw 64)))
[IO.File]::WriteAllBytes((Join-Path $Repo 'docs\icon.png'), (PngBytes (Draw 256)))

if ($Preview) {
  # Every size side by side, on dark and light, to check how the small ones read.
  $sheet = New-Object System.Drawing.Bitmap 760, 300
  $g = [System.Drawing.Graphics]::FromImage($sheet)
  $g.Clear([System.Drawing.Color]::FromArgb(255, 28, 23, 25))
  $g.FillRectangle([System.Drawing.Brushes]::WhiteSmoke, 0, 150, 760, 150)
  $x = 10
  foreach ($size in 16, 24, 32, 48, 64, 128) {
    $b = Draw $size
    $g.DrawImage($b, $x, [int](75 - $size / 2), $size, $size)
    $g.DrawImage($b, $x, [int](225 - $size / 2), $size, $size)
    $x += $size + 24
  }
  $g.DrawImage((Draw 256), 500, 22, 256, 256)
  $sheet.Save($Preview)
}
"icon written: $((Get-Item (Join-Path $assets 'app.ico')).Length) bytes, $($images.Count) sizes"
