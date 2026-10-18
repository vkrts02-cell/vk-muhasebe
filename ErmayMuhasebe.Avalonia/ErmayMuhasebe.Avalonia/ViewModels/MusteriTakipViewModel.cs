using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ErmayMuhasebe.Avalonia.Messages;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Repositories;
using ErmayMuhasebe.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using ErmayMuhasebe.Helpers;
using ErmayMuhasebe.Shared.Helpers;

namespace ErmayMuhasebe.Avalonia.ViewModels
{
    public partial class MusteriTakipKlasorCardViewModel : ObservableObject
    {
        public MusteriTakipKlasor Model { get; }

        public int Id => Model.Id;
        public int CariId => Model.CariId;
        public string CariUnvan => Model.CariUnvan;
        public string? CariKod => Model.CariKod;
        public string? Telefon => Model.Telefon;
        public string? Yetkili => (!string.IsNullOrWhiteSpace(Model.Yetkili) && !string.Equals(Model.Yetkili.Trim(), Model.CariUnvan?.Trim(), StringComparison.OrdinalIgnoreCase)) ? Model.Yetkili : null;
        public string? Etiket => Model.Etiket;
        public string Renk => string.IsNullOrWhiteSpace(Model.Renk) ? "#3B82F6" : Model.Renk;
        public DateTime SonIslemTarihi => Model.SonIslemTarihi;
        public string? Aciklama => Model.Aciklama;

        [ObservableProperty] private int _gorselSayisi;
        [ObservableProperty] private int _notSayisi;
        [ObservableProperty] private int _gorusmeSayisi;
        [ObservableProperty] private int _fiyatSayisi;
        [ObservableProperty] private decimal _sonVerilenFiyat;
        [ObservableProperty] private string _sonVerilenFiyatBirimi = "₺";

        public string SonIslemTarihiFormatli => SonIslemTarihi.ToString("dd.MM.yyyy HH:mm");

        public MusteriTakipKlasorCardViewModel(MusteriTakipKlasor model)
        {
            Model = model;
        }
    }

    public partial class MusteriTakipViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _uow;
        private readonly IPdfService _pdfService;

        [ObservableProperty] private ObservableCollection<MusteriTakipKlasorCardViewModel> _klasorler = new();
        [ObservableProperty] private ObservableCollection<MusteriTakipKlasorCardViewModel> _filteredKlasorler = new();
        [ObservableProperty] private MusteriTakipKlasorCardViewModel? _selectedKlasor;
        [ObservableProperty] private string _searchString = string.Empty;
        [ObservableProperty] private string _selectedEtiketFilter = "Tümü";
        [ObservableProperty] private ObservableCollection<string> _etiketListesi = new() { "Tümü", "Sıcak Müşteri", "Teklif Aşamasında", "Önemli", "Yeni İletişim", "Takipte" };
        [ObservableProperty] private bool _hasKlasorler = false;

        // Cari Seçim Modalı
        [ObservableProperty] private bool _isCariSelectionOpen = false;
        [ObservableProperty] private ObservableCollection<CariKart> _cariler = new();
        [ObservableProperty] private ObservableCollection<CariKart> _filteredCariler = new();
        [ObservableProperty] private string _cariSearchText = string.Empty;
        [ObservableProperty] private CariKart? _selectedCariForAdd;

        // Klasör Detay Ekranı
        [ObservableProperty] private bool _isInFolderDetail = false;
        [ObservableProperty] private MusteriTakipDetayViewModel? _currentFolderDetail;

        // File picker delegate for images
        public Func<Task<string?>>? ImagePickerAction { get; set; }
        public Action<string>? OpenImageExternalAction { get; set; }

        public MusteriTakipViewModel(IUnitOfWork uow, IPdfService pdfService)
        {
            _uow = uow;
            _pdfService = pdfService;
            WeakReferenceMessenger.Default.Register<FinancialDataChangedMessage>(this, (r, m) => 
            {
                if (IsCariSelectionOpen || IsInFolderDetail) return;
                _ = LoadKlasorlerAsync();
            });
            _ = LoadKlasorlerAsync();
        }

