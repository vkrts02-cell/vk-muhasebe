$cfg = Get-Content "$env:LOCALAPPDATA\ErmayMuhasebe\ermay_cloud_config.json" | ConvertFrom-Json
$rawUrl = $cfg.BaseUrl.TrimEnd('/')
$apiUrl = if ($rawUrl.EndsWith('/rest/v1')) { $rawUrl } else { "$rawUrl/rest/v1" }
$headers = @{ 'apikey' = $cfg.AuthSecret; 'Authorization' = "Bearer $($cfg.AuthSecret)" }
$tables = @('cariler','stoklar','faturalar','fatura_detaylar','cari_hareketler','kasalar','bankalar')
foreach ($t in $tables) {
    try {
        $res = Invoke-RestMethod -Uri "$apiUrl/$t?select=*" -Headers $headers
        Write-Host "=== Table $t (Count: $($res.Count)) ==="
        if ($res.Count -gt 0) {
            $res | Select-Object -First 3 | Format-Table -AutoSize
        }
    } catch {
        Write-Host "Table $t Error: $($_.Exception.Message)"
    }
}
