$ErrorActionPreference = "Stop"

Write-Host "==================================================="
Write-Host "  VK ON MUHASEBE - KUSURSUZ IKON VE KURULUM DERLEME"
Write-Host "==================================================="

Add-Type -AssemblyName System.Drawing

# 1. Kaynak PNG ve ICO olusturucu fonksiyon
function New-TrueWindowsIco {
    param(
        [string]$SourcePngPath,
        [string]$DestIcoPath
    )

    $srcImg = [System.Drawing.Image]::FromFile($SourcePngPath)
    $sizes = @(16, 24, 32, 48, 64, 128, 256)
    
    $imgBuffers = @()
    
    foreach ($size in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)
        $g.DrawImage($srcImg, 0, 0, $size, $size)
        $g.Dispose()

        if ($size -le 128) {
            $ms = New-Object System.IO.MemoryStream
            $bw = New-Object System.IO.BinaryWriter $ms

            $bw.Write([UInt32]40)
            $bw.Write([Int32]$size)
            $bw.Write([Int32]($size * 2))
            $bw.Write([UInt16]1)
            $bw.Write([UInt16]32)
            $bw.Write([UInt32]0)
            $xorSize = $size * $size * 4
            $andRowBytes = [Math]::Ceiling($size / 32.0) * 4
            $andSize = [int]($andRowBytes * $size)
            $bw.Write([UInt32]($xorSize + $andSize))
            $bw.Write([Int32]0)
            $bw.Write([Int32]0)
            $bw.Write([UInt32]0)
            $bw.Write([UInt32]0)

            $rect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
            $bmpData = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            
            $pixelBytes = New-Object byte[] ($size * 4)
            for ($y = $size - 1; $y -ge 0; $y--) {
                $rowPtr = [IntPtr]($bmpData.Scan0.ToInt64() + ($y * $bmpData.Stride))
                [System.Runtime.InteropServices.Marshal]::Copy($rowPtr, $pixelBytes, 0, $pixelBytes.Length)
                $bw.Write($pixelBytes)
            }
            $bmp.UnlockBits($bmpData)

            $andBytes = New-Object byte[] $andSize
            $bw.Write($andBytes)

            $bw.Flush()
            $rawBytes = $ms.ToArray()
            $bw.Close()
            $ms.Dispose()

            $imgBuffers += ,@($size, $rawBytes)
        } else {
            $ms = New-Object System.IO.MemoryStream
            $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
            $rawBytes = $ms.ToArray()
            $ms.Dispose()
            $imgBuffers += ,@($size, $rawBytes)
        }
        $bmp.Dispose()
    }
    $srcImg.Dispose()

    $fs = New-Object System.IO.FileStream $DestIcoPath, ([System.IO.FileMode]::Create)
    $bw = New-Object System.IO.BinaryWriter $fs

    $bw.Write([UInt16]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]$imgBuffers.Count)

    $offset = 6 + ($imgBuffers.Count * 16)

    foreach ($entry in $imgBuffers) {
        $size = $entry[0]
        $data = $entry[1]
        $w = if ($size -ge 256) { [byte]0 } else { [byte]$size }
        $h = if ($size -ge 256) { [byte]0 } else { [byte]$size }

        $bw.Write([byte]$w)
        $bw.Write([byte]$h)
        $bw.Write([byte]0)
        $bw.Write([byte]0)
        $bw.Write([UInt16]1)
        $bw.Write([UInt16]32)
        $bw.Write([UInt32]$data.Length)
        $bw.Write([UInt32]$offset)

        $offset += $data.Length
    }

    foreach ($entry in $imgBuffers) {
        $data = $entry[1]
        $bw.Write($data)
    }

    $bw.Flush()
    $bw.Close()
    $fs.Close()
}

$sourceLogoPng = "E:\avalonia yedek\ermaymuhasebe\ErmayMuhasebe.Avalonia\ErmayMuhasebe.Avalonia\Assets\vk_logo_master.png"

# 2. Tum Assets klasorlerine DIB multi-size ico dosyasini ve png logolari dagit
$assetDirs = @(
    "E:\avalonia yedek\ermaymuhasebe\ErmayMuhasebe.Avalonia\ErmayMuhasebe.Avalonia\Assets",
    "E:\avalonia yedek\ermaymuhasebe\ErmayMuhasebe.Avalonia\ErmayMuhasebe.Avalonia.Desktop\Assets"
)

