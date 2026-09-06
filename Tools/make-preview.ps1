Add-Type -AssemblyName System.Drawing

$outDir = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\About'))

function C([int]$r, [int]$g, [int]$b) { [System.Drawing.Color]::FromArgb(255, $r, $g, $b) }
function CA([int]$a, [int]$r, [int]$g, [int]$b) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }

# One uniform crop per band: the whole point of field-wide rotation is that a field is never
# a checkerboard of mixed crops. The third band is the fallow step, deliberately bare.
$bands = @(
    @{ soil = (C 58 43 30); crop = (C 130 176 78);  h = 0.17; scale = 0.70; fallow = $false }
    @{ soil = (C 66 49 34); crop = (C 214 184 76);  h = 0.23; scale = 0.88; fallow = $false }
    @{ soil = (C 74 57 41); crop = (C 0 0 0);       h = 0.20; scale = 1.00; fallow = $true  }
    @{ soil = (C 70 52 36); crop = (C 104 162 96);  h = 0.40; scale = 1.10; fallow = $false }
)

function Draw-Sprout {
    param($g, [double]$cx, [double]$cy, [double]$s, $color, $dark)

    $stem = New-Object System.Drawing.Pen($dark, [float]([Math]::Max(1.4, $s * 0.16)))
    $g.DrawLine($stem, [float]$cx, [float]($cy + $s * 0.5), [float]$cx, [float]($cy - $s * 0.35))
    $stem.Dispose()

    $brush = New-Object System.Drawing.SolidBrush($color)
    $lw = $s * 0.95
    $lh = $s * 0.52
    $g.FillEllipse($brush, [float]($cx - $lw), [float]($cy - $s * 0.30), [float]$lw, [float]$lh)
    $g.FillEllipse($brush, [float]$cx, [float]($cy - $s * 0.30), [float]$lw, [float]$lh)
    $g.FillEllipse($brush, [float]($cx - $s * 0.36), [float]($cy - $s * 0.78), [float]($s * 0.72), [float]($s * 0.62))
    $brush.Dispose()
}

function Draw-Field {
    param($g, [int]$x, [int]$y, [int]$w, [int]$h, [double]$unit)

    $cursor = [double]$y
    foreach ($band in $bands) {
        $bh = [int]([double]$h * $band.h)
        $rect = New-Object System.Drawing.Rectangle($x, [int]$cursor, $w, $bh)

        $soilBrush = New-Object System.Drawing.SolidBrush($band.soil)
        $g.FillRectangle($soilBrush, $rect)
        $soilBrush.Dispose()

        # tilled furrows
        $furrow = New-Object System.Drawing.Pen((CA 46 0 0 0), 1)
        $spacing = [int]([Math]::Max(7, $unit * 0.55))
        for ($ly = $rect.Top + 4; $ly -lt $rect.Bottom; $ly += $spacing) {
            $g.DrawLine($furrow, $x, $ly, $x + $w, $ly)
        }
        $furrow.Dispose()

        if ($band.fallow) {
            # bare soil: a few clods so the fallow band still reads as worked ground
            $clod = New-Object System.Drawing.SolidBrush((CA 70 20 12 6))
            $rand = New-Object System.Random(7)
            for ($i = 0; $i -lt [int]($w / 26); $i++) {
                $cxv = $rand.Next($x + 6, $x + $w - 6)
                $cyv = $rand.Next($rect.Top + 6, [Math]::Max($rect.Top + 7, $rect.Bottom - 6))
                $d = $unit * 0.35
                $g.FillEllipse($clod, [float]$cxv, [float]$cyv, [float]$d, [float]($d * 0.6))
            }
            $clod.Dispose()
        }
        else {
            $dark = [System.Drawing.Color]::FromArgb(255,
                [int]($band.crop.R * 0.55), [int]($band.crop.G * 0.55), [int]($band.crop.B * 0.55))
            $s = $unit * $band.scale
            $stepX = $s * 2.1
            $stepY = $s * 1.5
            $row = 0
            for ($py = $rect.Top + $s * 0.9; $py -lt $rect.Bottom; $py += $stepY) {
                $offset = 0.0
                if ($row % 2 -ne 0) { $offset = $stepX * 0.5 }
                for ($px = $x + $s + $offset; $px -lt $x + $w; $px += $stepX) {
                    Draw-Sprout $g $px $py $s $band.crop $dark
                }
                $row++
            }
        }

        $cursor += $bh
    }
}

