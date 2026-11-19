using System.Net.WebSockets;
using System.Threading;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using ErmayMuhasebe.Models;

namespace ErmayMuhasebe.Services
{
    public class CloudConfig
    {
        public const string DefaultSupabaseUrl = "";
        public const string DefaultSupabaseKey = "";

        public string BaseUrl { get; set; } = DefaultSupabaseUrl;
        public string AuthSecret { get; set; } = DefaultSupabaseKey;
        public string GoogleApiKey { get; set; } = "";
        public string GoogleClientId { get; set; } = "";
        public string GoogleClientSecret { get; set; } = "";
        public bool IsActive { get; set; } = false;
        public bool IsAutoSyncEnabled { get; set; } = true;
    }

    public class CloudSyncService
    {
        public string? LastSyncError { get; set; }
        public event EventHandler? OnCloudDataChanged;
        private ClientWebSocket? _realtimeWs;
        private CancellationTokenSource? _realtimeCts;
        private Task? _realtimeLoopTask;
        private readonly object _wsLock = new();
        private bool _isRealtimeStarted = false;

        public void StartRealtimeWebSocket()
        {
            lock (_wsLock)
            {
                if (_isRealtimeStarted) return;
                _isRealtimeStarted = true;
                _realtimeCts = new CancellationTokenSource();
                _realtimeLoopTask = Task.Run(() => RealtimeWorkerAsync(_realtimeCts.Token));
            }
        }

        public void StopRealtimeWebSocket()
        {
            lock (_wsLock)
            {
                _isRealtimeStarted = false;
                try
                {
                    _realtimeCts?.Cancel();
                    _realtimeWs?.Abort();
                    _realtimeWs?.Dispose();
                }
                catch { }
                finally
                {
                    _realtimeWs = null;
                    _realtimeCts = null;
                }
            }
        }

