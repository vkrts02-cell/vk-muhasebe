using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Repositories;
using ErmayMuhasebe.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Kernel.Sketches;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ErmayMuhasebe.Avalonia.ViewModels;

public partial class CariDetayViewModel : ErmayMuhasebe.Shared.ViewModels.CariDetayViewModel
{
    [ObservableProperty] private ISeries[] _maturityTimelineSeries = Array.Empty<ISeries>();
    [ObservableProperty] private ICartesianAxis[] _maturityTimelineXAxis = Array.Empty<ICartesianAxis>();
    [ObservableProperty] private ICartesianAxis[] _maturityTimelineYAxis = Array.Empty<ICartesianAxis>();
    [ObservableProperty] private ISeries[] _agingSeries = Array.Empty<ISeries>();
    [ObservableProperty] private SolidColorPaint _legendTextPaint = new() { Color = new SKColor(220, 220, 220) };

    public CariDetayViewModel(IUnitOfWork uow, IPdfService pdfService) 
        : base(uow, pdfService)
    {
    }

    public override void GoBack()
    {
        OnRequestClose();
    }

    public override async Task LoadExtendedDetailsAsync()
    {
        await base.LoadExtendedDetailsAsync();
        
        try
        {
            UpdateMaturityTimelineChart();
            UpdateAgingDonutChart();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating charts in CariDetayViewModel: {ex.Message}");
        }
    }

    private void UpdateMaturityTimelineChart()
    {
        var today = DateTime.Today;
        
        // 5 bins: 
        // 0: 0-7 days
        // 1: 8-14 days
        // 2: 15-21 days
        // 3: 22-30 days
        // 4: 30+ days
        var incomingValues = new decimal[5];
        var outgoingValues = new decimal[5];
        var labels = new string[] { "0-7 Gün", "8-14 Gün", "15-21 Gün", "22-30 Gün", "30+ Gün" };

        // Group future invoices
        foreach (var f in Faturalar.Where(f => f.Kalan > 0 && f.VadeTarihi.Date >= today))
        {
            int days = (f.VadeTarihi.Date - today).Days;
            int binIndex = GetBinIndex(days);

            if (f.IsIncoming)
                incomingValues[binIndex] += f.Kalan;
            else
                outgoingValues[binIndex] += f.Kalan;
        }

        // Group future checks and bills
        foreach (var ev in BekleyenEvraklar.Where(e => e.VadeTarihi.Date >= today))
        {
            int days = (ev.VadeTarihi.Date - today).Days;
            int binIndex = GetBinIndex(days);

            if (ev.Tur == "Alınan")
                incomingValues[binIndex] += ev.Tutar;
            else
                outgoingValues[binIndex] += ev.Tutar;
        }

        var incColor = new SKColor(16, 185, 129); // Emerald Green (#10B981)
        var outColor = new SKColor(239, 68, 68); // Red (#EF4444)

        MaturityTimelineSeries = new ISeries[]
        {
            new ColumnSeries<decimal>
            {
                Name = "Gelecek Tahsilat",
                Values = incomingValues,
                Fill = new SolidColorPaint(incColor),
                MaxBarWidth = 25
            },
            new ColumnSeries<decimal>
            {
                Name = "Gelecek Ödeme",
                Values = outgoingValues,
                Fill = new SolidColorPaint(outColor),
                MaxBarWidth = 25
            }
        };

        MaturityTimelineXAxis = new ICartesianAxis[]
        {
            new Axis
            {
                Labels = labels,
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                TextSize = 11
            }
        };

        MaturityTimelineYAxis = new ICartesianAxis[]
        {
            new Axis
            {
                MinLimit = 0,
                SeparatorsPaint = new SolidColorPaint(SKColors.Gray.WithAlpha(15)) { StrokeThickness = 1 },
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                TextSize = 11
            }
        };
    }

    private int GetBinIndex(int days)
    {
        if (days <= 7) return 0;
        if (days <= 14) return 1;
        if (days <= 21) return 2;
        if (days <= 30) return 3;
        return 4;
    }