function Draw-Greenhouse {
    param($g, [int]$x, [int]$y, [int]$w, [int]$h)

    # warm interior glow
    $glowRect = New-Object System.Drawing.Rectangle($x, $y, $w, $h)
    $glow = New-Object System.Drawing.Drawing2D.LinearGradientBrush($glowRect, (CA 70 255 214 130), (CA 120 120 190 150), 90.0)
    $g.FillRectangle($glow, $glowRect)
    $glow.Dispose()

    # two sprouts inside, so it reads as a greenhouse and not a window
    $u = $h * 0.20
    Draw-Sprout $g ($x + $w * 0.32) ($y + $h * 0.82) $u (C 150 214 122) (C 80 120 66)
    Draw-Sprout $g ($x + $w * 0.66) ($y + $h * 0.86) ($u * 1.15) (C 150 214 122) (C 80 120 66)

    # glazing bars
    $mullion = New-Object System.Drawing.Pen((CA 120 190 235 245), 1)
    for ($i = 1; $i -lt 4; $i++) {
        $mx = $x + [int]($w * $i / 4.0)
        $g.DrawLine($mullion, $mx, $y, $mx, $y + $h)
    }
    for ($i = 1; $i -lt 3; $i++) {
        $my = $y + [int]($h * $i / 3.0)
        $g.DrawLine($mullion, $x, $my, $x + $w, $my)
    }
    $mullion.Dispose()

    $frame = New-Object System.Drawing.Pen((CA 210 200 240 250), 3)
    $g.DrawRectangle($frame, $x, $y, $w, $h)
    $frame.Dispose()

    # roof vent, hinged open: module 4 bleeds excess heat off through it
    $vx = $x + [int]($w * 0.46)
    $vw = [int]($w * 0.40)
    $lift = [int]($h * 0.30)
    $pts = @(
        (New-Object System.Drawing.Point($vx, $y)),
        (New-Object System.Drawing.Point(($vx + $vw), ($y - $lift))),
        (New-Object System.Drawing.Point(($vx + $vw), ($y - $lift + [int]($h * 0.09)))),
        (New-Object System.Drawing.Point($vx, ($y + [int]($h * 0.09))))
    )
    $ventFill = New-Object System.Drawing.SolidBrush((CA 95 190 235 245))
    $g.FillPolygon($ventFill, $pts)
    $ventFill.Dispose()
    $ventPen = New-Object System.Drawing.Pen((CA 220 205 242 250), 2)
    $g.DrawPolygon($ventPen, $pts)
    $ventPen.Dispose()
}

