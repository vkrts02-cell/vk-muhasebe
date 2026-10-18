using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ErmayMuhasebe.Avalonia.Messages;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace ErmayMuhasebe.Avalonia.ViewModels;

public partial class CariListViewModel : ErmayMuhasebe.Shared.ViewModels.CariListViewModel, IHandleBack
{
    public CariListViewModel(IUnitOfWork uow, IPdfService pdfService, IExcelService excelService, ExternalApiService externalApi, IFinansService finansService) 
        : base(uow, pdfService, excelService, new CekSenetListViewModel(uow, pdfService), externalApi, finansService)
    {
        WeakReferenceMessenger.Default.Register<CekSavedMessage>(this, async (r, m) => 
        {
            await LoadCarilerAsync();
            if (SelectedCari != null) await LoadHareketlerAsync(SelectedCari.Id);
        });

        WeakReferenceMessenger.Default.Register<FinancialDataChangedMessage>(this, async (r, m) => 
        {
            if (m.Sender == this) return;
            if (IsTransactionDialogVisible || IsCariEkleVisible || IsEkstreOptionVisible) return;
            System.Diagnostics.Debug.WriteLine("[CariListViewModel] FinancialDataChangedMessage received. Reloading cariler silently...");
            await LoadCarilerAsync(isSilent: true);
            if (SelectedCari != null) await LoadHareketlerAsync(SelectedCari.Id);
        });
    }

    protected override void NotifyFinancialDataChanged()
    {
        WeakReferenceMessenger.Default.Send(new FinancialDataChangedMessage(this));
    }

    protected override async Task InvokeOnUIThreadAsync(Action action)
    {
        await Dispatcher.UIThread.InvokeAsync(action);
    }

