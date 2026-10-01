using ErmayMuhasebe.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ErmayMuhasebe.Services
{
    public class InboxProcessingResult
    {
        public int TotalFound { get; set; }
        public int ProcessedCount { get; set; }
        public int ErrorCount { get; set; }
        public List<string> Messages { get; set; } = new();
    }

    public class MobilGelenKutusuService
    {
        private readonly DatabaseService _dbService;
        private readonly CloudSyncService _cloudSync;
        private readonly IYearContext _yearContext;

        public MobilGelenKutusuService(DatabaseService dbService, CloudSyncService cloudSync, IYearContext yearContext)
        {
            _dbService = dbService;
            _cloudSync = cloudSync;
            _yearContext = yearContext;
        }

        public async Task<InboxProcessingResult> ProcessPendingInboxAsync()
        {
            var result = new InboxProcessingResult();
            if (!_cloudSync.IsConnected)
            {
                result.Messages.Add("Bulut bağlantısı aktif değil.");
                return result;
            }

            int currentYear = _yearContext.CurrentYear;
            var pendingItems = await _cloudSync.PullPendingInboxItemsAsync(currentYear);
            result.TotalFound = pendingItems.Count;

            if (pendingItems.Count == 0)
                return result;

            foreach (var item in pendingItems)
            {
                try
                {
                    string officialNo = await ProcessItemAsync(item);
                    await _cloudSync.UpdateInboxItemStatusAsync(item.Id, "Islendi", officialNo);
                    result.ProcessedCount++;
                    result.Messages.Add($"[{item.IslemTuru}] başarıyla işlendi. Resmi No: {officialNo}");
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    result.Messages.Add($"[{item.IslemTuru}] işlenirken hata oluştu: {ex.Message}");
                    await _cloudSync.UpdateInboxItemStatusAsync(item.Id, "Hata", null, ex.Message);
                }
            }

            // Gelen kutusundaki işlemler bittikten sonra güncel kanonik aynayı buluta it
            if (result.ProcessedCount > 0)
            {
                try
                {
                    await _dbService.SyncToCloudAsync();
                }
                catch (Exception syncEx)
                {
                    result.Messages.Add($"Ayna senkronizasyonu uyarısı: {syncEx.Message}");
                }
            }

            return result;
        }

        private async Task<string> ProcessItemAsync(MobilGelenKutusu item)
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(item.Payload) ? "{}" : item.Payload);
            var root = doc.RootElement;

            return item.IslemTuru switch
            {
                "Tahsilat" => await ProcessTahsilatAsync(root),
                "Ödeme" or "Odeme" => await ProcessOdemeAsync(root),
                "YeniCari" => await ProcessYeniCariAsync(root),
                "TaslakFatura" => await ProcessTaslakFaturaAsync(root),
                "Siparis" => await ProcessSiparisAsync(root),
                "Teklif" => await ProcessTeklifAsync(root),
                _ => throw new InvalidOperationException($"Bilinmeyen işlem türü: {item.IslemTuru}")
            };
        }

        private async Task<string> ProcessTahsilatAsync(JsonElement root)
        {
            int cariId = root.TryGetProperty("cariId", out var cId) ? cId.GetInt32() : 0;
            decimal tutar = root.TryGetProperty("amount", out var am) ? am.GetDecimal() : (root.TryGetProperty("tutar", out var tt) ? tt.GetDecimal() : 0);
            string method = root.TryGetProperty("method", out var mt) ? mt.GetString() ?? "Nakit" : "Nakit";
            string desc = root.TryGetProperty("description", out var ds) ? ds.GetString() ?? "" : "";
            DateTime date = root.TryGetProperty("date", out var dt) && DateTime.TryParse(dt.GetString(), out var parsedDt) ? parsedDt : DateTime.Now;

            var cari = await _dbService.GetCariKartAsync(cariId);
            if (cari == null) throw new InvalidOperationException($"Cari bulunamadı (ID: {cariId})");

            string evrakNo = $"TS-{_yearContext.CurrentYear}-{DateTime.Now:MMddHHmmss}";

            var bankalar = await _dbService.GetBankalarAsync();
            var targetBanka = bankalar.Find(b => method.Contains("Nakit") ? b.KartTuru == "Kasa" : b.KartTuru != "Kasa") ?? bankalar.FirstOrDefault();
            if (targetBanka == null)
            {
                targetBanka = new BankaKart
                {
                    BankaAdi = method.Contains("Nakit") ? "MERKEZ KASA" : "GENEL BANKA HESABI",
                    KartTuru = method.Contains("Nakit") ? "Kasa" : "Vadesiz"
                };
                await _dbService.SaveBankaKartAsync(targetBanka);
            }
            int bankaId = targetBanka.Id;

            if (method.Contains("Nakit"))
            {
                var kh = new KasaHareket
                {
                    KasaId = bankaId,
                    CariId = cari.Id,
                    CariUnvan = cari.Unvan,
                    Tarih = date,
                    EvrakNo = evrakNo,
                    IslemTuru = "Tahsilat (Nakit)",
                    Aciklama = $"[Mobil Gelen Kutusu] {desc}",
                    Giren = tutar
                };
                await _dbService.SaveKasaHareketAsync(kh);
            }
            else
            {
                var bh = new BankaHareket
                {
                    BankaId = bankaId,
                    BankaAdi = targetBanka?.BankaAdi,
                    CariId = cari.Id,
                    CariUnvan = cari.Unvan,
                    Tarih = date,
                    EvrakNo = evrakNo,
                    IslemTuru = $"Tahsilat ({method})",
                    Aciklama = $"[Mobil Gelen Kutusu] {desc}",
                    Giren = tutar
                };
                await _dbService.SaveBankaHareketAsync(bh);
            }

            return evrakNo;
        }

        private async Task<string> ProcessOdemeAsync(JsonElement root)
        {
            int cariId = root.TryGetProperty("cariId", out var cId) ? cId.GetInt32() : 0;
            decimal tutar = root.TryGetProperty("amount", out var am) ? am.GetDecimal() : (root.TryGetProperty("tutar", out var tt) ? tt.GetDecimal() : 0);
            string method = root.TryGetProperty("method", out var mt) ? mt.GetString() ?? "Nakit" : "Nakit";
            string desc = root.TryGetProperty("description", out var ds) ? ds.GetString() ?? "" : "";
            DateTime date = root.TryGetProperty("date", out var dt) && DateTime.TryParse(dt.GetString(), out var parsedDt) ? parsedDt : DateTime.Now;

            var cari = await _dbService.GetCariKartAsync(cariId);
            if (cari == null) throw new InvalidOperationException($"Cari bulunamadı (ID: {cariId})");

            string evrakNo = $"OD-{_yearContext.CurrentYear}-{DateTime.Now:MMddHHmmss}";

            var bankalar = await _dbService.GetBankalarAsync();
            var targetBanka = bankalar.Find(b => method.Contains("Nakit") ? b.KartTuru == "Kasa" : b.KartTuru != "Kasa") ?? bankalar.FirstOrDefault();
            if (targetBanka == null)
            {
                targetBanka = new BankaKart
                {
                    BankaAdi = method.Contains("Nakit") ? "MERKEZ KASA" : "GENEL BANKA HESABI",
                    KartTuru = method.Contains("Nakit") ? "Kasa" : "Vadesiz"
                };
                await _dbService.SaveBankaKartAsync(targetBanka);
            }
            int bankaId = targetBanka.Id;

            if (method.Contains("Nakit"))
            {
                var kh = new KasaHareket
                {
                    KasaId = bankaId,
                    CariId = cari.Id,
                    CariUnvan = cari.Unvan,
                    Tarih = date,
                    EvrakNo = evrakNo,
                    IslemTuru = "Ödeme (Nakit)",
                    Aciklama = $"[Mobil Gelen Kutusu] {desc}",
                    Cikan = tutar
                };
                await _dbService.SaveKasaHareketAsync(kh);
            }
            else
            {
                var bh = new BankaHareket
                {
                    BankaId = bankaId,
                    BankaAdi = targetBanka?.BankaAdi,
                    CariId = cari.Id,
                    CariUnvan = cari.Unvan,
                    Tarih = date,
                    EvrakNo = evrakNo,
                    IslemTuru = $"Ödeme ({method})",
                    Aciklama = $"[Mobil Gelen Kutusu] {desc}",
                    Cikan = tutar
                };
                await _dbService.SaveBankaHareketAsync(bh);
            }

            return evrakNo;
        }

        private async Task<string> ProcessYeniCariAsync(JsonElement root)
        {
            string unvan = root.TryGetProperty("unvan", out var u) ? u.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(unvan)) throw new InvalidOperationException("Cari unvanı boş olamaz.");

            var newCari = new CariKart
            {
                Unvan = unvan,
                Telefon = root.TryGetProperty("telefon", out var tel) ? tel.GetString() : null,
                Email = root.TryGetProperty("email", out var em) ? em.GetString() : null,
                VergiDairesi = root.TryGetProperty("vergiDairesi", out var vd) ? vd.GetString() : null,
                VergiNo = root.TryGetProperty("vergiNo", out var vn) ? vn.GetString() : null,
                Il = root.TryGetProperty("il", out var il) ? il.GetString() : null,
                Ilce = root.TryGetProperty("ilce", out var ilce) ? ilce.GetString() : null,
                Adres = root.TryGetProperty("adres", out var adr) ? adr.GetString() : null,
                Yetkili = root.TryGetProperty("yetkili", out var ytk) ? ytk.GetString() : null,
                Tur = root.TryGetProperty("tur", out var tr) ? tr.GetString() : "Müşteri",
                Aciklama = "[Mobil Gelen Kutusu Talebi]"
            };

            await _dbService.SaveCariKartAsync(newCari);
            return newCari.CariKod ?? $"CAR-{newCari.Id}";
        }

        private async Task<string> ProcessTaslakFaturaAsync(JsonElement root)
        {
            int cariId = root.TryGetProperty("cariId", out var cId) ? cId.GetInt32() : 0;
            var cari = await _dbService.GetCariKartAsync(cariId);
            if (cari == null) throw new InvalidOperationException($"Cari bulunamadı (ID: {cariId})");

            string tur = root.TryGetProperty("tur", out var tr) ? tr.GetString() ?? "Satış" : "Satış";
            DateTime date = root.TryGetProperty("tarih", out var dt) && DateTime.TryParse(dt.GetString(), out var parsedDt) ? parsedDt : DateTime.Now;

            string fNo = await _dbService.GetNextFaturaNoAsync(tur);

            var fatura = new Fatura
            {
                CariId = cari.Id,
                CariUnvan = cari.Unvan,
                FaturaNo = fNo,
                Tur = tur,
                Tarih = date,
                VadeTarihi = date.AddDays(cari.VadeGunu > 0 ? cari.VadeGunu : 30),
                Aciklama = "[Mobil Gelen Kutusundan Oluşturuldu]"
            };

            var details = new List<FaturaDetay>();
            if (root.TryGetProperty("details", out var detArr) && detArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in detArr.EnumerateArray())
                {
                    int sId = el.TryGetProperty("stokId", out var s) ? s.GetInt32() : 0;
                    decimal miktar = el.TryGetProperty("miktar", out var mq) ? mq.GetDecimal() : 1;
                    decimal fiyat = el.TryGetProperty("birimFiyat", out var bf) ? bf.GetDecimal() : 0;
                    int kdv = el.TryGetProperty("kdvOrani", out var ko) ? ko.GetInt32() : 20;

                    var stok = await _dbService.GetStokKartAsync(sId);
                    details.Add(new FaturaDetay
                    {
                        StokId = sId,
                        StokKodu = stok?.StokKodu,
                        StokAdi = stok?.StokAdi ?? "Stok",
                        Miktar = miktar,
                        BirimFiyat = fiyat,
                        KDVOrani = kdv,
                        KdvTutari = Math.Round(miktar * fiyat * kdv / 100m, 2),
                        ToplamTutar = Math.Round(miktar * fiyat * (1 + (kdv / 100m)), 2)
                    });
                }
            }

            fatura.AraToplam = details.Sum(d => d.Miktar * d.BirimFiyat);
            fatura.ToplamKDV = details.Sum(d => d.KdvTutari);
            fatura.GenelToplam = details.Sum(d => d.ToplamTutar);

            await _dbService.SaveFaturaWithTransactionAsync(fatura, details, cari);
            return fNo;
        }

        private async Task<string> ProcessSiparisAsync(JsonElement root)
        {
            int cariId = root.TryGetProperty("cariId", out var cId) ? cId.GetInt32() : 0;
            var cari = await _dbService.GetCariKartAsync(cariId);
            if (cari == null) throw new InvalidOperationException($"Cari bulunamadı (ID: {cariId})");

            string siparisNo = $"SIP-{_yearContext.CurrentYear}-{DateTime.Now:MMddHHmmss}";

            var siparis = new Siparis
            {
                CariId = cari.Id,
                CariUnvan = cari.Unvan,
                SiparisNo = siparisNo,
                Tarih = DateTime.Now,
                Durum = "Beklemede",
                Aciklama = "[Mobil Gelen Kutusu]"
            };

            var details = new List<SiparisDetay>();
            if (root.TryGetProperty("details", out var detArr) && detArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in detArr.EnumerateArray())
                {
                    int sId = el.TryGetProperty("stokId", out var s) ? s.GetInt32() : 0;
                    decimal miktar = el.TryGetProperty("miktar", out var mq) ? mq.GetDecimal() : 1;
                    decimal fiyat = el.TryGetProperty("birimFiyat", out var bf) ? bf.GetDecimal() : 0;

                    var stok = await _dbService.GetStokKartAsync(sId);
                    details.Add(new SiparisDetay
                    {
                        StokId = sId,
                        StokAdi = stok?.StokAdi ?? "Stok",
                        Miktar = miktar,
                        BirimFiyat = fiyat,
                        Tutar = miktar * fiyat
                    });
                }
            }

            await _dbService.SaveSiparisWithDetailsAsync(siparis, details);
            return siparisNo;
        }

        private async Task<string> ProcessTeklifAsync(JsonElement root)
        {
            int cariId = root.TryGetProperty("cariId", out var cId) ? cId.GetInt32() : 0;
            var cari = await _dbService.GetCariKartAsync(cariId);
            if (cari == null) throw new InvalidOperationException($"Cari bulunamadı (ID: {cariId})");

            string teklifNo = $"TEK-{_yearContext.CurrentYear}-{DateTime.Now:MMddHHmmss}";

            var teklif = new Teklif
            {
                CariId = cari.Id,
                CariUnvan = cari.Unvan,
                TeklifNo = teklifNo,
                Tarih = DateTime.Now,
                Durum = "Hazırlandı",
                Aciklama = "[Mobil Gelen Kutusu]"
            };

            var details = new List<TeklifDetay>();
            if (root.TryGetProperty("details", out var detArr) && detArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in detArr.EnumerateArray())
                {
                    int sId = el.TryGetProperty("stokId", out var s) ? s.GetInt32() : 0;
                    decimal miktar = el.TryGetProperty("miktar", out var mq) ? mq.GetDecimal() : 1;
                    decimal fiyat = el.TryGetProperty("birimFiyat", out var bf) ? bf.GetDecimal() : 0;

                    var stok = await _dbService.GetStokKartAsync(sId);
                    details.Add(new TeklifDetay
                    {
                        StokId = sId,
                        StokAdi = stok?.StokAdi ?? "Stok",
                        Miktar = miktar,
                        BirimFiyat = fiyat,
                        Tutar = miktar * fiyat
                    });
                }
            }

            await _dbService.SaveTeklifWithDetailsAsync(teklif, details);
            return teklifNo;
        }
    }
}
