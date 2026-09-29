$file = 'E:\avalonia yedek\ermaymuhasebe\ErmayMuhasebe.Avalonia\ErmayMuhasebe.Avalonia\ViewModels\LoginViewModel.cs'
$content = [IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)

$startIdx = $content.IndexOf('private async Task SendEmailAsync(string toEmail, string subject, string body)')
if ($startIdx -lt 0) { Write-Host 'METHOD NOT FOUND'; exit 1 }

$endIdx = $content.IndexOf('[RelayCommand]', $startIdx)
if ($endIdx -lt 0) { Write-Host 'END NOT FOUND'; exit 1 }

$searchLength = $endIdx - $startIdx
$methodEndRel = $content.Substring($startIdx, $searchLength).LastIndexOf('}')
if ($methodEndRel -lt 0) { Write-Host 'BRACE NOT FOUND'; exit 1 }

$methodEnd = $startIdx + $methodEndRel + 1

$oldMethod = $content.Substring($startIdx, $methodEnd - $startIdx)
Write-Host "Found method length: $($oldMethod.Length)"

$newMethod = [IO.File]::ReadAllText('E:\avalonia yedek\ermaymuhasebe\email_fix.cs', [System.Text.Encoding]::UTF8)

$newContent = $content.Substring(0, $startIdx) + $newMethod + $content.Substring($methodEnd)
[IO.File]::WriteAllText($file, $newContent, [System.Text.Encoding]::UTF8)
Write-Host 'SUCCESS: Method replaced'