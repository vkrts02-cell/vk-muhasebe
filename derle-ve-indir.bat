@echo off
cd /d "e:\avalonia yedek\ermaymuhasebe"
cls
echo ================================================================
echo      VK MUHASEBE - MOBIL IPA DERLEME VE INDIRME SISTEMI
echo ================================================================
echo.
echo [1/2] Guncel kodlar ve duzeltmeler GitHub a aktariliyor...
echo.

git push origin main
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ----------------------------------------------------------------
    echo [BILGI] GitHub oturumu acilmasi gerekiyor.
    echo Simdi tarayiciniz acilacak ve tek kullanimlik kod panoya kopyalanacak.
    echo Acilan sayfaya yapistirip [Ctrl+V] Authorize butonuna basin!
    echo ----------------------------------------------------------------
    echo.
    gh auth login -p https -h github.com -w -c -s repo,workflow
    gh auth setup-git
    echo.
    echo Tekrar GitHub a yukleniyor...
    git push origin main
)

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ================================================================
    echo [BASARILI] Kodlar basariyla GitHub a yuklendi!
    echo [2/2] GitHub Actions mobil (.ipa) derlemesi baslatildi.
    echo Lutfen bekleyin, derleme bitince dosya Masaustunuze otomatik inecek...
    echo ================================================================
    echo.
    powershell -ExecutionPolicy Bypass -File "e:\avalonia yedek\ermaymuhasebe\wait-and-download-ipa.ps1"
) else (
    echo.
    echo [HATA] GitHub a yukleme yapilamadi.
)

echo.
pause