        private async Task RealtimeWorkerAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _isRealtimeStarted)
            {
                if (!IsConnected || string.IsNullOrEmpty(_config.BaseUrl) || string.IsNullOrEmpty(_config.AuthSecret))
                {
                    try { int jitterMs = Random.Shared.Next(3000, 7000); await Task.Delay(jitterMs, ct).ConfigureAwait(false); } catch { break; }
                    continue;
                }

                try
                {
                    string wsScheme = _config.BaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? "wss://" : "ws://";
                    string host = _config.BaseUrl.Replace("https://", "").Replace("http://", "").TrimEnd('/');
                    var wsUri = new Uri($"{wsScheme}{host}/realtime/v1/websocket?apikey={_config.AuthSecret}&vsn=1.0.0");

                    using var ws = new ClientWebSocket();
                    _realtimeWs = ws;
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    
                    await ws.ConnectAsync(wsUri, linkedCts.Token).ConfigureAwait(false);
                    Console.WriteLine("[CloudSync Realtime] WebSocket baglantisi kuruldu. Phoenix kanali dinleniyor...");

                    var joinMsg = new
                    {
                        topic = "realtime:public",
                        @event = "phx_join",
                        payload = new
                        {
                            config = new
                            {
                                broadcast = new { ack = false },
                                presence = new { key = "" },
                                postgres_changes = new object[]
                                {
                                    new { @event = "*", schema = "public" }
                                }
                            }
                        },
                        @ref = "join_1"
                    };
                    var joinJson = JsonSerializer.Serialize(joinMsg, _jsonOpts);
                    var joinBytes = Encoding.UTF8.GetBytes(joinJson);
                    using var wsSendLock = new SemaphoreSlim(1, 1);
                    await wsSendLock.WaitAsync(linkedCts.Token).ConfigureAwait(false);
                    try
                    {
                        await ws.SendAsync(new ArraySegment<byte>(joinBytes), WebSocketMessageType.Text, true, linkedCts.Token).ConfigureAwait(false);
                    }
                    finally
                    {
                        wsSendLock.Release();
                    }

                    using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(linkedCts.Token);
                    var heartbeatTask = Task.Run(async () =>
                    {
                        int hbSeq = 1;
                        while (!sessionCts.IsCancellationRequested && ws.State == WebSocketState.Open)
                        {
                            try
                            {
                                await Task.Delay(TimeSpan.FromSeconds(25), sessionCts.Token).ConfigureAwait(false);
                                if (ws.State != WebSocketState.Open || sessionCts.IsCancellationRequested) break;

                                var hbMsg = new { topic = "phoenix", @event = "heartbeat", payload = new { }, @ref = $"hb_{hbSeq++}" };
                                string hbJson = JsonSerializer.Serialize(hbMsg, _jsonOpts);
                                var hbBytes = Encoding.UTF8.GetBytes(hbJson);

                                await wsSendLock.WaitAsync(sessionCts.Token).ConfigureAwait(false);
                                try
                                {
                                    if (ws.State == WebSocketState.Open)
                                    {
                                        await ws.SendAsync(new ArraySegment<byte>(hbBytes), WebSocketMessageType.Text, true, sessionCts.Token).ConfigureAwait(false);
                                    }
                                }
                                finally
                                {
                                    wsSendLock.Release();
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                            catch (Exception hbEx)
                            {
                                Console.WriteLine($"[CloudSync Realtime] Heartbeat hatasi: {hbEx.Message}");
                                try { sessionCts.Cancel(); } catch { }
                                break;
                            }
                        }
                    }, sessionCts.Token);

                    var buffer = new byte[8192];
                    using var ms = new MemoryStream();

                    try
                    {
                        while (ws.State == WebSocketState.Open && !sessionCts.IsCancellationRequested)
                        {
                            ms.SetLength(0);
                            WebSocketReceiveResult result;
                            do
                            {
                                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), sessionCts.Token).ConfigureAwait(false);
                                if (result.MessageType == WebSocketMessageType.Close) break;
                                ms.Write(buffer, 0, result.Count);
                            } while (!result.EndOfMessage);

                            if (result.MessageType == WebSocketMessageType.Close) break;

                            string msgStr = Encoding.UTF8.GetString(ms.ToArray());
                            if (msgStr.IndexOf("postgres_changes", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                msgStr.IndexOf("INSERT", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                msgStr.IndexOf("UPDATE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                msgStr.IndexOf("DELETE", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                Console.WriteLine("[CloudSync Realtime] Canli veri degisikligi yakalandi! Delta sync tetikleniyor.");
                                OnCloudDataChanged?.Invoke(this, EventArgs.Empty);
                            }
                        }
                    }
                    finally
                    {
                        sessionCts.Cancel();
                        try { await heartbeatTask.ConfigureAwait(false); } catch { }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CloudSync Realtime] WebSocket baglanti koptu ({ex.Message}), 5 saniye sonra yeniden baglanacak...");
                }

                if (!ct.IsCancellationRequested)
                {
                    try { int jitterMs = Random.Shared.Next(3000, 7000); await Task.Delay(jitterMs, ct).ConfigureAwait(false); } catch { break; }
                }
            }
        }

        public async Task<bool> SaveInvoiceAtomicAsync(Fatura fatura, List<FaturaDetay> detaylar, CariKart? cari, string? cihazRolu = "Windows Masaustu")
        {
            if (!IsConnected) return false;
            try
            {
                var items = new List<object>();
                foreach (var d in detaylar)
                {
                    items.Add(new
                    {
                        uuid = string.IsNullOrEmpty(d.Uuid) ? Guid.NewGuid().ToString("D") : d.Uuid,
                        stok_id = d.StokId > 0 ? d.StokId : (int?)null,
                        stok_uuid = d.StokUuid,
                        stok_adi = d.StokAdi ?? "",
                        stok_kodu = d.StokKodu ?? "",
                        miktar = (decimal)d.Miktar,
                        birim_fiyat = d.BirimFiyat,
                        toplam_tutar = d.ToplamTutar,
                        net_tutar = d.ToplamTutar,
                        kdv_orani = (decimal)d.KDVOrani,
                        kdv_tutari = d.KdvTutari,
                        iskonto_orani = d.IskontoOrani,
                        iskonto_tutari = d.IskontoTutari,
                        birim = d.Birim ?? "Adet",
                        aciklama = d.Aciklama ?? ""
                    });
                }

                string odemeSekli = string.IsNullOrWhiteSpace(fatura.OdemeSekli) ? "Açık Hesap" : fatura.OdemeSekli;
                bool isKapali = (fatura.GenelToplam > 0 && fatura.Odenen >= fatura.GenelToplam) ||
                                (!odemeSekli.Contains("Açık", StringComparison.OrdinalIgnoreCase) &&
                                 !odemeSekli.Contains("Vadeli", StringComparison.OrdinalIgnoreCase));

                var payload = new Dictionary<string, object?>
                {
                    ["idempotency_key"] = !string.IsNullOrEmpty(fatura.Uuid) ? $"inv_{fatura.Uuid}_{fatura.Version}" : Guid.NewGuid().ToString("D"),
                    ["uuid"] = string.IsNullOrEmpty(fatura.Uuid) ? (fatura.Uuid = Guid.NewGuid().ToString("D")) : fatura.Uuid,
                    ["fatura_no"] = string.IsNullOrWhiteSpace(fatura.FaturaNo) ? $"FTR-{fatura.Id}" : fatura.FaturaNo,
                    ["tenant_id"] = CurrentTenantId,
                    ["mali_yil"] = fatura.MaliYil > 0 ? fatura.MaliYil : fatura.Tarih.Year,
                    ["cari_id"] = fatura.CariId > 0 ? fatura.CariId : (int?)null,
                    ["cari_uuid"] = fatura.CariUuid ?? cari?.Uuid,
                    ["cari_unvan"] = fatura.CariUnvan ?? cari?.Unvan ?? "",
                    ["tur"] = string.IsNullOrWhiteSpace(fatura.Tur) ? "Satış" : fatura.Tur,
                    ["fatura_turu"] = string.IsNullOrWhiteSpace(fatura.Tur) ? "Satış" : fatura.Tur,
                    ["fatura_tipi"] = isKapali ? "Kapalı" : "Açık",
                    ["is_kapali"] = isKapali,
                    ["odeme_sekli"] = odemeSekli,
                    ["odeme_durumu"] = isKapali ? "Ödendi" : (fatura.Odenen > 0 ? "Kısmi Ödendi" : "Bekliyor"),
                    ["kasa_id"] = fatura.KasaId > 0 ? fatura.KasaId : (int?)null,
                    ["banka_id"] = fatura.BankaId > 0 ? fatura.BankaId : (int?)null,
                    ["odenen"] = fatura.Odenen,
                    ["kalan_tutar"] = fatura.Kalan,
                    ["tarih"] = FormatUtcIso(fatura.Tarih),
                    ["vade_tarihi"] = FormatUtcIso(fatura.VadeTarihi),
                    ["genel_toplam"] = fatura.GenelToplam,
                    ["ara_toplam"] = fatura.AraToplam,
                    ["kdv_toplam"] = fatura.KdvToplam,
                    ["iskonto_toplam"] = fatura.IskontoToplam,
                    ["doviz_turu"] = string.IsNullOrWhiteSpace(fatura.DovizTuru) ? "TRY" : fatura.DovizTuru,
                    ["doviz_kuru"] = fatura.DovizKuru > 0 ? fatura.DovizKuru : 1m,
                    ["vergi_dairesi"] = fatura.VergiDairesi ?? "",
                    ["vergi_no"] = fatura.VergiNo ?? "",
                    ["adres"] = fatura.Adres ?? "",
                    ["baglanti_evrak_no"] = fatura.BaglantiEvrakNo ?? "",
                    ["is_earsiv"] = fatura.IsEArsiv,
                    ["iptal_mi"] = fatura.IptalMi,
                    ["aciklama"] = fatura.Aciklama ?? "",
                    ["cihaz_rolu"] = cihazRolu ?? "Windows Masaustu",
                    ["detaylar"] = items
                };

                var doc = await CallRpcWithResultAsync("save_invoice_atomic", new { p_payload = payload });
                if (doc != null && doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("success", out var sProp) && sProp.ValueKind == JsonValueKind.True)
                    {
                        Console.WriteLine($"[CloudSyncService] save_invoice_atomic RPC basarili. Fatura: {fatura.FaturaNo}, UUID: {fatura.Uuid}");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CloudSyncService] save_invoice_atomic RPC hatasi: {ex.Message}");
            }
            return false;
        }

        public async Task<(bool Success, string Message)> RunSystemIntegrityRepairAsync()
        {
            if (!IsConnected) return (false, "Bulut baglantisi yok");
            try
            {
                var doc = await CallRpcWithResultAsync("auto_repair_system_integrity", new { p_tenant_id = CurrentTenantId });
                if (doc != null)
                {
                    return (true, "Bulut butunluk onarimi ve bakiye mutabakati basariyla tamamlandi.");
                }
                return (false, "RPC cagrisi sonuc uretmedi.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }


        public async Task<bool> DeleteStokHareketByFaturaIdAsync(int faturaId, string? faturaNo, string? faturaUuid)
        {
            bool ok = true;
            if (!string.IsNullOrEmpty(faturaUuid))
                ok &= await DeleteFilteredAsync("stok_hareketler", $"fatura_uuid=eq.{faturaUuid}");
            if (!string.IsNullOrEmpty(faturaNo))
                ok &= await DeleteFilteredAsync("stok_hareketler", $"evrak_no=eq.{faturaNo}");
            if (faturaId > 0)
                ok &= await DeleteFilteredAsync("stok_hareketler", $"fatura_id=eq.{faturaId}");
            return ok;
        }

        public async Task<bool> SoftDeleteStokHareketByFaturaIdAsync(int faturaId, string? faturaUuid)
        {
            bool ok = true;
            if (!string.IsNullOrEmpty(faturaUuid))
                ok &= await DeleteFilteredAsync("stok_hareketler", $"fatura_uuid=eq.{faturaUuid}");
            if (faturaId > 0)
                ok &= await DeleteFilteredAsync("stok_hareketler", $"fatura_id=eq.{faturaId}");
            return ok;
        }

        public async Task<bool> DeleteCariHareketByFaturaIdAsync(int faturaId, string? faturaNo, string? faturaUuid)
        {
            bool ok = true;
            if (!string.IsNullOrEmpty(faturaUuid))
                ok &= await DeleteFilteredAsync("cari_hareketler", $"fatura_uuid=eq.{faturaUuid}");
            if (!string.IsNullOrEmpty(faturaNo))
                ok &= await DeleteFilteredAsync("cari_hareketler", $"evrak_no=eq.{faturaNo}");
            if (faturaId > 0)
                ok &= await DeleteFilteredAsync("cari_hareketler", $"fatura_id=eq.{faturaId}");
            return ok;
        }

        public async Task<bool> SoftDeleteCariHareketByFaturaIdAsync(int faturaId, string? faturaUuid)
        {
            bool ok = true;
            if (!string.IsNullOrEmpty(faturaUuid))
                ok &= await DeleteFilteredAsync("cari_hareketler", $"fatura_uuid=eq.{faturaUuid}");
            if (faturaId > 0)
                ok &= await DeleteFilteredAsync("cari_hareketler", $"fatura_id=eq.{faturaId}");
            return ok;
        }

        public async Task<bool> SoftDeleteFaturaDetaylarAsync(int faturaId, string? faturaUuid)
        {
            bool ok = true;
            if (!string.IsNullOrEmpty(faturaUuid))
                ok &= await DeleteFilteredAsync("fatura_detaylar", $"fatura_uuid=eq.{faturaUuid}");
            if (faturaId > 0)
                ok &= await DeleteFilteredAsync("fatura_detaylar", $"fatura_id=eq.{faturaId}");
            return ok;
        }

        private CloudConfig _config = new();
        private readonly string _configPath;
        private readonly IYearContext _yearContext;
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        private static readonly JsonSerializerOptions _jsonOpts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public object? Client => this;
        public CloudConfig Config => _config;
        public static bool DisableCloudSync { get; set; } = false;
        
        public async Task<bool> QuickPingAsync(int timeoutMs = 2500)
        {
            if (!IsConnected || string.IsNullOrEmpty(_config.BaseUrl)) return false;
            try
            {
                using var cts = new CancellationTokenSource(timeoutMs);
                using var req = CreateRequest(HttpMethod.Get, "cariler?select=id&limit=1");
                using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                return res.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public bool IsConnected => !DisableCloudSync && !string.IsNullOrEmpty(_config.BaseUrl) && !string.IsNullOrEmpty(_config.AuthSecret) && _config.IsActive;
        public string BaseUrl => _config.BaseUrl;
        public string AuthSecret => _config.AuthSecret;
        public bool IsAutoSyncEnabled => _config.IsAutoSyncEnabled;
        private string _currentTenantId = ErmayMuhasebe.Data.Constants.DefaultEnterpriseTenant;
        public string CurrentTenantId
        {
            get => _currentTenantId;
            set => _currentTenantId = (string.IsNullOrWhiteSpace(value) || value == "default" || value == "tenant_vkrts")
                ? ErmayMuhasebe.Data.Constants.DefaultEnterpriseTenant
                : value;
        }

        public void Disconnect()
        {
            _config.IsActive = false;
        }

        private void SaveConfigInternal()
        {
            try
            {
                var persistentConfig = new CloudConfig
                {
                    BaseUrl = _config.BaseUrl,
                    AuthSecret = string.IsNullOrEmpty(_config.AuthSecret) ? "" : (AuthService.Encrypt(_config.AuthSecret) is { Length: > 0 } enc ? enc : _config.AuthSecret),
                    GoogleApiKey = string.IsNullOrEmpty(_config.GoogleApiKey) ? "" : (AuthService.Encrypt(_config.GoogleApiKey) is { Length: > 0 } encG ? encG : _config.GoogleApiKey),
                    GoogleClientId = _config.GoogleClientId,
                    GoogleClientSecret = string.IsNullOrEmpty(_config.GoogleClientSecret) ? "" : (AuthService.Encrypt(_config.GoogleClientSecret) is { Length: > 0 } encS ? encS : _config.GoogleClientSecret),
                    IsActive = _config.IsActive,
                    IsAutoSyncEnabled = _config.IsAutoSyncEnabled
                };
                var json = JsonSerializer.Serialize(persistentConfig);
                File.WriteAllText(_configPath, json);

                // Yedek konum: ErmayMuhasebe alt klasörü
                try
                {
                    var backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
                    if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);
                    var backupPath = Path.Combine(backupDir, "ermay_cloud_config.json");
                    File.WriteAllText(backupPath, json);
                }
                catch { }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving config: {ex.Message}");
            }
        }

        public void EnableAutoSync(bool enable)
        {
            _config.IsAutoSyncEnabled = enable;
            SaveConfigInternal();
        }

        public CloudSyncService(IYearContext yearContext)
        {
            _yearContext = yearContext;
            _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ermay_cloud_config.json");
            LoadConfig();
        }

        public string GetYearlyPath(string resourceName)
        {
            return resourceName;
        }

        public static string CleanSupabaseUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            string clean = url.Trim();
            while (clean.EndsWith("/")) clean = clean.Substring(0, clean.Length - 1);
            if (clean.EndsWith("/rest/v1", StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring(0, clean.Length - "/rest/v1".Length);
            while (clean.EndsWith("/")) clean = clean.Substring(0, clean.Length - 1);
            return clean;
        }

        public string GetCleanBaseUrl() => CleanSupabaseUrl(_config.BaseUrl);

        public void ReloadConfig() => LoadConfig();

        public void LoadConfig()
        {
            try
            {
                var candidatePaths = new List<string>
                {
                    _configPath,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe", "ermay_cloud_config.json"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ermay_cloud_config.json")
                };

                CloudConfig? loaded = null;

                foreach (var path in candidatePaths)
                {
                    if (File.Exists(path))
                    {
                        try
                        {
                            var json = File.ReadAllText(path);
                            var parsed = JsonSerializer.Deserialize<CloudConfig>(json);
                            if (parsed != null && !string.IsNullOrWhiteSpace(parsed.BaseUrl))
                            {
                                loaded = parsed;
                                break;
                            }
                        }
                        catch { }
                    }
                }

                // Eğer hala boşsa setup_initial_user.json veya setup_config.json dosyalarını kontrol et
                if (loaded == null || string.IsNullOrWhiteSpace(loaded.BaseUrl) || string.IsNullOrWhiteSpace(loaded.AuthSecret))
                {
                    var ermayDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
                    var setupJsonCandidates = new[]
                    {
                        Path.Combine(ermayDir, "setup_initial_user.json"),
                        Path.Combine(ermayDir, "setup_config.json")
                    };

                    foreach (var sPath in setupJsonCandidates)
                    {
                        if (File.Exists(sPath))
                        {
                            try
                            {
                                var sText = File.ReadAllText(sPath);
                                using var doc = JsonDocument.Parse(sText);
                                var root = doc.RootElement;
                                string sUrl = root.TryGetProperty("SupabaseUrl", out var pUrl) ? (pUrl.GetString() ?? "") : "";
                                string sKey = root.TryGetProperty("SupabaseKey", out var pKey) ? (pKey.GetString() ?? "") : "";

                                if (!string.IsNullOrWhiteSpace(sUrl) && !string.IsNullOrWhiteSpace(sKey))
                                {
                                    loaded = new CloudConfig
                                    {
                                        BaseUrl = sUrl,
                                        AuthSecret = sKey,
                                        IsActive = true,
                                        IsAutoSyncEnabled = true
                                    };
                                    break;
                                }
                            }
                            catch { }
                        }
                    }
                }

                if (loaded != null)
                {
                    if (!string.IsNullOrEmpty(loaded.BaseUrl) && !string.IsNullOrEmpty(loaded.AuthSecret))
                    {
                        if (loaded.AuthSecret.StartsWith("ENC::AES::"))
                        {
                            var decrypted = AuthService.Decrypt(loaded.AuthSecret);
                            if (!string.IsNullOrEmpty(decrypted))
                                loaded.AuthSecret = decrypted;
                            else
                                loaded.AuthSecret = CloudConfig.DefaultSupabaseKey;
                        }
                        if (!string.IsNullOrEmpty(loaded.GoogleApiKey) && loaded.GoogleApiKey.StartsWith("ENC::AES::"))
                        {
                            var decKey = AuthService.Decrypt(loaded.GoogleApiKey);
                            if (!string.IsNullOrEmpty(decKey))
                                loaded.GoogleApiKey = decKey;
                        }
                        if (!string.IsNullOrEmpty(loaded.GoogleClientSecret) && loaded.GoogleClientSecret.StartsWith("ENC::AES::"))
                        {
                            var decSec = AuthService.Decrypt(loaded.GoogleClientSecret);
                            if (!string.IsNullOrEmpty(decSec))
                                loaded.GoogleClientSecret = decSec;
                        }

                        // Check if legacy Firebase URL is stored
                        if (loaded.BaseUrl.Contains("firebaseio.com"))
                        {
                            _config = new CloudConfig
                            {
                                BaseUrl = CloudConfig.DefaultSupabaseUrl,
                                AuthSecret = CloudConfig.DefaultSupabaseKey,
                                IsActive = false,
                                IsAutoSyncEnabled = false
                            };
                        }
                        else
                        {
                            loaded.BaseUrl = CleanSupabaseUrl(loaded.BaseUrl);
                            _config = loaded;
                            _config.IsActive = !string.IsNullOrWhiteSpace(_config.BaseUrl) && !string.IsNullOrWhiteSpace(_config.AuthSecret);
                            _config.IsAutoSyncEnabled = _config.IsActive;
                        }
                    }
                    else
                    {
                        _config = loaded;
                        _config.BaseUrl = CloudConfig.DefaultSupabaseUrl;
                        _config.AuthSecret = CloudConfig.DefaultSupabaseKey;
                        _config.IsActive = false;
                        _config.IsAutoSyncEnabled = false;
                    }
                }
                else
                {
                    _config = new CloudConfig
                    {
                        BaseUrl = CloudConfig.DefaultSupabaseUrl,
                        AuthSecret = CloudConfig.DefaultSupabaseKey,
                        IsActive = false,
                        IsAutoSyncEnabled = false
                    };
                }

                if (string.IsNullOrWhiteSpace(_config.BaseUrl) || string.IsNullOrWhiteSpace(_config.AuthSecret) || _config.AuthSecret.StartsWith("ENC::AES::"))
                {
                    _config.BaseUrl = CloudConfig.DefaultSupabaseUrl;
                    _config.AuthSecret = CloudConfig.DefaultSupabaseKey;
                    _config.IsActive = false;
                    _config.IsAutoSyncEnabled = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cloud Config Load Error: {ex.Message}");
                _config = new CloudConfig
                {
                    BaseUrl = CloudConfig.DefaultSupabaseUrl,
                    AuthSecret = CloudConfig.DefaultSupabaseKey,
                    IsActive = false,
                    IsAutoSyncEnabled = false
                };
            }
        }

        public void SaveConfig(string url, string secret)
        {
            var cleanUrl = CleanSupabaseUrl(url);
            _config.BaseUrl = string.IsNullOrWhiteSpace(cleanUrl) ? CloudConfig.DefaultSupabaseUrl : cleanUrl;
            string rawSecret = secret?.Trim() ?? "";
            if (rawSecret.StartsWith("ENC::AES::"))
            {
                try { rawSecret = AuthService.Decrypt(rawSecret); } catch { }
            }
            _config.AuthSecret = string.IsNullOrWhiteSpace(rawSecret) ? CloudConfig.DefaultSupabaseKey : rawSecret;
            _config.IsActive = !string.IsNullOrEmpty(_config.BaseUrl) && !string.IsNullOrEmpty(_config.AuthSecret);
            _config.IsAutoSyncEnabled = true;
            SaveConfigInternal();
        }

        public async Task<(bool Success, string Message)> TestConnectionAsync()
        {
            if (!IsConnected)
                return (false, "Bulut bağlantı bilgileri eksik veya devre dışı bırakılmış.");

            try
            {
                using var req = CreateRequest(HttpMethod.Get, "cariler?select=id&limit=1");
                using var res = await _http.SendAsync(req);
                if (res.IsSuccessStatusCode)
                    return (true, "Supabase bulut bağlantısı başarılı. Senkronizasyon hazır.");

                return (false, $"Bulut sunucu yanıtı: HTTP {(int)res.StatusCode} ({res.ReasonPhrase})");
            }
            catch (Exception ex)
            {
                return (false, $"Bulut sunucusuna erişilemedi: {ex.Message}");
            }
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string pathAndQuery)
        {
            string cleanBase = GetCleanBaseUrl();
            string url = $"{cleanBase}/rest/v1/{pathAndQuery.TrimStart('/')}";
            var req = new HttpRequestMessage(method, url);
            req.Headers.Add("apikey", _config.AuthSecret);
            req.Headers.Add("Authorization", $"Bearer {_config.AuthSecret}");
            return req;
        }

        private async Task SafeRun(Func<Task> action)
        {
            if (!IsConnected) return;
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase SafeRun Error] {ex.Message}");
            }
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentDictionary<string, byte>> _unsupportedColumns = new(StringComparer.OrdinalIgnoreCase);

        private static void StripKnownUnsupportedColumns(string table, object? payload)
        {
            if (payload is not IDictionary<string, object?> dict) return;
            string baseTable = table.Split('?')[0].Trim().ToLowerInvariant();
            if (_unsupportedColumns.TryGetValue(baseTable, out var cols))
            {
                foreach (var col in cols.Keys)
                {
                    dict.Remove(col);
                }
            }
        }

        private static void RecordUnsupportedColumn(string table, string column)
        {
            if (string.IsNullOrWhiteSpace(column)) return;
            string baseTable = table.Split('?')[0].Trim().ToLowerInvariant();
            var cols = _unsupportedColumns.GetOrAdd(baseTable, _ => new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));
            cols.TryAdd(column, 1);
        }

        private object EnsureTenantPayload(string table, object payload)
        {
            if (string.IsNullOrEmpty(CurrentTenantId) || payload == null) return payload;
            if (payload is IDictionary<string, object?> dict)
            {
                var existingTenant = dict.TryGetValue("tenant_id", out var tv) ? tv?.ToString() : null;
                if (string.IsNullOrWhiteSpace(existingTenant) || existingTenant.Equals("default", StringComparison.OrdinalIgnoreCase))
                {
                    dict["tenant_id"] = CurrentTenantId;
                }
                return dict;
            }
            if (payload is Dictionary<string, object> dictObj)
            {
                var existingTenant = dictObj.TryGetValue("tenant_id", out var tv) ? tv?.ToString() : null;
                if (string.IsNullOrWhiteSpace(existingTenant) || existingTenant.Equals("default", StringComparison.OrdinalIgnoreCase))
                {
                    dictObj["tenant_id"] = CurrentTenantId;
                }
                return dictObj;
            }
            try
            {
                var json = JsonSerializer.Serialize(payload, _jsonOpts);
                var deserialized = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
                if (deserialized != null)
                {
                    var existingTenant = deserialized.TryGetValue("tenant_id", out var tv) ? tv?.ToString() : null;
                    if (string.IsNullOrWhiteSpace(existingTenant) || existingTenant.Equals("default", StringComparison.OrdinalIgnoreCase))
                    {
                        deserialized["tenant_id"] = CurrentTenantId;
                    }
                    return deserialized;
                }
            }
            catch { }
            return payload;
        }

        private string? BuildTenantFilter(string table, string? existingFilter)
        {
            if (string.IsNullOrEmpty(CurrentTenantId)) return existingFilter;

            var lowerTable = table.ToLowerInvariant();
            if (lowerTable == "kullanicilar" || lowerTable.StartsWith("kullanicilar?"))
            {
                return existingFilter;
            }

            if (existingFilter != null && existingFilter.Contains("tenant_id="))
            {
                return existingFilter;
            }

            var tenants = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "default",
                DatabaseService.DefaultTenantId
            };
            if (!string.IsNullOrWhiteSpace(CurrentTenantId))
            {
                tenants.Add(CurrentTenantId.Trim());
            }

            string tenantList = string.Join(",", tenants.Select(Uri.EscapeDataString));
            string tenantParam = $"tenant_id=in.({tenantList})";
            if (string.IsNullOrEmpty(existingFilter))
            {
                return tenantParam;
            }
            return $"{existingFilter}&{tenantParam}";
        }

        public async Task<bool> UpsertPayloadAsync(string table, object payload)
        {
            if (!IsConnected || payload == null) return false;
            try
            {
                object finalPayload = EnsureTenantPayload(table, payload);
                StripKnownUnsupportedColumns(table, finalPayload);
                bool hasUuid = finalPayload is IDictionary<string, object?> dict && 
                               dict.ContainsKey("uuid") && 
                               !string.IsNullOrWhiteSpace(dict["uuid"]?.ToString());
                string queryPath = (hasUuid && !table.Contains("on_conflict"))
                    ? (table.Contains("?") ? $"{table}&on_conflict=uuid" : $"{table}?on_conflict=uuid")
                    : table;

                int maxRetries = 35;
                while (maxRetries-- > 0)
                {
                    StripKnownUnsupportedColumns(table, finalPayload);
                    using var req = CreateRequest(HttpMethod.Post, queryPath);
                    req.Headers.Add("Prefer", "resolution=merge-duplicates");
                    string json = JsonSerializer.Serialize(finalPayload, _jsonOpts);
                    req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    using var res = await _http.SendAsync(req);
                    if (res.IsSuccessStatusCode) return true;

                    var err = await res.Content.ReadAsStringAsync();
                    if (err.Contains("42P10") || err.Contains("no unique or exclusion constraint matching the ON CONFLICT"))
                    {
                        // Fallback without on_conflict=uuid if unique index is not yet present in Supabase
                        queryPath = table;
                        continue;
                    }

                    if (err.Contains("23505") || err.Contains("duplicate key value violates unique constraint"))
                    {
                        string baseTable = table.Split('?')[0];
                        // NEVER fall back to on_conflict=id — that would overwrite another device's record sharing the same numeric id.
                        // Instead, strip the colliding local id so PostgreSQL assigns a fresh cloud sequence id while merging by uuid.
                        if (finalPayload is IDictionary<string, object?> idDict && idDict.ContainsKey("id"))
                        {
                            idDict.Remove("id");
                            queryPath = hasUuid ? $"{baseTable}?on_conflict=uuid" : baseTable;
                            continue;
                        }
                    }

                    if (err.Contains("PGRST204") || err.Contains("42703") || err.Contains("does not exist"))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(err, @"Could not find the '([^']+)' column");
                        if (!match.Success)
                            match = System.Text.RegularExpressions.Regex.Match(err, @"column ""([^""]+)"" of relation");
                        if (match.Success)
                        {
                            string missingCol = match.Groups[1].Value;
                            RecordUnsupportedColumn(table, missingCol);
                            if (finalPayload is IDictionary<string, object?> fDict && fDict.ContainsKey(missingCol))
                            {
                                fDict.Remove(missingCol);
                                continue;
                            }
                        }

                        if (err.Contains("tenant_id") && finalPayload is IDictionary<string, object?> fDictTenant && fDictTenant.ContainsKey("tenant_id"))
                        {
                            RecordUnsupportedColumn(table, "tenant_id");
                            fDictTenant.Remove("tenant_id");
                            continue;
                        }
                    }

                    Console.WriteLine($"[Supabase UpsertPayload Failed] {table} ({res.StatusCode}): {err}");
                    return false;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase Upsert Error] {table}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> PatchPayloadAsync(string table, string filter, object payload)
        {
            if (!IsConnected || payload == null) return false;
            try
            {
                object finalPayload = EnsureTenantPayload(table, payload);
                StripKnownUnsupportedColumns(table, finalPayload);
                string? finalFilter = BuildTenantFilter(table, filter) ?? filter;
                int maxRetries = 30;
                while (maxRetries-- > 0)
                {
                    StripKnownUnsupportedColumns(table, finalPayload);
                    using var req = CreateRequest(HttpMethod.Patch, $"{table}?{finalFilter}");
                    string json = JsonSerializer.Serialize(finalPayload, _jsonOpts);
                    req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    using var res = await _http.SendAsync(req);
                    if (res.IsSuccessStatusCode) return true;

                    var err = await res.Content.ReadAsStringAsync();
                    if (err.Contains("PGRST204") || err.Contains("42703") || err.Contains("does not exist"))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(err, @"Could not find the '([^']+)' column");
                        if (!match.Success)
                            match = System.Text.RegularExpressions.Regex.Match(err, @"column ""([^""]+)"" of relation");
                        if (match.Success)
                        {
                            string missingCol = match.Groups[1].Value;
                            RecordUnsupportedColumn(table, missingCol);
                            if (finalPayload is IDictionary<string, object?> fDict && fDict.ContainsKey(missingCol))
                            {
                                fDict.Remove(missingCol);
                                continue;
                            }
                        }
                    }

                    if (finalFilter != filter)
                    {
                        finalFilter = filter;
                        continue;
                    }
                    Console.WriteLine($"[Supabase Patch Failed] {table}?{finalFilter} ({res.StatusCode}): {err}");
                    return false;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase Patch Error] {table}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpsertBatchPayloadAsync(string table, IEnumerable<object> items)
        {
            if (!IsConnected || items == null || !items.Any()) return false;
            try
            {
                var enrichedItems = items.Select(item =>
                {
                    var p = EnsureTenantPayload(table, item);
                    StripKnownUnsupportedColumns(table, p);
                    return p;
                }).ToList();
                bool hasUuid = enrichedItems.Any(x => x is IDictionary<string, object?> dict && dict.ContainsKey("uuid") && !string.IsNullOrWhiteSpace(dict["uuid"]?.ToString()));
                bool hasId = enrichedItems.Any(x => x is IDictionary<string, object?> dict && dict.ContainsKey("id") && dict["id"] != null);
                string conflictCol = hasUuid ? "uuid" : (hasId ? "id" : "uuid");
                string queryPath = !table.Contains("on_conflict")
                    ? (table.Contains("?") ? $"{table}&on_conflict={conflictCol}" : $"{table}?on_conflict={conflictCol}")
                    : table;

                int maxRetries = 30;
                while (maxRetries-- > 0)
                {
                    foreach (var item in enrichedItems) StripKnownUnsupportedColumns(table, item);
                    using var req = CreateRequest(HttpMethod.Post, queryPath);
                    req.Headers.Add("Prefer", "resolution=merge-duplicates");
                    string json = JsonSerializer.Serialize(enrichedItems, _jsonOpts);
                    req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    using var res = await _http.SendAsync(req);
                    if (res.IsSuccessStatusCode) return true;

                    var err = await res.Content.ReadAsStringAsync();
                    if (err.Contains("42P10") || err.Contains("no unique or exclusion constraint matching the ON CONFLICT"))
                    {
                        queryPath = table;
                        continue;
                    }

                    if (err.Contains("23505") || err.Contains("duplicate key value violates unique constraint"))
                    {
                        string baseTable = table.Split('?')[0];
                        bool strippedAnyId = false;
                        foreach (var item in enrichedItems)
                        {
                            if (item is IDictionary<string, object?> idDict && idDict.ContainsKey("id"))
                            {
                                idDict.Remove("id");
                                strippedAnyId = true;
                            }
                        }
                        if (strippedAnyId)
                        {
                            queryPath = hasUuid ? $"{baseTable}?on_conflict=uuid" : baseTable;
                            continue;
                        }
                    }

                    if (err.Contains("PGRST204") || err.Contains("42703") || err.Contains("does not exist"))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(err, @"Could not find the '([^']+)' column");
                        if (!match.Success)
                            match = System.Text.RegularExpressions.Regex.Match(err, @"column ""([^""]+)"" of relation");
                        if (match.Success)
                        {
                            string missingCol = match.Groups[1].Value;
                            RecordUnsupportedColumn(table, missingCol);
                            foreach (var item in enrichedItems)
                            {
                                if (item is IDictionary<string, object?> dict && dict.ContainsKey(missingCol))
                                {
                                    dict.Remove(missingCol);
                                }
                            }
                            continue;
                        }

                        if (err.Contains("tenant_id"))
                        {
                            RecordUnsupportedColumn(table, "tenant_id");
                            foreach (var item in enrichedItems)
                            {
                                if (item is IDictionary<string, object?> dict) dict.Remove("tenant_id");
                            }
                            continue;
                        }
                    }

                    Console.WriteLine($"[Supabase UpsertBatch Failed] {table} ({res.StatusCode}): {err}");
                    return false;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase UpsertBatch Error] {table}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CallRpcAsync(string rpcName, object? parameters = null)
        {
            if (!IsConnected) return false;
            try
            {
                using var req = CreateRequest(HttpMethod.Post, $"rpc/{rpcName}");
                if (parameters != null)
                {
                    string json = JsonSerializer.Serialize(parameters, _jsonOpts);
                    req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }
                using var res = await _http.SendAsync(req);
                if (res.IsSuccessStatusCode) return true;

                var err = await res.Content.ReadAsStringAsync();
                Console.WriteLine($"[Supabase RPC Failed] {rpcName} ({res.StatusCode}): {err}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase RPC Error] {rpcName}: {ex.Message}");
                return false;
            }
        }

        public async Task<JsonDocument?> CallRpcWithResultAsync(string rpcName, object? parameters = null)
        {
            if (!IsConnected) return null;
            try
            {
                using var req = CreateRequest(HttpMethod.Post, $"rpc/{rpcName}");
                if (parameters != null)
                {
                    string json = JsonSerializer.Serialize(parameters, _jsonOpts);
                    req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }
                using var res = await _http.SendAsync(req);
                if (!res.IsSuccessStatusCode)
                {
                    var err = await res.Content.ReadAsStringAsync();
                    Console.WriteLine($"[Supabase RPC Failed] {rpcName} ({res.StatusCode}): {err}");
                    return null;
                }
                using var stream = await res.Content.ReadAsStreamAsync();
                return await JsonDocument.ParseAsync(stream);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase RPC Error] {rpcName}: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> SyncFinancialTransactionAtomicAsync(
            CariHareket? mainCH,
            CariHareket? supplierCH = null,
            KasaHareket? kasaHareket = null,
            KasaHareket? ciroKasaHareket = null,
            BankaHareket? bankaHareket = null,
            BankaHareket? ciroBankaHareket = null,
            KrediKartiIslem? kk = null,
            EftIslem? eft = null,
            List<string>? deletedUuids = null)
        {
            if (!IsConnected) return false;
            try
            {
                var payload = new Dictionary<string, object?>
                {
                    ["p_tenant_id"] = CurrentTenantId,
                    ["p_ref_id"] = mainCH?.RefId ?? "",
                    ["p_islem_turu"] = mainCH?.IslemTuru ?? "",
                    ["p_evrak_no"] = mainCH?.EvrakNo ?? "",
                    ["p_cari_hareket"] = mainCH != null ? MapCariHareketToPayload(mainCH) : null,
                    ["p_supplier_hareket"] = supplierCH != null ? MapCariHareketToPayload(supplierCH) : null,
                    ["p_kasa_hareket"] = kasaHareket != null ? MapKasaHareketToPayload(kasaHareket) : null,
                    ["p_ciro_kasa_hareket"] = ciroKasaHareket != null ? MapKasaHareketToPayload(ciroKasaHareket) : null,
                    ["p_banka_hareket"] = bankaHareket != null ? MapBankaHareketToPayload(bankaHareket) : null,
                    ["p_ciro_banka_hareket"] = ciroBankaHareket != null ? MapBankaHareketToPayload(ciroBankaHareket) : null,
                    ["p_kredi_karti"] = kk != null ? MapKrediKartiToPayload(kk) : null,
                    ["p_eft"] = eft != null ? MapEftToPayload(eft) : null,
                    ["p_deleted_uuids"] = deletedUuids ?? new List<string>()
                };

                bool rpcOk = await CallRpcAsync("api_save_financial_transaction", new { payload });
                if (rpcOk)
                {
                    Console.WriteLine($"[CloudSyncService] Atomik finansal işlem başarıyla buluta işlendi. RefId: {mainCH?.RefId}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CloudSyncService] Atomik işlem hatası, sıralı eşitlemeye dönülüyor: {ex.Message}");
            }

            // Fallback: Sıralı eşitleme
            if (mainCH != null) await SyncCariHareketAsync(mainCH);
            if (supplierCH != null) await SyncCariHareketAsync(supplierCH);
            if (kasaHareket != null) await SyncKasaHareketAsync(kasaHareket);
            if (ciroKasaHareket != null) await SyncKasaHareketAsync(ciroKasaHareket);
            if (bankaHareket != null) await SyncBankaHareketAsync(bankaHareket);
            if (ciroBankaHareket != null) await SyncBankaHareketAsync(ciroBankaHareket);
            if (kk != null) await SyncKrediKartiIslemAsync(kk);
            if (eft != null) await SyncEftIslemAsync(eft);

            return true;
        }

        public async Task<bool> DeleteFinancialTransactionAtomicAsync(string refId, List<string>? deletedUuids = null)
        {
            if (!IsConnected) return false;
            try
            {
                bool rpcOk = await CallRpcAsync("api_delete_financial_transaction", new
                {
                    p_tenant_id = CurrentTenantId,
                    p_ref_id = refId,
                    p_deleted_uuids = deletedUuids ?? new List<string>()
                });
                if (rpcOk)
                {
                    Console.WriteLine($"[CloudSyncService] Atomik finansal işlem başarıyla buluttan silindi. RefId: {refId}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CloudSyncService] Atomik silme hatası: {ex.Message}");
            }

            // Fallback: Sıralı silme
            if (deletedUuids != null)
            {
                foreach (var u in deletedUuids)
                {
                    await DeleteCariHareketAsync(0, u);
                    await DeleteKasaHareketAsync(0, u);
                    await DeleteBankaHareketAsync(0, u);
                }
            }
            return true;
        }

        public async Task<bool> DeleteAsync(string table, int id, string? uuid = null)
        {
            if (!IsConnected) return false;
            try
            {
                string filter = !string.IsNullOrEmpty(uuid) ? $"uuid=eq.{Uri.EscapeDataString(uuid)}" : $"id=eq.{id}";
                string filterWithTenant = filter;
                if (!string.IsNullOrEmpty(CurrentTenantId))
                {
                    filterWithTenant += $"&tenant_id=eq.{Uri.EscapeDataString(CurrentTenantId)}";
                }

                string nowStr = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                string softDeleteJson = $"{{\"is_deleted\":true,\"updated_at\":\"{nowStr}\",\"guncelleme_tarihi\":\"{nowStr}\"}}";

                // 1. Soft delete dene (tenant_id ile)
                bool patchOk = false;
                try
                {
                    using var patchReq = CreateRequest(HttpMethod.Patch, $"{table}?{filterWithTenant}");
                    patchReq.Content = new StringContent(softDeleteJson, Encoding.UTF8, "application/json");
                    using var patchRes = await _http.SendAsync(patchReq);
                    patchOk = patchRes.IsSuccessStatusCode;
                }
                catch { }

                // Fallback: tenant_id şemada yoksa veya uyuşmadıysa sadece filter ile soft delete yap
                if (!patchOk)
                {
                    try
                    {
                        using var fallbackReq = CreateRequest(HttpMethod.Patch, $"{table}?{filter}");
                        fallbackReq.Content = new StringContent(softDeleteJson, Encoding.UTF8, "application/json");
                        using var fbRes = await _http.SendAsync(fallbackReq);
                        patchOk = fbRes.IsSuccessStatusCode;
                    }
                    catch { }
                }

                // Fallback 2: updated_at/guncelleme_tarihi şemada yoksa (örn: stok_hareketler), sadece {"is_deleted":true} gönder
                if (!patchOk)
                {
                    try
                    {
                        string minJson = "{\"is_deleted\":true}";
                        using var minReq = CreateRequest(HttpMethod.Patch, $"{table}?{filterWithTenant}");
                        minReq.Content = new StringContent(minJson, Encoding.UTF8, "application/json");
                        using var minRes = await _http.SendAsync(minReq);
                        patchOk = minRes.IsSuccessStatusCode;
                        if (!patchOk)
                        {
                            using var minFbReq = CreateRequest(HttpMethod.Patch, $"{table}?{filter}");
                            minFbReq.Content = new StringContent(minJson, Encoding.UTF8, "application/json");
                            using var minFbRes = await _http.SendAsync(minFbReq);
                            patchOk = minFbRes.IsSuccessStatusCode;
                        }
                    }
                    catch { }
                }

                // Fallback 3: Doğrudan HTTP DELETE
                if (!patchOk)
                {
                    try
                    {
                        using var delReq = CreateRequest(HttpMethod.Delete, $"{table}?{filterWithTenant}");
                        using var delRes = await _http.SendAsync(delReq);
                        patchOk = delRes.IsSuccessStatusCode;
                        if (!patchOk)
                        {
                            using var delFbReq = CreateRequest(HttpMethod.Delete, $"{table}?{filter}");
                            using var delFbRes = await _http.SendAsync(delFbReq);
                            patchOk = delFbRes.IsSuccessStatusCode;
                        }
                    }
                    catch { }
                }

                // Fallback 4: UUID ile bulunamadıysa ve id > 0 ise id filtresiyle dene
                if (!patchOk && !string.IsNullOrEmpty(uuid) && id > 0)
                {
                    try
                    {
                        string idFilter = $"id=eq.{id}";
                        using var idReq = CreateRequest(HttpMethod.Patch, $"{table}?{idFilter}");
                        idReq.Content = new StringContent(softDeleteJson, Encoding.UTF8, "application/json");
                        using var idRes = await _http.SendAsync(idReq);
                        patchOk = idRes.IsSuccessStatusCode;
                    }
                    catch { }
                }

                return patchOk;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase Delete Error] {table}/{id}/{uuid}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteFilteredAsync(string table, string filter)
        {
            if (!IsConnected) return false;
            try
            {
                string finalFilter = BuildTenantFilter(table, filter) ?? filter;
                string nowStr = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                string softDeleteJson = $"{{\"is_deleted\":true,\"updated_at\":\"{nowStr}\",\"guncelleme_tarihi\":\"{nowStr}\"}}";

                bool patchOk = false;
                try
                {
                    using var patchReq = CreateRequest(HttpMethod.Patch, $"{table}?{finalFilter}");
                    patchReq.Content = new StringContent(softDeleteJson, Encoding.UTF8, "application/json");
                    using var patchRes = await _http.SendAsync(patchReq);
                    patchOk = patchRes.IsSuccessStatusCode;
                }
                catch { }

                // Fallback without tenant filter
                if (!patchOk && finalFilter != filter)
                {
                    try
                    {
                        using var fallbackReq = CreateRequest(HttpMethod.Patch, $"{table}?{filter}");
                        fallbackReq.Content = new StringContent(softDeleteJson, Encoding.UTF8, "application/json");
                        using var fbRes = await _http.SendAsync(fallbackReq);
                        patchOk = fbRes.IsSuccessStatusCode;
                    }
                    catch { }
                }

                // Fallback 2: updated_at/guncelleme_tarihi şemada yoksa sadece {"is_deleted":true} gönder
                if (!patchOk)
                {
                    try
                    {
                        string minJson = "{\"is_deleted\":true}";
                        using var minReq = CreateRequest(HttpMethod.Patch, $"{table}?{finalFilter}");
                        minReq.Content = new StringContent(minJson, Encoding.UTF8, "application/json");
                        using var minRes = await _http.SendAsync(minReq);
                        patchOk = minRes.IsSuccessStatusCode;
                        if (!patchOk && finalFilter != filter)
                        {
                            using var minFbReq = CreateRequest(HttpMethod.Patch, $"{table}?{filter}");
                            minFbReq.Content = new StringContent(minJson, Encoding.UTF8, "application/json");
                            using var minFbRes = await _http.SendAsync(minFbReq);
                            patchOk = minFbRes.IsSuccessStatusCode;
                        }
                    }
                    catch { }
                }

                // Fallback 3: Doğrudan HTTP DELETE
                if (!patchOk)
                {
                    try
                    {
                        using var delReq = CreateRequest(HttpMethod.Delete, $"{table}?{finalFilter}");
                        using var delRes = await _http.SendAsync(delReq);
                        patchOk = delRes.IsSuccessStatusCode;
                        if (!patchOk && finalFilter != filter)
                        {
                            using var delFbReq = CreateRequest(HttpMethod.Delete, $"{table}?{filter}");
                            using var delFbRes = await _http.SendAsync(delFbReq);
                            patchOk = delFbRes.IsSuccessStatusCode;
                        }
                    }
                    catch { }
                }

                return patchOk;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase DeleteFiltered Error] {table}?{filter}: {ex.Message}");
                return false;
            }
        }

        public async Task<JsonDocument?> GetJsonAsync(string table, string? filter = null)
        {
            if (!IsConnected) return null;
            try
            {
                // If caller explicitly provided limit or offset, execute single query
                if (filter != null && (filter.Contains("limit=") || filter.Contains("offset=")))
                {
                    string q = table + "?select=*";
                    string? f = BuildTenantFilter(table, filter);
                    if (!string.IsNullOrEmpty(f)) q += "&" + f;
                    using var singleReq = CreateRequest(HttpMethod.Get, q);
                    using var singleRes = await _http.SendAsync(singleReq);
                    if (!singleRes.IsSuccessStatusCode)
                    {
                        var err = await singleRes.Content.ReadAsStringAsync();
                        if ((err.Contains("PGRST204") || err.Contains("42703") || err.Contains("does not exist")) && err.Contains("tenant_id") && f != filter)
                        {
                            string fallbackQuery = table + "?select=*";
                            if (!string.IsNullOrEmpty(filter)) fallbackQuery += "&" + filter;
                            using var fallbackReq = CreateRequest(HttpMethod.Get, fallbackQuery);
                            using var fallbackRes = await _http.SendAsync(fallbackReq);
                            if (fallbackRes.IsSuccessStatusCode)
                            {
                                string fbJson = await fallbackRes.Content.ReadAsStringAsync();
                                return JsonDocument.Parse(fbJson);
                            }
                        }
                        return null;
                    }
                    string singleJson = await singleRes.Content.ReadAsStringAsync();
                    return JsonDocument.Parse(singleJson);
                }

                const int pageSize = 1000;
                int offset = 0;
                var rawItems = new List<string>();

                while (true)
                {
                    string query = table + $"?select=*&limit={pageSize}&offset={offset}";
                    string? finalFilter = BuildTenantFilter(table, filter);
                    if (!string.IsNullOrEmpty(finalFilter)) query += "&" + finalFilter;

                    using var req = CreateRequest(HttpMethod.Get, query);
                    req.Headers.TryAddWithoutValidation("Range-Unit", "items");
                    req.Headers.TryAddWithoutValidation("Range", $"items={offset}-{offset + pageSize - 1}");

                    using var res = await _http.SendAsync(req);
                    if (!res.IsSuccessStatusCode)
                    {
                        if (res.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable || (int)res.StatusCode == 416)
                        {
                            break;
                        }

                        var err = await res.Content.ReadAsStringAsync();
                        if ((err.Contains("PGRST204") || err.Contains("42703") || err.Contains("does not exist")) && err.Contains("tenant_id") && finalFilter != filter)
                        {
                            string fallbackQuery = table + $"?select=*&limit={pageSize}&offset={offset}";
                            if (!string.IsNullOrEmpty(filter)) fallbackQuery += "&" + filter;
                            using var fallbackReq = CreateRequest(HttpMethod.Get, fallbackQuery);
                            fallbackReq.Headers.TryAddWithoutValidation("Range-Unit", "items");
                            fallbackReq.Headers.TryAddWithoutValidation("Range", $"items={offset}-{offset + pageSize - 1}");
                            using var fallbackRes = await _http.SendAsync(fallbackReq);
                            if (fallbackRes.IsSuccessStatusCode)
                            {
                                string fbJson = await fallbackRes.Content.ReadAsStringAsync();
                                using var fbDoc = JsonDocument.Parse(fbJson);
                                if (fbDoc.RootElement.ValueKind == JsonValueKind.Array)
                                {
                                    int fbCount = 0;
                                    foreach (var el in fbDoc.RootElement.EnumerateArray())
                                    {
                                        rawItems.Add(el.GetRawText());
                                        fbCount++;
                                    }
                                    if (fbCount < pageSize || rawItems.Count >= 50000) break;
                                    offset += pageSize;
                                    continue;
                                }
                            }
                            else if (fallbackRes.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable || (int)fallbackRes.StatusCode == 416)
                            {
                                break;
                            }
                        }

                        if (offset > 0) break;
                        return null;
                    }

                    string json = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    {
                        return JsonDocument.Parse(json);
                    }

                    // Optimization: if first page has fewer than pageSize, return directly without re-parsing
                    if (offset == 0 && doc.RootElement.GetArrayLength() < pageSize)
                    {
                        return JsonDocument.Parse(json);
                    }

                    int count = 0;
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        rawItems.Add(el.GetRawText());
                        count++;
                    }

                    if (count < pageSize || rawItems.Count >= 50000) break;
                    offset += pageSize;
                }

                string combinedJson = "[" + string.Join(",", rawItems) + "]";
                return JsonDocument.Parse(combinedJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase GetJson Error] {table}: {ex.Message}");
                return null;
            }
        }

        public async Task<List<T>?> GetAllAsync<T>(string table, string? filter = null)
        {
            if (!IsConnected) return null;
            try
            {
                string query = table + "?select=*";
                string? finalFilter = BuildTenantFilter(table, filter);
                if (!string.IsNullOrEmpty(finalFilter)) query += "&" + finalFilter;
                using var req = CreateRequest(HttpMethod.Get, query);
                using var res = await _http.SendAsync(req);
                if (!res.IsSuccessStatusCode)
                {
                    var err = await res.Content.ReadAsStringAsync();
                    if (err.Contains("PGRST204") && err.Contains("tenant_id") && finalFilter != filter)
                    {
                        string fallbackQuery = table + "?select=*";
                        if (!string.IsNullOrEmpty(filter)) fallbackQuery += "&" + filter;
                        using var fallbackReq = CreateRequest(HttpMethod.Get, fallbackQuery);
                        using var fallbackRes = await _http.SendAsync(fallbackReq);
                        if (fallbackRes.IsSuccessStatusCode)
                        {
                            string fbJson = await fallbackRes.Content.ReadAsStringAsync();
                            return JsonSerializer.Deserialize<List<T>>(fbJson, _jsonOpts);
                        }
                    }
                    return null;
                }
                string json = await res.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<T>>(json, _jsonOpts);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase GetAll Error] {table}: {ex.Message}");
                return null;
            }
        }

        // ==========================================
        // SAFE JSON TYPE PARSERS (PostgreSQL NUMERIC/BIGINT tolerant)
        // ==========================================
        private static int ParseInt(JsonElement el, int defaultVal = 0)
        {
            if (el.ValueKind == JsonValueKind.Number)
            {
                if (el.TryGetInt32(out var i)) return i;
                if (el.TryGetInt64(out var l)) return (int)l;
                if (el.TryGetDouble(out var d)) return (int)Math.Round(d);
                if (el.TryGetDecimal(out var dec)) return (int)Math.Round(dec);
            }
            else if (el.ValueKind == JsonValueKind.String)
            {
                var str = el.GetString();
                if (int.TryParse(str, out var parsed)) return parsed;
                if (double.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pd)) return (int)Math.Round(pd);
            }
            return defaultVal;
        }

        private static int? ParseNullableInt(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Null || el.ValueKind == JsonValueKind.Undefined) return null;
            return ParseInt(el);
        }

        private static decimal ParseDecimal(JsonElement el, decimal defaultVal = 0m)
        {
            if (el.ValueKind == JsonValueKind.Number)
            {
                if (el.TryGetDecimal(out var dec)) return dec;
                if (el.TryGetDouble(out var d)) return (decimal)d;
            }
            else if (el.ValueKind == JsonValueKind.String)
            {
                var str = el.GetString();
                if (decimal.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var dec)) return dec;
            }
            return defaultVal;
        }

        private static double ParseDouble(JsonElement el, double defaultVal = 0.0)
        {
            if (el.ValueKind == JsonValueKind.Number)
            {
                if (el.TryGetDouble(out var d)) return d;
                if (el.TryGetDecimal(out var dec)) return (double)dec;
            }
            else if (el.ValueKind == JsonValueKind.String)
            {
                var str = el.GetString();
                if (double.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
            }
            return defaultVal;
        }

        // ==========================================
        // DTO MAPPERS (EXACT SUPABASE COLUMN SCHEMAS)
        // ==========================================
        private static Dictionary<string, object?> MapCariToPayload(CariKart c) => new()
        {
            ["id"] = c.Id,
            ["kod"] = c.CariKod ?? "",
            ["cari_kodu"] = c.CariKod ?? "",
            ["unvan"] = string.IsNullOrWhiteSpace(c.Unvan) ? (!string.IsNullOrWhiteSpace(c.CariKod) ? c.CariKod : $"Cari-{c.Id}") : c.Unvan,
            ["yetkili_kisi"] = c.Yetkili ?? "",
            ["tc_kimlik_no"] = c.TCNo ?? "",
            ["bakiye"] = c.Bakiye,
            ["borc_tutari"] = c.Borc,
            ["alacak_tutari"] = c.Alacak,
            ["borc"] = c.Borc,
            ["alacak"] = c.Alacak,
            ["devir_borc"] = c.DevirBorc,
            ["devir_alacak"] = c.DevirAlacak,
            ["vergi_dairesi"] = c.VergiDairesi,
            ["vergi_no"] = c.VergiNo,
            ["telefon"] = string.IsNullOrWhiteSpace(c.Telefon) ? c.CepTelefon : c.Telefon,
            ["telefon2"] = c.CepTelefon,
            ["gsm"] = c.CepTelefon,
            ["eposta"] = c.Email,
            ["email"] = c.Email,
            ["adres"] = c.Adres,
            ["sevk_adresi"] = c.SevkAdresi ?? "",
            ["il"] = c.Il,
            ["sehir"] = c.Il,
            ["ilce"] = c.Ilce,
            ["web_sitesi"] = c.WebAdresi,
            ["grup"] = c.Grup ?? "Müşteri",
            ["cari_turu"] = c.Tur ?? c.Grup ?? "Müşteri",
            ["odeme_plani"] = c.OdemePlani ?? "",
            ["ticaret_sicil_no"] = c.TicaretSicilNo ?? "",
            ["iban"] = c.IBAN ?? "",
            ["vade_gun"] = c.VadeGunu,
            ["kredi_limiti"] = c.RiskLimiti,
            ["notlar"] = c.Aciklama,
            ["is_active"] = c.AktifMi,
            ["aktif"] = c.AktifMi,
            ["bakiye_turu"] = c.Bakiye >= 0 ? "B" : "A",
            ["is_deleted"] = c.IsDeleted,
            ["deleted_at"] = c.IsDeleted ? (DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")) : null,
            ["device_id"] = $"{Environment.MachineName}-PC",
            ["mali_yil"] = DateTime.UtcNow.Year,
            ["uuid"] = string.IsNullOrEmpty(c.Uuid) ? (c.Uuid = Guid.NewGuid().ToString()) : c.Uuid,
            ["version"] = c.Version <= 0 ? 1 : c.Version,
            ["updated_at"] = FormatUtcIso(c.UpdatedAt),
            ["guncelleme_tarihi"] = FormatUtcIso(c.UpdatedAt)
        };

        public static string FormatUtcIso(DateTime dt)
        {
            if (dt == default) return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            var utc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
            return utc.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static CariKart MapPayloadToCari(JsonElement el)
        {
            var c = new CariKart();
            if (el.TryGetProperty("id", out var id)) c.Id = ParseInt(id);
            if (el.TryGetProperty("kod", out var k) && k.ValueKind == JsonValueKind.String) c.CariKod = k.GetString();
            else if (el.TryGetProperty("cari_kodu", out var ck) && ck.ValueKind == JsonValueKind.String) c.CariKod = ck.GetString();
            else if (el.TryGetProperty("cariKod", out var ckCamel) && ckCamel.ValueKind == JsonValueKind.String) c.CariKod = ckCamel.GetString();

            if (el.TryGetProperty("unvan", out var u) && u.ValueKind == JsonValueKind.String) c.Unvan = u.GetString();
            if (el.TryGetProperty("yetkili_kisi", out var yk) && yk.ValueKind == JsonValueKind.String) c.Yetkili = yk.GetString();
            else if (el.TryGetProperty("yetkili", out var yt) && yt.ValueKind == JsonValueKind.String) c.Yetkili = yt.GetString();

            if (el.TryGetProperty("tc_kimlik_no", out var tc) && tc.ValueKind == JsonValueKind.String) c.TCNo = tc.GetString();
            else if (el.TryGetProperty("tc_no", out var tcn) && tcn.ValueKind == JsonValueKind.String) c.TCNo = tcn.GetString();

            if (el.TryGetProperty("vergi_dairesi", out var vd) && vd.ValueKind == JsonValueKind.String) c.VergiDairesi = vd.GetString();
            else if (el.TryGetProperty("vergiDairesi", out var vdCamel) && vdCamel.ValueKind == JsonValueKind.String) c.VergiDairesi = vdCamel.GetString();

            if (el.TryGetProperty("vergi_no", out var vn) && vn.ValueKind == JsonValueKind.String) c.VergiNo = vn.GetString();
            else if (el.TryGetProperty("vergiNo", out var vnCamel) && vnCamel.ValueKind == JsonValueKind.String) c.VergiNo = vnCamel.GetString();

            if (el.TryGetProperty("adres", out var adr) && adr.ValueKind == JsonValueKind.String) c.Adres = adr.GetString();
            if (el.TryGetProperty("sevk_adresi", out var sa) && sa.ValueKind == JsonValueKind.String) c.SevkAdresi = sa.GetString();
            if (el.TryGetProperty("il", out var il) && il.ValueKind == JsonValueKind.String) c.Il = il.GetString();
            else if (el.TryGetProperty("sehir", out var sh) && sh.ValueKind == JsonValueKind.String) c.Il = sh.GetString();
            if (el.TryGetProperty("ilce", out var ilc) && ilc.ValueKind == JsonValueKind.String) c.Ilce = ilc.GetString();
            if (el.TryGetProperty("posta_kodu", out var pk) && pk.ValueKind == JsonValueKind.String) c.PostaKodu = pk.GetString();
            if (el.TryGetProperty("ulke", out var ulk) && ulk.ValueKind == JsonValueKind.String) c.Ulke = ulk.GetString();

            if (el.TryGetProperty("telefon", out var tel) && tel.ValueKind == JsonValueKind.String) c.Telefon = tel.GetString();
            if (el.TryGetProperty("telefon2", out var tel2) && tel2.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(tel2.GetString())) 
                c.CepTelefon = tel2.GetString();
            else if (el.TryGetProperty("gsm", out var gsm) && gsm.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(gsm.GetString())) 
                c.CepTelefon = gsm.GetString();
            else if (el.TryGetProperty("cep_telefon", out var ctel) && ctel.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(ctel.GetString())) 
                c.CepTelefon = ctel.GetString();
            else if (el.TryGetProperty("cepTelefon", out var ctel2) && ctel2.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(ctel2.GetString())) 
                c.CepTelefon = ctel2.GetString();

            if (el.TryGetProperty("eposta", out var ep) && ep.ValueKind == JsonValueKind.String) c.Email = ep.GetString();
            else if (el.TryGetProperty("email", out var em) && em.ValueKind == JsonValueKind.String) c.Email = em.GetString();

            if (el.TryGetProperty("web_sitesi", out var ws) && ws.ValueKind == JsonValueKind.String) c.WebAdresi = ws.GetString();
            if (el.TryGetProperty("grup", out var grp) && grp.ValueKind == JsonValueKind.String) c.Grup = grp.GetString();
            if (el.TryGetProperty("tur", out var tur) && tur.ValueKind == JsonValueKind.String) c.Tur = tur.GetString();
            if (el.TryGetProperty("iban", out var ibn) && ibn.ValueKind == JsonValueKind.String) c.IBAN = ibn.GetString();
            if (el.TryGetProperty("odeme_plani", out var op) && op.ValueKind == JsonValueKind.String) c.OdemePlani = op.GetString();
            if (el.TryGetProperty("ticaret_sicil_no", out var tsn) && tsn.ValueKind == JsonValueKind.String) c.TicaretSicilNo = tsn.GetString();
            if (el.TryGetProperty("aciklama", out var ack) && ack.ValueKind == JsonValueKind.String) c.Aciklama = ack.GetString();
            else if (el.TryGetProperty("notlar", out var ntr) && ntr.ValueKind == JsonValueKind.String) c.Aciklama = ntr.GetString();

            if (el.TryGetProperty("vade_gun", out var vg)) c.VadeGunu = ParseInt(vg);
            else if (el.TryGetProperty("vade_gunu", out var vgu)) c.VadeGunu = ParseInt(vgu);

            if (el.TryGetProperty("kredi_limiti", out var kl)) c.RiskLimiti = ParseDecimal(kl);
            else if (el.TryGetProperty("risk_limiti", out var rl)) c.RiskLimiti = ParseDecimal(rl);

            if (el.TryGetProperty("borc_tutari", out var bq1)) c.Borc = ParseDecimal(bq1);
            else if (el.TryGetProperty("borc", out var bq2)) c.Borc = ParseDecimal(bq2);

            if (el.TryGetProperty("alacak_tutari", out var aq1)) c.Alacak = ParseDecimal(aq1);
            else if (el.TryGetProperty("alacak", out var aq2)) c.Alacak = ParseDecimal(aq2);

            if (el.TryGetProperty("devir_borc", out var db1)) c.DevirBorc = ParseDecimal(db1);
            else if (el.TryGetProperty("devirBorc", out var db2)) c.DevirBorc = ParseDecimal(db2);

            if (el.TryGetProperty("devir_alacak", out var da1)) c.DevirAlacak = ParseDecimal(da1);
            else if (el.TryGetProperty("devirAlacak", out var da2)) c.DevirAlacak = ParseDecimal(da2);

            if (el.TryGetProperty("is_active", out var isAct) && isAct.ValueKind != JsonValueKind.Null) 
                c.AktifMi = isAct.ValueKind != JsonValueKind.False;
            else if (el.TryGetProperty("aktif", out var akt) && akt.ValueKind != JsonValueKind.Null) 
                c.AktifMi = akt.ValueKind != JsonValueKind.False;
            else if (el.TryGetProperty("aktif_mi", out var aktMi) && aktMi.ValueKind != JsonValueKind.Null) 
                c.AktifMi = aktMi.ValueKind != JsonValueKind.False;
            else if (el.TryGetProperty("aktifMi", out var aktMiCamel) && aktMiCamel.ValueKind != JsonValueKind.Null) 
                c.AktifMi = aktMiCamel.ValueKind != JsonValueKind.False;
            if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) c.IsDeleted = true;
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) c.UpdatedAt = dtUa;
            else if (el.TryGetProperty("guncelleme_tarihi", out var gt) && gt.ValueKind == JsonValueKind.String && DateTime.TryParse(gt.GetString(), out var dtGt)) c.UpdatedAt = dtGt;
            if (el.TryGetProperty("version", out var vr)) c.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) c.Uuid = uu.GetString()!;
            else c.Uuid = c.Id > 0 ? $"legacy-cari-{c.Id}" : "";
            return c;
        }

        private static Dictionary<string, object?> MapStokToPayload(StokKart s) => new()
        {
            ["id"] = s.Id,
            ["stok_kodu"] = s.StokKodu ?? "",
            ["stok_adi"] = string.IsNullOrWhiteSpace(s.StokAdi) ? (!string.IsNullOrWhiteSpace(s.StokKodu) ? s.StokKodu : $"Stok-{s.Id}") : s.StokAdi,
            ["barkod"] = s.Barkod ?? "",
            ["birim"] = s.Birim ?? "Adet",
            ["kdv_orani"] = s.KDV,
            ["alis_fiyati"] = s.AlisFiyati,
            ["satis_fiyati"] = s.SatisFiyati,
            ["ortalama_alis_fiyati"] = s.OrtalamaAlisFiyati,
            ["ortalama_satis_fiyati"] = s.OrtalamaSatisFiyati,
            ["mevcut_miktar"] = (decimal)s.Miktar,
            ["miktar"] = (decimal)s.Miktar,
            ["kritik_seviye"] = (decimal)s.MinSeviye,
            ["kritik_stok"] = (decimal)s.MinSeviye,
            ["grup_adi"] = s.Kategori ?? s.Grup ?? "",
            ["grup"] = s.Kategori ?? s.Grup ?? "",
            ["aciklama"] = s.Aciklama ?? "",
            ["resim_url"] = "",
            ["is_active"] = true,
            ["is_deleted"] = s.IsDeleted,
            ["uuid"] = string.IsNullOrEmpty(s.Uuid) ? (s.Uuid = Guid.NewGuid().ToString()) : s.Uuid,
            ["version"] = s.Version <= 0 ? 1 : s.Version,
            ["updated_at"] = FormatUtcIso(s.UpdatedAt),
            ["guncelleme_tarihi"] = FormatUtcIso(s.UpdatedAt)
        };

        private static StokKart MapPayloadToStok(JsonElement el)
        {
            var s = new StokKart();
            if (el.TryGetProperty("id", out var id)) s.Id = ParseInt(id);
            if (el.TryGetProperty("stok_kodu", out var sk) && sk.ValueKind == JsonValueKind.String) s.StokKodu = sk.GetString();
            if (el.TryGetProperty("stok_adi", out var sa) && sa.ValueKind == JsonValueKind.String) s.StokAdi = sa.GetString();
            if (el.TryGetProperty("barkod", out var bk) && bk.ValueKind == JsonValueKind.String) s.Barkod = bk.GetString();
            if (el.TryGetProperty("grup", out var grp) && grp.ValueKind == JsonValueKind.String) s.Kategori = grp.GetString();
            else if (el.TryGetProperty("kategori", out var kat) && kat.ValueKind == JsonValueKind.String) s.Kategori = kat.GetString();
            else if (el.TryGetProperty("grup_adi", out var ga) && ga.ValueKind == JsonValueKind.String) s.Kategori = ga.GetString();
            if (el.TryGetProperty("birim", out var br) && br.ValueKind == JsonValueKind.String) s.Birim = br.GetString() ?? "Adet";
            if (el.TryGetProperty("alis_fiyati", out var af)) s.AlisFiyati = ParseDecimal(af);
            if (el.TryGetProperty("satis_fiyati", out var sf)) s.SatisFiyati = ParseDecimal(sf);
            if (el.TryGetProperty("ortalama_alis_fiyati", out var oaf)) s.OrtalamaAlisFiyati = ParseDecimal(oaf);
            if (el.TryGetProperty("ortalama_satis_fiyati", out var osf)) s.OrtalamaSatisFiyati = ParseDecimal(osf);
            if (el.TryGetProperty("kdv_orani", out var ko)) s.KDV = ParseInt(ko, 20);
            if (el.TryGetProperty("mevcut_miktar", out var mm)) s.Miktar = ParseDecimal(mm);
            else if (el.TryGetProperty("miktar", out var mq)) s.Miktar = ParseDecimal(mq);
            if (el.TryGetProperty("kritik_stok", out var kst)) s.MinSeviye = ParseDecimal(kst);
            else if (el.TryGetProperty("kritik_seviye", out var ks)) s.MinSeviye = ParseDecimal(ks);
            if (el.TryGetProperty("aciklama", out var ack) && ack.ValueKind == JsonValueKind.String) s.Aciklama = ack.GetString();
            if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) s.IsDeleted = true;
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) s.UpdatedAt = dtUa;
            if (el.TryGetProperty("version", out var vr)) s.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) s.Uuid = uu.GetString()!;
            else s.Uuid = s.Id > 0 ? $"legacy-stok-{s.Id}" : "";
            return s;
        }

        private static Dictionary<string, object?> MapFaturaToPayload(Fatura f) => new()
        {
            ["id"] = f.Id,
            ["fatura_no"] = string.IsNullOrWhiteSpace(f.FaturaNo) ? $"FTR-{f.Id}" : f.FaturaNo,
            ["fatura_turu"] = string.IsNullOrWhiteSpace(f.Tur) ? "Satis" : f.Tur,
            ["tur"] = string.IsNullOrWhiteSpace(f.Tur) ? "Satis" : f.Tur,
            ["odeme_sekli"] = f.OdemeSekli ?? "Açık Hesap",
            ["odenen"] = f.Odenen,
            ["kalan_tutar"] = f.Kalan,
            ["doviz_turu"] = f.DovizTuru ?? "TRY",
            ["doviz_kuru"] = f.DovizKuru <= 0 ? 1m : f.DovizKuru,
            ["vergi_dairesi"] = f.VergiDairesi ?? "",
            ["vergi_no"] = f.VergiNo ?? "",
            ["adres"] = f.Adres ?? "",
            ["baglanti_evrak_no"] = f.BaglantiEvrakNo ?? "",
            ["is_earsiv"] = f.IsEArsiv,
            ["iptal_mi"] = f.IptalMi,
            ["cari_id"] = f.CariId > 0 ? f.CariId : null,
            ["cari_uuid"] = !string.IsNullOrEmpty(f.CariUuid) ? f.CariUuid : null,
            ["cari_unvan"] = f.CariUnvan ?? "",
            ["mali_yil"] = f.Tarih != default ? f.Tarih.Year : DateTime.UtcNow.Year,
            ["tarih"] = FormatUtcIso(f.Tarih),
            ["vade_tarihi"] = FormatUtcIso(f.VadeTarihi),
            ["ara_toplam"] = f.AraToplam,
            ["kdv_toplam"] = f.KdvToplam,
            ["iskonto_toplam"] = f.IskontoToplam,
            ["genel_toplam"] = f.GenelToplam,
            ["aciklama"] = f.Aciklama ?? "",
            ["is_kapali"] = f.IptalMi ? false : (f.Kalan <= 0),
            ["kasa_id"] = f.KasaId,
            ["banka_id"] = f.BankaId,
            ["is_deleted"] = f.IsDeleted || f.IptalMi,
            ["uuid"] = string.IsNullOrEmpty(f.Uuid) ? (f.Uuid = Guid.NewGuid().ToString()) : f.Uuid,
            ["version"] = f.Version <= 0 ? 1 : f.Version,
            ["updated_at"] = FormatUtcIso(f.UpdatedAt),
            ["guncelleme_tarihi"] = FormatUtcIso(f.UpdatedAt)
        };

        private static Fatura MapPayloadToFatura(JsonElement el)
        {
            var f = new Fatura();
            if (el.TryGetProperty("id", out var id)) f.Id = ParseInt(id);
            if (el.TryGetProperty("fatura_no", out var fn) && fn.ValueKind == JsonValueKind.String) f.FaturaNo = fn.GetString();
            if (el.TryGetProperty("fatura_turu", out var ft) && ft.ValueKind == JsonValueKind.String) f.Tur = ft.GetString();
            else if (el.TryGetProperty("tur", out var tr) && tr.ValueKind == JsonValueKind.String) f.Tur = tr.GetString();
            if (el.TryGetProperty("cari_id", out var ci)) f.CariId = ParseInt(ci);
            if (el.TryGetProperty("cari_uuid", out var cuu) && cuu.ValueKind == JsonValueKind.String) f.CariUuid = cuu.GetString();
            if (el.TryGetProperty("cari_unvan", out var cu) && cu.ValueKind == JsonValueKind.String) f.CariUnvan = cu.GetString();
            if (el.TryGetProperty("tarih", out var trh) && trh.ValueKind == JsonValueKind.String && DateTime.TryParse(trh.GetString(), out var t)) f.Tarih = t;
            if (el.TryGetProperty("vade_tarihi", out var vt) && vt.ValueKind == JsonValueKind.String && DateTime.TryParse(vt.GetString(), out var vd)) f.VadeTarihi = vd;
            if (el.TryGetProperty("ara_toplam", out var at)) f.AraToplam = ParseDecimal(at);
            if (el.TryGetProperty("kdv_toplam", out var kt)) f.KdvToplam = ParseDecimal(kt);
            if (el.TryGetProperty("iskonto_toplam", out var itp)) f.IskontoToplam = ParseDecimal(itp);
            if (el.TryGetProperty("genel_toplam", out var gt)) f.GenelToplam = ParseDecimal(gt);
            if (el.TryGetProperty("odenen", out var od)) f.Odenen = ParseDecimal(od);
            else if (el.TryGetProperty("odenen_tutar", out var odt)) f.Odenen = ParseDecimal(odt);
            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) f.Aciklama = ac.GetString();
            if (el.TryGetProperty("doviz_turu", out var dt) && dt.ValueKind == JsonValueKind.String) f.DovizTuru = dt.GetString();
            if (el.TryGetProperty("doviz_kuru", out var dk)) f.DovizKuru = ParseDecimal(dk);
            if (el.TryGetProperty("odeme_sekli", out var os) && os.ValueKind == JsonValueKind.String) f.OdemeSekli = os.GetString();
            if (el.TryGetProperty("is_earsiv", out var iea)) f.IsEArsiv = iea.ValueKind == JsonValueKind.True;
            if (el.TryGetProperty("vergi_dairesi", out var fvd) && fvd.ValueKind == JsonValueKind.String) f.VergiDairesi = fvd.GetString();
            if (el.TryGetProperty("vergi_no", out var fvn) && fvn.ValueKind == JsonValueKind.String) f.VergiNo = fvn.GetString();
            if (el.TryGetProperty("adres", out var fadr) && fadr.ValueKind == JsonValueKind.String) f.Adres = fadr.GetString();
            if (el.TryGetProperty("baglanti_evrak_no", out var ben) && ben.ValueKind == JsonValueKind.String) f.BaglantiEvrakNo = ben.GetString();
            if (el.TryGetProperty("iptal_mi", out var im)) f.IptalMi = im.ValueKind == JsonValueKind.True;
            if (el.TryGetProperty("kasa_id", out var ki)) f.KasaId = ParseNullableInt(ki);
            if (el.TryGetProperty("banka_id", out var bi)) f.BankaId = ParseNullableInt(bi);
            if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) f.IsDeleted = true;
            if (el.TryGetProperty("guncelleme_tarihi", out var gTrh) && gTrh.ValueKind == JsonValueKind.String && DateTime.TryParse(gTrh.GetString(), out var dtGt)) f.UpdatedAt = dtGt;
            else if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) f.UpdatedAt = dtUa;
            if (el.TryGetProperty("version", out var vr)) f.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("mali_yil", out var my)) f.MaliYil = ParseInt(my);
            else f.MaliYil = f.Tarih.Year;
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) f.Uuid = uu.GetString()!;
            else f.Uuid = f.Id > 0 ? $"legacy-fatura-{f.Id}" : "";
            return f;
        }

        private static Dictionary<string, object?> MapFaturaDetayToPayload(FaturaDetay d) => new()
        {
            ["id"] = d.Id,
            ["fatura_id"] = d.FaturaId > 0 ? d.FaturaId : null,
            ["fatura_uuid"] = !string.IsNullOrEmpty(d.FaturaUuid) ? d.FaturaUuid : null,
            ["stok_id"] = d.StokId > 0 ? d.StokId : null,
            ["stok_uuid"] = !string.IsNullOrEmpty(d.StokUuid) ? d.StokUuid : null,
            ["stok_kodu"] = d.StokKodu ?? "",
            ["stok_adi"] = d.StokAdi ?? "",
            ["birim"] = d.Birim ?? "Adet",
            ["miktar"] = (decimal)d.Miktar,
            ["birim_fiyat"] = d.BirimFiyat,
            ["kdv_orani"] = (decimal)d.KDVOrani,
            ["kdv_tutari"] = d.KdvTutari,
            ["toplam_tutar"] = d.ToplamTutar,
            ["net_tutar"] = d.ToplamTutar,
            ["iskonto_orani"] = d.IskontoOrani,
            ["iskonto_tutari"] = d.IskontoTutari,
            ["aciklama"] = d.Aciklama ?? "",
            ["is_deleted"] = false,
            ["uuid"] = string.IsNullOrEmpty(d.Uuid) ? (d.Uuid = Guid.NewGuid().ToString()) : d.Uuid,
            ["version"] = d.Version <= 0 ? 1 : d.Version,
            ["updated_at"] = FormatUtcIso(d.UpdatedAt)
        };

        private static FaturaDetay MapPayloadToFaturaDetay(JsonElement el)
        {
            var d = new FaturaDetay();
            if (el.TryGetProperty("id", out var id)) d.Id = ParseInt(id);
            if (el.TryGetProperty("fatura_id", out var fi)) d.FaturaId = ParseInt(fi);
            if (el.TryGetProperty("fatura_uuid", out var fu) && fu.ValueKind == JsonValueKind.String) d.FaturaUuid = fu.GetString();
            if (el.TryGetProperty("stok_id", out var si)) d.StokId = ParseInt(si);
            if (el.TryGetProperty("stok_uuid", out var su) && su.ValueKind == JsonValueKind.String) d.StokUuid = su.GetString();
            if (el.TryGetProperty("stok_kodu", out var sk) && sk.ValueKind == JsonValueKind.String) d.StokKodu = sk.GetString();
            if (el.TryGetProperty("stok_adi", out var sa) && sa.ValueKind == JsonValueKind.String) d.StokAdi = sa.GetString();
            if (el.TryGetProperty("miktar", out var mq)) d.Miktar = ParseDecimal(mq);
            if (el.TryGetProperty("birim", out var br) && br.ValueKind == JsonValueKind.String) d.Birim = br.GetString();
            if (el.TryGetProperty("birim_fiyat", out var bf)) d.BirimFiyat = ParseDecimal(bf);
            if (el.TryGetProperty("kdv_orani", out var ko)) d.KDVOrani = ParseInt(ko);
            if (el.TryGetProperty("kdv_tutari", out var kt)) d.KdvTutari = ParseDecimal(kt);
            if (el.TryGetProperty("toplam_tutar", out var tt)) d.ToplamTutar = ParseDecimal(tt);
            else if (el.TryGetProperty("net_tutar", out var nt)) d.ToplamTutar = ParseDecimal(nt);
            else if (el.TryGetProperty("tutar", out var tr)) d.ToplamTutar = ParseDecimal(tr);
            if (el.TryGetProperty("iskonto_orani", out var io)) d.IskontoOrani = ParseDecimal(io);
            if (el.TryGetProperty("iskonto_tutari", out var it)) d.IskontoTutari = ParseDecimal(it);
            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) d.Aciklama = ac.GetString();
            if (el.TryGetProperty("is_deleted", out var isDel)) d.IsDeleted = isDel.ValueKind == JsonValueKind.True;
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) d.Uuid = uu.GetString()!;
            else d.Uuid = d.Id > 0 ? $"legacy-faturadetay-{d.Id}" : "";
            if (el.TryGetProperty("version", out var vr)) d.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) d.UpdatedAt = dtUa;
            return d;
        }

        private static Dictionary<string, object?> MapCariHareketToPayload(CariHareket h) => new()
        {
            ["id"] = h.Id,
            ["cari_id"] = h.CariId > 0 ? h.CariId : null,
            ["cari_uuid"] = !string.IsNullOrEmpty(h.CariUuid) ? h.CariUuid : null,
            ["cari_unvan"] = h.CariUnvan ?? "",
            ["evrak_no"] = h.EvrakNo ?? "",
            ["islem_turu"] = h.IslemTuru ?? "",
            ["borc"] = h.Borc,
            ["alacak"] = h.Alacak,
            ["bakiye"] = h.KalanBakiye,
            ["aciklama"] = h.Aciklama ?? "",
            ["fatura_id"] = h.FaturaId > 0 ? h.FaturaId : null,
            ["fatura_uuid"] = !string.IsNullOrEmpty(h.FaturaUuid) ? h.FaturaUuid : null,
            ["ref_id"] = h.RefId ?? "",
            ["yonlendirilen_cari_id"] = h.YonlendirilenCariId > 0 ? h.YonlendirilenCariId : null,
            ["yonlendirilen_cari_uuid"] = !string.IsNullOrEmpty(h.YonlendirilenCariUuid) ? h.YonlendirilenCariUuid : null,
            ["yonlendirilen_cari_unvan"] = h.YonlendirilenCariUnvan ?? "",
            ["tarih"] = FormatUtcIso(h.Tarih),
            ["is_deleted"] = h.IsDeleted,
            ["uuid"] = string.IsNullOrEmpty(h.Uuid) ? (h.Uuid = Guid.NewGuid().ToString()) : h.Uuid,
            ["version"] = h.Version <= 0 ? 1 : h.Version,
            ["updated_at"] = FormatUtcIso(h.UpdatedAt)
        };

        private static CariHareket? MapPayloadToCariHareket(JsonElement el)
        {
            var h = new CariHareket();
            if (el.TryGetProperty("id", out var id)) h.Id = ParseInt(id);
            if (el.TryGetProperty("cari_id", out var ci)) h.CariId = ParseInt(ci);
            if (el.TryGetProperty("cari_uuid", out var cuu) && cuu.ValueKind == JsonValueKind.String) h.CariUuid = cuu.GetString();
            if (el.TryGetProperty("cari_unvan", out var cu) && cu.ValueKind == JsonValueKind.String) h.CariUnvan = cu.GetString();
            if (el.TryGetProperty("tarih", out var trh) && trh.ValueKind == JsonValueKind.String && DateTime.TryParse(trh.GetString(), out var t)) h.Tarih = t;
            if (el.TryGetProperty("islem_turu", out var it) && it.ValueKind == JsonValueKind.String) h.IslemTuru = it.GetString();
            if (el.TryGetProperty("evrak_no", out var en) && en.ValueKind == JsonValueKind.String) h.EvrakNo = en.GetString();
            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) h.Aciklama = ac.GetString();
            if (el.TryGetProperty("borc", out var b)) h.Borc = ParseDecimal(b);
            if (el.TryGetProperty("alacak", out var a)) h.Alacak = ParseDecimal(a);
            if (el.TryGetProperty("bakiye", out var bq)) h.KalanBakiye = ParseDecimal(bq);
            if (el.TryGetProperty("fatura_id", out var fi)) h.FaturaId = ParseNullableInt(fi);
            if (el.TryGetProperty("fatura_uuid", out var fuu) && fuu.ValueKind == JsonValueKind.String) h.FaturaUuid = fuu.GetString();
            if (el.TryGetProperty("ref_id", out var rf) && rf.ValueKind == JsonValueKind.String) h.RefId = rf.GetString();
            if (el.TryGetProperty("yonlendirilen_cari_id", out var yci)) h.YonlendirilenCariId = ParseNullableInt(yci);
            if (el.TryGetProperty("yonlendirilen_cari_uuid", out var ycuu) && ycuu.ValueKind == JsonValueKind.String) h.YonlendirilenCariUuid = ycuu.GetString();
            if (el.TryGetProperty("yonlendirilen_cari_unvan", out var ycu) && ycu.ValueKind == JsonValueKind.String) h.YonlendirilenCariUnvan = ycu.GetString();
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) h.Uuid = uu.GetString()!;
            else h.Uuid = h.Id > 0 ? $"legacy-carihareket-{h.Id}" : "";
            if (el.TryGetProperty("version", out var vr)) h.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) h.UpdatedAt = dtUa;
            if (el.TryGetProperty("is_deleted", out var isDel))
                h.IsDeleted = isDel.ValueKind == JsonValueKind.True || (isDel.ValueKind == JsonValueKind.Number && isDel.GetInt32() == 1);
            return h;
        }

        private static Dictionary<string, object?> MapStokHareketToPayload(StokHareket sh)
        {
            decimal miktar = sh.Miktar != 0 ? sh.Miktar : (sh.Giren > 0 ? sh.Giren : sh.Cikan);
            decimal toplam = miktar * sh.Fiyat;
            string tur = sh.IslemTuru ?? sh.EvrakTuru ?? (sh.Giren > 0 ? "Giriş" : "Çıkış");
            return new()
            {
                ["id"] = sh.Id,
                ["stok_id"] = sh.StokId > 0 ? sh.StokId : null,
                ["stok_uuid"] = !string.IsNullOrEmpty(sh.StokUuid) ? sh.StokUuid : null,
                ["evrak_no"] = sh.EvrakNo ?? "",
                ["hareket_tipi"] = tur,
                ["miktar"] = miktar,
                ["giren"] = sh.Giren,
                ["cikan"] = sh.Cikan,
                ["birim_fiyat"] = sh.Fiyat,
                ["fiyat"] = sh.Fiyat,
                ["kdv_orani"] = 0m,
                ["kdv_tutari"] = 0m,
                ["toplam_tutar"] = toplam,
                ["aciklama"] = sh.Aciklama ?? "",
                ["fatura_id"] = sh.FaturaId > 0 ? sh.FaturaId : null,
                ["fatura_uuid"] = !string.IsNullOrEmpty(sh.FaturaUuid) ? sh.FaturaUuid : null,
                ["tarih"] = FormatUtcIso(sh.Tarih),
                ["is_deleted"] = sh.IsDeleted,
                ["uuid"] = string.IsNullOrEmpty(sh.Uuid) ? (sh.Uuid = Guid.NewGuid().ToString()) : sh.Uuid,
                ["version"] = sh.Version <= 0 ? 1 : sh.Version,
                ["updated_at"] = FormatUtcIso(sh.UpdatedAt)
            };
        }

        private static StokHareket? MapPayloadToStokHareket(JsonElement el)
        {
            var sh = new StokHareket();
            if (el.TryGetProperty("id", out var id)) sh.Id = ParseInt(id);
            if (el.TryGetProperty("stok_id", out var si)) sh.StokId = ParseInt(si);
            if (el.TryGetProperty("stok_uuid", out var su) && su.ValueKind == JsonValueKind.String) sh.StokUuid = su.GetString();
            if (el.TryGetProperty("tarih", out var trh) && trh.ValueKind == JsonValueKind.String && DateTime.TryParse(trh.GetString(), out var t)) sh.Tarih = t;
            if (el.TryGetProperty("islem_turu", out var it) && it.ValueKind == JsonValueKind.String) sh.IslemTuru = it.GetString();
            else if (el.TryGetProperty("hareket_turu", out var ht) && ht.ValueKind == JsonValueKind.String) sh.IslemTuru = ht.GetString();
            else if (el.TryGetProperty("hareket_tipi", out var hti) && hti.ValueKind == JsonValueKind.String) sh.IslemTuru = hti.GetString();
            if (el.TryGetProperty("evrak_no", out var en) && en.ValueKind == JsonValueKind.String) sh.EvrakNo = en.GetString();
            if (el.TryGetProperty("giren", out var grn)) sh.Giren = ParseDecimal(grn);
            if (el.TryGetProperty("cikan", out var ckn)) sh.Cikan = ParseDecimal(ckn);
            if (el.TryGetProperty("miktar", out var m))
            {
                sh.Miktar = ParseDecimal(m);
                if (sh.Giren == 0 && sh.Cikan == 0)
                {
                    bool isGiris = StokHareket.IsStockInflow(sh.IslemTuru);
                    if (isGiris)
                    {
                        sh.Giren = sh.Miktar;
                        sh.Cikan = 0;
                    }
                    else
                    {
                        sh.Cikan = sh.Miktar;
                        sh.Giren = 0;
                    }
                }
            }
            if (el.TryGetProperty("birim_fiyat", out var bf)) sh.Fiyat = ParseDecimal(bf);
            if (el.TryGetProperty("fatura_id", out var fi)) sh.FaturaId = ParseNullableInt(fi);
            if (el.TryGetProperty("fatura_uuid", out var fu) && fu.ValueKind == JsonValueKind.String) sh.FaturaUuid = fu.GetString();
            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) sh.Aciklama = ac.GetString();
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) sh.Uuid = uu.GetString()!;
            else sh.Uuid = sh.Id > 0 ? $"legacy-stokhareket-{sh.Id}" : "";
            if (el.TryGetProperty("version", out var vr)) sh.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) sh.UpdatedAt = dtUa;
            if (el.TryGetProperty("is_deleted", out var isDel))
                sh.IsDeleted = isDel.ValueKind == JsonValueKind.True || (isDel.ValueKind == JsonValueKind.Number && isDel.GetInt32() == 1);
            return sh;
        }

        private static Dictionary<string, object?> MapBankaToPayload(BankaKart b)
        {
            string rawYetkili = b.Yetkili ?? "";
            string aciklama = (b.KartTuru == "Kasa" && !rawYetkili.StartsWith("[KASA]"))
                ? $"[KASA]{rawYetkili}"
                : rawYetkili;
            return new Dictionary<string, object?>
            {
                ["id"] = b.Id,
                ["banka_adi"] = b.BankaAdi ?? "",
                ["sube_adi"] = b.SubeAdi ?? "",
                ["hesap_no"] = b.HesapNo ?? "",
                ["iban"] = b.IBAN ?? "",
                ["bakiye"] = b.Bakiye,
                ["acilis_bakiyesi"] = b.AcilisBakiyesi,
                ["guncel_bakiye"] = b.GuncelBakiye,
                ["para_birimi"] = b.DovizTuru ?? "TRY",
                ["kart_turu"] = b.KartTuru ?? (aciklama.StartsWith("[KASA]") ? "Kasa" : "Vadesiz"),
                ["aciklama"] = aciklama,
                ["is_active"] = true,
                ["is_deleted"] = b.IsDeleted,
                ["uuid"] = string.IsNullOrEmpty(b.Uuid) ? (b.Uuid = Guid.NewGuid().ToString()) : b.Uuid,
                ["version"] = b.Version <= 0 ? 1 : b.Version,
                ["updated_at"] = (b.UpdatedAt != default ? b.UpdatedAt : DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm:ssZ")
            };
        }

        private static Dictionary<string, object?> MapKasaToPayload(BankaKart b) => new()
        {
            ["id"] = b.Id,
            ["kasa_kodu"] = b.HesapNo ?? $"KAS-{b.Id}",
            ["kasa_adi"] = b.BankaAdi ?? "",
            ["bakiye"] = b.Bakiye,
            ["acilis_bakiyesi"] = b.AcilisBakiyesi,
            ["guncel_bakiye"] = b.GuncelBakiye,
            ["para_birimi"] = b.DovizTuru ?? "TRY",
            ["aciklama"] = b.Yetkili ?? "",
            ["is_active"] = true,
            ["is_deleted"] = b.IsDeleted,
            ["uuid"] = string.IsNullOrEmpty(b.Uuid) ? (b.Uuid = Guid.NewGuid().ToString()) : b.Uuid,
            ["version"] = b.Version <= 0 ? 1 : b.Version,
            ["updated_at"] = (b.UpdatedAt != default ? b.UpdatedAt : DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        private static BankaKart MapPayloadToKasa(JsonElement el)
        {
            var b = new BankaKart { KartTuru = "Kasa" };
            if (el.TryGetProperty("id", out var id)) b.Id = ParseInt(id);
            if (el.TryGetProperty("kasa_adi", out var ka) && ka.ValueKind == JsonValueKind.String) b.BankaAdi = ka.GetString();
            if (el.TryGetProperty("kasa_kodu", out var kk) && kk.ValueKind == JsonValueKind.String) b.HesapNo = kk.GetString();
            if (el.TryGetProperty("bakiye", out var bq)) b.Bakiye = ParseDecimal(bq);
            if (el.TryGetProperty("guncel_bakiye", out var gbq) && ParseDecimal(gbq) != 0) b.GuncelBakiye = ParseDecimal(gbq);
            if (el.TryGetProperty("acilis_bakiyesi", out var abq)) b.AcilisBakiyesi = ParseDecimal(abq);
            if (el.TryGetProperty("para_birimi", out var pb) && pb.ValueKind == JsonValueKind.String) b.DovizTuru = pb.GetString();
            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String)
            {
                var acStr = ac.GetString() ?? "";
                b.Yetkili = acStr.StartsWith("[KASA]") ? acStr.Substring(6) : acStr;
            }
            if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) b.IsDeleted = true;
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) b.Uuid = uu.GetString()!;
            else b.Uuid = b.Id > 0 ? $"legacy-kasa-{b.Id}" : "";
            if (el.TryGetProperty("version", out var vr)) b.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) b.UpdatedAt = dtUa;
            return b;
        }

        private static BankaKart MapPayloadToBanka(JsonElement el)
        {
            var b = new BankaKart();
            if (el.TryGetProperty("id", out var id)) b.Id = ParseInt(id);
            if (el.TryGetProperty("banka_adi", out var ba) && ba.ValueKind == JsonValueKind.String) b.BankaAdi = ba.GetString();
            if (el.TryGetProperty("sube_adi", out var sa) && sa.ValueKind == JsonValueKind.String) b.SubeAdi = sa.GetString();
            if (el.TryGetProperty("hesap_no", out var hn) && hn.ValueKind == JsonValueKind.String) b.HesapNo = hn.GetString();
            if (el.TryGetProperty("iban", out var ib) && ib.ValueKind == JsonValueKind.String) b.IBAN = ib.GetString();
            if (el.TryGetProperty("bakiye", out var bq)) b.Bakiye = ParseDecimal(bq);
            if (el.TryGetProperty("guncel_bakiye", out var gbq)) b.GuncelBakiye = ParseDecimal(gbq);
            if (el.TryGetProperty("acilis_bakiyesi", out var abq)) b.AcilisBakiyesi = ParseDecimal(abq);
            if (el.TryGetProperty("para_birimi", out var pb) && pb.ValueKind == JsonValueKind.String) b.DovizTuru = pb.GetString();
            if (el.TryGetProperty("telefon", out var tel) && tel.ValueKind == JsonValueKind.String) b.Telefon = tel.GetString();
            if (el.TryGetProperty("kart_turu", out var kt) && kt.ValueKind == JsonValueKind.String) b.KartTuru = kt.GetString();
            if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) b.IsDeleted = true;
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) b.Uuid = uu.GetString()!;
            else b.Uuid = b.Id > 0 ? $"legacy-banka-{b.Id}" : "";
            if (el.TryGetProperty("version", out var vr)) b.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) b.UpdatedAt = dtUa;

            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String)
            {
                var acStr = ac.GetString() ?? "";
                if (acStr.StartsWith("[KASA]"))
                {
                    b.KartTuru = "Kasa";
                    var parts = acStr.Substring(6).Split('|');
                    if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0])) b.Yetkili = parts[0];
                }
                else
                {
                    b.Yetkili = acStr;
                    if (string.IsNullOrEmpty(b.KartTuru)) b.KartTuru = "Vadesiz";
                }
            }
            else if (string.IsNullOrEmpty(b.KartTuru))
            {
                b.KartTuru = "Vadesiz";
            }
            return b;
        }

        private static Dictionary<string, object?> MapBankaHareketToPayload(BankaHareket bh) => new()
        {
            ["id"] = bh.Id,
            ["banka_id"] = bh.BankaId,
            ["tarih"] = bh.Tarih.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["hareket_turu"] = bh.IslemTuru ?? "",
            ["evrak_no"] = bh.EvrakNo ?? "",
            ["aciklama"] = bh.Aciklama ?? "",
            ["yatan"] = bh.Giren,
            ["ceken"] = bh.Cikan,
            ["cari_id"] = bh.CariId > 0 ? bh.CariId : null,
            ["cari_unvan"] = bh.CariUnvan ?? "",
            ["fatura_id"] = bh.FaturaId > 0 ? bh.FaturaId : null,
            ["ref_id"] = bh.RefId ?? "",
            ["yonlendirilen_cari_id"] = bh.YonlendirilenCariId,
            ["yonlendirilen_cari_unvan"] = bh.YonlendirilenCariUnvan ?? "",
            ["is_deleted"] = bh.IsDeleted,
            ["uuid"] = string.IsNullOrEmpty(bh.Uuid) ? (bh.Uuid = Guid.NewGuid().ToString()) : bh.Uuid,
            ["version"] = bh.Version <= 0 ? 1 : bh.Version,
            ["updated_at"] = (bh.UpdatedAt != default ? bh.UpdatedAt : DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        private static BankaHareket? MapPayloadToBankaHareket(JsonElement el)
        {
            var bh = new BankaHareket();
            if (el.TryGetProperty("id", out var id)) bh.Id = ParseInt(id);
            if (el.TryGetProperty("banka_id", out var bi)) bh.BankaId = ParseInt(bi);
            if (el.TryGetProperty("tarih", out var trh) && trh.ValueKind == JsonValueKind.String && DateTime.TryParse(trh.GetString(), out var t)) bh.Tarih = t;
            if (el.TryGetProperty("hareket_turu", out var ht) && ht.ValueKind == JsonValueKind.String) bh.IslemTuru = ht.GetString();
            else if (el.TryGetProperty("islem_turu", out var it) && it.ValueKind == JsonValueKind.String) bh.IslemTuru = it.GetString();
            if (el.TryGetProperty("evrak_no", out var en) && en.ValueKind == JsonValueKind.String) bh.EvrakNo = en.GetString();
            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) bh.Aciklama = ac.GetString();
            if (el.TryGetProperty("yatan", out var y)) bh.Giren = ParseDecimal(y);
            if (el.TryGetProperty("ceken", out var c)) bh.Cikan = ParseDecimal(c);
            if (el.TryGetProperty("cari_id", out var ci)) bh.CariId = ParseNullableInt(ci);
            if (el.TryGetProperty("cari_unvan", out var cu) && cu.ValueKind == JsonValueKind.String) bh.CariUnvan = cu.GetString();
            if (el.TryGetProperty("ref_id", out var rf) && rf.ValueKind == JsonValueKind.String) bh.RefId = rf.GetString();
            if (el.TryGetProperty("yonlendirilen_cari_id", out var yci)) bh.YonlendirilenCariId = ParseNullableInt(yci);
            if (el.TryGetProperty("yonlendirilen_cari_unvan", out var ycu) && ycu.ValueKind == JsonValueKind.String) bh.YonlendirilenCariUnvan = ycu.GetString();
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) bh.Uuid = uu.GetString()!;
            else bh.Uuid = bh.Id > 0 ? $"legacy-banka_hareket-{bh.Id}" : "";
            if (el.TryGetProperty("version", out var vr)) bh.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) bh.UpdatedAt = dtUa;
            if (el.TryGetProperty("is_deleted", out var isDel))
                bh.IsDeleted = isDel.ValueKind == JsonValueKind.True || (isDel.ValueKind == JsonValueKind.Number && isDel.GetInt32() == 1);
            return bh;
        }

        private static Dictionary<string, object?> MapKasaHareketToPayload(KasaHareket kh) => new()
        {
            ["id"] = kh.Id,
            ["kasa_id"] = kh.KasaId,
            ["tarih"] = kh.Tarih.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["hareket_turu"] = kh.IslemTuru ?? "",
            ["evrak_no"] = kh.EvrakNo ?? "",
            ["aciklama"] = kh.Aciklama ?? "",
            ["gelir"] = kh.Giren,
            ["gider"] = kh.Cikan,
            ["cari_id"] = kh.CariId > 0 ? kh.CariId : null,
            ["cari_unvan"] = kh.CariUnvan ?? "",
            ["fatura_id"] = kh.FaturaId > 0 ? kh.FaturaId : null,
            ["ref_id"] = kh.RefId ?? "",
            ["yonlendirilen_cari_id"] = kh.YonlendirilenCariId,
            ["yonlendirilen_cari_unvan"] = kh.YonlendirilenCariUnvan ?? "",
            ["is_deleted"] = kh.IsDeleted,
            ["uuid"] = string.IsNullOrEmpty(kh.Uuid) ? (kh.Uuid = Guid.NewGuid().ToString()) : kh.Uuid,
            ["version"] = kh.Version <= 0 ? 1 : kh.Version,
            ["updated_at"] = (kh.UpdatedAt != default ? kh.UpdatedAt : DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        private static KasaHareket? MapPayloadToKasaHareket(JsonElement el)
        {
            var kh = new KasaHareket();
            if (el.TryGetProperty("id", out var id)) kh.Id = ParseInt(id);
            if (el.TryGetProperty("kasa_id", out var ki)) kh.KasaId = ParseInt(ki);
            if (el.TryGetProperty("tarih", out var trh) && trh.ValueKind == JsonValueKind.String && DateTime.TryParse(trh.GetString(), out var t)) kh.Tarih = t;
            if (el.TryGetProperty("hareket_turu", out var ht) && ht.ValueKind == JsonValueKind.String) kh.IslemTuru = ht.GetString();
            else if (el.TryGetProperty("islem_turu", out var it) && it.ValueKind == JsonValueKind.String) kh.IslemTuru = it.GetString();
            if (el.TryGetProperty("evrak_no", out var en) && en.ValueKind == JsonValueKind.String) kh.EvrakNo = en.GetString();
            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) kh.Aciklama = ac.GetString();
            if (el.TryGetProperty("gelir", out var g)) kh.Giren = ParseDecimal(g);
            if (el.TryGetProperty("gider", out var gd)) kh.Cikan = ParseDecimal(gd);
            if (el.TryGetProperty("cari_id", out var ci)) kh.CariId = ParseNullableInt(ci);
            if (el.TryGetProperty("cari_unvan", out var cu) && cu.ValueKind == JsonValueKind.String) kh.CariUnvan = cu.GetString();
            if (el.TryGetProperty("ref_id", out var rf) && rf.ValueKind == JsonValueKind.String) kh.RefId = rf.GetString();
            if (el.TryGetProperty("yonlendirilen_cari_id", out var yci)) kh.YonlendirilenCariId = ParseNullableInt(yci);
            if (el.TryGetProperty("yonlendirilen_cari_unvan", out var ycu) && ycu.ValueKind == JsonValueKind.String) kh.YonlendirilenCariUnvan = ycu.GetString();
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) kh.Uuid = uu.GetString()!;
            else kh.Uuid = kh.Id > 0 ? $"legacy-kasa_hareket-{kh.Id}" : "";
            if (el.TryGetProperty("version", out var vr)) kh.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) kh.UpdatedAt = dtUa;
            if (el.TryGetProperty("is_deleted", out var isDel))
                kh.IsDeleted = isDel.ValueKind == JsonValueKind.True || (isDel.ValueKind == JsonValueKind.Number && isDel.GetInt32() == 1);
            return kh;
        }

        private static Dictionary<string, object?> MapSiparisToPayload(Siparis sp) => new()
        {
            ["id"] = sp.Id,
            ["siparis_no"] = sp.SiparisNo ?? "",
            ["siparis_turu"] = "Standart",
            ["cari_id"] = sp.CariId > 0 ? sp.CariId : null,
            ["cari_unvan"] = sp.CariUnvan ?? "",
            ["tarih"] = sp.Tarih.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["teslim_tarihi"] = sp.TeslimatTarihi?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["ara_toplam"] = sp.GenelToplam,
            ["kdv_toplam"] = 0m,
            ["genel_toplam"] = sp.GenelToplam,
            ["durum"] = sp.Durum ?? "Onaylandı",
            ["aciklama"] = sp.Aciklama ?? "",
            ["is_deleted"] = sp.IsDeleted,
            ["uuid"] = string.IsNullOrEmpty(sp.Uuid) ? (sp.Uuid = Guid.NewGuid().ToString()) : sp.Uuid,
            ["version"] = sp.Version <= 0 ? 1 : sp.Version,
            ["updated_at"] = (sp.UpdatedAt != default ? sp.UpdatedAt : DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        private static Siparis MapPayloadToSiparis(JsonElement el)
        {
            var sp = new Siparis();
            if (el.TryGetProperty("id", out var id)) sp.Id = ParseInt(id);
            if (el.TryGetProperty("siparis_no", out var sn) && sn.ValueKind == JsonValueKind.String) sp.SiparisNo = sn.GetString();
            if (el.TryGetProperty("cari_id", out var ci)) sp.CariId = ParseInt(ci);
            if (el.TryGetProperty("cari_unvan", out var cu) && cu.ValueKind == JsonValueKind.String) sp.CariUnvan = cu.GetString();
            if (el.TryGetProperty("tarih", out var trh) && trh.ValueKind == JsonValueKind.String && DateTime.TryParse(trh.GetString(), out var t)) sp.Tarih = t;
            if (el.TryGetProperty("teslim_tarihi", out var tsl) && tsl.ValueKind == JsonValueKind.String && DateTime.TryParse(tsl.GetString(), out var dtTsl)) sp.TeslimatTarihi = dtTsl;
            else if (el.TryGetProperty("teslimatTarihi", out var tsl2) && tsl2.ValueKind == JsonValueKind.String && DateTime.TryParse(tsl2.GetString(), out var dtTsl2)) sp.TeslimatTarihi = dtTsl2;
            if (el.TryGetProperty("durum", out var dr) && dr.ValueKind == JsonValueKind.String) sp.Durum = dr.GetString();
            if (el.TryGetProperty("genel_toplam", out var gt)) sp.GenelToplam = ParseDecimal(gt);
            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) sp.Aciklama = ac.GetString();
            if (el.TryGetProperty("pdf_notlar", out var pn) && pn.ValueKind == JsonValueKind.String) sp.PdfNotlar = pn.GetString();
            if (el.TryGetProperty("odeme_bilgisi", out var ob) && ob.ValueKind == JsonValueKind.String) sp.OdemeBilgisi = ob.GetString();
            if (el.TryGetProperty("oncelik", out var onc) && onc.ValueKind == JsonValueKind.String) sp.Oncelik = onc.GetString();
            if (el.TryGetProperty("baglanti_evrak_no", out var ben) && ben.ValueKind == JsonValueKind.String) sp.BaglantiEvrakNo = ben.GetString();
            if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) sp.IsDeleted = true;
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) sp.Uuid = uu.GetString()!;
            else sp.Uuid = sp.Id > 0 ? $"legacy-siparis-{sp.Id}" : "";
            if (el.TryGetProperty("version", out var vr)) sp.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) sp.UpdatedAt = dtUa;
            return sp;
        }

        private static Dictionary<string, object?> MapTeklifToPayload(Teklif tk) => new()
        {
            ["id"] = tk.Id,
            ["teklif_no"] = tk.TeklifNo ?? "",
            ["teklif_turu"] = "Standart",
            ["cari_id"] = tk.CariId > 0 ? tk.CariId : null,
            ["cari_unvan"] = tk.CariUnvan ?? "",
            ["tarih"] = tk.Tarih.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["gecerlilik_tarihi"] = tk.GecerlilikTarihi?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["ara_toplam"] = tk.GenelToplam,
            ["kdv_toplam"] = 0m,
            ["genel_toplam"] = tk.GenelToplam,
            ["durum"] = tk.Durum ?? "Gönderildi",
            ["aciklama"] = tk.Aciklama ?? "",
            ["is_deleted"] = tk.IsDeleted,
            ["uuid"] = string.IsNullOrEmpty(tk.Uuid) ? (tk.Uuid = Guid.NewGuid().ToString()) : tk.Uuid,
            ["version"] = tk.Version <= 0 ? 1 : tk.Version,
            ["updated_at"] = (tk.UpdatedAt != default ? tk.UpdatedAt : DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        private static Teklif MapPayloadToTeklif(JsonElement el)
        {
            var tk = new Teklif();
            if (el.TryGetProperty("id", out var id)) tk.Id = ParseInt(id);
            if (el.TryGetProperty("teklif_no", out var tn) && tn.ValueKind == JsonValueKind.String) tk.TeklifNo = tn.GetString();
            if (el.TryGetProperty("cari_id", out var ci)) tk.CariId = ParseInt(ci);
            if (el.TryGetProperty("cari_unvan", out var cu) && cu.ValueKind == JsonValueKind.String) tk.CariUnvan = cu.GetString();
            if (el.TryGetProperty("tarih", out var trh) && trh.ValueKind == JsonValueKind.String && DateTime.TryParse(trh.GetString(), out var t)) tk.Tarih = t;
            if (el.TryGetProperty("gecerlilik_tarihi", out var gt) && gt.ValueKind == JsonValueKind.String && DateTime.TryParse(gt.GetString(), out var dtGt)) tk.GecerlilikTarihi = dtGt;
            else if (el.TryGetProperty("gecerlilikTarihi", out var gt2) && gt2.ValueKind == JsonValueKind.String && DateTime.TryParse(gt2.GetString(), out var dtGt2)) tk.GecerlilikTarihi = dtGt2;
            if (el.TryGetProperty("durum", out var dr) && dr.ValueKind == JsonValueKind.String) tk.Durum = dr.GetString();
            if (el.TryGetProperty("genel_toplam", out var gtot)) tk.GenelToplam = ParseDecimal(gtot);
            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) tk.Aciklama = ac.GetString();
            if (el.TryGetProperty("odeme_bilgisi", out var ob) && ob.ValueKind == JsonValueKind.String) tk.OdemeBilgisi = ob.GetString();
            if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) tk.IsDeleted = true;
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) tk.Uuid = uu.GetString()!;
            else tk.Uuid = tk.Id > 0 ? $"legacy-teklif-{tk.Id}" : "";
            if (el.TryGetProperty("version", out var vr)) tk.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) tk.UpdatedAt = dtUa;
            return tk;
        }

        // ==========================================
        // CARİ KARTLAR & CARİ HAREKETLER
        // ==========================================
        public static bool IsTestCari(CariKart? c)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.Unvan)) return false;
            var u = c.Unvan.Trim();
            if (u.StartsWith("Cari ", StringComparison.OrdinalIgnoreCase)) return true;
            return u.Equals("ANA MÜŞTERİ", StringComparison.OrdinalIgnoreCase) ||
                   u.Equals("ANA TEDARİKÇİ", StringComparison.OrdinalIgnoreCase) ||
                   u.Equals("DEV TEDARİKÇİ", StringComparison.OrdinalIgnoreCase) ||
                   u.Equals("HİPER MÜŞTERİ", StringComparison.OrdinalIgnoreCase) ||
                   u.Equals("Senkron Cari 1", StringComparison.OrdinalIgnoreCase) ||
                   u.Equals("Fatura Cari 1", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTestStok(StokKart? s)
        {
            if (s == null || string.IsNullOrWhiteSpace(s.StokAdi)) return false;
            var a = s.StokAdi.Trim();
            if (a.StartsWith("Stok ", StringComparison.OrdinalIgnoreCase)) return true;
            return a.Equals("AUDIT PRODUCT", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTestBanka(BankaKart? b)
        {
            if (b == null || string.IsNullOrWhiteSpace(b.BankaAdi)) return false;
            return b.BankaAdi.Trim().Equals("TEST BANKASI", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<bool> SyncCariAsync(CariKart cari)
        {
            if (!IsConnected || cari == null || IsTestCari(cari)) return false;
            return await UpsertPayloadAsync("cariler", MapCariToPayload(cari));
        }

        public async Task<bool> DeleteCariAsync(int id, string? uuid = null) => await DeleteAsync("cariler", id, uuid);
        public async Task<List<CariKart>?> PullCarilerAsync(DateTime? since = null)
        {
            using var doc = await GetJsonAsync("cariler");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<CariKart>();
            foreach (var el in doc.RootElement.EnumerateArray()) list.Add(MapPayloadToCari(el));
            return list;
        }

        public async Task<bool> SyncCariHareketAsync(CariHareket hareket) => await UpsertPayloadAsync("cari_hareketler", MapCariHareketToPayload(hareket));
        public async Task<bool> DeleteCariHareketAsync(int id, string? uuid = null) => await DeleteAsync("cari_hareketler", id, uuid);
        public async Task<List<CariHareket>?> PullCariHareketlerAsync(DateTime? since = null)
        {
            using var doc = await GetJsonAsync("cari_hareketler");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<CariHareket>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var h = MapPayloadToCariHareket(el);
                if (h != null) list.Add(h);
            }
            return list;
        }

        public async Task<bool> DeleteCariHareketByFaturaIdAsync(int faturaId, string? evrakNo = null)
        {
            bool ok = true;
            if (faturaId > 0)
                ok &= await DeleteFilteredAsync("cari_hareketler", $"fatura_id=eq.{faturaId}");
            if (!string.IsNullOrEmpty(evrakNo))
            {
                ok &= await DeleteFilteredAsync("cari_hareketler", $"evrak_no=eq.{Uri.EscapeDataString(evrakNo)}");
                ok &= await DeleteFilteredAsync("cari_hareketler", $"evrak_no=eq.{Uri.EscapeDataString("KPL-" + evrakNo)}");
            }
            return ok;
        }

        public async Task SyncCariToAllYearsAsync(CariKart cari) => await SyncCariAsync(cari);

        public async Task UpdateFutureBalancesAsync(string entityType, int entityId, decimal borcDelta, decimal alacakDelta)
        {
            await SafeRun(async () =>
            {
                string table = entityType.ToLower() switch
                {
                    "cariler" or "cari" => "cariler",
                    _ => entityType.ToLower()
                };

                using var doc = await GetJsonAsync(table, $"id=eq.{entityId}");
                if (doc != null && doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var item = doc.RootElement[0];
                    decimal curBorc = 0;
                    decimal curAlacak = 0;
                    if (item.TryGetProperty("borc_tutari", out var bt) && bt.ValueKind == JsonValueKind.Number) curBorc = bt.GetDecimal();
                    if (item.TryGetProperty("alacak_tutari", out var at) && at.ValueKind == JsonValueKind.Number) curAlacak = at.GetDecimal();

                    decimal newBorc = curBorc + borcDelta;
                    decimal newAlacak = curAlacak + alacakDelta;
                    decimal newBakiye = newBorc - newAlacak;

                    var updatePayload = new
                    {
                        id = entityId,
                        borc_tutari = newBorc,
                        alacak_tutari = newAlacak,
                        bakiye = newBakiye
                    };
                    await UpsertPayloadAsync(table, updatePayload);
                }
            });
        }

        // ==========================================
        // STOKLAR & STOK HAREKETLER
        // ==========================================
        public async Task<bool> SyncStokAsync(StokKart stok)
        {
            if (!IsConnected || stok == null || IsTestStok(stok)) return false;
            var payload = MapStokToPayload(stok);
            if (stok.Id > 0)
            {
                bool patched = await PatchPayloadAsync("stoklar", $"id=eq.{stok.Id}", payload);
                if (patched) return true;
            }
            return await UpsertPayloadAsync("stoklar", payload);
        }
        public async Task<bool> DeleteStokAsync(int id, string? uuid = null) => await DeleteAsync("stoklar", id, uuid);
        public async Task<List<StokKart>?> PullStoklarAsync(DateTime? since = null)
        {
            using var doc = await GetJsonAsync("stoklar");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<StokKart>();
            foreach (var el in doc.RootElement.EnumerateArray()) list.Add(MapPayloadToStok(el));
            return list;
        }

        public async Task<bool> SyncStokHareketAsync(StokHareket hareket) => await UpsertPayloadAsync("stok_hareketler", MapStokHareketToPayload(hareket));
        public async Task<bool> DeleteStokHareketAsync(int id, string? uuid = null) => await DeleteAsync("stok_hareketler", id, uuid);
        public async Task<List<StokHareket>?> PullStokHareketlerAsync(DateTime? since = null)
        {
            using var doc = await GetJsonAsync("stok_hareketler");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<StokHareket>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var sh = MapPayloadToStokHareket(el);
                if (sh != null) list.Add(sh);
            }
            return list;
        }

        public async Task<bool> DeleteStokHareketByFaturaIdAsync(int faturaId, string? evrakNo = null)
        {
            bool ok = true;
            if (faturaId > 0)
                ok &= await DeleteFilteredAsync("stok_hareketler", $"fatura_id=eq.{faturaId}");
            if (!string.IsNullOrEmpty(evrakNo))
                ok &= await DeleteFilteredAsync("stok_hareketler", $"evrak_no=eq.{Uri.EscapeDataString(evrakNo)}");
            return ok;
        }

        public async Task SyncStokGrupAsync(StokGrupDef grup) => await Task.CompletedTask;
        public async Task DeleteStokGrupAsync(int id) => await Task.CompletedTask;

        // ==========================================
        // FATURALAR & FATURA DETAYLAR
        // ==========================================
        public async Task<bool> SyncFaturaAsync(Fatura fatura) => await UpsertPayloadAsync("faturalar", MapFaturaToPayload(fatura));
        public async Task<bool> DeleteFaturaAsync(int id, string? uuid = null)
        {
            await DeleteFaturaDetaylarAsync(id, uuid);
            return await DeleteAsync("faturalar", id, uuid);
        }
        public async Task<List<Fatura>?> PullFaturalarAsync(DateTime? since = null, int? maliYil = null)
        {
            int targetYear = (maliYil.HasValue && maliYil.Value > 0)
                ? maliYil.Value
                : (_yearContext?.CurrentYear > 0 ? _yearContext.CurrentYear : DateTime.UtcNow.Year);

            // Hem hedef mali yıla ait hem de mobilden gelip mali_yil sütunu henüz NULL kalmış faturaları kapsa
            var filters = new List<string> { $"or=(mali_yil.eq.{targetYear},mali_yil.is.null)" };
            if (since.HasValue)
            {
                filters.Add($"updated_at=gte.{since.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}");
            }
            string filterStr = string.Join("&", filters);

            using var doc = await GetJsonAsync("faturalar", filterStr);
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                // Fallback: PostgREST or syntax desteklemezse doğrudan mali_yil filtresiz dene
                string? fallbackFilter = since.HasValue ? $"updated_at=gte.{since.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}" : null;
                using var fbDoc = await GetJsonAsync("faturalar", fallbackFilter);
                if (fbDoc != null && fbDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var fbList = new List<Fatura>();
                    foreach (var el in fbDoc.RootElement.EnumerateArray())
                    {
                        var f = MapPayloadToFatura(el);
                        if (f != null)
                        {
                            if (f.MaliYil <= 0 && f.Tarih.Year > 2000) f.MaliYil = f.Tarih.Year;
                            if (f.MaliYil == targetYear || f.MaliYil <= 0) fbList.Add(f);
                        }
                    }
                    return fbList;
                }
                return null;
            }
            var list = new List<Fatura>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var f = MapPayloadToFatura(el);
                if (f != null)
                {
                    if (f.MaliYil <= 0 && f.Tarih.Year > 2000) f.MaliYil = f.Tarih.Year;
                    // Eğer mali_yil null geldiyse ve tarihi hedef yıla aitse veya yılı belirsizse dahil et
                    if (f.MaliYil == targetYear || f.Tarih.Year == targetYear || f.MaliYil <= 0)
                    {
                        list.Add(f);
                    }
                }
            }
            return list;
        }

        public async Task<bool> DeleteFaturaDetaylarAsync(int faturaId, string? faturaUuid = null)
        {
            if (!string.IsNullOrEmpty(faturaUuid))
                return await DeleteFilteredAsync("fatura_detaylar", $"fatura_uuid=eq.{faturaUuid}");
            return await DeleteFilteredAsync("fatura_detaylar", $"fatura_id=eq.{faturaId}");
        }

        public async Task<List<FaturaDetay>> PullFaturaDetaylarAsync(int faturaId, string? faturaUuid = null)
        {
            string filter = !string.IsNullOrEmpty(faturaUuid) ? $"fatura_uuid=eq.{faturaUuid}" : $"fatura_id=eq.{faturaId}";
            using var doc = await GetJsonAsync("fatura_detaylar", filter);
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new List<FaturaDetay>();
            var list = new List<FaturaDetay>();
            foreach (var el in doc.RootElement.EnumerateArray()) list.Add(MapPayloadToFaturaDetay(el));
            return list;
        }

        public async Task<List<FaturaDetay>> PullFaturaDetaylarBatchAsync(IEnumerable<int> faturaIds)
        {
            var idList = faturaIds?.Where(id => id > 0).Distinct().ToList();
            if (idList == null || idList.Count == 0) return new List<FaturaDetay>();

            var resultList = new List<FaturaDetay>();
            const int chunkSize = 50;
            for (int i = 0; i < idList.Count; i += chunkSize)
            {
                var chunk = idList.Skip(i).Take(chunkSize);
                string filter = $"fatura_id=in.({string.Join(",", chunk)})";
                using var doc = await GetJsonAsync("fatura_detaylar", filter);
                if (doc != null && doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        resultList.Add(MapPayloadToFaturaDetay(el));
                    }
                }
            }
            return resultList;
        }

        public async Task<List<FaturaDetay>> PullFaturaDetaylarAsync(string? faturaUuid, int faturaId)
            => await PullFaturaDetaylarAsync(faturaId, faturaUuid);

        public async Task<bool> SyncFaturaDetaylarAsync(int faturaId, string? faturaUuid, List<FaturaDetay> detaylar)
        {
            await DeleteFaturaDetaylarAsync(faturaId, faturaUuid);
            if (detaylar == null || detaylar.Count == 0) return true;
            var payloads = new List<object>();
            foreach (var d in detaylar)
            {
                var p = MapFaturaDetayToPayload(d);
                if (!string.IsNullOrEmpty(faturaUuid)) p["fatura_uuid"] = faturaUuid;
                payloads.Add(p);
            }
            return await UpsertBatchPayloadAsync("fatura_detaylar", payloads);
        }

        public async Task<bool> SyncFaturaDetaylarAsync(int faturaId, List<FaturaDetay> detaylar)
            => await SyncFaturaDetaylarAsync(faturaId, null, detaylar);

        // ==========================================
        // SİPARİŞLER & TEKLİFLER
        // ==========================================
        public async Task<bool> SyncSiparisAsync(Siparis siparis) => await UpsertPayloadAsync("siparisler", MapSiparisToPayload(siparis));
        public async Task<bool> DeleteSiparisAsync(int id, string? uuid = null)
        {
            await DeleteSiparisDetaylarAsync(id, uuid);
            return await DeleteAsync("siparisler", id, uuid);
        }
        public async Task<List<Siparis>?> PullSiparislerAsync(DateTime? since = null)
        {
            using var doc = await GetJsonAsync("siparisler");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<Siparis>();
            foreach (var el in doc.RootElement.EnumerateArray()) list.Add(MapPayloadToSiparis(el));
            return list;
        }

        public async Task<bool> DeleteSiparisDetaylarAsync(int siparisId, string? siparisUuid = null)
        {
            bool ok = true;
            if (!string.IsNullOrEmpty(siparisUuid))
                ok &= await DeleteFilteredAsync("siparis_detaylar", $"siparis_uuid=eq.{siparisUuid}");
            if (siparisId > 0)
                ok &= await DeleteFilteredAsync("siparis_detaylar", $"siparis_id=eq.{siparisId}");
            return ok;
        }

        public async Task<List<SiparisDetay>> PullSiparisDetaylarAsync(int siparisId, string? siparisUuid = null)
        {
            string filter = !string.IsNullOrEmpty(siparisUuid) ? $"siparis_uuid=eq.{siparisUuid}" : $"siparis_id=eq.{siparisId}";
            filter += "&is_deleted=eq.false";
            using var doc = await GetJsonAsync("siparis_detaylar", filter);
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new List<SiparisDetay>();
            var list = new List<SiparisDetay>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.TryGetProperty("is_deleted", out var isDel) && (isDel.ValueKind == JsonValueKind.True || (isDel.ValueKind == JsonValueKind.String && isDel.GetString() == "true")))
                    continue;

                var sd = new SiparisDetay();
                if (el.TryGetProperty("id", out var id)) sd.Id = ParseInt(id);
                if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) sd.Uuid = uu.GetString()!;
                if (el.TryGetProperty("siparis_id", out var sid)) sd.SiparisId = ParseInt(sid);
                if (el.TryGetProperty("siparis_uuid", out var suu) && suu.ValueKind == JsonValueKind.String) sd.SiparisUuid = suu.GetString();
                if (el.TryGetProperty("stok_id", out var stid)) sd.StokId = ParseInt(stid);
                if (el.TryGetProperty("stok_kodu", out var sk) && sk.ValueKind == JsonValueKind.String) sd.StokKodu = sk.GetString();
                if (el.TryGetProperty("stok_adi", out var sa) && sa.ValueKind == JsonValueKind.String) sd.StokAdi = sa.GetString();
                if (el.TryGetProperty("birim", out var br) && br.ValueKind == JsonValueKind.String) sd.Birim = br.GetString();
                if (el.TryGetProperty("miktar", out var mq)) sd.Miktar = ParseDecimal(mq);
                if (el.TryGetProperty("birim_fiyat", out var bf)) sd.BirimFiyat = ParseDecimal(bf);
                if (el.TryGetProperty("kdv_orani", out var ko)) sd.KdvOrani = ParseDouble(ko);
                if (el.TryGetProperty("toplam_tutar", out var tt)) sd.Tutar = ParseDecimal(tt);
                if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) sd.Aciklama = ac.GetString();
                list.Add(sd);
            }
            return list;
        }

        public async Task<List<SiparisDetay>> PullSiparisDetaylarAsync(string? siparisUuid, int siparisId)
            => await PullSiparisDetaylarAsync(siparisId, siparisUuid);

        public async Task<bool> SyncSiparisDetaylarAsync(int siparisId, string? siparisUuid, List<SiparisDetay> detaylar)
        {
            await DeleteSiparisDetaylarAsync(siparisId, siparisUuid);
            if (detaylar == null || detaylar.Count == 0) return true;
            var payloads = new List<object>();
            foreach (var d in detaylar)
            {
                if (string.IsNullOrEmpty(d.Uuid)) d.Uuid = Guid.NewGuid().ToString("D");
                d.SiparisId = siparisId;
                if (!string.IsNullOrEmpty(siparisUuid)) d.SiparisUuid = siparisUuid;

                decimal kdvTutari = (decimal)((double)d.Tutar * (d.KdvOrani / 100.0));
                payloads.Add(new Dictionary<string, object?>
                {
                    ["uuid"] = d.Uuid,
                    ["tenant_id"] = CurrentTenantId,
                    ["siparis_id"] = siparisId > 0 ? siparisId : (int?)null,
                    ["siparis_uuid"] = siparisUuid,
                    ["stok_id"] = d.StokId > 0 ? d.StokId : (int?)null,
                    ["stok_kodu"] = d.StokKodu ?? "",
                    ["stok_adi"] = d.StokAdi ?? "",
                    ["birim"] = d.Birim ?? "Adet",
                    ["miktar"] = (decimal)d.Miktar,
                    ["birim_fiyat"] = d.BirimFiyat,
                    ["kdv_orani"] = (decimal)d.KdvOrani,
                    ["kdv_tutari"] = kdvTutari,
                    ["toplam_tutar"] = d.Tutar,
                    ["is_deleted"] = false,
                    ["version"] = d.Version <= 0 ? 1 : d.Version,
                    ["updated_at"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                });
            }
            return await UpsertBatchPayloadAsync("siparis_detaylar", payloads);
        }

        public async Task<bool> SyncSiparisDetaylarAsync(int siparisId, List<SiparisDetay> detaylar)
            => await SyncSiparisDetaylarAsync(siparisId, null, detaylar);

        public async Task<bool> SyncTeklifAsync(Teklif teklif) => await UpsertPayloadAsync("teklifler", MapTeklifToPayload(teklif));
        public async Task<bool> DeleteTeklifAsync(int id, string? uuid = null)
        {
            await DeleteTeklifDetaylarAsync(id, uuid);
            return await DeleteAsync("teklifler", id, uuid);
        }
        public async Task<List<Teklif>?> PullTekliflerAsync(DateTime? since = null)
        {
            using var doc = await GetJsonAsync("teklifler");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<Teklif>();
            foreach (var el in doc.RootElement.EnumerateArray()) list.Add(MapPayloadToTeklif(el));
            return list;
        }

        public async Task<bool> DeleteTeklifDetaylarAsync(int teklifId, string? teklifUuid = null)
        {
            bool ok = true;
            if (!string.IsNullOrEmpty(teklifUuid))
                ok &= await DeleteFilteredAsync("teklif_detaylar", $"teklif_uuid=eq.{teklifUuid}");
            if (teklifId > 0)
                ok &= await DeleteFilteredAsync("teklif_detaylar", $"teklif_id=eq.{teklifId}");
            return ok;
        }

        public async Task<List<TeklifDetay>> PullTeklifDetaylarAsync(int teklifId, string? teklifUuid = null)
        {
            string filter = !string.IsNullOrEmpty(teklifUuid) ? $"teklif_uuid=eq.{teklifUuid}" : $"teklif_id=eq.{teklifId}";
            filter += "&is_deleted=eq.false";
            using var doc = await GetJsonAsync("teklif_detaylar", filter);
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new List<TeklifDetay>();
            var list = new List<TeklifDetay>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.TryGetProperty("is_deleted", out var isDel) && (isDel.ValueKind == JsonValueKind.True || (isDel.ValueKind == JsonValueKind.String && isDel.GetString() == "true")))
                    continue;

                var td = new TeklifDetay();
                if (el.TryGetProperty("id", out var id)) td.Id = ParseInt(id);
                if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) td.Uuid = uu.GetString()!;
                if (el.TryGetProperty("teklif_id", out var tid)) td.TeklifId = ParseInt(tid);
                if (el.TryGetProperty("teklif_uuid", out var tuu) && tuu.ValueKind == JsonValueKind.String) td.TeklifUuid = tuu.GetString();
                if (el.TryGetProperty("stok_id", out var stid)) td.StokId = ParseInt(stid);
                if (el.TryGetProperty("stok_uuid", out var suu) && suu.ValueKind == JsonValueKind.String) td.StokUuid = suu.GetString();
                if (el.TryGetProperty("stok_kodu", out var sk) && sk.ValueKind == JsonValueKind.String) td.StokKodu = sk.GetString();
                if (el.TryGetProperty("stok_adi", out var sa) && sa.ValueKind == JsonValueKind.String) td.StokAdi = sa.GetString();
                if (el.TryGetProperty("birim", out var br) && br.ValueKind == JsonValueKind.String) td.Birim = br.GetString();
                if (el.TryGetProperty("miktar", out var mq)) td.Miktar = ParseDecimal(mq);
                if (el.TryGetProperty("birim_fiyat", out var bf)) td.BirimFiyat = ParseDecimal(bf);
                if (el.TryGetProperty("kdv_orani", out var ko)) td.KdvOrani = ParseDouble(ko);
                if (el.TryGetProperty("toplam_tutar", out var tt)) td.Tutar = ParseDecimal(tt);
                if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) td.Aciklama = ac.GetString();
                list.Add(td);
            }
            return list;
        }

        public async Task<List<TeklifDetay>> PullTeklifDetaylarAsync(string? teklifUuid, int teklifId)
            => await PullTeklifDetaylarAsync(teklifId, teklifUuid);

        public async Task<bool> SyncTeklifDetaylarAsync(int teklifId, string? teklifUuid, List<TeklifDetay> detaylar)
        {
            await DeleteTeklifDetaylarAsync(teklifId, teklifUuid);
            if (detaylar == null || detaylar.Count == 0) return true;
            var payloads = new List<object>();
            foreach (var d in detaylar)
            {
                if (string.IsNullOrEmpty(d.Uuid)) d.Uuid = Guid.NewGuid().ToString("D");
                d.TeklifId = teklifId;
                if (!string.IsNullOrEmpty(teklifUuid)) d.TeklifUuid = teklifUuid;

                decimal kdvTutari = (decimal)((double)d.Tutar * (d.KdvOrani / 100.0));
                payloads.Add(new Dictionary<string, object?>
                {
                    ["uuid"] = d.Uuid,
                    ["tenant_id"] = CurrentTenantId,
                    ["teklif_id"] = teklifId > 0 ? teklifId : (int?)null,
                    ["teklif_uuid"] = teklifUuid,
                    ["stok_id"] = d.StokId > 0 ? d.StokId : (int?)null,
                    ["stok_kodu"] = d.StokKodu ?? "",
                    ["stok_adi"] = d.StokAdi ?? "",
                    ["birim"] = d.Birim ?? "Adet",
                    ["miktar"] = (decimal)d.Miktar,
                    ["birim_fiyat"] = d.BirimFiyat,
                    ["kdv_orani"] = (decimal)d.KdvOrani,
                    ["kdv_tutari"] = kdvTutari,
                    ["toplam_tutar"] = d.Tutar,
                    ["is_deleted"] = false,
                    ["version"] = d.Version <= 0 ? 1 : d.Version,
                    ["updated_at"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                });
            }
            return await UpsertBatchPayloadAsync("teklif_detaylar", payloads);
        }

        public async Task<bool> SyncTeklifDetaylarAsync(int teklifId, List<TeklifDetay> detaylar)
            => await SyncTeklifDetaylarAsync(teklifId, null, detaylar);

        public async Task<bool> DeleteTeklifDetaylarAsync(int teklifId)
        {
            return await DeleteFilteredAsync("teklif_detaylar", $"teklif_id=eq.{teklifId}");
        }

        public async Task<List<TeklifDetay>> PullTeklifDetaylarAsync(int teklifId)
            => await PullTeklifDetaylarAsync(teklifId, null);

        // ==========================================
        // KASALAR & BANKALAR
        // ==========================================
        public async Task<bool> SyncKasaHareketAsync(KasaHareket hareket) => await UpsertPayloadAsync("kasa_hareketler", MapKasaHareketToPayload(hareket));
        public async Task<bool> DeleteKasaHareketAsync(int id, string? uuid = null) => await DeleteAsync("kasa_hareketler", id, uuid);
        public async Task<List<KasaHareket>?> PullKasaHareketlerAsync(DateTime? since = null)
        {
            using var doc = await GetJsonAsync("kasa_hareketler");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<KasaHareket>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var kh = MapPayloadToKasaHareket(el);
                if (kh != null) list.Add(kh);
            }
            return list;
        }

        public async Task<bool> SyncBankaAsync(BankaKart banka)
        {
            bool ok1 = await UpsertPayloadAsync("bankalar", MapBankaToPayload(banka));
            bool ok2 = true;
            if (banka.KartTuru == "Kasa")
            {
                ok2 = await UpsertPayloadAsync("kasalar", MapKasaToPayload(banka));
            }
            return ok1 && ok2;
        }

        public async Task<bool> DeleteBankaAsync(int id, string? uuid = null)
        {
            bool ok1 = await DeleteAsync("bankalar", id, uuid);
            bool ok2 = await DeleteAsync("kasalar", id, uuid);
            return ok1 || ok2;
        }

        public async Task<List<BankaKart>?> PullBankalarAsync()
        {
            var list = new List<BankaKart>();
            var seenIds = new HashSet<int>();

            // 1. Pull from kasalar table
            try
            {
                using var docKasa = await GetJsonAsync("kasalar");
                if (docKasa != null && docKasa.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in docKasa.RootElement.EnumerateArray())
                    {
                        var k = MapPayloadToKasa(el);
                        if (k.Id > 0)
                        {
                            list.Add(k);
                            seenIds.Add(k.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PullBankalarAsync] Error pulling kasalar: {ex.Message}");
            }

            // 2. Pull from bankalar table
            try
            {
                using var doc = await GetJsonAsync("bankalar");
                if (doc != null && doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        var b = MapPayloadToBanka(el);
                        if (seenIds.Contains(b.Id))
                        {
                            var existing = list.FirstOrDefault(x => x.Id == b.Id);
                            if (existing != null)
                            {
                                if (existing.KartTuru == "Kasa")
                                {
                                    // Kasa kaydını koru, banka olarak üstüne yazma
                                    if (existing.IsDeleted && !b.IsDeleted) existing.IsDeleted = false;
                                    continue;
                                }

                                if (!string.IsNullOrEmpty(b.Uuid) && !string.IsNullOrEmpty(existing.Uuid) && b.Uuid != existing.Uuid)
                                {
                                    if (existing.IsDeleted && !b.IsDeleted)
                                    {
                                        list.Remove(existing);
                                        list.Add(b);
                                    }
                                }
                                else
                                {
                                    if (existing.KartTuru != "Kasa" && b.KartTuru == "Kasa")
                                    {
                                        existing.KartTuru = "Kasa";
                                    }
                                    if (existing.IsDeleted && !b.IsDeleted)
                                    {
                                        existing.IsDeleted = false;
                                    }
                                }
                            }
                        }
                        else
                        {
                            list.Add(b);
                            seenIds.Add(b.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PullBankalarAsync] Error pulling bankalar: {ex.Message}");
            }

            return list;
        }

        public async Task<bool> SyncBankaHareketAsync(BankaHareket hareket) => await UpsertPayloadAsync("banka_hareketler", MapBankaHareketToPayload(hareket));
        public async Task<bool> DeleteBankaHareketAsync(int id, string? uuid = null) => await DeleteAsync("banka_hareketler", id, uuid);
        public async Task<List<BankaHareket>?> PullBankaHareketlerAsync(DateTime? since = null)
        {
            using var doc = await GetJsonAsync("banka_hareketler");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<BankaHareket>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var bh = MapPayloadToBankaHareket(el);
                if (bh != null) list.Add(bh);
            }
            return list;
        }

        // ==========================================
        // FİRMA PROFİLİ & NOTLAR
        // ==========================================
        public async Task<bool> SyncFirmaProfiliAsync(FirmaProfili profil)
        {
            if (profil == null) return false;
            var payload = new Dictionary<string, object?>
            {
                ["id"] = 1,
                ["unvan"] = profil.FirmaAdi ?? "",
                ["firma_adi"] = profil.FirmaAdi ?? "",
                ["vergi_dairesi"] = profil.VergiDairesi,
                ["vergi_no"] = profil.VergiNo,
                ["adres"] = profil.Adres,
                ["telefon"] = profil.Telefon,
                ["email"] = profil.Eposta,
                ["eposta"] = profil.Eposta,
                ["web_sitesi"] = profil.WebSitesi,
                ["web"] = profil.WebSitesi,
                ["logo_base64"] = profil.LogoBase64,
                ["updated_at"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };
            return await UpsertPayloadAsync("firma_profili", payload);
        }

        public async Task<FirmaProfili?> PullFirmaProfiliAsync()
        {
            using var doc = await GetJsonAsync("firma_profili", "id=eq.1");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0) return null;
            var el = doc.RootElement[0];
            var p = new FirmaProfili { Id = 1 };
            if (el.TryGetProperty("firma_adi", out var fa) && fa.ValueKind == JsonValueKind.String) p.FirmaAdi = fa.GetString();
            else if (el.TryGetProperty("unvan", out var u) && u.ValueKind == JsonValueKind.String) p.FirmaAdi = u.GetString();
            if (el.TryGetProperty("vergi_dairesi", out var vd) && vd.ValueKind == JsonValueKind.String) p.VergiDairesi = vd.GetString();
            if (el.TryGetProperty("vergi_no", out var vn) && vn.ValueKind == JsonValueKind.String) p.VergiNo = vn.GetString();
            if (el.TryGetProperty("adres", out var adr) && adr.ValueKind == JsonValueKind.String) p.Adres = adr.GetString();
            if (el.TryGetProperty("telefon", out var tel) && tel.ValueKind == JsonValueKind.String) p.Telefon = tel.GetString();
            if (el.TryGetProperty("email", out var em) && em.ValueKind == JsonValueKind.String) p.Eposta = em.GetString();
            else if (el.TryGetProperty("eposta", out var ep) && ep.ValueKind == JsonValueKind.String) p.Eposta = ep.GetString();
            if (el.TryGetProperty("web_sitesi", out var ws) && ws.ValueKind == JsonValueKind.String) p.WebSitesi = ws.GetString();
            else if (el.TryGetProperty("web", out var w) && w.ValueKind == JsonValueKind.String) p.WebSitesi = w.GetString();
            if (el.TryGetProperty("logo_base64", out var lb) && lb.ValueKind == JsonValueKind.String) p.LogoBase64 = lb.GetString();
            return p;
        }

        public async Task<bool> SyncFaturaTasarimiAsync(FaturaTasarimi tasarim)
        {
            if (tasarim == null) return false;
            var payload = new Dictionary<string, object?>
            {
                ["id"] = tasarim.Id > 0 ? tasarim.Id : 1,
                ["tenant_id"] = CurrentTenantId,
                ["show_logo"] = tasarim.ShowLogo,
                ["show_birim"] = tasarim.ShowBirim,
                ["show_kdv"] = tasarim.ShowKdv,
                ["show_ara_toplam"] = tasarim.ShowAraToplam,
                ["show_aciklama"] = tasarim.ShowAciklama,
                ["baslik_text"] = tasarim.BaslikText ?? "SATIŞ FATURASI",
                ["alt_bilgi"] = tasarim.AltBilgi ?? "",
                ["fatura_size"] = tasarim.FaturaSize ?? "A4",
                ["fatura_orientation"] = tasarim.FaturaOrientation ?? "Dikey",
                ["marka_rengi"] = tasarim.MarkaRengi ?? "#2563eb",
                ["is_deleted"] = false,
                ["updated_at"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };
            return await UpsertPayloadAsync("fatura_tasarimlari", payload);
        }

        // ==========================================
        // MALİ YILLAR SENKRONİZASYONU
        // ==========================================
        public async Task SyncMaliYilAsync(int year)
        {
            if (year <= 0) return;
            try
            {
                var payload = new Dictionary<string, object?>
                {
                    ["yil"] = year,
                    ["tenant_id"] = CurrentTenantId,
                    ["is_active"] = true,
                    ["is_deleted"] = false,
                    ["olusturma_tarihi"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };
                await UpsertPayloadAsync("mali_yillar", payload);
            }
            catch { }

            // Mirror to notlar table with tenant isolation
            try
            {
                var existingYears = await PullMaliYillarAsync() ?? new List<int>();
                if (!existingYears.Contains(year))
                {
                    existingYears.Add(year);
                    existingYears.Sort();
                }

                string noteTitle = CurrentTenantId == "default" ? "__MALI_YILLAR__" : $"__MALI_YILLAR_{CurrentTenantId}__";
                int noteId = CurrentTenantId == "default" ? 999998 : Math.Abs(CurrentTenantId.GetHashCode() % 800000) + 100000;

                var notePayload = new Dictionary<string, object?>
                {
                    ["id"] = noteId,
                    ["baslik"] = noteTitle,
                    ["icerik"] = System.Text.Json.JsonSerializer.Serialize(existingYears),
                    ["renk"] = "#0061FF",
                    ["tarih"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ["is_deleted"] = false,
                    ["tenant_id"] = CurrentTenantId
                };
                await UpsertPayloadAsync("notlar", notePayload);
            }
            catch { }
        }

        public async Task SyncMaliYillarAsync(IEnumerable<int> years)
        {
            if (!IsConnected || years == null || !years.Any()) return;
            var cleanYears = years.Where(y => y > 0).Distinct().ToList();
            if (!cleanYears.Any()) return;

            // Merge with existing cloud years for this tenant
            try
            {
                var existing = await PullMaliYillarAsync();
                if (existing != null && existing.Any())
                {
                    cleanYears = cleanYears.Union(existing).Distinct().ToList();
                }
            }
            catch { }

            cleanYears = cleanYears.OrderBy(y => y).ToList();

            try
            {
                var payloads = cleanYears.Select(y => (object)new Dictionary<string, object?>
                {
                    ["yil"] = y,
                    ["tenant_id"] = CurrentTenantId,
                    ["is_active"] = true,
                    ["is_deleted"] = false,
                    ["olusturma_tarihi"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                }).ToList();
                await UpsertBatchPayloadAsync("mali_yillar", payloads);
            }
            catch { }

            // Write merged years to tenant isolated notlar record
            try
            {
                string noteTitle = CurrentTenantId == "default" ? "__MALI_YILLAR__" : $"__MALI_YILLAR_{CurrentTenantId}__";
                int noteId = CurrentTenantId == "default" ? 999998 : Math.Abs(CurrentTenantId.GetHashCode() % 800000) + 100000;

                var notePayload = new Dictionary<string, object?>
                {
                    ["id"] = noteId,
                    ["baslik"] = noteTitle,
                    ["icerik"] = System.Text.Json.JsonSerializer.Serialize(cleanYears),
                    ["renk"] = "#0061FF",
                    ["tarih"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ["is_deleted"] = false,
                    ["tenant_id"] = CurrentTenantId
                };
                await UpsertPayloadAsync("notlar", notePayload);
            }
            catch { }
        }

        public async Task SyncActiveMaliYilAsync(int year)
        {
            if (year <= 0) return;
            await SyncMaliYilAsync(year);

            if (!IsConnected) return;

            // In mali_yillar, mark this year as is_active = true for this tenant
            try
            {
                var payload = new Dictionary<string, object?>
                {
                    ["yil"] = year,
                    ["tenant_id"] = CurrentTenantId,
                    ["is_active"] = true,
                    ["is_deleted"] = false,
                    ["olusturma_tarihi"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };
                await UpsertPayloadAsync("mali_yillar", payload);
            }
            catch { }

            // Mirror active year to notlar for this tenant
            try
            {
                string noteTitle = CurrentTenantId == "default" ? "__AKTIF_MALI_YIL__" : $"__AKTIF_MALI_YIL_{CurrentTenantId}__";
                int noteId = CurrentTenantId == "default" ? 999997 : Math.Abs((CurrentTenantId + "_active").GetHashCode() % 800000) + 100000;

                var notePayload = new Dictionary<string, object?>
                {
                    ["id"] = noteId,
                    ["baslik"] = noteTitle,
                    ["icerik"] = year.ToString(),
                    ["renk"] = "#10B981",
                    ["tarih"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ["is_deleted"] = false,
                    ["tenant_id"] = CurrentTenantId
                };
                await UpsertPayloadAsync("notlar", notePayload);
            }
            catch { }
        }

        public async Task<int?> PullActiveMaliYilAsync()
        {
            // 1. Check notlar for this tenant's active year
            try
            {
                string noteTitle = CurrentTenantId == "default" ? "__AKTIF_MALI_YIL__" : $"__AKTIF_MALI_YIL_{CurrentTenantId}__";
                using var noteDoc = await GetJsonAsync("notlar", $"baslik=eq.{Uri.EscapeDataString(noteTitle)}");
                if (noteDoc != null && noteDoc.RootElement.ValueKind == JsonValueKind.Array && noteDoc.RootElement.GetArrayLength() > 0)
                {
                    var item = noteDoc.RootElement[0];
                    if (item.TryGetProperty("icerik", out var icerikEl) && icerikEl.ValueKind == JsonValueKind.String)
                    {
                        var str = icerikEl.GetString();
                        if (int.TryParse(str?.Trim(), out int y) && y >= 2000 && y <= 2100)
                        {
                            return y;
                        }
                    }
                }
                else if (CurrentTenantId == "default")
                {
                    using var legacyDoc = await GetJsonAsync("notlar", "id=eq.999997");
                    if (legacyDoc != null && legacyDoc.RootElement.ValueKind == JsonValueKind.Array && legacyDoc.RootElement.GetArrayLength() > 0)
                    {
                        var item = legacyDoc.RootElement[0];
                        if (item.TryGetProperty("icerik", out var icerikEl) && icerikEl.ValueKind == JsonValueKind.String)
                        {
                            var str = icerikEl.GetString();
                            if (int.TryParse(str?.Trim(), out int y) && y >= 2000 && y <= 2100) return y;
                        }
                    }
                }
            }
            catch { }

            // 2. Check mali_yillar table where is_active = true
            try
            {
                string tenantFilter = (CurrentTenantId != "default" && !string.IsNullOrEmpty(CurrentTenantId))
                    ? $"tenant_id=eq.{Uri.EscapeDataString(CurrentTenantId)}"
                    : "or=(tenant_id.is.null,tenant_id.eq.default)";
                using var doc = await GetJsonAsync("mali_yillar", $"and=(is_active.eq.true,or=(is_deleted.is.null,is_deleted.eq.false),{tenantFilter})&order=olusturma_tarihi.desc,yil.desc&limit=1");
                if (doc != null && doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var item = doc.RootElement[0];
                    if (item.TryGetProperty("yil", out var yEl))
                    {
                        int y = ParseInt(yEl);
                        if (y >= 2000 && y <= 2100) return y;
                    }
                }
            }
            catch { }

            return null;
        }

        public async Task DeleteMaliYilAsync(int year)
        {
            if (!IsConnected || year <= 0) return;
            try
            {
                await DeleteFilteredAsync("mali_yillar", $"yil=eq.{year}");
            }
            catch { }
            try
            {
                var existingYears = await PullMaliYillarAsync() ?? new List<int>();
                existingYears.Remove(year);

                string noteTitle = CurrentTenantId == "default" ? "__MALI_YILLAR__" : $"__MALI_YILLAR_{CurrentTenantId}__";
                int noteId = CurrentTenantId == "default" ? 999998 : Math.Abs(CurrentTenantId.GetHashCode() % 800000) + 100000;

                var notePayload = new Dictionary<string, object?>
                {
                    ["id"] = noteId,
                    ["baslik"] = noteTitle,
                    ["icerik"] = System.Text.Json.JsonSerializer.Serialize(existingYears),
                    ["renk"] = "#0061FF",
                    ["tarih"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ["is_deleted"] = false,
                    ["tenant_id"] = CurrentTenantId
                };
                await UpsertPayloadAsync("notlar", notePayload);
            }
            catch { }
        }

        public async Task<List<int>?> PullMaliYillarAsync()
        {
            var list = new HashSet<int>();
            try
            {
                string tenantFilter = (CurrentTenantId != "default" && !string.IsNullOrEmpty(CurrentTenantId))
                    ? $"tenant_id=eq.{Uri.EscapeDataString(CurrentTenantId)}"
                    : "or=(tenant_id.is.null,tenant_id.eq.default)";
                using var doc = await GetJsonAsync("mali_yillar", $"and=(or=(is_deleted.is.null,is_deleted.eq.false),{tenantFilter})");
                if (doc != null && doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.TryGetProperty("yil", out var y))
                        {
                            int val = ParseInt(y);
                            if (val > 0) list.Add(val);
                        }
                    }
                }
            }
            catch { }

            // Fallback / mirror: check notlar for this tenant
            try
            {
                string noteTitle = CurrentTenantId == "default" ? "__MALI_YILLAR__" : $"__MALI_YILLAR_{CurrentTenantId}__";
                using var noteDoc = await GetJsonAsync("notlar", $"baslik=eq.{Uri.EscapeDataString(noteTitle)}");
                if (noteDoc != null && noteDoc.RootElement.ValueKind == JsonValueKind.Array && noteDoc.RootElement.GetArrayLength() > 0)
                {
                    var item = noteDoc.RootElement[0];
                    if (item.TryGetProperty("icerik", out var icerikEl) && icerikEl.ValueKind == JsonValueKind.String)
                    {
                        var json = icerikEl.GetString();
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            var parsed = System.Text.Json.JsonSerializer.Deserialize<List<int>>(json);
                            if (parsed != null)
                            {
                                foreach (var py in parsed) if (py > 0) list.Add(py);
                            }
                        }
                    }
                }
                else if (CurrentTenantId == "default")
                {
                    using var legacyDoc = await GetJsonAsync("notlar", "id=eq.999998");
                    if (legacyDoc != null && legacyDoc.RootElement.ValueKind == JsonValueKind.Array && legacyDoc.RootElement.GetArrayLength() > 0)
                    {
                        var item = legacyDoc.RootElement[0];
                        if (item.TryGetProperty("icerik", out var icerikEl) && icerikEl.ValueKind == JsonValueKind.String)
                        {
                            var json = icerikEl.GetString();
                            if (!string.IsNullOrWhiteSpace(json))
                            {
                                var parsed = System.Text.Json.JsonSerializer.Deserialize<List<int>>(json);
                                if (parsed != null)
                                {
                                    foreach (var py in parsed) if (py > 0) list.Add(py);
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return list.OrderBy(y => y).ToList();
        }

        public async Task<bool> SyncNoteAsync(Note note)
        {
            if (note == null) return false;
            var payload = new Dictionary<string, object?>
            {
                ["id"] = note.Id.ToString(),
                ["baslik"] = note.Title ?? "",
                ["icerik"] = note.Content ?? "",
                ["renk"] = note.Color ?? "#0061FF",
                ["tarih"] = note.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["is_deleted"] = note.IsDeleted
            };
            return await UpsertPayloadAsync("notlar", payload);
        }

        public async Task<bool> DeleteNoteAsync(int id, string? uuid = null) => await DeleteAsync("notlar", id, uuid);

        public async Task<List<Note>> PullNotesAsync()
        {
            using var doc = await GetJsonAsync("notlar", "id=neq.999999");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new List<Note>();
            var list = new List<Note>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var n = new Note();
                if (el.TryGetProperty("id", out var id)) n.Id = ParseInt(id);
                if (el.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String) n.Title = t.GetString();
                else if (el.TryGetProperty("baslik", out var b) && b.ValueKind == JsonValueKind.String) n.Title = b.GetString();
                if (el.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String) n.Content = c.GetString();
                else if (el.TryGetProperty("icerik", out var i) && i.ValueKind == JsonValueKind.String) n.Content = i.GetString();
                if (el.TryGetProperty("color", out var cl) && cl.ValueKind == JsonValueKind.String) n.Color = cl.GetString();
                else if (el.TryGetProperty("renk", out var r) && r.ValueKind == JsonValueKind.String) n.Color = r.GetString();
                list.Add(n);
            }
            return list;
        }

        // ==========================================
        // KULLANICI YÖNETİMİ & SUPABASE AUTH
        // ==========================================
        public async Task<bool> RegisterSupabaseAuthUserAsync(string username, string password, string? email = null)
        {
            if (!IsConnected) return false;
            try
            {
                string cleanUser = username.Trim();
                string cleanPass = password.Trim();
                string userEmail = !string.IsNullOrEmpty(email) && email.Contains("@") ? email.Trim() : $"{cleanUser.ToLower()}@ermay.local";

                // 1. Supabase Auth Signup API
                try
                {
                    string cleanBase = GetCleanBaseUrl();
                    string authUrl = $"{cleanBase}/auth/v1/signup";
                    using var authReq = new HttpRequestMessage(HttpMethod.Post, authUrl);
                    authReq.Headers.Add("apikey", _config.AuthSecret);
                    authReq.Headers.Add("Authorization", $"Bearer {_config.AuthSecret}");

                    var authPayload = new
                    {
                        email = userEmail,
                        password = cleanPass,
                        data = new
                        {
                            username = cleanUser.ToLower(),
                            role = "Admin",
                            full_name = cleanUser
                        }
                    };
                    authReq.Content = new StringContent(JsonSerializer.Serialize(authPayload), Encoding.UTF8, "application/json");
                    using var authRes = await _http.SendAsync(authReq);
                }
                catch (Exception authEx)
                {
                    Console.WriteLine($"[RegisterSupabaseAuthUser auth warning]: {authEx.Message}");
                }

                // 2. public.kullanicilar tablosuna ekle
                var salt = AuthService.GenerateSalt();
                var hash = AuthService.HashPassword(cleanPass, salt);
                var userRow = new Dictionary<string, object?>
                {
                    ["id"] = cleanUser.ToLower(),
                    ["username"] = cleanUser.ToLower(),
                    ["kullanici_adi"] = cleanUser.ToLower(),
                    ["password_hash"] = hash,
                    ["sifre"] = hash, // Güvenlik: Düz metin şifre yerine hash saklanıyor
                    ["password_salt"] = salt,
                    ["email"] = userEmail,
                    ["role"] = "Admin",
                    ["rol"] = "Admin",
                    ["is_active"] = true,
                    ["aktif_mi"] = true
                };
                await UpsertPayloadAsync("kullanicilar", userRow);

                // 3. User modelini oluşturup SyncUserAsync çağır
                var u = new User
                {
                    Username = cleanUser.ToLower(),
                    Password = hash,
                    PasswordSalt = salt,
                    Email = userEmail,
                    Role = "Admin"
                };
                await SyncUserAsync(u);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RegisterSupabaseAuthUser error]: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SyncGenericAsync<T>(string table, T item, object? id = null)
        {
            if (!IsConnected || item == null) return false;
            try
            {
                var json = JsonSerializer.Serialize(item, _jsonOpts);
                using var doc = JsonDocument.Parse(json);
                var dict = new Dictionary<string, object?>();
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString(),
                        JsonValueKind.Number => prop.Value.GetDecimal(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => null,
                        _ => prop.Value.ToString()
                    };
                }
                if (id != null && !dict.ContainsKey("id"))
                {
                    dict["id"] = id.ToString();
                }

                string targetTable = table.ToLower() switch
                {
                    "musteritakipklasorler" => "musteri_takip_klasorler",
                    "musteritakipdetaylar" => "musteri_takip_detaylar",
                    "portfoykartlar" or "portfoykartlari" => "portfoy_kartlar",
                    "dovizkurlari" => "doviz_kurlari",
                    _ => table.ToLower()
                };

                return await UpsertPayloadAsync(targetTable, dict);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase SyncGenericAsync Error] {ex.Message}");
                return false;
            }
        }

        public async Task<List<User>> PullUsersAsync()
        {
            if (!IsConnected) return new();
            var mergedUsers = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);
            try
            {
                // 1. First try pulling from public.kullanicilar table
                using var userDoc = await GetJsonAsync("kullanicilar");
                if (userDoc != null && userDoc.RootElement.ValueKind == JsonValueKind.Array && userDoc.RootElement.GetArrayLength() > 0)
                {
                    foreach (var el in userDoc.RootElement.EnumerateArray())
                    {
                        var u = new User();
                        if (el.TryGetProperty("username", out var un) && un.ValueKind == JsonValueKind.String) u.Username = un.GetString()?.Trim().ToLower();
                        else if (el.TryGetProperty("kullanici_adi", out var ka) && ka.ValueKind == JsonValueKind.String) u.Username = ka.GetString()?.Trim().ToLower();

                        if (el.TryGetProperty("password_hash", out var ph) && ph.ValueKind == JsonValueKind.String) u.Password = ph.GetString();
                        else if (el.TryGetProperty("sifre", out var sf) && sf.ValueKind == JsonValueKind.String) u.Password = sf.GetString();

                        if (el.TryGetProperty("password_salt", out var ps) && ps.ValueKind == JsonValueKind.String) u.PasswordSalt = ps.GetString();
                        if (el.TryGetProperty("email", out var em) && em.ValueKind == JsonValueKind.String) u.Email = em.GetString();
                        if (el.TryGetProperty("role", out var rl) && rl.ValueKind == JsonValueKind.String) u.Role = rl.GetString();
                        if (el.TryGetProperty("tenant_id", out var ti) && ti.ValueKind == JsonValueKind.String) u.TenantId = ti.GetString();

                        if (string.IsNullOrWhiteSpace(u.TenantId) || u.TenantId == "default")
                            u.TenantId = DatabaseService.DefaultTenantId;

                        if (!string.IsNullOrEmpty(u.Username) &&
                            !string.IsNullOrEmpty(u.Password) &&
                            !string.Equals(u.Password, "temporary", StringComparison.OrdinalIgnoreCase))
                        {
                            mergedUsers[u.Username] = u;
                        }
                    }
                }

                // 2. Also check notlar table (__SYS_USERS__ / id=999999) and merge any missing or newer credentials
                using var req = CreateRequest(HttpMethod.Get, "notlar?or=(id.eq.999999,baslik.eq.__SYS_USERS__,title.eq.__SYS_USERS__)&select=*");
                using var res = await _http.SendAsync(req);
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                    {
                        foreach (var first in doc.RootElement.EnumerateArray())
                        {
                            string? userJson = null;
                            if (first.TryGetProperty("icerik", out var icerikElem) && icerikElem.ValueKind == JsonValueKind.String)
                                userJson = icerikElem.GetString();
                            if (string.IsNullOrWhiteSpace(userJson) && first.TryGetProperty("content", out var contentElem) && contentElem.ValueKind == JsonValueKind.String)
                                userJson = contentElem.GetString();

                            if (!string.IsNullOrEmpty(userJson))
                            {
                                using var sysDoc = JsonDocument.Parse(userJson);
                                if (sysDoc.RootElement.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var uEl in sysDoc.RootElement.EnumerateArray())
                                    {
                                        var nu = new User();
                                        if (uEl.TryGetProperty("username", out var un) && un.ValueKind == JsonValueKind.String) nu.Username = un.GetString()?.Trim().ToLower();
                                        else if (uEl.TryGetProperty("Username", out var un2) && un2.ValueKind == JsonValueKind.String) nu.Username = un2.GetString()?.Trim().ToLower();

                                        if (uEl.TryGetProperty("password", out var pw) && pw.ValueKind == JsonValueKind.String) nu.Password = pw.GetString();
                                        else if (uEl.TryGetProperty("Password", out var pw2) && pw2.ValueKind == JsonValueKind.String) nu.Password = pw2.GetString();
                                        else if (uEl.TryGetProperty("password_hash", out var pwh) && pwh.ValueKind == JsonValueKind.String) nu.Password = pwh.GetString();

                                        if (uEl.TryGetProperty("password_salt", out var ps) && ps.ValueKind == JsonValueKind.String) nu.PasswordSalt = ps.GetString();
                                        else if (uEl.TryGetProperty("PasswordSalt", out var ps2) && ps2.ValueKind == JsonValueKind.String) nu.PasswordSalt = ps2.GetString();

                                        if (uEl.TryGetProperty("email", out var em) && em.ValueKind == JsonValueKind.String) nu.Email = em.GetString();
                                        else if (uEl.TryGetProperty("Email", out var em2) && em2.ValueKind == JsonValueKind.String) nu.Email = em2.GetString();

                                        if (uEl.TryGetProperty("role", out var rl) && rl.ValueKind == JsonValueKind.String) nu.Role = rl.GetString();
                                        else if (uEl.TryGetProperty("Role", out var rl2) && rl2.ValueKind == JsonValueKind.String) nu.Role = rl2.GetString();

                                        if (uEl.TryGetProperty("tenant_id", out var ti) && ti.ValueKind == JsonValueKind.String) nu.TenantId = ti.GetString();
                                        else if (uEl.TryGetProperty("TenantId", out var ti2) && ti2.ValueKind == JsonValueKind.String) nu.TenantId = ti2.GetString();

                                        if (string.IsNullOrWhiteSpace(nu.TenantId) || nu.TenantId == "default")
                                            nu.TenantId = DatabaseService.DefaultTenantId;

                                        if (!string.IsNullOrEmpty(nu.Username) &&
                                            !string.IsNullOrEmpty(nu.Password) &&
                                            !string.Equals(nu.Password, "temporary", StringComparison.OrdinalIgnoreCase) &&
                                            !mergedUsers.ContainsKey(nu.Username))
                                        {
                                            mergedUsers[nu.Username] = nu;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PullUsersAsync Error] {ex.Message}");
            }
            return mergedUsers.Values.ToList();
        }

        public async Task SyncUserAsync(User user)
        {
            if (!IsConnected || user == null) return;
            try
            {
                var normalizedUsername = (user.Username ?? "").Trim().ToLower();
                if (string.IsNullOrEmpty(normalizedUsername)) return;

                var resolvedTenant = string.IsNullOrWhiteSpace(user.TenantId) || user.TenantId == "default"
                    ? DatabaseService.DefaultTenantId
                    : user.TenantId;
                user.TenantId = resolvedTenant;

                // 1. Save to public.kullanicilar table
                var userRow = new Dictionary<string, object?>
                {
                    ["id"] = normalizedUsername,
                    ["uuid"] = normalizedUsername,
                    ["username"] = normalizedUsername,
                    ["kullanici_adi"] = normalizedUsername,
                    ["password_hash"] = user.Password ?? "",
                    ["sifre"] = user.Password ?? "",
                    ["password_salt"] = user.PasswordSalt ?? "",
                    ["email"] = user.Email ?? "",
                    ["eposta"] = user.Email ?? "",
                    ["role"] = user.Role ?? "Admin",
                    ["rol"] = user.Role ?? "Admin",
                    ["tenant_id"] = resolvedTenant,
                    ["is_active"] = true,
                    ["aktif_mi"] = true
                };
                await UpsertPayloadAsync("kullanicilar?on_conflict=id", userRow);

                // 2. Fallback / mirror to notlar
                var currentUsers = await PullUsersAsync();
                var existing = currentUsers.FirstOrDefault(u => (u.Username ?? "").Equals(normalizedUsername, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Password = user.Password;
                    existing.PasswordSalt = user.PasswordSalt;
                    existing.Role = user.Role ?? "Admin";
                    existing.Email = user.Email;
                    existing.TenantId = resolvedTenant;
                }
                else
                {
                    currentUsers.Add(user);
                }

                var usersJson = JsonSerializer.Serialize(currentUsers, _jsonOpts);
                var payload = new Dictionary<string, object?>
                {
                    ["id"] = 999999,
                    ["uuid"] = "sys_users_999999",
                    ["tenant_id"] = resolvedTenant,
                    ["title"] = "__SYS_USERS__",
                    ["baslik"] = "__SYS_USERS__",
                    ["content"] = usersJson,
                    ["icerik"] = usersJson,
                    ["color"] = "#0061FF",
                    ["renk"] = "#0061FF",
                    ["is_deleted"] = false
                };
                await UpsertPayloadAsync("notlar?on_conflict=id", payload);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SyncUserAsync Error] {ex.Message}");
            }
        }

        public async Task DeleteUserFromCloudAsync(int userId, string? username = null)
        {
            if (!IsConnected) return;
            try
            {
                if (!string.IsNullOrEmpty(username))
                {
                    await DeleteFilteredAsync("kullanicilar", $"username=eq.{Uri.EscapeDataString(username.ToLower())}");
                }

                var currentUsers = await PullUsersAsync();
                currentUsers.RemoveAll(u => u.Id == userId || (!string.IsNullOrEmpty(username) && u.Username?.Equals(username, StringComparison.OrdinalIgnoreCase) == true));

                var usersJson = JsonSerializer.Serialize(currentUsers, _jsonOpts);
                var payload = new Dictionary<string, object?>
                {
                    ["id"] = "999999",
                    ["title"] = "__SYS_USERS__",
                    ["baslik"] = "__SYS_USERS__",
                    ["content"] = usersJson,
                    ["icerik"] = usersJson,
                    ["color"] = "#0061FF",
                    ["renk"] = "#0061FF"
                };
                await UpsertPayloadAsync("notlar", payload);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeleteUserFromCloudAsync Error] {ex.Message}");
            }
        }

        // ==========================================
        // ÇEKLER & SENETLER
        // ==========================================
        private static Dictionary<string, object?> MapCekToPayload(Cek c) => new()
        {
            ["id"] = c.Id > 0 ? (object)c.Id : null,
            ["cek_no"] = c.CekNo ?? "",
            ["cari_id"] = (c.CariId.HasValue && c.CariId.Value > 0) ? (object)c.CariId.Value : null,
            ["cari_uuid"] = !string.IsNullOrEmpty(c.CariUuid) ? c.CariUuid : null,
            ["cari_unvan"] = c.CariUnvan ?? "",
            ["portfoy_no"] = c.PortfoyNo ?? "",
            ["seri_no"] = c.SeriNo ?? "",
            ["vade_tarihi"] = c.VadeTarihi.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["islem_tarihi"] = c.IslemTarihi.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["tutar"] = c.Tutar,
            ["borclu"] = c.Borclu ?? "",
            ["banka"] = c.Banka ?? "",
            ["sube"] = c.Sube ?? "",
            ["hesap_no"] = c.HesapNo ?? "",
            ["cek_turu"] = c.CekTuru ?? "",
            ["durum"] = c.Durum ?? "",
            ["aciklama"] = c.Aciklama ?? "",
            ["islem_turu"] = c.IslemTuru ?? "",
            ["yonlendirilen_cari_id"] = (c.YonlendirilenCariId.HasValue && c.YonlendirilenCariId.Value > 0) ? (object)c.YonlendirilenCariId.Value : null,
            ["yonlendirilen_cari_uuid"] = !string.IsNullOrEmpty(c.YonlendirilenCariUuid) ? c.YonlendirilenCariUuid : null,
            ["yonlendirilen_cari_unvan"] = c.YonlendirilenCariUnvan ?? "",
            ["is_deleted"] = c.IsDeleted,
            ["uuid"] = string.IsNullOrEmpty(c.Uuid) ? (c.Uuid = Guid.NewGuid().ToString("D")) : c.Uuid,
            ["version"] = c.Version <= 0 ? 1 : c.Version,
            ["updated_at"] = (c.UpdatedAt != default ? c.UpdatedAt : DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        private static Cek MapPayloadToCek(JsonElement el)
        {
            var c = new Cek();
            if (el.TryGetProperty("id", out var id)) c.Id = ParseInt(id);
            if (el.TryGetProperty("cek_no", out var cn) && cn.ValueKind == JsonValueKind.String) c.CekNo = cn.GetString();
            if (el.TryGetProperty("cari_id", out var ci)) c.CariId = ParseNullableInt(ci);
            if (el.TryGetProperty("cari_uuid", out var cuu) && cuu.ValueKind == JsonValueKind.String) c.CariUuid = cuu.GetString();
            if (el.TryGetProperty("cari_unvan", out var cu) && cu.ValueKind == JsonValueKind.String) c.CariUnvan = cu.GetString();
            if (el.TryGetProperty("portfoy_no", out var pn) && pn.ValueKind == JsonValueKind.String) c.PortfoyNo = pn.GetString();
            if (el.TryGetProperty("seri_no", out var sn) && sn.ValueKind == JsonValueKind.String) c.SeriNo = sn.GetString();
            if (el.TryGetProperty("vade_tarihi", out var vt) && vt.ValueKind == JsonValueKind.String && DateTime.TryParse(vt.GetString(), out var vtd)) c.VadeTarihi = vtd;
            if (el.TryGetProperty("islem_tarihi", out var it) && it.ValueKind == JsonValueKind.String && DateTime.TryParse(it.GetString(), out var itd)) c.IslemTarihi = itd;
            if (el.TryGetProperty("tutar", out var tt)) c.Tutar = ParseDecimal(tt);
            if (el.TryGetProperty("borclu", out var b) && b.ValueKind == JsonValueKind.String) c.Borclu = b.GetString();
            if (el.TryGetProperty("banka", out var bnk) && bnk.ValueKind == JsonValueKind.String) c.Banka = bnk.GetString();
            if (el.TryGetProperty("sube", out var sb) && sb.ValueKind == JsonValueKind.String) c.Sube = sb.GetString();
            if (el.TryGetProperty("hesap_no", out var hn) && hn.ValueKind == JsonValueKind.String) c.HesapNo = hn.GetString();
            if (el.TryGetProperty("cek_turu", out var ct) && ct.ValueKind == JsonValueKind.String) c.CekTuru = ct.GetString();
            if (el.TryGetProperty("durum", out var dm) && dm.ValueKind == JsonValueKind.String) c.Durum = dm.GetString();
            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) c.Aciklama = ac.GetString();
            if (el.TryGetProperty("islem_turu", out var ist) && ist.ValueKind == JsonValueKind.String) c.IslemTuru = ist.GetString();
            if (el.TryGetProperty("yonlendirilen_cari_id", out var yci)) c.YonlendirilenCariId = ParseNullableInt(yci);
            if (el.TryGetProperty("yonlendirilen_cari_uuid", out var ycuu) && ycuu.ValueKind == JsonValueKind.String) c.YonlendirilenCariUuid = ycuu.GetString();
            if (el.TryGetProperty("yonlendirilen_cari_unvan", out var ycu) && ycu.ValueKind == JsonValueKind.String) c.YonlendirilenCariUnvan = ycu.GetString();
            if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) c.IsDeleted = true;
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) c.Uuid = uu.GetString()!;
            else c.Uuid = c.Id > 0 ? $"legacy-cek-{c.Id}" : "";
            if (el.TryGetProperty("version", out var vr)) c.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) c.UpdatedAt = dtUa;
            return c;
        }

        public async Task<bool> SyncCekAsync(Cek cek) => await UpsertPayloadAsync("cekler", MapCekToPayload(cek));
        public async Task<bool> DeleteCekAsync(int id, string? uuid = null) => await DeleteAsync("cekler", id, uuid);
        public async Task<List<Cek>> PullCeklerAsync(DateTime? since = null)
        {
            using var doc = await GetJsonAsync("cekler");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new();
            var list = new List<Cek>();
            foreach (var el in doc.RootElement.EnumerateArray()) list.Add(MapPayloadToCek(el));
            return list;
        }

        private static Dictionary<string, object?> MapSenetToPayload(Senet s) => new()
        {
            ["id"] = s.Id > 0 ? (object)s.Id : null,
            ["senet_no"] = s.SenetNo ?? "",
            ["cari_id"] = (s.CariId.HasValue && s.CariId.Value > 0) ? (object)s.CariId.Value : null,
            ["cari_uuid"] = !string.IsNullOrEmpty(s.CariUuid) ? s.CariUuid : null,
            ["cari_unvan"] = s.CariUnvan ?? "",
            ["portfoy_no"] = s.PortfoyNo ?? "",
            ["vade_tarihi"] = s.VadeTarihi.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["tutar"] = s.Tutar,
            ["borclu"] = s.Borclu ?? "",
            ["senet_turu"] = s.SenetTuru ?? "",
            ["durum"] = s.Durum ?? "",
            ["aciklama"] = s.Aciklama ?? "",
            ["is_deleted"] = s.IsDeleted,
            ["uuid"] = string.IsNullOrEmpty(s.Uuid) ? (s.Uuid = Guid.NewGuid().ToString("D")) : s.Uuid,
            ["version"] = s.Version <= 0 ? 1 : s.Version,
            ["updated_at"] = (s.UpdatedAt != default ? s.UpdatedAt : DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        private static Senet MapPayloadToSenet(JsonElement el)
        {
            var s = new Senet();
            if (el.TryGetProperty("id", out var id)) s.Id = ParseInt(id);
            if (el.TryGetProperty("senet_no", out var sn) && sn.ValueKind == JsonValueKind.String) s.SenetNo = sn.GetString();
            if (el.TryGetProperty("cari_id", out var ci)) s.CariId = ParseNullableInt(ci);
            if (el.TryGetProperty("cari_uuid", out var cuu) && cuu.ValueKind == JsonValueKind.String) s.CariUuid = cuu.GetString();
            if (el.TryGetProperty("cari_unvan", out var cu) && cu.ValueKind == JsonValueKind.String) s.CariUnvan = cu.GetString();
            if (el.TryGetProperty("portfoy_no", out var pn) && pn.ValueKind == JsonValueKind.String) s.PortfoyNo = pn.GetString();
            if (el.TryGetProperty("vade_tarihi", out var vt) && vt.ValueKind == JsonValueKind.String && DateTime.TryParse(vt.GetString(), out var vtd)) s.VadeTarihi = vtd;
            if (el.TryGetProperty("tutar", out var tt)) s.Tutar = ParseDecimal(tt);
            if (el.TryGetProperty("borclu", out var b) && b.ValueKind == JsonValueKind.String) s.Borclu = b.GetString();
            if (el.TryGetProperty("senet_turu", out var st) && st.ValueKind == JsonValueKind.String) s.SenetTuru = st.GetString();
            if (el.TryGetProperty("durum", out var dm) && dm.ValueKind == JsonValueKind.String) s.Durum = dm.GetString();
            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) s.Aciklama = ac.GetString();
            if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) s.IsDeleted = true;
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) s.Uuid = uu.GetString()!;
            else s.Uuid = s.Id > 0 ? $"legacy-senet-{s.Id}" : "";
            if (el.TryGetProperty("version", out var vr)) s.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) s.UpdatedAt = dtUa;
            return s;
        }

        public async Task<bool> SyncSenetAsync(Senet senet) => await UpsertPayloadAsync("senetler", MapSenetToPayload(senet));
        public async Task<bool> DeleteSenetAsync(int id, string? uuid = null) => await DeleteAsync("senetler", id, uuid);
        public async Task<List<Senet>> PullSenetlerAsync(DateTime? since = null)
        {
            using var doc = await GetJsonAsync("senetler");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new();
            var list = new List<Senet>();
            foreach (var el in doc.RootElement.EnumerateArray()) list.Add(MapPayloadToSenet(el));
            return list;
        }

        // ==========================================
        // KREDİ KARTI VE EFT İŞLEMLERİ
        // ==========================================
        private static Dictionary<string, object?> MapKrediKartiToPayload(KrediKartiIslem kk) => new()
        {
            ["id"] = kk.Id.ToString(),
            ["musteri_id"] = kk.MusteriId.ToString(),
            ["musteri_unvan"] = kk.MusteriUnvan ?? "",
            ["tarih"] = kk.Tarih.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["vade_tarihi"] = kk.VadeTarihi?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["tutar"] = kk.Tutar,
            ["banka"] = kk.Banka ?? "",
            ["kart_no"] = kk.KartNo ?? "",
            ["onay_kodu"] = kk.OnayKodu ?? "",
            ["durum"] = kk.Durum ?? "Portföyde",
            ["yonlendirilen_cari_id"] = kk.YonlendirilenCariId?.ToString(),
            ["yonlendirilen_cari_unvan"] = kk.YonlendirilenCariUnvan ?? "",
            ["aciklama"] = kk.Aciklama ?? "",
            ["islem_turu"] = kk.IslemTuru ?? "",
            ["is_deleted"] = kk.IsDeleted,
            ["uuid"] = string.IsNullOrEmpty(kk.Uuid) ? (kk.Uuid = Guid.NewGuid().ToString()) : kk.Uuid,
            ["version"] = kk.Version <= 0 ? 1 : kk.Version,
            ["updated_at"] = (kk.UpdatedAt != default ? kk.UpdatedAt : DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        private static KrediKartiIslem MapPayloadToKrediKarti(JsonElement el)
        {
            var kk = new KrediKartiIslem();
            if (el.TryGetProperty("id", out var id)) kk.Id = ParseInt(id);
            if (el.TryGetProperty("musteri_id", out var mi)) kk.MusteriId = ParseInt(mi);
            if (el.TryGetProperty("musteri_unvan", out var mu) && mu.ValueKind == JsonValueKind.String) kk.MusteriUnvan = mu.GetString();
            if (el.TryGetProperty("tarih", out var trh) && trh.ValueKind == JsonValueKind.String && DateTime.TryParse(trh.GetString(), out var dt)) kk.Tarih = dt;
            if (el.TryGetProperty("vade_tarihi", out var vt) && vt.ValueKind == JsonValueKind.String && DateTime.TryParse(vt.GetString(), out var vtd)) kk.VadeTarihi = vtd;
            if (el.TryGetProperty("tutar", out var tt)) kk.Tutar = ParseDecimal(tt);
            if (el.TryGetProperty("banka", out var b) && b.ValueKind == JsonValueKind.String) kk.Banka = b.GetString();
            if (el.TryGetProperty("kart_no", out var kn) && kn.ValueKind == JsonValueKind.String) kk.KartNo = kn.GetString();
            if (el.TryGetProperty("onay_kodu", out var ok) && ok.ValueKind == JsonValueKind.String) kk.OnayKodu = ok.GetString();
            if (el.TryGetProperty("durum", out var dm) && dm.ValueKind == JsonValueKind.String) kk.Durum = dm.GetString();
            if (el.TryGetProperty("yonlendirilen_cari_id", out var yci)) kk.YonlendirilenCariId = ParseNullableInt(yci);
            if (el.TryGetProperty("yonlendirilen_cari_unvan", out var ycu) && ycu.ValueKind == JsonValueKind.String) kk.YonlendirilenCariUnvan = ycu.GetString();
            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) kk.Aciklama = ac.GetString();
            if (el.TryGetProperty("islem_turu", out var it) && it.ValueKind == JsonValueKind.String) kk.IslemTuru = it.GetString();
            if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) kk.IsDeleted = true;
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) kk.Uuid = uu.GetString()!;
            else kk.Uuid = kk.Id > 0 ? $"legacy-kk-{kk.Id}" : "";
            if (el.TryGetProperty("version", out var vr)) kk.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) kk.UpdatedAt = dtUa;
            return kk;
        }

        public async Task<bool> SyncKrediKartiIslemAsync(KrediKartiIslem islem) => islem != null && await UpsertPayloadAsync("kredi_karti_islemler", MapKrediKartiToPayload(islem));
        public async Task<bool> DeleteKrediKartiIslemAsync(int id, string? uuid = null) => await DeleteAsync("kredi_karti_islemler", id, uuid);
        public async Task<List<KrediKartiIslem>> PullKrediKartlariAsync(DateTime? since = null)
        {
            using var doc = await GetJsonAsync("kredi_karti_islemler");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new();
            var list = new List<KrediKartiIslem>();
            foreach (var el in doc.RootElement.EnumerateArray()) list.Add(MapPayloadToKrediKarti(el));
            return list;
        }

        private static Dictionary<string, object?> MapEftToPayload(EftIslem eft) => new()
        {
            ["id"] = eft.Id.ToString(),
            ["musteri_id"] = eft.MusteriId.ToString(),
            ["musteri_unvan"] = eft.MusteriUnvan ?? "",
            ["tarih"] = eft.Tarih.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["tutar"] = eft.Tutar,
            ["banka"] = eft.Banka ?? "",
            ["banka_id"] = eft.BankaId.ToString(),
            ["hesap_no"] = eft.HesapNo ?? "",
            ["dekont_no"] = eft.DekontNo ?? "",
            ["evrak_no"] = eft.EvrakNo ?? "",
            ["durum"] = eft.Durum ?? "Portföyde",
            ["yonlendirilen_cari_id"] = eft.YonlendirilenCariId?.ToString(),
            ["yonlendirilen_cari_unvan"] = eft.YonlendirilenCariUnvan ?? "",
            ["aciklama"] = eft.Aciklama ?? "",
            ["islem_turu"] = eft.IslemTuru ?? "",
            ["is_deleted"] = eft.IsDeleted,
            ["uuid"] = string.IsNullOrEmpty(eft.Uuid) ? (eft.Uuid = Guid.NewGuid().ToString()) : eft.Uuid,
            ["version"] = eft.Version <= 0 ? 1 : eft.Version,
            ["updated_at"] = (eft.UpdatedAt != default ? eft.UpdatedAt : DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        private static EftIslem MapPayloadToEft(JsonElement el)
        {
            var eft = new EftIslem();
            if (el.TryGetProperty("id", out var id)) eft.Id = ParseInt(id);
            if (el.TryGetProperty("musteri_id", out var mi)) eft.MusteriId = ParseInt(mi);
            if (el.TryGetProperty("musteri_unvan", out var mu) && mu.ValueKind == JsonValueKind.String) eft.MusteriUnvan = mu.GetString();
            if (el.TryGetProperty("tarih", out var trh) && trh.ValueKind == JsonValueKind.String && DateTime.TryParse(trh.GetString(), out var dt)) eft.Tarih = dt;
            if (el.TryGetProperty("tutar", out var tt)) eft.Tutar = ParseDecimal(tt);
            if (el.TryGetProperty("banka", out var b) && b.ValueKind == JsonValueKind.String) eft.Banka = b.GetString();
            if (el.TryGetProperty("banka_id", out var bi)) eft.BankaId = ParseInt(bi);
            if (el.TryGetProperty("hesap_no", out var hn) && hn.ValueKind == JsonValueKind.String) eft.HesapNo = hn.GetString();
            if (el.TryGetProperty("dekont_no", out var dn) && dn.ValueKind == JsonValueKind.String) eft.DekontNo = dn.GetString();
            if (el.TryGetProperty("evrak_no", out var en) && en.ValueKind == JsonValueKind.String) eft.EvrakNo = en.GetString() ?? "";
            if (el.TryGetProperty("durum", out var dm) && dm.ValueKind == JsonValueKind.String) eft.Durum = dm.GetString();
            if (el.TryGetProperty("yonlendirilen_cari_id", out var yci)) eft.YonlendirilenCariId = ParseNullableInt(yci);
            if (el.TryGetProperty("yonlendirilen_cari_unvan", out var ycu) && ycu.ValueKind == JsonValueKind.String) eft.YonlendirilenCariUnvan = ycu.GetString();
            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) eft.Aciklama = ac.GetString();
            if (el.TryGetProperty("islem_turu", out var it) && it.ValueKind == JsonValueKind.String) eft.IslemTuru = it.GetString();
            if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) eft.IsDeleted = true;
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) eft.Uuid = uu.GetString()!;
            else eft.Uuid = eft.Id > 0 ? $"legacy-eft-{eft.Id}" : "";
            if (el.TryGetProperty("version", out var vr)) eft.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) eft.UpdatedAt = dtUa;
            return eft;
        }

        public async Task<bool> SyncEftIslemAsync(EftIslem islem) => islem != null && await UpsertPayloadAsync("eft_islemler", MapEftToPayload(islem));
        public async Task<bool> DeleteEftIslemAsync(int id, string? uuid = null) => await DeleteAsync("eft_islemler", id, uuid);
        public async Task<List<EftIslem>> PullEftIslemleriAsync(DateTime? since = null)
        {
            using var doc = await GetJsonAsync("eft_islemler");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new();
            var list = new List<EftIslem>();
            foreach (var el in doc.RootElement.EnumerateArray()) list.Add(MapPayloadToEft(el));
            return list;
        }

        // ==========================================
        // GÖREVLER (KANBAN) & PERSONELLER
        // ==========================================
        private static Dictionary<string, object?> MapGorevToPayload(Gorev g) => new()
        {
            ["id"] = g.Id.ToString(),
            ["uuid"] = string.IsNullOrEmpty(g.Uuid) ? (g.Uuid = Guid.NewGuid().ToString()) : g.Uuid,
            ["baslik"] = g.Baslik ?? "",
            ["aciklama"] = g.Aciklama ?? "",
            ["atanan_personel_id"] = g.AtananPersonelId,
            ["atanan_personel_ad"] = g.AtananPersonelAd ?? "",
            ["durum"] = g.Durum ?? "Yapılacak",
            ["oncelik"] = g.Oncelik ?? "Normal",
            ["son_tarih"] = g.SonTarih.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["bitis_tarihi"] = g.BitisTarihi != default ? g.BitisTarihi.ToString("yyyy-MM-ddTHH:mm:ssZ") : null,
            ["olusturma_tarihi"] = g.OlusturmaTarihi.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["is_deleted"] = g.IsDeleted,
            ["version"] = g.Version <= 0 ? 1 : g.Version,
            ["updated_at"] = (g.UpdatedAt != default ? g.UpdatedAt : DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        private static Gorev MapPayloadToGorev(JsonElement el)
        {
            var g = new Gorev();
            if (el.TryGetProperty("id", out var id)) g.Id = ParseInt(id);
            if (el.TryGetProperty("baslik", out var b) && b.ValueKind == JsonValueKind.String) g.Baslik = b.GetString();
            if (el.TryGetProperty("aciklama", out var a) && a.ValueKind == JsonValueKind.String) g.Aciklama = a.GetString();
            if (el.TryGetProperty("atanan_personel_id", out var pi)) g.AtananPersonelId = ParseInt(pi);
            else if (el.TryGetProperty("atananPersonelId", out var pi2)) g.AtananPersonelId = ParseInt(pi2);
            if (el.TryGetProperty("atanan_personel_ad", out var pa) && pa.ValueKind == JsonValueKind.String) g.AtananPersonelAd = pa.GetString();
            else if (el.TryGetProperty("atananPersonelAd", out var pa2) && pa2.ValueKind == JsonValueKind.String) g.AtananPersonelAd = pa2.GetString();
            if (el.TryGetProperty("durum", out var d) && d.ValueKind == JsonValueKind.String) g.Durum = d.GetString();
            if (el.TryGetProperty("oncelik", out var o) && o.ValueKind == JsonValueKind.String) g.Oncelik = o.GetString();
            if (el.TryGetProperty("son_tarih", out var st) && st.ValueKind == JsonValueKind.String && DateTime.TryParse(st.GetString(), out var std)) g.SonTarih = std;
            else if (el.TryGetProperty("sonTarih", out var st2) && st2.ValueKind == JsonValueKind.String && DateTime.TryParse(st2.GetString(), out var std2)) g.SonTarih = std2;
            if (el.TryGetProperty("bitis_tarihi", out var bt) && bt.ValueKind == JsonValueKind.String && DateTime.TryParse(bt.GetString(), out var btd)) g.BitisTarihi = btd;
            if (el.TryGetProperty("olusturma_tarihi", out var ot) && ot.ValueKind == JsonValueKind.String && DateTime.TryParse(ot.GetString(), out var otd)) g.OlusturmaTarihi = otd;
            else if (el.TryGetProperty("olusturmaTarihi", out var ot2) && ot2.ValueKind == JsonValueKind.String && DateTime.TryParse(ot2.GetString(), out var otd2)) g.OlusturmaTarihi = otd2;
            if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) g.IsDeleted = true;
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) g.Uuid = uu.GetString()!;
            else g.Uuid = g.Id > 0 ? $"legacy-gorev-{g.Id}" : "";
            if (el.TryGetProperty("version", out var vr)) g.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) g.UpdatedAt = dtUa;
            return g;
        }

        public async Task<bool> SyncGorevAsync(Gorev gorev) => gorev != null && await UpsertPayloadAsync("gorevler", MapGorevToPayload(gorev));
        public async Task<bool> DeleteGorevAsync(int id, string? uuid = null) => await DeleteAsync("gorevler", id, uuid);
        public async Task<List<Gorev>> PullGorevlerAsync()
        {
            using var doc = await GetJsonAsync("gorevler");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new();
            var list = new List<Gorev>();
            foreach (var el in doc.RootElement.EnumerateArray()) list.Add(MapPayloadToGorev(el));
            return list;
        }

        private static Dictionary<string, object?> MapPersonelToPayload(Personel p) => new()
        {
            ["id"] = p.Id > 0 ? (object)p.Id : null,
            ["uuid"] = string.IsNullOrEmpty(p.Uuid) ? (p.Uuid = Guid.NewGuid().ToString()) : p.Uuid,
            ["ad"] = p.Ad ?? "",
            ["soyad"] = p.Soyad ?? "",
            ["telefon"] = p.Telefon ?? "",
            ["gorevi"] = p.Gorevi ?? "",
            ["unvan"] = p.Unvan ?? "",
            ["email"] = p.Email ?? "",
            ["maas"] = p.Maas,
            ["ise_giris_tarihi"] = p.IseGirisTarihi.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["is_deleted"] = p.IsDeleted,
            ["version"] = p.Version <= 0 ? 1 : p.Version,
            ["updated_at"] = (p.UpdatedAt != default ? p.UpdatedAt : DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        private static Personel MapPayloadToPersonel(JsonElement el)
        {
            var p = new Personel();
            if (el.TryGetProperty("id", out var id)) p.Id = ParseInt(id);
            if (el.TryGetProperty("ad", out var a) && a.ValueKind == JsonValueKind.String) p.Ad = a.GetString();
            if (el.TryGetProperty("soyad", out var s) && s.ValueKind == JsonValueKind.String) p.Soyad = s.GetString();
            if (el.TryGetProperty("telefon", out var t) && t.ValueKind == JsonValueKind.String) p.Telefon = t.GetString();
            if (el.TryGetProperty("gorevi", out var g) && g.ValueKind == JsonValueKind.String) p.Gorevi = g.GetString();
            if (el.TryGetProperty("unvan", out var u) && u.ValueKind == JsonValueKind.String) p.Unvan = u.GetString();
            if (el.TryGetProperty("email", out var e) && e.ValueKind == JsonValueKind.String) p.Email = e.GetString();
            if (el.TryGetProperty("maas", out var m)) p.Maas = ParseDecimal(m);
            if (el.TryGetProperty("ise_giris_tarihi", out var ig) && ig.ValueKind == JsonValueKind.String && DateTime.TryParse(ig.GetString(), out var igd)) p.IseGirisTarihi = igd;
            if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) p.IsDeleted = true;
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) p.Uuid = uu.GetString()!;
            else p.Uuid = p.Id > 0 ? $"legacy-personel-{p.Id}" : "";
            if (el.TryGetProperty("version", out var vr)) p.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) p.UpdatedAt = dtUa;
            return p;
        }

        public async Task<bool> SyncPersonelAsync(Personel personel) => personel != null && await UpsertPayloadAsync("personeller", MapPersonelToPayload(personel));
        public async Task<bool> DeletePersonelAsync(int id, string? uuid = null) => await DeleteAsync("personeller", id, uuid);
        public async Task<List<Personel>> PullPersonellerAsync()
        {
            using var doc = await GetJsonAsync("personeller");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new();
            var list = new List<Personel>();
            foreach (var el in doc.RootElement.EnumerateArray()) list.Add(MapPayloadToPersonel(el));
            return list;
        }

        // ==========================================
        // SATIŞ HEDEFLERİ
        // ==========================================
        public async Task<bool> SyncSatisHedefiAsync(SatisHedefi hedef) => hedef != null && await UpsertPayloadAsync("satis_hedefleri", new Dictionary<string, object?>
        {
            ["id"] = hedef.Id.ToString(),
            ["ay"] = hedef.Ay,
            ["yil"] = hedef.Yil,
            ["hedef_tutar"] = hedef.HedefTutari,
            ["hedef_tutari"] = hedef.HedefTutari
        });
        public async Task<bool> DeleteSatisHedefiAsync(int id, string? uuid = null) => await DeleteAsync("satis_hedefleri", id, uuid);

        public async Task<List<SatisHedefi>> PullSatisHedefleriAsync()
        {
            using var doc = await GetJsonAsync("satis_hedefleri");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new();
            var list = new List<SatisHedefi>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var h = new SatisHedefi();
                if (el.TryGetProperty("id", out var id)) h.Id = ParseInt(id);
                if (el.TryGetProperty("yil", out var y)) h.Yil = ParseInt(y);
                if (el.TryGetProperty("ay", out var a)) h.Ay = ParseInt(a);
                if (el.TryGetProperty("hedef_tutar", out var ht)) h.HedefTutari = ParseDecimal(ht);
                else if (el.TryGetProperty("hedef_tutari", out var ht2)) h.HedefTutari = ParseDecimal(ht2);
                list.Add(h);
            }
            return list;
        }

        public async Task<bool> SyncHaftalikSatisHedefiAsync(HaftalikSatisHedefi hedef) => hedef != null && await UpsertPayloadAsync("haftalik_satis_hedefleri", new Dictionary<string, object?>
        {
            ["id"] = hedef.Id.ToString(),
            ["yil"] = hedef.Yil,
            ["hafta"] = hedef.Hafta,
            ["hafta_no"] = hedef.Hafta,
            ["hedef_tutar"] = hedef.HedefTutari,
            ["hedef_tutari"] = hedef.HedefTutari
        });
        public async Task<bool> DeleteHaftalikSatisHedefiAsync(int id, string? uuid = null) => await DeleteAsync("haftalik_satis_hedefleri", id, uuid);

        public async Task<List<HaftalikSatisHedefi>> PullHaftalikSatisHedefleriAsync()
        {
            using var doc = await GetJsonAsync("haftalik_satis_hedefleri");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new();
            var list = new List<HaftalikSatisHedefi>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var h = new HaftalikSatisHedefi();
                if (el.TryGetProperty("id", out var id)) h.Id = ParseInt(id);
                if (el.TryGetProperty("yil", out var y)) h.Yil = ParseInt(y);
                if (el.TryGetProperty("hafta", out var hf)) h.Hafta = ParseInt(hf);
                else if (el.TryGetProperty("hafta_no", out var hn)) h.Hafta = ParseInt(hn);
                if (el.TryGetProperty("hedef_tutar", out var ht)) h.HedefTutari = ParseDecimal(ht);
                else if (el.TryGetProperty("hedef_tutari", out var ht2)) h.HedefTutari = ParseDecimal(ht2);
                list.Add(h);
            }
            return list;
        }

        public async Task<bool> SyncYillikSatisHedefiAsync(YillikSatisHedefi hedef) => hedef != null && await UpsertPayloadAsync("yillik_satis_hedefleri", new Dictionary<string, object?>
        {
            ["id"] = hedef.Id.ToString(),
            ["yil"] = hedef.Yil,
            ["hedef_tutar"] = hedef.HedefTutari,
            ["hedef_tutari"] = hedef.HedefTutari,
            ["aciklama"] = hedef.Aciklama ?? ""
        });
        public async Task<bool> DeleteYillikSatisHedefiAsync(int id, string? uuid = null) => await DeleteAsync("yillik_satis_hedefleri", id, uuid);

        public async Task<List<YillikSatisHedefi>> PullYillikSatisHedefleriAsync()
        {
            using var doc = await GetJsonAsync("yillik_satis_hedefleri");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new();
            var list = new List<YillikSatisHedefi>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var h = new YillikSatisHedefi();
                if (el.TryGetProperty("id", out var id)) h.Id = ParseInt(id);
                if (el.TryGetProperty("yil", out var y)) h.Yil = ParseInt(y);
                if (el.TryGetProperty("hedef_tutar", out var ht)) h.HedefTutari = ParseDecimal(ht);
                else if (el.TryGetProperty("hedef_tutari", out var ht2)) h.HedefTutari = ParseDecimal(ht2);
                if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) h.Aciklama = ac.GetString();
                list.Add(h);
            }
            return list;
        }

        // ==========================================
        // PORTFÖY KARTLARI
        // ==========================================
        private static Dictionary<string, object?> MapPortfoyToPayload(PortfoyKart p) => new()
        {
            ["id"] = p.Id,
            ["uuid"] = string.IsNullOrEmpty(p.Uuid) ? (p.Uuid = Guid.NewGuid().ToString()) : p.Uuid,
            ["portfoy_no"] = p.PortfoyNo ?? "",
            ["firma_ismi"] = p.FirmaIsmi ?? "",
            ["cari_tipi"] = p.CariTipi ?? "",
            ["yetkili_kisi"] = p.YetkiliKisi ?? "",
            ["vade_gunu"] = p.VadeGunu,
            ["risk_limiti"] = p.RiskLimiti,
            ["vkn"] = p.VKN ?? "",
            ["vergi_dairesi"] = p.VergiDairesi ?? "",
            ["gsm"] = p.GSM ?? "",
            ["telefon"] = p.Telefon ?? "",
            ["email"] = p.Email ?? "",
            ["web_sitesi"] = p.WebSitesi ?? "",
            ["ulke"] = p.Ulke ?? "Türkiye",
            ["il"] = p.Il ?? "",
            ["ilce"] = p.Ilce ?? "",
            ["adres"] = p.Adres ?? "",
            ["sevk_adresi"] = p.SevkAdresi ?? "",
            ["sektor"] = p.Sektor ?? "",
            ["aciklama"] = p.Aciklama ?? "",
            ["notlar"] = p.Notlar ?? "",
            ["acilis_bakiyesi"] = p.AcilisBakiyesi,
            ["is_deleted"] = p.IsDeleted,
            ["version"] = p.Version <= 0 ? 1 : p.Version,
            ["updated_at"] = (p.UpdatedAt != default ? p.UpdatedAt : DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        private static PortfoyKart MapPayloadToPortfoy(JsonElement el)
        {
            var p = new PortfoyKart();
            if (el.TryGetProperty("id", out var id)) p.Id = ParseInt(id);
            if (el.TryGetProperty("portfoy_no", out var pn) && pn.ValueKind == JsonValueKind.String) p.PortfoyNo = pn.GetString();
            if (el.TryGetProperty("firma_ismi", out var fi) && fi.ValueKind == JsonValueKind.String) p.FirmaIsmi = fi.GetString();
            if (el.TryGetProperty("cari_tipi", out var ct) && ct.ValueKind == JsonValueKind.String) p.CariTipi = ct.GetString();
            if (el.TryGetProperty("yetkili_kisi", out var yk) && yk.ValueKind == JsonValueKind.String) p.YetkiliKisi = yk.GetString();
            if (el.TryGetProperty("vade_gunu", out var vg)) p.VadeGunu = ParseInt(vg);
            if (el.TryGetProperty("risk_limiti", out var rl)) p.RiskLimiti = ParseDecimal(rl);
            if (el.TryGetProperty("vkn", out var vkn) && vkn.ValueKind == JsonValueKind.String) p.VKN = vkn.GetString();
            if (el.TryGetProperty("vergi_dairesi", out var vd) && vd.ValueKind == JsonValueKind.String) p.VergiDairesi = vd.GetString();
            if (el.TryGetProperty("gsm", out var gsm) && gsm.ValueKind == JsonValueKind.String) p.GSM = gsm.GetString();
            if (el.TryGetProperty("telefon", out var tel) && tel.ValueKind == JsonValueKind.String) p.Telefon = tel.GetString();
            if (el.TryGetProperty("email", out var em) && em.ValueKind == JsonValueKind.String) p.Email = em.GetString();
            if (el.TryGetProperty("web_sitesi", out var ws) && ws.ValueKind == JsonValueKind.String) p.WebSitesi = ws.GetString();
            if (el.TryGetProperty("ulke", out var ulk) && ulk.ValueKind == JsonValueKind.String) p.Ulke = ulk.GetString() ?? "Türkiye";
            if (el.TryGetProperty("il", out var il) && il.ValueKind == JsonValueKind.String) p.Il = il.GetString();
            if (el.TryGetProperty("ilce", out var ilc) && ilc.ValueKind == JsonValueKind.String) p.Ilce = ilc.GetString();
            if (el.TryGetProperty("adres", out var adr) && adr.ValueKind == JsonValueKind.String) p.Adres = adr.GetString();
            if (el.TryGetProperty("sevk_adresi", out var sa) && sa.ValueKind == JsonValueKind.String) p.SevkAdresi = sa.GetString();
            if (el.TryGetProperty("sektor", out var sk) && sk.ValueKind == JsonValueKind.String) p.Sektor = sk.GetString();
            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) p.Aciklama = ac.GetString();
            if (el.TryGetProperty("notlar", out var n) && n.ValueKind == JsonValueKind.String) p.Notlar = n.GetString();
            if (el.TryGetProperty("acilis_bakiyesi", out var ab)) p.AcilisBakiyesi = ParseDecimal(ab);
            if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) p.IsDeleted = true;
            if (el.TryGetProperty("version", out var vr)) p.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) p.UpdatedAt = dtUa;
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) p.Uuid = uu.GetString()!;
            else p.Uuid = p.Id > 0 ? $"legacy-portfoy-{p.Id}" : "";
            return p;
        }

                public async Task<bool> SyncPortfoyAsync(PortfoyKart portfoy) => await UpsertPayloadAsync("portfoy_kartlar", MapPortfoyToPayload(portfoy));
        public async Task<bool> DeletePortfoyAsync(int id, string? uuid = null) => await DeleteAsync("portfoy_kartlar", id, uuid);
        public async Task<List<PortfoyKart>> PullPortfoyAsync()
        {
            using var doc = await GetJsonAsync("portfoy_kartlar");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new();
            var list = new List<PortfoyKart>();
            foreach (var el in doc.RootElement.EnumerateArray()) list.Add(MapPayloadToPortfoy(el));
            return list;
        }

        // ==========================================
        // STOK SAYIM
        // ==========================================
        private static Dictionary<string, object?> MapStokSayimFisiToPayload(StokSayimFisi fis) => new()
        {
            ["id"] = fis.Id.ToString(),
            ["uuid"] = string.IsNullOrEmpty(fis.Uuid) ? (fis.Uuid = Guid.NewGuid().ToString()) : fis.Uuid,
            ["fis_no"] = fis.FisNo ?? "",
            ["tarih"] = fis.Tarih.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["aciklama"] = fis.Aciklama ?? "",
            ["sayim_yapan"] = fis.SayimYapan ?? "",
            ["is_applied"] = fis.IsApplied,
            ["is_deleted"] = fis.IsDeleted,
            ["version"] = fis.Version <= 0 ? 1 : fis.Version,
            ["updated_at"] = (fis.UpdatedAt != default ? fis.UpdatedAt : DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        private static StokSayimFisi MapPayloadToStokSayimFisi(JsonElement el)
        {
            var f = new StokSayimFisi();
            if (el.TryGetProperty("id", out var id)) f.Id = ParseInt(id);
            if (el.TryGetProperty("fis_no", out var fn) && fn.ValueKind == JsonValueKind.String) f.FisNo = fn.GetString();
            if (el.TryGetProperty("tarih", out var trh) && trh.ValueKind == JsonValueKind.String && DateTime.TryParse(trh.GetString(), out var dt)) f.Tarih = dt;
            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) f.Aciklama = ac.GetString();
            if (el.TryGetProperty("sayim_yapan", out var sy) && sy.ValueKind == JsonValueKind.String) f.SayimYapan = sy.GetString();
            if (el.TryGetProperty("is_applied", out var ia)) f.IsApplied = ia.ValueKind == JsonValueKind.True;
            if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) f.IsDeleted = true;
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) f.Uuid = uu.GetString()!;
            else f.Uuid = f.Id > 0 ? $"legacy-stoksayim-{f.Id}" : "";
            if (el.TryGetProperty("version", out var vr)) f.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) f.UpdatedAt = dtUa;
            return f;
        }

        private static Dictionary<string, object?> MapStokSayimDetayToPayload(StokSayimDetay d) => new()
        {
            ["id"] = d.Id.ToString(),
            ["uuid"] = string.IsNullOrEmpty(d.Uuid) ? (d.Uuid = Guid.NewGuid().ToString()) : d.Uuid,
            ["fis_id"] = d.FisId.ToString(),
            ["fisi_id"] = d.FisId.ToString(),
            ["stok_id"] = d.StokId.ToString(),
            ["stok_kodu"] = d.StokKodu ?? "",
            ["stok_adi"] = d.StokAdi ?? "",
            ["mevcut_miktar"] = d.MevcutMiktar,
            ["sayilan_miktar"] = d.SayilanMiktar,
            ["fark"] = d.Fark,
            ["aciklama"] = d.Aciklama ?? ""
        };

        private static StokSayimDetay MapPayloadToStokSayimDetay(JsonElement el)
        {
            var d = new StokSayimDetay();
            if (el.TryGetProperty("id", out var id)) d.Id = ParseInt(id);
            if (el.TryGetProperty("fis_id", out var fi)) d.FisId = ParseInt(fi);
            else if (el.TryGetProperty("fisi_id", out var fi2)) d.FisId = ParseInt(fi2);
            if (el.TryGetProperty("stok_id", out var si)) d.StokId = ParseInt(si);
            if (el.TryGetProperty("stok_kodu", out var sk) && sk.ValueKind == JsonValueKind.String) d.StokKodu = sk.GetString();
            if (el.TryGetProperty("stok_adi", out var sa) && sa.ValueKind == JsonValueKind.String) d.StokAdi = sa.GetString();
            if (el.TryGetProperty("mevcut_miktar", out var mm)) d.MevcutMiktar = ParseDecimal(mm);
            if (el.TryGetProperty("sayilan_miktar", out var sm)) d.SayilanMiktar = ParseDecimal(sm);
            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) d.Aciklama = ac.GetString();
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) d.Uuid = uu.GetString()!;
            else d.Uuid = d.Id > 0 ? $"legacy-stoksayim_detay-{d.Id}" : "";
            return d;
        }

                public async Task<bool> SyncStokSayimFisiAsync(StokSayimFisi fis) => await UpsertPayloadAsync("stok_sayim_fisileri", MapStokSayimFisiToPayload(fis));
        public async Task<bool> DeleteStokSayimFisiAsync(int id, string? uuid = null)
        {
            if (!string.IsNullOrEmpty(uuid))
                await DeleteFilteredAsync("stok_sayim_detaylari", $"fisi_uuid=eq.{uuid}");
            if (id > 0)
                await DeleteFilteredAsync("stok_sayim_detaylari", $"fisi_id=eq.{id}");
            return await DeleteAsync("stok_sayim_fisileri", id, uuid);
        }

        public async Task<bool> SyncStokSayimDetaylarAsync(int fisId, string? fisUuid, List<StokSayimDetay> detaylar)
        {
            if (!string.IsNullOrEmpty(fisUuid))
                await DeleteFilteredAsync("stok_sayim_detaylari", $"fisi_uuid=eq.{fisUuid}");
            if (fisId > 0)
                await DeleteFilteredAsync("stok_sayim_detaylari", $"fisi_id=eq.{fisId}");
            if (detaylar != null && detaylar.Any())
            {
                var payloads = detaylar.Select(d => (object)MapStokSayimDetayToPayload(d)).ToList();
                return await UpsertBatchPayloadAsync("stok_sayim_detaylari", payloads);
            }
            return true;
        }

        public async Task<bool> SyncStokSayimDetaylarAsync(int fisId, List<StokSayimDetay> detaylar)
            => await SyncStokSayimDetaylarAsync(fisId, null, detaylar);

        public async Task<List<StokSayimFisi>> PullStokSayimlarAsync()
        {
            using var doc = await GetJsonAsync("stok_sayim_fisileri");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new();
            var list = new List<StokSayimFisi>();
            foreach (var el in doc.RootElement.EnumerateArray()) list.Add(MapPayloadToStokSayimFisi(el));
            return list;
        }

        public async Task<List<StokSayimDetay>> PullStokSayimDetaylarAsync(int fisId)
        {
            using var doc = await GetJsonAsync("stok_sayim_detaylari", $"or=(fis_id.eq.{fisId},fisi_id.eq.{fisId})");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new();
            var list = new List<StokSayimDetay>();
            foreach (var el in doc.RootElement.EnumerateArray()) list.Add(MapPayloadToStokSayimDetay(el));
            return list;
        }

        // ==========================================
        // DÖVİZ KURLARI & BELGE ARŞİV
        // ==========================================
        public async Task<bool> SyncDovizKurAsync(DovizKur kur) => kur != null && await UpsertPayloadAsync("doviz_kurlari", new Dictionary<string, object?>
        {
            ["id"] = kur.Id.ToString(),
            ["kod"] = kur.Kod ?? "",
            ["alis"] = kur.Alis,
            ["satis"] = kur.Satis,
            ["tarih"] = kur.Tarih.ToString("yyyy-MM-ddTHH:mm:ssZ")
        });
        public async Task<bool> DeleteDovizKurAsync(int id, string? uuid = null) => await DeleteAsync("doviz_kurlari", id, uuid);

        public async Task<List<DovizKur>?> PullDovizKurlariAsync()
        {
            using var doc = await GetJsonAsync("doviz_kurlari");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new();
            var list = new List<DovizKur>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var k = new DovizKur();
                if (el.TryGetProperty("id", out var id)) k.Id = ParseInt(id);
                if (el.TryGetProperty("kod", out var kd) && kd.ValueKind == JsonValueKind.String) k.Kod = kd.GetString() ?? "";
                if (el.TryGetProperty("alis", out var al)) k.Alis = ParseDecimal(al);
                if (el.TryGetProperty("satis", out var st)) k.Satis = ParseDecimal(st);
                if (el.TryGetProperty("tarih", out var trh) && trh.ValueKind == JsonValueKind.String && DateTime.TryParse(trh.GetString(), out var dt)) k.Tarih = dt;
                list.Add(k);
            }
            return list;
        }

        public async Task<bool> SyncBelgeArsivAsync(BelgeArsiv belge) => belge != null && await UpsertPayloadAsync("belge_arsiv", new Dictionary<string, object?>
        {
            ["id"] = belge.Id.ToString(),
            ["ad"] = belge.Ad ?? "",
            ["belge_adi"] = belge.Ad ?? "",
            ["kategori"] = belge.Kategori ?? "",
            ["tur"] = belge.Tur ?? "",
            ["belge_turu"] = belge.Tur ?? "",
            ["boyut"] = belge.Boyut ?? "",
            ["tarih"] = belge.Tarih.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["veri"] = belge.Veri ?? "",
            ["yol"] = belge.Yol ?? "",
            ["dosya_yolu"] = belge.Yol ?? "",
            ["is_deleted"] = belge.IsDeleted
        });

        public async Task<bool> DeleteBelgeArsivAsync(int id, string? uuid = null) => await DeleteAsync("belge_arsiv", id, uuid);

        public async Task<List<BelgeArsiv>?> PullBelgeArsivAsync(DateTime? since = null)
        {
            using var doc = await GetJsonAsync("belge_arsiv");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new();
            var list = new List<BelgeArsiv>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var b = new BelgeArsiv();
                if (el.TryGetProperty("id", out var id)) b.Id = ParseInt(id);
                if (el.TryGetProperty("ad", out var ad) && ad.ValueKind == JsonValueKind.String) b.Ad = ad.GetString();
                else if (el.TryGetProperty("belge_adi", out var ba) && ba.ValueKind == JsonValueKind.String) b.Ad = ba.GetString();
                if (el.TryGetProperty("kategori", out var kt) && kt.ValueKind == JsonValueKind.String) b.Kategori = kt.GetString();
                if (el.TryGetProperty("tur", out var tr) && tr.ValueKind == JsonValueKind.String) b.Tur = tr.GetString();
                else if (el.TryGetProperty("belge_turu", out var bt) && bt.ValueKind == JsonValueKind.String) b.Tur = bt.GetString();
                if (el.TryGetProperty("boyut", out var by) && by.ValueKind == JsonValueKind.String) b.Boyut = by.GetString();
                if (el.TryGetProperty("tarih", out var trh) && trh.ValueKind == JsonValueKind.String && DateTime.TryParse(trh.GetString(), out var dt)) b.Tarih = dt;
                else if (el.TryGetProperty("yukleme_tarihi", out var yt) && yt.ValueKind == JsonValueKind.String && DateTime.TryParse(yt.GetString(), out var ytd)) b.Tarih = ytd;
                if (el.TryGetProperty("veri", out var vr) && vr.ValueKind == JsonValueKind.String) b.Veri = vr.GetString();
                if (el.TryGetProperty("yol", out var yl) && yl.ValueKind == JsonValueKind.String) b.Yol = yl.GetString();
                else if (el.TryGetProperty("dosya_yolu", out var dy) && dy.ValueKind == JsonValueKind.String) b.Yol = dy.GetString();
                if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) b.IsDeleted = true;
                list.Add(b);
            }
            return list;
        }

        // ==========================================
        // MÜŞTERİ TAKİP
        // ==========================================
        private static Dictionary<string, object?> MapMusteriTakipKlasorToPayload(MusteriTakipKlasor k) => new()
        {
            ["id"] = k.Id,
            ["uuid"] = string.IsNullOrEmpty(k.Uuid) ? (k.Uuid = Guid.NewGuid().ToString()) : k.Uuid,
            ["cari_id"] = k.CariId,
            ["cari_unvan"] = k.CariUnvan,
            ["cari_kod"] = k.CariKod,
            ["telefon"] = k.Telefon,
            ["yetkili"] = k.Yetkili,
            ["etiket"] = k.Etiket,
            ["klasor_adi"] = k.Etiket ?? k.CariUnvan,
            ["renk"] = string.IsNullOrWhiteSpace(k.Renk) ? "#3B82F6" : k.Renk,
            ["aciklama"] = k.Aciklama,
            ["is_deleted"] = k.IsDeleted,
            ["created_at"] = (k.OlusturmaTarihi == default ? DateTime.Now : k.OlusturmaTarihi).ToString("o"),
            ["son_islem_tarihi"] = (k.SonIslemTarihi == default ? DateTime.Now : k.SonIslemTarihi).ToString("o")
        };

        private static Dictionary<string, object?> MapMusteriTakipDetayToPayload(MusteriTakipDetay d) => new()
        {
            ["id"] = d.Id,
            ["uuid"] = string.IsNullOrEmpty(d.Uuid) ? (d.Uuid = Guid.NewGuid().ToString()) : d.Uuid,
            ["klasor_id"] = d.KlasorId,
            ["cari_id"] = d.CariId,
            ["baslik"] = d.Baslik,
            ["icerik"] = d.Icerik,
            ["tip"] = d.Tip ?? "Not",
            ["fiyat_bilgisi"] = d.FiyatBilgisi,
            ["para_birimi"] = d.ParaBirimi ?? "₺",
            ["dosya_yolu"] = d.DosyaYolu,
            ["gorsel_base64"] = d.GorselBase64,
            ["tarih"] = (d.Tarih == default ? DateTime.Now : d.Tarih).ToString("o"),
            ["is_deleted"] = d.IsDeleted
        };

        public async Task<bool> SyncMusteriTakipKlasorAsync(MusteriTakipKlasor klasor)
        {
            if (klasor == null) return false;
            return await UpsertPayloadAsync("musteri_takip_klasorler", MapMusteriTakipKlasorToPayload(klasor));
        }

        public async Task<bool> DeleteMusteriTakipKlasorAsync(int id)
        {
            await DeleteFilteredAsync("musteri_takip_detaylar", $"klasor_id=eq.{id}");
            return await DeleteAsync("musteri_takip_klasorler", id);
        }

        public async Task<bool> SyncMusteriTakipDetayAsync(MusteriTakipDetay detay)
        {
            if (detay == null) return false;
            return await UpsertPayloadAsync("musteri_takip_detaylar", MapMusteriTakipDetayToPayload(detay));
        }

        public async Task<bool> DeleteMusteriTakipDetayAsync(int id)
        {
            return await DeleteAsync("musteri_takip_detaylar", id);
        }

        public async Task<List<MusteriTakipKlasor>> PullMusteriTakipKlasorlerAsync()
        {
            using var doc = await GetJsonAsync("musteri_takip_klasorler");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new();
            var list = new List<MusteriTakipKlasor>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var k = new MusteriTakipKlasor();
                if (el.TryGetProperty("id", out var id)) k.Id = ParseInt(id);
                if (el.TryGetProperty("cari_id", out var ci)) k.CariId = ParseInt(ci);
                if (el.TryGetProperty("cari_unvan", out var cu) && cu.ValueKind == JsonValueKind.String) k.CariUnvan = cu.GetString() ?? "";
                if (el.TryGetProperty("cari_kod", out var ck) && ck.ValueKind == JsonValueKind.String) k.CariKod = ck.GetString();
                if (el.TryGetProperty("telefon", out var tel) && tel.ValueKind == JsonValueKind.String) k.Telefon = tel.GetString();
                if (el.TryGetProperty("yetkili", out var yk) && yk.ValueKind == JsonValueKind.String) k.Yetkili = yk.GetString();
                if (el.TryGetProperty("etiket", out var et) && et.ValueKind == JsonValueKind.String) k.Etiket = et.GetString();
                else if (el.TryGetProperty("klasor_adi", out var ka) && ka.ValueKind == JsonValueKind.String) k.Etiket = ka.GetString();
                if (el.TryGetProperty("renk", out var rn) && rn.ValueKind == JsonValueKind.String) k.Renk = rn.GetString() ?? "#3B82F6";
                if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) k.Aciklama = ac.GetString();
                if (el.TryGetProperty("son_islem_tarihi", out var sit) && sit.ValueKind == JsonValueKind.String && DateTime.TryParse(sit.GetString(), out var sitVal)) k.SonIslemTarihi = sitVal;
                else if (el.TryGetProperty("updated_at", out var upa) && upa.ValueKind == JsonValueKind.String && DateTime.TryParse(upa.GetString(), out var upaVal)) k.SonIslemTarihi = upaVal;
                if (el.TryGetProperty("created_at", out var cra) && cra.ValueKind == JsonValueKind.String && DateTime.TryParse(cra.GetString(), out var craVal)) k.OlusturmaTarihi = craVal;
                if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) k.IsDeleted = true;
                if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) k.Uuid = uu.GetString()!;
                else k.Uuid = k.Id > 0 ? $"legacy-mtklasor-{k.Id}" : "";
                list.Add(k);
            }
            return list;
        }

        public async Task<List<MusteriTakipDetay>> PullMusteriTakipDetaylarAsync()
        {
            using var doc = await GetJsonAsync("musteri_takip_detaylar");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new();
            var list = new List<MusteriTakipDetay>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var d = new MusteriTakipDetay();
                if (el.TryGetProperty("id", out var id)) d.Id = ParseInt(id);
                if (el.TryGetProperty("klasor_id", out var ki)) d.KlasorId = ParseInt(ki);
                if (el.TryGetProperty("cari_id", out var ci)) d.CariId = ParseInt(ci);
                if (el.TryGetProperty("baslik", out var bs) && bs.ValueKind == JsonValueKind.String) d.Baslik = bs.GetString() ?? "";
                if (el.TryGetProperty("icerik", out var ic) && ic.ValueKind == JsonValueKind.String) d.Icerik = ic.GetString();
                else if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) d.Icerik = ac.GetString();
                if (el.TryGetProperty("tip", out var tp) && tp.ValueKind == JsonValueKind.String) d.Tip = tp.GetString() ?? "Not";
                if (el.TryGetProperty("fiyat_bilgisi", out var fb)) d.FiyatBilgisi = ParseDecimal(fb);
                if (el.TryGetProperty("para_birimi", out var pb) && pb.ValueKind == JsonValueKind.String) d.ParaBirimi = pb.GetString() ?? "₺";
                if (el.TryGetProperty("dosya_yolu", out var dy) && dy.ValueKind == JsonValueKind.String) d.DosyaYolu = dy.GetString();
                if (el.TryGetProperty("gorsel_base64", out var gb) && gb.ValueKind == JsonValueKind.String) d.GorselBase64 = gb.GetString();
                if (el.TryGetProperty("tarih", out var trh) && trh.ValueKind == JsonValueKind.String && DateTime.TryParse(trh.GetString(), out var dt)) d.Tarih = dt;
                if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) d.IsDeleted = true;
                if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(uu.GetString())) d.Uuid = uu.GetString()!;
                else d.Uuid = d.Id > 0 ? $"legacy-mtdetay-{d.Id}" : "";
                list.Add(d);
            }
            return list;
        }

        public async Task ClearCloudTablesAsync(string tenantId = "default")
        {
            if (!IsConnected) return;

            var tenantsToClear = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(tenantId)) tenantsToClear.Add(tenantId);
            if (!string.IsNullOrWhiteSpace(CurrentTenantId)) tenantsToClear.Add(CurrentTenantId);
            tenantsToClear.Add("default");
            tenantsToClear.Add(DatabaseService.DefaultTenantId);

            // 0. Invoke PostgreSQL SECURITY DEFINER RPC `factory_reset_all_tables` first (atomic server-side wipe)
            try
            {
                using var rpcAllReq = CreateRequest(HttpMethod.Post, "rpc/factory_reset_all_tables");
                rpcAllReq.Content = new StringContent("{\"p_tenant_id\":null}", Encoding.UTF8, "application/json");
                using var rpcAllRes = await _http.SendAsync(rpcAllReq);
            }
            catch { }

            foreach (var tId in tenantsToClear)
            {
                try
                {
                    using var rpcTenantReq = CreateRequest(HttpMethod.Post, "rpc/factory_reset_all_tables");
                    var rpcBody = JsonSerializer.Serialize(new Dictionary<string, string?> { ["p_tenant_id"] = tId });
                    rpcTenantReq.Content = new StringContent(rpcBody, Encoding.UTF8, "application/json");
                    using var rpcTenantRes = await _http.SendAsync(rpcTenantReq);
                }
                catch { }
            }

            // Delete rows in reverse dependency order (detail/child tables first, intermediate/invoices next, parent master tables last)
            // to satisfy foreign key constraints in PostgreSQL.
            // Note: 'firma_profili' and 'kullanicilar' are preserved to retain system & connection settings.
            var wave1DetailTables = new[]
            {
                "fatura_detaylar",
                "fatura_kalemleri",
                "fatura_kalem_sablonlari",
                "siparis_detaylar",
                "teklif_detaylar",
                "stok_sayim_detaylari",
                "stok_sayim_fisileri",
                "stok_grup_tanimlari",
                "maliyet_merkezi_tanimlari",
                "acilis_kapanis_fisi_detaylar",
                "acilis_kapanis_fisileri",
                "musteri_takip_detaylar",
                "musteri_takip_detay",
                "kredi_karti_islemler",
                "eft_islemler",
                "cekler",
                "senetler",
                "cari_hareketler",
                "stok_hareketler",
                "banka_hareketler",
                "kasa_hareketler",
                "gorevler",
                "personeller",
                "satis_hedefleri",
                "haftalik_satis_hedefleri",
                "yillik_satis_hedefleri",
                "portfoy_kartlar",
                "belge_arsiv",
                "doviz_kurlari",
                "sms_gecmisi",
                "silinen_kayitlar",
                "senkron_catismalari",
                "sync_conflicts",
                "sync_conflict_logs",
                "idempotent_transactions",
                "bildirimler",
                "giderler",
                "gider_kategorileri"
            };

            var wave2OperationalTables = new[]
            {
                "musteri_takip_klasorler",
                "musteri_takip",
                "faturalar",
                "siparisler",
                "teklifler",
                "notlar",
                "mobil_gelen_kutusu",
                "security_requests",
                "user_security_states",
                "mali_yillar"
            };

            var wave3MasterTables = new[]
            {
                "cariler",
                "stoklar",
                "bankalar",
                "kasalar"
            };

            async Task DeleteTableUnconditionalAsync(string table)
            {
                try
                {
                    string filter = table == "mali_yillar"
                        ? "yil=gte.0"
                        : (table == "notlar"
                            ? "id=neq.999999"
                            : "id=not.is.null");
                    using var reqAll = CreateRequest(HttpMethod.Delete, $"{table}?{filter}");
                    reqAll.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
                    using var resAll = await _http.SendAsync(reqAll);
                }
                catch { }

                try
                {
                    if (table != "notlar" && table != "kullanicilar" && table != "firma_profili" && table != "mali_yillar")
                    {
                        using var reqTenant = CreateRequest(HttpMethod.Delete, $"{table}?tenant_id=not.is.null");
                        reqTenant.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
                        using var resTenant = await _http.SendAsync(reqTenant);
                    }
                }
                catch { }
            }

            // Three-pass sequential wave deletion satisfying foreign keys
            for (int pass = 1; pass <= 2; pass++)
            {
                await Task.WhenAll(wave1DetailTables.Select(DeleteTableUnconditionalAsync));
                await Task.WhenAll(wave2OperationalTables.Select(DeleteTableUnconditionalAsync));
                await Task.WhenAll(wave3MasterTables.Select(DeleteTableUnconditionalAsync));
            }

            // Also clear all tenant notes and all mali_yillar notes completely
            foreach (var tId in tenantsToClear)
            {
                try
                {
                    string noteTitle = tId == "default" ? "__MALI_YILLAR__" : $"__MALI_YILLAR_{tId}__";
                    using var req = CreateRequest(HttpMethod.Delete, $"notlar?baslik=eq.{Uri.EscapeDataString(noteTitle)}");
                    await _http.SendAsync(req);
                }
                catch { }

                try
                {
                    string activeTitle = tId == "default" ? "__AKTIF_MALI_YIL__" : $"__AKTIF_MALI_YIL_{tId}__";
                    using var req = CreateRequest(HttpMethod.Delete, $"notlar?baslik=eq.{Uri.EscapeDataString(activeTitle)}");
                    await _http.SendAsync(req);
                }
                catch { }
            }
            try
            {
                using var reqLegacy = CreateRequest(HttpMethod.Delete, "notlar?id=eq.999998");
                await _http.SendAsync(reqLegacy);
            }
            catch { }
            try
            {
                using var reqNotesLike = CreateRequest(HttpMethod.Delete, "notlar?baslik=like.*__MALI_YILLAR*");
                await _http.SendAsync(reqNotesLike);
            }
            catch { }
            try
            {
                using var reqActiveLike = CreateRequest(HttpMethod.Delete, "notlar?baslik=like.*__AKTIF_MALI_YIL*");
                await _http.SendAsync(reqActiveLike);
            }
            catch { }
            try
            {
                using var reqNotes1 = CreateRequest(HttpMethod.Delete, "notlar?baslik=like.__MALI_YILLAR*");
                await _http.SendAsync(reqNotes1);
            }
            catch { }
            try
            {
                using var reqNotes2 = CreateRequest(HttpMethod.Delete, "notlar?baslik=like.__AKTIF_MALI_YIL*");
                await _http.SendAsync(reqNotes2);
            }
            catch { }
            try
            {
                using var reqMaliAll = CreateRequest(HttpMethod.Delete, "mali_yillar?yil=gte.0");
                await _http.SendAsync(reqMaliAll);
            }
            catch { }

            System.Diagnostics.Debug.WriteLine($"[CloudSync] All cloud business tables cleared successfully.");
        }

        // ==========================================
        // BULK INITIAL PUSH (LOCAL SQLITE -> SUPABASE)
        // ==========================================
        public async Task PushAllDataAsync(
            List<CariKart> cariler, 
            List<CariHareket> cariHareketler, 
            List<StokKart> stoklar, 
            List<StokHareket> stokHareketler, 
            List<Fatura> faturalar, 
            List<FaturaDetay> faturaDetaylar, 
            List<Siparis> siparisler, 
            List<SiparisDetay> siparisDetaylar, 
            List<Teklif> teklifler, 
            List<TeklifDetay> teklifDetaylar, 
            List<BankaKart> bankalar, 
            List<KasaHareket> kasaHareketler, 
            List<BankaHareket> bankaHareketler, 
            List<Cek> cekler, 
            List<Senet> senetler, 
            List<KrediKartiIslem> kkIslemler, 
            List<EftIslem> eftIslemler, 
            List<DovizKur> kurlar, 
            List<BelgeArsiv> belgeler,
            List<Note>? notes = null,
            List<Gorev>? gorevler = null,
            List<Personel>? personeller = null,
            List<SatisHedefi>? hedefler = null,
            List<HaftalikSatisHedefi>? haftalikHedefler = null,
            List<YillikSatisHedefi>? yillikHedefler = null,
            List<StokSayimFisi>? stokSayimlar = null,
            List<StokSayimDetay>? stokSayimDetaylar = null,
            List<PortfoyKart>? portfoyler = null)
        {
            if (!IsConnected) return;

            await SafeRun(async () =>
            {
                // 1. Master records (Cariler, Stoklar, Bankalar, Kasalar)
                if (cariler?.Any() == true)
                {
                    var cleanCariler = cariler.Where(c => !IsTestCari(c)).ToList();
                    if (cleanCariler.Any())
                    {
                        var payloads = cleanCariler.Select(MapCariToPayload).ToList<object>();
                        await UpsertBatchPayloadAsync("cariler", payloads);
                    }
                }
                if (stoklar?.Any() == true)
                {
                    var cleanStoklar = stoklar.Where(s => !IsTestStok(s)).ToList();
                    if (cleanStoklar.Any())
                    {
                        var payloads = cleanStoklar.Select(MapStokToPayload).ToList<object>();
                        await UpsertBatchPayloadAsync("stoklar", payloads);
                    }
                }
                if (bankalar?.Any() == true)
                {
                    var cleanBankalar = bankalar.Where(b => !IsTestBanka(b)).ToList();
                    if (cleanBankalar.Any())
                    {
                        var payloads = cleanBankalar.Select(MapBankaToPayload).ToList<object>();
                        await UpsertBatchPayloadAsync("bankalar", payloads);

                        var kasalar = cleanBankalar.Where(b => b.KartTuru == "Kasa").Select(MapKasaToPayload).ToList<object>();
                        if (kasalar.Any())
                        {
                            await UpsertBatchPayloadAsync("kasalar", kasalar);
                        }
                    }
                }

                // 2. Parent documents (Faturalar, Siparisler, Teklifler)
                if (faturalar?.Any() == true)
                {
                    var payloads = faturalar.Select(MapFaturaToPayload).ToList<object>();
                    await UpsertBatchPayloadAsync("faturalar", payloads);
                }
                if (siparisler?.Any() == true)
                {
                    var payloads = siparisler.Select(MapSiparisToPayload).ToList<object>();
                    await UpsertBatchPayloadAsync("siparisler", payloads);
                }
                if (teklifler?.Any() == true)
                {
                    var payloads = teklifler.Select(MapTeklifToPayload).ToList<object>();
                    await UpsertBatchPayloadAsync("teklifler", payloads);
                }

                // 3. Document details
                if (faturaDetaylar?.Any() == true)
                {
                    var payloads = faturaDetaylar.Select(MapFaturaDetayToPayload).ToList<object>();
                    await UpsertBatchPayloadAsync("fatura_detaylar", payloads);
                }
                if (siparisDetaylar?.Any() == true)
                {
                    var payloads = siparisDetaylar.Select(d => (object)new
                    {
                        id = d.Id,
                        siparis_id = d.SiparisId,
                        stok_id = d.StokId,
                        stok_kodu = d.StokKodu ?? "",
                        stok_adi = d.StokAdi ?? "",
                        birim = d.Birim ?? "Adet",
                        miktar = (decimal)d.Miktar,
                        birim_fiyat = d.BirimFiyat,
                        kdv_orani = (decimal)d.KdvOrani,
                        toplam_tutar = d.Tutar
                    }).ToList();
                    await UpsertBatchPayloadAsync("siparis_detaylar", payloads);
                }
                if (teklifDetaylar?.Any() == true)
                {
                    var payloads = teklifDetaylar.Select(d => (object)new
                    {
                        id = d.Id,
                        teklif_id = d.TeklifId,
                        stok_id = d.StokId,
                        stok_kodu = d.StokKodu ?? "",
                        stok_adi = d.StokAdi ?? "",
                        birim = d.Birim ?? "Adet",
                        miktar = (decimal)d.Miktar,
                        birim_fiyat = d.BirimFiyat,
                        kdv_orani = (decimal)d.KdvOrani,
                        toplam_tutar = d.Tutar
                    }).ToList();
                    await UpsertBatchPayloadAsync("teklif_detaylar", payloads);
                }

                // 4. Financial & Stock Movements
                if (cariHareketler?.Any() == true)
                {
                    var payloads = cariHareketler.Select(MapCariHareketToPayload).ToList<object>();
                    await UpsertBatchPayloadAsync("cari_hareketler", payloads);
                }
                if (stokHareketler?.Any() == true)
                {
                    var payloads = stokHareketler.Select(MapStokHareketToPayload).ToList<object>();
                    await UpsertBatchPayloadAsync("stok_hareketler", payloads);
                }
                if (kasaHareketler?.Any() == true)
                {
                    var payloads = kasaHareketler.Select(MapKasaHareketToPayload).ToList<object>();
                    await UpsertBatchPayloadAsync("kasa_hareketler", payloads);
                }
                if (bankaHareketler?.Any() == true)
                {
                    var payloads = bankaHareketler.Select(MapBankaHareketToPayload).ToList<object>();
                    await UpsertBatchPayloadAsync("banka_hareketler", payloads);
                }

                // 5. Notes & Shared State
                if (notes?.Any() == true)
                {
                    var payloads = notes.Select(n => (object)new Dictionary<string, object?>
                    {
                        ["id"] = n.Id.ToString(),
                        ["baslik"] = n.Title ?? "",
                        ["icerik"] = n.Content ?? "",
                        ["renk"] = n.Color ?? "#0061FF",
                        ["tarih"] = n.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        ["is_deleted"] = n.IsDeleted
                    }).ToList();
                    await UpsertBatchPayloadAsync("notlar", payloads);
                }

                // 6. Supplementary Tables (safe try-catch per table)
                try
                {
                    if (cekler?.Any() == true)
                    {
                        var payloads = cekler.Select(MapCekToPayload).ToList<object>();
                        await UpsertBatchPayloadAsync("cekler", payloads);
                    }
                }
                catch { }

                try
                {
                    if (senetler?.Any() == true)
                    {
                        var payloads = senetler.Select(MapSenetToPayload).ToList<object>();
                        await UpsertBatchPayloadAsync("senetler", payloads);
                    }
                }
                catch { }

                try
                {
                    if (kkIslemler?.Any() == true)
                    {
                        var payloads = kkIslemler.Select(MapKrediKartiToPayload).ToList<object>();
                        await UpsertBatchPayloadAsync("kredi_karti_islemler", payloads);
                    }
                }
                catch { }

                try
                {
                    if (eftIslemler?.Any() == true)
                    {
                        var payloads = eftIslemler.Select(MapEftToPayload).ToList<object>();
                        await UpsertBatchPayloadAsync("eft_islemler", payloads);
                    }
                }
                catch { }

                try
                {
                    if (gorevler?.Any() == true)
                    {
                        var payloads = gorevler.Select(MapGorevToPayload).ToList<object>();
                        await UpsertBatchPayloadAsync("gorevler", payloads);
                    }
                }
                catch { }

                try
                {
                    if (personeller?.Any() == true)
                    {
                        var payloads = personeller.Select(MapPersonelToPayload).ToList<object>();
                        await UpsertBatchPayloadAsync("personeller", payloads);
                    }
                }
                catch { }

                try
                {
                    if (hedefler?.Any() == true)
                    {
                        var payloads = hedefler.Select(h => (object)new Dictionary<string, object?>
                        {
                            ["id"] = h.Id.ToString(),
                            ["yil"] = h.Yil,
                            ["ay"] = h.Ay,
                            ["hedef_tutar"] = h.HedefTutari
                        }).ToList();
                        await UpsertBatchPayloadAsync("satis_hedefleri", payloads);
                    }
                }
                catch { }

                try
                {
                    if (haftalikHedefler?.Any() == true)
                    {
                        var payloads = haftalikHedefler.Select(h => (object)new Dictionary<string, object?>
                        {
                            ["id"] = h.Id.ToString(),
                            ["yil"] = h.Yil,
                            ["hafta"] = h.Hafta,
                            ["hafta_no"] = h.Hafta,
                            ["hedef_tutar"] = h.HedefTutari
                        }).ToList();
                        await UpsertBatchPayloadAsync("haftalik_satis_hedefleri", payloads);
                    }
                }
                catch { }

                try
                {
                    if (yillikHedefler?.Any() == true)
                    {
                        var payloads = yillikHedefler.Select(h => (object)new Dictionary<string, object?>
                        {
                            ["id"] = h.Id.ToString(),
                            ["yil"] = h.Yil,
                            ["hedef_tutar"] = h.HedefTutari,
                            ["aciklama"] = h.Aciklama ?? ""
                        }).ToList();
                        await UpsertBatchPayloadAsync("yillik_satis_hedefleri", payloads);
                    }
                }
                catch { }

                try
                {
                    if (portfoyler?.Any() == true)
                    {
                        var payloads = portfoyler.Select(MapPortfoyToPayload).ToList<object>();
                        await UpsertBatchPayloadAsync("portfoy_kartlar", payloads);
                    }
                }
                catch { }

                try
                {
                    if (stokSayimlar?.Any() == true)
                    {
                        var payloads = stokSayimlar.Select(MapStokSayimFisiToPayload).ToList<object>();
                        await UpsertBatchPayloadAsync("stok_sayim_fisileri", payloads);
                    }
                }
                catch { }

                try
                {
                    if (stokSayimDetaylar?.Any() == true)
                    {
                        var payloads = stokSayimDetaylar.Select(MapStokSayimDetayToPayload).ToList<object>();
                        await UpsertBatchPayloadAsync("stok_sayim_detaylari", payloads);
                    }
                }
                catch { }

                try
                {
                    if (belgeler?.Any() == true)
                    {
                        var payloads = belgeler.Select(b => (object)new Dictionary<string, object?>
                        {
                            ["id"] = b.Id.ToString(),
                            ["ad"] = b.Ad ?? "",
                            ["belge_adi"] = b.Ad ?? "",
                            ["kategori"] = b.Kategori ?? "",
                            ["tur"] = b.Tur ?? "",
                            ["belge_turu"] = b.Tur ?? "",
                            ["boyut"] = b.Boyut ?? "",
                            ["tarih"] = b.Tarih.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                            ["veri"] = b.Veri ?? "",
                            ["yol"] = b.Yol ?? "",
                            ["dosya_yolu"] = b.Yol ?? "",
                            ["is_deleted"] = b.IsDeleted
                        }).ToList();
                        await UpsertBatchPayloadAsync("belge_arsiv", payloads);
                    }
                }
                catch { }

                try
                {
                    if (kurlar?.Any() == true)
                    {
                        var payloads = kurlar.Select(k => (object)new Dictionary<string, object?>
                        {
                            ["id"] = k.Id.ToString(),
                            ["kod"] = k.Kod ?? "",
                            ["alis"] = k.Alis,
                            ["satis"] = k.Satis,
                            ["tarih"] = k.Tarih.ToString("yyyy-MM-ddTHH:mm:ssZ")
                        }).ToList();
                        await UpsertBatchPayloadAsync("doviz_kurlari", payloads);
                    }
                }
                catch { }
            });
        }

        // ==========================================
        // MOBIL GELEN KUTUSU (INBOX)
        // ==========================================
        public async Task<List<MobilGelenKutusu>> PullPendingInboxItemsAsync(int maliYil)
        {
            var list = new List<MobilGelenKutusu>();
            if (!IsConnected) return list;

            try
            {
                using var doc = await GetJsonAsync("mobil_gelen_kutusu", $"durum=eq.Bekliyor&mali_yil=eq.{maliYil}&order=created_at.asc");
                if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return list;

                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var item = new MobilGelenKutusu();
                    if (el.TryGetProperty("id", out var idProp)) item.Id = idProp.GetString() ?? "";
                    if (el.TryGetProperty("islem_turu", out var it)) item.IslemTuru = it.GetString() ?? "";
                    if (el.TryGetProperty("kaynak_cihaz", out var kc)) item.KaynakCihaz = kc.GetString();
                    if (el.TryGetProperty("payload", out var pld)) item.Payload = pld.GetRawText();
                    if (el.TryGetProperty("durum", out var drm)) item.Durum = drm.GetString() ?? "Bekliyor";
                    if (el.TryGetProperty("hata_mesaji", out var hm)) item.HataMesaji = hm.GetString();
                    if (el.TryGetProperty("resmi_evrak_no", out var re)) item.ResmiEvrakNo = re.GetString();
                    if (el.TryGetProperty("mali_yil", out var my)) item.MaliYil = ParseInt(my, maliYil);
                    if (el.TryGetProperty("created_at", out var ca) && DateTime.TryParse(ca.GetString(), out var dtCa)) item.CreatedAt = dtCa;
                    if (el.TryGetProperty("islenme_tarihi", out var ite) && DateTime.TryParse(ite.GetString(), out var dtIte)) item.IslenmeTarihi = dtIte;

                    list.Add(item);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Inbox Pull Error]: {ex.Message}");
            }

            return list;
        }

        public async Task<bool> UpdateInboxItemStatusAsync(string id, string durum, string? resmiEvrakNo = null, string? hataMesaji = null)
        {
            if (!IsConnected || string.IsNullOrEmpty(id)) return false;

            try
            {
                var payload = new Dictionary<string, object?>
                {
                    ["durum"] = durum,
                    ["resmi_evrak_no"] = resmiEvrakNo,
                    ["hata_mesaji"] = hataMesaji,
                    ["islenme_tarihi"] = DateTime.UtcNow.ToString("o")
                };

                using var req = CreateRequest(HttpMethod.Patch, $"mobil_gelen_kutusu?id=eq.{id}");
                string json = JsonSerializer.Serialize(payload, _jsonOpts);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                using var res = await _http.SendAsync(req);
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Inbox Update Error]: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ClaimInboxItemAsync(string id, string workerId)
        {
            if (!IsConnected || string.IsNullOrEmpty(id)) return false;

            try
            {
                var payload = new Dictionary<string, object?>
                {
                    ["durum"] = "Islemde",
                    ["kaynak_cihaz"] = $"{workerId}_{Environment.MachineName}",
                    ["islenme_tarihi"] = DateTime.UtcNow.ToString("o")
                };

                // Atomic optimistic lock: condition on durum=eq.Bekliyor
                using var req = CreateRequest(HttpMethod.Patch, $"mobil_gelen_kutusu?id=eq.{id}&durum=eq.Bekliyor");
                req.Headers.TryAddWithoutValidation("Prefer", "return=representation");
                string json = JsonSerializer.Serialize(payload, _jsonOpts);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                using var res = await _http.SendAsync(req);
                if (!res.IsSuccessStatusCode) return false;

                string respBody = await res.Content.ReadAsStringAsync();
                return !string.IsNullOrWhiteSpace(respBody) && respBody.Trim() != "[]";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Inbox Claim Error]: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> PushInboxItemAsync(MobilGelenKutusu item)
        {
            if (!IsConnected || item == null) return false;

            var payload = new Dictionary<string, object?>
            {
                ["id"] = item.Id,
                ["islem_turu"] = item.IslemTuru,
                ["kaynak_cihaz"] = item.KaynakCihaz,
                ["payload"] = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(item.Payload) ? "{}" : item.Payload),
                ["durum"] = item.Durum,
                ["hata_mesaji"] = item.HataMesaji,
                ["resmi_evrak_no"] = item.ResmiEvrakNo,
                ["mali_yil"] = item.MaliYil,
                ["created_at"] = item.CreatedAt.ToString("o"),
                ["islenme_tarihi"] = item.IslenmeTarihi?.ToString("o")
            };

            return await UpsertPayloadAsync("mobil_gelen_kutusu", payload);
        }
    }
}
