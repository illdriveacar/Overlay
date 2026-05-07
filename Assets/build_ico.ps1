Add-Type -AssemblyName System.Drawing
Set-Location -Path $PSScriptRoot

$sizes = 16, 32, 48, 64, 128, 256
$entries = @()

foreach ($sz in $sizes) {
    $pngPath = Join-Path $PSScriptRoot ("CECOM_{0}.png" -f $sz)
    if ($sz -eq 256) {
        # PNG payload for 256
        $data = [System.IO.File]::ReadAllBytes($pngPath)
    } else {
        # BMP DIB payload (no BITMAPFILEHEADER, BITMAPINFOHEADER + pixel data + AND mask)
        $img = [System.Drawing.Image]::FromFile($pngPath)
        $bmp = New-Object System.Drawing.Bitmap $img
        $img.Dispose()
        $rect = New-Object System.Drawing.Rectangle 0, 0, $sz, $sz
        $bmpData = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $stride = $bmpData.Stride
        $buf = New-Object byte[] ($stride * $sz)
        [System.Runtime.InteropServices.Marshal]::Copy($bmpData.Scan0, $buf, 0, $buf.Length)
        $bmp.UnlockBits($bmpData)
        $bmp.Dispose()

        # Build DIB: BITMAPINFOHEADER (40 bytes) + bottom-up XOR (32bpp BGRA) + AND mask (1bpp, all zero)
        $ms = New-Object System.IO.MemoryStream
        $bw = New-Object System.IO.BinaryWriter($ms)
        $bw.Write([uint32]40)        # biSize
        $bw.Write([int32]$sz)        # biWidth
        $bw.Write([int32]($sz * 2))  # biHeight (XOR + AND combined)
        $bw.Write([uint16]1)         # biPlanes
        $bw.Write([uint16]32)        # biBitCount
        $bw.Write([uint32]0)         # biCompression (BI_RGB)
        $bw.Write([uint32]0)         # biSizeImage
        $bw.Write([int32]0)          # biXPelsPerMeter
        $bw.Write([int32]0)          # biYPelsPerMeter
        $bw.Write([uint32]0)         # biClrUsed
        $bw.Write([uint32]0)         # biClrImportant

        # XOR pixels (bottom-up, BGRA)
        for ($y = $sz - 1; $y -ge 0; $y--) {
            $rowStart = $y * $stride
            $bw.Write($buf, $rowStart, $sz * 4)
        }
        # AND mask (1 bit per pixel, rows padded to 4-byte boundary, all zero)
        $maskRowBytes = [math]::Ceiling($sz / 32.0) * 4
        $zeros = New-Object byte[] ($maskRowBytes * $sz)
        $bw.Write($zeros)
        $bw.Flush()
        $data = $ms.ToArray()
        $ms.Dispose()
    }
    $entries += [pscustomobject]@{ Size = $sz; Data = $data }
}

# Build ICO container
$icoMs = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($icoMs)
# ICONDIR
$bw.Write([uint16]0)               # reserved
$bw.Write([uint16]1)               # type=1 (icon)
$bw.Write([uint16]$entries.Count)  # count

$headerSize = 6 + 16 * $entries.Count
$offset = $headerSize
foreach ($e in $entries) {
    $w = if ($e.Size -ge 256) { 0 } else { $e.Size }
    $h = if ($e.Size -ge 256) { 0 } else { $e.Size }
    $bw.Write([byte]$w)            # width
    $bw.Write([byte]$h)            # height
    $bw.Write([byte]0)             # color count
    $bw.Write([byte]0)             # reserved
    $bw.Write([uint16]1)           # planes
    $bw.Write([uint16]32)          # bit count
    $bw.Write([uint32]$e.Data.Length)
    $bw.Write([uint32]$offset)
    $offset += $e.Data.Length
}
foreach ($e in $entries) {
    $bw.Write($e.Data)
}
$bw.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'CECOM.ico'), $icoMs.ToArray())
$icoMs.Dispose()
"ICO: {0} bytes" -f (Get-Item (Join-Path $PSScriptRoot 'CECOM.ico')).Length
