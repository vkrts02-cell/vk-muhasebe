using CommunityToolkit.Mvvm.Messaging;
using ErmayMuhasebe.Avalonia.Messages;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;

namespace ErmayMuhasebe.Avalonia.ViewModels;


public partial class FaturaDetayViewModel : ErmayMuhasebe.Shared.ViewModels.FaturaDetayViewModel, IHandleBack
{

    public FaturaDetayViewModel(IUnitOfWork uow, IPdfService pdfService, CariKart? cari, string tur) : base(uow, pdfService)
    {
        _ = InitializeAsync(null, cari?.Id, tur);
    }

    public FaturaDetayViewModel(IUnitOfWork uow, IPdfService pdfService, CariKart? cari, Fatura fatura, List<FaturaDetay> detaylar) : base(uow, pdfService)
    {
        FaturaTuru = fatura.Tur ?? "Satış";
        LoadFromExisting(fatura, detaylar);
        _ = LoadKasalarAndBankalarAsync();
    }

    public bool HandleBack()
    {
        if (IsStokSecimVisible) { IsStokSecimVisible = false; return true; }
        if (IsCariSecimVisible) { IsCariSecimVisible = false; return true; }
        return false;
    }

    [RelayCommand]
    public async Task OpenCariSecim()
    {
        IsCariSecimVisible = true;
        await SearchCariAsync();
    }
    
    [RelayCommand]
    public async Task OpenStokSecim()
    {
        IsStokSecimVisible = true;
        await SearchStokAsync();
    }

    [RelayCommand]
    public void CloseStokSecim() => IsStokSecimVisible = false;

    [RelayCommand]
    public void CloseCariSecim() => IsCariSecimVisible = false;

    protected override void NotifyFinancialDataChanged()
    {
        WeakReferenceMessenger.Default.Send(new FinancialDataChangedMessage());
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
            
            // Add timestamp to filename to avoid locks
            string safeName = System.IO.Path.GetFileNameWithoutExtension(fileName) + "_" + DateTime.Now.ToString("HHmmss") + System.IO.Path.GetExtension(fileName);
            string path = System.IO.Path.Combine(tempDir, safeName);
            
            await System.IO.File.WriteAllBytesAsync(path, content);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"File Open Error: {ex.Message}");
            // Optional: fallback to desktop or show error msg
        }
    }

    [RelayCommand]
    public async Task PrintFaturaAsync()
    {
        try 
        {
            var fatura = new Fatura 
            { 
                Id = FaturaId,
                FaturaNo = FaturaNo, 
                Tarih = GetParsedDate(TarihStr),
                VadeTarihi = GetParsedDate(VadeTarihiStr),
                CariId = Cari?.Id ?? 0,
                CariUnvan = Cari?.Unvan ?? "",
                Tur = FaturaTuru,
                Aciklama = Aciklama,
                AraToplam = AraToplam,
                ToplamKDV = KdvToplam,
                GenelToplam = GenelToplam,
                OdemeSekli = OdemeSekli
            };

            if (Cari != null && Cari.Id > 0)
            {
                var fullCari = await _uow.Cariler.GetByIdAsync(Cari.Id);
                if (fullCari != null)
                {
                    fatura.VergiDairesi = fullCari.VergiDairesi;
                    fatura.VergiNo = fullCari.VergiNo;
                    fatura.Adres = fullCari.Adres;
                }
            }

            var detaylar = Items.Select(i => new FaturaDetay 
            { 
                StokKodu = i.Stok?.StokKodu,
                StokAdi = i.Ad, 
                Miktar = (double)(i.Miktar ?? 0), 
                Birim = i.Birim,
                BirimFiyat = i.BirimFiyat ?? 0,
                KDVOrani = (int)(i.KdvOrani ?? 0),
                KDVTutari = i.KdvTutari,
                ToplamTutar = i.Tutar
            }).ToList();
            
            var pdfBytes = await _pdfService.GenerateFaturaPdfBytesAsync(fatura, detaylar);
            
            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ErmayPrint");
            if (!System.IO.Directory.Exists(tempDir)) System.IO.Directory.CreateDirectory(tempDir);
            string safeNo = string.Join("_", (string.IsNullOrWhiteSpace(FaturaNo) ? "Fatura" : FaturaNo).Split(System.IO.Path.GetInvalidFileNameChars()));
            string path = System.IO.Path.Combine(tempDir, $"Fatura_{safeNo}_{DateTime.Now:HHmmss}.pdf");
            await System.IO.File.WriteAllBytesAsync(path, pdfBytes);
            
            try 
            {
               System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) 
               { 
                   UseShellExecute = true,
                   Verb = "print" 
               }); 
            }
            catch 
            {
               System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PrintFatura Error: {ex.Message}");
        }
    }
}
