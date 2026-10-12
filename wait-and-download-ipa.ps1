param(
    [string]$runId = ""
)

# VK Muhasebe - GitHub Actions Takip ve Otomatik Indirme Scripti
$repo = "vkrts02-cell/vk-muhasebe"

if ([string]::IsNullOrWhiteSpace($runId) -and $args.Count -gt 0) {
    $runId = $args[0]
}

if ([string]::IsNullOrWhiteSpace($runId)) {
    Write-Host "En guncel workflow run tespit ediliyor..." -ForegroundColor Yellow
    $latestRun = gh run list --repo $repo --limit 1 --json databaseId | ConvertFrom-Json
    if ($latestRun -and $latestRun.Count -gt 0) {
        $runId = [string]$latestRun[0].databaseId
    }
}

$desktopPaths = @(
    [Environment]::GetFolderPath('Desktop'),
    "C:\Users\mazik\OneDrive\Masaüstü",
    "C:\Users\mazik\Desktop"
) | Where-Object { ! [string]::IsNullOrEmpty($_) -and (Test-Path $_) } | Select-Object -Unique

$primaryDesktop = $desktopPaths | Select-Object -First 1
$targetFile = Join-Path $primaryDesktop "VK.ipa"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  VK Muhasebe - Otomatik iOS IPA Derleme Takipcisi" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "Hedef Repo: $repo | Run ID: $runId" -ForegroundColor Yellow
Write-Host "Hedef Konum: $targetFile" -ForegroundColor Yellow

$maxWaitSeconds = 1800 # 30 dakika
$elapsed = 0

while ($elapsed -lt $maxWaitSeconds) {
    try {
        $runJson = gh run view $runId --repo $repo --json status,conclusion | ConvertFrom-Json
        $status = $runJson.status
        $conclusion = $runJson.conclusion

        Write-Host "[$elapsed sn] Durum: $status | Sonuc: $conclusion" -ForegroundColor Gray

        if ($status -eq "completed") {
            if ($conclusion -eq "success") {
                Write-Host "`n[HARIKA] Derleme basariyla tamamlandi! IPA indiriliyor..." -ForegroundColor Green
                Start-Sleep -Seconds 5

                $tempDir = Join-Path $env:TEMP "vk-ipa-download-$(Get-Random)"
                New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
                
                # 1. gh run download dene
                Write-Host "Artifact indiriliyor (gh run download)..." -ForegroundColor Cyan
                gh run download $runId --repo $repo --name VK-iOS-IPA --dir $tempDir

                $downloadedIpa = Get-ChildItem -Path $tempDir -Filter "*.ipa" -Recurse | Select-Object -First 1
                
                # 2. Release asset dene (eger artifactten gelmediyse)
                if (-not $downloadedIpa) {
                    Write-Host "Release asset deneniyor..." -ForegroundColor Cyan
                    try {
                        $rel = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/tags/latest-ios" -UserAgent "Mozilla/5.0"
                        $asset = $rel.assets | Where-Object { $_.name -eq "VK.ipa" } | Select-Object -First 1
                        if ($asset -and $asset.browser_download_url) {
                            $assetDest = Join-Path $tempDir "VK.ipa"
                            Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $assetDest -UserAgent "Mozilla/5.0"
                            if (Test-Path $assetDest) {
                                $downloadedIpa = Get-Item $assetDest
                            }
                        }
                    } catch {
                        Write-Host "Release kontrol hatasi: $($_.Exception.Message)" -ForegroundColor DarkGray
                    }
                }

                if ($downloadedIpa -and (Test-Path $downloadedIpa.FullName) -and ((Get-Item $downloadedIpa.FullName).Length -gt 1000000)) {
                    Copy-Item -Path $downloadedIpa.FullName -Destination $targetFile -Force
                    $sizeMB = [math]::Round((Get-Item $targetFile).Length / 1MB, 2)
                    
                    foreach ($dp in $desktopPaths) {
                        $dest = Join-Path $dp "VK.ipa"
                        if ($dest -ne $targetFile) {
                            Copy-Item -Path $targetFile -Destination $dest -Force -ErrorAction SilentlyContinue
                        }
                    }

                    Write-Host "`n[TAMAMLANDI] Yeni VK.ipa Masaustune kaydedildi! ($sizeMB MB)" -ForegroundColor Green
                    Write-Host "Konum: $targetFile" -ForegroundColor Green
                    explorer.exe /select,"$targetFile"
                    
                    Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue
                    exit 0
                } else {
                    Write-Host "`n[HATA] IPA dosyasi indirilemedi veya boyutu cok kucuk." -ForegroundColor Red
                    exit 1
                }
            } else {
                Write-Host "`n[HATA] Derleme basarisiz oldu: $conclusion" -ForegroundColor Red
                exit 1
            }
        }
    } catch {
        Write-Host "Kontrol sirasinda hata: $($_.Exception.Message)" -ForegroundColor DarkGray
    }

    Start-Sleep -Seconds 30
    $elapsed += 30
}

Write-Host "`nZaman asimi: 30 dakika icinde tamamlanmadi." -ForegroundColor Red
exit 1
