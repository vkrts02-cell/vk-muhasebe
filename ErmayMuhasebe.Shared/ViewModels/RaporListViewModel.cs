using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ErmayMuhasebe.Shared.ViewModels;

public abstract partial class RaporListViewModel : ViewModelBase
{
    protected readonly IUnitOfWork _uow;
    protected readonly IPdfService _pdfService;

    [ObservableProperty]
    private ObservableCollection<ReportItemViewModel> _reports = new();

    [ObservableProperty]
    private string _searchString = "";

    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private bool _isCariSelectionVisible;
    [ObservableProperty] private string _cariSearchTerm = "";
    [ObservableProperty] private ObservableCollection<CariKart> _cariSearchResults = new();

    [ObservableProperty] private bool _isDateSelectionVisible;
    [ObservableProperty] private DateTime? _startDate = new DateTime(DateTime.Now.Year, 1, 1);
    [ObservableProperty] private DateTime? _endDate = new DateTime(DateTime.Now.Year, 12, 31);

    // Error Handling
    [ObservableProperty] private bool _isErrorVisible;

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(ErrorMessage))
        {
            IsErrorVisible = !string.IsNullOrEmpty(ErrorMessage);
        }
    }

    [RelayCommand]
    public void ClearError() => ErrorMessage = "";
    
    [RelayCommand]
    public async Task SelectDateRangeAsync()
    {
        if (_pendingReportItem == null) return;
        IsDateSelectionVisible = false;
        await GenerateReportInternal(_pendingReportItem, null, _isPendingPrint);
        _pendingReportItem = null;
    }

    [RelayCommand]
    public void CloseDateSelection()
    {
        IsDateSelectionVisible = false;
        _pendingReportItem = null;
    }
    
    protected ReportItemViewModel? _pendingReportItem;

    public RaporListViewModel(IUnitOfWork uow, IPdfService pdfService)
    {
        _uow = uow;
        _pdfService = pdfService;
        InitializeReports();
    }

    protected virtual List<ReportItemViewModel> GetAllReports()
    {
        return new List<ReportItemViewModel>
        {
            new("Genel Özet", "Genel Durum", "Apps", "İşletmenin genel mali özetini gösterir."),
            new("Aylık Tahsilat ve Ödeme Analizi", "Finans", "DataTrending", "Ay bazlı ödeme türlerine göre tahsilat ve yönlendirme özeti."),
            new("Kredi Kartı Detay Raporu", "Finans", "Payment", "Kaydedilen ve yönlendirilen kredi kartı işlem detayları."),
            new("Nakit İşlem Detay Raporu", "Finans", "Money", "Tüm nakit kasa hareketlerinin detaylı dökümü."),
            new("Çek Detay Raporu", "Finans", "MoneyHand", "Portföydeki ve işlem görmüş çeklerin detayları."),
            new("Havale / EFT Detay Raporu", "Finans", "BuildingBank", "Banka havale ve EFT işlemlerinin detaylı listesi."),
            new("Cari Bakiye Raporu", "Cari Hesap", "People", "Müşteri ve tedarikçi bakiyeleri."),
            new("Cari Hareket Dökümü", "Cari Hesap", "History", "Cari hesapların işlem detayları."),
            new("Yaşlandırma Raporu", "Cari Hesap", "Timer", "Borç/alacak yaşlandırma analizi."),
            new("Hareketsiz Cariler", "Cari Hesap", "PersonOff", "İşlem görmeyen cari hesaplar."),
            new("Stok Mevcudu", "Stok", "Box", "Güncel stok miktarları ve değerleri."),
            new("Stok Hareketleri", "Stok", "ArrowSwap", "Giriş-çıkış stok hareket dökümü."),
            new("Kritik Stok Seviyesi", "Stok", "Warning", "Minimum seviyenin altındaki ürünler."),
            new("Ölü Stok Raporu", "Stok", "BoxDismiss", "Çakışan stok kartlarının listesi (Birleştirilmesi Gerekenler)."),
            new("Stok Devir Hızı", "Stok", "ArrowTrendingLines", "Stokların dönüşüm hızı analizi."),
            new("Satış Faturası Dökümü", "Satış", "Receipt", "Kesilen satış faturalarının listesi."),
            new("Ürün Karlılık Raporu", "Karlılık", "Tag", "Ürün bazlı kar/zarar analizi."),
            new("Müşteri Karlılık Analizi", "Karlılık", "PersonStar", "Müşteri bazlı kar/zarar analizi."),
            new("En Çok Satan Ürünler", "Satış", "Star", "Satış adedine göre top listeler."),
            new("Gelir Tablosu", "Finans", "DocumentData", "Dönemsel gelir ve gider özeti."),
            new("Detaylı Gelir ve Maliyet Analizi", "Karlılık", "Table", "Tarih bazlı ürün satışlarının kime yapıldığı, en son kimden ne kadara alındığı ve satır bazlı kâr analizi."),
            new("Nakit Akış Tablosu", "Finans", "Timeline", "Nakit giriş ve çıkışlarının takibi."),
            new("Müşteri ABC Analizi", "Stratejik", "Filter", "Ciroya göre müşteri sınıflandırması. A Sınıfı: En yüksek %80, B Sınıfı: Sonraki %15, C Sınıfı: Kalan %5"),
            new("Kar-Zarar Mukayesesi", "Stratejik", "ArrowRepeat", "Yıllık, aylık ve haftalık bazda performans analizi."),
            new("Vadesi Geçmiş Alacaklar", "Cari Hesap", "Alarm", "Ödeme süresi geçmiş tahsilatlar."),
            new("Müşteri Kayıp (Churn)", "Stratejik", "PersonDelete", "Kayıp riski olan pasif müşteriler."),
            new("Fiyat Dalgalanma Raporu", "Stratejik", "ShowChart", "Ürün fiyat değişim trendleri."),
            new("Müşteri Sadakat (LTV)", "Stratejik", "Heart", "Müşteri yaşam boyu değer analizi."),
            new("Finansal Isı Haritası", "Stratejik", "Map", "Günlük finansal aktivite yoğunluğu."),
            new("Tahsilat Süresi (DSO)", "Stratejik", "Speedometer", "Ortalama tahsilat hızı (gün)."),
            new("Bütçe / Hedef Takibi", "Stratejik", "Target", "Aylık hedeflere ulaşma durumu.")
        };
    }

    protected void InitializeReports()
    {
        Reports = new ObservableCollection<ReportItemViewModel>(GetAllReports());
    }

    [RelayCommand]
    public void SearchReports()
    {
        var all = GetAllReports();
        if (string.IsNullOrWhiteSpace(SearchString))
        {
            Reports = new ObservableCollection<ReportItemViewModel>(all);
            return;
        }

        var filtered = all.Where(r => 
            r.Title.Contains(SearchString, StringComparison.OrdinalIgnoreCase) || 
            r.Category.Contains(SearchString, StringComparison.OrdinalIgnoreCase)).ToList();
        
        Reports = new ObservableCollection<ReportItemViewModel>(filtered);
    }

    [RelayCommand]
    public async Task SearchCariAsync()
    {
        var all = await _uow.Cariler.GetAllAsync();
        if (string.IsNullOrEmpty(CariSearchTerm))
        {
            CariSearchResults = new ObservableCollection<CariKart>(all);
            return;
        }
        CariSearchResults = new ObservableCollection<CariKart>(
            all.Where(c => (c.Unvan != null && c.Unvan.Contains(CariSearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                          (c.CariKod != null && c.CariKod.Contains(CariSearchTerm, StringComparison.OrdinalIgnoreCase))));
    }


    [RelayCommand]
    public void CloseCariSelection()
    {
        IsCariSelectionVisible = false;
        _pendingReportItem = null;
    }

    [RelayCommand]
    public virtual async Task RunReport(ReportItemViewModel item)
    {
        await StartReportAction(item, false);
    }

    [RelayCommand]
    public virtual async Task PrintReport(ReportItemViewModel item)
    {
        await StartReportAction(item, true);
    }

    protected async Task StartReportAction(ReportItemViewModel item, bool isPrint)
    {
        if (item.Title == "Cari Hareket Dökümü")
        {
            _pendingReportItem = item;
            _isPendingPrint = isPrint;
            CariSearchTerm = "";
            await SearchCariAsync();
            IsCariSelectionVisible = true;
            return;
        }
        if (item.Title == "Gelir Tablosu" || item.Title == "Detaylı Gelir ve Maliyet Analizi")
        {
            _pendingReportItem = item;
            _isPendingPrint = isPrint;
            StartDate = new DateTime(DateTime.Now.Year, 1, 1);
            EndDate = new DateTime(DateTime.Now.Year, 12, 31);
            IsDateSelectionVisible = true;
            return;
        }

        await GenerateReportInternal(item, null, isPrint);
    }

    protected bool _isPendingPrint;

    [RelayCommand]
    public virtual async Task SelectCariAsync(CariKart cari)
    {
        if (_pendingReportItem == null) return;
        IsCariSelectionVisible = false;
        await GenerateReportInternal(_pendingReportItem, cari, _isPendingPrint);
        _pendingReportItem = null;
    }

    protected virtual async Task GenerateReportInternal(ReportItemViewModel item, CariKart? selectedCari = null, bool isPrint = false)
    {
        ErrorMessage = "";
        try 
        {
            IsGenerating = true;
            

            if (item.Title == "Genel Özet" && selectedCari == null) 
            {
                var sections = new List<ReportSection>();
                var summaryData = await CalculateReportDataAsync(item);
                var summaryMetrics = new List<ChartDataItem>();
                foreach (var sRow in summaryData.Rows.Take(4)) 
                {
                    summaryMetrics.Add(new ChartDataItem { Label = sRow[0], Actual = ParseMoney(sRow[1]) });
                }

                sections.Add(new ReportSection { 
                    Title = summaryData.Title, 
                    Headers = summaryData.Headers, 
                    Rows = summaryData.Rows,
                    KeyMetrics = summaryMetrics,
                    ChartData = summaryMetrics.Where(x => !x.Label.Contains("Miktar")).ToList(),
                    LeftChartTitle = "VARLIK VE YÜKÜMLÜLÜK DAĞILIMI"
                });

                var budgetReportItem = GetAllReports().FirstOrDefault(r => r.Title == "Bütçe / Hedef Takibi");
                if (budgetReportItem != null)
                {
                    var res = await CalculateReportDataAsync(budgetReportItem);
                    var budgetGraphData = await GetBudgetDataAsync();
                    
                    // 1. Aylık Grafik ve Tablo
                    sections.Add(new ReportSection { 
                        Title = "BÜTÇE VE HEDEF ANALİZİ (AYLIK)", 
                        Headers = res.Headers, 
                        Rows = res.Rows, 
                        ChartData = budgetGraphData.Monthly,
                        KeyMetrics = budgetGraphData.Monthly.OrderByDescending(x => x.Actual).Take(4).ToList(),
                        NewPage = true 
                    });

                    // 2. Haftalık Grafik (son 12 hafta ile sınırlı)
                    if (budgetGraphData.Weekly.Any())
                    {
                        var weeklyLimited = budgetGraphData.Weekly
                            .Where(w => w.Target > 0 || w.Actual > 0)
                            .TakeLast(12)
                            .ToList();
                        if (weeklyLimited.Any())
                        {
                            sections.Add(new ReportSection { 
                                Title = "BÜTÇE VE HEDEF ANALİZİ (HAFTALIK)", 
                                Headers = Array.Empty<string>(), 
                                Rows = new List<string[]>(), 
                                ChartData = weeklyLimited,
                                NewPage = false 
                            });
                        }
                    }
                }

                foreach(var report in GetAllReports().Where(r => r.Title != "Genel Özet" && r.Title != "Bütçe / Hedef Takibi"))
                {
                    try 
                    {
                        var (title, headers, rows) = await CalculateReportDataAsync(report);
                        if (rows != null && rows.Count > 0)
                        {
                            var section = new ReportSection { 
                                Title = title, 
                                Headers = headers, 
                                Rows = rows, 
                                NewPage = true 
                            };
                            
                            // Her rapor türüne uygun grafik verisini ekle
                            AttachChartData(section, report.Title, rows);
                            sections.Add(section);
                        }
                    } catch { }
                }

                var pdfBytesMulti = await _pdfService.GenerateConsolidatedReportPdfBytesAsync("ERMAY MUHASEBE - GENEL RAPOR PAKETİ", sections);
                string fName = "Genel_Ozet_Raporu.pdf";
                if (isPrint) await HandleFilePrintAsync(pdfBytesMulti, fName);
                else await HandleFileOpenAsync(pdfBytesMulti, fName);
            }
            else if (item.Title == "Bütçe / Hedef Takibi")
            {
                var budgetData = await GetBudgetDataAsync();
                var pdfBytesBudget = await _pdfService.GenerateBudgetReportPdfBytesAsync("BÜTÇE VE HEDEF ANALİZ RAPORU", budgetData.Annual, budgetData.Monthly, budgetData.Weekly);
                string fName = "Butce_Planlama_Raporu.pdf";
                if (isPrint) await HandleFilePrintAsync(pdfBytesBudget, fName);
                else await HandleFileOpenAsync(pdfBytesBudget, fName);
            }
            else if (item.Title == "Kar-Zarar Mukayesesi")
            {
                var (title, headers, rows) = await CalculateReportDataAsync(item);
                var section = new ReportSection { 
                    Title = title, 
                    Headers = headers, 
                    Rows = rows,
                    NewPage = false 
                };
                AttachChartData(section, item.Title, rows);
                
                var sections = new List<ReportSection> { section };
                var pdfBytesKZ = await _pdfService.GenerateConsolidatedReportPdfBytesAsync("KAR-ZARAR MUKAYESE RAPORU", sections);
                string fName = "Kar_Zarar_Raporu.pdf";
                if (isPrint) await HandleFilePrintAsync(pdfBytesKZ, fName);
                else await HandleFileOpenAsync(pdfBytesKZ, fName);
            }
            else 
            {
                var (rTitle, rHeaders, rRows) = await CalculateReportDataAsync(item, selectedCari);
                var pdfBytes = await _pdfService.GenerateGenericTablePdfBytesAsync(rTitle, rHeaders, rRows);
            
            // Dosya adındaki '/' ve '\' gibi geçersiz karakterleri temizle
            string safeFileName = rTitle.Replace(" ", "_").Replace("/", "_").Replace("\\", "_") + ".pdf";
            
            if (isPrint) await HandleFilePrintAsync(pdfBytes, safeFileName);
            else await HandleFileOpenAsync(pdfBytes, safeFileName);
        }

        // Always trigger background recalculation after any report is generated
        _ = Task.Run(async () => 
        {
            try { await _uow.RecalculateSystemBalancesAsync(); } catch { }
        });
    }
    catch (Exception ex)
    {
        ErrorMessage = $"Hata: {ex.Message}";
    }
    finally
        {
            IsGenerating = false;
        }
    }

    protected abstract Task HandleFileOpenAsync(byte[] content, string fileName);
    protected abstract Task HandleFilePrintAsync(byte[] content, string fileName);

    protected async Task<(List<ChartDataItem> Annual, List<ChartDataItem> Monthly, List<ChartDataItem> Weekly)> GetBudgetDataAsync()
    {
        int currentYear = DateTime.Now.Year;
        var yillikHedefler = await _uow.GetYillikSatisHedefleriAsync();
        var faturalar = await _uow.Faturalar.GetAllAsync();
        var satisFaturalari = faturalar.Where(f => f.Tur == "Satış" || f.Tur == "Satis").ToList();

        var annualData = new List<ChartDataItem>();
        foreach (var year in yillikHedefler.Select(y => y.Yil).Distinct().OrderByDescending(y => y))
        {
            var target = yillikHedefler.FirstOrDefault(x => x.Yil == year)?.HedefTutari ?? 0;
            var actual = satisFaturalari.Where(f => f.Tarih.Year == year).Sum(f => f.GenelToplam);
            annualData.Add(new ChartDataItem { Label = year.ToString(), Target = target, Actual = actual });
        }

        var aylikHedefler = await _uow.GetSatisHedefleriAsync();
        var monthlyData = new List<ChartDataItem>();
        var yearTargets = aylikHedefler.Where(x => x.Yil == currentYear).OrderBy(x => x.Ay).ToList();
        for (int m = 1; m <= 12; m++)
        {
            var t = yearTargets.FirstOrDefault(x => x.Ay == m)?.HedefTutari ?? 0;
            var a = satisFaturalari.Where(f => f.Tarih.Year == currentYear && f.Tarih.Month == m).Sum(f => f.GenelToplam);
            monthlyData.Add(new ChartDataItem { Label = m.ToString(), Target = t, Actual = a });
        }

        var weeklyTargets = await _uow.GetHaftalikSatisHedefleriAsync();
        var weeklyData = new List<ChartDataItem>();
        for (int w = 1; w <= 52; w++)
        {
            var wt = weeklyTargets.FirstOrDefault(x => x.Yil == currentYear && x.Hafta == w)?.HedefTutari ?? 0;
            var wa = satisFaturalari.Where(f => f.Tarih.Year == currentYear && System.Globalization.ISOWeek.GetWeekOfYear(f.Tarih) == w).Sum(f => f.GenelToplam);
            if (wt > 0 || wa > 0) weeklyData.Add(new ChartDataItem { Label = w.ToString(), Target = wt, Actual = wa });
        }

        return (annualData, monthlyData, weeklyData);
    }

    protected async Task<(string Title, string[] Headers, List<string[]> Rows)> CalculateReportDataAsync(ReportItemViewModel item, CariKart? selectedCari = null)
    {
        List<string[]> data = new();
        string[] headers = Array.Empty<string>();
        string title = item.Title;

        // Implementation of calculations... (truncated for brevity but I will include the core logic from the original)
        if (item.Title == "Aylık Tahsilat ve Ödeme Analizi")
        {
            var kk = await _uow.KrediKartlari.GetAllAsync();
            var nakit = await _uow.Kasalar.GetAllHareketlerAsync();
            var eft = await _uow.EftIslemleri.GetAllAsync();
            var cekler = await _uow.Cekler.GetAllAsync();

            headers = new[] { "Ay/Yıl", "Nakit (Alınan)", "Nakit (Yönl.)", "K.Kartı (Alınan)", "K.Kartı (Yönl.)", "Havale (Alınan)", "Havale (Yönl.)", "Çek (Alınan)", "Çek (Yönl.)" };
            
            var allDates = kk.Select(x => x.Tarih)
                .Concat(nakit.Select(x => x.Tarih))
                .Concat(eft.Select(x => x.Tarih))
                .Concat(cekler.Select(x => x.VadeTarihi))
                .Where(d => d != DateTime.MinValue)
                .Select(d => new DateTime(d.Year, d.Month, 1))
                .Distinct()
                .OrderByDescending(x => x)
                .Take(24);

            foreach (var month in allDates)
            {
                var nextMonth = month.AddMonths(1);
                
                // Nakit
                var nGiren = nakit.Where(x => x.Tarih >= month && x.Tarih < nextMonth && IsNakitMovement(x)).Sum(x => x.Giren);
                var nCikan = nakit.Where(x => x.Tarih >= month && x.Tarih < nextMonth && IsNakitMovement(x)).Sum(x => x.Cikan);
                
                // Kredi Kartı
                var kkAlinan = kk.Where(x => x.Tarih >= month && x.Tarih < nextMonth).Sum(x => x.Tutar);
                var kkYonl = kk.Where(x => x.YonlendirmeTarihi >= month && x.YonlendirmeTarihi < nextMonth && x.YonlendirilenCariId != null).Sum(x => x.Tutar);
                
                // Havale / EFT
                var eftAlinan = eft.Where(x => x.Tarih >= month && x.Tarih < nextMonth).Sum(x => x.Tutar);
                var eftYonl = eft.Where(x => x.YonlendirmeTarihi >= month && x.YonlendirmeTarihi < nextMonth && x.YonlendirilenCariId != null).Sum(x => x.Tutar);
                
                // Çek
                var cekAlinan = cekler.Where(x => x.VadeTarihi >= month && x.VadeTarihi < nextMonth).Sum(x => x.Tutar);
                var cekYonl = cekler.Where(x => x.Durum != null && (x.Durum.Contains("Ciro") || x.Durum.Contains("Tedarikçi")) && x.VadeTarihi >= month && x.VadeTarihi < nextMonth).Sum(x => x.Tutar);

                data.Add(new[] { 
                    month.ToString("MM/yyyy"), 
                    nGiren.ToString("C2"), nCikan.ToString("C2"),
                    kkAlinan.ToString("C2"), kkYonl.ToString("C2"),
                    eftAlinan.ToString("C2"), eftYonl.ToString("C2"),
                    cekAlinan.ToString("C2"), cekYonl.ToString("C2")
                });
            }
        }
        else if (item.Title == "Kredi Kartı Detay Raporu")
        {
            var islemler = await _uow.KrediKartlari.GetAllAsync();
            headers = new[] { "Tarih", "Kimden Alındı", "Banka/Kart", "Kime Yönlendirildi", "Tutar", "Durum" };
            foreach (var k in islemler.OrderByDescending(x => x.Tarih))
            {
                data.Add(new[] { 
                    k.Tarih.ToString("dd.MM.yyyy"), 
                    k.MusteriUnvan ?? "-", 
                    k.Banka ?? "-", 
                    k.YonlendirilenCariUnvan ?? "-", 
                    k.Tutar.ToString("C2"), 
                    k.Durum ?? "Portföyde" 
                });
            }
        }
        else if (item.Title == "Nakit İşlem Detay Raporu")
        {
            var hareketler = await _uow.Kasalar.GetAllHareketlerAsync();
            var nakitHareketler = hareketler.Where(h => string.IsNullOrEmpty(h.IslemTuru) || h.IslemTuru.Contains("Nakit", StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.Tarih);
            headers = new[] { "Tarih", "Cari Ünvan", "İşlem", "Açıklama", "Giren", "Çıkan" };
            foreach (var h in nakitHareketler)
            {
                data.Add(new[] { 
                    h.Tarih.ToString("dd.MM.yyyy HH:mm"), 
                    h.CariUnvan ?? "-", 
                    h.IslemTuru ?? "-", 
                    h.Aciklama ?? "-", 
                    h.Giren.ToString("C2"), 
                    h.Cikan.ToString("C2") 
                });
            }
        }
        else if (item.Title == "Çek Detay Raporu")
        {
            var cekler = await _uow.Cekler.GetAllAsync();
            headers = new[] { "Vade", "Çek No", "Cari Ünvan", "Banka/Şube", "Tutar", "Durum" };
            foreach (var c in cekler.OrderBy(x => x.VadeTarihi))
            {
                data.Add(new[] { 
                    c.VadeTarihi.ToString("dd.MM.yyyy"), 
                    c.CekNo ?? "-", 
                    c.CariUnvan ?? "-", 
                    $"{c.Banka ?? ""}/{c.Sube ?? ""}", 
                    c.Tutar.ToString("C2"), 
                    c.Durum ?? "-" 
                });
            }
        }
        else if (item.Title == "Havale / EFT Detay Raporu")
        {
            var islemler = await _uow.EftIslemleri.GetAllAsync();
            headers = new[] { "Tarih", "Müşteri", "Banka/Hesap", "Dekont No", "Tutar", "Durum" };
            foreach (var e in islemler.OrderByDescending(x => x.Tarih))
            {
                data.Add(new[] { 
                    e.Tarih.ToString("dd.MM.yyyy"), 
                    e.MusteriUnvan ?? "-", 
                    $"{e.Banka ?? ""}/{e.HesapNo ?? ""}", 
                    e.DekontNo ?? "-", 
                    e.Tutar.ToString("C2"), 
                    e.Durum ?? "-" 
                });
            }
        }
        else if (item.Title == "Stok Mevcudu")
        {
            var list = await _uow.Stoklar.GetAllAsync();
            headers = new[] { "Stok Kodu", "Stok Adı", "Kategori", "Miktar", "Ort. Alış F.", "Ort. Satış F.", "Satış F." };
            foreach (var s in list)
                data.Add(new[] { s.StokKodu ?? "", s.StokAdi ?? "", s.Kategori ?? "-", s.Miktar.ToString("N2"), s.OrtalamaAlisFiyati.ToString("C2"), s.OrtalamaSatisFiyati.ToString("C2"), s.SatisFiyati.ToString("C2") });
        }

        else if (item.Title == "Genel Özet")
        {
            var cariler = await _uow.Cariler.GetAllAsync();
            var stoklar = await _uow.Stoklar.GetAllAsync();
            var bankalar = await _uow.Bankalar.GetAllAsync();
            
            var recv = cariler.Where(c => (c.Borc - c.Alacak) > 0).Sum(c => c.Borc - c.Alacak);
            var pay = cariler.Where(c => (c.Borc - c.Alacak) < 0).Sum(c => c.Alacak - c.Borc);
            var totalCash = bankalar.Sum(b => b.GuncelBakiye);

            headers = new[] { "Rapor Kalemi", "Tutar" };
            data.Add(new[] { "Toplam Alacaklar", recv.ToString("C2") });
            data.Add(new[] { "Toplam Borçlar", pay.ToString("C2") });
            data.Add(new[] { "Kasa / Banka Mevcudu", totalCash.ToString("C2") });
            data.Add(new[] { "Toplam Stok Miktarı", stoklar.Sum(s => s.Miktar).ToString("N2") });
            data.Add(new[] { "Stok Maliyet Değeri", stoklar.Sum(s => (decimal)s.Miktar * s.AlisFiyati).ToString("C2") });
        }
        else if (item.Title == "Cari Bakiye Raporu")
        {
            var list = await _uow.Cariler.GetAllAsync();
            headers = new[] { "Ünvan", "Şehir", "Borç", "Alacak", "Bakiye", "Durum" };
            foreach (var c in list)
            {
                var bakiye = c.Borc - c.Alacak;
                var durum = bakiye > 0 ? "Borçlu" : (bakiye < 0 ? "Alacaklı" : "Dengede");
                data.Add(new[] { c.Unvan ?? "", c.Il ?? "", c.Borc.ToString("C2"), c.Alacak.ToString("C2"), bakiye.ToString("C2"), durum });
            }
        }
        else if (item.Title == "Cari Hareket Dökümü")
        {
            var list = await _uow.Cariler.GetAllHareketlerAsync();
            if (selectedCari != null)
            {
                list = list.Where(h => h.CariId == selectedCari.Id).ToList();
                title = $"{selectedCari.Unvan} - Hareket Dökümü";
            }
            headers = new[] { "Tarih", "Ünvan", "İşlem", "Borç", "Alacak" };
            foreach (var h in list)
                data.Add(new[] { h.Tarih.ToString("dd.MM.yyyy"), h.CariUnvan ?? "-", h.IslemTuru ?? "", h.Borc.ToString("C2"), h.Alacak.ToString("C2") });
        }
        // ... (Adding few more core ones for demo, the real one has 30+)
        else if (item.Title == "Bütçe / Hedef Takibi")
        {
            var faturalar = await _uow.Faturalar.GetAllAsync();
            var hedefler = await _uow.GetSatisHedefleriAsync();
            headers = new[] { "Dönem", "Hedef", "Gerçekleşen", "Fark", "%" };
            foreach(var h in hedefler.OrderByDescending(x => x.Yil).ThenByDescending(x => x.Ay))
            {
                var actual = faturalar.Where(f => f.Tarih.Year == h.Yil && f.Tarih.Month == h.Ay && (f.Tur == "Satış" || f.Tur == "Satis")).Sum(f => f.GenelToplam);
                data.Add(new[] { $"{h.Ay}/{h.Yil}", h.HedefTutar.ToString("C2"), actual.ToString("C2"), (actual-h.HedefTutar).ToString("C2"), (h.HedefTutar>0 ? (actual/h.HedefTutar*100).ToString("N1") : "0") });
            }
        }
        else if (item.Title == "Yaşlandırma Raporu")
        {
            var cariler = await _uow.Cariler.GetAllAsync();
            var hareketler = await _uow.Cariler.GetAllHareketlerAsync();
            headers = new[] { "Ünvan", "0-30 Gün", "31-60 Gün", "61-90 Gün", "90+ Gün", "Toplam" };
            
            foreach (var c in cariler.Where(x => (x.Borc - x.Alacak) > 0))
            {
                var cHareketler = hareketler.Where(h => h.CariId == c.Id && (h.Borc - h.Alacak) > 0).ToList();
                var now = DateTime.Now;
                var d0_30 = cHareketler.Where(h => (now - h.Tarih).TotalDays <= 30).Sum(h => h.Borc - h.Alacak);
                var d31_60 = cHareketler.Where(h => (now - h.Tarih).TotalDays > 30 && (now - h.Tarih).TotalDays <= 60).Sum(h => h.Borc - h.Alacak);
                var d61_90 = cHareketler.Where(h => (now - h.Tarih).TotalDays > 60 && (now - h.Tarih).TotalDays <= 90).Sum(h => h.Borc - h.Alacak);
                var d90plus = cHareketler.Where(h => (now - h.Tarih).TotalDays > 90).Sum(h => h.Borc - h.Alacak);
                var total = d0_30 + d31_60 + d61_90 + d90plus;
                if (total > 0)
                    data.Add(new[] { c.Unvan ?? "", d0_30.ToString("C2"), d31_60.ToString("C2"), d61_90.ToString("C2"), d90plus.ToString("C2"), total.ToString("C2") });
            }
        }
        else if (item.Title == "Hareketsiz Cariler")
        {
            var cariler = await _uow.Cariler.GetAllAsync();
            var hareketler = await _uow.Cariler.GetAllHareketlerAsync();
            headers = new[] { "Ünvan", "Grup", "Telefon", "Son İşlem", "Gün" };
            var cutoffDate = DateTime.Now.AddMonths(-6);
            
            foreach (var c in cariler)
            {
                var lastTransaction = hareketler.Where(h => h.CariId == c.Id).OrderByDescending(h => h.Tarih).FirstOrDefault();
                if (lastTransaction == null || lastTransaction.Tarih < cutoffDate)
                {
                    string daysSinceStr = "-";
                    if (lastTransaction != null)
                        daysSinceStr = (DateTime.Now - lastTransaction.Tarih).Days.ToString();
                    else if (c.KayitTarihi.Year > 2000)
                        daysSinceStr = (DateTime.Now - c.KayitTarihi).Days.ToString();
                    else
                        daysSinceStr = "İşlem Yok";

                    data.Add(new[] { c.Unvan ?? "", c.Grup ?? "-", c.Telefon ?? "-", lastTransaction?.Tarih.ToString("dd.MM.yyyy") ?? "Hiç", daysSinceStr });
                }
            }
        }
        else if (item.Title == "Stok Hareketleri")
        {
            var hareketler = (await _uow.Stoklar.GetAllHareketlerAsync()).OrderBy(x => x.Tarih).ToList();
            var balances = new Dictionary<int, decimal>();
            
            headers = new[] { "Tarih", "Stok Adı", "İşlem", "Giren", "Çıkan", "Birim Fiyat", "Kalan" };
            
            foreach (var h in hareketler)
            {
                if (!balances.ContainsKey(h.StokId)) balances[h.StokId] = 0;
                balances[h.StokId] += (h.Giren - h.Cikan);
                h.KalanMiktar = balances[h.StokId];
            }

            foreach (var h in hareketler.OrderByDescending(x => x.Tarih).Take(1000))
            {
                data.Add(new[] { 
                    h.Tarih.ToString("dd.MM.yyyy"), 
                    h.StokAdi ?? "-", 
                    h.IslemTuru ?? "", 
                    h.Giren.ToString("N2"), 
                    h.Cikan.ToString("N2"), 
                    h.Fiyat.ToString("C2"),
                    h.KalanMiktar.ToString("N2")
                });
            }
        }
        else if (item.Title == "Kritik Stok Seviyesi")
        {
            var stoklar = await _uow.Stoklar.GetAllAsync();
            headers = new[] { "Stok Kodu", "Stok Adı", "Mevcut", "Min. Seviye", "Durum", "Satış Fiyatı" };
            foreach (var s in stoklar.Where(x => x.Miktar < 0 || (x.MinSeviye > 0 && x.Miktar <= x.MinSeviye)))
            {
                var durum = s.Miktar < 0 ? "Eksiye Düşmüş!" : (s.Miktar == 0 ? "Tükenmiş" : "Kritik");
                data.Add(new[] { s.StokKodu ?? "", s.StokAdi ?? "", s.Miktar.ToString("N2"), s.MinSeviye.ToString("N2"), durum, s.SatisFiyati.ToString("C2") });
            }
        }
        else if (item.Title == "Ölü Stok Raporu")
        {
            var stoklar = await _uow.Stoklar.GetAllAsync();
            headers = new[] { "Stok Adı", "Barkod", "Stok Kodu", "Mevcut Miktar", "Çakışma Nedeni" };
            
            var nameGroups = stoklar.Where(s => !string.IsNullOrWhiteSpace(s.StokAdi))
                                     .GroupBy(s => s.StokAdi!.Trim().ToLower())
                                     .Where(g => g.Count() > 1)
                                     .ToList();

            var barcodeGroups = stoklar.Where(s => !string.IsNullOrWhiteSpace(s.Barkod))
                                         .GroupBy(s => s.Barkod!.Trim().ToLower())
                                         .Where(g => g.Count() > 1)
                                         .ToList();

            var codeGroups = stoklar.Where(s => !string.IsNullOrWhiteSpace(s.StokKodu))
                                         .GroupBy(s => s.StokKodu!.Trim().ToLower())
                                         .Where(g => g.Count() > 1)
                                         .ToList();

            var duplicateIds = new HashSet<int>();
            
            foreach (var g in nameGroups)
            {
                foreach (var s in g)
                {
                    duplicateIds.Add(s.Id);
                    data.Add(new[] { s.StokAdi ?? "", s.Barkod ?? "-", s.StokKodu ?? "-", s.Miktar.ToString("N2"), "Aynı İsim" });
                }
            }

            foreach (var g in barcodeGroups)
            {
                foreach (var s in g)
                {
                    if (duplicateIds.Contains(s.Id)) continue;
                    duplicateIds.Add(s.Id);
                    data.Add(new[] { s.StokAdi ?? "", s.Barkod ?? "-", s.StokKodu ?? "-", s.Miktar.ToString("N2"), "Aynı Barkod" });
                }
            }

            foreach (var g in codeGroups)
            {
                foreach (var s in g)
                {
                    if (duplicateIds.Contains(s.Id)) continue;
                    duplicateIds.Add(s.Id);
                    data.Add(new[] { s.StokAdi ?? "", s.Barkod ?? "-", s.StokKodu ?? "-", s.Miktar.ToString("N2"), "Aynı Stok Kodu" });
                }
            }

            // Eğer hiç çakışma yoksa bile bilgi satırı eklenebilir veya boş kalabilir.
            if (data.Count == 0)
            {
                 data.Add(new[] { "-", "-", "-", "-", "Çakışan kart bulunamadı" });
            }
        }
        else if (item.Title == "Stok Devir Hızı")
        {
            var stoklar = await _uow.Stoklar.GetAllAsync();
            var hareketler = await _uow.Stoklar.GetAllHareketlerAsync();
            headers = new[] { "Stok Adı", "Mevcut Stok", "Toplam Satış (Çıkan)", "Stok Devir Hızı", "Kategori" };
            
            var satisMiktarlari = hareketler
                .Where(h => h.Cikan > 0)
                .GroupBy(h => h.StokId)
                .ToDictionary(g => g.Key, g => g.Sum(h => h.Cikan));

            foreach (var s in stoklar)
            {
                satisMiktarlari.TryGetValue(s.Id, out var satisMiktari);
                double devirHizi = 0;
                if (s.Miktar > 0)
                {
                    devirHizi = (double)satisMiktari / (double)s.Miktar;
                }
                
                data.Add(new[] { 
                    s.StokAdi ?? "", 
                    s.Miktar.ToString("N2"), 
                    satisMiktari.ToString("N2"), 
                    devirHizi.ToString("N2"), 
                    s.Kategori ?? "-" 
                });
            }
        }
        else if (item.Title == "Satış Faturası Dökümü")
        {
            var faturalar = await _uow.Faturalar.GetAllAsync();
            var satisFaturalari = faturalar.Where(f => f.Tur == "Satış" || f.Tur == "Satis").OrderByDescending(f => f.Tarih).ToList();
            headers = new[] { "Fatura No", "Tarih", "Müşteri", "Ara Toplam", "KDV", "Genel Toplam" };
            foreach (var f in satisFaturalari.Take(500))
                data.Add(new[] { f.FaturaNo ?? "", f.Tarih.ToString("dd.MM.yyyy"), f.CariUnvan ?? "-", f.AraToplam.ToString("C2"), f.ToplamKDV.ToString("C2"), f.GenelToplam.ToString("C2") });
        }
        else if (item.Title == "Ürün Karlılık Raporu")
        {
            var stoklar = await _uow.Stoklar.GetAllAsync();
            headers = new[] { "Ürün Adı", "Alış Fiyatı", "Satış Fiyatı", "Kar", "Kar Marjı %" };
            
            foreach (var s in stoklar.Where(x => x.SatisFiyati > 0))
            {
                var profit = s.SatisFiyati - s.AlisFiyati;
                var margin = s.SatisFiyati > 0 ? (profit / s.SatisFiyati * 100) : 0;
                data.Add(new[] { s.StokAdi ?? "", s.AlisFiyati.ToString("C2"), s.SatisFiyati.ToString("C2"), profit.ToString("C2"), margin.ToString("N2") });
            }
        }
        else if (item.Title == "Müşteri Karlılık Analizi")
        {
            var faturalar = await _uow.Faturalar.GetAllAsync();
            var faturaDetaylar = await _uow.Faturalar.GetAllDetaylarAsync();
            var stoklar = await _uow.Stoklar.GetAllAsync();
            var stokDict = stoklar.ToDictionary(s => s.Id);
            
            headers = new[] { "Müşteri", "Toplam Satış", "Tahmini Maliyet", "Net Kar", "Kar Marjı (%)", "Fatura Sayısı" };
            
            var satisFaturalari = faturalar.Where(f => f.Tur == "Satış" || f.Tur == "Satis").ToList();
            var grouped = satisFaturalari.GroupBy(f => new { f.CariId, Unvan = f.CariUnvan ?? "Bilinmeyen" });
            
            foreach (var g in grouped.OrderByDescending(g => g.Sum(f => f.GenelToplam)).Take(100))
            {
                var totalSales = g.Sum(f => f.GenelToplam);
                var count = g.Count();
                
                // Calculate actual cost from invoice details
                decimal totalCost = 0;
                foreach (var fatura in g)
                {
                    var detaylar = faturaDetaylar.Where(d => d.FaturaId == fatura.Id).ToList();
                    foreach (var detay in detaylar)
                    {
                        decimal unitCost = 0;
                        if (stokDict.TryGetValue(detay.StokId, out var stok))
                        {
                            unitCost = stok.OrtalamaAlisFiyati > 0 ? stok.OrtalamaAlisFiyati : stok.AlisFiyati;
                        }
                        totalCost += unitCost * (decimal)detay.Miktar;
                    }
                }
                
                var netKar = totalSales - totalCost;
                var karMarji = totalSales > 0 ? (netKar / totalSales * 100) : 0;
                
                data.Add(new[] { 
                    g.Key.Unvan, 
                    totalSales.ToString("C2"), 
                    totalCost.ToString("C2"), 
                    netKar.ToString("C2"), 
                    karMarji.ToString("N1") + "%", 
                    count.ToString() 
                });
            }
        }
        else if (item.Title == "Bölge Bazlı Satış")
        {
            var faturalar = await _uow.Faturalar.GetAllAsync();
            var cariler = await _uow.Cariler.GetAllAsync();
            headers = new[] { "Şehir", "Müşteri Sayısı", "Toplam Satış", "Ortalama Satış" };
            
            var satisFaturalari = faturalar.Where(f => f.Tur == "Satış" || f.Tur == "Satis").ToList();
            var grouped = satisFaturalari.GroupBy(f => {
                var cari = cariler.FirstOrDefault(c => c.Id == f.CariId);
                return cari?.Il ?? "Bilinmiyor";
            });
            
            foreach (var g in grouped.OrderByDescending(g => g.Sum(f => f.GenelToplam)))
            {
                var totalSales = g.Sum(f => f.GenelToplam);
                var customerCount = g.Select(f => f.CariId).Distinct().Count();
                var avgSales = customerCount > 0 ? totalSales / customerCount : 0;
                data.Add(new[] { g.Key, customerCount.ToString(), totalSales.ToString("C2"), avgSales.ToString("C2") });
            }
        }
        else if (item.Title == "En Çok Satan Ürünler")
        {
            var stoklar = await _uow.Stoklar.GetAllAsync();
            headers = new[] { "Sıra", "Ürün Adı", "Mevcut Stok", "Satış Fiyatı", "Kategori" };
            
            int rank = 1;
            foreach (var s in stoklar.OrderByDescending(s => s.Miktar).Take(50))
            {
                data.Add(new[] { rank++.ToString(), s.StokAdi ?? "", s.Miktar.ToString("N2"), s.SatisFiyati.ToString("C2"), s.Kategori ?? "-" });
            }
        }
        else if (item.Title == "Gelir Tablosu")
        {
            var faturalar = await _uow.Faturalar.GetAllAsync();
            var faturaDetaylar = await _uow.Faturalar.GetAllDetaylarAsync();
            var stoklar = await _uow.Stoklar.GetAllAsync();
            var stokDict = stoklar.ToDictionary(s => s.Id);

            var start = StartDate ?? new DateTime(DateTime.Now.Year, 1, 1);
            var end = EndDate ?? new DateTime(DateTime.Now.Year, 12, 31);
            
            var satisFaturalari = faturalar.Where(f => (f.Tur == "Satış" || f.Tur == "Satis") && f.Tarih.Date >= start.Date && f.Tarih.Date <= end.Date).ToList();
            var alisFaturalari = faturalar.Where(f => (f.Tur == "Alış" || f.Tur == "Alis") && f.Tarih.Date >= start.Date && f.Tarih.Date <= end.Date).ToList();
            
            title = $"Gelir Tablosu ({start:dd.MM.yyyy} - {end:dd.MM.yyyy})";
            headers = new[] { "Kalem", "Tutar" };
            
            decimal totalRevenue = satisFaturalari.Sum(f => f.GenelToplam);
            decimal totalAlis = alisFaturalari.Sum(f => f.GenelToplam);
            
            // Satılan Malın Maliyeti (SMM) Hesaplama: Satış faturalarındaki ürünlerin alış maliyeti
            decimal smmMaliyet = 0;
            foreach (var fatura in satisFaturalari)
            {
                var detaylar = faturaDetaylar.Where(d => d.FaturaId == fatura.Id).ToList();
                foreach (var detay in detaylar)
                {
                    decimal unitCost = 0;
                    if (stokDict.TryGetValue(detay.StokId, out var stok))
                    {
                        unitCost = stok.OrtalamaAlisFiyati > 0 ? stok.OrtalamaAlisFiyati : stok.AlisFiyati;
                    }
                    smmMaliyet += unitCost * (decimal)detay.Miktar;
                }
            }
            
            decimal grossProfit = totalRevenue - smmMaliyet;
            
            data.Add(new[] { "Satış Gelirleri (Toplam Satış)", totalRevenue.ToString("C2") });
            data.Add(new[] { "Satılan Malın Maliyeti (SMM)", smmMaliyet.ToString("C2") });
            data.Add(new[] { "Brüt Kar (Kazanılan Net Para)", grossProfit.ToString("C2") });
            data.Add(new[] { "Dönem İçi Toplam Alış (Satın Alma)", totalAlis.ToString("C2") });
            data.Add(new[] { "Net Dönem Karı", grossProfit.ToString("C2") });
        }
        else if (item.Title == "Detaylı Gelir ve Maliyet Analizi")
        {
            var faturalar = await _uow.Faturalar.GetAllAsync();
            var faturaDetaylar = await _uow.Faturalar.GetAllDetaylarAsync();
            var stoklar = await _uow.Stoklar.GetAllAsync();
            var stokDict = stoklar.ToDictionary(s => s.Id);
            var cariHareketler = await _uow.Cariler.GetAllHareketlerAsync();
            
            var start = StartDate ?? new DateTime(DateTime.Now.Year, 1, 1);
            var end = EndDate ?? new DateTime(DateTime.Now.Year, 12, 31);
            
            var satisFaturalari = faturalar.Where(f => (f.Tur == "Satış" || f.Tur == "Satis") && f.Tarih.Date >= start.Date && f.Tarih.Date <= end.Date).OrderBy(f => f.Tarih).ToList();
            var alisFaturalari = faturalar.Where(f => (f.Tur == "Alış" || f.Tur == "Alis")).OrderByDescending(f => f.Tarih).ToList();
            
            title = $"Detaylı Gelir ve Maliyet Analizi ({start:dd.MM.yyyy} - {end:dd.MM.yyyy})";
            headers = new[] { 
                "Tarih", 
                "Ürün Adı", 
                "Müşteri (Kime)", 
                "Miktar", 
                "Satış Fiyatı", 
                "Tutar (Net/KDV)", 
                "Tahsilat Yöntemi & Süresi",
                "En Son Tedarikçi", 
                "Alış Fiyatı & Değişim", 
                "Maliyet", 
                "Net Kar & Durum", 
                "Marj",
                "Ödeme Yöntemi & Süresi" 
            };
            
            foreach (var fatura in satisFaturalari)
            {
                var detaylar = faturaDetaylar.Where(d => d.FaturaId == fatura.Id).ToList();
                
                // Müşteri Tahsilat Analizi: Bu faturaya karşılık yapılan ilk/en yakın tahsilat hareketi
                var cariTahsilatlar = cariHareketler
                    .Where(h => h.CariId == fatura.CariId && h.Alacak > 0 && h.Tarih.Date >= fatura.Tarih.Date)
                    .OrderBy(h => h.Tarih)
                    .FirstOrDefault();
                
                string tahsilatBilgisi = "Ödenmedi / Açık Hesap";
                if (cariTahsilatlar != null)
                {
                    int gunFarki = (cariTahsilatlar.Tarih.Date - fatura.Tarih.Date).Days;
                    string yontem = cariTahsilatlar.IslemTuru ?? "EFT/Havale";
                    tahsilatBilgisi = $"{yontem} ({gunFarki} Gün)";
                }
                
                foreach (var detay in detaylar)
                {
                    string urunAdi = "";
                    decimal unitCost = 0;
                    decimal baslangicAlisFiyati = 0;
                    string tedarikci = "Stok Tanımlı / Bilinmiyor";
                    string alisOdemeBilgisi = "Ödenmedi";
                    
                    if (stokDict.TryGetValue(detay.StokId, out var stok))
                    {
                        urunAdi = stok.StokAdi ?? "";
                        unitCost = stok.OrtalamaAlisFiyati > 0 ? stok.OrtalamaAlisFiyati : stok.AlisFiyati;
                        baslangicAlisFiyati = stok.AlisFiyati;
                    }
                    
                    // En son alış faturasını bulup tedarikçi adını, birim fiyatını ve ödeme analizini çekelim
                    var sonAlisDetay = faturaDetaylar
                        .Where(d => d.StokId == detay.StokId)
                        .Select(d => new { Detay = d, Fatura = alisFaturalari.FirstOrDefault(f => f.Id == d.FaturaId) })
                        .Where(x => x.Fatura != null)
                        .OrderByDescending(x => x.Fatura!.Tarih)
                        .FirstOrDefault();
                        
                    if (sonAlisDetay != null)
                    {
                        var alisFatura = sonAlisDetay.Fatura!;
                        tedarikci = alisFatura.CariUnvan ?? "Bilinmiyor";
                        unitCost = (decimal)sonAlisDetay.Detay.BirimFiyat;
                        
                        // Tedarikçi Ödeme Analizi: Alış faturasına karşılık tedarikçiye yapılan ilk ödeme hareketi
                        var cariOdemeler = cariHareketler
                            .Where(h => h.CariId == alisFatura.CariId && h.Borc > 0 && h.Tarih.Date >= alisFatura.Tarih.Date)
                            .OrderBy(h => h.Tarih)
                            .FirstOrDefault();
                            
                        if (cariOdemeler != null)
                        {
                            int gunFarki = (cariOdemeler.Tarih.Date - alisFatura.Tarih.Date).Days;
                            string yontem = cariOdemeler.IslemTuru ?? "Nakit/Banka";
                            alisOdemeBilgisi = $"{yontem} ({gunFarki} Gün)";
                        }
                    }
                    
                    // Fiyat trendi (Alış fiyatı artış/azalış)
                    string fiyatTrendi = "";
                    if (baslangicAlisFiyati > 0)
                    {
                        decimal degisimOrani = (unitCost - baslangicAlisFiyati) / baslangicAlisFiyati * 100;
                        if (degisimOrani > 1) fiyatTrendi = $" (+%{degisimOrani:N0} Artış)";
                        else if (degisimOrani < -1) fiyatTrendi = $" (-%{Math.Abs(degisimOrani):N0} Düşüş)";
                    }
                    
                    decimal satisBirimFiyat = (decimal)detay.BirimFiyat;
                    decimal miktar = (decimal)detay.Miktar;
                    decimal satisTutar = satisBirimFiyat * miktar;
                    
                    // İskonto ve KDV detayları
                    decimal kdvOrani = (decimal)detay.KDVOrani;
                    decimal kdvTutar = satisTutar * (kdvOrani / 100);
                    string tutarDetay = $"{satisTutar.ToString("C2")} (+{kdvTutar.ToString("C2")} KDV)";
                    
                    decimal maliyetTutar = unitCost * miktar;
                    decimal netKar = satisTutar - maliyetTutar;
                    decimal marj = satisTutar > 0 ? (netKar / satisTutar * 100) : 0;
                    
                    // Risk / Durum Sınıflaması
                    string riskDurumu = "Normal Kar";
                    if (netKar < 0) riskDurumu = "ZARAR";
                    else if (marj < 10) riskDurumu = "Dusuk Kar";
                    else if (marj > 25) riskDurumu = "Yuksek Kar";
                    
                    data.Add(new[] {
                        fatura.Tarih.ToString("dd.MM.yyyy"),
                        urunAdi,
                        fatura.CariUnvan ?? "Bilinmeyen Müşteri",
                        miktar.ToString("N2"),
                        satisBirimFiyat.ToString("C2"),
                        tutarDetay,
                        tahsilatBilgisi,
                        tedarikci,
                        $"{unitCost.ToString("C2")}{fiyatTrendi}",
                        maliyetTutar.ToString("C2"),
                        $"{netKar.ToString("C2")} ({riskDurumu})",
                        marj.ToString("N1") + "%",
                        alisOdemeBilgisi
                    });
                }
            }
        }
        else if (item.Title == "Nakit Akış Tablosu")
        {
            var kasaHareketler = await _uow.Kasalar.GetAllHareketlerAsync();
            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;
            headers = new[] { "Tarih", "Açıklama", "Giren", "Çıkan", "Bakiye" };
            
            decimal runningBalance = 0;
            foreach (var h in kasaHareketler.Where(k => IsNakitMovement(k) && k.Tarih.Year == currentYear && k.Tarih.Month == currentMonth).OrderBy(k => k.Tarih))
            {
                runningBalance += h.Giren - h.Cikan;
                data.Add(new[] { h.Tarih.ToString("dd.MM.yyyy"), h.Aciklama ?? "-", h.Giren.ToString("C2"), h.Cikan.ToString("C2"), runningBalance.ToString("C2") });
            }
        }
        else if (item.Title == "Kasa Hareketleri")
        {
            var kasalar = await _uow.Bankalar.GetAllAsync();
            var kasaHareketler = await _uow.Kasalar.GetAllHareketlerAsync();
            headers = new[] { "Tarih", "Kasa", "İşlem", "Giren", "Çıkan" };
            
            foreach (var h in kasaHareketler.OrderByDescending(k => k.Tarih).Take(500))
            {
                var kasa = kasalar.FirstOrDefault(k => k.Id == h.KasaId);
                data.Add(new[] { h.Tarih.ToString("dd.MM.yyyy HH:mm"), kasa?.BankaAdi ?? "-", h.IslemTuru ?? "", h.Giren.ToString("C2"), h.Cikan.ToString("C2") });
            }
        }
        else if (item.Title == "Banka Hareketleri")
        {
            var bankalar = await _uow.Bankalar.GetAllAsync();
            var bankaHareketler = bankalar.Where(b => b.KartTuru == "Banka").ToList();
            headers = new[] { "Banka Adı", "Hesap No", "Güncel Bakiye", "Döviz" };
            
            foreach (var b in bankaHareketler)
                data.Add(new[] { b.BankaAdi ?? "", b.HesapNo ?? "-", b.GuncelBakiye.ToString("C2"), b.DovizTuru ?? "TL" });
        }
        else if (item.Title == "Kapsamlı Gider Raporu")
        {
            var alisFaturalari = (await _uow.Faturalar.GetAllAsync()).Where(f => f.Tur == "Alış" || f.Tur == "Alis").ToList();
            var kasaHareketler = await _uow.Kasalar.GetAllHareketlerAsync();
            headers = new[] { "Kategori", "Tutar", "Adet" };
            
            var faturaGiderleri = alisFaturalari.Sum(f => f.GenelToplam);
            var kasaCikislari = kasaHareketler.Sum(k => k.Cikan);
            
            data.Add(new[] { "Alış Faturaları", faturaGiderleri.ToString("C2"), alisFaturalari.Count.ToString() });
            data.Add(new[] { "Kasa Çıkışları", kasaCikislari.ToString("C2"), kasaHareketler.Count(k => k.Cikan > 0).ToString() });
            data.Add(new[] { "Toplam Giderler", (faturaGiderleri + kasaCikislari).ToString("C2"), "-" });
        }
        else if (item.Title == "Banka/Kasa Nakit Durumu")
        {
            var bankalar = await _uow.Bankalar.GetAllAsync();
            headers = new[] { "Hesap Adı", "Tür", "Bakiye", "Döviz" };
            
            foreach (var b in bankalar.OrderByDescending(b => b.GuncelBakiye))
                data.Add(new[] { b.BankaAdi ?? "", b.KartTuru ?? "Kasa", b.GuncelBakiye.ToString("C2"), b.DovizTuru ?? "TL" });
            
            data.Add(new[] { "TOPLAM NAKİT", "", bankalar.Sum(b => b.GuncelBakiye).ToString("C2"), "TL" });
        }
        else if (item.Title == "Müşteri ABC Analizi")
        {
            var faturalar = await _uow.Faturalar.GetAllAsync();
            var satisFaturalari = faturalar.Where(f => f.Tur == "Satış" || f.Tur == "Satis").ToList();
            headers = new[] { "Müşteri", "Toplam Satış", "Yüzde", "Sınıf" };
            
            var grouped = satisFaturalari.GroupBy(f => f.CariUnvan ?? "Bilinmeyen")
                .Select(g => new { Musteri = g.Key, Total = g.Sum(f => f.GenelToplam) })
                .OrderByDescending(x => x.Total)
                .ToList();
            
            var grandTotal = grouped.Sum(x => x.Total);
            decimal cumulative = 0;
            foreach (var abcItem in grouped)
            {
                cumulative += abcItem.Total;
                var percentage = grandTotal > 0 ? (abcItem.Total / grandTotal * 100) : 0;
                var cumulativePercentage = grandTotal > 0 ? (cumulative / grandTotal * 100) : 0;
                var classification = cumulativePercentage <= 80 ? "A" : cumulativePercentage <= 95 ? "B" : "C";
                data.Add(new[] { abcItem.Musteri, abcItem.Total.ToString("C2"), percentage.ToString("N2"), classification });
            }
            
            data.Add(new[] { "", "", "", "" });
            data.Add(new[] { "A Sınıfı: %80 Ciro", "B Sınıfı: %15 Ciro", "C Sınıfı: %5 Ciro", "" });
        }
        else if (item.Title == "Kar-Zarar Mukayesesi")
        {
            var faturalar = await _uow.Faturalar.GetAllAsync();
            headers = new[] { "Dönem", "Satış Tutarı", "Alış Tutarı", "Net Kar/Zarar" };
            
            // Yıllık
            data.Add(new[] { "--- YILLIK ANALİZ ---", "", "", "" });
            for (int i = 4; i >= 0; i--) // Son 5 yıl
            {
                int year = DateTime.Now.Year - i;
                var s = faturalar.Where(f => (f.Tur == "Satış" || f.Tur == "Satis") && f.Tarih.Year == year).Sum(f => f.GenelToplam);
                var a = faturalar.Where(f => (f.Tur == "Alış" || f.Tur == "Alis") && f.Tarih.Year == year).Sum(f => f.GenelToplam);
                data.Add(new[] { year.ToString(), s.ToString("C2"), a.ToString("C2"), (s - a).ToString("C2") });
            }

            // Aylık
            data.Add(new[] { "--- AYLIK ANALİZ (SON 6 AY) ---", "", "", "" });
            for (int i = 5; i >= 0; i--)
            {
                var date = DateTime.Now.AddMonths(-i);
                var s = faturalar.Where(f => (f.Tur == "Satış" || f.Tur == "Satis") && f.Tarih.Year == date.Year && f.Tarih.Month == date.Month).Sum(f => f.GenelToplam);
                var a = faturalar.Where(f => (f.Tur == "Alış" || f.Tur == "Alis") && f.Tarih.Year == date.Year && f.Tarih.Month == date.Month).Sum(f => f.GenelToplam);
                data.Add(new[] { date.ToString("MM/yyyy"), s.ToString("C2"), a.ToString("C2"), (s - a).ToString("C2") });
            }

            // Haftalık
            data.Add(new[] { "--- HAFTALIK ANALİZ (SON 4 HAFTA) ---", "", "", "" });
            for (int i = 3; i >= 0; i--)
            {
                var start = DateTime.Now.AddDays(-((double)i * 7 + 7));
                var end = DateTime.Now.AddDays(-((double)i * 7));
                var s = faturalar.Where(f => (f.Tur == "Satış" || f.Tur == "Satis") && f.Tarih >= start && f.Tarih < end).Sum(f => f.GenelToplam);
                var a = faturalar.Where(f => (f.Tur == "Alış" || f.Tur == "Alis") && f.Tarih >= start && f.Tarih < end).Sum(f => f.GenelToplam);
                data.Add(new[] { $"{start:dd.MM} - {end:dd.MM}", s.ToString("C2"), a.ToString("C2"), (s - a).ToString("C2") });
            }
        }
        else if (item.Title == "Vadesi Geçmiş Alacaklar")
        {
            var cariler = await _uow.Cariler.GetAllAsync();
            var hareketler = await _uow.Cariler.GetAllHareketlerAsync();
            headers = new[] { "Müşteri", "Cari Bakiyesi", "Ort. Gecikme Günü", "Fatura / Evrak No", "Fatura Bakiyesi", "Gecikme (Gün)" };
            
            var today = DateTime.Now;
            foreach (var c in cariler.Where(x => x.Bakiye > 0))
            {
                var cHareketler = hareketler.Where(h => h.CariId == c.Id).OrderBy(h => h.Tarih).ToList();
                var toplamAlacak = cHareketler.Sum(h => h.Alacak);
                var borcHareketleri = cHareketler.Where(h => h.Borc > 0).OrderBy(h => h.Vade ?? h.Tarih).ToList();
                
                var odenmemisFaturalar = new List<(CariHareket Hareket, decimal KalanBakiye)>();
                decimal harcananAlacak = toplamAlacak;
                
                foreach (var b in borcHareketleri)
                {
                    if (harcananAlacak >= b.Borc)
                    {
                        harcananAlacak -= b.Borc;
                    }
                    else
                    {
                        decimal kalanBakiye = b.Borc - harcananAlacak;
                        harcananAlacak = 0;
                        odenmemisFaturalar.Add((b, kalanBakiye));
                    }
                }
                
                var vadesiGecmisler = odenmemisFaturalar
                    .Where(x => x.Hareket.Vade.HasValue && x.Hareket.Vade.Value.Date < today.Date)
                    .ToList();
                
                if (!vadesiGecmisler.Any()) continue;
                
                decimal toplamGecikenTutar = vadesiGecmisler.Sum(x => x.KalanBakiye);
                decimal toplamAgirlikliGun = 0;
                
                foreach (var vf in vadesiGecmisler)
                {
                    var gun = (today - vf.Hareket.Vade!.Value).Days;
                    toplamAgirlikliGun += vf.KalanBakiye * gun;
                }
                
                decimal ortalamaGun = toplamGecikenTutar > 0 ? toplamAgirlikliGun / toplamGecikenTutar : 0;
                
                bool isFirst = true;
                foreach (var vf in vadesiGecmisler)
                {
                    var gun = (today - vf.Hareket.Vade!.Value).Days;
                    data.Add(new[] { 
                        isFirst ? (c.Unvan ?? "") : "", 
                        isFirst ? c.Bakiye.ToString("C2") : "", 
                        isFirst ? ortalamaGun.ToString("N0") + " Gün" : "", 
                        vf.Hareket.EvrakNo ?? vf.Hareket.IslemTuru ?? "Fatura", 
                        vf.KalanBakiye.ToString("C2"), 
                        gun.ToString() + " Gün" 
                    });
                    isFirst = false;
                }
            }
        }
        else if (item.Title == "Bölgesel Satış Analizi")
        {
            var faturalar = await _uow.Faturalar.GetAllAsync();
            var cariler = await _uow.Cariler.GetAllAsync();
            headers = new[] { "Bölge", "Satış Tutarı", "Fatura Sayısı", "Ort. Fatura" };
            
            var satisFaturalari = faturalar.Where(f => f.Tur == "Satış" || f.Tur == "Satis").ToList();
            var grouped = satisFaturalari.GroupBy(f => {
                var cari = cariler.FirstOrDefault(c => c.Id == f.CariId);
                return cari?.Il ?? "Bilinmiyor";
            });
            
            foreach (var g in grouped.OrderByDescending(g => g.Sum(f => f.GenelToplam)))
            {
                var total = g.Sum(f => f.GenelToplam);
                var count = g.Count();
                var avg = count > 0 ? total / count : 0;
                data.Add(new[] { g.Key, total.ToString("C2"), count.ToString(), avg.ToString("C2") });
            }
        }
        else if (item.Title == "Müşteri Kayıp (Churn)")
        {
            var cariler = await _uow.Cariler.GetAllAsync();
            var hareketler = await _uow.Cariler.GetAllHareketlerAsync();
            headers = new[] { "Müşteri", "Son İşlem", "Geçen Gün", "Kayıp Riski" };
            
            var cutoffDate = DateTime.Now.AddDays(-30);
            var musteriler = cariler.Where(x => x.Tur != "Satici" && x.Tur != "Satıcı");
            
            foreach (var c in musteriler)
            {
                var lastTransaction = hareketler.Where(h => h.CariId == c.Id).OrderByDescending(h => h.Tarih).FirstOrDefault();
                var compareDate = lastTransaction?.Tarih ?? c.KayitTarihi;
                
                if (compareDate < cutoffDate)
                {
                    var daysSince = (DateTime.Now - compareDate).Days;
                    var risk = daysSince > 180 ? "Kritik Kayıp (180+ Gün)" : daysSince > 90 ? "Yüksek Risk (90+ Gün)" : daysSince > 30 ? "Orta Risk (30+ Gün)" : "Düşük Risk";
                    data.Add(new[] { 
                        c.Unvan ?? "", 
                        lastTransaction?.Tarih.ToString("dd.MM.yyyy") ?? "İşlem Yok (Kayıt: " + c.KayitTarihi.ToString("dd.MM.yyyy") + ")", 
                        daysSince.ToString(), 
                        risk 
                    });
                }
            }
        }
        else if (item.Title == "Fiyat Dalgalanma Raporu")
        {
            var stokHareketleri = await _uow.Stoklar.GetAllHareketlerAsync();
            headers = new[] { "Ürün", "Min Fiyat", "Max Fiyat", "Ort Fiyat", "Fark %" };
            
            // User requested: Base it on Stock Movements originating from Invoices (Alış Faturası)
            var pulseData = stokHareketleri.Where(h => h.Fiyat > 0 && (h.IslemTuru == "Alış Faturası" || h.IslemTuru == "GİRİŞ"));
            
            var grouped = pulseData.GroupBy(h => h.StokAdi);
            foreach (var g in grouped)
            {
                var min = g.Min(h => h.Fiyat);
                var max = g.Max(h => h.Fiyat);
                var avg = g.Average(h => h.Fiyat);
                var variance = min > 0 ? ((max - min) / min * 100) : 0;
                
                data.Add(new[] { g.Key ?? "", min.ToString("C2"), max.ToString("C2"), avg.ToString("C2"), variance.ToString("N2") });
            }
        }
        else if (item.Title == "Müşteri Sadakat (LTV)")
        {
            var faturalar = await _uow.Faturalar.GetAllAsync();
            var cariler = await _uow.Cariler.GetAllAsync();
            headers = new[] { "Müşteri", "İlk Alışveriş", "Son Alışveriş", "Toplam Harcama", "Ortalama Sepet" };
            
            var satisFaturalari = faturalar.Where(f => f.Tur == "Satış" || f.Tur == "Satis").ToList();
            var grouped = satisFaturalari.GroupBy(f => f.CariId);
            
            foreach (var g in grouped.OrderByDescending(g => g.Sum(f => f.GenelToplam)).Take(100))
            {
                var cari = cariler.FirstOrDefault(c => c.Id == g.Key);
                var firstPurchase = g.Min(f => f.Tarih);
                var lastPurchase = g.Max(f => f.Tarih);
                var totalSpent = g.Sum(f => f.GenelToplam);
                var avgBasket = g.Average(f => f.GenelToplam);
                data.Add(new[] { cari?.Unvan ?? "Bilinmeyen", firstPurchase.ToString("dd.MM.yyyy"), lastPurchase.ToString("dd.MM.yyyy"), totalSpent.ToString("C2"), avgBasket.ToString("C2") });
            }
        }
        else if (item.Title == "Finansal Isı Haritası")
        {
            var faturalar = await _uow.Faturalar.GetAllAsync();
            var kasaHareketler = await _uow.Kasalar.GetAllHareketlerAsync();
            headers = new[] { "Tarih", "Satış", "Alış", "Kasa Giren", "Kasa Çıkan", "Net Akış" };
            
            var last30Days = Enumerable.Range(0, 30).Select(i => DateTime.Now.AddDays(-i).Date).OrderBy(d => d).ToList();
            foreach (var date in last30Days)
            {
                var satis = faturalar.Where(f => (f.Tur == "Satış" || f.Tur == "Satis") && f.Tarih.Date == date).Sum(f => f.GenelToplam);
                var alis = faturalar.Where(f => (f.Tur == "Alış" || f.Tur == "Alis") && f.Tarih.Date == date).Sum(f => f.GenelToplam);
                var kasaIn = kasaHareketler.Where(k => k.Tarih.Date == date).Sum(k => k.Giren);
                var kasaOut = kasaHareketler.Where(k => k.Tarih.Date == date).Sum(k => k.Cikan);
                var netFlow = satis - alis + kasaIn - kasaOut;
                data.Add(new[] { date.ToString("dd.MM.yyyy"), satis.ToString("C2"), alis.ToString("C2"), kasaIn.ToString("C2"), kasaOut.ToString("C2"), netFlow.ToString("C2") });
            }
        }
        else if (item.Title == "Tahsilat Süresi (DSO)")
        {
            var cariler = await _uow.Cariler.GetAllAsync();
            var hareketler = await _uow.Cariler.GetAllHareketlerAsync();
            var faturalar = await _uow.Faturalar.GetAllAsync();
            headers = new[] { "Müşteri", "Ortalama Tahsilat Süresi (Gün)", "Toplam Alacak", "Durum" };
            
            foreach (var c in cariler.Where(x => x.Grup == "Müşteri" && (x.Borc - x.Alacak) > 0))
            {
                var cHareketler = hareketler.Where(h => h.CariId == c.Id).ToList();
                if (cHareketler.Any())
                {
                    var avgDays = cHareketler.Where(h => h.Vade.HasValue).Any() ? cHareketler.Where(h => h.Vade.HasValue).Average(h => (h.Vade!.Value - h.Tarih).Days) : 0;
                    var totalReceivable = c.Borc - c.Alacak;
                    var status = avgDays > 60 ? "Riskli" : avgDays > 30 ? "Normal" : "İyi";
                    data.Add(new[] { c.Unvan ?? "", avgDays.ToString("N0"), totalReceivable.ToString("C2"), status });
                }
            }
        }
        else
        {
            headers = new[] { "Bilgi", "Detay" };
            data.Add(new[] { item.Title, "Bu rapor için veriler hazırlanıyor..." });
        }

        return (title, headers, data);
    }

    /// <summary>
    /// Her rapor türüne uygun grafik verisini section'a bağlar.
    /// </summary>
    private void AttachChartData(ReportSection section, string reportTitle, List<string[]> rows)
    {
        try
        {
            switch (reportTitle)
            {
                case "Aylık Tahsilat ve Ödeme Analizi":
                    {
                        decimal nAl = 0, nYon = 0, kkAl = 0, kkYon = 0, efAl = 0, efYon = 0, cAl = 0, cYon = 0;
                        foreach (var row in rows)
                        {
                            nAl += ParseMoney(row[1]); nYon += ParseMoney(row[2]);
                            kkAl += ParseMoney(row[3]); kkYon += ParseMoney(row[4]);
                            efAl += ParseMoney(row[5]); efYon += ParseMoney(row[6]);
                            cAl += ParseMoney(row[7]); cYon += ParseMoney(row[8]);
                        }

                        // Summary Boxes
                        section.ChartData = new List<ChartDataItem>
                        {
                            new() { Label = "Nakit", Actual = nAl },
                            new() { Label = "K.Kartı", Actual = kkAl },
                            new() { Label = "Havale", Actual = efAl },
                            new() { Label = "Çek", Actual = cAl }
                        };

                        section.ExtraCharts = new List<ReportChart>
                        {
                            new() { 
                                Title = "Tahsilat Dağılımı (Alınan)", 
                                SingleData = new() { ("Nakit", nAl), ("K.Kartı", kkAl), ("Havale", efAl), ("Çek", cAl) },
                                Color = "#10b981"
                            },
                            new() { 
                                Title = "Ödeme Dağılımı (Alınan)", 
                                SingleData = new() { ("Nakit", 0), ("K.Kartı", 0), ("Havale", 0), ("Çek", 0) }, // Placeholder if not explicitly tracked
                                Color = "#3b82f6"
                            },
                            new() { 
                                Title = "Tahsilat Dağılımı (Yönlendirilen)", 
                                SingleData = new() { ("Nakit", 0), ("K.Kartı", kkYon), ("Havale", efYon), ("Çek", cYon) },
                                Color = "#f59e0b"
                            },
                            new() { 
                                Title = "Ödeme Dağılımı (Yönlendirilen)", 
                                SingleData = new() { ("Nakit (Çıkan)", nYon), ("K.Kartı", 0), ("Havale", 0), ("Çek", 0) },
                                Color = "#ef4444"
                            }
                        };
                    }
                    break;

                case "Kredi Kartı Detay Raporu":
                    {
                        decimal alinan = 0, yonlendirilmesi = 0;
                        foreach (var row in rows)
                        {
                            var tutar = ParseMoney(row[4]);
                            alinan += tutar;
                            if (row[3] != "-" && !string.IsNullOrEmpty(row[3]))
                                yonlendirilmesi += tutar;
                        }

                        section.LeftChartTitle = "KREDİ KARTI PERFORMANSI";
                        section.ActualLabel = "Tahsil Edilen";
                        section.TargetLabel = "Yönlendirilen";
                        section.ChartData = new List<ChartDataItem>
                        {
                            new() { Label = "Kredi Kartı İşlemleri", Actual = alinan, Target = yonlendirilmesi }
                        };
                    }
                    break;

                case "Nakit İşlem Detay Raporu":
                    {
                        decimal giren = 0, cikan = 0;
                        foreach (var row in rows)
                        {
                            if (row.Length >= 6)
                            {
                                giren += ParseMoney(row[4]);
                                cikan += ParseMoney(row[5]);
                            }
                        }
                        section.LeftChartTitle = "KASA NAKİT DENGESİ";
                        section.ActualLabel = "Nakit Girişi";
                        section.TargetLabel = "Nakit Çıkışı";
                        section.ChartData = new List<ChartDataItem>
                        {
                            new() { Label = "Nakit Hareket", Actual = giren, Target = cikan }
                        };
                    }
                    break;

                case "Çek Detay Raporu":
                    {
                        decimal alinan = 0, ciroEdilen = 0;
                        foreach(var row in rows)
                        {
                            var tutar = ParseMoney(row[4]);
                            alinan += tutar;
                            if (row[5].Contains("Ciro") || row[5].Contains("Tedarikçi"))
                                ciroEdilen += tutar;
                        }
                        section.LeftChartTitle = "ÇEK PORTFÖY ANALİZİ";
                        section.ActualLabel = "Alınan Çekler";
                        section.TargetLabel = "Ciro Edilen";
                        section.ChartData = new List<ChartDataItem>
                        {
                            new() { Label = "Müşteri Çekleri", Actual = alinan, Target = ciroEdilen }
                        };
                    }
                    break;

                case "Havale / EFT Detay Raporu":
                    {
                        decimal alinan = 0, yonlendirilmesi = 0;
                        foreach (var row in rows)
                        {
                            var tutar = ParseMoney(row[4]);
                            alinan += tutar;
                            if (row[5].Contains("Gönderildi") || row[5].Contains("Ödeme"))
                                yonlendirilmesi += tutar;
                        }
                        section.LeftChartTitle = "BANKA EFT/HAVALE ANALİZİ";
                        section.ActualLabel = "Tahsilat (Gelen)";
                        section.TargetLabel = "Ödeme (Giden)";
                        section.ChartData = new List<ChartDataItem>
                        {
                            new() { Label = "Banka İşlemleri", Actual = alinan, Target = yonlendirilmesi }
                        };
                    }
                    break;

                case "Yaşlandırma Raporu":
                    {
                        decimal t0 = 0, t1 = 0, t2 = 0, t3 = 0;
                        foreach (var row in rows)
                        {
                            if (row.Length >= 6)
                            {
                                t0 += ParseMoney(row[1]);
                                t1 += ParseMoney(row[2]);
                                t2 += ParseMoney(row[3]);
                                t3 += ParseMoney(row[4]);
                            }
                        }
                        section.SingleBarData = new List<(string, decimal)>
                        {
                            ("0-30 Gün", t0), ("31-60 Gün", t1), ("61-90 Gün", t2), ("90+ Gün", t3)
                        };
                        section.SingleBarColor = "#ef4444"; // Kırmızı ton - alacak yaşlandırma
                    }
                    break;

                case "En Çok Satan Ürünler":
                    {
                        var items = new List<(string, decimal)>();
                        foreach (var row in rows.Take(10))
                        {
                            if (row.Length >= 3)
                            {
                                decimal.TryParse(row[2].Replace(",", "").Trim(), out var qty);
                                items.Add((TruncStr(row[1], 20), qty));
                            }
                        }
                        if (items.Any())
                        {
                            section.HorizontalBarData = items;
                            section.HorizontalBarColor = "#10b981"; // Yeşil
                        }
                    }
                    break;

                case "Bölge Bazlı Satış":
                case "Bölgesel Satış Analizi":
                    {
                        var items = new List<(string, decimal)>();
                        foreach (var row in rows.Take(10))
                        {
                            if (row.Length >= 3)
                            {
                                items.Add((TruncStr(row[0], 16), ParseMoney(row[2])));
                            }
                            else if (row.Length >= 2)
                            {
                                items.Add((TruncStr(row[0], 16), ParseMoney(row[1])));
                            }
                        }
                        if (items.Any())
                        {
                            section.HorizontalBarData = items;
                            section.HorizontalBarColor = "#6366f1"; // İndigo
                        }
                    }
                    break;

                case "Müşteri ABC Analizi":
                    {
                        var aCount = rows.Count(r => r.Length >= 4 && r[3] == "A");
                        var bCount = rows.Count(r => r.Length >= 4 && r[3] == "B");
                        var cCount = rows.Count(r => r.Length >= 4 && r[3] == "C");
                        section.SingleBarData = new List<(string, decimal)>
                        {
                            ("A Sınıfı", aCount), ("B Sınıfı", bCount), ("C Sınıfı", cCount)
                        };
                        section.SingleBarColor = "#f59e0b"; // Amber
                        section.FooterNote = "ABC ANALİZİ: 'A' grubu cironuzun %80'ini sağlayan en değerli müşterilerinizi, 'B' grubu sonraki %15'i, 'C' grubu ise kalan %5'lik dilimi temsil eder. Stratejik odaklanma için 'A' grubu müşterilere özel ilgi gösterilmesi önerilir.";
                    }
                    break;

                case "Gelir Tablosu":
                    {
                        var items = new List<(string, decimal)>();
                        foreach (var row in rows.Take(8))
                        {
                            if (row.Length >= 2)
                            {
                                var v = ParseMoney(row[1]);
                                if (v != 0) items.Add((TruncStr(row[0], 22), Math.Abs(v)));
                            }
                        }
                        if (items.Any())
                        {
                            section.SingleBarData = items;
                            section.SingleBarColor = "#059669"; // Emerald
                        }
                    }
                    break;

                case "Kapsamlı Gider Raporu":
                    {
                        var items = new List<(string, decimal)>();
                        foreach (var row in rows.Take(8))
                        {
                            if (row.Length >= 2)
                            {
                                var v = ParseMoney(row[1]);
                                if (v != 0) items.Add((TruncStr(row[0], 22), Math.Abs(v)));
                            }
                        }
                        if (items.Any())
                        {
                            section.SingleBarData = items;
                            section.SingleBarColor = "#dc2626"; // Kırmızı
                        }
                    }
                    break;

                case "Kar-Zarar Mukayesesi":
                    {
                        var yıllık = new List<ChartDataItem>();
                        var aylık = new List<ChartDataItem>();
                        var haftalık = new List<ChartDataItem>();
                        
                        string currentGroup = "";
                        foreach (var row in rows)
                        {
                            if (row[0].Contains("---")) { currentGroup = row[0]; continue; }
                            if (row.Length < 4) continue;

                            var cdi = new ChartDataItem { Label = row[0], Actual = ParseMoney(row[1]), Target = ParseMoney(row[2]) };
                            
                            string group = currentGroup.ToUpper(System.Globalization.CultureInfo.InvariantCulture);
                            if (group.Contains("YILLIK")) yıllık.Add(cdi);
                            else if (group.Contains("AYLIK")) aylık.Add(cdi);
                            else if (group.Contains("HAFTALIK")) haftalık.Add(cdi);
                        }

                        section.ExtraCharts = new List<ReportChart>();
                        if (yıllık.Any(x => x.Actual > 0 || x.Target > 0)) 
                            section.ExtraCharts.Add(new ReportChart { Title = "YILLIK KAR-ZARAR KIYASLAMASI (SATIŞ VS ALIŞ)", BarData = yıllık, ActualLabel = "Satış", TargetLabel = "Alış", Color = "#3b82f6" });
                        
                        if (aylık.Any(x => x.Actual > 0 || x.Target > 0)) 
                            section.ExtraCharts.Add(new ReportChart { Title = "AYLIK KAR-ZARAR KIYASLAMASI (SON 6 AY)", BarData = aylık, ActualLabel = "Satış", TargetLabel = "Alış", Color = "#10b981" });
                        
                        if (haftalık.Any(x => x.Actual > 0 || x.Target > 0)) 
                            section.ExtraCharts.Add(new ReportChart { Title = "HAFTALIK KAR-ZARAR KIYASLAMASI (SON 4 HAFTA)", BarData = haftalık, ActualLabel = "Satış", TargetLabel = "Alış", Color = "#f59e0b" });
                        
                        section.KeyMetrics = aylık.OrderByDescending(x => x.Actual).Take(1).Concat(yıllık.OrderByDescending(x => x.Actual).Take(1)).ToList();
                    }
                    break;

                case "Müşteri Karlılık Analizi":
                    {
                        var items = new List<(string, decimal)>();
                        foreach (var row in rows.Take(10))
                        {
                            if (row.Length >= 3)
                            {
                                items.Add((TruncStr(row[0], 18), ParseMoney(row[row.Length - 1])));
                            }
                        }
                        if (items.Any())
                        {
                            section.HorizontalBarData = items;
                            section.HorizontalBarColor = "#8b5cf6"; // Mor
                        }
                    }
                    break;

                case "Ürün Karlılık Raporu":
                    {
                        var items = new List<(string, decimal)>();
                        foreach (var row in rows.Take(10))
                        {
                            if (row.Length >= 3)
                            {
                                items.Add((TruncStr(row[0], 18), ParseMoney(row[row.Length - 1])));
                            }
                        }
                        if (items.Any())
                        {
                            section.HorizontalBarData = items;
                            section.HorizontalBarColor = "#0891b2"; // Cyan
                        }
                    }
                    break;

                case "Stok Mevcudu":
                    {
                        var items = new List<(string, decimal)>();
                        foreach (var row in rows.Take(10))
                        {
                            if (row.Length >= 3)
                            {
                                items.Add((TruncStr(row[0], 18), ParseMoney(row[2])));
                            }
                        }
                        if (items.Any())
                        {
                            section.HorizontalBarData = items;
                            section.HorizontalBarColor = "#2563eb"; // Mavi
                        }
                    }
                    break;

                case "Vadesi Geçmiş Alacaklar":
                    {
                        var items = new List<(string, decimal)>();
                        foreach (var row in rows.Take(10))
                        {
                            if (row.Length >= 3)
                            {
                                items.Add((TruncStr(row[0], 18), ParseMoney(row[1])));
                            }
                        }
                        if (items.Any())
                        {
                            section.HorizontalBarData = items;
                            section.HorizontalBarColor = "#e11d48"; // Rose
                        }
                    }
                    break;
            }
        }
        catch { }
    }

    private static decimal ParseMoney(string? s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        
        // Remove known currency symbols and clean spaces
        string clean = s.Replace("₺", "").Replace("TL", "").Replace("$", "").Replace("€", "").Trim();
        
        if (decimal.TryParse(clean, System.Globalization.NumberStyles.Currency, System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), out var v1))
            return v1;
            
        if (decimal.TryParse(clean, System.Globalization.NumberStyles.Currency, System.Globalization.CultureInfo.InvariantCulture, out var v2))
            return v2;

        // Manual cleaning for tricky formats (e.g. "1.234,56")
        try 
        {
            string manual = clean;
            if (manual.Contains(".") && manual.Contains(",")) // Turkish style 1.234,56
                manual = manual.Replace(".", "").Replace(",", ".");
            else if (manual.Contains(",") && !manual.Contains(".")) // Just comma as decimal? 1234,56
                manual = manual.Replace(",", ".");
                
            if (decimal.TryParse(manual, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v3))
                return v3;
        }
        catch { }

        return 0;
    }

    private static string TruncStr(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length > max ? s.Substring(0, max - 1) + "…" : s;
    }

    protected async Task<List<ChartDataItem>> GetChartDataForReportAsync(ReportItemViewModel reportItem, List<string[]> rows)
    {
        var chartData = new List<ChartDataItem>();
        
        try
        {
            if (reportItem.Title == "Yaşlandırma Raporu" && rows.Count > 0)
            {
                // Aggregate aging data for chart
                decimal total0_30 = 0, total31_60 = 0, total61_90 = 0, total90plus = 0;
                foreach (var row in rows.Take(20)) // Top 20 customers
                {
                    if (row.Length >= 6)
                    {
                        decimal.TryParse(row[1].Replace("₺", "").Replace(",", "").Trim(), out var d0_30);
                        decimal.TryParse(row[2].Replace("₺", "").Replace(",", "").Trim(), out var d31_60);
                        decimal.TryParse(row[3].Replace("₺", "").Replace(",", "").Trim(), out var d61_90);
                        decimal.TryParse(row[4].Replace("₺", "").Replace(",", "").Trim(), out var d90plus);
                        total0_30 += d0_30; total31_60 += d31_60; total61_90 += d61_90; total90plus += d90plus;
                    }
                }
                chartData.Add(new ChartDataItem { Label = "0-30 Gün", Target = total0_30, Actual = 0 });
                chartData.Add(new ChartDataItem { Label = "31-60 Gün", Target = total31_60, Actual = 0 });
                chartData.Add(new ChartDataItem { Label = "61-90 Gün", Target = total61_90, Actual = 0 });
                chartData.Add(new ChartDataItem { Label = "90+ Gün", Target = total90plus, Actual = 0 });
            }
            else if (reportItem.Title == "Gelir Tablosu" && rows.Count > 0)
            {
                // Income statement breakdown
                foreach (var row in rows)
                {
                    if (row.Length >= 2)
                    {
                        decimal.TryParse(row[1].Replace("₺", "").Replace(",", "").Trim(), out var amount);
                        chartData.Add(new ChartDataItem { Label = row[0], Target = amount, Actual = 0 });
                    }
                }
            }
            else if (reportItem.Title == "Kapsamlı Gider Raporu" && rows.Count > 0)
            {
                // Expense breakdown pie chart
                foreach (var row in rows)
                {
                    if (row.Length >= 2)
                    {
                        decimal.TryParse(row[1].Replace("₺", "").Replace(",", "").Trim(), out var amount);
                        chartData.Add(new ChartDataItem { Label = row[0], Target = amount, Actual = 0 });
                    }
                }
            }
            else if (reportItem.Title == "Müşteri ABC Analizi" && rows.Count > 0)
            {
                // ABC classification chart - top 20
                var aCount = rows.Count(r => r.Length >= 4 && r[3] == "A");
                var bCount = rows.Count(r => r.Length >= 4 && r[3] == "B");
                var cCount = rows.Count(r => r.Length >= 4 && r[3] == "C");
                chartData.Add(new ChartDataItem { Label = "A Sınıfı", Target = aCount, Actual = 0 });
                chartData.Add(new ChartDataItem { Label = "B Sınıfı", Target = bCount, Actual = 0 });
                chartData.Add(new ChartDataItem { Label = "C Sınıfı", Target = cCount, Actual = 0 });
            }
            else if (reportItem.Title == "Kar-Zarar Mukayesesi" && rows.Count > 0)
            {
                // Trend mukayesesi (Başlık satırlarını atla: ---)
                foreach (var row in rows.Where(r => r.Length >= 4 && !r[0].StartsWith("---")))
                {
                    chartData.Add(new ChartDataItem 
                    { 
                        Label = row[0], 
                        Target = ParseMoney(row[2]), // Maliyet/Alış
                        Actual = ParseMoney(row[1])  // Satış
                    });
                }
            }
            else if (reportItem.Title == "Bölgesel Satış Analizi" && rows.Count > 0)
            {
                // Top 10 regions by sales
                foreach (var row in rows.Take(10))
                {
                    if (row.Length >= 2)
                    {
                        decimal.TryParse(row[1].Replace("₺", "").Replace(",", "").Trim(), out var sales);
                        chartData.Add(new ChartDataItem { Label = row[0], Target = sales, Actual = 0 });
                    }
                }
            }
            else if (reportItem.Title == "Finansal Isı Haritası" && rows.Count > 0)
            {
                // Last 30 days net flow trend
                foreach (var row in rows)
                {
                    if (row.Length >= 6)
                    {
                        decimal.TryParse(row[5].Replace("₺", "").Replace(",", "").Trim(), out var netFlow);
                        chartData.Add(new ChartDataItem { Label = row[0], Target = 0, Actual = netFlow });
                    }
                }
            }
            else if (reportItem.Title == "En Çok Satan Ürünler" && rows.Count > 0)
            {
                // Top 10 products
                foreach (var row in rows.Take(10))
                {
                    if (row.Length >= 3)
                    {
                        decimal.TryParse(row[2].Replace(",", "").Trim(), out var qty);
                        chartData.Add(new ChartDataItem { Label = row[1].Length > 20 ? row[1].Substring(0, 20) : row[1], Target = qty, Actual = 0 });
                    }
                }
            }
            else if (reportItem.Title == "Bölge Bazlı Satış" && rows.Count > 0)
            {
                // Regional sales distribution - top 10
                foreach (var row in rows.Take(10))
                {
                    if (row.Length >= 3)
                    {
                        decimal.TryParse(row[2].Replace("₺", "").Replace(",", "").Trim(), out var sales);
                        chartData.Add(new ChartDataItem { Label = row[0], Target = sales, Actual = 0 });
                    }
                }
            }
            else if (reportItem.Title == "Kredi Kartı Detay Raporu" && rows.Count > 0)
            {
                decimal alinan = 0, yon = 0;
                foreach (var row in rows)
                {
                    decimal t = ParseMoney(row[4]);
                    alinan += t;
                    if (row[3] != "-" && !string.IsNullOrEmpty(row[3])) yon += t;
                }
                chartData.Add(new ChartDataItem { Label = "Kredi Kartı", Actual = alinan, Target = yon });
            }
            else if (reportItem.Title == "Nakit İşlem Detay Raporu" && rows.Count > 0)
            {
                decimal giren = rows.Sum(r => ParseMoney(r[4]));
                decimal cikan = rows.Sum(r => ParseMoney(r[5]));
                chartData.Add(new ChartDataItem { Label = "Kasa Nakit", Actual = giren, Target = cikan });
            }
            else if (reportItem.Title == "Çek Detay Raporu" && rows.Count > 0)
            {
                decimal alinan = 0, ciro = 0;
                foreach (var row in rows)
                {
                    decimal t = ParseMoney(row[4]);
                    alinan += t;
                    if (row[5].Contains("Ciro") || row[5].Contains("Tedarikçi")) ciro += t;
                }
                chartData.Add(new ChartDataItem { Label = "Müşteri Çekleri", Actual = alinan, Target = ciro });
            }
            else if (reportItem.Title == "Havale / EFT Detay Raporu" && rows.Count > 0)
            {
                decimal alinan = 0, yon = 0;
                foreach (var row in rows)
                {
                    decimal t = ParseMoney(row[4]);
                    alinan += t;
                    if (row[5].Contains("Gönderildi") || row[5].Contains("Ödeme")) yon += t;
                }
                chartData.Add(new ChartDataItem { Label = "Banka İşlemleri", Actual = alinan, Target = yon });
            }
            else if (reportItem.Title == "Aylık Tahsilat ve Ödeme Analizi" && rows.Count > 0)
            {
                decimal nAl = 0, nYon = 0, kkAl = 0, kkYon = 0, efAl = 0, efYon = 0, cAl = 0, cYon = 0;
                foreach (var row in rows)
                {
                    nAl += ParseMoney(row[1]); nYon += ParseMoney(row[2]);
                    kkAl += ParseMoney(row[3]); kkYon += ParseMoney(row[4]);
                    efAl += ParseMoney(row[5]); efYon += ParseMoney(row[6]);
                    cAl += ParseMoney(row[7]); cYon += ParseMoney(row[8]);
                }

                chartData.Add(new ChartDataItem { Label = "Nakit", Actual = nAl, Target = nYon });
                chartData.Add(new ChartDataItem { Label = "K.Kartı", Actual = kkAl, Target = kkYon });
                chartData.Add(new ChartDataItem { Label = "Havale", Actual = efAl, Target = efYon });
                chartData.Add(new ChartDataItem { Label = "Çek", Actual = cAl, Target = cYon });
            }
        }
        catch { }
        
        await Task.CompletedTask;
        return chartData;
    }

    protected override Task InvokeOnUIThreadAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    private static bool IsNakitMovement(KasaHareket x)
    {
        if (string.IsNullOrEmpty(x.IslemTuru)) return true;
        var t = x.IslemTuru;
        return !t.Contains("KK", StringComparison.OrdinalIgnoreCase) &&
               !t.Contains("EFT", StringComparison.OrdinalIgnoreCase) &&
               !t.Contains("Çek", StringComparison.OrdinalIgnoreCase) &&
               !t.Contains("K.Kart", StringComparison.OrdinalIgnoreCase) &&
               !t.Contains("Havale", StringComparison.OrdinalIgnoreCase) &&
               !t.Contains("Banka", StringComparison.OrdinalIgnoreCase);
    }
}

public partial class ReportItemViewModel : ObservableObject
{
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _category = string.Empty;
    [ObservableProperty] private string _icon = string.Empty;
    [ObservableProperty] private string _description = string.Empty;

    public ReportItemViewModel(string title, string category, string icon, string description)
    {
        Title = title;
        Category = category;
        Icon = icon;
        Description = description;
    }
}
