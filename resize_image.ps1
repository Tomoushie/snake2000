Add-Type -AssemblyName System.Drawing
$src = 'E:\Corpus\Snake2000\Images\1.png'
$dst = 'E:\Corpus\Snake2000\assets\screenshot.png'
if (!(Test-Path 'assets')) { New-Item -ItemType Directory -Path 'assets' | Out-Null }
$img = [System.Drawing.Image]::FromFile($src)
$thumb = New-Object System.Drawing.Bitmap 800,600
$g = [System.Drawing.Graphics]::FromImage($thumb)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($img,0,0,800,600)
$thumb.Save($dst,[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$img.Dispose()
$thumb.Dispose()
Write-Output 'RESIZED'