    protected override async Task HandleFileOpenAsync(byte[] content, string fileName)
    {
        try 
        {
            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ErmayFiles");
            if (!System.IO.Directory.Exists(tempDir)) System.IO.Directory.CreateDirectory(tempDir);
            
            // Add timestamp to prevent locking
            string safeName = System.IO.Path.GetFileNameWithoutExtension(fileName) + "_" + DateTime.Now.ToString("HHmmss") + System.IO.Path.GetExtension(fileName);
            string path = System.IO.Path.Combine(tempDir, safeName);
            
            await System.IO.File.WriteAllBytesAsync(path, content);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"File Open Error: {ex.Message}");
        }
    }


    public override async Task DownloadEkstreAsync(object? root)
    {
        if (_pendingPdfBytes == null || SelectedCari == null) return;
        IsEkstreOptionVisible = false;
        if (root is not global::Avalonia.Controls.Control control) return;
        var topLevel = global::Avalonia.Controls.TopLevel.GetTopLevel(control);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Hesap Ekstresini Kaydet",
            DefaultExtension = "pdf",
            SuggestedFileName = $"Ekstre_{SelectedCari.Unvan}_{DateTime.Now:ddMMyyyy}.pdf",
            FileTypeChoices = new[] { global::Avalonia.Platform.Storage.FilePickerFileTypes.Pdf }
        });

        if (file != null)
        {
            await using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(_pendingPdfBytes);
        }
    }


    public override void OpenSatisFaturasi() => OpenFaturaEkrani("Satış");


    public override void OpenAlisFaturasi() => OpenFaturaEkrani("Alış");

    private void OpenFaturaEkrani(string tur)
    {
        if (SelectedCari == null) return;
        var vm = new FaturaDetayViewModel(_uow, _pdfService, SelectedCari, tur);
        vm.RequestClose += () => 
        {
            WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(this));
             _ = LoadCarilerAsync();
        };
        WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(vm));
    }


    public override async Task EditTransactionAsync()
    {
        if (SelectedHareket == null || SelectedCari == null) return;

        // 1. Faturalar (Alış, Satış, Kapalı, Açık Fatura)
        var fatura = await FindLinkedFaturaAsync(SelectedHareket);
        if (fatura != null)
        {
            var detaylar = await _uow.Faturalar.GetDetaylarAsync(fatura.Id);
            var vm = new FaturaDetayViewModel(_uow, _pdfService, SelectedCari, fatura, detaylar);
            vm.RequestClose += () => 
            {
                WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(this));
                _ = LoadCarilerAsync();
                if (SelectedCari != null) _ = LoadHareketlerAsync(SelectedCari.Id);
            };
            WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(vm));
            return;
        }

        // 2. Finansal İşlemler (Tahsilat, Ödeme, Borç Dekontu / Borçlandır, Alacak Dekontu / Alacaklandır)
        _editingHareket = SelectedHareket;
        string rawTur = SelectedHareket.IslemTuru ?? "";
        if (rawTur.Contains("Tahsilat", StringComparison.OrdinalIgnoreCase))
            TransactionType = "Tahsilat";
        else if (rawTur.Contains("Ödeme", StringComparison.OrdinalIgnoreCase) || rawTur.Contains("Odeme", StringComparison.OrdinalIgnoreCase))
            TransactionType = "Ödeme";
        else if (rawTur.Contains("Borç", StringComparison.OrdinalIgnoreCase) || rawTur.Contains("Borc", StringComparison.OrdinalIgnoreCase))
            TransactionType = "Borç Dekontu";
        else if (rawTur.Contains("Alacak", StringComparison.OrdinalIgnoreCase))
            TransactionType = "Alacak Dekontu";
        else
            TransactionType = SelectedHareket.Borc > 0 ? "Borç Dekontu" : "Tahsilat";

        TransactionTitle = $"{TransactionType} Düzenle - {SelectedCari.Unvan}";
        TransactionAmount = SelectedHareket.Borc > 0 ? SelectedHareket.Borc : SelectedHareket.Alacak;
        
        string desc = SelectedHareket.Aciklama ?? "";
        if (desc.StartsWith("[") && desc.Contains("]"))
        {
            int endIdx = desc.IndexOf("]");
            TransactionMethod = desc.Substring(1, endIdx - 1);
            TransactionDescription = desc.Substring(endIdx + 1).Trim();
        }
        else
        {
            TransactionMethod = (TransactionType == "Borç Dekontu" || TransactionType == "Alacak Dekontu") ? "Dekont" : "Nakit";
            TransactionDescription = desc;
        }
        bool isAcilis = (SelectedHareket.IslemTuru?.Contains("Açılış", StringComparison.OrdinalIgnoreCase) == true) || 
                        (SelectedHareket.Aciklama?.Contains("Açılış", StringComparison.OrdinalIgnoreCase) == true);
        IsAcilisFisi = isAcilis;

        if (SelectedHareket.YonlendirilenCariId.HasValue)
        {
            KkYonlendirilenTedarikciId = SelectedHareket.YonlendirilenCariId;
            KkYonlendirilenTedarikci = SelectedHareket.YonlendirilenCariUnvan ?? "";
        }
        else
        {
            KkYonlendirilenTedarikciId = null;
            KkYonlendirilenTedarikci = "";
        }

        TransactionDate = SelectedHareket.Tarih;
        TransactionDateStr = SelectedHareket.Tarih.ToString("dd.MM.yyyy");
        await LoadAvailableKasalarAsync(TransactionMethod);

        // Bağlı Kasa veya Banka hesabını tespit et ve seç
        if (!string.IsNullOrEmpty(SelectedHareket.RefId) || !string.IsNullOrEmpty(SelectedHareket.EvrakNo))
        {
            try
            {
                var refId = SelectedHareket.RefId ?? "";
                var evrakNo = SelectedHareket.EvrakNo ?? "";
                var allKh = await _uow.Kasalar.GetAllAsync();
                var kh = allKh.FirstOrDefault(k => (!string.IsNullOrEmpty(refId) && k.RefId == refId) || (!string.IsNullOrEmpty(evrakNo) && k.EvrakNo == evrakNo));
                if (kh != null)
                {
                    var matchedKasa = AvailableKasalar.FirstOrDefault(k => k.Id == kh.KasaId);
                    if (matchedKasa != null)
                    {
                        SelectedKasaForTransaction = matchedKasa;
                        SelectedKasaId = matchedKasa.Id;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EditTransactionAsync] Kasa eşleştirme: {ex.Message}");
            }
        }

        IsTransactionDialogVisible = true;
    }

    [RelayCommand]
    public void OpenBelgeArsiv()
    {
        if (SelectedCari == null) return;
        var vm = new BelgeArsivViewModel();
        vm.SetCategory($"CARI_{SelectedCari.Id}");
        WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(vm));
    }

    [RelayCommand]
    public async Task OpenMusteriTakipKlasorAsync()
    {
        if (SelectedCari == null) return;
        var musteriTakipVm = new MusteriTakipViewModel(_uow, _pdfService);
        await musteriTakipVm.SelectCariAndCreateKlasorAsync(SelectedCari);
        WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(musteriTakipVm));
    }

    public override Task HesapEkstresiAsync(CariKart? cari = null) => base.HesapEkstresiAsync(cari);

    public override Task DetayliHesapEkstresiAsync(CariKart? cari = null) => base.DetayliHesapEkstresiAsync(cari);


    [RelayCommand]
    public async Task OpenSlipSecAsync(object? root)
    {
        if (root is not global::Avalonia.Controls.Control control) return;
        var topLevel = global::Avalonia.Controls.TopLevel.GetTopLevel(control);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = TransactionMethod == "Kredi Kartı" ? "Slip Görseli Seç" : "Dekont Görseli Seç",
            AllowMultiple = false,
            FileTypeFilter = new[] { global::Avalonia.Platform.Storage.FilePickerFileTypes.ImageAll, global::Avalonia.Platform.Storage.FilePickerFileTypes.Pdf }
        });

        if (files != null && files.Count > 0)
        {
            KkSlipPath = files[0].Path.LocalPath;
        }
    }

    public bool HandleBack()
    {
        if (IsEkstreOptionVisible) { IsEkstreOptionVisible = false; return true; }
        if (IsCariEkleVisible) { CloseDialog(); return true; }
        if (IsTransactionDialogVisible) { CloseTransactionDialog(); return true; }
        if (IsCariSelected) { SelectedCari = null; return true; }
        return false;
    }
}
