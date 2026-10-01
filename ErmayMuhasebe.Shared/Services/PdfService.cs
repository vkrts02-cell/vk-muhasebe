using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Helpers;
using ErmayMuhasebe.Models;
using SkiaSharp;
using Humanizer;
using System.Globalization;

namespace ErmayMuhasebe.Services
{
    public class PdfService : IPdfService
    {
        private readonly IFileService? _fileService;

        public PdfService(IFileService? fileService = null) 
        {
            _fileService = fileService;
            if (OperatingSystem.IsBrowser())
            {
                Console.WriteLine("PdfService Initialized (WASM Mode - Direct rendering disabled)");
                return;
            }
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
            Console.WriteLine("PdfService Initialized (Native Mode)");
        }

        private async Task<(bool Success, string Error)> SaveAndOpenHelper(string fileName, byte[] bytes)
        {
            if (_fileService == null) return (false, "FileService not available.");
            try 
            {
                await _fileService.SaveAndOpenFileAsync(fileName, bytes, "application/pdf");
                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }


        public virtual async Task<(bool Success, string Error)> GenerateFaturaPdfAsync(Fatura fatura, List<FaturaDetay> detaylar)
            => await SaveAndOpenHelper($"Fatura_{fatura.FaturaNo}.pdf", GenerateFaturaPdf(fatura, detaylar));

        public virtual async Task<(bool Success, string Error)> GenerateTeklifPdfAsync(Teklif teklif, List<TeklifDetay> detaylar, CariKart? cari)
            => await SaveAndOpenHelper($"Teklif_{teklif.TeklifNo}.pdf", GenerateTeklifPdf(teklif, detaylar, cari));

        public virtual async Task<(bool Success, string Error)> GenerateSiparisPdfAsync(Siparis siparis, List<SiparisDetay> detaylar, CariKart? cari = null)
            => await SaveAndOpenHelper($"Siparis_{siparis.SiparisNo}.pdf", GenerateSiparisPdf(siparis, detaylar, cari));

        public virtual async Task<(bool Success, string Error)> GenerateGenericTablePdfAsync(string title, string[] headers, List<string[]> rows, string? subtitle = null)
            => await SaveAndOpenHelper($"{title}.pdf", GenerateGenericTablePdf(title, headers, rows));

        public virtual async Task<(bool Success, string Error)> GenerateBatchFaturaPdfAsync(List<(Fatura Fatura, List<FaturaDetay> Detaylar)> items)
        {
            // Simple approach: Generate one by one or combine. Desktop usually doesn't have a combined one yet.
            // For now, let's just do the first one to avoid errors, or implement if exists.
            if (!items.Any()) return (true, "");
            return await GenerateFaturaPdfAsync(items[0].Fatura, items[0].Detaylar);
        }

        public virtual async Task<(bool Success, string Error)> GenerateMakbuzPdfAsync(string? makbuzTipi, string? cariUnvan, DateTime tarih, decimal tutar, string? aciklama)
            => await SaveAndOpenHelper("Makbuz.pdf", GenerateMakbuzPdf(makbuzTipi, cariUnvan, tarih, tutar, aciklama));

        public virtual async Task<(bool Success, string Error)> GenerateEftPdfAsync(EftIslem islem)
            => await SaveAndOpenHelper($"EFT_{islem.DekontNo}.pdf", GenerateEftSlipPdf(islem));

        public virtual async Task<(bool Success, string Error)> GenerateKkPdfAsync(KrediKartiIslem islem)
            => await SaveAndOpenHelper("KrediKarti_Slip.pdf", GenerateMakbuzPdf("Kredi Kartı", islem.MusteriUnvan, islem.Tarih, islem.Tutar, islem.Aciklama)); // Simple fallback

        public virtual Task<(bool Success, string Error)> GenerateBudgetReportPdfAsync(string title, List<ErmayMuhasebe.Services.ChartDataItem> annual, List<ErmayMuhasebe.Services.ChartDataItem> monthly, List<ErmayMuhasebe.Services.ChartDataItem> weekly)
            => Task.FromResult((false, "Not implemented natively yet"));

        public virtual async Task<(bool Success, string Error)> GenerateConsolidatedReportPdfAsync(string title, List<ErmayMuhasebe.Services.ReportSection> sections)
            => await SaveAndOpenHelper($"{title}.pdf", GenerateConsolidatedReportPdf(title, sections));

        public virtual async Task<(bool Success, string Error)> GenerateCariEkstrePdfAsync(CariKart cari, List<CariHareket> hareketler)
            => await SaveAndOpenHelper($"Ekstre_{cari.Unvan}.pdf", GenerateCariEkstrePdf(cari, hareketler));
        public virtual async Task<(bool Success, string Error)> GenerateDetayliCariEkstrePdfAsync(CariKart cari, List<CariHareket> hareketler, Dictionary<int, List<FaturaDetay>> detaylar)
             => await SaveAndOpenHelper($"Detayli_Ekstre_{cari.Unvan}.pdf", GenerateDetayliCariEkstrePdf(cari, hareketler, detaylar));

        public virtual Task<byte[]> GenerateFaturaPdfBytesAsync(Fatura fatura, List<FaturaDetay> detaylar) 
            => Task.FromResult(GenerateFaturaPdf(fatura, detaylar));

        public virtual Task<byte[]> GenerateTeklifPdfBytesAsync(Teklif teklif, List<TeklifDetay> detaylar, CariKart? cari)
            => Task.FromResult(GenerateTeklifPdf(teklif, detaylar, cari));

        public virtual Task<byte[]> GenerateSiparisPdfBytesAsync(Siparis siparis, List<SiparisDetay> detaylar, CariKart? cari)
            => Task.FromResult(GenerateSiparisPdf(siparis, detaylar, cari));

        public virtual Task<byte[]> GenerateGenericTablePdfBytesAsync(string title, string[] headers, List<string[]> rows, string? subtitle = null)
            => Task.FromResult(GenerateGenericTablePdf(title, headers, rows));

        public virtual Task<byte[]> GenerateCariEkstrePdfBytesAsync(CariKart cari, List<CariHareket> hareketler)
            => Task.FromResult(GenerateCariEkstrePdf(cari, hareketler));

        public virtual Task<byte[]> GenerateDetayliCariEkstrePdfBytesAsync(CariKart cari, List<CariHareket> hareketler, Dictionary<int, List<FaturaDetay>> detaylar)
            => Task.FromResult(GenerateDetayliCariEkstrePdf(cari, hareketler, detaylar));

        public virtual Task<byte[]> GenerateFaturaBatchPdfBytesAsync(List<(Fatura Fatura, List<FaturaDetay> Detaylar)> items)
            => Task.FromResult(GenerateFaturaBatchPdf(items));

        public virtual Task<byte[]> GenerateMakbuzPdfBytesAsync(string? makbuzTipi, string? cariUnvan, DateTime tarih, decimal tutar, string? aciklama)
            => Task.FromResult(GenerateMakbuzPdf(makbuzTipi, cariUnvan, tarih, tutar, aciklama));

        public virtual Task<byte[]> GenerateEftSlipPdfBytesAsync(EftIslem islem)
            => Task.FromResult(GenerateEftSlipPdf(islem));

        public virtual Task<byte[]> GenerateKrediKartiSlipPdfBytesAsync(KrediKartiIslem islem)
            => Task.FromResult(GenerateKrediKartiSlipPdf(islem));

        public virtual Task<byte[]> GenerateBudgetReportPdfBytesAsync(string title, List<ErmayMuhasebe.Services.ChartDataItem> annual, List<ErmayMuhasebe.Services.ChartDataItem> monthly, List<ErmayMuhasebe.Services.ChartDataItem> weekly)
            => Task.FromResult(GenerateBudgetReportPdf(title, annual, monthly, weekly));

        public virtual Task<byte[]> GenerateConsolidatedReportPdfBytesAsync(string title, List<ErmayMuhasebe.Services.ReportSection> sections)
            => Task.FromResult(GenerateConsolidatedReportPdf(title, sections));

        public virtual Task<byte[]> GenerateKasaEkstrePdfBytesAsync(BankaKart kasa, List<KasaHareket> hareketler)
            => Task.FromResult(GenerateKasaEkstrePdf(kasa, hareketler));

        public virtual Task<byte[]> GenerateMakbuzFromKasaPdfBytesAsync(KasaHareket h)
            => Task.FromResult(GenerateMakbuzFromKasaPdf(h));

        public virtual Task<byte[]> GenerateCekPdfBytesAsync(Cek cek)
            => Task.FromResult(GenerateCekPdf(cek));

        public virtual Task<byte[]> GenerateCekListPdfBytesAsync(List<Cek> cekler)
            => Task.FromResult(GenerateCekListPdf(cekler));

        public virtual Task<byte[]> GenerateKrediKartiListPdfBytesAsync(List<KrediKartiIslem> islemler)
            => Task.FromResult(GenerateKrediKartiListPdf(islemler));

        public virtual Task<byte[]> GenerateEftListPdfBytesAsync(List<EftIslem> islemler)
            => Task.FromResult(GenerateEftListPdf(islemler));

        public virtual Task<byte[]> GenerateKasaListesiPdfBytesAsync(List<BankaKart> kasalar)
            => Task.FromResult(GenerateKasaListesiPdf(kasalar));

        public virtual Task<byte[]> GenerateAdresEtiketiPdfBytesAsync(string unvan, string adSoyad, string adres, string ilIlce, string postaKodu, string telefon)
            => Task.FromResult(GenerateAdresEtiketiPdf(unvan, adSoyad, adres, ilIlce, postaKodu, telefon));

        public virtual Task<byte[]> GenerateFaturaListPdfBytesAsync(List<Fatura> faturalar, DateTime startDate, DateTime endDate)
            => Task.FromResult(GenerateFaturaListPdf(faturalar, startDate, endDate));

        public virtual Task<byte[]> GenerateFinansRaporPdfBytesAsync(string title, FinancialReportData data)
            => Task.FromResult(GenerateFinansRaporPdf(title, data));

        public virtual Task<byte[]> GenerateStokListPdfBytesAsync(List<StokKart> stoklar)
            => Task.FromResult(GenerateStokListPdf(stoklar));

        public virtual Task<byte[]> GenerateStokHareketleriPdfBytesAsync(StokKart stok, List<StokHareket> hareketler)
            => Task.FromResult(GenerateStokHareketleriPdf(stok, hareketler));

        public virtual Task<byte[]> GenerateVadeRaporuPdfBytesAsync(List<VadeReportItem> items)
            => Task.FromResult(GenerateVadeRaporuPdf(items));

        public string DefaultFontFamily { get; set; } = "Arial";
        public FaturaTasarimi? AktifFaturaTasarimi { get; set; }
        private byte[]? _logoBytes;
        private bool _isLogoLoaded = false;
        public byte[]? LogoBytes 
        { 
            get 
            {
                if (_logoBytes != null && _logoBytes.Length > 0) return _logoBytes;
                return LoadLogoBytes();
            }
            set 
            { 
                _logoBytes = value; 
                _isLogoLoaded = (value != null && value.Length > 0); 
            } 
        }

        public void ResetLogoCache()
        {
            _logoBytes = null;
            _isLogoLoaded = false;
        }

        public bool ShowLogoFatura { get; set; } = true;
        public bool ShowLogoSiparis { get; set; } = true;
        public bool ShowLogoTeklif { get; set; } = true;
        public bool ShowLogoEkstre { get; set; } = true;
        public bool ShowLogoRaporlar { get; set; } = true;
        public bool ShowLogoTahsilat { get; set; } = true;
        public bool ShowLogoOdeme { get; set; } = true;
        public bool ShowLogoAcilisBakiye { get; set; } = true;

        public string FaturaSize { get; set; } = "A4";
        public string FaturaOrientation { get; set; } = "Portrait";
        public string SiparisSize { get; set; } = "A5";
        public string SiparisOrientation { get; set; } = "Portrait";
        public string TeklifSize { get; set; } = "A4";
        public string TeklifOrientation { get; set; } = "Portrait";
        public string EkstreSize { get; set; } = "A4";
        public string EkstreOrientation { get; set; } = "Portrait";
        public string RaporSize { get; set; } = "A4";
        public string RaporOrientation { get; set; } = "Portrait";
        public string TahsilatSize { get; set; } = "A5";
        public string TahsilatOrientation { get; set; } = "Portrait";
        public string OdemeSize { get; set; } = "A5";
        public string OdemeOrientation { get; set; } = "Portrait";
        public string AcilisBakiyeSize { get; set; } = "A5";
        public string AcilisBakiyeOrientation { get; set; } = "Portrait";

        private QuestPDF.Helpers.PageSize GetPageSize(string size, string orientation)
        {
            var pageSize = size.ToUpper() == "A5" ? QuestPDF.Helpers.PageSizes.A5 : QuestPDF.Helpers.PageSizes.A4;
            bool isLandscape = orientation.ToLower() == "landscape" || orientation == "Yatay";
            return isLandscape ? pageSize.Landscape() : pageSize.Portrait();
        }

        private string GetReportOrientation(string title)
        {
            if (title != null && 
                (title.Contains("GENEL RAPOR", StringComparison.OrdinalIgnoreCase) || 
                 title.Contains("360 DERECE", StringComparison.OrdinalIgnoreCase) || 
                 title.Contains("GENEL ÖZET", StringComparison.OrdinalIgnoreCase) || 
                 title.Contains("GENEL DURUM", StringComparison.OrdinalIgnoreCase)))
            {
                return "Portrait";
            }
            return "Landscape";
        }

        private class StandardPdfItem
        {
            public string Description { get; set; } = "";
            public decimal Quantity { get; set; }
            public string Unit { get; set; } = "kg";
            public decimal UnitPrice { get; set; }
            public double TaxRate { get; set; }
            public decimal TotalPrice { get; set; }
            public string? Aciklama { get; set; }
            public string? MiktarAciklama { get; set; }
        }

        private void AddInfoRow(QuestPDF.Fluent.ColumnDescriptor column, string label, string value)
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(128).Text(label).FontSize(8).Bold();
                row.RelativeItem().Text(value).FontSize(8);
            });
        }

        private byte[] GenerateSharedA4Document(
            string docNoLabel,
            string docNo,
            DateTime date,
            string customerUnvan,
            string customerAdres,
            string customerTaxNo,
            List<StandardPdfItem> items,
            decimal araToplam,
            decimal kdvTutari,
            decimal genelToplam,
            string odemeBilgisi = "",
            string optionalAciklama = "",
            bool showInfoRows = true,
            bool showLogo = true,
            string size = "A4",
            string orientation = "Portrait",
            string? baslikText = null,
            string? altBilgiText = null,
            bool showBirim = true,
            bool showKdv = true,
            bool showAraToplam = true,
            bool showAciklama = true)
        {
            var trCulture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(GetPageSize(size, orientation));
                    page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(DefaultFontFamily).LineHeight(1.2f).FontColor("#000000"));

                    page.Content().PaddingVertical(5).Column(col =>
                    {
                        // CUSTOM TITLE (Fatura Tasarımı)
                        if (!string.IsNullOrWhiteSpace(baslikText))
                        {
                            col.Item().PaddingBottom(6).Text(baslikText.ToUpper()).FontSize(16).ExtraBold().AlignCenter();
                        }

                        // HEADER INFO (FIRST PAGE ONLY)
                        col.Item().Column(headerCol => 
                        {
                            // Logo Section
                            if (showLogo)
                            {
                                headerCol.Item().PaddingBottom(5).Row(row => 
                                {
                                    var logoBytes = LoadLogoBytes();
                                    if (logoBytes != null && logoBytes.Length > 0)
                                    {
                                        row.ConstantItem(130).MaxHeight(50).AlignLeft().Image(logoBytes).FitArea();
                                    }
                                });
                            }

                            // Double Line Under Logo
                            headerCol.Item().PaddingVertical(3).Column(lines => 
                            {
                                lines.Item().LineHorizontal(0.6f).LineColor("#000000");
                                lines.Item().PaddingTop(1.2f).LineHorizontal(0.6f).LineColor("#000000");
                            });

                            // Info Boxes
                            headerCol.Item().PaddingTop(4).Row(row =>
                            {
                                // Left Box: Firm Info
                                row.RelativeItem().Border(0.4f).BorderColor("#000000").PaddingVertical(4).PaddingHorizontal(6).Column(c =>
                                {
                                    c.Item().Text("ERMAY TEKNİK TEKSTİL SAN. VE TİC. A.Ş.").ExtraBold().FontSize(9);
                                    c.Item().PaddingTop(1).Text("Adres: ORUCREİS MAH. TEKSTİLKENT SAN. SİT.").FontSize(8);
                                    c.Item().Text("G2 BLOK KAT:3 NO:407 ESENLER/ İSTANBUL").FontSize(8);
                                    c.Item().Text("VKN: 3640652611").FontSize(8);
                                    c.Item().Text("Vergi Dairesi: ATISALANI VERGİ DAİRESİ").FontSize(8);
                                    c.Item().Text("Telefon: (850) 762 60 05").FontSize(8);
                                    c.Item().Text("Web Sitesi: www.ermaysanayi.com").FontSize(8);
                                    c.Item().Text("E-Posta: info@ermaysanayi.com").FontSize(8);
                                });

                                row.ConstantItem(12); 

                                // Right Box: Customer Info
                                row.RelativeItem().Border(0.4f).BorderColor("#000000").PaddingVertical(4).PaddingHorizontal(6).Column(c =>
                                {
                                    c.Item().Text(customerUnvan.ToUpper()).ExtraBold().FontSize(9);
                                    c.Item().PaddingTop(1).Text($"Adres: {customerAdres.ToUpper()}").FontSize(8);
                                    c.Item().Text($"{docNoLabel}: {docNo}").FontSize(8);
                                    c.Item().Text($"Tarih: {date:dd.MM.yyyy}").FontSize(8);
                                    if (!string.IsNullOrEmpty(customerTaxNo)) c.Item().Text($"V.No: {customerTaxNo}").FontSize(8);
                                });
                            });
                            
                            // Double Line Under Boxes
                            headerCol.Item().PaddingTop(8).Column(lines => 
                            {
                                lines.Item().LineHorizontal(0.6f).LineColor("#000000");
                                lines.Item().PaddingTop(1.2f).LineHorizontal(0.6f).LineColor("#000000");
                            });

                            headerCol.Item().PaddingBottom(10);
                        });

                        // Product Table
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);   // NO
                                columns.RelativeColumn(6);    // AÇIKLAMA
                                if (showBirim)
                                {
                                    columns.RelativeColumn(2);   // ADET
                                    columns.RelativeColumn(3);   // BİRİM FİYATI
                                }
                                if (showKdv)
                                {
                                    columns.RelativeColumn(2);   // KDV ORANI
                                }
                                columns.RelativeColumn(3);  // TOPLAM FİYAT
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderStyle).Text("NO.");
                                header.Cell().Element(HeaderStyle).Text("AÇIKLAMA");
                                if (showBirim)
                                {
                                    header.Cell().Element(HeaderStyleRight).Text("ADET");
                                    header.Cell().Element(HeaderStyleRight).Text("BİRİM FİYATI");
                                }
                                if (showKdv)
                                {
                                    header.Cell().Element(HeaderStyle).AlignCenter().Text("KDV ORANI");
                                }
                                header.Cell().Element(HeaderStyleRight).Text("TOPLAM FİYAT");

                                static QuestPDF.Infrastructure.IContainer HeaderStyle(QuestPDF.Infrastructure.IContainer container)
                                {
                                    return container.Border(0.4f).BorderColor("#999999").Background("#F5F5F5").Padding(4)
                                        .DefaultTextStyle(x => x.FontSize(8.5f).Bold());
                                }
                                static QuestPDF.Infrastructure.IContainer HeaderStyleRight(QuestPDF.Infrastructure.IContainer container) => HeaderStyle(container).AlignRight();
                            });

                            for (int i = 0; i < items.Count; i++)
                            {
                                var item = items[i];
                                table.Cell().Element(CellStyle).Text($"{i + 1}.");
                                table.Cell().Element(CellStyle).Column(c => {
                                    c.Item().Text(item.Description.ToUpper()).Bold();
                                    if (!string.IsNullOrEmpty(item.Aciklama))
                                        c.Item().Text(item.Aciklama).FontSize(7).FontColor("#64748B").Italic();
                                });
                                if (showBirim)
                                {
                                    table.Cell().Element(CellStyle).AlignRight().Column(c => {
                                        c.Item().Text($"{item.Quantity.ToString("N1", trCulture)} {item.Unit.ToLower()}".Replace(",0 kg", " kg")).FontSize(8);
                                    });
                                    table.Cell().Element(CellStyle).AlignRight().Text($"{item.UnitPrice.ToString("N2", trCulture)} TL");
                                }
                                if (showKdv)
                                {
                                    table.Cell().Element(CellStyle).AlignCenter().Text(((int)item.TaxRate).ToString());
                                }
                                table.Cell().Element(CellStyle).AlignRight().Text($"{item.TotalPrice.ToString("N2", trCulture)} TL");

                                static QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer container)
                                {
                                    return container.Border(0.4f).BorderColor("#999999").PaddingVertical(3).PaddingHorizontal(5).DefaultTextStyle(x => x.FontSize(8));
                                }
                            }
                        });


                        // Totals
                        col.Item().PaddingTop(5).Row(row => 
                        {
                            row.RelativeItem().Column(c => {
                                c.Item().PaddingTop(10).Text($"Yalnız: {AmountToWordsTurkish(genelToplam)}").FontSize(8).Italic();
                            });
                            row.ConstantItem(180).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                });

                                string taxLabel = "KDV";
                                if(araToplam > 0)
                                {
                                    int rate = (int)Math.Round((kdvTutari / araToplam) * 100);
                                    if(rate > 0) taxLabel = $"KDV %{rate}";
                                }

                                if (showAraToplam)
                                {
                                    table.Cell().Element(LabelStyle).Text("ARA TOPLAM");
                                    table.Cell().Element(ValueStyle).Text($"{araToplam.ToString("N2", trCulture)} TL");
                                }
                                if (showKdv)
                                {
                                    table.Cell().Element(LabelStyle).Text(taxLabel);
                                    table.Cell().Element(ValueStyle).Text($"{kdvTutari.ToString("N2", trCulture)} TL");
                                }

                                table.Cell().Element(LabelStyle).Text("GENEL TOPLAM");
                                table.Cell().Element(ValueStyle).Text($"{genelToplam.ToString("N2", trCulture)} TL");

                                static QuestPDF.Infrastructure.IContainer LabelStyle(QuestPDF.Infrastructure.IContainer container)
                                {
                                    return container.Border(0.4f).BorderColor("#999999").Padding(4).DefaultTextStyle(x => x.FontSize(8));
                                }
                                static QuestPDF.Infrastructure.IContainer ValueStyle(QuestPDF.Infrastructure.IContainer container)
                                {
                                    return container.Border(0.4f).BorderColor("#999999").Padding(4).AlignRight().DefaultTextStyle(x => x.FontSize(8));
                                }
                            });
                        });

                        // Info Rows (only for Teklif)
                        if (showInfoRows)
                        {
                            col.Item().PaddingTop(10).Column(footerCol =>
                            {
                                footerCol.Spacing(1);
                                
                                AddInfoRow(footerCol, "SEVKİYAT YERİ", ":İSTANBUL");
                                AddInfoRow(footerCol, "PAKETLEME", ":Rulo ve P.e. Torba");
                                AddInfoRow(footerCol, "QUANTITY", ":10% +/- Acceptable");
                                AddInfoRow(footerCol, "ÖDEME", $":{odemeBilgisi}");
                                AddInfoRow(footerCol, "MENŞEİ", ":COUNTRY OF ORIGINAL TURKEY");
                                AddInfoRow(footerCol, "BANKA ADI", ":KUVEYTTÜRK");
                                AddInfoRow(footerCol, "ŞUBE ADI", ":TEKSTİLKENT ŞUBE");
                                AddInfoRow(footerCol, "ŞUBE KODU", ":485");
                                AddInfoRow(footerCol, "IBAN NO", ":TR34 0020 5000 0998 0123 4000 01");
                                AddInfoRow(footerCol, "SWIFT CODE", ":KTEFTRSXXX");
                                AddInfoRow(footerCol, "BANKA ADRESİ", ":Oruçreis Mah. Tekstilkent Cad. Tekstilkent Ticaret Merkezi Çarşı Blok No:10-U/Z-02-03-04");
                                footerCol.Item().PaddingLeft(128).Text("ESENLER / İstanbul").FontSize(8);
                            });
                        }

                        // Double Line Above Notes
                        col.Item().PaddingTop(10).Column(lines => 
                        {
                            lines.Item().LineHorizontal(0.6f).LineColor("#000000");
                            lines.Item().PaddingTop(1.2f).LineHorizontal(0.6f).LineColor("#000000");
                        });

                        // Notes Section
                        if (showAciklama)
                        {
                            col.Item().PaddingTop(4).Column(noteCol =>
                            {
                                noteCol.Item().Text("NOTLAR:").Bold().FontSize(10);
                                noteCol.Item().PaddingTop(1).Text("Bu proforma fatura sadece usulüne uygun imzalanmış, kaşelenmiş olarak geçerlidir.").FontSize(8.5f);
                                noteCol.Item().Text("Merkez Bankası Satış Kuru Uygulanmaktadır.").FontSize(8.5f);
                                noteCol.Item().Text("Kur Fatura Kesim Günü Merkez Bankası Kuru Alınacaktır.").FontSize(8.5f);
                                if (!string.IsNullOrWhiteSpace(altBilgiText))
                                {
                                    noteCol.Item().PaddingTop(4).Text(altBilgiText).FontSize(8.5f).Italic();
                                }
                            });
                        }

                        // Double Line bottom frame
                        col.Item().PaddingTop(8).Column(lines => 
                        {
                            lines.Item().LineHorizontal(0.6f).LineColor("#000000");
                            lines.Item().PaddingTop(1.2f).LineHorizontal(0.6f).LineColor("#000000");
                        });

                        // Extra Aciklama
                        if (!string.IsNullOrWhiteSpace(optionalAciklama))
                        {
                            col.Item().PaddingTop(10).Text("Açıklama:").Bold();
                            col.Item().Text(optionalAciklama);
                        }
                    });

                    page.Footer()
                        .AlignCenter()
                        .PaddingBottom(10)
                        .Text(x =>
                        {
                            x.DefaultTextStyle(s => s.FontSize(8));
                            x.Span("Sayfa ");
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                        });
                });
            });

            return document.GeneratePdf();
        }

        private string AmountToWordsTurkish(decimal amount)
        {
            try
            {
                long lira = (long)Math.Truncate(amount);
                int kurus = (int)Math.Round((amount - lira) * 100);

                var culture = new CultureInfo("tr-TR");
                string liraText = lira == 0 ? "SIFIR" : lira.ToWords(culture).ToUpper(culture);
                string kurusText = kurus == 0 ? "" : kurus.ToWords(culture).ToUpper(culture);

                string result = $"# {liraText} TÜRK LİRASI";
                if (kurus > 0) result += $" {kurusText} KURUŞ";
                result += " #";

                return result.Replace("-", " ");
            }
            catch { return ""; }
        }
        private static readonly Dictionary<string, byte[]> _badgeCache = new Dictionary<string, byte[]>();

        private bool _isSkiaSharpBroken = false;
        private byte[] GetBadgeBytes(string hexColor, string text)
        {
            if (_isSkiaSharpBroken) return Array.Empty<byte>();

            var cacheKey = $"{hexColor}_{text}";
            if (_badgeCache.TryGetValue(cacheKey, out var cached)) return cached;

            try 
            {
                using var paintText = new SKPaint
                {
                    Color = SKColors.White,
                    IsAntialias = true,
                    TextSize = 24,
                    Typeface = SKTypeface.FromFamilyName(DefaultFontFamily ?? "Arial", SKFontStyle.Bold)
                };

                float textWidth = paintText.MeasureText(text);
                float horizontalPadding = 18;
                float totalWidth = textWidth + (horizontalPadding * 2);
                float totalHeight = 40;

                using var surface = SKSurface.Create(new SKImageInfo((int)totalWidth, (int)totalHeight));
                var canvas = surface.Canvas;
                canvas.Clear(SKColors.Transparent);

                using var paintBg = new SKPaint
                {
                    Color = SKColor.Parse(hexColor),
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawRoundRect(new SKRect(0, 0, totalWidth, totalHeight), totalHeight / 2, totalHeight / 2, paintBg);

                var fontMetrics = paintText.FontMetrics;
                float x = horizontalPadding;
                float y = (totalHeight / 2) - (fontMetrics.Ascent + fontMetrics.Descent) / 2;
                canvas.DrawText(text, x, y, paintText);

                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                var bytes = data.ToArray();
                _badgeCache[cacheKey] = bytes;
                return bytes;
            }
            catch
            {
                _isSkiaSharpBroken = true;
                return Array.Empty<byte>();
            }
        }

        private void ComposeBadge(QuestPDF.Infrastructure.IContainer container, string type, string text)
        {
            var color = "#6B7280"; // Default Grey
            string t = (type ?? "").ToUpper(new System.Globalization.CultureInfo("tr-TR"));

            if (t.Contains("SATIŞ") || t.Contains("SATIS") || t.Contains("FATURA") || t.Contains("GİRİŞ") || t.Contains("GIRIS")) color = "#2F6FED";
            else if (t.Contains("ALIŞ") || t.Contains("ALIS") || t.Contains("ÇIKIŞ") || t.Contains("CIKIS")) color = "#F59E0B";
            else if (t.Contains("TAHSİLAT") || t.Contains("TAHSILAT")) color = "#10B981";
            else if (t.Contains("ÖDEME") || t.Contains("ODEME")) color = "#EF4444";
            else if (t.Contains("ÇEK") || t.Contains("CEK") || t.Contains("SENET")) color = "#8B5CF6";
            else if (t.Contains("KREDİ") || t.Contains("KREDI")) color = "#1E3A8A";
            else if (t.Contains("ALACAK")) color = "#10B981";
            else if (t.Contains("BORÇ") || t.Contains("BORC")) color = "#EF4444";
            else if (t.Contains("AÇILIŞ") || t.Contains("ACILIS")) color = "#6366f1";

            var badgeBytes = GetBadgeBytes(color, text);

            container.AlignLeft().AlignMiddle().MaxHeight(10).Image(badgeBytes).FitArea();
        }

        public byte[] GenerateKasaEkstrePdf(BankaKart kasa, List<KasaHareket> hareketler)
        {
            var orderedHareketler = hareketler.OrderBy(h => h.Tarih).ThenBy(h => h.Id).ToList();
            
            var runningBalance = kasa.AcilisBakiyesi;
            var rows = new List<dynamic>();
            foreach(var h in orderedHareketler)
            {
               runningBalance += (decimal)(h.Giren - h.Cikan);
               rows.Add(new { Hareket = h, Bakiye = runningBalance });
            }

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(QuestPDF.Helpers.PageSizes.A5);
                    page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(DefaultFontFamily).FontColor("#000000"));

                    page.Header().Column(headerCol =>
                    {
                        headerCol.Item().Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("KASA/BANKA EKSTRESİ").FontSize(18).ExtraBold().FontColor("#1e3a8a");
                                col.Item().PaddingTop(2).Text(kasa.BankaAdi ?? "İsimsiz Kasa").FontSize(12).Bold();
                                if(!string.IsNullOrEmpty(kasa.HesapNo))
                                    col.Item().Text($"Hesap: {kasa.HesapNo}").FontSize(9);
                            });
                            row.ConstantItem(120).AlignRight().MaxHeight(30).Element(c => {
                                var logoBytes = LoadLogoBytes();
                                if (logoBytes != null) c.Image(logoBytes).FitArea();
                                else c.Text("ERMAY").FontSize(10).Bold();
                            });
                        });
                        headerCol.Item().PaddingTop(5).LineHorizontal(0.6f).LineColor("#1e3a8a");
                    });

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3); // Tarih
                                columns.RelativeColumn(6);    // Açıklama
                                columns.RelativeColumn(3);  // Giren
                                columns.RelativeColumn(3);  // Çıkan
                                columns.RelativeColumn(3); // Bakiye
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderStyle).Text("Tarih");
                                header.Cell().Element(HeaderStyle).Text("Açıklama");
                                header.Cell().Element(HeaderStyle).AlignRight().Text("Giren");
                                header.Cell().Element(HeaderStyle).AlignRight().Text("Çıkan");
                                header.Cell().Element(HeaderStyle).AlignRight().Text("Bakiye");

                                static QuestPDF.Infrastructure.IContainer HeaderStyle(QuestPDF.Infrastructure.IContainer container)
                                {
                                    return container.Background("#f8fafc").BorderBottom(0.4f).BorderColor("#999999").Padding(5).DefaultTextStyle(x => x.Bold());
                                }
                            });

                            foreach (var row in rows)
                            {
                                var h = (KasaHareket)row.Hareket;
                                decimal bak = (decimal)row.Bakiye;
                                
                                table.Cell().Element(CellStyle).Text(h.Tarih.ToString("dd.MM.yyyy HH:mm"));
                                table.Cell().Element(CellStyle).Text(h.Aciklama ?? "-");
                                table.Cell().Element(CellStyle).AlignRight().Text(h.Giren > 0 ? h.Giren.ToString("N2") : "-");
                                table.Cell().Element(CellStyle).AlignRight().Text(h.Cikan > 0 ? h.Cikan.ToString("N2") : "-");
                                table.Cell().Element(CellStyle).AlignRight().Text(bak.ToString("N2")).Bold();

                                static QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer container)
                                {
                                    return container.BorderBottom(0.4f).BorderColor("#e2e8f0").Padding(5).AlignMiddle();
                                }
                            }
                        });
                    });

                    page.Footer().Row(r => {
                        r.RelativeItem().AlignLeft().Text("Ermay Bilgi İşlem").FontSize(8).FontColor("#999999");
                        r.RelativeItem().AlignCenter().DefaultTextStyle(x => x.FontSize(8)).Text(x => { x.Span("Sayfa "); x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                        r.RelativeItem().AlignRight().Text(DateTime.Now.ToString("dd.MM.yyyy HH:mm")).FontSize(8);
                    });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateMakbuzFromKasaPdf(KasaHareket h)
        {
            return GenerateMakbuzPdf(h.IslemTuru, h.CariUnvan ?? "Peşin İşlem", h.Tarih, h.Giren + h.Cikan, h.Aciklama);
        }
        public byte[] GenerateCekPdf(Cek cek)
        {
            // License set in Program.cs

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A5);
                    page.Margin(0.5f, Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10.5f).FontFamily(DefaultFontFamily));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text($"{cek.CekTuru} ÇEK FORMU").FontSize(16).Bold().FontColor("#2563eb");
                            col.Item().Text($"Portföy No: {cek.PortfoyNo}").FontSize(9).FontColor("#444444");
                        });
                        var logoBytes = LoadLogoBytes();
                        if (logoBytes != null) row.ConstantItem(120).AlignRight().MaxHeight(30).Image(logoBytes).FitArea();
                        else row.ConstantItem(120).AlignRight().PaddingTop(5).Text("ERMAY").FontSize(9).Bold();
                    });

                    page.Content().PaddingVertical(0.3f, Unit.Centimetre).Column(col =>
                    {
                        col.Spacing(4);
                        
                        col.Item().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(2).Row(row =>
                        {
                            row.RelativeItem(1).Text("VAADE TARİHİ:").Bold();
                            row.RelativeItem(2).Text($"{cek.VadeTarihi:dd.MM.yyyy}");
                        });

                        col.Item().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(2).Row(row =>
                        {
                            row.RelativeItem(1).Text("ASIL BORÇLU:").Bold();
                            row.RelativeItem(2).Text(cek.AsilBorclu ?? "-");
                        });

                        col.Item().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(2).Row(row =>
                        {
                            row.RelativeItem(1).Text("BANKA / ŞUBE:").Bold();
                            row.RelativeItem(2).Text($"{cek.Banka} / {cek.Sube}");
                        });

                        col.Item().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(2).Row(row =>
                        {
                            row.RelativeItem(1).Text("SERİ NO:").Bold();
                            row.RelativeItem(2).Text(cek.SeriNo ?? "-");
                        });

                        col.Item().PaddingTop(3).Row(row =>
                        {
                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().Text("TOPLAM TUTAR").FontSize(9).Bold().FontColor("#64748b");
                                c.Item().Text($"{cek.Tutar:N2} ₺").FontSize(20).ExtraBold().FontColor("#2563eb");
                            });
                        });

                        if (!string.IsNullOrEmpty(cek.Aciklama))
                        {
                            col.Item().PaddingTop(3).Column(noteCol =>
                            {
                                noteCol.Item().Text("AÇIKLAMA:").Bold().FontSize(9);
                                noteCol.Item().Text(cek.Aciklama).FontSize(9);
                            });
                        }

                        // --- Images Section (Refactored for Best Practices) ---
                        if (!string.IsNullOrEmpty(cek.GorselYoluOn) || !string.IsNullOrEmpty(cek.GorselYoluArka))
                        {
                            col.Item().PaddingTop(5).Column(imgCol =>
                            {
                                imgCol.Item().Text("ÇEK GÖRSELLERİ").Bold().FontSize(9).FontColor("#64748b");
                                imgCol.Item().Row(row =>
                                {
                                    // Ön Yüz Kutu - Constraint-safe container with AspcetRatio
                                    row.RelativeItem().PaddingRight(5).Column(c =>
                                    {
                                        c.Item().AlignCenter().Text("ÖN YÜZ").FontSize(7).FontColor("#64748b");
                                        c.Item().PaddingTop(2).AspectRatio(1.66f).Element(container => 
                                        {
                                            var bytes = LoadImageBytes(cek.GorselYoluOn);
                                            if (bytes != null) container.AlignCenter().AlignMiddle().Image(bytes).FitArea();
                                        });
                                    });

                                    // Arka Yüz Kutu - Constraint-safe container with AspectRatio
                                    row.RelativeItem().PaddingLeft(5).Column(c =>
                                    {
                                        c.Item().AlignCenter().Text("ARKA YÜZ").FontSize(7).FontColor("#64748b");
                                        c.Item().PaddingTop(2).AspectRatio(1.66f).Element(container => 
                                        {
                                            var bytes = LoadImageBytes(cek.GorselYoluArka);
                                            if (bytes != null) container.AlignCenter().AlignMiddle().Image(bytes).FitArea();
                                        });
                                    });
                                });
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Bu belge sistem tarafından otomatik oluşturulmuştur. Tarih: ");
                        x.Span(DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
                    });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateMakbuzPdf(string? makbuzTipi, string? cariUnvan, DateTime tarih, decimal tutar, string? aciklama)
        {
            // License set in Program.cs
            
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    bool isTahsilat = (makbuzTipi ?? "").Contains("Tahsilat", StringComparison.OrdinalIgnoreCase);
                    bool isAcilis = (makbuzTipi ?? "").Contains("Açılış", StringComparison.OrdinalIgnoreCase) || (makbuzTipi ?? "").Contains("Acilis", StringComparison.OrdinalIgnoreCase);
                    
                    if (isTahsilat) page.Size(GetPageSize(TahsilatSize, TahsilatOrientation));
                    else if (isAcilis) page.Size(GetPageSize(AcilisBakiyeSize, AcilisBakiyeOrientation));
                    else page.Size(GetPageSize(OdemeSize, OdemeOrientation));
                    page.Margin(0.5f, Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10.5f).FontFamily(DefaultFontFamily));
 
                    page.Header().Row(row =>
                    {
                        row.RelativeItem(3).Column(col =>
                        {
                            col.Item().Text((makbuzTipi ?? "MAKBUZ").ToUpper()).FontSize(14).Bold().FontColor("#2563eb");
                            col.Item().Text($"İşlem Tarihi: {tarih:dd.MM.yyyy}").FontSize(9).FontColor("#444444");
                        });

                        bool isTahsilat = (makbuzTipi ?? "").Contains("Tahsilat", StringComparison.OrdinalIgnoreCase);
                        bool isOdeme = (makbuzTipi ?? "").Contains("Ödeme", StringComparison.OrdinalIgnoreCase) || (makbuzTipi ?? "").Contains("Odeme", StringComparison.OrdinalIgnoreCase);
                        bool isAcilis = (makbuzTipi ?? "").Contains("Açılış", StringComparison.OrdinalIgnoreCase) || (makbuzTipi ?? "").Contains("Acilis", StringComparison.OrdinalIgnoreCase);
                        
                        bool showLogo = (isTahsilat && ShowLogoTahsilat) || (isOdeme && ShowLogoOdeme) || (isAcilis && ShowLogoAcilisBakiye) || (!isTahsilat && !isOdeme && !isAcilis && ShowLogoRaporlar);

                        if (showLogo)
                        {
                            var logoBytes = LoadLogoBytes();
                            if (logoBytes != null && logoBytes.Length > 0) 
                            {
                                  row.ConstantItem(120).AlignRight().MaxHeight(30).Image(logoBytes).FitArea();
                            }
                        }
                    });
 
                    page.Content().PaddingVertical(0.3f, Unit.Centimetre).Column(x =>
                    {
                        x.Spacing(4);
 
                        x.Item().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(3).Row(row =>
                        {
                            row.RelativeItem(1).Text("CARİ HESAP:").Bold();
                            row.RelativeItem(2).Text(cariUnvan ?? "-");
                        });
 
                        x.Item().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(3).Row(row =>
                        {
                            row.RelativeItem(1).Text("TUTAR:").Bold();
                            row.RelativeItem(2).Text($"{tutar:N2} TL").FontSize(14).Bold().FontColor("#16a34a");
                        });
 
                        if (!string.IsNullOrEmpty(aciklama))
                        {
                            x.Item().PaddingTop(3).Column(noteCol =>
                            {
                                noteCol.Item().Text("AÇIKLAMA:").Bold().FontSize(10);
                                noteCol.Item().Text(aciklama).FontSize(10);
                            });
                        }
                    });
 
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Bu belge sistem tarafından otomatik oluşturulmuştur. Tarih: ");
                        x.Span(DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
                    });
                });
            });
 
            return document.GeneratePdf();
        }

        public byte[] GenerateEftSlipPdf(EftIslem islem)
        {
            // License set in Program.cs

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A5);
                    page.Margin(0.5f, Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily(DefaultFontFamily));
 
                    page.Header().Row(row =>
                    {
                        row.RelativeItem(3).Column(col =>
                        {
                            col.Item().Text("HAVALE / EFT SLİP FORMU").FontSize(14).Bold().FontColor("#2563eb");
                            col.Item().Text($"Ref: {islem.DekontNo}").FontSize(9).FontColor("#444444");
                        });

                        // EFT slipleri genellikle tahsilat olarak kabul edilir
                        if (ShowLogoTahsilat)
                        {
                            var logoBytes = LoadLogoBytes();
                            if (logoBytes != null && logoBytes.Length > 0)
                            {
                                row.ConstantItem(120).AlignRight().MaxHeight(30).Image(logoBytes).FitArea();
                            }
                        }
                    });
 
                    page.Content().PaddingVertical(0.4f, Unit.Centimetre).Column(col =>
                    {
                        col.Spacing(4);
 
                        // Müşteri
                        col.Item().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(3).Row(row =>
                        {
                            row.RelativeItem(1).Text("MÜŞTERİ:").Bold();
                            row.RelativeItem().Text(islem.MusteriUnvan ?? "-");
                        });
 
                        // Tarih
                        col.Item().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(3).Row(row =>
                        {
                            row.RelativeItem(1).Text("TARİH:").Bold();
                            row.RelativeItem().Text($"{islem.Tarih:dd.MM.yyyy}");
                        });
 
                        // Tutar
                        col.Item().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(3).Row(row =>
                        {
                            row.RelativeItem(1).Text("TUTAR:").Bold();
                            row.RelativeItem().Text($"{islem.Tutar:N2} TL").FontSize(14).Bold().FontColor("#16a34a");
                        });
 
                        // Banka Bilgileri
                        col.Item().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(3).Row(row =>
                        {
                            row.RelativeItem(1).Text("BANKA / HESAP:").Bold();
                            row.RelativeItem().Text($"{islem.Banka} - {islem.HesapNo}");
                        });
 
                        // Durum
                        col.Item().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(3).Row(row =>
                        {
                            row.RelativeItem(1).Text("DURUM:").Bold();
                            row.RelativeItem().Text(islem.Durum ?? "-");
                        });
 
                        // Yönlendirilen Tedarikçi (Ciro)
                        if (islem.YonlendirilenCariId.HasValue)
                        {
                            col.Item().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(3).Row(row =>
                            {
                                row.RelativeItem(1).Text("CİRO EDİLEN:").Bold().FontColor("#ca8a04");
                                row.RelativeItem().Text(islem.YonlendirilenCariUnvan ?? "-").FontColor("#ca8a04");
                            });
                        }
 
                        // Açıklama
                        if (!string.IsNullOrEmpty(islem.Aciklama))
                        {
                            col.Item().PaddingTop(3).Column(noteCol =>
                            {
                                noteCol.Item().Text("AÇIKLAMA:").Bold().FontSize(9);
                                noteCol.Item().Text(islem.Aciklama).FontSize(9);
                            });
                        }
 
                        // Dekont Görseli / PDF bilgisi
                        if (!string.IsNullOrEmpty(islem.DekontPath))
                        {
                             var path = islem.DekontPath;
                             var extension = Path.GetExtension(path).ToLower();
                             bool isImage = extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".webp" || extension == ".bmp";

                             if (isImage)
                             {
                                 var imgBytes = LoadImageBytes(path);
                                 col.Item().PaddingTop(10).Column(imgCol =>
                                 {
                                     imgCol.Item().Text("DEKONT GÖRSELİ").Bold().FontSize(9).FontColor("#64748b");
                                     if (imgBytes != null && imgBytes.Length > 0)
                                     {
                                         imgCol.Item().PaddingTop(5).AspectRatio(1.4f).Element(container => 
                                         {
                                             container.AlignCenter().AlignMiddle().Image(imgBytes).FitArea();
                                         });
                                     }
                                     else 
                                     {
                                         imgCol.Item().PaddingTop(3).Column(ec => {
                                             ec.Item().Text($"Görsel Yüklenemedi: {Path.GetFileName(path)}").FontSize(7).Italic();
                                             ec.Item().Text(path).FontSize(5).FontColor("#999999");
                                         });
                                     }
                                 });
                             }
                             else 
                             {
                                 col.Item().PaddingTop(3).Text($"Ek: Dekont dosyası mevcuttur ({Path.GetFileName(path)})").FontSize(9).Italic().FontColor("#64748b");
                             }
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Oluşturulma Tarihi: ");
                        x.Span(DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
                    });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateKrediKartiSlipPdf(KrediKartiIslem islem)
        {
            // License set in Program.cs

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A5);
                    page.Margin(0.5f, Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily(DefaultFontFamily));
 
                     page.Header().Row(row =>
                     {
                         row.RelativeItem(3).Column(col =>
                         {
                             col.Item().Text("KREDİ KARTI SLİP FORMU").FontSize(14).Bold().FontColor("#2563eb");
                             col.Item().Text($"Ref: {islem.OnayKodu}").FontSize(9).FontColor("#444444");
                         });

                         // Kredi kartı slipleri genellikle tahsilat olarak kabul edilir
                         if (ShowLogoTahsilat)
                         {
                             var logoBytes = LoadLogoBytes();
                             if (logoBytes != null && logoBytes.Length > 0)
                             {
                                 row.ConstantItem(120).AlignRight().MaxHeight(30).Image(logoBytes).FitArea();
                             }
                         }
                     });
 
                    page.Content().PaddingVertical(0.4f, Unit.Centimetre).Column(col =>
                    {
                        col.Spacing(4);
 
                        // Müşteri
                        col.Item().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(3).Row(row =>
                        {
                            row.RelativeItem(1).Text("MÜŞTERİ:").Bold();
                            row.RelativeItem().Text(islem.MusteriUnvan ?? "-");
                        });
 
                        // Tarih
                        col.Item().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(3).Row(row =>
                        {
                            row.RelativeItem(1).Text("TARİH:").Bold();
                            row.RelativeItem().Text($"{islem.Tarih:dd.MM.yyyy}");
                        });
 
                        // Tutar
                        col.Item().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(3).Row(row =>
                        {
                            row.RelativeItem(1).Text("TUTAR:").Bold();
                            row.RelativeItem().Text($"{islem.Tutar:N2} TL").FontSize(14).Bold().FontColor("#16a34a");
                        });
 
                        // Banka Bilgileri
                        col.Item().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(3).Row(row =>
                        {
                            row.RelativeItem(1).Text("BANKA / KART:").Bold();
                            row.RelativeItem().Text($"{islem.Banka} - {islem.KartNo}");
                        });
 
                        // Durum
                        col.Item().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(3).Row(row =>
                        {
                            row.RelativeItem(1).Text("DURUM:").Bold();
                            row.RelativeItem().Text(islem.Durum ?? "-");
                        });
 
                        // Yönlendirilen Tedarikçi (Ciro)
                        if (islem.YonlendirilenCariId.HasValue)
                        {
                            col.Item().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(3).Row(row =>
                            {
                                row.RelativeItem(1).Text("CİRO EDİLEN:").Bold().FontColor("#ca8a04");
                                row.RelativeItem().Text(islem.YonlendirilenCariUnvan ?? "-").FontColor("#ca8a04");
                            });
                        }
 
                        // Açıklama
                        if (!string.IsNullOrEmpty(islem.Aciklama))
                        {
                            col.Item().PaddingTop(3).Column(noteCol =>
                            {
                                noteCol.Item().Text("AÇIKLAMA:").Bold().FontSize(9);
                                noteCol.Item().Text(islem.Aciklama).FontSize(9);
                            });
                        }
 
                        if (!string.IsNullOrEmpty(islem.SlipDosyaYolu))
                        {
                            col.Item().PaddingTop(10).Column(imgCol =>
                            {
                                imgCol.Item().Text("SLİP GÖRSELİ").Bold().FontSize(9).FontColor("#64748b");
                                imgCol.Item().PaddingTop(5).AspectRatio(1.4f).Element(container => 
                                {
                                    var bytes = LoadImageBytes(islem.SlipDosyaYolu);
                                    if (bytes != null) container.AlignCenter().AlignMiddle().Image(bytes).FitArea();
                                });
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Oluşturulma Tarihi: ");
                        x.Span(DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
                    });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateCekListPdf(List<Cek> cekler)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(DefaultFontFamily));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("ÇEK / SENET LİSTESİ").FontSize(16).Bold().FontColor("#1e40af");
                            col.Item().Text($"Rapor Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(8).FontColor("#64748b");
                        });
                        
                        var logoBytes = LoadLogoBytes();
                        if (logoBytes != null) row.RelativeItem().AlignRight().MaxWidth(120).MaxHeight(30).Image(logoBytes).FitArea();
                    });

                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(70);  // Vade
                            columns.ConstantColumn(70);  // Portföy No
                            columns.RelativeColumn(3);   // Borçlu
                            columns.RelativeColumn(2);   // Banka
                            columns.ConstantColumn(80);  // Durum
                            columns.ConstantColumn(100); // Tutar
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderStyle).Text("Vade");
                            header.Cell().Element(HeaderStyle).Text("P.No");
                            header.Cell().Element(HeaderStyle).Text("Borçlu / Cari");
                            header.Cell().Element(HeaderStyle).Text("Banka");
                            header.Cell().Element(HeaderStyle).Text("Durum");
                            header.Cell().Element(HeaderStyle).AlignRight().Text("Tutar");

                            static QuestPDF.Infrastructure.IContainer HeaderStyle(QuestPDF.Infrastructure.IContainer container)
                            {
                                return container.Background("#f1f5f9").BorderBottom(1).BorderColor("#cbd5e1").Padding(5).DefaultTextStyle(x => x.Bold());
                            }
                        });

                        foreach (var cek in cekler)
                        {
                            table.Cell().Element(CellStyle).Text(cek.VadeTarihi.ToString("dd.MM.yyyy"));
                            table.Cell().Element(CellStyle).Text(cek.PortfoyNo ?? "-");
                            table.Cell().Element(CellStyle).Text(cek.AsilBorclu ?? cek.CariUnvan ?? "-");
                            table.Cell().Element(CellStyle).Text($"{cek.Banka} {cek.Sube}");
                            table.Cell().Element(CellStyle).Text(cek.Durum ?? "-");
                            table.Cell().Element(CellStyle).AlignRight().Text($"{cek.Tutar:N2} TL").Bold();

                            static QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer container)
                            {
                                return container.BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(5);
                            }
                        }

                        table.Footer(footer =>
                        {
                            footer.Cell().ColumnSpan(5).Padding(5).AlignRight().Text("TOPLAM:").Bold();
                            footer.Cell().Padding(5).AlignRight().Text($"{cekler.Sum(x => x.Tutar):N2} TL").Bold().FontSize(11).FontColor("#1e40af");
                        });
                    });

                    page.Footer().AlignCenter().Text(x => { x.Span("Sayfa "); x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });
            });

            return document.GeneratePdf();
        }


        public byte[] GenerateKrediKartiListPdf(List<KrediKartiIslem> islemler)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(DefaultFontFamily));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("KREDİ KARTI İŞLEMLERİ LİSTESİ").FontSize(16).Bold().FontColor("#1e40af");
                            col.Item().Text($"Rapor Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(8).FontColor("#64748b");
                        });
                        var logoBytes = LoadLogoBytes();
                        if (logoBytes != null) row.RelativeItem().AlignRight().MaxWidth(120).MaxHeight(30).Image(logoBytes).FitArea();
                    });

                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(70);  // Tarih
                            columns.RelativeColumn(3);   // Müşteri
                            columns.RelativeColumn(2);   // Banka
                            columns.ConstantColumn(100); // Kart No
                            columns.ConstantColumn(80);  // Onay Kodu
                            columns.ConstantColumn(100); // Tutar
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderStyle).Text("Tarih");
                            header.Cell().Element(HeaderStyle).Text("Müşteri");
                            header.Cell().Element(HeaderStyle).Text("Banka");
                            header.Cell().Element(HeaderStyle).Text("Kart No");
                            header.Cell().Element(HeaderStyle).Text("Onay Kodu");
                            header.Cell().Element(HeaderStyle).AlignRight().Text("Tutar");

                            static QuestPDF.Infrastructure.IContainer HeaderStyle(QuestPDF.Infrastructure.IContainer container)
                            {
                                return container.Background("#f1f5f9").BorderBottom(1).BorderColor("#cbd5e1").Padding(5).DefaultTextStyle(x => x.Bold());
                            }
                        });

                        foreach (var islem in islemler)
                        {
                            table.Cell().Element(CellStyle).Text(islem.Tarih.ToString("dd.MM.yyyy"));
                            table.Cell().Element(CellStyle).Text(islem.MusteriUnvan ?? "-");
                            table.Cell().Element(CellStyle).Text(islem.Banka ?? "-");
                            table.Cell().Element(CellStyle).Text(islem.KartNo ?? "-");
                            table.Cell().Element(CellStyle).Text(islem.OnayKodu ?? "-");
                            table.Cell().Element(CellStyle).AlignRight().Text($"{islem.Tutar:N2} TL").Bold();

                            static QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer container)
                            {
                                return container.BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(5);
                            }
                        }

                        table.Footer(footer =>
                        {
                            footer.Cell().ColumnSpan(5).Padding(5).AlignRight().Text("TOPLAM:").Bold();
                            footer.Cell().Padding(5).AlignRight().Text($"{islemler.Sum(x => x.Tutar):N2} TL").Bold().FontSize(11).FontColor("#1e40af");
                        });
                    });

                    page.Footer().AlignCenter().Text(x => { x.Span("Sayfa "); x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateEftListPdf(List<EftIslem> islemler)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(DefaultFontFamily));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("HAVALE / EFT İŞLEMLERİ LİSTESİ").FontSize(16).Bold().FontColor("#1e40af");
                            col.Item().Text($"Rapor Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(8).FontColor("#64748b");
                        });
                        var logoBytes = LoadLogoBytes();
                        if (logoBytes != null) row.RelativeItem().AlignRight().MaxWidth(120).MaxHeight(30).Image(logoBytes).FitArea();
                    });

                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(70);  // Tarih
                            columns.RelativeColumn(3);   // Müşteri
                            columns.RelativeColumn(2);   // Banka
                            columns.ConstantColumn(100); // Hesap No
                            columns.ConstantColumn(80);  // Dekont No
                            columns.ConstantColumn(100); // Tutar
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderStyle).Text("Tarih");
                            header.Cell().Element(HeaderStyle).Text("Müşteri");
                            header.Cell().Element(HeaderStyle).Text("Banka");
                            header.Cell().Element(HeaderStyle).Text("Hesap No");
                            header.Cell().Element(HeaderStyle).Text("Dekont No");
                            header.Cell().Element(HeaderStyle).AlignRight().Text("Tutar");

                            static QuestPDF.Infrastructure.IContainer HeaderStyle(QuestPDF.Infrastructure.IContainer container)
                            {
                                return container.Background("#f1f5f9").BorderBottom(1).BorderColor("#cbd5e1").Padding(5).DefaultTextStyle(x => x.Bold());
                            }
                        });

                        foreach (var islem in islemler)
                        {
                            table.Cell().Element(CellStyle).Text(islem.Tarih.ToString("dd.MM.yyyy"));
                            table.Cell().Element(CellStyle).Text(islem.MusteriUnvan ?? "-");
                            table.Cell().Element(CellStyle).Text(islem.Banka ?? "-");
                            table.Cell().Element(CellStyle).Text(islem.HesapNo ?? "-");
                            table.Cell().Element(CellStyle).Text(islem.DekontNo ?? "-");
                            table.Cell().Element(CellStyle).AlignRight().Text($"{islem.Tutar:N2} TL").Bold();

                            static QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer container)
                            {
                                return container.BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(5);
                            }
                        }

                        table.Footer(footer =>
                        {
                            footer.Cell().ColumnSpan(5).Padding(5).AlignRight().Text("TOPLAM:").Bold();
                            footer.Cell().Padding(5).AlignRight().Text($"{islemler.Sum(x => x.Tutar):N2} TL").Bold().FontSize(11).FontColor("#1e40af");
                        });
                    });

                    page.Footer().AlignCenter().Text(x => { x.Span("Sayfa "); x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateKasaListesiPdf(List<BankaKart> kasalar)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(DefaultFontFamily));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("KASA LİSTESİ VE BAKİYELERİ").FontSize(16).Bold().FontColor("#1e40af");
                            col.Item().Text($"Rapor Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(8).FontColor("#64748b");
                        });
                        var logoBytes = LoadLogoBytes();
                        if (logoBytes != null) row.RelativeItem().AlignRight().MaxWidth(120).MaxHeight(30).Image(logoBytes).FitArea();
                    });

                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4);   // Kasa Adı
                            columns.RelativeColumn(3);   // Yetkili
                            columns.ConstantColumn(60);  // Döviz
                            columns.RelativeColumn(3);   // Bakiye
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderStyle).Text("Kasa Adı");
                            header.Cell().Element(HeaderStyle).Text("Yetkili");
                            header.Cell().Element(HeaderStyle).AlignCenter().Text("Döviz");
                            header.Cell().Element(HeaderStyle).AlignRight().Text("Bakiye");

                            static QuestPDF.Infrastructure.IContainer HeaderStyle(QuestPDF.Infrastructure.IContainer container)
                                => container.Background("#f1f5f9").BorderBottom(1).BorderColor("#cbd5e1").Padding(5).DefaultTextStyle(x => x.Bold());
                        });

                        foreach (var kasa in kasalar)
                        {
                            table.Cell().Element(CellStyle).Text(kasa.BankaAdi ?? "-");
                            table.Cell().Element(CellStyle).Text(kasa.Yetkili ?? "-");
                            table.Cell().Element(CellStyle).AlignCenter().Text(kasa.DovizTuru ?? "TL");
                            table.Cell().Element(CellStyle).AlignRight().Text($"{kasa.GuncelBakiye:N2}").Bold();

                            static QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer container)
                                => container.BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(5);
                        }
                    });

                    page.Footer().AlignCenter().Text(x => { x.Span("Sayfa "); x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateCariEkstrePdf(CariKart cari, List<CariHareket> hareketler)
        {
            var trCulture = new System.Globalization.CultureInfo("tr-TR");
            var orderedHareketler = hareketler.OrderBy(h => h.Tarih).ThenBy(h => h.Id).ToList();
            
            var runningBalance = 0m;
            var rows = new List<dynamic>();
            foreach(var h in orderedHareketler)
            {
               runningBalance += (decimal)(h.Borc - h.Alacak);
               rows.Add(new { Hareket = h, Bakiye = runningBalance });
            }

            var toplamBorc = hareketler.Sum(h => h.Borc);
            var toplamAlacak = hareketler.Sum(h => h.Alacak);
            var netBakiye = toplamBorc - toplamAlacak;

            // Color constants (Detaylı Ekstre ile aynı)
            var ColorRed = "#cc0000";
            var ColorGreen = "#008000";
            var ColorHeaderBg = "#f9f9f9";
            var ColorBorder = "#eeeeee";
            var ColorMuted = "#333333";

            string FormatCurrency(decimal amount) => $"₺{amount:N2}";

            string GetPaymentMethodLabel(string? aciklama)
            {
                if (string.IsNullOrEmpty(aciklama)) return "-";
                var upper = aciklama.ToUpper(trCulture);
                if (upper.Contains("NAKİT") || upper.Contains("NAKIT")) return "Nakit";
                if (upper.Contains("KREDİ") || upper.Contains("KREDI")) return "Kredi Kartı";
                if (upper.Contains("HAVALE") || upper.Contains("EFT")) return "Havale";
                if (upper.Contains("ÇEK") || upper.Contains("CEK")) return "Çek";
                if (upper.Contains("BANKA")) return "Banka Havalesi";
                return "-";
            }

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(GetPageSize(EkstreSize, EkstreOrientation));
                    page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(DefaultFontFamily));

                    // CONTENT
                    page.Content().Column(column =>
                    {
                        // HEADER INFO (FIRST PAGE ONLY)
                        if (ShowLogoEkstre)
                        {
                            var logoBytes = LoadLogoBytes();
                            if (logoBytes != null && logoBytes.Length > 0)
                            {
                                column.Item().PaddingBottom(10).Row(row => 
                                {
                                    row.ConstantItem(120).MaxHeight(50).Image(logoBytes).FitArea();
                                });
                            }
                        }


                        column.Item().PaddingTop(10);

                        // GENEL BİLGİLER CARD
                        column.Item().Border(0.5f).BorderColor("#dddddd").Padding(10).Column(card =>
                        {
                            card.Item().Text("Genel Bilgiler").FontSize(11).SemiBold();
                            card.Item().PaddingTop(8);

                            card.Item().Table(infoTable =>
                            {
                                infoTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(2);
                                });

                                // Müşteri Adı
                                infoTable.Cell().Padding(2).Text("Müşteri Adı:").FontSize(9).Bold();
                                infoTable.Cell().Padding(2).Text(cari.Unvan ?? "-").FontSize(9);

                                // Toplam Satış
                                infoTable.Cell().Padding(2).Text("Toplam Satış:").FontSize(9).Bold();
                                infoTable.Cell().Padding(2).Text(FormatCurrency(toplamBorc)).FontSize(9);

                                // Toplam Ödeme
                                infoTable.Cell().Padding(2).Text("Toplam Ödeme:").FontSize(9).Bold();
                                infoTable.Cell().Padding(2).Text(FormatCurrency(toplamAlacak)).FontSize(9);

                                // Bakiye (renkli)
                                var balanceColor = netBakiye > 0 ? ColorRed : ColorGreen;
                                var balanceLabel = netBakiye > 0 ? " (Borçlu)" : " (Alacaklı)";

                                infoTable.Cell().Padding(2).Text("Bakiye:").FontSize(9).Bold();
                                infoTable.Cell().Padding(2).Text(text =>
                                {
                                    text.Span(FormatCurrency(netBakiye))
                                        .FontSize(9).Bold().FontColor(balanceColor);
                                    text.Span(balanceLabel)
                                        .FontSize(9).Bold().FontColor(balanceColor);
                                });
                            });
                        });

                        column.Item().PaddingTop(10);

                        // İŞLEM DETAYLARI
                        column.Item().Border(0.5f).BorderColor("#dddddd").Column(card =>
                        {
                            card.Item().Padding(10).Text("İşlem Detayları").FontSize(11).SemiBold();

                            card.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(12);  // Tarih
                                    columns.RelativeColumn(10);  // İşlem Tipi
                                    columns.RelativeColumn(38);  // Açıklama
                                    columns.RelativeColumn(13);  // Borç
                                    columns.RelativeColumn(13);  // Alacak
                                    columns.RelativeColumn(14);  // Bakiye
                                });

                                // Header
                                table.Header(header =>
                                {
                                    header.Cell().Background(ColorHeaderBg).Border(0.5f).BorderColor(ColorBorder).Padding(4).Text("Tarih").FontSize(8).Bold();
                                    header.Cell().Background(ColorHeaderBg).Border(0.5f).BorderColor(ColorBorder).Padding(4).Text("İşlem Tipi").FontSize(8).Bold();
                                    header.Cell().Background(ColorHeaderBg).Border(0.5f).BorderColor(ColorBorder).Padding(4).Text("Açıklama").FontSize(8).Bold();

                                    header.Cell().Background(ColorHeaderBg).Border(0.5f).BorderColor(ColorBorder).Padding(4).AlignRight().Text("Borç").FontSize(8).Bold();
                                    header.Cell().Background(ColorHeaderBg).Border(0.5f).BorderColor(ColorBorder).Padding(4).AlignRight().Text("Alacak").FontSize(8).Bold();
                                    header.Cell().Background(ColorHeaderBg).Border(0.5f).BorderColor(ColorBorder).Padding(4).AlignRight().Text("Bakiye").FontSize(8).Bold();
                                });

                                if (!rows.Any())
                                {
                                    table.Cell().ColumnSpan(6).Padding(10).AlignCenter()
                                        .Text("Bu müşteri için herhangi bir işlem bulunamadı.")
                                        .FontSize(8).FontColor(ColorMuted);
                                }
                                else
                                {
                                    foreach (var row in rows)
                                    {
                                        var h = (CariHareket)row.Hareket;
                                        decimal bak = (decimal)row.Bakiye;

                                        bool isSale = h.Borc > 0;

                                        // Tarih
                                        table.Cell().Border(0.5f).BorderColor(ColorBorder).Padding(3)
                                            .Text(h.Tarih.ToString("dd.MM.yyyy", trCulture)).FontSize(8);

                                        // İşlem Tipi Badge
                                        string rawTuru = (h.IslemTuru ?? "").ToUpper(trCulture);
                                        string badgeText = h.IslemTuru ?? "İşlem";
                                        if (rawTuru.Contains("FATURA") || rawTuru.Contains("SATIŞ") || rawTuru.Contains("SATIS")) badgeText = "Satış";
                                        else if (rawTuru.Contains("ÖDEME") || rawTuru.Contains("ODEME")) badgeText = "Ödeme";
                                        else if (rawTuru.Contains("TAHSİLAT") || rawTuru.Contains("TAHSILAT")) badgeText = "Tahsilat";
                                        else if (rawTuru.Contains("ALIŞ") || rawTuru.Contains("ALIS")) badgeText = "Alış";

                                        table.Cell().Border(0.5f).BorderColor(ColorBorder).Padding(3)
                                            .AlignLeft().AlignMiddle()
                                            .Element(e => ComposeBadge(e, rawTuru, badgeText));

                                        // Açıklama
                                        string method = !isSale ? GetPaymentMethodLabel(h.Aciklama) : "-";
                                        string desc = h.Aciklama ?? "-";
                                        if (method != "-")
                                        {
                                            desc = $"{desc} ({method})";
                                        }
                                        table.Cell().Border(0.5f).BorderColor(ColorBorder).Padding(3)
                                            .Text(desc).FontSize(8);

                                        // Borç (kırmızı bold)
                                        if (isSale)
                                        {
                                            table.Cell().Border(0.5f).BorderColor(ColorBorder).Padding(3)
                                                .AlignRight()
                                                .Text(FormatCurrency(h.Borc))
                                                .FontSize(8).Bold().FontColor(ColorRed);
                                        }
                                        else
                                        {
                                            table.Cell().Border(0.5f).BorderColor(ColorBorder).Padding(3)
                                                .AlignRight().Text("-").FontSize(8);
                                        }

                                        // Alacak (yeşil bold)
                                        if (!isSale)
                                        {
                                            table.Cell().Border(0.5f).BorderColor(ColorBorder).Padding(3)
                                                .AlignRight()
                                                .Text(FormatCurrency(h.Alacak))
                                                .FontSize(8).Bold().FontColor(ColorGreen);
                                        }
                                        else
                                        {
                                            table.Cell().Border(0.5f).BorderColor(ColorBorder).Padding(3)
                                                .AlignRight().Text("-").FontSize(8);
                                        }

                                        // Bakiye (bold)
                                        table.Cell().Border(0.5f).BorderColor(ColorBorder).Padding(3)
                                            .AlignRight()
                                            .Text(FormatCurrency(bak))
                                            .FontSize(8).Bold();
                                    }
                                }
                            });
                        });
                    });

                    // FOOTER: Sayfa numarası ortada
                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Sayfa ").FontSize(7).FontColor(ColorMuted);
                        text.CurrentPageNumber().FontSize(7).FontColor(ColorMuted);
                        text.Span(" / ").FontSize(7).FontColor(ColorMuted);
                        text.TotalPages().FontSize(7).FontColor(ColorMuted);
                    });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateDetayliCariEkstrePdf(CariKart cari, List<CariHareket> hareketler, Dictionary<int, List<FaturaDetay>> faturaDetaylari)
        {
            var trCulture = new System.Globalization.CultureInfo("tr-TR");
            var orderedHareketler = hareketler.OrderBy(h => h.Tarih).ThenBy(h => h.Id).ToList();
            
            var runningBalance = 0m;
            var rows = new List<dynamic>();
            foreach(var h in orderedHareketler)
            {
               runningBalance += (decimal)(h.Borc - h.Alacak);
               rows.Add(new { Hareket = h, Bakiye = runningBalance });
            }

            var toplamBorc = hareketler.Sum(h => h.Borc);
            var toplamAlacak = hareketler.Sum(h => h.Alacak);
            var netBakiye = toplamBorc - toplamAlacak;

            // Color constants
            var ColorRed = "#cc0000";
            var ColorGreen = "#008000";
            var ColorHeaderBg = "#f9f9f9";
            var ColorBorder = "#eeeeee";
            var ColorMuted = "#333333";

            string FormatCurrency(decimal amount) => $"₺{amount:N2}";

            string GetPaymentMethodLabel(string? aciklama)
            {
                if (string.IsNullOrEmpty(aciklama)) return "-";
                var upper = aciklama.ToUpper(trCulture);
                if (upper.Contains("NAKİT") || upper.Contains("NAKIT")) return "Nakit";
                if (upper.Contains("KREDİ") || upper.Contains("KREDI")) return "Kredi Kartı";
                if (upper.Contains("HAVALE") || upper.Contains("EFT")) return "Havale";
                if (upper.Contains("ÇEK") || upper.Contains("CEK")) return "Çek";
                if (upper.Contains("BANKA")) return "Banka Havalesi";
                return "-";
            }

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(GetPageSize(EkstreSize, EkstreOrientation));
                    page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(DefaultFontFamily));

                    // CONTENT
                    page.Content().Column(column =>
                    {
                        // HEADER INFO (FIRST PAGE ONLY)
                        if (ShowLogoEkstre)
                        {
                            var logoBytes = LoadLogoBytes();
                            if (logoBytes != null && logoBytes.Length > 0)
                            {
                                column.Item().PaddingBottom(10).Row(row => 
                                {
                                    row.ConstantItem(120).MaxHeight(50).Image(logoBytes).FitArea();
                                });
                            }
                        }


                        column.Item().PaddingTop(10);

                        // GENEL BİLGİLER CARD
                        column.Item().Border(0.5f).BorderColor("#dddddd").Padding(10).Column(card =>
                        {
                            card.Item().Text("Genel Bilgiler").FontSize(11).SemiBold();
                            card.Item().PaddingTop(8);

                            card.Item().Table(infoTable =>
                            {
                                infoTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(2);
                                });

                                // Müşteri Adı
                                infoTable.Cell().Padding(2).Text("Müşteri Adı:").FontSize(9).Bold();
                                infoTable.Cell().Padding(2).Text(cari.Unvan ?? "-").FontSize(9);

                                // Toplam Satış
                                infoTable.Cell().Padding(2).Text("Toplam Satış:").FontSize(9).Bold();
                                infoTable.Cell().Padding(2).Text(FormatCurrency(toplamBorc)).FontSize(9);

                                // Toplam Ödeme
                                infoTable.Cell().Padding(2).Text("Toplam Ödeme:").FontSize(9).Bold();
                                infoTable.Cell().Padding(2).Text(FormatCurrency(toplamAlacak)).FontSize(9);

                                // Bakiye (renkli)
                                var balanceColor = netBakiye > 0 ? ColorRed : ColorGreen;
                                var balanceLabel = netBakiye > 0 ? " (Borçlu)" : " (Alacaklı)";

                                infoTable.Cell().Padding(2).Text("Bakiye:").FontSize(9).Bold();
                                infoTable.Cell().Padding(2).Text(text =>
                                {
                                    text.Span(FormatCurrency(netBakiye))
                                        .FontSize(9).Bold().FontColor(balanceColor);
                                    text.Span(balanceLabel)
                                        .FontSize(9).Bold().FontColor(balanceColor);
                                });
                            });
                        });

                        column.Item().PaddingTop(10);

                        // İŞLEM DETAYLARI
                        column.Item().Border(0.5f).BorderColor("#dddddd").Column(card =>
                        {
                            card.Item().Padding(10).Text("İşlem Detayları").FontSize(11).SemiBold();

                            card.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(12);  // Tarih
                                    columns.RelativeColumn(10);  // İşlem Tipi
                                    columns.RelativeColumn(38);  // Açıklama
                                    columns.RelativeColumn(13);  // Borç
                                    columns.RelativeColumn(13);  // Alacak
                                    columns.RelativeColumn(14);  // Bakiye
                                });

                                // Header
                                table.Header(header =>
                                {
                                    header.Cell().Background(ColorHeaderBg).Border(0.5f).BorderColor(ColorBorder).Padding(4).Text("Tarih").FontSize(8).Bold();
                                    header.Cell().Background(ColorHeaderBg).Border(0.5f).BorderColor(ColorBorder).Padding(4).Text("İşlem Tipi").FontSize(8).Bold();
                                    header.Cell().Background(ColorHeaderBg).Border(0.5f).BorderColor(ColorBorder).Padding(4).Text("Açıklama").FontSize(8).Bold();

                                    header.Cell().Background(ColorHeaderBg).Border(0.5f).BorderColor(ColorBorder).Padding(4).AlignRight().Text("Borç").FontSize(8).Bold();
                                    header.Cell().Background(ColorHeaderBg).Border(0.5f).BorderColor(ColorBorder).Padding(4).AlignRight().Text("Alacak").FontSize(8).Bold();
                                    header.Cell().Background(ColorHeaderBg).Border(0.5f).BorderColor(ColorBorder).Padding(4).AlignRight().Text("Bakiye").FontSize(8).Bold();
                                });

                                if (!rows.Any())
                                {
                                    table.Cell().ColumnSpan(6).Padding(10).AlignCenter()
                                        .Text("Bu müşteri için herhangi bir işlem bulunamadı.")
                                        .FontSize(8).FontColor(ColorMuted);
                                }
                                else
                                {
                                    foreach (var row in rows)
                                    {
                                        var h = (CariHareket)row.Hareket;
                                        decimal bak = (decimal)row.Bakiye;

                                        bool isSale = h.Borc > 0;

                                        // Tarih
                                        table.Cell().Border(0.5f).BorderColor(ColorBorder).Padding(3)
                                            .Text(h.Tarih.ToString("dd.MM.yyyy", trCulture)).FontSize(8);

                                        // İşlem Tipi Badge
                                        string rawTuru = (h.IslemTuru ?? "").ToUpper(trCulture);
                                        string badgeText = h.IslemTuru ?? "İşlem";
                                        if (rawTuru.Contains("FATURA") || rawTuru.Contains("SATIŞ") || rawTuru.Contains("SATIS")) badgeText = "Satış";
                                        else if (rawTuru.Contains("ÖDEME") || rawTuru.Contains("ODEME")) badgeText = "Ödeme";
                                        else if (rawTuru.Contains("TAHSİLAT") || rawTuru.Contains("TAHSILAT")) badgeText = "Tahsilat";
                                        else if (rawTuru.Contains("ALIŞ") || rawTuru.Contains("ALIS")) badgeText = "Alış";

                                        table.Cell().Border(0.5f).BorderColor(ColorBorder).Padding(3)
                                            .AlignLeft().AlignMiddle()
                                            .Element(e => ComposeBadge(e, rawTuru, badgeText));

                                        // Açıklama
                                        string method = !isSale ? GetPaymentMethodLabel(h.Aciklama) : "-";
                                        string desc = h.Aciklama ?? "-";
                                        if (method != "-")
                                        {
                                            desc = $"{desc} ({method})";
                                        }
                                        table.Cell().Border(0.5f).BorderColor(ColorBorder).Padding(3)
                                            .Text(desc).FontSize(8);

                                        // Borç (kırmızı bold)
                                        if (isSale)
                                        {
                                            table.Cell().Border(0.5f).BorderColor(ColorBorder).Padding(3)
                                                .AlignRight()
                                                .Text(FormatCurrency(h.Borc))
                                                .FontSize(8).Bold().FontColor(ColorRed);
                                        }
                                        else
                                        {
                                            table.Cell().Border(0.5f).BorderColor(ColorBorder).Padding(3)
                                                .AlignRight().Text("-").FontSize(8);
                                        }

                                        // Alacak (yeşil bold)
                                        if (!isSale)
                                        {
                                            table.Cell().Border(0.5f).BorderColor(ColorBorder).Padding(3)
                                                .AlignRight()
                                                .Text(FormatCurrency(h.Alacak))
                                                .FontSize(8).Bold().FontColor(ColorGreen);
                                        }
                                        else
                                        {
                                            table.Cell().Border(0.5f).BorderColor(ColorBorder).Padding(3)
                                                .AlignRight().Text("-").FontSize(8);
                                        }

                                        // Bakiye (bold)
                                        table.Cell().Border(0.5f).BorderColor(ColorBorder).Padding(3)
                                            .AlignRight()
                                            .Text(FormatCurrency(bak))
                                            .FontSize(8).Bold();

                                         // Fatura detayları (alt tablo)
                                         if (h.FaturaId.HasValue && faturaDetaylari.ContainsKey(h.FaturaId.Value))
                                         {
                                             var details = faturaDetaylari[h.FaturaId.Value];
                                             if (details != null && details.Count > 0)
                                             {
                                                 table.Cell().ColumnSpan(6).PaddingLeft(25).PaddingRight(15).PaddingBottom(8).Column(c => {
                                                     c.Item().Table(subTable => {
                                                         subTable.ColumnsDefinition(cols => {
                                                             cols.RelativeColumn(4.2f); // Stok Kodu
                                                             cols.RelativeColumn(4.0f); // Stok Adı
                                                             cols.RelativeColumn(1.0f); // Miktar
                                                             cols.RelativeColumn(1.0f); // Birimi
                                                             cols.RelativeColumn(1.8f); // Birim Fiyat
                                                             cols.RelativeColumn(1.0f); // KDV
                                                             cols.RelativeColumn(2.0f); // Tutar
                                                         });

                                                         // Sub-Header
                                                         subTable.Header(header => {
                                                             header.Cell().BorderBottom(0.5f).BorderColor("#999999").Padding(1).Text("Stok Kodu").FontSize(6).Bold().FontColor("#666666");
                                                             header.Cell().BorderBottom(0.5f).BorderColor("#999999").Padding(1).Text("Stok Adı").FontSize(6).Bold().FontColor("#666666");
                                                             header.Cell().BorderBottom(0.5f).BorderColor("#999999").Padding(1).AlignRight().Text("Miktar").FontSize(6).Bold().FontColor("#666666");
                                                             header.Cell().BorderBottom(0.5f).BorderColor("#999999").Padding(1).AlignCenter().Text("Birimi").FontSize(6).Bold().FontColor("#666666");
                                                             header.Cell().BorderBottom(0.5f).BorderColor("#999999").Padding(1).AlignRight().Text("Birim Fiyat").FontSize(6).Bold().FontColor("#666666");
                                                             header.Cell().BorderBottom(0.5f).BorderColor("#999999").Padding(1).AlignCenter().Text("KDV").FontSize(6).Bold().FontColor("#666666");
                                                             header.Cell().BorderBottom(0.5f).BorderColor("#999999").Padding(1).AlignRight().Text("Tutar").FontSize(6).Bold().FontColor("#666666");
                                                         });

                                                         foreach(var d in details) {
                                                             // Veritabanındaki tersliği düzeltmek için StokAdi ile StokKodu alanlarını yer değiştiriyoruz
                                                             subTable.Cell().BorderBottom(0.2f).BorderColor(ColorBorder).Padding(2).Text(d.StokAdi ?? "-").FontSize(7f);
                                                             subTable.Cell().BorderBottom(0.2f).BorderColor(ColorBorder).Padding(2).Text(d.StokKodu ?? "Ürün").FontSize(7f);
                                                             subTable.Cell().BorderBottom(0.2f).BorderColor(ColorBorder).Padding(2).AlignRight().Text($"{d.Miktar:N1}").FontSize(7f);
                                                             subTable.Cell().BorderBottom(0.2f).BorderColor(ColorBorder).Padding(2).AlignCenter().Text(d.Birim ?? "").FontSize(6.5f);
                                                             subTable.Cell().BorderBottom(0.2f).BorderColor(ColorBorder).Padding(2).AlignRight().Text(FormatCurrency(d.BirimFiyat)).FontSize(7f);
                                                             subTable.Cell().BorderBottom(0.2f).BorderColor(ColorBorder).Padding(2).AlignCenter().Text($"%{d.KDVOrani}").FontSize(6.5f);
                                                             subTable.Cell().BorderBottom(0.2f).BorderColor(ColorBorder).Padding(2).AlignRight().Text(FormatCurrency(d.Tutar)).FontSize(7f).Bold();
                                                         }
                                                     });
                                                 });
                                             }
                                         }
                                    }
                                }
                            });
                        });
                    });

                    // FOOTER: Sayfa numarası ortada
                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Sayfa ").FontSize(7).FontColor(ColorMuted);
                        text.CurrentPageNumber().FontSize(7).FontColor(ColorMuted);
                        text.Span(" / ").FontSize(7).FontColor(ColorMuted);
                        text.TotalPages().FontSize(7).FontColor(ColorMuted);
                    });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateAdresEtiketiPdf(string unvan, string adSoyad, string adres, string ilIlce, string postaKodu, string telefon)
        {
            // License set in Program.cs
            
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    // 10cm x 15cm (~A6)
                    page.Size(QuestPDF.Helpers.PageSizes.A5);
                    page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Content()
                        .Column(x =>
                        {
                            
                            if(!string.IsNullOrEmpty(unvan))
                                x.Item().Text(unvan).Bold().FontSize(16);
                            
                            x.Item().Text(adSoyad).FontSize(14);
                            
                            x.Item().EnsureSpace(10);
                            x.Item().PaddingTop(10).Text(adres);
                            x.Item().Text($"{ilIlce} {postaKodu}");
                            
                            if(!string.IsNullOrEmpty(telefon))
                                x.Item().EnsureSpace(10);
                                x.Item().PaddingTop(10).Text($"Tel: {telefon}");
                                
                            // Gönderici Bilgisi (Footer gibi)
                            x.Item().EnsureSpace(20);
                            x.Item().PaddingTop(20).LineHorizontal(1);
                            x.Item().PaddingTop(5).Text("GÖNDEREN:").FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                        });
                });
            });

            return document.GeneratePdf();
        }


        public byte[] GenerateTeklifPdf(Teklif teklif, List<TeklifDetay> detaylar, CariKart? cari)
        {
            var items = detaylar.Select(x => new StandardPdfItem
            {
                Description = x.StokAdi ?? "",
                Quantity = x.Miktar,
                Unit = string.IsNullOrWhiteSpace(x.Birim) ? "Adet" : x.Birim,
                UnitPrice = x.BirimFiyat,
                TaxRate = x.KdvOrani,
                TotalPrice = x.Tutar
            }).ToList();

            decimal araToplam = detaylar.Sum(x => x.Tutar);
            decimal genelToplam = teklif.GenelToplam;
            decimal kdvTutari = genelToplam - araToplam;

            return GenerateSharedA4Document(
                "Teklif No",
                teklif.TeklifNo ?? "-",
                teklif.Tarih,
                cari?.Unvan ?? teklif.CariUnvan ?? "",
                cari?.Adres ?? "-",
                cari?.VergiNo ?? "",
                items,
                araToplam,
                kdvTutari,
                genelToplam,
                teklif.OdemeBilgisi ?? "",
                "",
                true,
                ShowLogoTeklif,
                TeklifSize,
                TeklifOrientation
            );
        }

        public byte[] GenerateFaturaPdf(Fatura fatura, List<FaturaDetay> detaylar)
        {
            var items = detaylar.Select(x => new StandardPdfItem
            {
                Description = x.StokAdi ?? "",
                Quantity = x.Miktar,
                Unit = x.Birim ?? "kg",
                UnitPrice = x.BirimFiyat,
                TaxRate = x.KDVOrani,
                TotalPrice = x.ToplamTutar,
                Aciklama = x.Aciklama
            }).ToList();

            string cleanedAciklama = fatura.Aciklama ?? "";
            if (!string.IsNullOrEmpty(cleanedAciklama))
            {
                cleanedAciklama = System.Text.RegularExpressions.Regex.Replace(cleanedAciklama, @"(Sipariş Ref:.*?(?:\.|$))", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                cleanedAciklama = System.Text.RegularExpressions.Regex.Replace(cleanedAciklama, @"(Teklif Ref:.*?(?:\.|$))", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                cleanedAciklama = System.Text.RegularExpressions.Regex.Replace(cleanedAciklama, @"\(Siparişten Dönüştürüldü\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                cleanedAciklama = System.Text.RegularExpressions.Regex.Replace(cleanedAciklama, @"\(Tekliften Dönüştürüldü\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                cleanedAciklama = cleanedAciklama.Trim();
            }

            var tasarim = AktifFaturaTasarimi;
            bool showLogo = tasarim?.ShowLogo ?? ShowLogoFatura;
            if (fatura.IsEArsiv) showLogo = true;

            return GenerateSharedA4Document(
                "Fatura No",
                fatura.FaturaNo ?? "-",
                fatura.Tarih,
                fatura.CariUnvan ?? "",
                fatura.Adres ?? "-",
                fatura.VergiNo ?? "",
                items,
                fatura.AraToplam,
                fatura.ToplamKDV,
                fatura.GenelToplam,
                "",
                cleanedAciklama,
                showInfoRows: false,
                showLogo: showLogo,
                size: string.IsNullOrWhiteSpace(tasarim?.FaturaSize) ? FaturaSize : tasarim.FaturaSize,
                orientation: string.IsNullOrWhiteSpace(tasarim?.FaturaOrientation) ? FaturaOrientation : tasarim.FaturaOrientation,
                baslikText: tasarim?.BaslikText,
                altBilgiText: tasarim?.AltBilgi,
                showBirim: tasarim?.ShowBirim ?? true,
                showKdv: tasarim?.ShowKdv ?? true,
                showAraToplam: tasarim?.ShowAraToplam ?? true,
                showAciklama: tasarim?.ShowAciklama ?? true
            );
        }

        public byte[] GenerateFaturaBatchPdf(List<(Fatura Fatura, List<FaturaDetay> Detaylar)> items)
        {
            var document = Document.Create(container =>
            {
                foreach (var item in items)
                {
                    var fatura = item.Fatura;
                    var detaylar = item.Detaylar;
                    
                    container.Page(page =>
                    {
                        page.Size(GetPageSize(FaturaSize, FaturaOrientation));
                        page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                        page.PageColor(QuestPDF.Helpers.Colors.White);
                        
                        page.Content().PaddingVertical(5).Column(col => 
                        {
                            col.Item().Text($"FATURA NO: {fatura.FaturaNo}").FontSize(14).Bold();
                            col.Item().Text($"TARİH: {fatura.Tarih:dd.MM.yyyy}");
                            col.Item().Text($"Müşteri: {fatura.CariUnvan}").Bold();
                            col.Item().PaddingVertical(10).LineHorizontal(0.5f);
                            
                            col.Item().Table(table => {
                                table.ColumnsDefinition(columns => {
                                   columns.RelativeColumn();
                                   columns.ConstantColumn(60);
                                   columns.ConstantColumn(80);
                                });
                                table.Header(header => {
                                   header.Cell().Text("Ürün");
                                   header.Cell().AlignRight().Text("Miktar");
                                   header.Cell().AlignRight().Text("Tutar");
                                });
                                foreach(var d in detaylar) {
                                   table.Cell().Text(d.StokAdi);
                                   table.Cell().AlignRight().Text(d.Miktar.ToString("N1"));
                                   table.Cell().AlignRight().Text(d.ToplamTutar.ToString("N2"));
                                }
                            });
                            
                            col.Item().PaddingTop(10).AlignRight().Text($"TOPLAM: {fatura.GenelToplam:N2} TL").FontSize(12).Bold();
                        });
                    });
                }
            });
            return document.GeneratePdf();
        }

        public byte[] GenerateFaturaListPdf(List<Fatura> faturalar, DateTime startDate, DateTime endDate)
        {
            // License set in Program.cs

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(QuestPDF.Helpers.PageSizes.A5);
                    page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(DefaultFontFamily));

                    page.Header()
                        .Row(row =>
                        {
                            row.RelativeItem(3).Column(col =>
                            {
                                col.Item().Text("FATURA LİSTESİ").FontSize(18).Bold().FontColor("#2563eb");
                                col.Item().Text($"Filtre: {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}").FontSize(10).FontColor("#666666");
                            });

                            row.RelativeItem().AlignRight().Column(c => {
                                var logoBytes = LoadLogoBytes();
                                if (logoBytes != null) c.Item().MaxWidth(60).Image(logoBytes);
                                else c.Item().Text("ERMAY").FontSize(10).Bold();
                            });
                        });

                    page.Content()
                        .PaddingVertical(1, QuestPDF.Infrastructure.Unit.Centimetre)
                        .Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1); // S.No
                                columns.RelativeColumn(3); // Fatura No
                                columns.RelativeColumn(3); // Tarih
                                columns.RelativeColumn(6); // Cari Hesap
                                columns.RelativeColumn(2); // Tür
                                columns.RelativeColumn(3); // Toplam Tutar
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderStyle).Text("S.No");
                                header.Cell().Element(HeaderStyle).Text("Fatura No");
                                header.Cell().Element(HeaderStyle).Text("Tarih");
                                header.Cell().Element(HeaderStyle).Text("Cari Hesap");
                                header.Cell().Element(HeaderStyle).AlignCenter().Text("Tür");
                                header.Cell().Element(HeaderStyle).AlignRight().Text("Tutar");

                                static QuestPDF.Infrastructure.IContainer HeaderStyle(QuestPDF.Infrastructure.IContainer container)
                                {
                                    return container.Background("#f1f5f9").BorderBottom(1).BorderColor("#cbd5e1").Padding(5).DefaultTextStyle(x => x.Bold());
                                }
                            });

                            int index = 1;
                            foreach (var fatura in faturalar)
                            {
                                table.Cell().Element(CellStyle).Text(index++.ToString());
                                table.Cell().Element(CellStyle).Text(fatura.FaturaNo ?? "-");
                                table.Cell().Element(CellStyle).Text(fatura.Tarih.ToString("dd.MM.yyyy"));
                                table.Cell().Element(CellStyle).Text(fatura.CariUnvan ?? "-");
                                table.Cell().Element(CellStyle).AlignCenter().AlignMiddle()
                                    .Element(e => ComposeBadge(e, fatura.Tur ?? "", fatura.Tur ?? "-"));
                                table.Cell().Element(CellStyle).AlignRight().Text(fatura.GenelToplam.ToString("N2") + " ₺");

                                static QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer container)
                                {
                                    return container.BorderBottom(0.5f).BorderColor("#f1f5f9").Padding(5);
                                }
                            }
                            
                            // Footer row for totals
                            table.Cell().ColumnSpan(5).Element(TotalStyle).AlignRight().Text("TOPLAM:");
                            table.Cell().Element(TotalStyle).AlignRight().Text(faturalar.Sum(f => f.GenelToplam).ToString("N2") + " ₺");
                            
                            static QuestPDF.Infrastructure.IContainer TotalStyle(QuestPDF.Infrastructure.IContainer container)
                            {
                                return container.Padding(5).DefaultTextStyle(x => x.Bold());
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Sayfa ");
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                        });
                });
            });

            return document.GeneratePdf();
        }
        public byte[] GenerateGenericTablePdf(string title, string[] headers, List<string[]> rows, string? subtitle = null)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(GetPageSize(RaporSize, GetReportOrientation(title)));
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(8).FontFamily(DefaultFontFamily));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row => 
                        {
                            if (ShowLogoRaporlar)
                            {
                                var logo = LoadLogoBytes();
                                if (logo != null && logo.Length > 0)
                                {
                                    row.ConstantItem(85).Image(logo).FitArea();
                                    row.ConstantItem(15);
                                }
                            }

                            row.RelativeItem().Column(titleCol => 
                            {
                                titleCol.Item().Text(title.ToUpper(new CultureInfo("tr-TR"))).FontSize(16).Bold().FontColor("#1E3A8A");
                                if (!string.IsNullOrEmpty(subtitle))
                                {
                                    titleCol.Item().Text(subtitle).FontSize(9).FontColor("#475569");
                                }
                            });

                            row.ConstantItem(160).AlignRight().Column(metaCol =>
                            {
                                metaCol.Item().Text($"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(8).FontColor("#64748B");
                                metaCol.Item().Text($"Kayıt Sayısı: {rows?.Count ?? 0} Satır").FontSize(8).Bold().FontColor("#1E3A8A");
                            });
                        });

                        // Kurumsal Vurgu Çizgisi
                        col.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor("#2563EB");
                    });

                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        int colCount = headers?.Length ?? 0;
                        if (colCount == 0) return;

                        table.ColumnsDefinition(columns =>
                        {
                            for (int i = 0; i < colCount; i++)
                            {
                                string h = (headers![i] ?? "").Trim().ToLower();
                                if (h == "no" || h == "no." || h == "sıra" || h == "sira" || h == "sıra no" || h == "sira no" || h == "#") 
                                    columns.RelativeColumn(1.2f);
                                else if (h.Contains("tarih") || h.Contains("vade")) 
                                    columns.RelativeColumn(3.5f);
                                else if (h.Contains("durum") || h.Contains("tür") || h.Contains("tur") || h.Contains("tip")) 
                                    columns.RelativeColumn(4f);
                                else if (h.Contains("no")) // like dekont no, fatura no, evrak no
                                    columns.RelativeColumn(4.5f);
                                else if (h.Contains("borç") || h.Contains("borc") || h.Contains("alacak") || h.Contains("bakiye") || h.Contains("tutar") || h.Contains("fiyat") || h.Contains("satış") || h.Contains("satis") || h.Contains("ortalama") || h.Contains("toplam")) 
                                    columns.RelativeColumn(4.5f);
                                else if (h.Contains("miktar") || h.Contains("adet") || h.Contains("sayı") || h.Contains("sayisi") || h.Contains("sayısı")) 
                                    columns.RelativeColumn(3.5f);
                                else if (h.Contains("şehir") || h.Contains("sehir") || h.Contains("il") || h.Contains("ilçe") || h.Contains("ilce"))
                                    columns.RelativeColumn(4f);
                                else 
                                    columns.RelativeColumn(8f); // Açıklama/Unvan/Müşteri/Banka
                            }
                        });

                        table.Header(header =>
                        {
                            for (int i = 0; i < colCount; i++)
                            {
                                var h = headers![i] ?? "";
                                string hLower = h.ToLower();
                                bool isRight = hLower.Contains("borç") || hLower.Contains("borc") || hLower.Contains("alacak") || hLower.Contains("bakiye") || hLower.Contains("tutar") || hLower.Contains("fiyat") || hLower.Contains("ortalama") || hLower.Contains("toplam") || hLower.Contains("miktar") || hLower.Contains("adet") || hLower.Contains("sayı") || hLower.Contains("sayisi") || hLower.Contains("sayısı");
                                bool isCenter = hLower == "no" || hLower == "no." || hLower == "sıra" || hLower == "sira" || hLower == "sıra no" || hLower == "sira no" || hLower == "#" || hLower.Contains("tarih") || hLower.Contains("vade");

                                var cell = header.Cell().Background("#1e3a8a").PaddingVertical(6).PaddingHorizontal(6);
                                if (isRight)
                                    cell.AlignRight().Text(h).FontSize(8).Bold().FontColor("#ffffff");
                                else if (isCenter)
                                    cell.AlignCenter().Text(h).FontSize(8).Bold().FontColor("#ffffff");
                                else
                                    cell.AlignLeft().Text(h).FontSize(8).Bold().FontColor("#ffffff");
                            }
                        });

                        // Satırlar
                        for (int i = 0; i < rows.Count; i++)
                        {
                            var rowData = rows[i];
                            string bgColor = i % 2 == 0 ? "#ffffff" : "#f8fafc";
                            
                            // Check if this row is a total row
                            bool isTotalRow = false;
                            for (int j = 0; j < colCount; j++)
                            {
                                string cellData = j < rowData.Length ? rowData[j] : "";
                                if (cellData != null && cellData.ToLower().Contains("toplam"))
                                {
                                    isTotalRow = true;
                                    break;
                                }
                            }

                            for (int j = 0; j < colCount; j++)
                            {
                                string cellData = j < rowData.Length ? rowData[j] : "";
                                string h = headers![j].ToLower();
                                bool isRight = h.Contains("borç") || h.Contains("borc") || h.Contains("alacak") || h.Contains("bakiye") || h.Contains("tutar") || h.Contains("fiyat") || h.Contains("ortalama") || h.Contains("toplam") || h.Contains("miktar") || h.Contains("adet") || h.Contains("sayı") || h.Contains("sayisi") || h.Contains("sayısı");
                                bool isCenter = h == "no" || h == "no." || h == "sıra" || h == "sira" || h == "sıra no" || h == "sira no" || h == "#" || h.Contains("tarih") || h.Contains("vade");
                                
                                IContainer cell = table.Cell();
                                if (isTotalRow)
                                {
                                    cell = cell.Background("#f1f5f9").PaddingVertical(6).PaddingHorizontal(6).BorderTop(1f).BorderColor("#cbd5e1").BorderBottom(1.5f).BorderColor("#94a3b8");
                                }
                                else
                                {
                                    cell = cell.Background(bgColor).PaddingVertical(5).PaddingHorizontal(6).BorderBottom(0.5f).BorderColor("#e2e8f0");
                                }

                                if (isRight)
                                {
                                    var txt = cell.AlignRight().Text(cellData ?? "").FontSize(8);
                                    if (isTotalRow) txt.Bold();
                                }
                                else if (isCenter)
                                {
                                    var txt = cell.AlignCenter().Text(cellData ?? "").FontSize(8);
                                    if (isTotalRow) txt.Bold();
                                }
                                else
                                {
                                    var txt = cell.AlignLeft().Text(cellData ?? "").FontSize(8);
                                    if (isTotalRow) txt.Bold();
                                }
                            }
                        }
                    });

                    page.Footer().PaddingTop(6).BorderTop(0.5f).BorderColor("#CBD5E1").Row(fRow =>
                    {
                        fRow.RelativeItem().AlignLeft().Text("VK Ön Muhasebe Yönetim Sistemi").FontSize(7).FontColor("#94A3B8");
                        fRow.RelativeItem().AlignRight().Text(x =>
                        {
                            x.DefaultTextStyle(s => s.FontSize(7).FontColor("#94A3B8"));
                            x.Span("Sayfa ");
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                        });
                    });
                });
            });

            try 
            {
                // Report models moved to IPdfService.cs
                return document.GeneratePdf();
            }
            catch (Exception ex)
            {
                int firstRowLen = (rows != null && rows.Count > 0) ? rows[0].Length : 0;
                throw new Exception($"PDF Generation Failed (Table): Headers={headers?.Length}, Rows={rows?.Count}, FirstRowLen={firstRowLen}: {ex.Message}", ex);
            }
        }

        // Models moved to IPdfService.cs

        public byte[] GenerateConsolidatedReportPdf(string title, List<ReportSection> sections)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(GetPageSize(RaporSize, GetReportOrientation(title)));
                    page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(DefaultFontFamily));

                    // HEADER
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            if (ShowLogoRaporlar)
                            {
                                var logo = LoadLogoBytes();
                                if (logo != null && logo.Length > 0) col.Item().MaxWidth(100).Image(logo);
                            }
                        });

                        row.RelativeItem().AlignRight().Text(text =>
                        {
                            text.Span($"FINANCIAL REPORT {DateTime.Now.Year}").FontSize(8).FontColor("#64748b");
                            text.Span(" | ").FontSize(8).FontColor("#64748b");
                            text.Span("Ermay Muhasebe").FontSize(8).FontColor("#64748b");
                        });
                    });

                    // CONTENT
                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Spacing(15);

                        foreach (var section in sections)
                        {
                            if (section.NewPage) col.Item().PageBreak();

                            col.Item().Column(secCol =>
                            {
                                secCol.Spacing(10);

                                // Title and Desc
                                secCol.Item().Text(section.Title.ToUpper()).FontSize(18).Bold().FontColor("#1e3a8a");
                                secCol.Item().Text("Kurumsal performans göstergelerimiz ve detaylı veri tablolarımız aşağıda sunulmuştur.").FontSize(9).FontColor("#475569");

                                // Key Metrics
                                var metricsToDraw = section.KeyMetrics ?? (section.ChartData?.Count <= 4 ? section.ChartData : null);
                                if (metricsToDraw != null && metricsToDraw.Any())
                                {
                                    secCol.Item().Row(row =>
                                    {
                                        row.Spacing(10);
                                        foreach(var metric in metricsToDraw.Take(4))
                                        {
                                            row.RelativeItem().Background("#f8fafc").Border(0.8f).BorderColor("#cbd5e1").Padding(12).Column(mCol =>
                                            {
                                                mCol.Item().Text(metric.Label.ToUpper()).FontSize(7).Bold().FontColor("#64748b");
                                                mCol.Item().PaddingTop(2).Text(string.Format("₺{0:N0}", metric.Actual)).FontSize(12).ExtraBold().FontColor("#1e3a8a");
                                                if (metric.Target > 0)
                                                {
                                                    decimal pct = (metric.Actual / metric.Target) * 100;
                                                    mCol.Item().Text($"% {pct:N0} Hedef").FontSize(6).Bold().FontColor(pct >= 100 ? "#16a34a" : "#ca8a04");
                                                }
                                            });
                                        }
                                    });
                                }

                                // Charts
                                bool hasBar = section.ChartData != null && section.ChartData.Any();
                                bool hasSingle = section.SingleBarData != null && section.SingleBarData.Any();
                                bool hasHoriz = section.HorizontalBarData != null && section.HorizontalBarData.Any();

                                 if (hasBar || hasSingle || hasHoriz || section.ExtraCharts.Any())
                                {
                                    if (section.ExtraCharts.Any())
                                    {
                                        secCol.Item().PaddingBottom(10).Table(table =>
                                        {
                                            table.ColumnsDefinition(columns =>
                                            {
                                                columns.RelativeColumn();
                                                columns.RelativeColumn();
                                            });

                                            foreach (var chart in section.ExtraCharts)
                                            {
                                                table.Cell().PaddingRight(10).PaddingBottom(25).Column(c =>
                                                {
                                                    c.Item().PaddingBottom(3).Text(chart.Title.ToUpper()).FontSize(9).Bold().FontColor("#1e40af");
                                                    byte[]? img = null;
                                                    if (chart.SingleData != null)
                                                        img = ChartHelper.GetSingleBarChartImage(400, 250, chart.SingleData, "", chart.Color);
                                                    else if (chart.BarData != null)
                                                        img = ChartHelper.GetBarChartImage(400, 250, chart.BarData, "", chart.ActualLabel, chart.TargetLabel);
                                                    
                                                    if (img != null) c.Item().Image(img);
                                                });
                                            }
                                        });
                                    }
                                    else
                                    {
                                        secCol.Item().Row(chartRow =>
                                        {
                                            chartRow.Spacing(20);

                                            if (hasBar || hasSingle)
                                            {
                                                chartRow.RelativeItem().Column(c =>
                                                {
                                                    c.Item().PaddingBottom(5).Text(section.LeftChartTitle.ToUpper()).FontSize(8).Bold().FontColor("#1e40af");
                                                    byte[]? img = null;
                                                    if (hasSingle && section.SingleBarData != null)
                                                        img = ChartHelper.GetSingleBarChartImage(400, 180, section.SingleBarData, "", section.SingleBarColor ?? "#3b82f6");
                                                    else if (hasBar && section.ChartData != null) 
                                                        img = ChartHelper.GetBarChartImage(400, 180, section.ChartData, "", section.ActualLabel, section.TargetLabel);
                                                    
                                                    if (img != null) c.Item().Image(img);
                                                });
                                            }

                                            if (hasHoriz && section.HorizontalBarData != null)
                                            {
                                                chartRow.RelativeItem().Column(c =>
                                                {
                                                    c.Item().PaddingBottom(5).Text(section.RightChartTitle.ToUpper()).FontSize(8).Bold().FontColor("#1e40af");
                                                    int hh = Math.Min(section.HorizontalBarData.Count * 20 + 30, 200);
                                                    var img = ChartHelper.GetHorizontalBarChartImage(400, hh, section.HorizontalBarData, "", section.HorizontalBarColor ?? "#8b5cf6");
                                                    if (img != null) c.Item().Image(img);
                                                });
                                            }
                                        });
                                    }
                                }

                                // Table
                                if (section.Headers.Any())
                                {
                                    secCol.Item().Table(table =>
                                    {
                                        int colCount = section.Headers.Length;
                                        table.ColumnsDefinition(columns =>
                                        {
                                            for (int i = 0; i < colCount; i++)
                                            {
                                                string h = (section.Headers[i] ?? "").Trim().ToLower();
                                                if (h == "no" || h == "no." || h == "sıra" || h == "sira" || h == "sıra no" || h == "sira no" || "#" == h) 
                                                    columns.RelativeColumn(1.2f);
                                                else if (h.Contains("tarih") || h.Contains("vade")) 
                                                     columns.RelativeColumn(3.5f);
                                                else if (h.Contains("durum") || h.Contains("tür") || h.Contains("tur") || h.Contains("tip")) 
                                                     columns.RelativeColumn(4f);
                                                else if (h.Contains("no")) // like dekont no, fatura no, evrak no
                                                     columns.RelativeColumn(4.5f);
                                                else if (h.Contains("borç") || h.Contains("borc") || h.Contains("alacak") || h.Contains("bakiye") || h.Contains("tutar") || h.Contains("fiyat") || h.Contains("satış") || h.Contains("satis") || h.Contains("ortalama") || h.Contains("toplam")) 
                                                     columns.RelativeColumn(4.5f);
                                                else if (h.Contains("miktar") || h.Contains("adet") || h.Contains("sayı") || h.Contains("sayisi") || h.Contains("sayısı")) 
                                                     columns.RelativeColumn(3.5f);
                                                else if (h.Contains("şehir") || h.Contains("sehir") || h.Contains("il") || h.Contains("ilçe") || h.Contains("ilce"))
                                                     columns.RelativeColumn(4f);
                                                else 
                                                     columns.RelativeColumn(8f); // Açıklama/Unvan
                                            }
                                        });

                                        table.Header(header =>
                                        {
                                            for (int i = 0; i < colCount; i++)
                                            {
                                                var h = section.Headers[i] ?? "";
                                                string hLower = h.ToLower();
                                                bool isRight = hLower.Contains("borç") || hLower.Contains("borc") || hLower.Contains("alacak") || hLower.Contains("bakiye") || hLower.Contains("tutar") || hLower.Contains("fiyat") || hLower.Contains("ortalama") || hLower.Contains("toplam") || hLower.Contains("miktar") || hLower.Contains("adet") || hLower.Contains("sayı") || hLower.Contains("sayisi") || hLower.Contains("sayısı");
                                                bool isCenter = hLower == "no" || hLower == "no." || hLower == "sıra" || hLower == "sira" || hLower == "sıra no" || hLower == "sira no" || hLower == "#" || hLower.Contains("tarih") || hLower.Contains("vade");

                                                var cell = header.Cell().Background("#1e3a8a").PaddingVertical(6).PaddingHorizontal(6);
                                                if (isRight)
                                                    cell.AlignRight().Text(h).FontSize(8).Bold().FontColor("#ffffff");
                                                else if (isCenter)
                                                    cell.AlignCenter().Text(h).FontSize(8).Bold().FontColor("#ffffff");
                                                else
                                                    cell.AlignLeft().Text(h).FontSize(8).Bold().FontColor("#ffffff");
                                            }
                                        });

                                        int rowIndex = 0;
                                        foreach (var rowData in section.Rows)
                                        {
                                            if (rowData.Length > 0 && (rowData[0] ?? "").Trim().StartsWith("---"))
                                            {
                                                string hText = (rowData[0] ?? "").Replace("---", "").Trim();
                                                table.Cell().ColumnSpan((uint)colCount).Background("#f1f5f9").PaddingVertical(5).PaddingHorizontal(6).Text(hText.ToUpper()).FontSize(8).Bold().FontColor("#1e3a8a");
                                                continue;
                                            }

                                            bool isTotal = rowData.Any(c => (c ?? "").ToLower().Contains("toplam"));
                                            string bgColor = rowIndex % 2 == 0 ? "#ffffff" : "#f8fafc";
                                            rowIndex++;

                                            for (int j = 0; j < colCount; j++)
                                            {
                                                string cellData = j < rowData.Length ? rowData[j] : "";
                                                string h = section.Headers[j].ToLower();
                                                bool isRight = h.Contains("borç") || h.Contains("borc") || h.Contains("alacak") || h.Contains("bakiye") || h.Contains("tutar") || h.Contains("fiyat") || h.Contains("ortalama") || h.Contains("toplam") || h.Contains("miktar") || h.Contains("adet") || h.Contains("sayı") || h.Contains("sayisi") || h.Contains("sayısı");
                                                bool isCenter = h == "no" || h == "no." || h == "sıra" || h == "sira" || h == "sıra no" || h == "sira no" || h == "#" || h.Contains("tarih") || h.Contains("vade");

                                                IContainer cell = table.Cell();
                                                if (isTotal)
                                                {
                                                    cell = cell.Background("#f1f5f9").PaddingVertical(6).PaddingHorizontal(6).BorderTop(1f).BorderColor("#cbd5e1").BorderBottom(1.5f).BorderColor("#94a3b8");
                                                }
                                                else
                                                {
                                                    cell = cell.Background(bgColor).PaddingVertical(5).PaddingHorizontal(6).BorderBottom(0.5f).BorderColor("#e2e8f0");
                                                }

                                                if (isRight)
                                                {
                                                    var txt = cell.AlignRight().Text(cellData ?? "").FontSize(8);
                                                    if (isTotal) txt.Bold();
                                                }
                                                else if (isCenter)
                                                {
                                                    var txt = cell.AlignCenter().Text(cellData ?? "").FontSize(8);
                                                    if (isTotal) txt.Bold();
                                                }
                                                else
                                                {
                                                    var txt = cell.AlignLeft().Text(cellData ?? "").FontSize(8);
                                                    if (isTotal) txt.Bold();
                                                }
                                            }
                                        }
                                    });
                                }

                                // Footer Note
                                if (!string.IsNullOrEmpty(section.FooterNote))
                                {
                                    secCol.Item().PaddingTop(5).Text(section.FooterNote).FontSize(8).Italic().FontColor("#475569");
                                }
                            });
                        }
                    });

                    // FOOTER
                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().Text("Ermay Muhasebe | info@ermaymuhasebe.com").FontSize(8).FontColor("#94a3b8");
                        row.RelativeItem().AlignRight().Text(text =>
                        {
                            text.DefaultTextStyle(s => s.FontSize(8).FontColor("#94a3b8"));
                            text.Span("Sayfa ");
                            text.CurrentPageNumber();
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateFinansRaporPdf(string title, FinancialReportData data)
        {
            // License set in Program.cs

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(GetPageSize(RaporSize, GetReportOrientation(title)));
                    page.Margin(1.5f, QuestPDF.Infrastructure.Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(DefaultFontFamily));

                    page.Header()
                        .Row(row =>
                        {
                            if (ShowLogoRaporlar)
                            {
                                var logo = LoadLogoBytes();
                                if (logo != null && logo.Length > 0)
                                {
                                    row.ConstantItem(60).Image(logo).FitArea();
                                    row.ConstantItem(10);
                                }
                            }

                            row.RelativeItem(3).Column(col =>
                            {
                                col.Item().Text(title).FontSize(16).Bold().FontColor("#1e3a8a");
                                col.Item().Text($"Dönem: {data.StartDate:dd.MM.yyyy} - {data.EndDate:dd.MM.yyyy}").FontSize(10).FontColor("#64748b");
                            });

                            row.RelativeItem().AlignRight().Column(c => {
                                c.Item().Text("ERMAY").FontSize(10).Bold();
                            });
                        });

                    page.Content()
                        .PaddingVertical(1, QuestPDF.Infrastructure.Unit.Centimetre)
                        .Column(col =>
                        {
                            col.Spacing(10);
                            
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3); // Ödeme Türü
                                    columns.RelativeColumn(2); // Tahsilat
                                    columns.RelativeColumn(2); // Ödeme
                                    columns.RelativeColumn(2); // Net
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background("#1e3a8a").PaddingVertical(6).PaddingHorizontal(6).Text("Ödeme Türü").Bold().FontSize(8).FontColor("#ffffff");
                                    header.Cell().Background("#1e3a8a").PaddingVertical(6).PaddingHorizontal(6).AlignRight().Text("Tahsilat (+)").Bold().FontSize(8).FontColor("#ffffff");
                                    header.Cell().Background("#1e3a8a").PaddingVertical(6).PaddingHorizontal(6).AlignRight().Text("Ödeme (-)").Bold().FontSize(8).FontColor("#ffffff");
                                    header.Cell().Background("#1e3a8a").PaddingVertical(6).PaddingHorizontal(6).AlignRight().Text("Net Durum").Bold().FontSize(8).FontColor("#ffffff");
                                });

                                int rowIndex = 0;
                                foreach (var item in data.Items)
                                {
                                    string bgColor = rowIndex % 2 == 0 ? "#ffffff" : "#f8fafc";
                                    rowIndex++;

                                    table.Cell().Background(bgColor).PaddingVertical(5).PaddingHorizontal(6).BorderBottom(0.5f).BorderColor("#e2e8f0").AlignMiddle()
                                        .Element(e => ComposeBadge(e, item.Type ?? "", item.Type ?? ""));
                                    table.Cell().Background(bgColor).PaddingVertical(5).PaddingHorizontal(6).BorderBottom(0.5f).BorderColor("#e2e8f0").AlignRight().Text(item.TotalCollection.ToString("C2")).FontSize(8);
                                    table.Cell().Background(bgColor).PaddingVertical(5).PaddingHorizontal(6).BorderBottom(0.5f).BorderColor("#e2e8f0").AlignRight().Text(item.TotalPayment.ToString("C2")).FontSize(8);
                                    table.Cell().Background(bgColor).PaddingVertical(5).PaddingHorizontal(6).BorderBottom(0.5f).BorderColor("#e2e8f0").AlignRight().Text(item.Net.ToString("C2")).FontSize(8);
                                }
                            });
                            
                            col.Item().EnsureSpace(20);
                            col.Item().PaddingTop(20).AlignRight().Column(c => {
                                var totalNet = data.Items.Sum(x => x.Net);
                                c.Item().Text($"GENEL NET DURUM: {totalNet:C2}").FontSize(14).Bold().FontColor(totalNet >= 0 ? "#16a34a" : "#dc2626");
                            });
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Sayfa ");
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                        });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateMultiSectionReport(string title, List<(string Title, string[] Headers, List<string[]> Rows)> sections)
        {
            // License set in Program.cs

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(QuestPDF.Helpers.PageSizes.A5);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header()
                        .Text(title)
                        .Bold().FontSize(18).FontColor("#2563eb");

                    page.Content()
                        .PaddingVertical(1, QuestPDF.Infrastructure.Unit.Centimetre)
                        .Column(col =>
                        {
                            bool isFirstSection = true;
                            foreach (var section in sections)
                            {
                                // Sayfa sonu ekle (ilk bölüm hariç)
                                if (!isFirstSection && section.Title.Contains("STOK") || section.Title.Contains("FATURA"))
                                {
                                    col.Item().PageBreak();
                                }
                                isFirstSection = false;

                                // Bölüm başlığı
                                col.Item().PaddingTop(isFirstSection ? 0 : 20).Text(section.Title).FontSize(13).Bold();

                                // Tablo
                                col.Item().EnsureSpace(10);
                                col.Item().PaddingTop(10).Table(table =>
                                {
                                    // Kolon tanımları
                                    table.ColumnsDefinition(columns =>
                                    {
                                        for (int i = 0; i < section.Headers.Length; i++)
                                        {
                                            columns.RelativeColumn();
                                        }
                                    });

                                    // Başlıklar
                                    foreach (var header in section.Headers)
                                    {
                                        table.Cell().Border(1).Padding(5).Background("#e0e7ff").Text(header).Bold();
                                    }

                                    // Satırlar
                                    foreach (var row in section.Rows)
                                    {
                                        foreach (var cell in row)
                                        {
                                            table.Cell().Border(1).Padding(5).Text(cell ?? "");
                                        }
                                    }
                                });
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Sayfa ");
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                            x.Span($" - Oluşturulma: {DateTime.Now:dd.MM.yyyy HH:mm}");
                        });
                });
            });

            return document.GeneratePdf();
        }
        public byte[] GenerateStokListPdf(List<StokKart> stoklar)
        {
            // License set in Program.cs
            
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(8).FontFamily(DefaultFontFamily));

                    page.Header().Row(row =>
                    {
                        if (ShowLogoRaporlar)
                        {
                            var logoBytes = LoadLogoBytes();
                            if (logoBytes != null && logoBytes.Length > 0)
                            {
                                row.ConstantItem(80).Image(logoBytes).FitArea();
                                row.ConstantItem(15);
                            }
                        }

                        row.RelativeItem(3).Column(col =>
                        {
                            col.Item().Text("TOPLU STOK RAPORU").Bold().FontSize(18).FontColor("#2563eb");
                            col.Item().Text($"{DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(10).FontColor("#666");
                        });
                    });

                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2); // Kod
                            columns.RelativeColumn(4); // Stok Adı
                            columns.RelativeColumn(2); // Grup
                            columns.RelativeColumn(1); // Birim
                            columns.RelativeColumn(1.5f); // Miktar
                            columns.RelativeColumn(2.5f); // Ort. Alış
                            columns.RelativeColumn(2.5f); // Ort. Satış
                            columns.RelativeColumn(2.5f); // Güncel Satış
                            columns.RelativeColumn(2.5f); // Toplam Maliyet
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Kod");
                            header.Cell().Element(CellStyle).Text("Stok Adı");
                            header.Cell().Element(CellStyle).Text("Grup");
                            header.Cell().Element(CellStyle).Text("Birim");
                            header.Cell().Element(CellStyle).AlignRight().Text("Miktar");
                            header.Cell().Element(CellStyle).AlignRight().Text("Ort. Alış");
                            header.Cell().Element(CellStyle).AlignRight().Text("Ort. Satış");
                            header.Cell().Element(CellStyle).AlignRight().Text("Satış F.");
                            header.Cell().Element(CellStyle).AlignRight().Text("T. Maliyet");

                            static IContainer CellStyle(IContainer container) => container.DefaultTextStyle(x => x.Bold()).PaddingVertical(5).BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Black);
                        });

                        decimal grandTotalCost = 0;
                        foreach (var s in stoklar)
                        {
                            decimal totalCost = (decimal)s.Miktar * s.OrtalamaAlisFiyati;
                            grandTotalCost += totalCost;

                            table.Cell().Element(RowStyle).Text(s.StokKodu ?? "-");
                            table.Cell().Element(RowStyle).Text(s.StokAdi ?? "-");
                            table.Cell().Element(RowStyle).Text(s.Grup ?? s.Kategori ?? "-");
                            table.Cell().Element(RowStyle).Text(s.Birim ?? "Adet");
                            table.Cell().Element(RowStyle).AlignRight().Text(s.Miktar.ToString("N2"));
                            table.Cell().Element(RowStyle).AlignRight().Text(s.OrtalamaAlisFiyati.ToString("N2") + " ₺");
                            table.Cell().Element(RowStyle).AlignRight().Text(s.OrtalamaSatisFiyati.ToString("N2") + " ₺");
                            table.Cell().Element(RowStyle).AlignRight().Text(s.SatisFiyati.ToString("N2") + " ₺");
                            table.Cell().Element(RowStyle).AlignRight().Text(totalCost.ToString("N2") + " ₺");

                            static IContainer RowStyle(IContainer container) => container.PaddingVertical(2).BorderBottom(0.5f).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2);
                        }

                        // Footer row for Grand Total
                        table.Cell().ColumnSpan(8).PaddingVertical(5).AlignRight().Text("GENEL TOPLAM MALİYET DEĞERİ:").Bold();
                        table.Cell().PaddingVertical(5).AlignRight().Text(grandTotalCost.ToString("C2")).Bold();
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Sayfa ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });

            return document.GeneratePdf();
        }


        public byte[] GenerateStokHareketleriPdf(StokKart stok, List<StokHareket> hareketler)
        {
            // License set in Program.cs
            
            var orderedHareketler = hareketler.OrderBy(h => h.Tarih).ToList();
            decimal runningBalance = 0;
            
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(DefaultFontFamily));

                    page.Header().Row(row =>
                    {
                        if (ShowLogoRaporlar)
                        {
                            var logoBytes = LoadLogoBytes();
                            if (logoBytes != null && logoBytes.Length > 0)
                            {
                                row.ConstantItem(60).Image(logoBytes).FitArea();
                                row.ConstantItem(10);
                            }
                        }

                        row.RelativeItem(3).Column(col =>
                        {
                            col.Item().Text("STOK HAREKET RAPORU").Bold().FontSize(18).FontColor("#1e3a8a");
                            col.Item().Text($"{stok.StokKodu} - {stok.StokAdi}").FontSize(12).Bold();
                        });
                    });

                    page.Content().PaddingVertical(10).Column(col => 
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3); // Tarih
                                columns.RelativeColumn(2); // Tür
                                columns.RelativeColumn(4); // Açıklama
                                columns.RelativeColumn(2); // Giren
                                columns.RelativeColumn(2); // Çıkan
                                columns.RelativeColumn(2); // Kalan
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("Tarih");
                                header.Cell().Element(CellStyle).Text("İşlem Türü");
                                header.Cell().Element(CellStyle).Text("Açıklama");
                                header.Cell().Element(CellStyle).AlignRight().Text("Giren");
                                header.Cell().Element(CellStyle).AlignRight().Text("Çıkan");
                                header.Cell().Element(CellStyle).AlignRight().Text("Kalan");

                                static IContainer CellStyle(IContainer container) => container.DefaultTextStyle(x => x.Bold()).PaddingVertical(5).BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Black);
                            });

                            foreach (var h in orderedHareketler)
                            {
                                bool isGiris = (h.Giren > 0) || (h.IslemTuru != null && (
                                    h.IslemTuru.Equals("GİRİŞ", StringComparison.OrdinalIgnoreCase) ||
                                    h.IslemTuru.Equals("Giris", StringComparison.OrdinalIgnoreCase) ||
                                    h.IslemTuru.Equals("GİREN", StringComparison.OrdinalIgnoreCase) ||
                                    h.IslemTuru.Contains("Alış", StringComparison.OrdinalIgnoreCase) ||
                                    h.IslemTuru.Contains("Alis", StringComparison.OrdinalIgnoreCase) ||
                                    h.IslemTuru.Contains("Açılış", StringComparison.OrdinalIgnoreCase) ||
                                    h.IslemTuru.Contains("Acilis", StringComparison.OrdinalIgnoreCase) ||
                                    h.IslemTuru.Contains("Devir", StringComparison.OrdinalIgnoreCase)
                                ));

                                decimal girenVal = h.Giren > 0 ? h.Giren : (isGiris ? h.Miktar : 0);
                                decimal cikanVal = h.Cikan > 0 ? h.Cikan : (!isGiris ? h.Miktar : 0);

                                if (girenVal == 0 && cikanVal == 0 && h.Miktar > 0)
                                {
                                    if (isGiris) girenVal = h.Miktar;
                                    else cikanVal = h.Miktar;
                                }

                                runningBalance += (girenVal - cikanVal);

                                string rawTuru = isGiris ? "GİRİŞ" : "ÇIKIŞ";
                                string badgeText = string.IsNullOrWhiteSpace(h.IslemTuru) ? rawTuru : h.IslemTuru;

                                table.Cell().Element(RowStyle).Text(h.Tarih.ToString("dd.MM.yyyy HH:mm"));
                                table.Cell().Element(RowStyle).AlignMiddle()
                                    .Element(e => ComposeBadge(e, rawTuru, badgeText));
                                table.Cell().Element(RowStyle).Text(h.Aciklama ?? "-");
                                table.Cell().Element(RowStyle).AlignRight().Text(girenVal > 0 ? girenVal.ToString("N2") : "-");
                                table.Cell().Element(RowStyle).AlignRight().Text(cikanVal > 0 ? cikanVal.ToString("N2") : "-");
                                table.Cell().Element(RowStyle).AlignRight().Text(runningBalance.ToString("N2")).Bold();

                                static IContainer RowStyle(IContainer container) => container.PaddingVertical(4).BorderBottom(0.5f).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2);
                            }
                        });

                        col.Item().EnsureSpace(10);
                        col.Item().PaddingTop(10).AlignRight().Text(x => {
                            x.Span("Güncel Stok Miktarı: ").Bold().FontSize(11);
                            x.Span(runningBalance.ToString("N2")).Bold().FontSize(11).FontColor("#1e3a8a");
                            x.Span($" {stok.Birim ?? "Adet"}").Bold().FontSize(11);
                        });
                    });

                    page.Footer().AlignCenter().Text(x => {
                        x.Span("Sayfa ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateBudgetReportPdf(
            string title,
            List<ChartDataItem> annualData,
            List<ChartDataItem> monthlyData,
            List<ChartDataItem> weeklyData)
        {
            // License set in Program.cs

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(GetPageSize(RaporSize, GetReportOrientation(title)));
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(DefaultFontFamily));

                    page.Header().Row(row =>
                    {
                        if (ShowLogoRaporlar)
                        {
                            var logo = LoadLogoBytes();
                            if (logo != null && logo.Length > 0)
                            {
                                row.ConstantItem(60).Image(logo).FitArea();
                                row.ConstantItem(10);
                            }
                        }

                        row.RelativeItem(3).Column(col =>
                        {
                            col.Item().Text(title).FontSize(18).Bold().FontColor("#1e3a8a");
                            col.Item().Text($"{DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(10).FontColor("#64748b");
                        });
                    });

                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                    {
                        col.Spacing(20);

                        // 1. Annual Section
                        if (annualData.Any())
                        {
                            col.Item().Text("YILLIK PERFORMANS").FontSize(14).Bold().FontColor("#2563eb");
                            col.Item().EnsureSpace(10);
                            var imgAnnual = ChartHelper.GetBarChartImage(500, 180, annualData, "Yıllık Bazda Hedef vs Gerçekleşen", "Gerçekleşen", "Hedef");
                            if (imgAnnual.Length > 0) col.Item().Image(imgAnnual);
                            col.Item().EnsureSpace(10);
                            col.Item().PaddingTop(10).Element(c => DrawTable(c, annualData, "Yıl"));
                        }

                        col.Item().PageBreak();

                        // 2. Monthly Section
                        if (monthlyData.Any())
                        {
                            col.Item().Text($"AYLIK DETAY ({DateTime.Now.Year})").FontSize(14).Bold().FontColor("#2563eb");
                            col.Item().EnsureSpace(10);
                            var imgMonthly = ChartHelper.GetBarChartImage(500, 200, monthlyData, "Aylık Bazda Hedef vs Gerçekleşen", "Gerçekleşen", "Hedef");
                            if (imgMonthly.Length > 0) col.Item().Image(imgMonthly);
                            col.Item().EnsureSpace(10);
                            col.Item().PaddingTop(10).Element(c => DrawTable(c, monthlyData, "Ay"));
                        }

                        col.Item().PageBreak();

                        // 3. Weekly Section
                        if (weeklyData.Any())
                        {
                            col.Item().Text($"HAFTALIK DETAY ({System.Globalization.CultureInfo.GetCultureInfo("tr-TR").DateTimeFormat.GetMonthName(DateTime.Now.Month)})").FontSize(14).Bold().FontColor("#2563eb");
                            col.Item().EnsureSpace(10);
                            var imgWeekly = ChartHelper.GetBarChartImage(500, 200, weeklyData, "Haftalık Bazda Hedef vs Gerçekleşen", "Gerçekleşen", "Hedef");
                            if (imgWeekly.Length > 0) col.Item().Image(imgWeekly);
                            col.Item().EnsureSpace(10);
                            col.Item().PaddingTop(10).Element(c => DrawTable(c, weeklyData, "Hafta"));
                        }
                    });

                    page.Footer().AlignCenter().Text(x => { x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });
            });

            return document.GeneratePdf();
        }



        private void DrawTable(QuestPDF.Infrastructure.IContainer container, List<ChartDataItem> data, string labelHeader)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Background("#1e3a8a").PaddingVertical(6).PaddingHorizontal(6).Text(labelHeader).Bold().FontSize(8).FontColor("#ffffff");
                    header.Cell().Background("#1e3a8a").PaddingVertical(6).PaddingHorizontal(6).AlignRight().Text("Hedef").Bold().FontSize(8).FontColor("#ffffff");
                    header.Cell().Background("#1e3a8a").PaddingVertical(6).PaddingHorizontal(6).AlignRight().Text("Gerçekleşen").Bold().FontSize(8).FontColor("#ffffff");
                    header.Cell().Background("#1e3a8a").PaddingVertical(6).PaddingHorizontal(6).AlignRight().Text("Fark").Bold().FontSize(8).FontColor("#ffffff");
                    header.Cell().Background("#1e3a8a").PaddingVertical(6).PaddingHorizontal(6).AlignCenter().Text("Durum").Bold().FontSize(8).FontColor("#ffffff");
                });

                int rowIndex = 0;
                foreach(var d in data)
                {
                    decimal diff = d.Actual - d.Target;
                    decimal pct = d.Target > 0 ? (d.Actual / d.Target * 100) : 0;
                    string color = pct >= 100 ? "#16a34a" : (pct >= 50 ? "#ca8a04" : "#dc2626");
                    string bgColor = rowIndex % 2 == 0 ? "#ffffff" : "#f8fafc";
                    rowIndex++;

                    table.Cell().Background(bgColor).PaddingVertical(5).PaddingHorizontal(6).BorderBottom(0.5f).BorderColor("#e2e8f0").Text(d.Label).FontSize(8);
                    table.Cell().Background(bgColor).PaddingVertical(5).PaddingHorizontal(6).BorderBottom(0.5f).BorderColor("#e2e8f0").AlignRight().Text($"{d.Target:C0}").FontSize(8);
                    table.Cell().Background(bgColor).PaddingVertical(5).PaddingHorizontal(6).BorderBottom(0.5f).BorderColor("#e2e8f0").AlignRight().Text($"{d.Actual:C0}").FontSize(8);
                    table.Cell().Background(bgColor).PaddingVertical(5).PaddingHorizontal(6).BorderBottom(0.5f).BorderColor("#e2e8f0").AlignRight().Text($"{diff:C0}").FontColor(diff >= 0 ? "#16a34a" : "#dc2626").FontSize(8);
                    table.Cell().Background(bgColor).PaddingVertical(5).PaddingHorizontal(6).BorderBottom(0.5f).BorderColor("#e2e8f0").AlignCenter().Text($"%{pct:N0}").FontColor(color).Bold().FontSize(8);
                }
            });
        }

        public byte[] GenerateSiparisPdf(Siparis siparis, List<SiparisDetay> detaylar, CariKart? cari = null)
        {
            // License set in Program.cs
            var trCulture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
            var logoBytes = LoadLogoBytes();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(GetPageSize(SiparisSize, SiparisOrientation));
                    page.Margin(0.5f, QuestPDF.Infrastructure.Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(8).FontFamily(DefaultFontFamily).LineHeight(1.1f).FontColor("#000000"));

                    page.Header().Column(headerCol =>
                    {
                        // 1) Tarih - Sağ üst köşe
                        headerCol.Item().AlignRight().PaddingBottom(2).Text($"TARİH: {siparis.Tarih:dd.MM.yyyy}").Bold().FontSize(8).FontColor("#1e293b");

                        // 2) Cari Bilgileri + Logo - Yan yana, aynı yükseklikte
                        headerCol.Item().Height(80).Row(row =>
                        {
                            // Sol: Cari Bilgileri - logo ile aynı hizada
                            row.RelativeItem(2).PaddingRight(10).AlignMiddle().MaxHeight(60).Border(0.5f).BorderColor("#E2E8F0").Background("#F8FAFC").PaddingVertical(5).PaddingHorizontal(10).Column(innerColumn =>
                            {
                                innerColumn.Item().Text((siparis.CariUnvan ?? "").ToUpper()).Bold().FontSize(9).FontColor("#000000");
                                
                                string vkn = cari?.VergiNo ?? "";
                                if (!string.IsNullOrEmpty(vkn))
                                    innerColumn.Item().Text($"VKN/TC: {vkn}").FontSize(7).FontColor("#64748B");
                                
                                if (!string.IsNullOrEmpty(cari?.Adres))
                                    innerColumn.Item().Text(cari.Adres).FontSize(7).FontColor("#64748B");
                            });

                            // Boşluk
                            row.RelativeItem(1);
 
                            // Sağ: Logo (büyük)
                            if (ShowLogoSiparis)
                            {
                                row.RelativeItem(2).AlignRight().AlignMiddle().Element(logoContainer =>
                                {
                                    if (logoBytes != null && logoBytes.Length > 0)
                                    {
                                        logoContainer.MaxHeight(50).Image(logoBytes).FitArea();
                                    }
                                });
                            }
                        });
                    });




                    page.Content().PaddingVertical(5).Column(col =>
                    {
                        // Product Table
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);   // #
                                columns.RelativeColumn(5);    // ÜRÜN / HİZMET
                                columns.RelativeColumn(2.5f); // MİKTAR
                                columns.RelativeColumn(2.5f); // MİKTAR AÇIKLAMASI
                                columns.RelativeColumn(3);    // AÇIKLAMA
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderStyle).Text("#");
                                header.Cell().Element(HeaderStyle).Text("ÜRÜN / HİZMET");
                                header.Cell().Element(HeaderStyle).Text("MİKTAR");
                                header.Cell().Element(HeaderStyle).Text("MİKTAR AÇIKLAMASI");
                                header.Cell().Element(HeaderStyle).Text("AÇIKLAMA");

                                static QuestPDF.Infrastructure.IContainer HeaderStyle(QuestPDF.Infrastructure.IContainer container)
                                {
                                    return container.Border(0.5f).BorderColor("#000000").Padding(6)
                                        .DefaultTextStyle(x => x.FontSize(8).Bold());
                                }
                            });

                            foreach (var item in detaylar.Select((value, index) => new { value, index }))
                            {
                                table.Cell().Element(CellStyle).Text($"{item.index + 1}");
                                table.Cell().Element(CellStyle).Column(c => {
                                    c.Item().Text((item.value.StokAdi ?? "").ToUpper()).Bold();
                                    if (!string.IsNullOrEmpty(item.value.Aciklama))
                                        c.Item().Text(item.value.Aciklama).FontSize(7).FontColor("#64748B").Italic();
                                });
                                table.Cell().Element(CellStyle).Text($"{item.value.Miktar:N1} {item.value.Birim ?? ""}");
                                table.Cell().Element(CellStyle).Text(item.value.MiktarAciklama ?? "");
                                table.Cell().Element(CellStyle).Text(item.value.Aciklama ?? "");

                                static QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer container)
                                {
                                    return container.Border(0.6f).BorderColor("#000000").PaddingVertical(3).PaddingHorizontal(5).DefaultTextStyle(x => x.FontSize(8.5f));
                                }
                            }
                        });


                        // Notes Section (Optional for Siparis)
                        string displayNotes = !string.IsNullOrWhiteSpace(siparis.PdfNotlar) ? siparis.PdfNotlar : (siparis.Aciklama ?? "");
                        
                        if (!string.IsNullOrEmpty(displayNotes))
                        {
                            string cleanedNotes = displayNotes;
                            cleanedNotes = System.Text.RegularExpressions.Regex.Replace(cleanedNotes, @"\(Siparişten Dönüştürüldü\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            cleanedNotes = System.Text.RegularExpressions.Regex.Replace(cleanedNotes, @"\(Tekliften Dönüştürüldü\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            cleanedNotes = cleanedNotes.Trim();

                            if (!string.IsNullOrWhiteSpace(cleanedNotes))
                            {
                                col.Item().PaddingTop(15).Column(noteCol =>
                                {
                                    noteCol.Item().Text("NOTLAR:").Bold().FontSize(9);
                                    noteCol.Item().PaddingTop(1).Text(cleanedNotes).FontSize(8);
                                });
                            }
                        }
                    });

                    page.Footer().Row(footerRow =>
                    {
                        footerRow.RelativeItem().AlignLeft().Text("ERMAY TEKNİK TEKSTİL | www.ermaysanayi.com").FontSize(7).FontColor("#999999");
                        footerRow.RelativeItem().AlignRight().Text(t =>
                        {
                            t.Span("Sayfa ").FontSize(7);
                            t.CurrentPageNumber().FontSize(7);
                            t.Span(" / ").FontSize(7);
                            t.TotalPages().FontSize(7);
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

        protected byte[]? LoadLogoBytes()
        {
            // 1. Eğer bellekte geçerli bir logo varsa hemen döndür
            if (_logoBytes != null && _logoBytes.Length > 0)
            {
                return _logoBytes;
            }

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string ermayDir = System.IO.Path.Combine(appData, "ErmayMuhasebe");
            string customLogoPath = System.IO.Path.Combine(ermayDir, "company_logo.png");

            // 2. Disk üzerindeki şirket logosunu oku (en garantili ve hızlı yöntem)
            try
            {
                if (System.IO.File.Exists(customLogoPath))
                {
                    var fileBytes = System.IO.File.ReadAllBytes(customLogoPath);
                    if (fileBytes != null && fileBytes.Length > 0)
                    {
                        _logoBytes = fileBytes;
                        _isLogoLoaded = true;
                        return _logoBytes;
                    }
                }
            }
            catch { }

            // 3. Şifreli SQLite ana veritabanını kontrol et (ErmayV4_Stable.db3)
            try
            {
                string dbPath = System.IO.Path.Combine(ermayDir, "ErmayV4_Stable.db3");
                if (System.IO.File.Exists(dbPath))
                {
                    var pwd = ErmayMuhasebe.Data.Constants.DatabasePassword;
                    var opts = new SQLite.SQLiteConnectionString(dbPath, SQLite.SQLiteOpenFlags.ReadOnly | SQLite.SQLiteOpenFlags.FullMutex, true, key: pwd);
                    using var conn = new SQLite.SQLiteConnection(opts);
                    var profil = conn.Table<FirmaProfili>().FirstOrDefault();
                    if (profil != null && !string.IsNullOrEmpty(profil.LogoBase64))
                    {
                        var bytes = Convert.FromBase64String(profil.LogoBase64);
                        if (bytes != null && bytes.Length > 0)
                        {
                            _logoBytes = bytes;
                            _isLogoLoaded = true;
                            try
                            {
                                if (!System.IO.Directory.Exists(ermayDir)) System.IO.Directory.CreateDirectory(ermayDir);
                                System.IO.File.WriteAllBytes(customLogoPath, bytes);
                            }
                            catch { }
                            return _logoBytes;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private byte[]? LoadImageBytes(string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                string localPath = path;
                
                // 1. Handle URI formats
                if (localPath.Contains("://"))
                {
                    try 
                    { 
                        var uri = new Uri(path);
                        localPath = uri.LocalPath; 
                    } 
                    catch { }
                }

                // 2. Unescape any URL-encoded characters (like %20 for space)
                localPath = Uri.UnescapeDataString(localPath);

                // 3. Standardize slashes for Windows
                localPath = localPath.Replace("/", "\\");
                
                // 4. Cleanup leading slashes from mixed path conversions (e.g. \C:\... -> C:\...)
                if (localPath.Length > 2 && localPath[0] == '\\' && localPath[2] == ':')
                {
                    localPath = localPath.Substring(1);
                }
                
                // 5. If it's still not found, try to fix relative paths or common trimming issues
                if (!System.IO.File.Exists(localPath))
                {
                    // If it starts with a drive letter without a slash after colon (C:temp -> C:\temp)
                    if (localPath.Length > 2 && localPath[1] == ':' && localPath[2] != '\\')
                    {
                        localPath = localPath.Insert(2, "\\");
                    }
                }

                if (System.IO.File.Exists(localPath))
                {
                    using (var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        if (fs.Length == 0) return null;
                        using (var ms = new MemoryStream())
                        {
                            fs.CopyTo(ms);
                            var bytes = ms.ToArray();
                            return ResizeImageIfLarge(bytes, 1000, 1000); // Preprocessing to avoid layout conflicts and high memory usage
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"File NOT found at resolved path: {localPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Image load error for {path}: {ex.Message}");
            }
            return null;
        }
        public string GetLogoPath()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] paths = new[]
            {
                Path.Combine(baseDir, "wwwroot", "images", "ermay_logo.png"),
                Path.Combine(baseDir, "Assets", "ermay_logo.png"),
                Path.Combine(baseDir, "_content", "ErmayMuhasebe.Shared", "images", "ermay_logo.png"),
                @"d:\2\ermaymuhasebe\ErmayMuhasebe.Avalonia\ErmayMuhasebe.Avalonia\Assets\ermay_logo.png",
                @"e:\2\ermaymuhasebe\ErmayMuhasebe.Avalonia\ErmayMuhasebe.Avalonia\Assets\ermay_logo.png",
                @"C:\Users\mazik\.gemini\antigravity\scratch\on_muhasebe\ErmayMuhasebe.Shared\wwwroot\images\ermay_logo.png"
            };

            foreach (var p in paths)
            {
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
            }
            return ""; 
        }
        
        private byte[]? LoadSiparisLogoBytes()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] paths = new[]
            {
                Path.Combine(baseDir, "wwwroot", "images", "ermay_logo_siparis.png"),
                Path.Combine(baseDir, "Assets", "ermay_logo_siparis.png"),
                Path.Combine(baseDir, "_content", "ErmayMuhasebe.Shared", "images", "ermay_logo_siparis.png"),
                @"d:\2\ermaymuhasebe\ErmayMuhasebe.Shared\wwwroot\images\ermay_logo_siparis.png",
                @"d:\2\ermaymuhasebe\ErmayMuhasebe.Avalonia\ErmayMuhasebe.Avalonia\Assets\ermay_logo_siparis.png"
            };

            foreach (var p in paths)
            {
                if (File.Exists(p)) return LoadImageBytes(p);
            }

            return LoadLogoBytes(); // Fallback to standard logo
        }

        private byte[] ResizeImageIfLarge(byte[] imageBytes, int maxWidth, int maxHeight)
        {
            // SkiaSharp is disabled here to avoid WASM load issues
            return imageBytes;
            /*
            try
            {
                using var ms = new MemoryStream(imageBytes);
                using var bitmap = SKBitmap.Decode(ms);
                if (bitmap == null) return imageBytes;

                if (bitmap.Width <= maxWidth && bitmap.Height <= maxHeight)
                    return imageBytes;

                float ratio = Math.Min((float)maxWidth / bitmap.Width, (float)maxHeight / bitmap.Height);
                int newWidth = (int)(bitmap.Width * ratio);
                int newHeight = (int)(bitmap.Height * ratio);

                using var resizedBitmap = bitmap.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.Medium);
                if (resizedBitmap == null) return imageBytes;

                using var image = SKImage.FromBitmap(resizedBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
                return data.ToArray();
            }
            catch
            {
                return imageBytes;
            }
            */
        }
        public byte[] GenerateVadeRaporuPdf(List<VadeReportItem> items)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(DefaultFontFamily));

                    page.Header().Column(col =>
                    {
                         col.Item().PaddingBottom(10).Row(row =>
                         {
                            row.RelativeItem().Column(c => {
                                c.Item().Text("VADE TAKİP RAPORU").FontSize(20).ExtraBold().FontColor("#1e3a8a");
                                c.Item().Text($"Rapor Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(9);
                            });
                             var logoBytes = LoadLogoBytes();
                             if (logoBytes != null) row.ConstantItem(120).AlignRight().Image(logoBytes).FitArea();
                         });
                         col.Item().LineHorizontal(1).LineColor("#1e3a8a");
                    });

                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3); // Tür
                            columns.RelativeColumn(6); // Cari
                            columns.RelativeColumn(3); // Vade
                            columns.RelativeColumn(3); // Tutar
                            columns.RelativeColumn(3); // Durum
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderStyle).Text("Tür");
                            header.Cell().Element(HeaderStyle).Text("Cari / Muhatap");
                            header.Cell().Element(HeaderStyle).Text("Vade Tarihi");
                            header.Cell().Element(HeaderStyle).AlignRight().Text("Tutar");
                            header.Cell().Element(HeaderStyle).Text("Durum");

                            static QuestPDF.Infrastructure.IContainer HeaderStyle(QuestPDF.Infrastructure.IContainer container)
                                => container.BorderBottom(1).Padding(5).DefaultTextStyle(x => x.Bold());
                        });

                        foreach (var item in items)
                        {
                            table.Cell().Element(CellStyle).Text(item.Tur);
                            table.Cell().Element(CellStyle).Text(item.CariAdi);
                            table.Cell().Element(CellStyle).Text(item.VadeTarihi.ToString("dd.MM.yyyy"));
                            table.Cell().Element(CellStyle).AlignRight().Text(item.Tutar.ToString("N2") + " TL");
                            table.Cell().Element(CellStyle).Text(item.Durum);

                            static QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer container)
                                => container.BorderBottom(0.5f).BorderColor("#EEEEEE").Padding(5);
                        }
                    });
                    
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.DefaultTextStyle(s => s.FontSize(8));
                        x.Span("Sayfa ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });
            return document.GeneratePdf();
        }

        public virtual async Task<(bool Success, string Error)> GenerateMusteriTakipRaporuPdfAsync(MusteriTakipKlasor klasor, List<MusteriTakipDetay> detaylar)
            => await SaveAndOpenHelper($"MusteriTakip_{klasor.CariUnvan.Replace(" ", "_")}.pdf", GenerateMusteriTakipRaporuPdf(klasor, detaylar));

        public virtual Task<byte[]> GenerateMusteriTakipRaporuPdfBytesAsync(MusteriTakipKlasor klasor, List<MusteriTakipDetay> detaylar)
            => Task.FromResult(GenerateMusteriTakipRaporuPdf(klasor, detaylar));

        public byte[] GenerateMusteriTakipRaporuPdf(MusteriTakipKlasor klasor, List<MusteriTakipDetay> detaylar)
        {
            var gorusmeler = detaylar.Where(x => x.Tip == "Gorusme").OrderByDescending(x => x.Tarih).ToList();
            var fiyatlar = detaylar.Where(x => x.Tip == "Fiyat").OrderByDescending(x => x.Tarih).ToList();
            var notlar = detaylar.Where(x => x.Tip == "Not").OrderByDescending(x => x.Tarih).ToList();
            var gorseller = detaylar.Where(x => x.Tip == "Gorsel").OrderByDescending(x => x.Tarih).ToList();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(DefaultFontFamily ?? "Arial"));

                    // HEADER
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("MÜŞTERİ TAKİP & GÖRÜŞME RAPORU").FontSize(18).Bold().FontColor("#1E3A8A");
                                c.Item().Text($"Müşteri / Ünvan: {klasor.CariUnvan}").FontSize(13).Bold().FontColor("#111827");
                                if (!string.IsNullOrEmpty(klasor.Yetkili)) c.Item().Text($"Yetkili Kişi: {klasor.Yetkili}").FontSize(10).FontColor("#4B5563");
                                if (!string.IsNullOrEmpty(klasor.Telefon)) c.Item().Text($"Telefon: {klasor.Telefon}").FontSize(10).FontColor("#4B5563");
                            });

                            row.ConstantItem(140).Column(c =>
                            {
                                c.Item().AlignRight().Text($"Rapor Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(9).FontColor("#6B7280");
                                c.Item().AlignRight().Text($"Durum: {klasor.Etiket ?? "Yeni İletişim"}").FontSize(10).Bold().FontColor("#2563EB");
                            });
                        });

                        col.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor("#2563EB");
                    });

                    // CONTENT
                    page.Content().PaddingVertical(15).Column(col =>
                    {
                        // 1. GÖRÜŞME KAYITLARI
                        col.Item().Text("1. GÖRÜŞME VE TOPLANTI NOTLARI").FontSize(13).Bold().FontColor("#1E40AF");
                        if (gorusmeler.Count == 0)
                        {
                            col.Item().PaddingVertical(4).Text("Kayıtlı görüşme bulunmamaktadır.").Italic().FontColor("#9CA3AF");
                        }
                        else
                        {
                            col.Item().PaddingTop(4).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(100);
                                    columns.ConstantColumn(150);
                                    columns.RelativeColumn();
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderStyle).Text("Tarih");
                                    header.Cell().Element(HeaderStyle).Text("Görüşme Konusu");
                                    header.Cell().Element(HeaderStyle).Text("Detay ve Anlaşmalar");

                                    static QuestPDF.Infrastructure.IContainer HeaderStyle(QuestPDF.Infrastructure.IContainer c)
                                        => c.Background("#F1F5F9").BorderBottom(1).BorderColor("#CBD5E1").Padding(5).DefaultTextStyle(x => x.Bold().FontSize(9));
                                });

                                foreach (var g in gorusmeler)
                                {
                                    table.Cell().Element(CellStyle).Text(g.Tarih.ToString("dd.MM.yyyy HH:mm")).FontSize(9);
                                    table.Cell().Element(CellStyle).Text(g.Baslik ?? "-").Bold().FontSize(9);
                                    table.Cell().Element(CellStyle).Text(g.Icerik ?? "-").FontSize(9);

                                    static QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer c)
                                        => c.BorderBottom(0.5f).BorderColor("#E2E8F0").Padding(5);
                                }
                            });
                        }

                        col.Item().PaddingVertical(12).LineHorizontal(0.5f).LineColor("#E2E8F0");

                        // 2. VERİLEN FİYAT TEKLİFLERİ
                        col.Item().Text("2. VERİLEN FİYAT TEKLİFLERİ").FontSize(13).Bold().FontColor("#047857");
                        if (fiyatlar.Count == 0)
                        {
                            col.Item().PaddingVertical(4).Text("Kayıtlı fiyat teklifi bulunmamaktadır.").Italic().FontColor("#9CA3AF");
                        }
                        else
                        {
                            col.Item().PaddingTop(4).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(90);
                                    columns.ConstantColumn(160);
                                    columns.ConstantColumn(110);
                                    columns.RelativeColumn();
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderStyle).Text("Tarih");
                                    header.Cell().Element(HeaderStyle).Text("Teklif / Ürün");
                                    header.Cell().Element(HeaderStyle).AlignRight().Text("Fiyat");
                                    header.Cell().Element(HeaderStyle).Text("Açıklama / Şartlar");

                                    static QuestPDF.Infrastructure.IContainer HeaderStyle(QuestPDF.Infrastructure.IContainer c)
                                        => c.Background("#F0FDF4").BorderBottom(1).BorderColor("#A7F3D0").Padding(5).DefaultTextStyle(x => x.Bold().FontSize(9));
                                });

                                foreach (var f in fiyatlar)
                                {
                                    table.Cell().Element(CellStyle).Text(f.Tarih.ToString("dd.MM.yyyy")).FontSize(9);
                                    table.Cell().Element(CellStyle).Text(f.Baslik ?? "-").Bold().FontSize(9);
                                    table.Cell().Element(CellStyle).AlignRight().Text($"{(f.FiyatBilgisi.HasValue ? f.FiyatBilgisi.Value.ToString("N2") : "0.00")} {f.ParaBirimi ?? "₺"}").Bold().FontColor("#059669").FontSize(9);
                                    table.Cell().Element(CellStyle).Text(f.Icerik ?? "-").FontSize(9);

                                    static QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer c)
                                        => c.BorderBottom(0.5f).BorderColor("#E2E8F0").Padding(5);
                                }
                            });
                        }

                        col.Item().PaddingVertical(12).LineHorizontal(0.5f).LineColor("#E2E8F0");

                        // 3. GENEL NOTLAR
                        col.Item().Text("3. ÖZEL NOTLAR & HATIRLATMALAR").FontSize(13).Bold().FontColor("#B45309");
                        if (notlar.Count == 0)
                        {
                            col.Item().PaddingVertical(4).Text("Kayıtlı not bulunmamaktadır.").Italic().FontColor("#9CA3AF");
                        }
                        else
                        {
                            foreach (var n in notlar)
                            {
                                col.Item().PaddingTop(4).Border(1).BorderColor("#FDE68A").Background("#FFFBEB").Padding(8).Column(nc =>
                                {
                                    nc.Item().Row(nr =>
                                    {
                                        nr.RelativeItem().Text(n.Baslik ?? "Not").Bold().FontSize(9.5f).FontColor("#92400E");
                                        nr.ConstantItem(100).AlignRight().Text(n.Tarih.ToString("dd.MM.yyyy HH:mm")).FontSize(8.5f).FontColor("#B45309");
                                    });
                                    if (!string.IsNullOrEmpty(n.Icerik))
                                    {
                                        nc.Item().PaddingTop(2).Text(n.Icerik).FontSize(9).FontColor("#78350F");
                                    }
                                });
                            }
                        }

                        // 4. EVRAK VE GÖRSELLER
                        col.Item().PaddingVertical(12).LineHorizontal(0.5f).LineColor("#E2E8F0");
                        col.Item().Text($"4. GÖRSELLER & EVRAKLAR ({gorseller.Count} Adet)").FontSize(13).Bold().FontColor("#6D28D9");
                        if (gorseller.Count == 0)
                        {
                            col.Item().PaddingVertical(4).Text("Kayıtlı görsel veya evrak bulunmamaktadır.").Italic().FontColor("#9CA3AF");
                        }
                        else
                        {
                            foreach (var img in gorseller)
                            {
                                col.Item().PaddingTop(6).Border(1).BorderColor("#DDD6FE").Background("#F5F3FF").Padding(8).Column(ic =>
                                {
                                    ic.Item().Row(ir =>
                                    {
                                        ir.RelativeItem().Text(img.Baslik ?? "Evrak / Görsel").Bold().FontSize(10).FontColor("#5B21B6");
                                        ir.ConstantItem(120).AlignRight().Text(img.Tarih.ToString("dd.MM.yyyy HH:mm")).FontSize(8.5f).FontColor("#7C3AED");
                                    });

                                    byte[]? imgBytes = null;
                                    try
                                    {
                                        if (!string.IsNullOrEmpty(img.GorselBase64))
                                        {
                                            imgBytes = Convert.FromBase64String(img.GorselBase64);
                                        }
                                        else if (!string.IsNullOrEmpty(img.DosyaYolu) && File.Exists(img.DosyaYolu))
                                        {
                                            imgBytes = File.ReadAllBytes(img.DosyaYolu);
                                        }
                                    }
                                    catch { }

                                    if (imgBytes != null && imgBytes.Length > 0)
                                    {
                                        try
                                        {
                                            ic.Item().PaddingTop(6).MaxHeight(200).Image(imgBytes, ImageScaling.FitArea);
                                        }
                                        catch
                                        {
                                            ic.Item().PaddingTop(4).Text($"[Belge Dosyası: {Path.GetFileName(img.DosyaYolu ?? "Belge")}]").Italic().FontSize(8.5f).FontColor("#6B7280");
                                        }
                                    }
                                    else if (!string.IsNullOrEmpty(img.DosyaYolu))
                                    {
                                        ic.Item().PaddingTop(4).Text($"[Dosya Yolu: {img.DosyaYolu}]").Italic().FontSize(8.5f).FontColor("#6B7280");
                                    }
                                });
                            }
                        }
                    });

                    // FOOTER
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.DefaultTextStyle(s => s.FontSize(8).FontColor("#9CA3AF"));
                        x.Span("Ermay Muhasebe • Sayfa ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
