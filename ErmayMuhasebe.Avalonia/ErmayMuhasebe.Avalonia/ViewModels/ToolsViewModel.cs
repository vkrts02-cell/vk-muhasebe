using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using SVM = ErmayMuhasebe.Shared.ViewModels;
using AVM = ErmayMuhasebe.Avalonia.ViewModels;

namespace ErmayMuhasebe.Avalonia.ViewModels;

public partial class ToolItem : ObservableObject
{
    [ObservableProperty] private string _title;
    [ObservableProperty] private string _description;
    [ObservableProperty] private string _icon;
    [ObservableProperty] private string _color;
    [ObservableProperty] private Type _viewModelType;
    [ObservableProperty] private bool _isImplemented;
    [ObservableProperty] private string _statusText;

    public ToolItem(string title, string description, string icon, string color, Type viewModelType, bool implemented = true)
    {
        _title = title;
        _description = description;
        _icon = icon;
        _color = color;
        _viewModelType = viewModelType;
        _isImplemented = implemented;
        _statusText = implemented ? "AKTİF" : "YENİ";
    }
}

public partial class ToolsViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    [ObservableProperty] private ObservableCollection<ToolItem> _tools = new();

    public ToolsViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        InitializeTools();
    }

    private void InitializeTools()
    {
        var list = new List<ToolItem>
        {
            new("Sistem Sağlığı ve Bakımı", "Sistem durumu izleme, temizlik ve yedekleme.", "Activity", "#10B981", typeof(AVM.SistemSaglikViewModel)),
            new("Belge Arşivleme", "Dijital evrak saklama.", "Folder", "#8B5CF6", typeof(AVM.BelgeArsivViewModel)),
            new("Döviz Kurları & Otomasyon", "Canlı kur ekranı ve otomatik çekme ayarları.", "CurrencyDollar", "#F59E0B", typeof(SVM.DovizOtomasyonViewModel)),
            new("Cari Birleştirme", "Mükerrer cari kartları birleştir.", "People", "#3B82F6", typeof(AVM.CariBirlestirmeViewModel)),
            new("Ürün Birleştirme", "Mükerrer kartları birleştir.", "Table", "#EC4899", typeof(AVM.UrunBirlestirmeViewModel)),
            new("Gecikme Faizi", "Faiz hesaplama aracı.", "Receipt", "#EF4444", typeof(AVM.GecikmeFaiziViewModel)),
            new("Risk Puanlayıcı", "Ödeme analizi paneli.", "Pulse", "#F59E0B", typeof(SVM.CekRiskAnalizViewModel)),
            new("Toplu Fiyat Güncelleme", "Tüm ürünlere toplu zam/indirim.", "Money", "#10B981", typeof(AVM.TopluFiyatViewModel)),
            new("Limit Yönetimi", "Risk ve kredi limitleri.", "LockShield", "#EF4444", typeof(SVM.MusteriLimitViewModel)),
            new("Bütçe Planlama Merkezi", "Yıllık, aylık ve haftalık hedef yönetimi.", "DataArea", "#60A5FA", typeof(SVM.ButcePlanlamaViewModel)),
            new("Portföy Listesi", "Varlık yönetimi paneli.", "Briefcase", "#8B5CF6", typeof(SVM.PortfoyListViewModel))
        };


        Tools = new ObservableCollection<ToolItem>(list);
    }

    [RelayCommand]
    public void OpenTool(ToolItem tool)
    {
        var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
        mainVm.NavigateTo(tool.ViewModelType);
    }
}
