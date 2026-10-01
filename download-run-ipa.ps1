param(
    [string]$RunId = "36706639126",
    [string]$Repo = "vkrts02-cell/vk-muhasebe"
)

$tempDir = Join-Path $env:TEMP "vk_run_artifact_$RunId"
if (Test-Path $tempDir) { Remove-Item -Recurse -Force $tempDir }
New-Item -ItemType Directory -Path $tempDir | Out-Null

Write-Host "Run $RunId artifact'ı indiriliyor..." -ForegroundColor Cyan
gh run download $RunId -R $Repo -D $tempDir

$ipa = Get-ChildItem -Path $tempDir -Filter "*.ipa" -Recurse | Select-Object -First 1
if ($ipa) {
    $sizeMB = [math]::Round($ipa.Length / 1MB, 2)
    Write-Host "İndirilen IPA: $($ipa.FullName) ($sizeMB MB)" -ForegroundColor Green

    $targets = @(
        "C:\Users\mazik\Desktop\VK.ipa",
        "C:\Users\mazik\OneDrive\Masaüstü\VK.ipa"
    )
    foreach ($t in $targets) {
        $parent = Split-Path $t -Parent
        if (Test-Path $parent) {
            Copy-Item -Path $ipa.FullName -Destination $t -Force
            Write-Host "[BAŞARILI] Güncel VK.ipa kaydedildi: $t" -ForegroundColor Green
        }
    }
} else {
    Write-Error "Artifact içinde .ipa dosyası bulunamadı!"
}