    private void UpdateAgingDonutChart()
    {
        var today = DateTime.Today;
        decimal bin1 = 0; // 1-30 days overdue
        decimal bin2 = 0; // 31-60 days overdue
        decimal bin3 = 0; // 60+ days overdue

        int maxDelay1 = 0, maxDelay2 = 0, maxDelay3 = 0;

        // Add overdue invoices
        foreach (var f in Faturalar.Where(f => f.Kalan > 0 && f.VadeTarihi.Date < today))
        {
            int delay = (today - f.VadeTarihi.Date).Days;
            if (delay <= 30) { bin1 += f.Kalan; maxDelay1 = Math.Max(maxDelay1, delay); }
            else if (delay <= 60) { bin2 += f.Kalan; maxDelay2 = Math.Max(maxDelay2, delay); }
            else { bin3 += f.Kalan; maxDelay3 = Math.Max(maxDelay3, delay); }
        }

        // Add overdue checks and bills
        foreach (var ev in BekleyenEvraklar.Where(e => e.VadeTarihi.Date < today))
        {
            int delay = (today - ev.VadeTarihi.Date).Days;
            if (delay <= 30) { bin1 += ev.Tutar; maxDelay1 = Math.Max(maxDelay1, delay); }
            else if (delay <= 60) { bin2 += ev.Tutar; maxDelay2 = Math.Max(maxDelay2, delay); }
            else { bin3 += ev.Tutar; maxDelay3 = Math.Max(maxDelay3, delay); }
        }

        string name1 = maxDelay1 > 0 ? $"0-30 Gün ({maxDelay1} gün)" : "0-30 Gün";
        string name2 = maxDelay2 > 0 ? $"31-60 Gün ({maxDelay2} gün)" : "31-60 Gün";
        string name3 = maxDelay3 > 0 ? $"60+ Gün ({maxDelay3} gün)" : "60+ Gün";

        if (bin1 == 0 && bin2 == 0 && bin3 == 0)
        {
            AgingSeries = new ISeries[]
            {
                new PieSeries<decimal>
                {
                    Name = "Vadesi Geçen Borç/Alacak Yok",
                    Values = new decimal[] { 1 },
                    Fill = new SolidColorPaint(new SKColor(16, 185, 129, 120)), // Green
                    InnerRadius = 55
                }
            };
        }
        else
        {
            AgingSeries = new ISeries[]
            {
                new PieSeries<decimal>
                {
                    Name = name1,
                    Values = new decimal[] { bin1 },
                    Fill = new SolidColorPaint(new SKColor(245, 158, 11)), // Amber (#F59E0B)
                    InnerRadius = 55
                },
                new PieSeries<decimal>
                {
                    Name = name2,
                    Values = new decimal[] { bin2 },
                    Fill = new SolidColorPaint(new SKColor(249, 115, 22)), // Orange (#F97316)
                    InnerRadius = 55
                },
                new PieSeries<decimal>
                {
                    Name = name3,
                    Values = new decimal[] { bin3 },
                    Fill = new SolidColorPaint(new SKColor(239, 68, 68)), // Red (#EF4444)
                    InnerRadius = 55
                }
            };
        }
    }

    [RelayCommand]
    public Task OpenEditCariAsync()
    {
        return Task.CompletedTask;
    }

