# VK Muhasebe - Otomatik iOS IPA Indirme Scripti
$ErrorActionPreference = "Stop"

$repo = "vkrts02-cell/vk-muhasebe"
$desktopPaths = @(
    "C:\Users\mazik\Desktop\VK.ipa",
    "C:\Users\mazik\OneDrive\Masaüstü\VK.ipa"
)
$targetFile = $desktopPaths[0]
$releaseUrl = "https://github.com/$repo/releases/download/latest-ios/VK.ipa"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  VK Muhasebe - iOS IPA Indirme Araci" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "Hedef Konumlar: $($desktopPaths -join ', ')" -ForegroundColor Yellow

# 1. Once Releases uzerinden dogrudan indirmeyi dene
Write-Host "`n[1/3] En guncel Release surumu kontrol ediliyor..." -ForegroundColor Cyan
try {
    Write-Host "Releases uzerinden indiriliyor ($releaseUrl)..." -ForegroundColor Gray
    $tempFile = Join-Path $env:TEMP "VK_download.ipa"
    Invoke-WebRequest -Uri $releaseUrl -OutFile $tempFile -UserAgent "Mozilla/5.0"
    if ((Test-Path $tempFile) -and ((Get-Item $tempFile).Length -gt 1000000)) {
        $sizeMB = [math]::Round((Get-Item $tempFile).Length / 1MB, 2)
        foreach ($dp in $desktopPaths) {
            $p = Split-Path $dp -Parent
            if (Test-Path $p) {
                Copy-Item -Path $tempFile -Destination $dp -Force
                Write-Host "`n[BASARILI] VK.ipa basariyla Masaustune kopyalandi: $dp ($sizeMB MB)" -ForegroundColor Green
            }
        }
        exit 0
    }
} catch {
    Write-Host "Release henuz hazir degil veya henuz yayinlanmadi. Actions Artifact kontrol ediliyor..." -ForegroundColor Yellow
}

# 2. Eger gh CLI giris yapildiysa gh run download dene
Write-Host "`n[2/3] GitHub CLI (gh) kontrol ediliyor..." -ForegroundColor Cyan
$ghInstalled = Get-Command gh -ErrorAction SilentlyContinue
if ($ghInstalled) {
    try {
        $authCheck = gh auth status 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "GitHub CLI oturumu aktif. Son artifact indiriliyor..." -ForegroundColor Green
            $tempZipDir = Join-Path $env:TEMP "vk_ipa_artifact"
            if (Test-Path $tempZipDir) { Remove-Item -Recurse -Force $tempZipDir }
            New-Item -ItemType Directory -Path $tempZipDir | Out-Null
            
            gh run download -n VK-iOS-IPA --dir $tempZipDir --repo $repo
            $downloadedIpa = Get-ChildItem -Path $tempZipDir -Filter "*.ipa" -Recurse | Select-Object -First 1
            if ($downloadedIpa) {
                Copy-Item -Path $downloadedIpa.FullName -Destination $targetFile -Force
                $sizeMB = [math]::Round((Get-Item $targetFile).Length / 1MB, 2)
                Write-Host "`n[BASARILI] VK.ipa Masaustune aktarildi!" -ForegroundColor Green
                Write-Host "Dosya Boyutu: $sizeMB MB" -ForegroundColor Green
                Write-Host "Konum: $targetFile" -ForegroundColor Green
                exit 0
            }
        }
    } catch {
        Write-Host "gh CLI ile indirme basarisiz oldu." -ForegroundColor Gray
    }
}

# 3. Bilgi ve Yonlendirme
Write-Host "`n[3/3] GitHub Actions Durumu:" -ForegroundColor Cyan
try {
    $runs = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/actions/runs?per_page=3" -UserAgent "Mozilla/5.0"
    foreach ($run in $runs.workflow_runs) {
        $statusText = if ($run.conclusion) { $run.conclusion } else { $run.status }
        Write-Host " - [$statusText] $($run.name) - $($run.created_at)" -ForegroundColor Gray
        Write-Host "   Link: $($run.html_url)" -ForegroundColor Gray
    }
} catch {
    Write-Host "GitHub API sorgulanamadi." -ForegroundColor Gray
}

Write-Host "`nEger derleme henuz tamamlanmadiysa GitHub Actions uzerinden su linki takip edebilirsiniz:" -ForegroundColor Yellow
Write-Host "https://github.com/$repo/actions" -ForegroundColor White
Write-Host "Derleme bittiginde bu scripti tekrar calistirabilir veya tarayicinizdan indirebilirsiniz.`n" -ForegroundColor Yellow
