# VK Muhasebe - GitHub Actions Takip ve Otomatik Indirme Scripti
$repo = "vkrts02-cell/vk-muhasebe"
$desktopPath = [Environment]::GetFolderPath('Desktop')
$targetFile = Join-Path $desktopPath "VK.ipa"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  VK Muhasebe - Otomatik iOS IPA Derleme Takipcisi" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 1. En son commit'i al
$latestLocalCommit = (git rev-parse HEAD).Trim()
Write-Host "Yerel En Guncel Commit: $latestLocalCommit" -ForegroundColor Yellow

Write-Host "`nGitHub Actions derlemesi bekleniyor..." -ForegroundColor Cyan

$maxWaitSeconds = 600 # 10 dakika
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
                        Invoke-WebRequest -Uri $releaseUrl -OutFile $targetFile -UserAgent "Mozilla/5.0"
                        
                        if (Test-Path $targetFile) {
                            $sizeMB = [math]::Round((Get-Item $targetFile).Length / 1MB, 2)
                            Write-Host "`n[TAMAMLANDI] Yeni VK.ipa Masaustune kaydedildi! ($sizeMB MB)" -ForegroundColor Green
                            explorer.exe /select,"$targetFile"
                            exit 0
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