foreach ($dir in $assetDirs) {
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    New-TrueWindowsIco $sourceLogoPng (Join-Path $dir "app.ico")
    New-TrueWindowsIco $sourceLogoPng (Join-Path $dir "avalonia-logo.ico")
    New-TrueWindowsIco $sourceLogoPng (Join-Path $dir "app_icon.ico")
    Copy-Item $sourceLogoPng (Join-Path $dir "vk_logo.png") -Force
    Copy-Item $sourceLogoPng (Join-Path $dir "app_icon.png") -Force
    Write-Host "Tum ikonlar guncellendi: $dir"
}

# 3. Publish Desktop
$publishDesktop = "E:\avalonia yedek\ermaymuhasebe\Publish_Output\Desktop"
$publishInstaller = "E:\avalonia yedek\ermaymuhasebe\Publish_Output\Installer"
if (Test-Path $publishDesktop) { Remove-Item -Path $publishDesktop -Recurse -Force }
New-Item -ItemType Directory -Path $publishDesktop -Force | Out-Null
if (-not (Test-Path $publishInstaller)) { New-Item -ItemType Directory -Path $publishInstaller -Force | Out-Null }

Write-Host "dotnet publish calistiriliyor..."
& dotnet publish "E:\avalonia yedek\ermaymuhasebe\ErmayMuhasebe.Avalonia\ErmayMuhasebe.Avalonia.Desktop\ErmayMuhasebe.Avalonia.Desktop.csproj" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=false -o $publishDesktop
if ($LASTEXITCODE -ne 0) { throw "dotnet publish basarisiz oldu!" }

# Assets klasorunu publish altina kopyala
$publishAssets = Join-Path $publishDesktop "Assets"
if (-not (Test-Path $publishAssets)) { New-Item -ItemType Directory -Path $publishAssets -Force | Out-Null }
New-TrueWindowsIco $sourceLogoPng (Join-Path $publishAssets "app.ico")
New-TrueWindowsIco $sourceLogoPng (Join-Path $publishAssets "avalonia-logo.ico")
New-TrueWindowsIco $sourceLogoPng (Join-Path $publishAssets "app_icon.ico")
Copy-Item $sourceLogoPng (Join-Path $publishAssets "vk_logo.png") -Force
Copy-Item $sourceLogoPng (Join-Path $publishAssets "app_icon.png") -Force

# 4. Inno Setup ile derle
$isccPath = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $isccPath)) { $isccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" }
if (-not (Test-Path $isccPath)) { $isccPath = "C:\Program Files\Inno Setup 6\ISCC.exe" }

Write-Host "Inno Setup Compiler: $isccPath"
& "$isccPath" "E:\avalonia yedek\ermaymuhasebe\vk_setup.iss"
if ($LASTEXITCODE -ne 0) { throw "ISCC derlemesi basarisiz!" }

# 5. Kurulum dosyasini ek isimlerle kopyala (Windows Explorer onbellegini asmak icin)
$mainSetup = "E:\avalonia yedek\ermaymuhasebe\Publish_Output\Installer\VK_On_Muhasebe_Kurulum.exe"
$altSetup1 = "E:\avalonia yedek\ermaymuhasebe\Publish_Output\Installer\VK_Kurulum.exe"
$altSetup2 = "E:\avalonia yedek\ermaymuhasebe\Publish_Output\Installer\VK_Setup.exe"
$altSetup3 = "E:\avalonia yedek\ermaymuhasebe\Publish_Output\Installer\VK_Setup_v1.0.0.exe"

Copy-Item $mainSetup $altSetup1 -Force
Copy-Item $mainSetup $altSetup2 -Force
Copy-Item $mainSetup $altSetup3 -Force

Write-Host "Kurulum dosyalari hazirlandi:"
Write-Host " - $mainSetup"
Write-Host " - $altSetup1"
Write-Host " - $altSetup2"

# 6. Windows Explorer Onbellegi (Istege bagli)
# Write-Host "Kurulum tamamlandi."

Write-Host "=== ISLEM BASARIYLA TAMAMLANDI ==="