function Render {
    param([int]$W, [int]$H, [string]$dir, [string]$file, [bool]$square)

    $bmp = New-Object System.Drawing.Bitmap($W, $H)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias

    $full = New-Object System.Drawing.Rectangle(0, 0, $W, $H)
    $sky = New-Object System.Drawing.Drawing2D.LinearGradientBrush($full, (C 14 24 31), (C 30 51 42), 90.0)
    $g.FillRectangle($sky, $full)
    $sky.Dispose()

    $fieldFrac = 0.55
    if ($square) { $fieldFrac = 0.58 }
    $fieldTop = [int]($H * $fieldFrac)
    $unit = $W / 74.0
    Draw-Field $g 0 $fieldTop $W ($H - $fieldTop) $unit

    # haze along the horizon
    $hazeH = [int]($H * 0.10)
    $hazeY = $fieldTop - $hazeH
    $hazeRect = New-Object System.Drawing.Rectangle(0, $hazeY, $W, $hazeH)
    $haze = New-Object System.Drawing.Drawing2D.LinearGradientBrush($hazeRect, (CA 0 22 34 27), (CA 165 22 34 27), 90.0)
    $g.FillRectangle($haze, $hazeRect)
    $haze.Dispose()

    # greenhouse straddles the horizon on the right
    $ghW = [int]($W * 0.30)
    $ghH = [int]($ghW * 0.52)
    $ghX = $W - $ghW - [int]($W * 0.06)
    $ghY = $fieldTop - [int]($ghH * 0.62)
    Draw-Greenhouse $g $ghX $ghY $ghW $ghH

    # ---- text
    $pad = [int]($W * 0.055)
    $titleSize = [int]($W * 0.070)
    if ($square) { $titleSize = [int]($W * 0.090) }
    $subSize = [int]($titleSize * 0.40)
    $tagSize = [int]($titleSize * 0.29)

    $titleFont = New-Object System.Drawing.Font('Segoe UI', $titleSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $subFont = New-Object System.Drawing.Font('Segoe UI', $subSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)

    $white = New-Object System.Drawing.SolidBrush((C 246 249 244))
    $green = New-Object System.Drawing.SolidBrush((C 158 218 124))
    $shadow = New-Object System.Drawing.SolidBrush((CA 155 0 0 0))

    $y = [int]($H * 0.075)
    foreach ($line in @('SMART', 'AGRICULTURE')) {
        $g.DrawString($line, $titleFont, $shadow, ($pad + 3), ($y + 3))
        $g.DrawString($line, $titleFont, $white, $pad, $y)
        $y += [int]($titleSize * 1.00)
    }

    $y += [int]($titleSize * 0.08)
    $g.DrawString('CLIMATE & COLONY LOGISTICS', $subFont, $shadow, ($pad + 2), ($y + 2))
    $g.DrawString('CLIMATE & COLONY LOGISTICS', $subFont, $green, $pad, $y)
    $y += [int]($subSize * 1.55)

    $rule = New-Object System.Drawing.Pen((CA 130 158 218 124), 2)
    $g.DrawLine($rule, $pad, $y, ($pad + [int]($W * 0.40)), $y)
    $rule.Dispose()

    # version chip, bottom right
    $verFont = New-Object System.Drawing.Font('Segoe UI', $tagSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $verText = 'RimWorld 1.6'
    $vs = $g.MeasureString($verText, $verFont)
    $vx = $W - $vs.Width - $pad
    $vy = $H - $vs.Height - [int]($H * 0.045)
    $chipRect = New-Object System.Drawing.RectangleF(($vx - 14), ($vy - 8), ($vs.Width + 28), ($vs.Height + 14))
    $chip = New-Object System.Drawing.SolidBrush((CA 195 10 17 14))
    $g.FillRectangle($chip, $chipRect)
    $chip.Dispose()
    $chipPen = New-Object System.Drawing.Pen((CA 130 158 218 124), 1)
    $g.DrawRectangle($chipPen, $chipRect.X, $chipRect.Y, $chipRect.Width, $chipRect.Height)
    $chipPen.Dispose()
    $g.DrawString($verText, $verFont, $white, $vx, $vy)

    foreach ($d in @($titleFont, $subFont, $verFont, $white, $green, $shadow)) { $d.Dispose() }
    $g.Dispose()

    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    $path = Join-Path $dir $file
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host ("wrote {0}  {1}x{2}  {3:N0} bytes" -f $path, $W, $H, (Get-Item $path).Length)
}

# Preview.png ships with the mod; the square cover is only for the Workshop collection page.
$mediaDir = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Media'))
Render 1024 576 $outDir 'Preview.png' $false
Render 640 640 $mediaDir 'CollectionCover.png' $true
