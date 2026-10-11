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
$startTimeUtc = (Get-Date).ToUniversalTime().AddMinutes(-2)

$targetCommits = @($latestLocalCommit)

while ($elapsed -lt $maxWaitSeconds) {
    try {
        $runs = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/actions/runs?per_page=10" -UserAgent "Mozilla/5.0"
        if ($runs.workflow_runs.Count -gt 0) {
            # Target commit'i iceren veya son tetiklenen iOS kosusunu bul
            $matchedRuns = $runs.workflow_runs | Where-Object { 
                $sha = $_.head_sha
                ($targetCommits | Where-Object { $sha.StartsWith($_.Substring(0,7)) }).Count -gt 0
            }
            
            # Eger commit tam eslesmiyorsa ama son baslatilan iOS kosusu varsa onu da degerlendir
            if (-not $matchedRuns -or $matchedRuns.Count -eq 0) {
                $matchedRuns = $runs.workflow_runs | Where-Object {
                    $_.name -like "*iOS*" -and ([DateTime]$_.created_at -gt $startTimeUtc)
                }
            }

            # Eger tamamlanmis basarili bir run varsa onu sec, yoksa en son run'i sec
            $matchedRun = ($matchedRuns | Where-Object { $_.status -eq "completed" -and $_.conclusion -eq "success" } | Select-Object -First 1)
            if (-not $matchedRun) {
                $matchedRun = ($matchedRuns | Select-Object -First 1)
            }

            if ($matchedRun) {
                $status = $matchedRun.status
                $conclusion = $matchedRun.conclusion
                $headSha = $matchedRun.head_sha
                $runUrl = $matchedRun.html_url
                $runId = $matchedRun.id

                Write-Host "[$elapsed sn] Run #$($matchedRun.run_number) ($runId) - Durum: $status | Sonuc: $conclusion | Commit: $($headSha.Substring(0,7))" -ForegroundColor Gray

                if ($status -eq "completed") {
                    if ($conclusion -eq "success") {
                        Write-Host "`n[HARIKA] Derleme basariyla tamamlandi! Yeni IPA indiriliyor..." -ForegroundColor Green
                        Start-Sleep -Seconds 3
                        
                        $downloaded = $false
                        for ($retry = 0; $retry -lt 15; $retry++) {
                            try {
                                # Release asset guncellenme zamanini kontrol et
                                $rel = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/tags/latest-ios" -UserAgent "Mozilla/5.0"
                                $asset = $rel.assets | Where-Object { $_.name -eq "VK.ipa" } | Select-Object -First 1
                                if ($asset -and ([DateTime]$asset.updated_at -gt $startTimeUtc)) {
                                    Write-Host "En guncel asset bulundu ($($asset.updated_at)), indiriliyor..." -ForegroundColor Cyan
                                    $releaseUrl = if ($asset.browser_download_url) { $asset.browser_download_url } else { "https://github.com/$repo/releases/download/latest-ios/VK.ipa" }
                                    Invoke-WebRequest -Uri $releaseUrl -OutFile $targetFile -UserAgent "Mozilla/5.0"
                                    if ((Test-Path $targetFile) -and ((Get-Item $targetFile).Length -gt 1000000)) {
                                        $downloaded = $true
                                        break
                                    }
                                }
                            } catch { }
                            Write-Host "Release dosyasinin guncellenmesi bekleniyor... ($($retry + 1)/15)" -ForegroundColor Yellow
                            Start-Sleep -Seconds 5
                        }

                        if (-not $downloaded) {
                            Write-Host "GitHub Releases dogrudan URL deneniyor..." -ForegroundColor Cyan
                            try {
                                $directUrl = "https://github.com/$repo/releases/download/latest-ios/VK.ipa"
                                Invoke-WebRequest -Uri $directUrl -OutFile $targetFile -UserAgent "Mozilla/5.0"
                                if ((Test-Path $targetFile) -and ((Get-Item $targetFile).Length -gt 1000000)) {
                                    $downloaded = $true
                                }
                            } catch { }
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
                        Write-Host "`n[UYARI] Derleme basarisiz oldu: $conclusion. Link: $runUrl" -ForegroundColor Red
                        exit 1
                    }
                }
            } else {
                Write-Host "[$elapsed sn] Henuz eslesen derleme bulunamadi. GitHub Actions baslatilmasi bekleniyor..." -ForegroundColor Yellow
            }
        }
    } catch {
        Write-Host "GitHub API kontrol edilirken hata: $($_.Exception.Message)" -ForegroundColor DarkGray
    }

    Start-Sleep -Seconds 20
    $elapsed += 20
}

Write-Host "`nZaman asimi: 30 dakika icinde derleme tamamlanmadi." -ForegroundColor Red
exit 1
