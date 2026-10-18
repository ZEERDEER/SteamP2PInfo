Add-Type -AssemblyName System.Drawing
$sizes = 16,24,32,48,64,128,256
$pngs = @()
foreach ($s in $sizes) {
  $bmp = New-Object System.Drawing.Bitmap($s, $s)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'
  $g.Clear([System.Drawing.Color]::Transparent)

  $bg = [System.Drawing.Color]::FromArgb(255, 32, 32, 32)
  $accent = [System.Drawing.Color]::FromArgb(255, 76, 194, 255)
  $bgBrush = New-Object System.Drawing.SolidBrush($bg)
  $accentPen = New-Object System.Drawing.Pen($accent, [Math]::Max(1.5, $s / 10))
  $g.FillEllipse($bgBrush, 0, 0, $s - 1, $s - 1)

  # two connected dots (network motif)
  $r = [Math]::Max(1.5, $s / 10)
  $g.DrawLine($accentPen, $s * 0.32, $s * 0.68, $s * 0.68, $s * 0.32)
  $g.FillEllipse((New-Object System.Drawing.SolidBrush($accent)), $s * 0.22, $s * 0.58, $r * 2, $r * 2)
  $g.FillEllipse((New-Object System.Drawing.SolidBrush($accent)), $s * 0.68 - $r, $s * 0.22, $r * 2, $r * 2)

  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $pngs += , ($ms.ToArray())
  $g.Dispose(); $bmp.Dispose()
}

# assemble a PNG-compressed ICO
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter($out)
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $s = $sizes[$i]; $data = $pngs[$i]
  $dim = 0
  if ($s -lt 256) { $dim = $s }
  $w.Write([Byte]$dim)
  $w.Write([Byte]$dim)
  $w.Write([Byte]0); $w.Write([Byte]0)
  $w.Write([UInt16]1); $w.Write([UInt16]32)
  $w.Write([UInt32]$data.Length); $w.Write([UInt32]$offset)
  $offset += $data.Length
}
foreach ($data in $pngs) { $w.Write($data) }
$w.Flush()
New-Item -ItemType Directory -Force -Path "SteamP2PInfo/Resources" | Out-Null
[System.IO.File]::WriteAllBytes("SteamP2PInfo/Resources/app.ico", $out.ToArray())
Write-Host "wrote SteamP2PInfo/Resources/app.ico"
