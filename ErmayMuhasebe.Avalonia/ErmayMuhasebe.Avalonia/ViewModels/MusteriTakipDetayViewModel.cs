using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Repositories;
using ErmayMuhasebe.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ErmayMuhasebe.Avalonia.ViewModels
{
    public partial class MusteriGorselItemViewModel : ObservableObject
    {
        public MusteriTakipDetay Model { get; }
        public int Id => Model.Id;
        public string Baslik => Model.Baslik;
        public string? DosyaYolu => Model.DosyaYolu;
        public DateTime Tarih => Model.Tarih;
        public string TarihFormatli => Tarih.ToString("dd.MM.yyyy HH:mm");

        [ObservableProperty] private Bitmap? _previewBitmap;

        public MusteriGorselItemViewModel(MusteriTakipDetay model)
        {
            Model = model;
            LoadPreview();
        }

        private void LoadPreview()
        {
            try
            {
                if (!string.IsNullOrEmpty(Model.GorselBase64))
                {
                    byte[] bytes = Convert.FromBase64String(Model.GorselBase64);
                    using var ms = new MemoryStream(bytes);
                    PreviewBitmap = new Bitmap(ms);
                }
                else if (!string.IsNullOrEmpty(Model.DosyaYolu) && File.Exists(Model.DosyaYolu))
                {
                    PreviewBitmap = new Bitmap(Model.DosyaYolu);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MusteriGorselItem] LoadPreview error: {ex.Message}");
            }
        }
    }

    public partial class MusteriTakipDetayViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _uow;
        private readonly IPdfService _pdfService;
        private readonly Action _onBack;

        public MusteriTakipKlasor Klasor { get; }

        public string CariUnvan => Klasor.CariUnvan;
        public string? CariKod => Klasor.CariKod;
        public string? Telefon => Klasor.Telefon;
        public string? Yetkili => (!string.IsNullOrWhiteSpace(Klasor.Yetkili) && !string.Equals(Klasor.Yetkili.Trim(), Klasor.CariUnvan?.Trim(), StringComparison.OrdinalIgnoreCase)) ? Klasor.Yetkili : null;

        [ObservableProperty] private string _klasorEtiket = "Sıcak Müşteri";
        [ObservableProperty] private string _klasorRenk = "#3B82F6";
        [ObservableProperty] private string _klasorAciklama = string.Empty;

        // Sekmeler: "Gorseller", "Notlar", "Gorusmeler", "Fiyatlar"
        [ObservableProperty] private string _activeTab = "Gorusmeler";

        // Listeler
        [ObservableProperty] private ObservableCollection<MusteriGorselItemViewModel> _gorseller = new();
        [ObservableProperty] private ObservableCollection<MusteriTakipDetay> _notlar = new();
        [ObservableProperty] private ObservableCollection<MusteriTakipDetay> _gorusmeler = new();
        [ObservableProperty] private ObservableCollection<MusteriTakipDetay> _fiyatlar = new();

        // Not Ekleme Alanları
        [ObservableProperty] private string _newNotBaslik = string.Empty;
        [ObservableProperty] private string _newNotIcerik = string.Empty;
        [ObservableProperty] private DateTime? _newNotTarih = DateTime.Now;

        // Görüşme Ekleme Alanları
        [ObservableProperty] private string _newGorusmeBaslik = string.Empty;
        [ObservableProperty] private string _newGorusmeIcerik = string.Empty;
        [ObservableProperty] private DateTime? _newGorusmeTarih = DateTime.Now;

        // Fiyat Ekleme Alanları
        [ObservableProperty] private string _newFiyatBaslik = string.Empty;
        [ObservableProperty] private decimal? _newFiyatTutar;
        [ObservableProperty] private string _newFiyatParaBirimi = "₺";
        [ObservableProperty] private string _newFiyatAciklama = string.Empty;
        [ObservableProperty] private DateTime? _newFiyatTarih = DateTime.Now;

        public bool HasGorusmeler => Gorusmeler != null && Gorusmeler.Count > 0;
        public bool HasFiyatlar => Fiyatlar != null && Fiyatlar.Count > 0;
        public bool HasGorseller => Gorseller != null && Gorseller.Count > 0;
        public bool HasNotlar => Notlar != null && Notlar.Count > 0;

        // Görsel Ekleme
        [ObservableProperty] private string _newGorselBaslik = string.Empty;

        public Func<Task<string?>>? ImagePickerAction { get; set; }
        public Action<string>? OpenImageExternalAction { get; set; }

        public ObservableCollection<string> ParaBirimleri { get; } = new() { "₺", "$", "€" };
        public ObservableCollection<string> EtiketSecenekleri { get; } = new() { "Sıcak Müşteri", "Teklif Aşamasında", "Önemli", "Yeni İletişim", "Takipte" };

        public MusteriTakipDetayViewModel(IUnitOfWork uow, IPdfService pdfService, MusteriTakipKlasor klasor, Action onBack)
        {
            _uow = uow;
            _pdfService = pdfService;
            Klasor = klasor;
            _onBack = onBack;

            _klasorEtiket = klasor.Etiket ?? "Yeni İletişim";
            _klasorRenk = string.IsNullOrWhiteSpace(klasor.Renk) ? "#3B82F6" : klasor.Renk;
            _klasorAciklama = klasor.Aciklama ?? string.Empty;

            _ = LoadDetaylarAsync();
        }

        public async Task LoadDetaylarAsync()
        {
            try
            {
                var tumDetaylar = await _uow.MusteriTakip.GetDetaylarByKlasorIdAsync(Klasor.Id);

                // Görseller
                var gorselList = tumDetaylar.Where(x => x.Tip == "Gorsel").Select(x => new MusteriGorselItemViewModel(x));
                Gorseller = new ObservableCollection<MusteriGorselItemViewModel>(gorselList);

                // Notlar
                var notList = tumDetaylar.Where(x => x.Tip == "Not");
                Notlar = new ObservableCollection<MusteriTakipDetay>(notList);

                // Görüşmeler
                var gorusmeList = tumDetaylar.Where(x => x.Tip == "Gorusme");
                Gorusmeler = new ObservableCollection<MusteriTakipDetay>(gorusmeList);

                // Fiyatlar
                var fiyatList = tumDetaylar.Where(x => x.Tip == "Fiyat");
                Fiyatlar = new ObservableCollection<MusteriTakipDetay>(fiyatList);

                OnPropertyChanged(nameof(HasGorusmeler));
                OnPropertyChanged(nameof(HasFiyatlar));
                OnPropertyChanged(nameof(HasGorseller));
                OnPropertyChanged(nameof(HasNotlar));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MusteriTakipDetayViewModel] LoadDetaylarAsync Error: {ex.Message}");
            }
        }

        [RelayCommand]
        public void SetTab(string tabName)
        {
            ActiveTab = tabName;
        }

        [RelayCommand]
        public void GoBack()
        {
            _onBack?.Invoke();
        }

        [RelayCommand]
        public async Task SaveKlasorInfoAsync()
        {
            Klasor.Etiket = KlasorEtiket;
            Klasor.Renk = KlasorRenk;
            Klasor.Aciklama = KlasorAciklama;
            await _uow.MusteriTakip.SaveAsync(Klasor);
        }

        // --- GÖRÜŞME İŞLEMLERİ ---
        [RelayCommand]
        public async Task AddGorusmeAsync()
        {
            if (string.IsNullOrWhiteSpace(NewGorusmeBaslik) && string.IsNullOrWhiteSpace(NewGorusmeIcerik)) return;

            var detay = new MusteriTakipDetay
            {
                KlasorId = Klasor.Id,
                CariId = Klasor.CariId,
                Tip = "Gorusme",
                Baslik = string.IsNullOrWhiteSpace(NewGorusmeBaslik) ? "Müşteri Görüşmesi" : NewGorusmeBaslik.Trim(),
                Icerik = NewGorusmeIcerik?.Trim(),
                Tarih = NewGorusmeTarih ?? DateTime.Now
            };

            await _uow.MusteriTakip.SaveDetayAsync(detay);
            NewGorusmeBaslik = string.Empty;
            NewGorusmeIcerik = string.Empty;
            NewGorusmeTarih = DateTime.Now;

            await LoadDetaylarAsync();
        }

        // --- NOT İŞLEMLERİ ---
        [RelayCommand]
        public async Task AddNotAsync()
        {
            if (string.IsNullOrWhiteSpace(NewNotBaslik) && string.IsNullOrWhiteSpace(NewNotIcerik)) return;

            var detay = new MusteriTakipDetay
            {
                KlasorId = Klasor.Id,
                CariId = Klasor.CariId,
                Tip = "Not",
                Baslik = string.IsNullOrWhiteSpace(NewNotBaslik) ? "Genel Not" : NewNotBaslik.Trim(),
                Icerik = NewNotIcerik?.Trim(),
                Tarih = NewNotTarih ?? DateTime.Now
            };

            await _uow.MusteriTakip.SaveDetayAsync(detay);
            NewNotBaslik = string.Empty;
            NewNotIcerik = string.Empty;
            NewNotTarih = DateTime.Now;

            await LoadDetaylarAsync();
        }

        // --- FİYAT İŞLEMLERİ ---
        [RelayCommand]
        public async Task AddFiyatAsync()
        {
            if (string.IsNullOrWhiteSpace(NewFiyatBaslik) && !NewFiyatTutar.HasValue) return;

            var detay = new MusteriTakipDetay
            {
                KlasorId = Klasor.Id,
                CariId = Klasor.CariId,
                Tip = "Fiyat",
                Baslik = string.IsNullOrWhiteSpace(NewFiyatBaslik) ? "Fiyat Teklifi" : NewFiyatBaslik.Trim(),
                FiyatBilgisi = NewFiyatTutar,
                ParaBirimi = NewFiyatParaBirimi ?? "₺",
                Icerik = NewFiyatAciklama?.Trim(),
                Tarih = NewFiyatTarih ?? DateTime.Now
            };

            await _uow.MusteriTakip.SaveDetayAsync(detay);
            NewFiyatBaslik = string.Empty;
            NewFiyatTutar = null;
            NewFiyatAciklama = string.Empty;
            NewFiyatTarih = DateTime.Now;

            await LoadDetaylarAsync();
        }

        // --- GÖRSEL İŞLEMLERİ ---
        [RelayCommand]
        public async Task AddGorselAsync()
        {
            if (ImagePickerAction == null) return;
            var filePath = await ImagePickerAction.Invoke();
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            try
            {
                // Görseli yerel appdata/ErmayMusteri klasörüne kopyala ve base64 oluştur
                string appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMusteriTakip", Klasor.Id.ToString());
                if (!Directory.Exists(appDir)) Directory.CreateDirectory(appDir);

                string destPath = Path.Combine(appDir, $"{Guid.NewGuid():N}_{Path.GetFileName(filePath)}");
                File.Copy(filePath, destPath, true);

                byte[] fileBytes = await File.ReadAllBytesAsync(destPath);
                string base64 = Convert.ToBase64String(fileBytes);

                var detay = new MusteriTakipDetay
                {
                    KlasorId = Klasor.Id,
                    CariId = Klasor.CariId,
                    Tip = "Gorsel",
                    Baslik = string.IsNullOrWhiteSpace(NewGorselBaslik) ? Path.GetFileName(filePath) : NewGorselBaslik.Trim(),
                    DosyaYolu = destPath,
                    GorselBase64 = base64,
                    Tarih = DateTime.Now
                };

                await _uow.MusteriTakip.SaveDetayAsync(detay);
                NewGorselBaslik = string.Empty;
                await LoadDetaylarAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MusteriTakipDetayViewModel] AddGorselAsync Error: {ex.Message}");
            }
        }

        [RelayCommand]
        public void OpenGorsel(MusteriGorselItemViewModel item)
        {
            if (item == null) return;
            if (!string.IsNullOrEmpty(item.DosyaYolu) && File.Exists(item.DosyaYolu))
            {
                OpenImageExternalAction?.Invoke(item.DosyaYolu);
            }
        }

        // --- SİLME İŞLEMİ ---
        [RelayCommand]
        public async Task DeleteDetayAsync(object item)
        {
            int id = 0;
            if (item is MusteriTakipDetay detay) id = detay.Id;
            else if (item is MusteriGorselItemViewModel gorsel) id = gorsel.Id;

            if (id > 0)
            {
                await _uow.MusteriTakip.DeleteDetayAsync(id);
                await LoadDetaylarAsync();
            }
        }

        // --- PDF RAPOR ALMA ---
        [RelayCommand]
        public async Task ExportToPdfAsync()
        {
            try
            {
                var tumDetaylar = await _uow.MusteriTakip.GetDetaylarByKlasorIdAsync(Klasor.Id);
                var res = await _pdfService.GenerateMusteriTakipRaporuPdfAsync(Klasor, tumDetaylar.ToList());
                if (!res.Success && !string.IsNullOrEmpty(res.Error))
                {
                    System.Diagnostics.Debug.WriteLine($"PDF Hatası: {res.Error}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExportToPdfAsync Hatası: {ex.Message}");
            }
        }
    }
}
