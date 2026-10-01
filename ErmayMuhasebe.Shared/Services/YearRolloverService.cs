using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SQLite;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Data;

namespace ErmayMuhasebe.Services
{
    public class RolloverOptions
    {
        public bool TransferCariler { get; set; } = true;
        public bool TransferStoklar { get; set; } = true;
        public bool TransferKasaBanka { get; set; } = true;
        public bool TransferCekSenet { get; set; } = true;
        public bool TransferTanimlar { get; set; } = true;
        public bool ExcludeZeroBalances { get; set; } = false;
    }

    public class RolloverResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int CarilerCount { get; set; }
        public int StoklarCount { get; set; }
        public int KasalarCount { get; set; }
        public int BankalarCount { get; set; }
        public int CeklerCount { get; set; }
        public int SenetlerCount { get; set; }
    }

    public class YearRolloverService
    {
        private readonly DatabaseService _dbService;
        private readonly IYearContext _yearContext;

        public YearRolloverService(DatabaseService dbService, IYearContext yearContext)
        {
            _dbService = dbService;
            _yearContext = yearContext;
        }

        public string GetAppDirectory()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        public string GetDbPathForYear(int year, string? tenantId = null)
        {
            var dir = GetAppDirectory();
            var tenant = tenantId ?? _dbService.CurrentTenantId ?? "default";
            if (string.IsNullOrEmpty(tenant) || tenant == "default")
            {
                return Path.Combine(dir, $"ermay_{year}.db");
            }
            return Path.Combine(dir, $"ermay_{year}_{tenant}.db");
        }

        public List<int> GetAvailableYears()
        {
            var years = new HashSet<int>();
            var dir = GetAppDirectory();
            var tenant = _dbService.CurrentTenantId ?? "default";

            if (Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir, "ermay_*.db");
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        // 0 bayt veya bozuk/oluşamamış dosyaları temizle ve listeye ekleme
                        if (fileInfo.Length <= 0)
                        {
                            try { File.Delete(file); } catch { }
                            continue;
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    var fileName = Path.GetFileNameWithoutExtension(file);

                    // Filter by tenant
                    if (tenant == "default")
                    {
                        if (fileName.Contains("tenant_")) continue;
                    }
                    else
                    {
                        if (!fileName.Contains($"_{tenant}")) continue;
                    }

                    var parts = fileName.Split('_');
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int y))
                    {
                        years.Add(y);
                    }
                }
            }

            return years.OrderByDescending(y => y).ToList();
        }

        public async Task<SQLiteAsyncConnection> OpenConnectionAsync(string dbPath)
        {
            var pwd = _dbService.UseEncryption ? (_dbService.CustomPassword ?? Constants.DatabasePassword) : null;
            var options = new SQLiteConnectionString(dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex, !string.IsNullOrEmpty(pwd), key: pwd);
            var conn = new SQLiteAsyncConnection(options);
            if (_dbService.UseEncryption)
            {
                try { await conn.ExecuteAsync("PRAGMA cipher_memory_security = OFF;"); } catch { }
                try { await conn.ExecuteAsync("PRAGMA busy_timeout = 30000;"); } catch { }
            }
            return conn;
        }

        public async Task InitializeTargetDatabaseSchemaAsync(SQLiteAsyncConnection db)
        {
            if (_dbService.UseEncryption)
            {
                try { await db.ExecuteAsync("PRAGMA cipher_memory_security = OFF;"); } catch { }
                try { await db.ExecuteAsync("PRAGMA busy_timeout = 30000;"); } catch { }
            }

            var syncDb = db.GetConnection();
            syncDb.BusyTimeout = TimeSpan.FromSeconds(60);

            try
            {
                syncDb.Execute("PRAGMA journal_mode=WAL;");
                syncDb.Execute("PRAGMA synchronous=NORMAL;");
            }
            catch { }

            syncDb.CreateTable<CariKart>();
            syncDb.CreateTable<StokKart>();
            syncDb.CreateTable<Fatura>();
            syncDb.CreateTable<FaturaDetay>();
            syncDb.CreateTable<BankaKart>();

            Type[] tables = new[] {
                typeof(CariHareket), typeof(StokHareket), typeof(KasaHareket), typeof(BankaHareket),
                typeof(Cek), typeof(Senet), typeof(Siparis), typeof(SiparisDetay), typeof(Teklif), typeof(TeklifDetay),
                typeof(Personel), typeof(Gorev), typeof(DovizKur),
                typeof(AcilisKapanisFisi), typeof(AcilisKapanisFisiDetay), typeof(MaliyetMerkeziDef),
                typeof(PortfoyKart), typeof(SatisHedefi), typeof(SmsGecmisi), typeof(SilinenKayit),
                typeof(KasaSayimFisi), typeof(StokSayimFisi), typeof(StokSayimDetay), typeof(TurkiyeSehirler),
                typeof(KrediKartiIslem), typeof(EftIslem), typeof(HaftalikSatisHedefi), typeof(YillikSatisHedefi),
                typeof(FirmaProfili), typeof(FaturaTasarimi), typeof(BelgeArsiv), typeof(Note), typeof(CariDosya), typeof(SyncQueueItem),
                typeof(RecycleBinRecord), typeof(CronJobRecord), typeof(StokGrupDef),
                typeof(MusteriTakipKlasor), typeof(MusteriTakipDetay)
            };

            foreach (var table in tables)
            {
                try { syncDb.CreateTable(table); } catch { }
            }

            try
            {
                syncDb.Execute("CREATE INDEX IF NOT EXISTS IX_CariHareket_Cari_Tarih ON CariHareket (CariId, Tarih);");
                syncDb.Execute("CREATE INDEX IF NOT EXISTS IX_StokHareket_Stok_Tarih ON StokHareket (StokId, Tarih);");
                syncDb.Execute("CREATE INDEX IF NOT EXISTS IX_Fatura_Cari_Tarih ON Fatura (CariId, Tarih);");
                syncDb.Execute("CREATE INDEX IF NOT EXISTS IX_KasaHareket_Kasa_Tarih ON KasaHareket (KasaId, Tarih);");
                syncDb.Execute("CREATE INDEX IF NOT EXISTS IX_BankaHareket_Banka_Tarih ON BankaHareket (BankaId, Tarih);");
            }
            catch { }
        }

        /// <summary>
        /// Kaynak yıldan hedef yıla tam yıl sonu devri gerçekleştirir.
        /// </summary>
        public async Task<RolloverResult> RolloverYearAsync(int sourceYear, int targetYear, RolloverOptions? options = null, IProgress<string>? progress = null)
        {
            options ??= new RolloverOptions();
            var result = new RolloverResult();

            if (sourceYear == targetYear)
            {
                result.Success = false;
                result.Message = "Kaynak yıl ile hedef yıl aynı olamaz.";
                return result;
            }

            var sourceDbPath = GetDbPathForYear(sourceYear);
            if (!File.Exists(sourceDbPath))
            {
                result.Success = false;
                result.Message = $"{sourceYear} yılına ait kaynak veritabanı bulunamadı.";
                return result;
            }

            var targetDbPath = GetDbPathForYear(targetYear);
            progress?.Report($"{targetYear} yılı veritabanı hazırlanıyor...");

            SQLiteAsyncConnection? sourceDb = null;
            SQLiteAsyncConnection? targetDb = null;
            bool shouldCloseSourceDb = true;

            try
            {
                if (sourceYear == _yearContext.CurrentYear)
                {
                    sourceDb = await _dbService.GetConnectionAsync();
                    shouldCloseSourceDb = false;
                }
                else
                {
                    sourceDb = await OpenConnectionAsync(sourceDbPath);
                    shouldCloseSourceDb = true;
                }
                targetDb = await OpenConnectionAsync(targetDbPath);

                await InitializeTargetDatabaseSchemaAsync(targetDb);

                var openingDate = new DateTime(targetYear, 1, 1, 0, 0, 0);

                // 1. TANIMLAR & PROFİLLER
                if (options.TransferTanimlar)
                {
                    progress?.Report("Sabit tanımlar ve firma profili aktarılıyor...");
                    try
                    {
                        var profiller = await sourceDb.Table<FirmaProfili>().ToListAsync();
                        foreach (var p in profiller) await targetDb.InsertOrReplaceAsync(p);

                        var tasarimlar = await sourceDb.Table<FaturaTasarimi>().ToListAsync();
                        foreach (var t in tasarimlar) await targetDb.InsertOrReplaceAsync(t);

                        var personeller = await sourceDb.Table<Personel>().ToListAsync();
                        foreach (var p in personeller) await targetDb.InsertOrReplaceAsync(p);

                        var stokGruplari = await sourceDb.Table<StokGrupDef>().ToListAsync();
                        foreach (var g in stokGruplari) await targetDb.InsertOrReplaceAsync(g);

                        var maliyetMerkezleri = await sourceDb.Table<MaliyetMerkeziDef>().ToListAsync();
                        foreach (var m in maliyetMerkezleri) await targetDb.InsertOrReplaceAsync(m);
                    }
                    catch { }
                }

                // 2. CARİ DEVİR
                if (options.TransferCariler)
                {
                    progress?.Report("Cari kartlar ve kapanış bakiyeleri hesaplanıyor...");
                    var sourceCariler = await sourceDb.Table<CariKart>().Where(c => !c.IsDeleted).ToListAsync();
                    var sourceCariHarekets = await sourceDb.Table<CariHareket>().ToListAsync();

                    var devirDetaylar = new List<AcilisKapanisFisiDetay>();

                    foreach (var c in sourceCariler)
                    {
                        var moves = sourceCariHarekets.Where(h => h.CariId == c.Id).ToList();
                        decimal borcSum = moves.Sum(m => m.Borc);
                        decimal alacakSum = moves.Sum(m => m.Alacak);

                        // Net Bakiye = (Borc + DevirBorc) - (Alacak + DevirAlacak)
                        decimal netBakiye = (borcSum + c.DevirBorc) - (alacakSum + c.DevirAlacak);

                        if (options.ExcludeZeroBalances && netBakiye == 0)
                            continue;

                        decimal newDevirBorc = netBakiye > 0 ? netBakiye : 0;
                        decimal newDevirAlacak = netBakiye < 0 ? Math.Abs(netBakiye) : 0;

                        var newCari = new CariKart
                        {
                            Id = c.Id,
                            TenantId = c.TenantId,
                            CariKod = c.CariKod,
                            Unvan = c.Unvan,
                            Tur = c.Tur,
                            Grup = c.Grup,
                            VergiDairesi = c.VergiDairesi,
                            VergiNo = c.VergiNo,
                            TicaretSicilNo = c.TicaretSicilNo,
                            Yetkili = c.Yetkili,
                            Telefon = c.Telefon,
                            CepTelefon = c.CepTelefon,
                            Email = c.Email,
                            WebAdresi = c.WebAdresi,
                            Adres = c.Adres,
                            SevkAdresi = c.SevkAdresi,
                            Il = c.Il,
                            Ilce = c.Ilce,
                            PostaKodu = c.PostaKodu,
                            Ulke = c.Ulke,
                            RiskLimiti = c.RiskLimiti,
                            VadeGunu = c.VadeGunu,
                            IBAN = c.IBAN,
                            OdemePlani = c.OdemePlani,
                            TCNo = c.TCNo,
                            Latitude = c.Latitude,
                            Longitude = c.Longitude,
                            Aciklama = c.Aciklama,
                            RiskTakibiYapilsin = c.RiskTakibiYapilsin,
                            VadeGecmisteEngelle = c.VadeGecmisteEngelle,
                            FaturadaRiskKontrolu = c.FaturadaRiskKontrolu,
                            AktifMi = c.AktifMi,
                            KayitTarihi = c.KayitTarihi,
                            IsDeleted = false,
                            DevirBorc = newDevirBorc,
                            DevirAlacak = newDevirAlacak,
                            Borc = 0,
                            Alacak = 0
                        };

                        await targetDb.InsertOrReplaceAsync(newCari);
                        result.CarilerCount++;

                        // Devir Hareketi Kaydı (Ekstrede görünmesi için)
                        if (netBakiye != 0)
                        {
                            string devirEvrakNo = $"DEVIR-{sourceYear}";
                            var existingMoves = await targetDb.QueryAsync<CariHareket>(
                                "SELECT * FROM CariHareket WHERE CariId = ? AND (EvrakNo = ? OR IslemTuru = 'Devir Fişi')", 
                                c.Id, devirEvrakNo);

                            if (existingMoves != null && existingMoves.Any())
                            {
                                var first = existingMoves.First();
                                first.Tarih = openingDate;
                                first.IslemTuru = "Devir Fişi";
                                first.EvrakNo = devirEvrakNo;
                                first.Aciklama = $"{sourceYear} Yılı Kapanış Bakiye Devri";
                                first.Borc = newDevirBorc;
                                first.Alacak = newDevirAlacak;
                                first.KalanBakiye = netBakiye;
                                await targetDb.UpdateAsync(first);

                                for (int i = 1; i < existingMoves.Count; i++)
                                {
                                    await targetDb.DeleteAsync(existingMoves[i]);
                                }
                            }
                            else
                            {
                                await targetDb.InsertAsync(new CariHareket
                                {
                                    CariId = c.Id,
                                    CariUnvan = c.Unvan,
                                    Tarih = openingDate,
                                    IslemTuru = "Devir Fişi",
                                    EvrakNo = devirEvrakNo,
                                    Aciklama = $"{sourceYear} Yılı Kapanış Bakiye Devri",
                                    Borc = newDevirBorc,
                                    Alacak = newDevirAlacak,
                                    KalanBakiye = netBakiye
                                });
                            }

                            devirDetaylar.Add(new AcilisKapanisFisiDetay
                            {
                                HesapId = c.Id,
                                HesapKodu = c.CariKod,
                                HesapAdi = c.Unvan,
                                HesapTuru = "Cari",
                                Aciklama = $"{sourceYear} Yılı Cari Devir",
                                Borc = newDevirBorc,
                                Alacak = newDevirAlacak
                            });
                        }
                    }

                    // Açılış Fişi Genel Kaydı
                    if (devirDetaylar.Any())
                    {
                        var acilisFisi = new AcilisKapanisFisi
                        {
                            FisNo = $"ACILIS-{targetYear}",
                            FisTuru = "Acilis",
                            DonemYil = targetYear.ToString(),
                            Tarih = openingDate,
                            Aciklama = $"{sourceYear} Yılından Devir Açılış Fişi",
                            ToplamBorc = devirDetaylar.Sum(x => x.Borc),
                            ToplamAlacak = devirDetaylar.Sum(x => x.Alacak)
                        };
                        await targetDb.InsertAsync(acilisFisi);
                        foreach (var d in devirDetaylar)
                        {
                            d.FisId = acilisFisi.Id;
                            await targetDb.InsertAsync(d);
                        }
                    }
                }

                // 3. STOK DEVİR
                if (options.TransferStoklar)
                {
                    progress?.Report("Stok kartları ve envanter devri yapılıyor...");
                    var sourceStoklar = await sourceDb.Table<StokKart>().Where(s => !s.IsDeleted).ToListAsync();

                    foreach (var s in sourceStoklar)
                    {
                        if (options.ExcludeZeroBalances && s.Miktar <= 0)
                            continue;

                        var newStok = new StokKart
                        {
                            Id = s.Id,
                            TenantId = s.TenantId,
                            StokKodu = s.StokKodu,
                            StokAdi = s.StokAdi,
                            Barkod = s.Barkod,
                            Birim = s.Birim,
                            Kategori = s.Kategori,
                            AlisFiyati = s.AlisFiyati,
                            OrtalamaAlisFiyati = s.OrtalamaAlisFiyati,
                            SatisFiyati = s.SatisFiyati,
                            OrtalamaSatisFiyati = s.OrtalamaSatisFiyati,
                            KDV = s.KDV,
                            MinSeviye = s.MinSeviye,
                            Aciklama = s.Aciklama,
                            KayitTarihi = s.KayitTarihi,
                            IsDeleted = false,
                            Miktar = s.Miktar
                        };

                        await targetDb.InsertOrReplaceAsync(newStok);
                        result.StoklarCount++;

                        if (s.Miktar > 0)
                        {
                            var existingMove = await targetDb.Table<StokHareket>()
                                .FirstOrDefaultAsync(h => h.StokId == s.Id && h.EvrakNo == $"DEVIR-{sourceYear}");

                            if (existingMove == null)
                            {
                                await targetDb.InsertAsync(new StokHareket
                                {
                                    StokId = s.Id,
                                    StokKodu = s.StokKodu,
                                    StokAdi = s.StokAdi,
                                    Tarih = openingDate,
                                    IslemTuru = "Devir Girişi",
                                    EvrakNo = $"DEVIR-{sourceYear}",
                                    Aciklama = $"{sourceYear} Yılı Stok Devri",
                                    Giren = (decimal)s.Miktar,
                                    Cikan = 0,
                                    Miktar = (decimal)s.Miktar,
                                    KalanMiktar = (decimal)s.Miktar,
                                    Fiyat = s.OrtalamaAlisFiyati > 0 ? s.OrtalamaAlisFiyati : s.AlisFiyati,
                                    Birim = s.Birim
                                });
                            }
                        }
                    }
                }

                // 4. KASA & BANKA DEVİR
                if (options.TransferKasaBanka)
                {
                    progress?.Report("Kasa ve banka açılış bakiyeleri devrediliyor...");
                    List<BankaKart> allBankas;
                    List<KasaHareket> sourceKasaMoves;
                    List<BankaHareket> sourceBankaMoves;

                    if (sourceYear == _yearContext.CurrentYear)
                    {
                        allBankas = await _dbService.GetBankalarAsync();
                        sourceKasaMoves = await _dbService.GetKasaHareketleriAsync();
                        sourceBankaMoves = await _dbService.GetBankaHareketleriAsync_All();
                    }
                    else
                    {
                        allBankas = await sourceDb.Table<BankaKart>().ToListAsync();
                        sourceKasaMoves = await sourceDb.Table<KasaHareket>().ToListAsync();
                        sourceBankaMoves = await sourceDb.Table<BankaHareket>().ToListAsync();
                    }

                    var sourceBankalar = allBankas.Where(b => !b.IsDeleted).ToList();

                    foreach (var b in sourceBankalar)
                    {
                        bool isKasa = b.KartTuru != null && b.KartTuru.Contains("Kasa", StringComparison.OrdinalIgnoreCase);
                        decimal netBakiye = b.AcilisBakiyesi;

                        if (isKasa)
                        {
                            var moves = sourceKasaMoves.Where(h => h.KasaId == b.Id).ToList();
                            netBakiye += moves.Sum(h => h.Giren - h.Cikan);
                        }
                        else
                        {
                            var moves = sourceBankaMoves.Where(h => h.BankaId == b.Id).ToList();
                            netBakiye += moves.Sum(h => h.Giren - h.Cikan);
                        }

                        decimal targetGuncel = netBakiye;
                        if (isKasa)
                        {
                            var targetMoves = await targetDb.Table<KasaHareket>().Where(h => h.KasaId == b.Id).ToListAsync();
                            var nonDevirTargetMoves = targetMoves.Where(h => (h.IslemTuru == null || (h.IslemTuru != "Devir Fişi" && h.IslemTuru != "Açılış Fişi" && !h.IslemTuru.StartsWith("Devir"))) && 
                                                                            (h.EvrakNo == null || !h.EvrakNo.StartsWith("DEVIR-"))).ToList();
                            targetGuncel = netBakiye + nonDevirTargetMoves.Sum(h => h.Giren - h.Cikan);
                        }
                        else
                        {
                            var targetMoves = await targetDb.Table<BankaHareket>().Where(h => h.BankaId == b.Id).ToListAsync();
                            var nonDevirTargetMoves = targetMoves.Where(h => (h.IslemTuru == null || (h.IslemTuru != "Devir Fişi" && h.IslemTuru != "Açılış Fişi" && !h.IslemTuru.StartsWith("Devir"))) && 
                                                                            (h.EvrakNo == null || !h.EvrakNo.StartsWith("DEVIR-"))).ToList();
                            targetGuncel = netBakiye + nonDevirTargetMoves.Sum(h => h.Giren - h.Cikan);
                        }

                        var newBanka = new BankaKart
                        {
                            Id = b.Id,
                            TenantId = b.TenantId,
                            BankaAdi = b.BankaAdi,
                            SubeAdi = b.SubeAdi,
                            SubeKodu = b.SubeKodu,
                            HesapNo = b.HesapNo,
                            IBAN = b.IBAN,
                            DovizTuru = b.DovizTuru,
                            Yetkili = b.Yetkili,
                            Telefon = b.Telefon,
                            KartTuru = b.KartTuru ?? (isKasa ? "Kasa" : "Banka"),
                            IsDeleted = false,
                            AcilisBakiyesi = netBakiye,
                            GuncelBakiye = targetGuncel
                        };

                        await targetDb.InsertOrReplaceAsync(newBanka);

                        if (isKasa)
                        {
                            result.KasalarCount++;
                            string devirEvrakNo = $"DEVIR-KASA-{sourceYear}";
                            var existingMove = await targetDb.Table<KasaHareket>()
                                .FirstOrDefaultAsync(h => h.KasaId == b.Id && h.EvrakNo == devirEvrakNo);

                            if (existingMove != null)
                            {
                                existingMove.Tarih = openingDate;
                                existingMove.IslemTuru = "Devir Fişi";
                                existingMove.Aciklama = $"{sourceYear} Yılı Kasa Açılış Bakiyesi";
                                existingMove.Giren = netBakiye > 0 ? netBakiye : 0;
                                existingMove.Cikan = netBakiye < 0 ? Math.Abs(netBakiye) : 0;
                                existingMove.Tutar = Math.Abs(netBakiye);
                                await targetDb.UpdateAsync(existingMove);
                            }
                            else if (netBakiye != 0)
                            {
                                await targetDb.InsertAsync(new KasaHareket
                                {
                                    KasaId = b.Id,
                                    Tarih = openingDate,
                                    IslemTuru = "Devir Fişi",
                                    EvrakNo = devirEvrakNo,
                                    Aciklama = $"{sourceYear} Yılı Kasa Açılış Bakiyesi",
                                    Giren = netBakiye > 0 ? netBakiye : 0,
                                    Cikan = netBakiye < 0 ? Math.Abs(netBakiye) : 0,
                                    Tutar = Math.Abs(netBakiye)
                                });
                            }
                        }
                        else
                        {
                            result.BankalarCount++;
                            string devirEvrakNo = $"DEVIR-BNK-{sourceYear}";
                            var existingMove = await targetDb.Table<BankaHareket>()
                                .FirstOrDefaultAsync(h => h.BankaId == b.Id && h.EvrakNo == devirEvrakNo);

                            if (existingMove != null)
                            {
                                existingMove.Tarih = openingDate;
                                existingMove.IslemTuru = "Devir Fişi";
                                existingMove.Aciklama = $"{sourceYear} Yılı Banka Açılış Bakiyesi";
                                existingMove.Giren = netBakiye > 0 ? netBakiye : 0;
                                existingMove.Cikan = netBakiye < 0 ? Math.Abs(netBakiye) : 0;
                                existingMove.Tutar = Math.Abs(netBakiye);
                                await targetDb.UpdateAsync(existingMove);
                            }
                            else if (netBakiye != 0)
                            {
                                await targetDb.InsertAsync(new BankaHareket
                                {
                                    BankaId = b.Id,
                                    Tarih = openingDate,
                                    IslemTuru = "Devir Fişi",
                                    EvrakNo = devirEvrakNo,
                                    Aciklama = $"{sourceYear} Yılı Banka Açılış Bakiyesi",
                                    Giren = netBakiye > 0 ? netBakiye : 0,
                                    Cikan = netBakiye < 0 ? Math.Abs(netBakiye) : 0,
                                    Tutar = Math.Abs(netBakiye)
                                });
                            }
                        }
                    }
                }

                // 5. AÇIK ÇEK & SENET DEVİR
                if (options.TransferCekSenet)
                {
                    progress?.Report("Portföydeki açık çek ve senetler devrediliyor...");
                    var sourceCekler = await sourceDb.Table<Cek>().ToListAsync();
                    var openCekler = sourceCekler.Where(c => c.Durum != "Tahsil Edildi" && c.Durum != "Ödendi" && c.Durum != "İptal").ToList();

                    foreach (var c in openCekler)
                    {
                        await targetDb.InsertOrReplaceAsync(c);
                        result.CeklerCount++;
                    }

                    var sourceSenetler = await sourceDb.Table<Senet>().ToListAsync();
                    var openSenetler = sourceSenetler.Where(s => s.Durum != "Tahsil Edildi" && s.Durum != "Ödendi" && s.Durum != "İptal").ToList();

                    foreach (var s in openSenetler)
                    {
                        await targetDb.InsertOrReplaceAsync(s);
                        result.SenetlerCount++;
                    }
                }

                result.Success = true;
                result.Message = $"{sourceYear} yılından {targetYear} yılına devir başarıyla tamamlandı. ({result.CarilerCount} Cari, {result.StoklarCount} Stok, {result.KasalarCount} Kasa, {result.BankalarCount} Banka)";
                progress?.Report(result.Message);
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Devir sırasında hata oluştu: {ex.Message}";
                return result;
            }
            finally
            {
                if (sourceDb != null && shouldCloseSourceDb) { try { await sourceDb.CloseAsync(); } catch { } }
                if (targetDb != null) { try { await targetDb.CloseAsync(); } catch { } }
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        /// <summary>
        /// Geçmiş bir yılda (örn: 2026) yapılan düzenlemeyi, var olan sonraki yıla (örn: 2027) otomatik olarak yansıtır.
        /// Carinin güncel 2026 bakiyesini 2027'deki DevirBorc / DevirAlacak alanlarına günceller ve mükerrer devir hareketlerini temizler.
        /// </summary>
        public async Task<bool> ReflectSingleCariForwardAsync(int sourceYear, int cariId)
        {
            SQLiteAsyncConnection? sourceDb = null;
            SQLiteAsyncConnection? targetDb = null;
            try
            {
                int nextYear = sourceYear + 1;
                var nextYearDbPath = GetDbPathForYear(nextYear);

                if (!File.Exists(nextYearDbPath))
                    return false; // Sonraki yıl veritabanı henüz oluşturulmamış

                CariKart? cari = null;
                decimal borcSum = 0;
                decimal alacakSum = 0;
                decimal devirBorc = 0;
                decimal devirAlacak = 0;

                // EĞER sourceYear aktif çalışma yılı ise, _dbService bağlantısını doğrudan kullan (çift SQLCipher bağlantı çökmesini önle)
                if (sourceYear == _yearContext.CurrentYear)
                {
                    cari = await _dbService.GetCariByIdAsync(cariId);
                    if (cari == null) return false;

                    var moves = await _dbService.GetCariHareketlerAsync(cariId);
                    borcSum = moves.Sum(m => m.Borc);
                    alacakSum = moves.Sum(m => m.Alacak);
                    devirBorc = cari.DevirBorc;
                    devirAlacak = cari.DevirAlacak;
                }
                else
                {
                    var sourceDbPath = GetDbPathForYear(sourceYear);
                    if (!File.Exists(sourceDbPath))
                        return false;

                    sourceDb = await OpenConnectionAsync(sourceDbPath);
                    cari = await sourceDb.Table<CariKart>().FirstOrDefaultAsync(c => c.Id == cariId);
                    if (cari == null) return false;

                    var moves = await sourceDb.Table<CariHareket>().Where(h => h.CariId == cariId).ToListAsync();
                    borcSum = moves.Sum(m => m.Borc);
                    alacakSum = moves.Sum(m => m.Alacak);
                    devirBorc = cari.DevirBorc;
                    devirAlacak = cari.DevirAlacak;
                }

                decimal netBakiye = (borcSum + devirBorc) - (alacakSum + devirAlacak);
                decimal nextDevirBorc = netBakiye > 0 ? netBakiye : 0;
                decimal nextDevirAlacak = netBakiye < 0 ? Math.Abs(netBakiye) : 0;

                targetDb = await OpenConnectionAsync(nextYearDbPath);
                string cariKod = cari.CariKod;
                var targetCari = await targetDb.Table<CariKart>().FirstOrDefaultAsync(c => c.Id == cariId || c.CariKod == cariKod);

                if (targetCari == null)
                {
                    // Kaynak yılda yeni açılmış olan cari sonraki yılda henüz yoksa ekle
                    targetCari = new CariKart
                    {
                        Id = cari.Id,
                        TenantId = cari.TenantId,
                        CariKod = cari.CariKod,
                        Unvan = cari.Unvan,
                        Tur = cari.Tur,
                        Grup = cari.Grup,
                        VergiDairesi = cari.VergiDairesi,
                        VergiNo = cari.VergiNo,
                        TicaretSicilNo = cari.TicaretSicilNo,
                        Yetkili = cari.Yetkili,
                        Telefon = cari.Telefon,
                        CepTelefon = cari.CepTelefon,
                        Email = cari.Email,
                        WebAdresi = cari.WebAdresi,
                        Adres = cari.Adres,
                        SevkAdresi = cari.SevkAdresi,
                        Il = cari.Il,
                        Ilce = cari.Ilce,
                        PostaKodu = cari.PostaKodu,
                        Ulke = cari.Ulke,
                        RiskLimiti = cari.RiskLimiti,
                        VadeGunu = cari.VadeGunu,
                        IBAN = cari.IBAN,
                        OdemePlani = cari.OdemePlani,
                        TCNo = cari.TCNo,
                        Latitude = cari.Latitude,
                        Longitude = cari.Longitude,
                        Aciklama = cari.Aciklama,
                        RiskTakibiYapilsin = cari.RiskTakibiYapilsin,
                        VadeGecmisteEngelle = cari.VadeGecmisteEngelle,
                        FaturadaRiskKontrolu = cari.FaturadaRiskKontrolu,
                        AktifMi = cari.AktifMi,
                        KayitTarihi = cari.KayitTarihi,
                        IsDeleted = false,
                        DevirBorc = nextDevirBorc,
                        DevirAlacak = nextDevirAlacak,
                        Borc = 0,
                        Alacak = 0
                    };
                    await targetDb.InsertOrReplaceAsync(targetCari);
                }
                else
                {
                    targetCari.Unvan = cari.Unvan;
                    targetCari.Tur = cari.Tur;
                    targetCari.Grup = cari.Grup;
                    targetCari.VergiDairesi = cari.VergiDairesi;
                    targetCari.VergiNo = cari.VergiNo;
                    targetCari.TicaretSicilNo = cari.TicaretSicilNo;
                    targetCari.Yetkili = cari.Yetkili;
                    targetCari.Telefon = cari.Telefon;
                    targetCari.CepTelefon = cari.CepTelefon;
                    targetCari.Email = cari.Email;
                    targetCari.WebAdresi = cari.WebAdresi;
                    targetCari.Adres = cari.Adres;
                    targetCari.SevkAdresi = cari.SevkAdresi;
                    targetCari.Il = cari.Il;
                    targetCari.Ilce = cari.Ilce;
                    targetCari.PostaKodu = cari.PostaKodu;
                    targetCari.Ulke = cari.Ulke;
                    targetCari.RiskLimiti = cari.RiskLimiti;
                    targetCari.VadeGunu = cari.VadeGunu;
                    targetCari.IBAN = cari.IBAN;
                    targetCari.OdemePlani = cari.OdemePlani;
                    targetCari.TCNo = cari.TCNo;
                    targetCari.Latitude = cari.Latitude;
                    targetCari.Longitude = cari.Longitude;
                    targetCari.Aciklama = cari.Aciklama;
                    targetCari.RiskTakibiYapilsin = cari.RiskTakibiYapilsin;
                    targetCari.VadeGecmisteEngelle = cari.VadeGecmisteEngelle;
                    targetCari.FaturadaRiskKontrolu = cari.FaturadaRiskKontrolu;
                    targetCari.AktifMi = cari.AktifMi;
                }
                // Hedef yıldaki dönem içi hareketlerin toplamını hesapla (Devir Fişi hariç)
                var targetMoves = await targetDb.Table<CariHareket>().Where(h => h.CariId == targetCari.Id).ToListAsync();
                var nonDevirTargetMoves = targetMoves.Where(h => (h.IslemTuru == null || (h.IslemTuru != "Devir Fişi" && h.IslemTuru != "Açılış Fişi" && !h.IslemTuru.StartsWith("Devir"))) && 
                                                                 (h.EvrakNo == null || !h.EvrakNo.StartsWith("DEVIR-"))).ToList();
                targetCari.DevirBorc = nextDevirBorc;
                targetCari.DevirAlacak = nextDevirAlacak;
                targetCari.Borc = nonDevirTargetMoves.Sum(m => m.Borc);
                targetCari.Alacak = nonDevirTargetMoves.Sum(m => m.Alacak);
                await targetDb.UpdateAsync(targetCari);

                // Devir hareketini güncelle veya ekle (mükerrer kayıtları temizle)
                string evrakNo = $"DEVIR-{sourceYear}";
                var existingMoves = await targetDb.QueryAsync<CariHareket>(
                    "SELECT * FROM CariHareket WHERE CariId = ? AND (EvrakNo = ? OR IslemTuru = 'Devir Fişi')", 
                    targetCari.Id, evrakNo);

                if (existingMoves != null && existingMoves.Any())
                {
                    var first = existingMoves.First();
                    first.Tarih = new DateTime(nextYear, 1, 1);
                    first.IslemTuru = "Devir Fişi";
                    first.EvrakNo = evrakNo;
                    first.Aciklama = $"{sourceYear} Yılı Güncellenen Bakiye Devri";
                    first.Borc = nextDevirBorc;
                    first.Alacak = nextDevirAlacak;
                    first.KalanBakiye = netBakiye;
                    await targetDb.UpdateAsync(first);

                    // Mükerrer olan diğer tüm devir fişlerini sil
                    for (int i = 1; i < existingMoves.Count; i++)
                    {
                        await targetDb.DeleteAsync(existingMoves[i]);
                    }
                }
                else if (netBakiye != 0)
                {
                    await targetDb.InsertAsync(new CariHareket
                    {
                        CariId = targetCari.Id,
                        CariUnvan = targetCari.Unvan,
                        Tarih = new DateTime(nextYear, 1, 1),
                        IslemTuru = "Devir Fişi",
                        EvrakNo = evrakNo,
                        Aciklama = $"{sourceYear} Yılı Güncellenen Bakiye Devri",
                        Borc = nextDevirBorc,
                        Alacak = nextDevirAlacak,
                        KalanBakiye = netBakiye
                    });
                }

                // Zincirleme kontrol: Sonraki yıllar da varsa (örn: 2028)
                _ = ReflectSingleCariForwardAsync(nextYear, targetCari.Id);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YearRolloverService] ReflectSingleCariForwardAsync Hata: {ex.Message}");
                return false;
            }
            finally
            {
                if (sourceDb != null) { try { await sourceDb.CloseAsync(); } catch { } }
                if (targetDb != null) { try { await targetDb.CloseAsync(); } catch { } }
            }
        }

        /// <summary>
        /// Geçmiş bir yılda (örn: 2026) stokta yapılan bir hareketi, sonraki yıldaki (örn: 2027) devir fişine otomatik yansıtır.
        /// </summary>
        public async Task<bool> ReflectSingleStokForwardAsync(int sourceYear, int stokId)
        {
            SQLiteAsyncConnection? sourceDb = null;
            SQLiteAsyncConnection? targetDb = null;
            try
            {
                int nextYear = sourceYear + 1;
                var nextYearDbPath = GetDbPathForYear(nextYear);

                if (!File.Exists(nextYearDbPath))
                    return false;

                StokKart? stok = null;
                if (sourceYear == _yearContext.CurrentYear)
                {
                    stok = await _dbService.GetStokKartAsync(stokId);
                }
                else
                {
                    var sourceDbPath = GetDbPathForYear(sourceYear);
                    if (!File.Exists(sourceDbPath))
                        return false;

                    sourceDb = await OpenConnectionAsync(sourceDbPath);
                    stok = await sourceDb.Table<StokKart>().FirstOrDefaultAsync(s => s.Id == stokId);
                }

                if (stok == null) return false;

                decimal netMiktar = (decimal)stok.Miktar;

                targetDb = await OpenConnectionAsync(nextYearDbPath);
                string stokKodu = stok.StokKodu;
                var targetStok = await targetDb.Table<StokKart>().FirstOrDefaultAsync(s => s.Id == stokId || s.StokKodu == stokKodu);

                if (targetStok == null)
                {
                    targetStok = new StokKart
                    {
                        Id = stok.Id,
                        TenantId = stok.TenantId,
                        StokKodu = stok.StokKodu,
                        StokAdi = stok.StokAdi,
                        Barkod = stok.Barkod,
                        Birim = stok.Birim,
                        Kategori = stok.Kategori,
                        AlisFiyati = stok.AlisFiyati,
                        SatisFiyati = stok.SatisFiyati,
                        OrtalamaAlisFiyati = stok.OrtalamaAlisFiyati,
                        OrtalamaSatisFiyati = stok.OrtalamaSatisFiyati,
                        KdvOrani = stok.KdvOrani,
                        KDV = stok.KDV,
                        Miktar = (double)netMiktar,
                        IsDeleted = false
                    };
                    await targetDb.InsertOrReplaceAsync(targetStok);
                }

                // Hedef yıldaki devir hareketini güncelle (mükerrerleri temizle)
                string evrakNo = $"DEVIR-{sourceYear}";
                var existingMoves = await targetDb.QueryAsync<StokHareket>(
                    "SELECT * FROM StokHareket WHERE StokId = ? AND (EvrakNo = ? OR IslemTuru = 'Devir Fişi' OR IslemTuru = 'Devir Girişi')", 
                    targetStok.Id, evrakNo);

                decimal devirFiyat = stok.OrtalamaAlisFiyati > 0 ? stok.OrtalamaAlisFiyati : stok.AlisFiyati;

                if (existingMoves != null && existingMoves.Any())
                {
                    var first = existingMoves.First();
                    decimal eskiDevir = first.Miktar;
                    decimal fark = netMiktar - eskiDevir;
                    first.Tarih = new DateTime(nextYear, 1, 1);
                    first.IslemTuru = "Devir Fişi";
                    first.EvrakNo = evrakNo;
                    first.Aciklama = $"{sourceYear} Yılı Güncellenen Envanter Devri";
                    first.Miktar = netMiktar;
                    first.Giren = netMiktar;
                    first.Cikan = 0;
                    first.KalanMiktar = netMiktar;
                    first.Fiyat = devirFiyat;
                    await targetDb.UpdateAsync(first);

                    for (int i = 1; i < existingMoves.Count; i++)
                    {
                        await targetDb.DeleteAsync(existingMoves[i]);
                    }

                    targetStok.Miktar += (double)fark;
                    targetStok.AlisFiyati = stok.AlisFiyati;
                    targetStok.SatisFiyati = stok.SatisFiyati;
                    targetStok.OrtalamaAlisFiyati = stok.OrtalamaAlisFiyati;
                    targetStok.OrtalamaSatisFiyati = stok.OrtalamaSatisFiyati;
                    targetStok.KdvOrani = stok.KdvOrani;
                    targetStok.KDV = stok.KDV;
                    await targetDb.UpdateAsync(targetStok);
                }
                else if (netMiktar != 0)
                {
                    await targetDb.InsertAsync(new StokHareket
                    {
                        StokId = targetStok.Id,
                        StokAdi = targetStok.StokAdi,
                        Tarih = new DateTime(nextYear, 1, 1),
                        IslemTuru = "Devir Fişi",
                        EvrakNo = evrakNo,
                        Aciklama = $"{sourceYear} Yılı Güncellenen Envanter Devri",
                        Giren = netMiktar,
                        Cikan = 0,
                        Miktar = netMiktar,
                        KalanMiktar = netMiktar,
                        Fiyat = devirFiyat
                    });
                    targetStok.Miktar = (double)netMiktar;
                    targetStok.AlisFiyati = stok.AlisFiyati;
                    targetStok.SatisFiyati = stok.SatisFiyati;
                    targetStok.OrtalamaAlisFiyati = stok.OrtalamaAlisFiyati;
                    targetStok.OrtalamaSatisFiyati = stok.OrtalamaSatisFiyati;
                    targetStok.KdvOrani = stok.KdvOrani;
                    targetStok.KDV = stok.KDV;
                    await targetDb.UpdateAsync(targetStok);
                }

                // Zincirleme aktarım
                _ = ReflectSingleStokForwardAsync(nextYear, targetStok.Id);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YearRolloverService] ReflectSingleStokForwardAsync Hata: {ex.Message}");
                return false;
            }
            finally
            {
                if (sourceDb != null) { try { await sourceDb.CloseAsync(); } catch { } }
                if (targetDb != null) { try { await targetDb.CloseAsync(); } catch { } }
            }
        }

        /// <summary>
        /// Geçmiş bir yılda (örn: 2026) kasada yapılan bir hareketi veya yeni tanımlanan kasayı, sonraki yıldaki (örn: 2027) devir fişine ve kasa kartına otomatik yansıtır.
        /// </summary>
        public async Task<bool> ReflectSingleKasaForwardAsync(int sourceYear, int kasaId)
        {
            SQLiteAsyncConnection? sourceDb = null;
            SQLiteAsyncConnection? targetDb = null;
            try
            {
                int nextYear = sourceYear + 1;
                var nextYearDbPath = GetDbPathForYear(nextYear);

                if (!File.Exists(nextYearDbPath))
                    return false; // Sonraki yıl veritabanı henüz oluşturulmamış

                BankaKart? kasa = null;
                decimal netBakiye = 0;

                // EĞER sourceYear aktif çalışma yılı ise, _dbService bağlantısını doğrudan kullan (çift SQLCipher bağlantı çökmesini önle)
                if (sourceYear == _yearContext.CurrentYear)
                {
                    kasa = await _dbService.GetBankaAsync(kasaId);
                    if (kasa == null) return false;

                    var moves = await _dbService.GetKasaHareketleriAsync(kasaId);
                    netBakiye = kasa.AcilisBakiyesi + moves.Sum(h => h.Giren - h.Cikan);
                }
                else
                {
                    var sourceDbPath = GetDbPathForYear(sourceYear);
                    if (!File.Exists(sourceDbPath))
                        return false;

                    sourceDb = await OpenConnectionAsync(sourceDbPath);
                    kasa = await sourceDb.Table<BankaKart>().FirstOrDefaultAsync(k => k.Id == kasaId);
                    if (kasa == null) return false;

                    var moves = await sourceDb.Table<KasaHareket>().Where(h => h.KasaId == kasaId).ToListAsync();
                    netBakiye = kasa.AcilisBakiyesi + moves.Sum(h => h.Giren - h.Cikan);
                }

                targetDb = await OpenConnectionAsync(nextYearDbPath);
                string kasaAdi = kasa.BankaAdi ?? "";
                var targetKasa = await targetDb.Table<BankaKart>().FirstOrDefaultAsync(k => k.Id == kasaId || (k.BankaAdi == kasaAdi && k.KartTuru == "Kasa"));

                if (targetKasa == null)
                {
                    targetKasa = new BankaKart
                    {
                        Id = kasa.Id,
                        TenantId = kasa.TenantId,
                        BankaAdi = kasa.BankaAdi,
                        SubeAdi = kasa.SubeAdi,
                        SubeKodu = kasa.SubeKodu,
                        HesapNo = kasa.HesapNo,
                        IBAN = kasa.IBAN,
                        DovizTuru = kasa.DovizTuru,
                        Yetkili = kasa.Yetkili,
                        Telefon = kasa.Telefon,
                        KartTuru = kasa.KartTuru ?? "Kasa",
                        IsDeleted = false,
                        AcilisBakiyesi = netBakiye,
                        GuncelBakiye = netBakiye
                    };
                    await targetDb.InsertOrReplaceAsync(targetKasa);
                }
                else
                {
                    targetKasa.AcilisBakiyesi = netBakiye;
                    var targetMoves = await targetDb.Table<KasaHareket>().Where(h => h.KasaId == targetKasa.Id).ToListAsync();
                    var nonDevirTargetMoves = targetMoves.Where(h => (h.IslemTuru == null || (h.IslemTuru != "Devir Fişi" && h.IslemTuru != "Açılış Fişi" && !h.IslemTuru.StartsWith("Devir"))) && 
                                                                    (h.EvrakNo == null || !h.EvrakNo.StartsWith("DEVIR-"))).ToList();
                    targetKasa.GuncelBakiye = netBakiye + nonDevirTargetMoves.Sum(h => h.Giren - h.Cikan);
                    targetKasa.BankaAdi = kasa.BankaAdi;
                    targetKasa.DovizTuru = kasa.DovizTuru;
                    targetKasa.Yetkili = kasa.Yetkili;
                    await targetDb.UpdateAsync(targetKasa);
                }

                // Devir hareketini güncelle veya ekle
                string evrakNo = $"DEVIR-KASA-{sourceYear}";
                var existingMoves = await targetDb.QueryAsync<KasaHareket>(
                    "SELECT * FROM KasaHareket WHERE KasaId = ? AND (EvrakNo = ? OR IslemTuru = 'Devir Fişi')", 
                    targetKasa.Id, evrakNo);

                if (existingMoves != null && existingMoves.Any())
                {
                    var first = existingMoves.First();
                    first.Tarih = new DateTime(nextYear, 1, 1);
                    first.IslemTuru = "Devir Fişi";
                    first.EvrakNo = evrakNo;
                    first.Aciklama = $"{sourceYear} Yılı Güncellenen Kasa Açılış Bakiyesi";
                    first.Giren = netBakiye > 0 ? netBakiye : 0;
                    first.Cikan = netBakiye < 0 ? Math.Abs(netBakiye) : 0;
                    first.Tutar = Math.Abs(netBakiye);
                    await targetDb.UpdateAsync(first);

                    for (int i = 1; i < existingMoves.Count; i++)
                    {
                        await targetDb.DeleteAsync(existingMoves[i]);
                    }
                }
                else if (netBakiye != 0)
                {
                    await targetDb.InsertAsync(new KasaHareket
                    {
                        KasaId = targetKasa.Id,
                        Tarih = new DateTime(nextYear, 1, 1),
                        IslemTuru = "Devir Fişi",
                        EvrakNo = evrakNo,
                        Aciklama = $"{sourceYear} Yılı Kasa Açılış Bakiyesi",
                        Giren = netBakiye > 0 ? netBakiye : 0,
                        Cikan = netBakiye < 0 ? Math.Abs(netBakiye) : 0,
                        Tutar = Math.Abs(netBakiye)
                    });
                }

                // Zincirleme sonraki yıl kontrolü
                _ = ReflectSingleKasaForwardAsync(nextYear, targetKasa.Id);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YearRolloverService] ReflectSingleKasaForwardAsync Hata: {ex.Message}");
                return false;
            }
            finally
            {
                if (sourceDb != null) { try { await sourceDb.CloseAsync(); } catch { } }
                if (targetDb != null) { try { await targetDb.CloseAsync(); } catch { } }
            }
        }

        /// <summary>
        /// Kaynak yıldaki (örn: 2026) tüm Cariler, Stoklar, Kasa ve Banka bakiyelerini sonraki yıla (örn: 2027) topluca yansıtır / senkronize eder.
        /// </summary>
        public async Task<RolloverResult> ReflectBalancesForwardAsync(int sourceYear, IProgress<string>? progress = null)
        {
            var result = new RolloverResult();
            int nextYear = sourceYear + 1;
            var nextYearDbPath = GetDbPathForYear(nextYear);

            if (!File.Exists(nextYearDbPath))
            {
                result.Success = false;
                result.Message = $"{nextYear} yılı veritabanı bulunamadı. Önce yeni yıl açılmalı veya devir yapılmalıdır.";
                return result;
            }

            progress?.Report($"{sourceYear} yılı kapanış bakiyeleri okunuyor...");

            SQLiteAsyncConnection? sourceDb = null;
            SQLiteAsyncConnection? targetDb = null;

            try
            {
                List<CariKart> sourceCariler;
                List<CariHareket> sourceMoves;
                List<StokKart> sourceStoklar;
                List<BankaKart> sourceBankalar;

                if (sourceYear == _yearContext.CurrentYear)
                {
                    sourceCariler = await _dbService.GetCarilerAsync();
                    sourceMoves = await _dbService.GetCariHareketleriAsync_All();
                    sourceStoklar = await _dbService.GetStoklarAsync();
                    sourceBankalar = await _dbService.GetBankalarAsync();
                }
                else
                {
                    var sourceDbPath = GetDbPathForYear(sourceYear);
                    if (!File.Exists(sourceDbPath))
                    {
                        result.Success = false;
                        result.Message = $"{sourceYear} yılı kaynak veritabanı bulunamadı.";
                        return result;
                    }
                    sourceDb = await OpenConnectionAsync(sourceDbPath);
                    sourceCariler = await sourceDb.Table<CariKart>().Where(c => !c.IsDeleted).ToListAsync();
                    sourceMoves = await sourceDb.Table<CariHareket>().ToListAsync();
                    sourceStoklar = await sourceDb.Table<StokKart>().Where(s => !s.IsDeleted).ToListAsync();
                    sourceBankalar = await sourceDb.Table<BankaKart>().Where(b => !b.IsDeleted).ToListAsync();
                }

                targetDb = await OpenConnectionAsync(nextYearDbPath);

                // 1. CARİ BAKİYELERİNİ YANSIT
                progress?.Report("Cari bakiyeler sonraki yıla yansıtılıyor...");
                string devirEvrakNo = $"DEVIR-{sourceYear}";

                foreach (var c in sourceCariler)
                {
                    var moves = sourceMoves.Where(m => m.CariId == c.Id).ToList();
                    decimal borc = moves.Sum(m => m.Borc);
                    decimal alacak = moves.Sum(m => m.Alacak);
                    decimal netBakiye = (borc + c.DevirBorc) - (alacak + c.DevirAlacak);

                    decimal newDevirBorc = netBakiye > 0 ? netBakiye : 0;
                    decimal newDevirAlacak = netBakiye < 0 ? Math.Abs(netBakiye) : 0;

                    string cariKod = c.CariKod;
                    var targetCari = await targetDb.Table<CariKart>().FirstOrDefaultAsync(tc => tc.Id == c.Id || tc.CariKod == cariKod);
                    if (targetCari != null)
                    {
                        var targetMoves = await targetDb.Table<CariHareket>().Where(h => h.CariId == targetCari.Id).ToListAsync();
                        var nonDevirTargetMoves = targetMoves.Where(h => (h.IslemTuru == null || (h.IslemTuru != "Devir Fişi" && h.IslemTuru != "Açılış Fişi" && !h.IslemTuru.StartsWith("Devir"))) && 
                                                                         (h.EvrakNo == null || !h.EvrakNo.StartsWith("DEVIR-"))).ToList();

                        targetCari.DevirBorc = newDevirBorc;
                        targetCari.DevirAlacak = newDevirAlacak;
                        targetCari.Borc = nonDevirTargetMoves.Sum(m => m.Borc);
                        targetCari.Alacak = nonDevirTargetMoves.Sum(m => m.Alacak);
                        await targetDb.UpdateAsync(targetCari);
                        result.CarilerCount++;

                        // Devir hareketi - mükerrer kayıtları temizle
                        var existingMoves = await targetDb.QueryAsync<CariHareket>(
                            "SELECT * FROM CariHareket WHERE CariId = ? AND (EvrakNo = ? OR IslemTuru = 'Devir Fişi')", 
                            targetCari.Id, devirEvrakNo);

                        if (existingMoves != null && existingMoves.Any())
                        {
                            var first = existingMoves.First();
                            first.Tarih = new DateTime(nextYear, 1, 1);
                            first.IslemTuru = "Devir Fişi";
                            first.EvrakNo = devirEvrakNo;
                            first.Aciklama = $"{sourceYear} Yılı Güncellenen Bakiye Devri";
                            first.Borc = newDevirBorc;
                            first.Alacak = newDevirAlacak;
                            first.KalanBakiye = netBakiye;
                            await targetDb.UpdateAsync(first);

                            for (int i = 1; i < existingMoves.Count; i++)
                            {
                                await targetDb.DeleteAsync(existingMoves[i]);
                            }
                        }
                        else if (netBakiye != 0)
                        {
                            await targetDb.InsertAsync(new CariHareket
                            {
                                CariId = targetCari.Id,
                                CariUnvan = targetCari.Unvan,
                                Tarih = new DateTime(nextYear, 1, 1),
                                IslemTuru = "Devir Fişi",
                                EvrakNo = devirEvrakNo,
                                Aciklama = $"{sourceYear} Yılı Güncellenen Bakiye Devri",
                                Borc = newDevirBorc,
                                Alacak = newDevirAlacak,
                                KalanBakiye = netBakiye
                            });
                        }
                    }
                    else
                    {
                        // 2026'da yeni açılan cari 2027'de henüz yoksa ekle
                        var clone = new CariKart
                        {
                            Id = c.Id,
                            TenantId = c.TenantId,
                            CariKod = c.CariKod,
                            Unvan = c.Unvan,
                            Tur = c.Tur,
                            Grup = c.Grup,
                            VergiDairesi = c.VergiDairesi,
                            VergiNo = c.VergiNo,
                            Telefon = c.Telefon,
                            Email = c.Email,
                            DevirBorc = newDevirBorc,
                            DevirAlacak = newDevirAlacak,
                            Borc = 0,
                            Alacak = 0
                        };
                        await targetDb.InsertOrReplaceAsync(clone);
                        result.CarilerCount++;

                        if (netBakiye != 0)
                        {
                            await targetDb.InsertAsync(new CariHareket
                            {
                                CariId = clone.Id,
                                CariUnvan = clone.Unvan,
                                Tarih = new DateTime(nextYear, 1, 1),
                                IslemTuru = "Devir Fişi",
                                EvrakNo = devirEvrakNo,
                                Aciklama = $"{sourceYear} Yılı Güncellenen Bakiye Devri",
                                Borc = newDevirBorc,
                                Alacak = newDevirAlacak,
                                KalanBakiye = netBakiye
                            });
                        }
                    }
                }

                // 2. STOKLARI YANSIT
                progress?.Report("Stok envanteri sonraki yıla yansıtılıyor...");
                foreach (var s in sourceStoklar)
                {
                    string stokKodu = s.StokKodu;
                    var targetStok = await targetDb.Table<StokKart>().FirstOrDefaultAsync(ts => ts.Id == s.Id || ts.StokKodu == stokKodu);
                    decimal devirFiyat = s.OrtalamaAlisFiyati > 0 ? s.OrtalamaAlisFiyati : s.AlisFiyati;

                    if (targetStok != null)
                    {
                        var existingMoves = await targetDb.QueryAsync<StokHareket>(
                            "SELECT * FROM StokHareket WHERE StokId = ? AND (EvrakNo = ? OR IslemTuru = 'Devir Fişi' OR IslemTuru = 'Devir Girişi')", 
                            targetStok.Id, devirEvrakNo);

                        if (existingMoves != null && existingMoves.Any())
                        {
                            var first = existingMoves.First();
                            decimal eskiDevirMiktar = first.Miktar;
                            decimal yeniDevirMiktar = (decimal)s.Miktar;
                            decimal fark = yeniDevirMiktar - eskiDevirMiktar;

                            first.Tarih = new DateTime(nextYear, 1, 1);
                            first.IslemTuru = "Devir Fişi";
                            first.EvrakNo = devirEvrakNo;
                            first.Aciklama = $"{sourceYear} Yılı Güncellenen Envanter Devri";
                            first.Miktar = yeniDevirMiktar;
                            first.Giren = yeniDevirMiktar;
                            first.Cikan = 0;
                            first.KalanMiktar = yeniDevirMiktar;
                            first.Fiyat = devirFiyat;
                            await targetDb.UpdateAsync(first);

                            for (int i = 1; i < existingMoves.Count; i++)
                            {
                                await targetDb.DeleteAsync(existingMoves[i]);
                            }

                            targetStok.Miktar += (double)fark;
                            targetStok.AlisFiyati = s.AlisFiyati;
                            targetStok.SatisFiyati = s.SatisFiyati;
                            targetStok.OrtalamaAlisFiyati = s.OrtalamaAlisFiyati;
                            targetStok.OrtalamaSatisFiyati = s.OrtalamaSatisFiyati;
                            targetStok.KdvOrani = s.KdvOrani;
                            targetStok.KDV = s.KDV;
                            await targetDb.UpdateAsync(targetStok);
                            result.StoklarCount++;
                        }
                    }
                    else
                    {
                        targetStok = new StokKart
                        {
                            Id = s.Id,
                            TenantId = s.TenantId,
                            StokKodu = s.StokKodu,
                            StokAdi = s.StokAdi,
                            Barkod = s.Barkod,
                            Birim = s.Birim,
                            Kategori = s.Kategori,
                            AlisFiyati = s.AlisFiyati,
                            SatisFiyati = s.SatisFiyati,
                            OrtalamaAlisFiyati = s.OrtalamaAlisFiyati,
                            OrtalamaSatisFiyati = s.OrtalamaSatisFiyati,
                            KdvOrani = s.KdvOrani,
                            KDV = s.KDV,
                            Miktar = s.Miktar,
                            IsDeleted = false
                        };
                        await targetDb.InsertOrReplaceAsync(targetStok);
                        result.StoklarCount++;
                    }
                }

                // 3. KASA & BANKA BAKİYELERİNİ YANSIT
                progress?.Report("Kasa ve banka bakiyeleri sonraki yıla yansıtılıyor...");
                List<KasaHareket> sourceKasaMovesList = sourceYear == _yearContext.CurrentYear 
                    ? await _dbService.GetKasaHareketleriAsync() 
                    : await (sourceDb != null ? sourceDb.Table<KasaHareket>().ToListAsync() : Task.FromResult(new List<KasaHareket>()));
                List<BankaHareket> sourceBankaMovesList = sourceYear == _yearContext.CurrentYear 
                    ? await _dbService.GetBankaHareketleriAsync_All() 
                    : await (sourceDb != null ? sourceDb.Table<BankaHareket>().ToListAsync() : Task.FromResult(new List<BankaHareket>()));

                foreach (var b in sourceBankalar)
                {
                    bool isKasa = b.KartTuru != null && b.KartTuru.Contains("Kasa", StringComparison.OrdinalIgnoreCase);
                    decimal netBakiye = b.AcilisBakiyesi;

                    if (isKasa)
                    {
                        var moves = sourceKasaMovesList.Where(h => h.KasaId == b.Id).ToList();
                        netBakiye += moves.Sum(h => h.Giren - h.Cikan);
                    }
                    else
                    {
                        var moves = sourceBankaMovesList.Where(h => h.BankaId == b.Id).ToList();
                        netBakiye += moves.Sum(h => h.Giren - h.Cikan);
                    }

                    var targetBanka = await targetDb.Table<BankaKart>().FirstOrDefaultAsync(tb => tb.Id == b.Id || (tb.BankaAdi == b.BankaAdi && tb.KartTuru == b.KartTuru));
                    if (targetBanka != null)
                    {
                        targetBanka.AcilisBakiyesi = netBakiye;
                        if (isKasa)
                        {
                            var targetMoves = await targetDb.Table<KasaHareket>().Where(h => h.KasaId == targetBanka.Id).ToListAsync();
                            var nonDevirTargetMoves = targetMoves.Where(h => (h.IslemTuru == null || (h.IslemTuru != "Devir Fişi" && h.IslemTuru != "Açılış Fişi" && !h.IslemTuru.StartsWith("Devir"))) && 
                                                                            (h.EvrakNo == null || !h.EvrakNo.StartsWith("DEVIR-"))).ToList();
                            targetBanka.GuncelBakiye = netBakiye + nonDevirTargetMoves.Sum(h => h.Giren - h.Cikan);
                            targetBanka.BankaAdi = b.BankaAdi;
                            targetBanka.DovizTuru = b.DovizTuru;
                            targetBanka.Yetkili = b.Yetkili;
                        }
                        else
                        {
                            var targetMoves = await targetDb.Table<BankaHareket>().Where(h => h.BankaId == targetBanka.Id).ToListAsync();
                            var nonDevirTargetMoves = targetMoves.Where(h => (h.IslemTuru == null || (h.IslemTuru != "Devir Fişi" && h.IslemTuru != "Açılış Fişi" && !h.IslemTuru.StartsWith("Devir"))) && 
                                                                            (h.EvrakNo == null || !h.EvrakNo.StartsWith("DEVIR-"))).ToList();
                            targetBanka.GuncelBakiye = netBakiye + nonDevirTargetMoves.Sum(h => h.Giren - h.Cikan);
                            targetBanka.BankaAdi = b.BankaAdi;
                            targetBanka.DovizTuru = b.DovizTuru;
                            targetBanka.Yetkili = b.Yetkili;
                        }
                        await targetDb.UpdateAsync(targetBanka);
                        result.BankalarCount++;

                        if (isKasa)
                        {
                            result.KasalarCount++;
                            var existingMove = await targetDb.Table<KasaHareket>()
                                .FirstOrDefaultAsync(h => h.KasaId == targetBanka.Id && h.EvrakNo == $"DEVIR-KASA-{sourceYear}");
                            if (existingMove != null)
                            {
                                existingMove.Giren = netBakiye > 0 ? netBakiye : 0;
                                existingMove.Cikan = netBakiye < 0 ? Math.Abs(netBakiye) : 0;
                                existingMove.Tutar = Math.Abs(netBakiye);
                                await targetDb.UpdateAsync(existingMove);
                            }
                            else if (netBakiye != 0)
                            {
                                await targetDb.InsertAsync(new KasaHareket
                                {
                                    KasaId = targetBanka.Id,
                                    Tarih = new DateTime(nextYear, 1, 1),
                                    IslemTuru = "Devir Fişi",
                                    EvrakNo = $"DEVIR-KASA-{sourceYear}",
                                    Aciklama = $"{sourceYear} Yılı Kasa Açılış Bakiyesi",
                                    Giren = netBakiye > 0 ? netBakiye : 0,
                                    Cikan = netBakiye < 0 ? Math.Abs(netBakiye) : 0,
                                    Tutar = Math.Abs(netBakiye)
                                });
                            }
                        }
                        else
                        {
                            var existingMove = await targetDb.Table<BankaHareket>()
                                .FirstOrDefaultAsync(h => h.BankaId == targetBanka.Id && h.EvrakNo == $"DEVIR-BNK-{sourceYear}");
                            if (existingMove != null)
                            {
                                existingMove.Giren = netBakiye > 0 ? netBakiye : 0;
                                existingMove.Cikan = netBakiye < 0 ? Math.Abs(netBakiye) : 0;
                                existingMove.Tutar = Math.Abs(netBakiye);
                                await targetDb.UpdateAsync(existingMove);
                            }
                            else if (netBakiye != 0)
                            {
                                await targetDb.InsertAsync(new BankaHareket
                                {
                                    BankaId = targetBanka.Id,
                                    Tarih = new DateTime(nextYear, 1, 1),
                                    IslemTuru = "Devir Fişi",
                                    EvrakNo = $"DEVIR-BNK-{sourceYear}",
                                    Aciklama = $"{sourceYear} Yılı Banka Açılış Bakiyesi",
                                    Giren = netBakiye > 0 ? netBakiye : 0,
                                    Cikan = netBakiye < 0 ? Math.Abs(netBakiye) : 0,
                                    Tutar = Math.Abs(netBakiye)
                                });
                            }
                        }
                    }
                    else
                    {
                        // 2026'da yeni açılan kasa veya banka 2027'de henüz yoksa ekle
                        var clone = new BankaKart
                        {
                            Id = b.Id,
                            TenantId = b.TenantId,
                            BankaAdi = b.BankaAdi,
                            SubeAdi = b.SubeAdi,
                            SubeKodu = b.SubeKodu,
                            HesapNo = b.HesapNo,
                            IBAN = b.IBAN,
                            DovizTuru = b.DovizTuru,
                            Yetkili = b.Yetkili,
                            Telefon = b.Telefon,
                            KartTuru = b.KartTuru ?? (isKasa ? "Kasa" : "Banka"),
                            IsDeleted = false,
                            AcilisBakiyesi = netBakiye,
                            GuncelBakiye = netBakiye
                        };
                        await targetDb.InsertOrReplaceAsync(clone);
                        result.BankalarCount++;

                        if (isKasa)
                        {
                            result.KasalarCount++;
                            if (netBakiye != 0)
                            {
                                await targetDb.InsertAsync(new KasaHareket
                                {
                                    KasaId = clone.Id,
                                    Tarih = new DateTime(nextYear, 1, 1),
                                    IslemTuru = "Devir Fişi",
                                    EvrakNo = $"DEVIR-KASA-{sourceYear}",
                                    Aciklama = $"{sourceYear} Yılı Kasa Açılış Bakiyesi",
                                    Giren = netBakiye > 0 ? netBakiye : 0,
                                    Cikan = netBakiye < 0 ? Math.Abs(netBakiye) : 0,
                                    Tutar = Math.Abs(netBakiye)
                                });
                            }
                        }
                        else
                        {
                            if (netBakiye != 0)
                            {
                                await targetDb.InsertAsync(new BankaHareket
                                {
                                    BankaId = clone.Id,
                                    Tarih = new DateTime(nextYear, 1, 1),
                                    IslemTuru = "Devir Fişi",
                                    EvrakNo = $"DEVIR-BNK-{sourceYear}",
                                    Aciklama = $"{sourceYear} Yılı Banka Açılış Bakiyesi",
                                    Giren = netBakiye > 0 ? netBakiye : 0,
                                    Cikan = netBakiye < 0 ? Math.Abs(netBakiye) : 0,
                                    Tutar = Math.Abs(netBakiye)
                                });
                            }
                        }
                    }
                }

                result.Success = true;
                result.Message = $"{sourceYear} yılındaki güncel veriler {nextYear} yılına başarıyla yansıtıldı. ({result.CarilerCount} Cari, {result.StoklarCount} Stok, {result.BankalarCount} Kasa/Banka)";
                progress?.Report(result.Message);

                // Zincirleme sonraki yıl kontrolü (örn: 2028 varsa)
                int furtherYear = nextYear + 1;
                if (File.Exists(GetDbPathForYear(furtherYear)))
                {
                    await ReflectBalancesForwardAsync(nextYear);
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Yansıtma sırasında hata oluştu: {ex.Message}";
                return result;
            }
            finally
            {
                if (sourceDb != null) { try { await sourceDb.CloseAsync(); } catch { } }
                if (targetDb != null) { try { await targetDb.CloseAsync(); } catch { } }
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
    }
}
