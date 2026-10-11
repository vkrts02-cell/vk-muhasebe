using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ErmayMuhasebe.Avalonia.Messages;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace ErmayMuhasebe.Avalonia.ViewModels;

public partial class FaturaListViewModel : ErmayMuhasebe.Shared.ViewModels.FaturaListViewModel
{
    private readonly IFileService _fileService;

    public FaturaListViewModel(IUnitOfWork uow, IExcelService excelService, IPdfService pdfService, IFileService fileService) 
        : base(uow, excelService, pdfService)
    {
        _fileService = fileService;
        // Listen for financial changes
        WeakReferenceMessenger.Default.Register<FinancialDataChangedMessage>(this, (r, m) => 
        {
            if (m.Sender == this) return;
            _ = LoadFaturalarAsync();
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
        if (content == null || content.Length == 0) return;
        
        try
        {
            await _fileService.SaveAndOpenFileAsync(fileName, content, "application/pdf");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Dosya açılamadı: {ex.Message}";
        }
    }

    public override async Task ViewFaturaPdfAsync(Fatura? fatura)
    {
        if (fatura == null) return;
        ErrorMessage = null;
        IsLoading = true;
        try
        {
            await base.ViewFaturaPdfAsync(fatura);
        }
        finally
        {
            IsLoading = false;
        }
    }


    public override async Task EditFaturaAsync(Fatura? fatura)
    {
        if (fatura == null) return;
        
        var vm = new FaturaDetayViewModel(_uow, _pdfService, null, fatura.Tur ?? "Satış");
        var detaylar = await _uow.Faturalar.GetDetaylarAsync(fatura.Id);
        vm.LoadFromExisting(fatura, detaylar);
        
        vm.RequestClose += () => {
             _ = LoadFaturalarAsync();
             WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(this));
        };
        WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(vm));
    }


    public override void OpenCreateInvoice(string type)
    {
        var tur = type == "Satis" ? "Satış" : (type == "Alis" ? "Alış" : type);
        var vm = new FaturaDetayViewModel(_uow, _pdfService, null, tur);
        vm.RequestClose += () => {
             _ = LoadFaturalarAsync();
             WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(this));
        };
        WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(vm));
    }
}
