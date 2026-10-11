using CommunityToolkit.Mvvm.Input;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using System.Threading.Tasks;
using System;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using ErmayMuhasebe.Avalonia.Messages;
using Avalonia.Threading;

namespace ErmayMuhasebe.Avalonia.ViewModels;

public partial class CekSenetListViewModel : ErmayMuhasebe.Shared.ViewModels.CekSenetListViewModel
{
    public CekSenetListViewModel(IUnitOfWork uow, IPdfService pdfService) : base(uow, pdfService)
    {
        WeakReferenceMessenger.Default.Register<FinancialDataChangedMessage>(this, (r, m) => 
        {
            if (m.Sender == this) return;
            _ = LoadDataAsync();
        });
    }

    protected override void NotifyFinancialDataChanged(Cek? cek)
    {
        WeakReferenceMessenger.Default.Send(new FinancialDataChangedMessage(this));
        if (cek != null) WeakReferenceMessenger.Default.Send(new CekSavedMessage(cek));
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

    [RelayCommand]
    public async Task OpenGorselSecOnAsync(Control? control)
    {
        if (control == null) return;
        var topLevel = TopLevel.GetTopLevel(control);
        if (topLevel == null) return;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Çek Ön Yüz Görseli Seç",
            AllowMultiple = false,
            FileTypeFilter = new[] { global::Avalonia.Platform.Storage.FilePickerFileTypes.ImageAll }
        });
        if (files != null && files.Count > 0) EditGorselYoluOn = files[0].Path.LocalPath;
    }

    [RelayCommand]
    public async Task OpenGorselSecArkaAsync(Control? control)
    {
        if (control == null) return;
        var topLevel = TopLevel.GetTopLevel(control);
        if (topLevel == null) return;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Çek Arka Yüz Görseli Seç",
            AllowMultiple = false,
            FileTypeFilter = new[] { global::Avalonia.Platform.Storage.FilePickerFileTypes.ImageAll }
        });
        if (files != null && files.Count > 0) EditGorselYoluArka = files[0].Path.LocalPath;
    }
}
