# Рисует значок Snapkit и собирает его в ICO с несколькими размерами.
# Запуск: powershell -File tools/make-icon.ps1
# Результат: src/Snapkit.App/Assets/snapkit.ico
#
# Уголки видоискателя и точка рисуются целыми пикселями без сглаживания,
# иначе на маленьких кадрах (16–24 px, значок в трее) они расплываются.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$output = Join-Path $PSScriptRoot '..\src\Snapkit.App\Assets\snapkit.ico'
$accent = [System.Drawing.Color]::FromArgb(255, 0x2F, 0x7B, 0xF5)

function New-IconFrame([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)

    # Скруглённый квадрат цвета акцента — со сглаживанием края
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $d = [float]([Math]::Max(3, [Math]::Round($s * 0.2)) * 2)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($s - $d, 0, $d, $d, 270, 90)
    $path.AddArc($s - $d, $s - $d, $d, $d, 0, 90)
    $path.AddArc(0, $s - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush($accent)), $path)

    # Уголки видоискателя: прямоугольники по пиксельной сетке, без сглаживания
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::None
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $t = [int][Math]::Max(1, [Math]::Round($s * 0.09))   # толщина линии
    $p = [int][Math]::Round($s * 0.19)                   # отступ от края
    $l = [int][Math]::Max($t + 1, [Math]::Round($s * 0.22)) # длина плеча
    $far = $s - $p                                       # правая/нижняя граница (исключительно)
    foreach ($corner in @(@($p, $p, 1, 1), @($far, $p, -1, 1), @($far, $far, -1, -1), @($p, $far, 1, -1))) {
        $x = $corner[0]; $y = $corner[1]; $dx = $corner[2]; $dy = $corner[3]
        $hx = if ($dx -gt 0) { $x } else { $x - $l }
        $hy = if ($dy -gt 0) { $y } else { $y - $t }
        $g.FillRectangle($white, $hx, $hy, $l, $t)          # горизонтальное плечо
        $vx = if ($dx -gt 0) { $x } else { $x - $t }
        $vy = if ($dy -gt 0) { $y } else { $y - $l }
        $g.FillRectangle($white, $vx, $vy, $t, $l)          # вертикальное плечо
    }

    # Точка в центре: на маленьких кадрах — ровный квадрат, на крупных — круг
    $dot = [int][Math]::Max(2, [Math]::Round($s * 0.125))
    if (($s - $dot) % 2 -ne 0) { $dot += 1 }               # чтобы точка стояла ровно по центру
    $o = ($s - $dot) / 2
    if ($s -le 24) {
        $g.FillRectangle($white, $o, $o, $dot, $dot)
    } else {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.FillEllipse($white, [float]$o, [float]$o, [float]$dot, [float]$dot)
    }

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
