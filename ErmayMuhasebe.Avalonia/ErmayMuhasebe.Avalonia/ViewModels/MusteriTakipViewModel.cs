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

    public class MusteriAdayItem
    {
        public int Id { get; set; }
        public string? CariKod { get; set; }
        public string Unvan { get; set; } = string.Empty;
        public string? Yetkili { get; set; }
        public string? Telefon { get; set; }
        public string Kaynak { get; set; } = "Cari Hesap";
        public string KaynakRenk => Kaynak == "Portföy" ? "#10B981" : "#3B82F6";
        public CariKart? OriginalCari { get; set; }
        public PortfoyKart? OriginalPortfoy { get; set; }
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

        // Cari ve Portföy Seçim Modalı
        [ObservableProperty] private bool _isCariSelectionOpen = false;
        [ObservableProperty] private ObservableCollection<MusteriAdayItem> _adaylar = new();
        [ObservableProperty] private ObservableCollection<MusteriAdayItem> _filteredAdaylar = new();
        [ObservableProperty] private ObservableCollection<CariKart> _cariler = new();
        [ObservableProperty] private ObservableCollection<CariKart> _filteredCariler = new();
        [ObservableProperty] private string _cariSearchText = string.Empty;
        [ObservableProperty] private MusteriAdayItem? _selectedAdayForAdd;
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
                FilteredAdaylar = new ObservableCollection<MusteriAdayItem>(Adaylar);
            }
            else
            {
                var filtered = Adaylar.Where(c => 
                    TurkishSearchHelper.MatchSearch($"{c.Unvan} {c.CariKod} {c.Yetkili} {c.Telefon} {c.Kaynak}", value)
                );
                FilteredAdaylar = new ObservableCollection<MusteriAdayItem>(filtered);
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
                var existingUnvanlar = existingKlasorler
                    .Where(k => !string.IsNullOrWhiteSpace(k.CariUnvan))
                    .Select(k => k.CariUnvan.Trim().ToLower(TurkishSearchHelper.Culture))
                    .ToHashSet();

                var adaylarList = new List<MusteriAdayItem>();

                // 1. Cari Hesaplar
                var carilerList = await _uow.Cariler.GetAllAsync();
                foreach (var c in carilerList.Where(c => !existingCariIds.Contains(c.Id) && !c.IsDeleted))
                {
                    adaylarList.Add(new MusteriAdayItem
                    {
                        Id = c.Id,
                        CariKod = c.CariKod,
                        Unvan = c.Unvan ?? "İsimsiz Cari",
                        Yetkili = c.Yetkili,
                        Telefon = !string.IsNullOrWhiteSpace(c.Telefon) ? c.Telefon : c.CepTelefon,
                        Kaynak = "Cari Hesap",
                        OriginalCari = c
                    });
                }

                // 2. Portföy Listesi (Araçlar -> Portföy Kartları)
                try
                {
                    var portfoyList = await _uow.Portfolyo.GetAllAsync();
                    foreach (var p in portfoyList.Where(p => !p.IsDeleted))
                    {
                        string pUnvan = (p.FirmaIsmi ?? "").Trim().ToLower(TurkishSearchHelper.Culture);
                        // Eğer aynı isimde cari zaten takipte değilse ve aday listesinde henüz yoksa ekle
                        if (!existingUnvanlar.Contains(pUnvan) && !adaylarList.Any(a => a.Unvan.Trim().ToLower(TurkishSearchHelper.Culture) == pUnvan))
                        {
                            adaylarList.Add(new MusteriAdayItem
                            {
                                Id = p.Id,
                                CariKod = p.PortfoyNo,
                                Unvan = p.FirmaIsmi ?? "İsimsiz Portföy",
                                Yetkili = p.YetkiliKisi,
                                Telefon = !string.IsNullOrWhiteSpace(p.GSM) ? p.GSM : p.Telefon,
                                Kaynak = "Portföy",
                                OriginalPortfoy = p
                            });
                        }
                    }
                }
                catch (Exception pex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MusteriTakipViewModel] Portfoy load error: {pex.Message}");
                }

                var orderedAdaylar = adaylarList.OrderBy(a => a.Unvan).ToList();
                Adaylar = new ObservableCollection<MusteriAdayItem>(orderedAdaylar);
                FilteredAdaylar = new ObservableCollection<MusteriAdayItem>(Adaylar);
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
            SelectedAdayForAdd = null;
            SelectedCariForAdd = null;
        }

        [RelayCommand]
        public async Task SelectAdayAndCreateKlasorAsync(MusteriAdayItem aday)
        {
            if (aday == null) return;

            try
            {
                // Seçilen adayı modal listesinden kaldır
                var itemInAll = Adaylar.FirstOrDefault(a => a.Id == aday.Id && a.Kaynak == aday.Kaynak);
                if (itemInAll != null) Adaylar.Remove(itemInAll);

                var itemInFiltered = FilteredAdaylar.FirstOrDefault(a => a.Id == aday.Id && a.Kaynak == aday.Kaynak);
                if (itemInFiltered != null) FilteredAdaylar.Remove(itemInFiltered);

                int targetCariId = 0;
                string targetUnvan = aday.Unvan;
                string? targetKod = aday.CariKod;
                string? targetTelefon = aday.Telefon;
                string? targetYetkili = aday.Yetkili;

                if (aday.OriginalPortfoy != null)
                {
                    // Portföy adayı: Cari karta dönüştürme! Cari hesaplar kısmına aktarılmamalıdır.
                    // Yalnızca kullanıcı Portföy ekranında "Cariye Aktar" butonuna tıklarsa cari oluşturulur.
                    var portfoy = aday.OriginalPortfoy;
                    targetCariId = 0;
                    targetUnvan = portfoy.FirmaIsmi ?? aday.Unvan;
                    targetKod = portfoy.PortfoyNo ?? aday.CariKod;
                    targetTelefon = !string.IsNullOrWhiteSpace(portfoy.GSM) ? portfoy.GSM : portfoy.Telefon;
                    targetYetkili = portfoy.YetkiliKisi ?? aday.Yetkili;
                }
                else if (aday.OriginalCari != null)
                {
                    targetCariId = aday.OriginalCari.Id;
                    targetUnvan = aday.OriginalCari.Unvan ?? aday.Unvan;
                    targetKod = aday.OriginalCari.CariKod;
                    targetTelefon = aday.OriginalCari.Telefon ?? aday.OriginalCari.CepTelefon;
                    targetYetkili = aday.OriginalCari.Yetkili;
                }
                else
                {
                    targetCariId = aday.Id;
                }

                // Zaten takip klasörü var mı kontrol et
                MusteriTakipKlasor? existing = null;
                if (targetCariId > 0)
                {
                    existing = await _uow.MusteriTakip.GetByCariIdAsync(targetCariId);
                }
                else
                {
                    var all = await _uow.MusteriTakip.GetKlasorlerAsync();
                    existing = all.FirstOrDefault(k => !string.IsNullOrEmpty(k.CariUnvan) && k.CariUnvan.Trim().Equals(targetUnvan.Trim(), StringComparison.OrdinalIgnoreCase));
                }

                if (existing != null)
                {
                    CloseCariSelection();
                    return;
                }

                var yeniKlasor = new MusteriTakipKlasor
                {
                    CariId = targetCariId,
                    CariUnvan = targetUnvan,
                    CariKod = targetKod,
                    Telefon = targetTelefon,
                    Yetkili = targetYetkili,
                    Etiket = "Yeni İletişim",
                    Renk = "#3B82F6",
                    OlusturmaTarihi = DateTime.Now,
                    SonIslemTarihi = DateTime.Now
                };

                await _uow.MusteriTakip.SaveAsync(yeniKlasor);
                CloseCariSelection();
                await LoadKlasorlerAsync();

                // 2. Sorun Çözümü: Müşteri eklendiğinde detay sayfası otomatik açılmasın!
                // Kullanıcı ana listeden istediği zaman klasörün üzerine tıklayarak açabilir.
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MusteriTakipViewModel] SelectAdayAndCreateKlasorAsync Error: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task SelectCariAndCreateKlasorAsync(CariKart cari)
        {
            if (cari == null) return;
            var aday = new MusteriAdayItem
            {
                Id = cari.Id,
                CariKod = cari.CariKod,
                Unvan = cari.Unvan ?? "İsimsiz Müşteri",
                Yetkili = cari.Yetkili,
                Telefon = cari.Telefon,
                Kaynak = "Cari Hesap",
                OriginalCari = cari
            };
            await SelectAdayAndCreateKlasorAsync(aday);
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
        public void DeleteKlasorConfirm(MusteriTakipKlasorCardViewModel card)
        {
            if (card == null) return;
            ShowConfirm("Müşteri Takip Klasörünü Sil", 
                $"'{card.CariUnvan}' müşterisine ait takip klasörünü ve tüm kayıtlarını silmek istediğinize emin misiniz?", 
                async () =>
                {
                    await DeleteKlasorAsync(card);
                });
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
