using System;
using System.Linq;
using System.Threading.Tasks;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Repositories;

namespace ErmayMuhasebe.Services
{
    public class FinansService : IFinansService
    {
        private readonly DatabaseService _dbService;
        private readonly IUnitOfWork _uow;
        private readonly CloudSyncService _syncService;

        public FinansService(DatabaseService dbService, IUnitOfWork uow, CloudSyncService syncService)
        {
            _dbService = dbService;
            _uow = uow;
            _syncService = syncService;
        }

        public async Task<bool> SaveTransactionAsync(FinancialTransactionRequest request)
        {
            if (request.Cari == null || request.Amount <= 0) return false;

            var db = await _dbService.GetConnectionAsync();
            var refId = !string.IsNullOrEmpty(request.ExistingRefId) 
                ? request.ExistingRefId 
                : Guid.NewGuid().ToString("N");
            var evrakNo = !string.IsNullOrEmpty(request.ExistingEvrakNo) 
                ? request.ExistingEvrakNo 
                : (GetEvrakNoPrefix(request.Method) + DateTime.Now.ToString("yyMMddHHmmss"));

            CariHareket? mainCH = null;
            CariHareket? supplierCH = null;
            KasaHareket? kasaHareket = null;
            KasaHareket? ciroKasaHareket = null;
            BankaHareket? bankaHareket = null;
            BankaHareket? ciroBankaHareket = null;
            KrediKartiIslem? kk = null;
            EftIslem? eft = null;
            BankaKart? updatedFinancialAccount = null;

            // Transaction başlatıyoruz
            bool success = false;
            await db.RunInTransactionAsync(tran =>
            {
                bool isOdeme = request.TransactionType == "Ödeme" || request.TransactionType == "Borç Dekontu";

                // Eğer mevcut bir işlem düzenleniyorsa, ID'yi koruyarak yerinde (in-place) güncelleme yapıyoruz
                if (request.ExistingHareketId > 0)
                {
                    var oldCH = tran.Find<CariHareket>(request.ExistingHareketId);
                    if (oldCH != null)
                    {
                        // 1. Eski cari bakiyesini geri al
                        var oldCust = tran.Find<CariKart>(oldCH.CariId);
                        if (oldCust != null)
                        {
                            oldCust.Borc -= oldCH.Borc;
                            oldCust.Alacak -= oldCH.Alacak;
                            tran.Update(oldCust);
                        }

                        // 2. Eski tedarikçi/ciro hareketini temizle ve bakiyesini geri al
                        string baseRef = !string.IsNullOrEmpty(oldCH.RefId) ? (oldCH.RefId.EndsWith("-SUP") ? oldCH.RefId.Substring(0, oldCH.RefId.Length - 4) : oldCH.RefId) : "";
                        string supRef = !string.IsNullOrEmpty(baseRef) ? baseRef + "-SUP" : "";
                        string oldEvrak = oldCH.EvrakNo ?? "";
                        string supEvrak = !string.IsNullOrEmpty(oldEvrak) ? oldEvrak + "-SUP" : "";

                        var oldSupHarekets = tran.Query<CariHareket>("SELECT * FROM CariHareket WHERE (RefId = ? OR EvrakNo = ?) AND Id != ?", supRef, supEvrak, oldCH.Id);
                        foreach (var sh in oldSupHarekets)
                        {
                            var supCari = tran.Find<CariKart>(sh.CariId);
                            if (supCari != null)
                            {
                                supCari.Borc -= sh.Borc;
                                supCari.Alacak -= sh.Alacak;
                                tran.Update(supCari);
                            }
                            tran.Delete(sh);
                        }

                        // 3. Eski Kasa / Banka hareketlerini bul, bakiyelerini düzelt ve sil
                        var oldKasas = tran.Query<KasaHareket>("SELECT * FROM KasaHareket WHERE (RefId = ? OR RefId = ? OR EvrakNo = ? OR EvrakNo = ?)", baseRef, supRef, oldEvrak, supEvrak);
                        foreach (var kh in oldKasas)
                        {
                            var fa = tran.Find<BankaKart>(kh.KasaId);
                            if (fa != null)
                            {
                                fa.GuncelBakiye -= (kh.Giren - kh.Cikan);
                                tran.Update(fa);
                            }
                            tran.Delete(kh);
                        }

                        var oldBankas = tran.Query<BankaHareket>("SELECT * FROM BankaHareket WHERE (RefId = ? OR RefId = ? OR EvrakNo = ? OR EvrakNo = ?)", baseRef, supRef, oldEvrak, supEvrak);
                        foreach (var bh in oldBankas)
                        {
                            var fa = tran.Find<BankaKart>(bh.BankaId);
                            if (fa != null)
                            {
                                fa.GuncelBakiye -= (bh.Giren - bh.Cikan);
                                tran.Update(fa);
                            }
                            tran.Delete(bh);
                        }

                        // 4. Eski KK / EFT detay kayıtlarını sil
                        if (!string.IsNullOrEmpty(baseRef))
                        {
                            tran.Execute("DELETE FROM KrediKartiIslem WHERE OnayKodu = ?", baseRef);
                            tran.Execute("DELETE FROM EftIslem WHERE DekontNo = ?", baseRef);
                        }

                        // 5. Ana cari hareketini ID'sini değiştirmeden yerinde güncelle
                        mainCH = oldCH;
                        mainCH.CariId = request.Cari.Id;
                        mainCH.CariUnvan = request.Cari.Unvan ?? "";
                        mainCH.Tarih = request.Date;
                        mainCH.EvrakNo = evrakNo;
                        mainCH.RefId = refId;
                        mainCH.IslemTuru = $"{request.TransactionType} ({GetMethodAbbreviation(request.Method)})";
                        mainCH.Aciklama = $"[{request.Method}] {request.Description}".Trim();
                        mainCH.Borc = isOdeme ? request.Amount : 0;
                        mainCH.Alacak = !isOdeme ? request.Amount : 0;
                        mainCH.YonlendirilenCariId = request.DirectedSupplier?.Id;
                        mainCH.YonlendirilenCariUnvan = request.DirectedSupplier?.Unvan;
                        tran.Update(mainCH);
                    }
                }

                // Eğer yeni bir işlemse normal ekleme yapıyoruz
                if (mainCH == null)
                {
                    mainCH = new CariHareket
                    {
                        CariId = request.Cari.Id,
                        CariUnvan = request.Cari.Unvan ?? "",
                        Tarih = request.Date,
                        EvrakNo = evrakNo,
                        RefId = refId,
                        IslemTuru = $"{request.TransactionType} ({GetMethodAbbreviation(request.Method)})",
                        Aciklama = $"[{request.Method}] {request.Description}".Trim(),
                        Borc = isOdeme ? request.Amount : 0,
                        Alacak = !isOdeme ? request.Amount : 0,
                        YonlendirilenCariId = request.DirectedSupplier?.Id,
                        YonlendirilenCariUnvan = request.DirectedSupplier?.Unvan
                    };
                    tran.Insert(mainCH);
                }

                // Müşteri cari bakiyesini güncelle
                var customer = tran.Find<CariKart>(request.Cari.Id);
                if (customer != null)
                {
                    customer.Borc += mainCH.Borc;
                    customer.Alacak += mainCH.Alacak;
                    tran.Update(customer);
                }

                // 2. Kasa veya Banka Hareketi Ekleme (Eğer finans hesabı seçildiyse)
                if (request.SelectedKasaOrBanka != null)
                {
                    var financialAccount = tran.Find<BankaKart>(request.SelectedKasaOrBanka.Id);
                    if (financialAccount != null)
                    {
                        bool isKasa = financialAccount.KartTuru == "Kasa";

                        if (isKasa)
                        {
                            // Kasa Giriş/Çıkış hareketi
                            kasaHareket = new KasaHareket
                            {
                                KasaId = financialAccount.Id,
                                CariId = request.Cari.Id,
                                CariUnvan = request.Cari.Unvan,
                                Tarih = request.Date,
                                EvrakNo = evrakNo,
                                RefId = refId,
                                IslemTuru = mainCH.IslemTuru,
                                Aciklama = $"{request.Cari.Unvan} - {request.TransactionType} ({request.Description})",
                                Giren = !isOdeme ? request.Amount : 0,
                                Cikan = isOdeme ? request.Amount : 0,
                                YonlendirilenCariId = request.DirectedSupplier?.Id,
                                YonlendirilenCariUnvan = request.DirectedSupplier?.Unvan
                            };
                            tran.Insert(kasaHareket);

                            // Kasa bakiyesini güncelle
                            financialAccount.GuncelBakiye += (kasaHareket.Giren - kasaHareket.Cikan);
                        }
                        else
                        {
                            // Banka Giriş/Çıkış hareketi
                            bankaHareket = new BankaHareket
                            {
                                BankaId = financialAccount.Id,
                                CariId = request.Cari.Id,
                                CariUnvan = request.Cari.Unvan,
                                Tarih = request.Date,
                                EvrakNo = evrakNo,
                                RefId = refId,
                                IslemTuru = mainCH.IslemTuru,
                                Aciklama = $"{request.Cari.Unvan} - {request.TransactionType} ({request.Description})",
                                Giren = !isOdeme ? request.Amount : 0,
                                Cikan = isOdeme ? request.Amount : 0,
                                Tutar = request.Amount,
                                YonlendirilenCariId = request.DirectedSupplier?.Id,
                                YonlendirilenCariUnvan = request.DirectedSupplier?.Unvan
                            };
                            tran.Insert(bankaHareket);

                            // Banka bakiyesini güncelle
                            financialAccount.GuncelBakiye += (bankaHareket.Giren - bankaHareket.Cikan);
                        }

                        tran.Update(financialAccount);
                        updatedFinancialAccount = financialAccount;
                    }
                }

                // 3. Detay Tablolarına Kayıt (Kredi Kartı / EFT)
                if (request.Method == "Kredi Kartı")
                {
                    kk = new KrediKartiIslem
                    {
                        MusteriId = request.Cari.Id,
                        MusteriUnvan = request.Cari.Unvan ?? "",
                        Tarih = request.Date,
                        Tutar = request.Amount,
                        Banka = request.BankaAdi ?? "",
                        KartNo = request.KartHesapNo ?? "",
                        OnayKodu = refId, // RefId ile doğrudan ilişkilendiriyoruz
                        Durum = request.DirectedSupplier != null ? "Tedarikçiye Verildi" : "Portföyde",
                        Aciklama = request.Description ?? "",
                        IslemTuru = request.TransactionType,
                        SlipDosyaYolu = request.SlipDekontPath ?? "",
                        YonlendirilenCariId = request.DirectedSupplier?.Id,
                        YonlendirilenCariUnvan = request.DirectedSupplier?.Unvan
                    };
                    tran.Insert(kk);
                }
                else if (request.Method == "Havale / EFT" || request.Method == "Havale/EFT" || request.Method == "EFT")
                {
                    eft = new EftIslem
                    {
                        MusteriId = request.Cari.Id,
                        MusteriUnvan = request.Cari.Unvan ?? "",
                        Tarih = request.Date,
                        Tutar = request.Amount,
                        Banka = request.BankaAdi ?? "",
                        BankaId = request.SelectedKasaOrBanka?.Id ?? 0,
                        HesapNo = request.KartHesapNo ?? "",
                        DekontNo = refId, // RefId ile doğrudan ilişkilendiriyoruz
                        Durum = request.DirectedSupplier != null ? "Tedarikçiye Yönlendirildi" : "Tamamlandı",
                        Aciklama = request.Description ?? "",
                        IslemTuru = request.TransactionType,
                        DekontPath = request.SlipDekontPath ?? "",
                        YonlendirilenCariId = request.DirectedSupplier?.Id,
                        YonlendirilenCariUnvan = request.DirectedSupplier?.Unvan
                    };
                    tran.Insert(eft);
                }

                // 4. Yönlendirilen Tedarikçi (Ciro / Endorsement) Kayıtları
                if (request.DirectedSupplier != null)
                {
                    // Tedarikçi Cari Hareketi (Tedarikçi Borçlandırılır - Ödeme Yapıldı)
                    supplierCH = new CariHareket
                    {
                        CariId = request.DirectedSupplier.Id,
                        CariUnvan = request.DirectedSupplier.Unvan ?? "",
                        Tarih = request.YonlendirmeTarihi ?? request.Date,
                        EvrakNo = evrakNo + "-SUP",
                        RefId = refId + "-SUP",
                        IslemTuru = $"Ödeme ({GetMethodAbbreviation(request.Method)} Ciro)",
                        Aciklama = $"[Ciro] {request.Cari.Unvan} üzerinden ciro edilen {request.Method} tahsilatı. ({request.Description})".Trim(),
                        Borc = request.Amount, // Tedarikçiye yapılan ödeme (Borç)
                        Alacak = 0
                    };
                    tran.Insert(supplierCH);

                    // Tedarikçi cari bakiyesini güncelle
                    var supplier = tran.Find<CariKart>(request.DirectedSupplier.Id);
                    if (supplier != null)
                    {
                        supplier.Borc += supplierCH.Borc;
                        supplier.Alacak += supplierCH.Alacak;
                        tran.Update(supplier);
                    }

                    // Kasa/Banka ciro çıkış hareketi (Kullanıcı onaylı)
                    if (request.SelectedKasaOrBanka != null)
                    {
                        var financialAccount = tran.Find<BankaKart>(request.SelectedKasaOrBanka.Id);
                        if (financialAccount != null)
                        {
                            bool isKasa = financialAccount.KartTuru == "Kasa";

                            if (isKasa)
                            {
                                ciroKasaHareket = new KasaHareket
                                {
                                    KasaId = financialAccount.Id,
                                    CariId = request.DirectedSupplier.Id,
                                    CariUnvan = request.DirectedSupplier.Unvan,
                                    Tarih = request.YonlendirmeTarihi ?? request.Date,
                                    EvrakNo = evrakNo + "-SUP",
                                    RefId = refId + "-SUP",
                                    IslemTuru = supplierCH.IslemTuru,
                                    Aciklama = $"Ciro Çıkışı -> {request.DirectedSupplier.Unvan} ({request.Cari.Unvan} üzerinden)",
                                    Giren = 0,
                                    Cikan = request.Amount
                                };
                                tran.Insert(ciroKasaHareket);

                                // Kasa bakiyesini güncelle (Giriş yapılmıştı, şimdi çıkış yapıyoruz, net etki 0)
                                financialAccount.GuncelBakiye += (ciroKasaHareket.Giren - ciroKasaHareket.Cikan);
                            }
                            else
                            {
                                ciroBankaHareket = new BankaHareket
                                {
                                    BankaId = financialAccount.Id,
                                    CariId = request.DirectedSupplier.Id,
                                    CariUnvan = request.DirectedSupplier.Unvan,
                                    Tarih = request.YonlendirmeTarihi ?? request.Date,
                                    EvrakNo = evrakNo + "-SUP",
                                    RefId = refId + "-SUP",
                                    IslemTuru = supplierCH.IslemTuru,
                                    Aciklama = $"Ciro Çıkışı -> {request.DirectedSupplier.Unvan} ({request.Cari.Unvan} üzerinden)",
                                    Giren = 0,
                                    Cikan = request.Amount,
                                    Tutar = request.Amount
                                };
                                tran.Insert(ciroBankaHareket);

                                // Banka bakiyesini güncelle
                                financialAccount.GuncelBakiye += (ciroBankaHareket.Giren - ciroBankaHareket.Cikan);
                            }

                            tran.Update(financialAccount);
                            updatedFinancialAccount = financialAccount;
                        }
                    }
                }

                success = true;
            });

            if (success)
            {
                try
                {
                    await _uow.Cariler.RecalculateBalanceAsync(request.Cari.Id);
                    if (request.DirectedSupplier != null)
                    {
                        await _uow.Cariler.RecalculateBalanceAsync(request.DirectedSupplier.Id);
                    }

                    var updatedCari = await _uow.Cariler.GetByIdAsync(request.Cari.Id);
                    if (updatedCari != null) 
                    {
                        await _dbService.SaveCariAsync(updatedCari);
                        await _syncService.SyncCariAsync(updatedCari);
                    }

                    if (request.DirectedSupplier != null)
                    {
                        var updatedSupplier = await _uow.Cariler.GetByIdAsync(request.DirectedSupplier.Id);
                        if (updatedSupplier != null) 
                        {
                            await _dbService.SaveCariAsync(updatedSupplier);
                            await _syncService.SyncCariAsync(updatedSupplier);
                        }
                    }

                    // --- CANLI BULUT SENKRONİZASYONU (MOBİL VE BULUT İÇİN ANINDA EŞİTLEME) ---
                    if (mainCH != null) await _syncService.SyncCariHareketAsync(mainCH);
                    if (supplierCH != null) await _syncService.SyncCariHareketAsync(supplierCH);
                    if (kasaHareket != null) await _syncService.SyncKasaHareketAsync(kasaHareket);
                    if (ciroKasaHareket != null) await _syncService.SyncKasaHareketAsync(ciroKasaHareket);
                    if (bankaHareket != null) await _syncService.SyncBankaHareketAsync(bankaHareket);
                    if (ciroBankaHareket != null) await _syncService.SyncBankaHareketAsync(ciroBankaHareket);
                    if (updatedFinancialAccount != null) await _syncService.SyncBankaAsync(updatedFinancialAccount);
                    if (kk != null) await _syncService.SyncKrediKartiIslemAsync(kk);
                    if (eft != null) await _syncService.SyncEftIslemAsync(eft);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FinansService] Bulut esitleme hatasi: {ex.Message}");
                }
            }