    [RelayCommand]
    public async Task PrintAsync()
    {
        if (Cari == null) return;
        
        try
        {
            IsLoading = true;
            
            var sections = new List<ReportSection>();
            
            // 1. Finansal Durum & İletişim Bilgileri Özeti
            sections.Add(new ReportSection
            {
                Title = "Finansal Durum",
                KeyMetrics = new List<ChartDataItem>
                {
                    new ChartDataItem { Label = "Toplam Borç", Actual = Borc },
                    new ChartDataItem { Label = "Toplam Alacak", Actual = Alacak },
                    new ChartDataItem { Label = "Net Bakiye", Actual = Math.Abs(Bakiye) },
                    new ChartDataItem { Label = "Geciken Toplam", Actual = GecikenBorcToplam }
                }
            });

            sections.Add(new ReportSection
            {
                Title = "Risk Limiti & Performans",
                Headers = new string[] { "Bilgi", "Değer" },
                Rows = new List<string[]>
                {
                    new string[] { "Risk Limiti Durumu", RiskLimitStatusText ?? "-" },
                    new string[] { "Kullanılan Risk", $"% {RiskOrani:F1}" },
                    new string[] { "Ödeme Performans Skoru", PerformansSkoru ?? "-" },
                    new string[] { "Performans Değerlendirmesi", PerformansMetni ?? "-" }
                }
            });

            sections.Add(new ReportSection
            {
                Title = "İletişim Bilgileri",
                Headers = new string[] { "Bilgi", "Detay" },
                Rows = new List<string[]>
                {
                    new string[] { "Telefon", Cari.Telefon ?? "-" },
                    new string[] { "E-Posta", Cari.Email ?? "-" },
                    new string[] { "İl / İlçe", $"{Cari.Il ?? "-"} / {Cari.Ilce ?? "-"}" },
                    new string[] { "Açık Adres", Cari.Adres ?? "-" }
                }
            });

            var today = DateTime.Today;
            decimal bin1 = 0, bin2 = 0, bin3 = 0;
            int pdfMaxDelay1 = 0, pdfMaxDelay2 = 0, pdfMaxDelay3 = 0;

            if (Faturalar != null)
            {
                foreach (var f in Faturalar.Where(f => f.Kalan > 0 && f.VadeTarihi.Date < today))
                {
                    int delay = (today - f.VadeTarihi.Date).Days;
                    if (delay <= 30) { bin1 += f.Kalan; pdfMaxDelay1 = Math.Max(pdfMaxDelay1, delay); }
                    else if (delay <= 60) { bin2 += f.Kalan; pdfMaxDelay2 = Math.Max(pdfMaxDelay2, delay); }
                    else { bin3 += f.Kalan; pdfMaxDelay3 = Math.Max(pdfMaxDelay3, delay); }
                }
            }
            if (BekleyenEvraklar != null)
            {
                foreach (var ev in BekleyenEvraklar.Where(e => e.VadeTarihi.Date < today))
                {
                    int delay = (today - ev.VadeTarihi.Date).Days;
                    if (delay <= 30) { bin1 += ev.Tutar; pdfMaxDelay1 = Math.Max(pdfMaxDelay1, delay); }
                    else if (delay <= 60) { bin2 += ev.Tutar; pdfMaxDelay2 = Math.Max(pdfMaxDelay2, delay); }
                    else { bin3 += ev.Tutar; pdfMaxDelay3 = Math.Max(pdfMaxDelay3, delay); }
                }
            }

            string pdfName1 = pdfMaxDelay1 > 0 ? $"0-30 Gün ({pdfMaxDelay1} gün)" : "0-30 Gün";
            string pdfName2 = pdfMaxDelay2 > 0 ? $"31-60 Gün ({pdfMaxDelay2} gün)" : "31-60 Gün";
            string pdfName3 = pdfMaxDelay3 > 0 ? $"60+ Gün ({pdfMaxDelay3} gün)" : "60+ Gün";

            sections.Add(new ReportSection
            {
                Title = "Analiz & Grafikler (Vade Yaşlandırma)",
                LeftChartTitle = "VADE YAŞLANDIRMA",
                ChartData = new List<ChartDataItem>
                {
                    new ChartDataItem { Label = pdfName1, Actual = bin1 },
                    new ChartDataItem { Label = pdfName2, Actual = bin2 },
                    new ChartDataItem { Label = pdfName3, Actual = bin3 }
                }
            });

            // 2. Hesap Ekstresi
            if (Hareketler != null && Hareketler.Any())
            {
                var ekstreRows = Hareketler.Select(h => new string[]
                {
                    h.Tarih.ToString("dd.MM.yyyy"),
                    h.IslemTuru ?? "",
                    h.Aciklama ?? "",
                    h.Borc.ToString("N2"),
                    h.Alacak.ToString("N2"),
                    h.KalanBakiye.ToString("N2")
                }).ToList();

                sections.Add(new ReportSection
                {
                    Title = "Hesap Ekstresi",
                    Headers = new string[] { "Tarih", "İşlem Türü", "Açıklama", "Borç", "Alacak", "Bakiye" },
                    Rows = ekstreRows
                });
            }

            // 3. Fatura Vadeleri
            if (Faturalar != null && Faturalar.Any())
            {
                var faturaRows = Faturalar.Select(f => new string[]
                {
                    f.FaturaNo ?? "",
                    f.Tarih.ToString("dd.MM.yyyy"),
                    f.VadeTarihi.ToString("dd.MM.yyyy"),
                    f.GenelToplam.ToString("N2"),
                    f.Odenen.ToString("N2"),
                    f.Kalan.ToString("N2"),
                    f.OdemeDurumu,
                    f.KalanGunText
                }).ToList();

                sections.Add(new ReportSection
                {
                    Title = "Fatura Vadeleri",
                    Headers = new string[] { "Fatura No", "Tarih", "Vade Tarihi", "Genel Toplam", "Ödenen", "Kalan Tutar", "Durum", "Vade Durumu" },
                    Rows = faturaRows,
                    NewPage = true
                });
            }

            // 4. Çek / Senet
            if (BekleyenEvraklar != null && BekleyenEvraklar.Any())
            {
                var evrakRows = BekleyenEvraklar.Select(e => new string[]
                {
                    e.Type,
                    e.No ?? "",
                    e.VadeTarihi.ToString("dd.MM.yyyy"),
                    e.Tutar.ToString("N2"),
                    e.Tur,
                    e.Durum ?? "",
                    e.KalanGunText
                }).ToList();

                sections.Add(new ReportSection
                {
                    Title = "Bekleyen Evraklar (Çek / Senet)",
                    Headers = new string[] { "Tür", "Seri/Evrak No", "Vade Tarihi", "Tutar", "Yönü", "Durum", "Vade Durumu" },
                    Rows = evrakRows,
                    NewPage = true
                });
            }

            var pdfBytes = await _pdfService.GenerateConsolidatedReportPdfBytesAsync($"Cari Detay Raporu - {Cari.Unvan}", sections);
            
            if (pdfBytes != null && pdfBytes.Length > 0)
            {
                string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ErmayFiles");
                if (!System.IO.Directory.Exists(tempDir)) System.IO.Directory.CreateDirectory(tempDir);
                
                string safeName = $"CariDetay_{Cari.Unvan}_{DateTime.Now:HHmmss}.pdf";
                foreach (var c in System.IO.Path.GetInvalidFileNameChars()) { safeName = safeName.Replace(c.ToString(), "_"); }
                string path = System.IO.Path.Combine(tempDir, safeName);
                
                await System.IO.File.WriteAllBytesAsync(path, pdfBytes);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error printing Cari Detay: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
