Add-Type -AssemblyName System.Drawing

# Textures live under 1.6 so the existing Deploy target picks them up with the rest of the version folder.
$outDir = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\1.6\Textures\Things\Building\SACL'))
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

$S = 128

function CA([int]$a, [int]$r, [int]$g, [int]$b) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }

function New-Canvas {
    $bmp = New-Object System.Drawing.Bitmap($S, $S, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    return @($bmp, $g)
}

function Save-Canvas {
    param($bmp, $g, [string]$name)
    $g.Dispose()
    $path = Join-Path $outDir $name
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host ("wrote {0}  {1}x{1}  {2:N0} bytes" -f $path, $S, (Get-Item $path).Length)
}

function Fill-Rect { param($g, $col, [double]$x, [double]$y, [double]$w, [double]$h)
    $b = New-Object System.Drawing.SolidBrush($col)
    $g.FillRectangle($b, [float]$x, [float]$y, [float]$w, [float]$h)
    $b.Dispose()
}

function Draw-Rect { param($g, $col, [double]$t, [double]$x, [double]$y, [double]$w, [double]$h)
    $p = New-Object System.Drawing.Pen($col, [float]$t)
    $g.DrawRectangle($p, [float]$x, [float]$y, [float]$w, [float]$h)
    $p.Dispose()
}

function Draw-Line { param($g, $col, [double]$t, [double]$x1, [double]$y1, [double]$x2, [double]$y2)
    $p = New-Object System.Drawing.Pen($col, [float]$t)
    $g.DrawLine($p, [float]$x1, [float]$y1, [float]$x2, [float]$y2)
    $p.Dispose()
}

function Fill-Ellipse { param($g, $col, [double]$cx, [double]$cy, [double]$r)
    $b = New-Object System.Drawing.SolidBrush($col)
    $g.FillEllipse($b, [float]($cx - $r), [float]($cy - $r), [float]($r * 2), [float]($r * 2))
    $b.Dispose()
}

# ---------------------------------------------------------------- glazing frame
# Sits on a walkable cell and is drawn above the crops, so the pane has to stay see-through:
# a solid panel would hide the field it is meant to be warming.
function Make-Skylight {
    $c = New-Canvas; $bmp = $c[0]; $g = $c[1]
    $m = 6.0
    $in = $S - 2 * $m

    # pale glass tint
    Fill-Rect $g (CA 34 150 205 235) $m $m $in $in

    # reflection streak across the pane
    $streak = New-Object System.Drawing.Drawing2D.GraphicsPath
    $streak.AddPolygon(@(
        (New-Object System.Drawing.PointF([float]($m + 6), [float]($S - $m - 6))),
        (New-Object System.Drawing.PointF([float]($m + 34), [float]($S - $m - 6))),
        (New-Object System.Drawing.PointF([float]($S - $m - 6), [float]($m + 6))),
        (New-Object System.Drawing.PointF([float]($S - $m - 34), [float]($m + 6)))
    ))
    $sb = New-Object System.Drawing.SolidBrush((CA 42 255 255 255))
    $g.FillPath($sb, $streak)
    $sb.Dispose(); $streak.Dispose()

    # glazing bars
    Draw-Line $g (CA 205 176 206 218) 5 ($S / 2) $m ($S / 2) ($S - $m)
    Draw-Line $g (CA 205 176 206 218) 5 $m ($S / 2) ($S - $m) ($S / 2)
    Draw-Line $g (CA 110 240 250 252) 2 ($S / 2 - 2) $m ($S / 2 - 2) ($S - $m)
    Draw-Line $g (CA 110 240 250 252) 2 $m ($S / 2 - 2) ($S - $m) ($S / 2 - 2)

    # aluminium frame, doubled so it reads at small zoom
    Draw-Rect $g (CA 240 168 198 212) 9 $m $m $in $in
    Draw-Rect $g (CA 200 96 124 138) 2 ($m + 5) ($m + 5) ($in - 10) ($in - 10)

    # corner bolts
    $lo = $m + 11
    $hi = $S - $m - 11
    foreach ($p in @(@($lo, $lo), @($hi, $lo), @($lo, $hi), @($hi, $hi))) {
        Fill-Ellipse $g (CA 220 70 95 108) $p[0] $p[1] 4.0
    }

    Save-Canvas $bmp $g 'Skylight.png'
}

# ---------------------------------------------------------------- roof vent
# Reads as a hatch in the ceiling with its flap propped open, so it is obvious at a glance that
# this is the thing letting heat out.
function Make-RoofVent {
    $c = New-Canvas; $bmp = $c[0]; $g = $c[1]
    $m = 8.0
    $in = $S - 2 * $m

    # dark opening
    Fill-Rect $g (CA 255 48 54 57) $m $m $in $in

    # louvre slats
    $n = 4
    $slotH = $in / $n
    for ($i = 0; $i -lt $n; $i++) {
        $y = $m + $i * $slotH + 3
        Fill-Rect $g (CA 255 158 170 176) ($m + 5) $y ($in - 10) ($slotH - 9)
        Fill-Rect $g (CA 255 96 105 110) ($m + 5) ($y + $slotH - 11) ($in - 10) 4
    }

    # hinge rail down one side
    Fill-Rect $g (CA 255 118 128 134) $m $m 7 $in

    # steel frame
    Draw-Rect $g (CA 255 178 190 196) 7 $m $m $in $in
    Draw-Rect $g (CA 200 62 70 74) 2 ($m + 4) ($m + 4) ($in - 8) ($in - 8)

    Save-Canvas $bmp $g 'RoofVent.png'
}

# ---------------------------------------------------------------- field crate
# Kept pale and low-saturation on purpose: the crate is made from stuff, so RimWorld multiplies this
# texture by the wood's colour. Anything already brown would come out muddy on dark woods.
function Make-FieldCrate {
    $c = New-Canvas; $bmp = $c[0]; $g = $c[1]
    $m = 5.0
    $in = $S - 2 * $m

    # plank field
    Fill-Rect $g (CA 255 226 216 196) $m $m $in $in

    # vertical planks with a shadowed gap between each
    $planks = 4
    $pw = $in / $planks
    for ($i = 0; $i -lt $planks; $i++) {
        $x = $m + $i * $pw
        Fill-Rect $g (CA 255 238 229 210) ($x + 2) ($m + 2) ($pw - 5) ($in - 4)
        if ($i -gt 0) { Draw-Line $g (CA 140 92 76 56) 3 $x ($m + 2) $x ($S - $m - 2) }
    }

    # inner rim, so it reads as an open crate rather than a plain box. Translucent on purpose: the
    # plank gaps have to keep showing through or the crate loses its slatted look.
    Draw-Rect $g (CA 175 74 60 44) 4 ($m + 13) ($m + 13) ($in - 26) ($in - 26)
    Fill-Rect $g (CA 95 188 176 156) ($m + 16) ($m + 16) ($in - 32) ($in - 32)

    # outer frame and corner brackets
    Draw-Rect $g (CA 255 150 124 92) 6 $m $m $in $in
    $lo = $m
    $hi = $S - $m - 16
    foreach ($p in @(@($lo, $lo), @($hi, $lo), @($lo, $hi), @($hi, $hi))) {
        Fill-Rect $g (CA 235 168 142 108) $p[0] $p[1] 16 16
    }

    Save-Canvas $bmp $g 'FieldCrate.png'
}

Make-Skylight
Make-RoofVent
Make-FieldCrate