        public override void OnNavigatedTo()
        {
            base.OnNavigatedTo();
            _ = LoadKlasorlerAsync();
        }

        partial void OnSearchStringChanged(string value) => FilterKlasorler();
        partial void OnSelectedEtiketFilterChanged(string value) => FilterKlasorler();

        partial void OnCariSearchTextChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                FilteredCariler = new ObservableCollection<CariKart>(Cariler);
            }
            else
            {
                var filtered = Cariler.Where(c => 
                    TurkishSearchHelper.MatchSearch($"{c.Unvan} {c.CariKod} {c.Yetkili} {c.Telefon}", value)
                );
                FilteredCariler = new ObservableCollection<CariKart>(filtered);
            }
        }

        public async Task LoadKlasorlerAsync()
        {
            try
            {
                var list = await _uow.MusteriTakip.GetKlasorlerAsync();
                var cardList = new ObservableCollection<MusteriTakipKlasorCardViewModel>();

                foreach (var item in list)
                {
                    var card = new MusteriTakipKlasorCardViewModel(item);
                    var detaylar = await _uow.MusteriTakip.GetDetaylarByKlasorIdAsync(item.Id);
                    card.GorselSayisi = detaylar.Count(x => x.Tip == "Gorsel");
                    card.NotSayisi = detaylar.Count(x => x.Tip == "Not");
                    card.GorusmeSayisi = detaylar.Count(x => x.Tip == "Gorusme");
                    card.FiyatSayisi = detaylar.Count(x => x.Tip == "Fiyat");
                    
                    var sonFiyat = detaylar.FirstOrDefault(x => x.Tip == "Fiyat" && x.FiyatBilgisi.HasValue);
                    if (sonFiyat != null)
                    {
                        card.SonVerilenFiyat = sonFiyat.FiyatBilgisi ?? 0;
                        card.SonVerilenFiyatBirimi = sonFiyat.ParaBirimi ?? "₺";
                    }

                    cardList.Add(card);
                }

                await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (Klasorler == null) Klasorler = new ObservableCollection<MusteriTakipKlasorCardViewModel>();
                    Klasorler.SyncWith(cardList, c => c.Id, (target, src) =>
                    {
                        target.GorselSayisi = src.GorselSayisi;
                        target.NotSayisi = src.NotSayisi;
                        target.GorusmeSayisi = src.GorusmeSayisi;
                        target.FiyatSayisi = src.FiyatSayisi;
                        target.SonVerilenFiyat = src.SonVerilenFiyat;
                        target.SonVerilenFiyatBirimi = src.SonVerilenFiyatBirimi;
                    });
                    FilterKlasorler();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MusteriTakipViewModel] LoadKlasorlerAsync Error: {ex.Message}");
            }
        }

        private void FilterKlasorler()
        {
            var query = Klasorler.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchString))
            {
                query = query.Where(x => 
                    TurkishSearchHelper.MatchSearch($"{x.CariUnvan} {x.CariKod} {x.Telefon} {x.Yetkili} {x.Aciklama}", SearchString)
                );
            }

            if (!string.IsNullOrEmpty(SelectedEtiketFilter) && SelectedEtiketFilter != "Tümü")
            {
                query = query.Where(x => x.Etiket == SelectedEtiketFilter);
            }

            var filtered = query.ToList();
            if (FilteredKlasorler == null) FilteredKlasorler = new ObservableCollection<MusteriTakipKlasorCardViewModel>();
            FilteredKlasorler.SyncWith(filtered, c => c.Id, (target, src) =>
            {
                target.GorselSayisi = src.GorselSayisi;
                target.NotSayisi = src.NotSayisi;
                target.GorusmeSayisi = src.GorusmeSayisi;
                target.FiyatSayisi = src.FiyatSayisi;
                target.SonVerilenFiyat = src.SonVerilenFiyat;
                target.SonVerilenFiyatBirimi = src.SonVerilenFiyatBirimi;
            });
            HasKlasorler = FilteredKlasorler.Count > 0;
        }

        [RelayCommand]
        public async Task OpenCariSelectionAsync()
        {
            try
            {
                var existingKlasorler = await _uow.MusteriTakip.GetKlasorlerAsync();
                var existingCariIds = existingKlasorler.Select(k => k.CariId).ToHashSet();

                var carilerList = await _uow.Cariler.GetAllAsync();
                var availableCariler = carilerList.Where(c => !existingCariIds.Contains(c.Id)).OrderBy(c => c.Unvan);

                Cariler = new ObservableCollection<CariKart>(availableCariler);
                FilteredCariler = new ObservableCollection<CariKart>(Cariler);
                CariSearchText = string.Empty;
                IsCariSelectionOpen = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MusteriTakipViewModel] OpenCariSelectionAsync Error: {ex.Message}");
            }
        }

        [RelayCommand]
        public void CloseCariSelection()
        {
            IsCariSelectionOpen = false;
            SelectedCariForAdd = null;
        }

        [RelayCommand]
        public async Task SelectCariAndCreateKlasorAsync(CariKart cari)
        {
            if (cari == null) return;

            try
            {
                // Seçilen müşteriyi seçim listesinden kaldır (gizle)
                var itemInAll = Cariler.FirstOrDefault(c => c.Id == cari.Id);
                if (itemInAll != null) Cariler.Remove(itemInAll);

                var itemInFiltered = FilteredCariler.FirstOrDefault(c => c.Id == cari.Id);
                if (itemInFiltered != null) FilteredCariler.Remove(itemInFiltered);

                // Zaten var mı kontrol et
                var existing = await _uow.MusteriTakip.GetByCariIdAsync(cari.Id);
                if (existing != null)
                {
                    CloseCariSelection();
                    // Zaten varsa o klasörü aç
                    OpenKlasorDetail(new MusteriTakipKlasorCardViewModel(existing));
                    return;
                }

                var yeniKlasor = new MusteriTakipKlasor
                {
                    CariId = cari.Id,
                    CariUnvan = cari.Unvan ?? "İsimsiz Müşteri",
                    CariKod = cari.CariKod,
                    Telefon = cari.Telefon,
                    Yetkili = cari.Yetkili,
                    Etiket = "Yeni İletişim",
                    Renk = "#3B82F6",
                    OlusturmaTarihi = DateTime.Now,
                    SonIslemTarihi = DateTime.Now
                };

                await _uow.MusteriTakip.SaveAsync(yeniKlasor);
                CloseCariSelection();
                await LoadKlasorlerAsync();

                var createdCard = Klasorler.FirstOrDefault(k => k.Id == yeniKlasor.Id);
                if (createdCard != null)
                {
                    OpenKlasorDetail(createdCard);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MusteriTakipViewModel] SelectCariAndCreateKlasorAsync Error: {ex.Message}");
            }
        }

        [RelayCommand]
        public void OpenKlasorDetail(MusteriTakipKlasorCardViewModel card)
        {
            if (card == null) return;
            SelectedKlasor = card;

            var detailVm = new MusteriTakipDetayViewModel(_uow, _pdfService, card.Model, OnBackFromDetail);
            detailVm.ImagePickerAction = ImagePickerAction;
            detailVm.OpenImageExternalAction = OpenImageExternalAction;
            detailVm.RequestClose += OnBackFromDetail;

            // Ayrı yeni bir sayfa / pencere olarak aç
            WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(detailVm));
        }

        private async void OnBackFromDetail()
        {
            await LoadKlasorlerAsync();
        }

        [RelayCommand]
        public async Task DeleteKlasorAsync(MusteriTakipKlasorCardViewModel card)
        {
            if (card == null) return;
            try
            {
                await _uow.MusteriTakip.DeleteAsync(card.Id);
                await LoadKlasorlerAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MusteriTakipViewModel] DeleteKlasorAsync Error: {ex.Message}");
            }
        }
    }
}
