# Рисует значок Snapkit и собирает его в ICO с несколькими размерами.
# Запуск: powershell -File tools/make-icon.ps1
# Результат: src/Snapkit.App/Assets/snapkit.ico

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sizes = 16, 24, 32, 48, 256
$output = Join-Path $PSScriptRoot '..\src\Snapkit.App\Assets\snapkit.ico'
$accent = [System.Drawing.Color]::FromArgb(255, 0x2F, 0x7B, 0xF5)

function New-IconFrame([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # Скруглённый квадрат цвета акцента
    $r = [float]($s * 0.22)
    $d = 2 * $r
    $max = [float]($s - 1)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($max - $d, 0, $d, $d, 270, 90)
    $path.AddArc($max - $d, $max - $d, $d, $d, 0, 90)
    $path.AddArc(0, $max - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $brush = New-Object System.Drawing.SolidBrush($accent)
    $g.FillPath($brush, $path)

    # Четыре белых уголка видоискателя
    $penWidth = [float][Math]::Max(1.5, $s * 0.075)
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, $penWidth)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $a = [float]($s * 0.24)
    $b = [float]($s - $s * 0.24)
    $l = [float]($s * 0.17)
    $g.DrawLines($pen, [System.Drawing.PointF[]]@((New-Object System.Drawing.PointF($a, ($a + $l))), (New-Object System.Drawing.PointF($a, $a)), (New-Object System.Drawing.PointF(($a + $l), $a))))
    $g.DrawLines($pen, [System.Drawing.PointF[]]@((New-Object System.Drawing.PointF(($b - $l), $a)), (New-Object System.Drawing.PointF($b, $a)), (New-Object System.Drawing.PointF($b, ($a + $l)))))
    $g.DrawLines($pen, [System.Drawing.PointF[]]@((New-Object System.Drawing.PointF($b, ($b - $l))), (New-Object System.Drawing.PointF($b, $b)), (New-Object System.Drawing.PointF(($b - $l), $b))))
    $g.DrawLines($pen, [System.Drawing.PointF[]]@((New-Object System.Drawing.PointF(($a + $l), $b)), (New-Object System.Drawing.PointF($a, $b)), (New-Object System.Drawing.PointF($a, ($b - $l)))))

    # Белая точка в центре
    $dot = [float][Math]::Max(2, $s * 0.14)
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $g.FillEllipse($white, [float](($s - $dot) / 2), [float](($s - $dot) / 2), $dot, $dot)

    $g.Dispose()
    $stream = New-Object System.IO.MemoryStream
    $bmp.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$stream.ToArray()
}

$frames = @()
foreach ($s in $sizes) { $frames += ,(New-IconFrame $s) }

# ICO: заголовок 6 байт, записи по 16 байт, затем данные PNG
$ms = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter($ms)
$w.Write([UInt16]0)
$w.Write([UInt16]1)
$w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim)
    $w.Write([byte]$dim)
    $w.Write([byte]0)
    $w.Write([byte]0)
    $w.Write([UInt16]1)
    $w.Write([UInt16]32)
    $w.Write([UInt32]$frames[$i].Length)
    $w.Write([UInt32]$offset)
    $offset += $frames[$i].Length
}
foreach ($f in $frames) { $w.Write([byte[]]$f) }
$w.Flush()

New-Item -ItemType Directory -Force -Path (Split-Path $output) | Out-Null
[System.IO.File]::WriteAllBytes($output, $ms.ToArray())
Write-Host "Значок сохранён: $((Resolve-Path $output).Path)"
