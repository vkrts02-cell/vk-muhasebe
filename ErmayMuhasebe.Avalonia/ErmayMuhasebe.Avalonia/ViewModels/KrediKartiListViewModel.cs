using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ErmayMuhasebe.Avalonia.Messages;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using System.Threading.Tasks;
using System;
using Avalonia.Threading;

namespace ErmayMuhasebe.Avalonia.ViewModels;

public partial class KrediKartiListViewModel : ErmayMuhasebe.Shared.ViewModels.KrediKartiListViewModel
{
    public KrediKartiListViewModel(IUnitOfWork uow, IPdfService pdfService) : base(uow, pdfService)
    {
        WeakReferenceMessenger.Default.Register<FinancialDataChangedMessage>(this, (r, m) => 
        {
            if (m.Sender == this) return;
            if (!IsEditDialogVisible && !IsCariSecimVisible) _ = LoadIslemlerAsync();
        });
    }

    protected override async Task InvokeOnUIThreadAsync(Action action)
    {
        await Dispatcher.UIThread.InvokeAsync(action);
    }

    protected override async Task HandleFileOpenAsync(byte[] content, string fileName)
    {
        string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ErmayFiles");
        if (!System.IO.Directory.Exists(tempDir)) System.IO.Directory.CreateDirectory(tempDir);
        string path = System.IO.Path.Combine(tempDir, fileName);
        await System.IO.File.WriteAllBytesAsync(path, content);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }

    protected override void NotifyFinancialDataChanged()
    {
        WeakReferenceMessenger.Default.Send(new FinancialDataChangedMessage(this));
    }
}
