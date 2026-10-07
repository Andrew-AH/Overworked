# Regenerates the app's icon assets from art/overworked.png.
Add-Type -AssemblyName System.Drawing
$source = [System.Drawing.Image]::FromFile("$PSScriptRoot/overworked.png")
$assets = "$PSScriptRoot/../src/Overworked.App/Assets"

function Resize([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.DrawImage($source, 0, 0, $size, $size)
    $g.Dispose()
    $bitmap
}

function PngBytes([int]$size) {
    $bitmap = Resize $size
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    , $stream.ToArray()
}

# In-app images: title-bar mark and empty state.
foreach ($size in 64, 256) { [IO.File]::WriteAllBytes("$assets/logo-$size.png", (PngBytes $size)) }

# Windows .ico with PNG-compressed frames.
$sizes = 16, 24, 32, 48, 64, 128, 256
$frames = $sizes | ForEach-Object { , (PngBytes $_) }
$out = New-Object System.IO.BinaryWriter([IO.File]::Create("$assets/overworked.ico"))
$out.Write([uint16]0); $out.Write([uint16]1); $out.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $out.Write([byte]$dim); $out.Write([byte]$dim); $out.Write([byte]0); $out.Write([byte]0)
    $out.Write([uint16]1); $out.Write([uint16]32)
    $out.Write([uint32]$frames[$i].Length); $out.Write([uint32]$offset)
    $offset += $frames[$i].Length
}
foreach ($frame in $frames) { $out.Write($frame) }
$out.Close()
$source.Dispose()
