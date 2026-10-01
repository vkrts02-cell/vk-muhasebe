# VK Muhasebe - GitHub Actions Takip ve Otomatik Indirme Scripti
$repo = "vkrts2/vk-muhasebe"
$desktopPaths = @(
    [Environment]::GetFolderPath('Desktop'),
    "C:\Users\mazik\OneDrive\Masaüstü",
    "C:\Users\mazik\Desktop"
) | Where-Object { ! [string]::IsNullOrEmpty($_) -and (Test-Path $_) } | Select-Object -Unique
$targetFile = Join-Path ($desktopPaths | Select-Object -First 1) "VK.ipa"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  VK Muhasebe - Otomatik iOS IPA Derleme Takipcisi" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 1. En son commit'i al
$latestLocalCommit = (git rev-parse HEAD).Trim()
Write-Host "Yerel En Guncel Commit: $latestLocalCommit" -ForegroundColor Yellow

Write-Host "`nGitHub Actions derlemesi bekleniyor..." -ForegroundColor Cyan

$maxWaitSeconds = 1800 # 30 dakika
$elapsed = 0

while ($elapsed -lt $maxWaitSeconds) {
    try {
        $runs = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/actions/runs?per_page=1" -UserAgent "Mozilla/5.0"
        if ($runs.workflow_runs.Count -gt 0) {
            $latestRun = $runs.workflow_runs[0]
            $status = $latestRun.status
            $conclusion = $latestRun.conclusion
            $headSha = $latestRun.head_sha
            $runUrl = $latestRun.html_url

            Write-Host "[$elapsed sn] Durum: $status | Sonuc: $conclusion | Commit: $($headSha.Substring(0,7))" -ForegroundColor Gray

            if ($headSha -eq $latestLocalCommit -or $headSha.StartsWith($latestLocalCommit.Substring(0,7))) {
                if ($status -eq "completed") {
                    if ($conclusion -eq "success") {
                        Write-Host "`n[HARIKA] Derleme basariyla tamamlandi! Yeni IPA indiriliyor..." -ForegroundColor Green
                        Start-Sleep -Seconds 5
                        
                        $releaseUrl = "https://github.com/$repo/releases/download/latest-ios/VK.ipa"
                        $downloaded = $false
                        for ($retry = 0; $retry -lt 10; $retry++) {
                            try {
                                Invoke-WebRequest -Uri $releaseUrl -OutFile $targetFile -UserAgent "Mozilla/5.0"
                                if ((Test-Path $targetFile) -and ((Get-Item $targetFile).Length -gt 1000000)) {
                                    $downloaded = $true
                                    break
                                }
                            } catch { }
                            Write-Host "Release dosyasinin olusmasi bekleniyor... ($($retry + 1)/10)" -ForegroundColor Yellow
                            Start-Sleep -Seconds 6
                        }

                        if ($downloaded) {
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
                            exit 0
                        } else {
                            Write-Host "`n[UYARI] VK.ipa release dosyasina ulasilamadi." -ForegroundColor Red
                            exit 1
                        }
                    } else {
                        Write-Host "`n[UYARI] Derleme sonucu: $conclusion. Link: $runUrl" -ForegroundColor Red
                        exit 1
                    }
                }
            } else {
                Write-Host "[$elapsed sn] Henuz yeni commit GitHub'a ulasmadi veya yeni derleme baslamadi. Bekleniyor..." -ForegroundColor Yellow
            }
        }
    } catch {
        Write-Host "GitHub API kontrol edilirken hata: $($_.Exception.Message)" -ForegroundColor DarkGray
    }

    Start-Sleep -Seconds 15
    $elapsed += 15
}

Write-Host "`nZaman asimi: 10 dakika icinde derleme tamamlanmadi." -ForegroundColor Red