            return success;
        }

        public async Task<bool> DeleteTransactionAsync(CariHareket hareket)
        {
            if (hareket == null) return false;

            var db = await _dbService.GetConnectionAsync();
            var refId = hareket.RefId;
            var evrakNo = hareket.EvrakNo;
            var hareketId = hareket.Id;

            // Eğer RefId boşsa ve evrakNo da yoksa doğrudan ID üzerinden silme yapıyoruz
            var baseRefId = !string.IsNullOrEmpty(refId)
                ? (refId.EndsWith("-SUP") ? refId.Substring(0, refId.Length - 4) : refId)
                : "";
            var supRefId = !string.IsNullOrEmpty(baseRefId) ? baseRefId + "-SUP" : "";

            List<CariHareket> chToDelete = new();
            List<KasaHareket> khToDelete = new();
            List<BankaHareket> bhToDelete = new();
            List<KrediKartiIslem> kkToDelete = new();
            List<EftIslem> eftToDelete = new();
            List<Cek> cekToDelete = new();
            List<int> affectedCariIds = new();
            List<int> affectedKasaBankaIds = new();

            bool success = false;
            await db.RunInTransactionAsync(tran =>
            {
                // 1. Cari Hareketleri Bul ve Bakiye Düzeltmesi Yap
                chToDelete = new List<CariHareket>();
                CariHareket? primary = null;
                if (hareketId > 0)
                {
                    primary = tran.Find<CariHareket>(hareketId);
                }
                else if (!string.IsNullOrEmpty(evrakNo))
                {
                    primary = tran.Query<CariHareket>("SELECT * FROM CariHareket WHERE EvrakNo = ?", evrakNo).FirstOrDefault();
                    if (primary != null) hareketId = primary.Id;
                }

                if (primary != null)
                {
                    chToDelete.Add(primary);
                    if (string.IsNullOrEmpty(baseRefId) && !string.IsNullOrEmpty(primary.RefId))
                    {
                        refId = primary.RefId;
                        baseRefId = refId.EndsWith("-SUP") ? refId.Substring(0, refId.Length - 4) : refId;
                        supRefId = baseRefId + "-SUP";
                    }
                    if (string.IsNullOrEmpty(evrakNo) && !string.IsNullOrEmpty(primary.EvrakNo))
                    {
                        evrakNo = primary.EvrakNo;
                    }
                }
                if (!string.IsNullOrEmpty(baseRefId))
                {
                    var refMatches = tran.Query<CariHareket>("SELECT * FROM CariHareket WHERE RefId = ? OR RefId = ?", baseRefId, supRefId);
                    foreach (var m in refMatches)
                    {
                        if (!chToDelete.Any(x => x.Id == m.Id)) chToDelete.Add(m);
                    }
                }
                if (!string.IsNullOrEmpty(evrakNo))
                {
                    var evrakMatches = tran.Query<CariHareket>("SELECT * FROM CariHareket WHERE EvrakNo = ?", evrakNo);
                    foreach (var m in evrakMatches)
                    {
                        if (!chToDelete.Any(x => x.Id == m.Id)) chToDelete.Add(m);
                    }
                }

                foreach (var h in chToDelete)
                {
                    affectedCariIds.Add(h.CariId);
                    if (h.YonlendirilenCariId.HasValue) affectedCariIds.Add(h.YonlendirilenCariId.Value);

                    var cari = tran.Find<CariKart>(h.CariId);
                    if (cari != null)
                    {
                        cari.Borc -= h.Borc;
                        cari.Alacak -= h.Alacak;
                        tran.Update(cari);
                    }
                    tran.Delete(h);
                }

                // 2. Kasa Hareketlerini Geri Al ve Sil
                khToDelete = new List<KasaHareket>();
                if (!string.IsNullOrEmpty(baseRefId))
                {
                    var refMatches = tran.Query<KasaHareket>("SELECT * FROM KasaHareket WHERE RefId = ? OR RefId = ?", baseRefId, supRefId);
                    foreach (var k in refMatches) if (!khToDelete.Any(x => x.Id == k.Id)) khToDelete.Add(k);
                }
                if (!string.IsNullOrEmpty(evrakNo))
                {
                    var evrakMatches = tran.Query<KasaHareket>("SELECT * FROM KasaHareket WHERE EvrakNo = ?", evrakNo);
                    foreach (var k in evrakMatches) if (!khToDelete.Any(x => x.Id == k.Id)) khToDelete.Add(k);
                }

                foreach (var kh in khToDelete)
                {
                    affectedKasaBankaIds.Add(kh.KasaId);
                    var kasa = tran.Find<BankaKart>(kh.KasaId);
                    if (kasa != null)
                    {
                        kasa.GuncelBakiye -= (kh.Giren - kh.Cikan);
                        tran.Update(kasa);
                    }
                    tran.Delete(kh);
                }

                // 3. Banka Hareketlerini Geri Al ve Sil
                bhToDelete = new List<BankaHareket>();
                if (!string.IsNullOrEmpty(baseRefId))
                {
                    var refMatches = tran.Query<BankaHareket>("SELECT * FROM BankaHareket WHERE RefId = ? OR RefId = ?", baseRefId, supRefId);
                    foreach (var b in refMatches) if (!bhToDelete.Any(x => x.Id == b.Id)) bhToDelete.Add(b);
                }
                if (!string.IsNullOrEmpty(evrakNo))
                {
                    var evrakMatches = tran.Query<BankaHareket>("SELECT * FROM BankaHareket WHERE EvrakNo = ?", evrakNo);
                    foreach (var b in evrakMatches) if (!bhToDelete.Any(x => x.Id == b.Id)) bhToDelete.Add(b);
                }

                foreach (var bh in bhToDelete)
                {
                    affectedKasaBankaIds.Add(bh.BankaId);
                    var banka = tran.Find<BankaKart>(bh.BankaId);
                    if (banka != null)
                    {
                        banka.GuncelBakiye -= (bh.Giren - bh.Cikan);
                        tran.Update(banka);
                    }
                    tran.Delete(bh);
                }

                // 4. Detay Tablolarından Kayıtları Sil
                kkToDelete = new List<KrediKartiIslem>();
                if (!string.IsNullOrEmpty(baseRefId))
                {
                    var refMatches = tran.Query<KrediKartiIslem>("SELECT * FROM KrediKartiIslem WHERE OnayKodu = ?", baseRefId);
                    foreach (var k in refMatches) if (!kkToDelete.Any(x => x.Id == k.Id)) kkToDelete.Add(k);
                }
                if (!string.IsNullOrEmpty(evrakNo))
                {
                    var evrakMatches = tran.Query<KrediKartiIslem>("SELECT * FROM KrediKartiIslem WHERE OnayKodu = ?", evrakNo);
                    foreach (var k in evrakMatches) if (!kkToDelete.Any(x => x.Id == k.Id)) kkToDelete.Add(k);
                }
                foreach (var kkItem in kkToDelete) tran.Delete(kkItem);

                eftToDelete = new List<EftIslem>();
                if (!string.IsNullOrEmpty(baseRefId))
                {
                    var refMatches = tran.Query<EftIslem>("SELECT * FROM EftIslem WHERE DekontNo = ?", baseRefId);
                    foreach (var e in refMatches) if (!eftToDelete.Any(x => x.Id == e.Id)) eftToDelete.Add(e);
                }
                if (!string.IsNullOrEmpty(evrakNo))
                {
                    var evrakMatches = tran.Query<EftIslem>("SELECT * FROM EftIslem WHERE DekontNo = ?", evrakNo);
                    foreach (var e in evrakMatches) if (!eftToDelete.Any(x => x.Id == e.Id)) eftToDelete.Add(e);
                }
                foreach (var eftItem in eftToDelete) tran.Delete(eftItem);

                // 5. İlişkili Çek Kaydını Sil (Eğer evrak bir çek ise)
                cekToDelete = new List<Cek>();
                if (!string.IsNullOrEmpty(evrakNo))
                {
                    var cMatches = tran.Query<Cek>("SELECT * FROM Cek WHERE PortfoyNo = ? OR CekNo = ?", evrakNo, evrakNo);
                    foreach (var c in cMatches) if (!cekToDelete.Any(x => x.Id == c.Id)) cekToDelete.Add(c);
                }
                foreach (var cItem in cekToDelete) tran.Delete(cItem);

                success = true;
            });

            if (success)
            {
                try
                {
                    await _dbService.EnsureInitializedAsync();

                    // Etkilenen carilerin bakiyelerini yeniden hesapla ve buluta senkronize et
                    foreach (var cId in affectedCariIds.Distinct())
                    {
                        await _uow.Cariler.RecalculateBalanceAsync(cId);
                        var updatedCari = await _uow.Cariler.GetByIdAsync(cId);
                        if (updatedCari != null)
                        {
                            await _dbService.SaveCariAsync(updatedCari);
                            await _syncService.SyncCariAsync(updatedCari);
                        }
                    }

                    // Etkilenen kasa ve bankaları buluta senkronize et
                    foreach (var kbId in affectedKasaBankaIds.Distinct())
                    {
                        var kb = await _uow.Bankalar.GetByIdAsync(kbId);
                        if (kb != null)
                        {
                            await _syncService.SyncBankaAsync(kb);
                        }
                    }

                    // --- CANLI BULUT SENKRONİZASYONU: SİLİNEN TÜM HAREKETLERİ FIREBASE'DEN DE SİL ---
                    foreach (var ch in chToDelete)
                    {
                        await _syncService.DeleteCariHareketAsync(ch.Id);
                    }
                    if (hareketId > 0 && !chToDelete.Any(x => x.Id == hareketId))
                    {
                        await _syncService.DeleteCariHareketAsync(hareketId);
                    }

                    foreach (var kh in khToDelete)
                    {
                        await _syncService.DeleteKasaHareketAsync(kh.Id);
                    }

                    foreach (var bh in bhToDelete)
                    {
                        await _syncService.DeleteBankaHareketAsync(bh.Id);
                    }

                    foreach (var kkItem in kkToDelete)
                    {
                        await _syncService.DeleteKrediKartiIslemAsync(kkItem.Id);
                    }

                    foreach (var eftItem in eftToDelete)
                    {
                        await _syncService.DeleteEftIslemAsync(eftItem.Id);
                    }

                    foreach (var cItem in cekToDelete)
                    {
                        await _syncService.DeleteCekAsync(cItem.Id);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FinansService] Cloud delete sync error: {ex.Message}");
                }
            }

            return success;
        }

        private static string GetEvrakNoPrefix(string method) => method switch
        {
            "Kredi Kartı" => "KK-",
            "Havale / EFT" => "EFT-",
            "Havale/EFT" => "EFT-",
            "Çek" => "CK-",
            _ => "TS-"
        };

        private static string GetMethodAbbreviation(string method) => method switch
        {
            "Kredi Kartı" => "KK",
            "Havale / EFT" => "EFT",
            "Havale/EFT" => "EFT",
            "EFT" => "EFT",
            "Çek" => "Çek",
            "Nakit" => "Nakit",
            _ => method
        };
    }
}
