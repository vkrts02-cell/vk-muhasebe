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
        public const string DefaultSupabaseUrl = "https://fqgbdymffknglqeqoogt.supabase.co";
        public const string DefaultSupabaseKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImZxZ2JkeW1mZmtuZ2xxZXFvb2d0Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODk1ODUzMDEsImV4cCI6MjEwNTE2MTMwMX0.pBeE2ivWpbkAd8KSN1y2pXNZPIr_1mGMLXXHYPzjTDg";

        public string BaseUrl { get; set; } = DefaultSupabaseUrl;
        public string AuthSecret { get; set; } = DefaultSupabaseKey;
        public string GoogleApiKey { get; set; } = "";
        public string GoogleClientId { get; set; } = "";
        public string GoogleClientSecret { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public bool IsAutoSyncEnabled { get; set; } = true;
    }

    public class CloudSyncService
    {
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
        public bool IsConnected => !DisableCloudSync && !string.IsNullOrEmpty(_config.BaseUrl) && !string.IsNullOrEmpty(_config.AuthSecret) && _config.IsActive;
        public string BaseUrl => _config.BaseUrl;
        public string AuthSecret => _config.AuthSecret;
        public bool IsAutoSyncEnabled => _config.IsAutoSyncEnabled;

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
                                IsActive = true,
                                IsAutoSyncEnabled = true
                            };
                        }
                        else
                        {
                            loaded.BaseUrl = CleanSupabaseUrl(loaded.BaseUrl);
                            _config = loaded;
                            _config.IsActive = true;
                            _config.IsAutoSyncEnabled = true;
                        }
                    }
                    else
                    {
                        _config = loaded;
                        _config.BaseUrl = CloudConfig.DefaultSupabaseUrl;
                        _config.AuthSecret = CloudConfig.DefaultSupabaseKey;
                        _config.IsActive = true;
                        _config.IsAutoSyncEnabled = true;
                    }
                }
                else
                {
                    _config = new CloudConfig
                    {
                        BaseUrl = CloudConfig.DefaultSupabaseUrl,
                        AuthSecret = CloudConfig.DefaultSupabaseKey,
                        IsActive = true,
                        IsAutoSyncEnabled = true
                    };
                }

                if (string.IsNullOrWhiteSpace(_config.BaseUrl) || string.IsNullOrWhiteSpace(_config.AuthSecret))
                {
                    _config.BaseUrl = CloudConfig.DefaultSupabaseUrl;
                    _config.AuthSecret = CloudConfig.DefaultSupabaseKey;
                    _config.IsActive = true;
                    _config.IsAutoSyncEnabled = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cloud Config Load Error: {ex.Message}");
                _config = new CloudConfig
                {
                    BaseUrl = CloudConfig.DefaultSupabaseUrl,
                    AuthSecret = CloudConfig.DefaultSupabaseKey,
                    IsActive = true,
                    IsAutoSyncEnabled = true
                };
            }
        }

        public void SaveConfig(string url, string secret)
        {
            var cleanUrl = CleanSupabaseUrl(url);
            _config.BaseUrl = string.IsNullOrWhiteSpace(cleanUrl) ? CloudConfig.DefaultSupabaseUrl : cleanUrl;
            _config.AuthSecret = string.IsNullOrWhiteSpace(secret) ? CloudConfig.DefaultSupabaseKey : secret.Trim();
            _config.IsActive = !string.IsNullOrEmpty(_config.BaseUrl) && !string.IsNullOrEmpty(_config.AuthSecret);
            _config.IsAutoSyncEnabled = true;
            SaveConfigInternal();
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

        public async Task<bool> UpsertPayloadAsync(string table, object payload)
        {
            if (!IsConnected || payload == null) return false;
            try
            {
                using var req = CreateRequest(HttpMethod.Post, table);
                req.Headers.Add("Prefer", "resolution=merge-duplicates");
                string json = JsonSerializer.Serialize(payload, _jsonOpts);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                using var res = await _http.SendAsync(req);
                if (!res.IsSuccessStatusCode)
                {
                    var err = await res.Content.ReadAsStringAsync();
                    Console.WriteLine($"[Supabase UpsertPayload Failed] {table} ({res.StatusCode}): {err}");
                }
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase Upsert Error] {table}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpsertBatchPayloadAsync(string table, IEnumerable<object> items)
        {
            if (!IsConnected || items == null || !items.Any()) return false;
            try
            {
                using var req = CreateRequest(HttpMethod.Post, table);
                req.Headers.Add("Prefer", "resolution=merge-duplicates");
                string json = JsonSerializer.Serialize(items, _jsonOpts);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                using var res = await _http.SendAsync(req);
                if (!res.IsSuccessStatusCode)
                {
                    var err = await res.Content.ReadAsStringAsync();
                    Console.WriteLine($"[Supabase UpsertBatch Failed] {table} ({res.StatusCode}): {err}");
                }
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase UpsertBatch Error] {table}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteAsync(string table, int id)
        {
            if (!IsConnected) return false;
            try
            {
                // Soft delete first so mobile / other clients immediately recognize deletion
                try
                {
                    using var patchReq = CreateRequest(HttpMethod.Patch, $"{table}?id=eq.{id}");
                    patchReq.Content = new StringContent("{\"is_deleted\":true}", Encoding.UTF8, "application/json");
                    await _http.SendAsync(patchReq);
                }
                catch { }

                using var req = CreateRequest(HttpMethod.Delete, $"{table}?id=eq.{id}");
                using var res = await _http.SendAsync(req);
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase Delete Error] {table}/{id}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteFilteredAsync(string table, string filter)
        {
            if (!IsConnected) return false;
            try
            {
                try
                {
                    using var patchReq = CreateRequest(HttpMethod.Patch, $"{table}?{filter}");
                    patchReq.Content = new StringContent("{\"is_deleted\":true}", Encoding.UTF8, "application/json");
                    await _http.SendAsync(patchReq);
                }
                catch { }

                using var req = CreateRequest(HttpMethod.Delete, $"{table}?{filter}");
                using var res = await _http.SendAsync(req);
                return res.IsSuccessStatusCode;
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
                string query = table + "?select=*";
                if (!string.IsNullOrEmpty(filter)) query += "&" + filter;
                using var req = CreateRequest(HttpMethod.Get, query);
                using var res = await _http.SendAsync(req);
                if (!res.IsSuccessStatusCode) return null;
                string json = await res.Content.ReadAsStringAsync();
                return JsonDocument.Parse(json);
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
                if (!string.IsNullOrEmpty(filter)) query += "&" + filter;
                using var req = CreateRequest(HttpMethod.Get, query);
                using var res = await _http.SendAsync(req);
                if (!res.IsSuccessStatusCode) return null;
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
            ["id"] = c.Id.ToString(),
            ["cari_kodu"] = c.CariKod ?? "",
            ["kod"] = c.CariKod ?? "",
            ["unvan"] = c.Unvan ?? "",
            ["vergi_dairesi"] = c.VergiDairesi,
            ["vergi_no"] = c.VergiNo,
            ["tc_kimlik_no"] = c.TCNo,
            ["tc_no"] = c.TCNo,
            ["adres"] = c.Adres,
            ["sevk_adresi"] = c.SevkAdresi,
            ["sehir"] = c.Il,
            ["il"] = c.Il,
            ["ilce"] = c.Ilce,
            ["posta_kodu"] = c.PostaKodu,
            ["ulke"] = c.Ulke,
            ["telefon"] = c.Telefon,
            ["telefon2"] = c.CepTelefon,
            ["cep_telefon"] = c.CepTelefon,
            ["yetkili_kisi"] = c.Yetkili,
            ["yetkili"] = c.Yetkili,
            ["email"] = c.Email,
            ["eposta"] = c.Email,
            ["web_sitesi"] = c.WebAdresi,
            ["web_adresi"] = c.WebAdresi,
            ["iban"] = c.IBAN,
            ["aciklama"] = c.Aciklama,
            ["notlar"] = c.Aciklama,
            ["bakiye"] = c.Bakiye,
            ["borc_tutari"] = c.Borc,
            ["alacak_tutari"] = c.Alacak,
            ["borc"] = c.Borc,
            ["alacak"] = c.Alacak,
            ["kredi_limiti"] = c.RiskLimiti,
            ["risk_limiti"] = c.RiskLimiti,
            ["vade_gun"] = c.VadeGunu,
            ["vade_gunu"] = c.VadeGunu,
            ["odeme_plani"] = c.OdemePlani,
            ["ticaret_sicil_no"] = c.TicaretSicilNo,
            ["tur"] = c.Tur,
            ["grup"] = c.Grup,
            ["latitude"] = c.Latitude,
            ["lat"] = c.Latitude,
            ["longitude"] = c.Longitude,
            ["lng"] = c.Longitude,
            ["risk_takibi_yapilsin"] = c.RiskTakibiYapilsin,
            ["vade_gecmiste_engelle"] = c.VadeGecmisteEngelle,
            ["faturada_risk_kontrolu"] = c.FaturadaRiskKontrolu,
            ["is_active"] = c.AktifMi,
            ["is_deleted"] = c.IsDeleted,
            ["guncelleme_tarihi"] = c.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["updated_at"] = c.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["uuid"] = c.Uuid,
            ["version"] = c.Version
        };

        private static CariKart MapPayloadToCari(JsonElement el)
        {
            var c = new CariKart();
            if (el.TryGetProperty("id", out var id)) c.Id = ParseInt(id);
            if (el.TryGetProperty("cari_kodu", out var ck) && ck.ValueKind == JsonValueKind.String) c.CariKod = ck.GetString();
            else if (el.TryGetProperty("kod", out var k) && k.ValueKind == JsonValueKind.String) c.CariKod = k.GetString();
            else if (el.TryGetProperty("cariKod", out var ckCamel) && ckCamel.ValueKind == JsonValueKind.String) c.CariKod = ckCamel.GetString();

            if (el.TryGetProperty("unvan", out var u) && u.ValueKind == JsonValueKind.String) c.Unvan = u.GetString();
            if (el.TryGetProperty("vergi_dairesi", out var vd) && vd.ValueKind == JsonValueKind.String) c.VergiDairesi = vd.GetString();
            else if (el.TryGetProperty("vergiDairesi", out var vdCamel) && vdCamel.ValueKind == JsonValueKind.String) c.VergiDairesi = vdCamel.GetString();

            if (el.TryGetProperty("vergi_no", out var vn) && vn.ValueKind == JsonValueKind.String) c.VergiNo = vn.GetString();
            else if (el.TryGetProperty("vergiNo", out var vnCamel) && vnCamel.ValueKind == JsonValueKind.String) c.VergiNo = vnCamel.GetString();

            if (el.TryGetProperty("tc_kimlik_no", out var tc) && tc.ValueKind == JsonValueKind.String) c.TCNo = tc.GetString();
            else if (el.TryGetProperty("tc_no", out var tcn) && tcn.ValueKind == JsonValueKind.String) c.TCNo = tcn.GetString();
            else if (el.TryGetProperty("tcNo", out var tcCamel) && tcCamel.ValueKind == JsonValueKind.String) c.TCNo = tcCamel.GetString();
            else if (el.TryGetProperty("tcKimlikNo", out var tcKCamel) && tcKCamel.ValueKind == JsonValueKind.String) c.TCNo = tcKCamel.GetString();

            if (el.TryGetProperty("adres", out var adr) && adr.ValueKind == JsonValueKind.String) c.Adres = adr.GetString();
            if (el.TryGetProperty("sevk_adresi", out var sadr) && sadr.ValueKind == JsonValueKind.String) c.SevkAdresi = sadr.GetString();
            else if (el.TryGetProperty("sevkAdresi", out var sadrCamel) && sadrCamel.ValueKind == JsonValueKind.String) c.SevkAdresi = sadrCamel.GetString();

            if (el.TryGetProperty("sehir", out var sh) && sh.ValueKind == JsonValueKind.String) c.Il = sh.GetString();
            else if (el.TryGetProperty("il", out var il) && il.ValueKind == JsonValueKind.String) c.Il = il.GetString();

            if (el.TryGetProperty("ilce", out var ilc) && ilc.ValueKind == JsonValueKind.String) c.Ilce = ilc.GetString();
            if (el.TryGetProperty("posta_kodu", out var pk) && pk.ValueKind == JsonValueKind.String) c.PostaKodu = pk.GetString();
            else if (el.TryGetProperty("postaKodu", out var pkCamel) && pkCamel.ValueKind == JsonValueKind.String) c.PostaKodu = pkCamel.GetString();

            if (el.TryGetProperty("ulke", out var ulk) && ulk.ValueKind == JsonValueKind.String) c.Ulke = ulk.GetString();

            if (el.TryGetProperty("telefon", out var tel) && tel.ValueKind == JsonValueKind.String) c.Telefon = tel.GetString();
            if (el.TryGetProperty("telefon2", out var tel2) && tel2.ValueKind == JsonValueKind.String) c.CepTelefon = tel2.GetString();
            else if (el.TryGetProperty("cep_telefon", out var ctel) && ctel.ValueKind == JsonValueKind.String) c.CepTelefon = ctel.GetString();
            else if (el.TryGetProperty("cepTelefon", out var ctelCamel) && ctelCamel.ValueKind == JsonValueKind.String) c.CepTelefon = ctelCamel.GetString();

            if (el.TryGetProperty("yetkili_kisi", out var yk) && yk.ValueKind == JsonValueKind.String) c.Yetkili = yk.GetString();
            else if (el.TryGetProperty("yetkili", out var yt) && yt.ValueKind == JsonValueKind.String) c.Yetkili = yt.GetString();
            else if (el.TryGetProperty("yetkiliKisi", out var ykCamel) && ykCamel.ValueKind == JsonValueKind.String) c.Yetkili = ykCamel.GetString();

            if (el.TryGetProperty("email", out var em) && em.ValueKind == JsonValueKind.String) c.Email = em.GetString();
            else if (el.TryGetProperty("eposta", out var ep) && ep.ValueKind == JsonValueKind.String) c.Email = ep.GetString();

            if (el.TryGetProperty("web_sitesi", out var ws) && ws.ValueKind == JsonValueKind.String) c.WebAdresi = ws.GetString();
            else if (el.TryGetProperty("web_adresi", out var wa) && wa.ValueKind == JsonValueKind.String) c.WebAdresi = wa.GetString();
            else if (el.TryGetProperty("webAdresi", out var waCamel) && waCamel.ValueKind == JsonValueKind.String) c.WebAdresi = waCamel.GetString();

            if (el.TryGetProperty("iban", out var ib) && ib.ValueKind == JsonValueKind.String) c.IBAN = ib.GetString();
            if (el.TryGetProperty("aciklama", out var ack) && ack.ValueKind == JsonValueKind.String) c.Aciklama = ack.GetString();
            else if (el.TryGetProperty("notlar", out var ntl) && ntl.ValueKind == JsonValueKind.String) c.Aciklama = ntl.GetString();

            if (el.TryGetProperty("borc_tutari", out var bt)) c.Borc = ParseDecimal(bt);
            else if (el.TryGetProperty("borc", out var brc)) c.Borc = ParseDecimal(brc);

            if (el.TryGetProperty("alacak_tutari", out var at)) c.Alacak = ParseDecimal(at);
            else if (el.TryGetProperty("alacak", out var alc)) c.Alacak = ParseDecimal(alc);

            if (el.TryGetProperty("vade_gun", out var vg)) c.VadeGunu = ParseInt(vg);
            else if (el.TryGetProperty("vade_gunu", out var vgu)) c.VadeGunu = ParseInt(vgu);
            else if (el.TryGetProperty("vadeGunu", out var vguCamel)) c.VadeGunu = ParseInt(vguCamel);
            else if (el.TryGetProperty("vadeGun", out var vgCamel)) c.VadeGunu = ParseInt(vgCamel);

            if (el.TryGetProperty("kredi_limiti", out var kl)) c.RiskLimiti = ParseDecimal(kl);
            else if (el.TryGetProperty("risk_limiti", out var rl)) c.RiskLimiti = ParseDecimal(rl);
            else if (el.TryGetProperty("riskLimiti", out var rlCamel)) c.RiskLimiti = ParseDecimal(rlCamel);
            else if (el.TryGetProperty("krediLimiti", out var klCamel)) c.RiskLimiti = ParseDecimal(klCamel);

            if (el.TryGetProperty("odeme_plani", out var op) && op.ValueKind == JsonValueKind.String) c.OdemePlani = op.GetString();
            else if (el.TryGetProperty("odemePlani", out var opCamel) && opCamel.ValueKind == JsonValueKind.String) c.OdemePlani = opCamel.GetString();

            if (el.TryGetProperty("ticaret_sicil_no", out var tsn) && tsn.ValueKind == JsonValueKind.String) c.TicaretSicilNo = tsn.GetString();
            else if (el.TryGetProperty("ticaretSicilNo", out var tsnCamel) && tsnCamel.ValueKind == JsonValueKind.String) c.TicaretSicilNo = tsnCamel.GetString();

            if (el.TryGetProperty("tur", out var tr) && tr.ValueKind == JsonValueKind.String) c.Tur = tr.GetString();
            if (el.TryGetProperty("grup", out var grp) && grp.ValueKind == JsonValueKind.String) c.Grup = grp.GetString();

            if (el.TryGetProperty("latitude", out var latEl)) c.Latitude = ParseDouble(latEl);
            else if (el.TryGetProperty("lat", out var latEl2)) c.Latitude = ParseDouble(latEl2);

            if (el.TryGetProperty("longitude", out var lngEl)) c.Longitude = ParseDouble(lngEl);
            else if (el.TryGetProperty("lng", out var lngEl2)) c.Longitude = ParseDouble(lngEl2);

            if (el.TryGetProperty("risk_takibi_yapilsin", out var rty)) c.RiskTakibiYapilsin = rty.ValueKind == JsonValueKind.True;
            else if (el.TryGetProperty("riskTakibiYapilsin", out var rtyCamel)) c.RiskTakibiYapilsin = rtyCamel.ValueKind == JsonValueKind.True;

            if (el.TryGetProperty("vade_gecmiste_engelle", out var vge)) c.VadeGecmisteEngelle = vge.ValueKind == JsonValueKind.True;
            else if (el.TryGetProperty("vadeGecmisteEngelle", out var vgeCamel)) c.VadeGecmisteEngelle = vgeCamel.ValueKind == JsonValueKind.True;

            if (el.TryGetProperty("faturada_risk_kontrolu", out var frk)) c.FaturadaRiskKontrolu = frk.ValueKind != JsonValueKind.False;
            else if (el.TryGetProperty("faturadaRiskKontrolu", out var frkCamel)) c.FaturadaRiskKontrolu = frkCamel.ValueKind != JsonValueKind.False;

            if (el.TryGetProperty("is_active", out var isAct)) c.AktifMi = isAct.ValueKind != JsonValueKind.False;
            else if (el.TryGetProperty("aktifMi", out var actCamel)) c.AktifMi = actCamel.ValueKind != JsonValueKind.False;

            if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) c.IsDeleted = true;
            if (el.TryGetProperty("guncelleme_tarihi", out var gt) && gt.ValueKind == JsonValueKind.String && DateTime.TryParse(gt.GetString(), out var dtGt)) c.UpdatedAt = dtGt;
            else if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) c.UpdatedAt = dtUa;
            if (el.TryGetProperty("version", out var vr)) c.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(uu.GetString())) c.Uuid = uu.GetString()!;
            return c;
        }

        private static Dictionary<string, object?> MapStokToPayload(StokKart s) => new()
        {
            ["id"] = s.Id.ToString(),
            ["stok_kodu"] = s.StokKodu ?? "",
            ["stok_adi"] = s.StokAdi ?? "",
            ["barkod"] = s.Barkod,
            ["grup_adi"] = s.Kategori ?? s.Grup,
            ["grup"] = s.Kategori ?? s.Grup,
            ["birim"] = s.Birim ?? "Adet",
            ["alis_fiyati"] = s.AlisFiyati,
            ["ortalama_alis_fiyati"] = s.OrtalamaAlisFiyati,
            ["satis_fiyati"] = s.SatisFiyati,
            ["ortalama_satis_fiyati"] = s.OrtalamaSatisFiyati,
            ["kdv_orani"] = s.KDV,
            ["mevcut_miktar"] = (decimal)s.Miktar,
            ["miktar"] = (decimal)s.Miktar,
            ["kritik_seviye"] = (decimal)s.MinSeviye,
            ["kritik_stok"] = (decimal)s.MinSeviye,
            ["aciklama"] = s.Aciklama,
            ["kayit_tarihi"] = s.KayitTarihi.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["is_active"] = true,
            ["is_deleted"] = s.IsDeleted,
            ["guncelleme_tarihi"] = s.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["updated_at"] = s.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["uuid"] = s.Uuid,
            ["version"] = s.Version
        };

        private static StokKart MapPayloadToStok(JsonElement el)
        {
            var s = new StokKart();
            if (el.TryGetProperty("id", out var id)) s.Id = ParseInt(id);
            if (el.TryGetProperty("stok_kodu", out var sk) && sk.ValueKind == JsonValueKind.String) s.StokKodu = sk.GetString();
            if (el.TryGetProperty("stok_adi", out var sa) && sa.ValueKind == JsonValueKind.String) s.StokAdi = sa.GetString();
            if (el.TryGetProperty("barkod", out var bk) && bk.ValueKind == JsonValueKind.String) s.Barkod = bk.GetString();
            if (el.TryGetProperty("grup_adi", out var ga) && ga.ValueKind == JsonValueKind.String) s.Kategori = ga.GetString();
            else if (el.TryGetProperty("grup", out var grp) && grp.ValueKind == JsonValueKind.String) s.Kategori = grp.GetString();
            if (el.TryGetProperty("birim", out var br) && br.ValueKind == JsonValueKind.String) s.Birim = br.GetString() ?? "Adet";
            if (el.TryGetProperty("alis_fiyati", out var af)) s.AlisFiyati = ParseDecimal(af);
            if (el.TryGetProperty("ortalama_alis_fiyati", out var oaf)) s.OrtalamaAlisFiyati = ParseDecimal(oaf);
            if (el.TryGetProperty("satis_fiyati", out var sf)) s.SatisFiyati = ParseDecimal(sf);
            if (el.TryGetProperty("ortalama_satis_fiyati", out var osf)) s.OrtalamaSatisFiyati = ParseDecimal(osf);
            if (el.TryGetProperty("kdv_orani", out var ko)) s.KDV = ParseInt(ko, 20);
            if (el.TryGetProperty("mevcut_miktar", out var mm)) s.Miktar = ParseDecimal(mm);
            else if (el.TryGetProperty("miktar", out var mq)) s.Miktar = ParseDecimal(mq);
            if (el.TryGetProperty("kritik_seviye", out var ks)) s.MinSeviye = ParseDecimal(ks);
            else if (el.TryGetProperty("kritik_stok", out var kst)) s.MinSeviye = ParseDecimal(kst);
            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) s.Aciklama = ac.GetString();
            if (el.TryGetProperty("kayit_tarihi", out var kt) && kt.ValueKind == JsonValueKind.String && DateTime.TryParse(kt.GetString(), out var dtKt)) s.KayitTarihi = dtKt;
            if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) s.IsDeleted = true;
            if (el.TryGetProperty("guncelleme_tarihi", out var gt) && gt.ValueKind == JsonValueKind.String && DateTime.TryParse(gt.GetString(), out var dtGt)) s.UpdatedAt = dtGt;
            else if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) s.UpdatedAt = dtUa;
            if (el.TryGetProperty("version", out var vr)) s.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(uu.GetString())) s.Uuid = uu.GetString()!;
            return s;
        }

        private static Dictionary<string, object?> MapFaturaToPayload(Fatura f) => new()
        {
            ["id"] = f.Id.ToString(),
            ["fatura_no"] = f.FaturaNo ?? "",
            ["fatura_turu"] = string.IsNullOrWhiteSpace(f.Tur) ? "Satis" : f.Tur,
            ["cari_id"] = f.CariId.ToString(),
            ["cari_unvan"] = f.CariUnvan ?? "",
            ["tarih"] = f.Tarih.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["vade_tarihi"] = f.VadeTarihi.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["ara_toplam"] = f.AraToplam,
            ["kdv_toplam"] = f.KdvToplam,
            ["iskonto_toplam"] = 0m,
            ["genel_toplam"] = f.GenelToplam,
            ["aciklama"] = f.Aciklama,
            ["doviz_turu"] = f.DovizTuru ?? "TRY",
            ["doviz_kuru"] = f.DovizKuru,
            ["odeme_sekli"] = f.OdemeSekli,
            ["is_earsiv"] = f.IsEArsiv,
            ["vergi_dairesi"] = f.VergiDairesi,
            ["vergi_no"] = f.VergiNo,
            ["adres"] = f.Adres,
            ["baglanti_evrak_no"] = f.BaglantiEvrakNo,
            ["kasa_id"] = f.KasaId?.ToString(),
            ["banka_id"] = f.BankaId?.ToString(),
            ["iptal_mi"] = f.IptalMi,
            ["is_deleted"] = f.IsDeleted,
            ["guncelleme_tarihi"] = f.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["updated_at"] = f.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["uuid"] = f.Uuid,
            ["version"] = f.Version
        };

        private static Fatura MapPayloadToFatura(JsonElement el)
        {
            var f = new Fatura();
            if (el.TryGetProperty("id", out var id)) f.Id = ParseInt(id);
            if (el.TryGetProperty("fatura_no", out var fn) && fn.ValueKind == JsonValueKind.String) f.FaturaNo = fn.GetString();
            if (el.TryGetProperty("fatura_turu", out var ft) && ft.ValueKind == JsonValueKind.String) f.Tur = ft.GetString();
            else if (el.TryGetProperty("tur", out var tr) && tr.ValueKind == JsonValueKind.String) f.Tur = tr.GetString();
            if (el.TryGetProperty("cari_id", out var ci)) f.CariId = ParseInt(ci);
            if (el.TryGetProperty("cari_unvan", out var cu) && cu.ValueKind == JsonValueKind.String) f.CariUnvan = cu.GetString();
            if (el.TryGetProperty("tarih", out var trh) && trh.ValueKind == JsonValueKind.String && DateTime.TryParse(trh.GetString(), out var t)) f.Tarih = t;
            if (el.TryGetProperty("vade_tarihi", out var vt) && vt.ValueKind == JsonValueKind.String && DateTime.TryParse(vt.GetString(), out var vd)) f.VadeTarihi = vd;
            if (el.TryGetProperty("ara_toplam", out var at)) f.AraToplam = ParseDecimal(at);
            if (el.TryGetProperty("kdv_toplam", out var kt)) f.KdvToplam = ParseDecimal(kt);
            if (el.TryGetProperty("genel_toplam", out var gt)) f.GenelToplam = ParseDecimal(gt);
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
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(uu.GetString())) f.Uuid = uu.GetString()!;
            return f;
        }

        private static Dictionary<string, object?> MapFaturaDetayToPayload(FaturaDetay d) => new()
        {
            ["id"] = d.Id.ToString(),
            ["fatura_id"] = d.FaturaId.ToString(),
            ["stok_id"] = d.StokId.ToString(),
            ["stok_kodu"] = d.StokKodu ?? "",
            ["stok_adi"] = d.StokAdi ?? "",
            ["birim"] = d.Birim ?? "Adet",
            ["miktar"] = (decimal)d.Miktar,
            ["birim_fiyat"] = d.BirimFiyat,
            ["kdv_orani"] = d.KDVOrani,
            ["kdv_tutari"] = d.KdvTutari,
            ["iskonto_orani"] = 0m,
            ["iskonto_tutari"] = 0m,
            ["toplam_tutar"] = d.ToplamTutar,
            ["aciklama"] = d.Aciklama,
            ["uuid"] = d.Uuid,
            ["version"] = d.Version,
            ["updated_at"] = d.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["is_deleted"] = d.IsDeleted
        };

        private static FaturaDetay MapPayloadToFaturaDetay(JsonElement el)
        {
            var d = new FaturaDetay();
            if (el.TryGetProperty("id", out var id)) d.Id = ParseInt(id);
            if (el.TryGetProperty("fatura_id", out var fi)) d.FaturaId = ParseInt(fi);
            if (el.TryGetProperty("stok_id", out var si)) d.StokId = ParseInt(si);
            if (el.TryGetProperty("stok_kodu", out var sk) && sk.ValueKind == JsonValueKind.String) d.StokKodu = sk.GetString();
            if (el.TryGetProperty("stok_adi", out var sa) && sa.ValueKind == JsonValueKind.String) d.StokAdi = sa.GetString();
            if (el.TryGetProperty("miktar", out var mq)) d.Miktar = ParseDecimal(mq);
            if (el.TryGetProperty("birim", out var br) && br.ValueKind == JsonValueKind.String) d.Birim = br.GetString();
            if (el.TryGetProperty("birim_fiyat", out var bf)) d.BirimFiyat = ParseDecimal(bf);
            if (el.TryGetProperty("kdv_orani", out var ko)) d.KDVOrani = ParseInt(ko);
            if (el.TryGetProperty("kdv_tutari", out var kt)) d.KdvTutari = ParseDecimal(kt);
            if (el.TryGetProperty("toplam_tutar", out var tt)) d.ToplamTutar = ParseDecimal(tt);
            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) d.Aciklama = ac.GetString();
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(uu.GetString())) d.Uuid = uu.GetString()!;
            if (el.TryGetProperty("version", out var vr)) d.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) d.UpdatedAt = dtUa;
            if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) d.IsDeleted = true;
            return d;
        }

        private static Dictionary<string, object?> MapCariHareketToPayload(CariHareket h) => new()
        {
            ["id"] = h.Id.ToString(),
            ["cari_id"] = h.CariId.ToString(),
            ["tarih"] = h.Tarih.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["islem_turu"] = h.IslemTuru ?? "",
            ["evrak_no"] = h.EvrakNo ?? "",
            ["aciklama"] = h.Aciklama ?? "",
            ["borc"] = h.Borc,
            ["alacak"] = h.Alacak,
            ["bakiye"] = h.KalanBakiye,
            ["fatura_id"] = h.FaturaId?.ToString(),
            ["is_deleted"] = h.IsDeleted,
            ["updated_at"] = h.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["guncelleme_tarihi"] = h.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["uuid"] = h.Uuid,
            ["version"] = h.Version
        };

        private static CariHareket? MapPayloadToCariHareket(JsonElement el)
        {
            if (el.TryGetProperty("is_deleted", out var isDel) && (isDel.ValueKind == JsonValueKind.True || (isDel.ValueKind == JsonValueKind.Number && isDel.GetInt32() == 1)))
                return null;

            var h = new CariHareket();
            if (el.TryGetProperty("id", out var id)) h.Id = ParseInt(id);
            if (el.TryGetProperty("cari_id", out var ci)) h.CariId = ParseInt(ci);
            if (el.TryGetProperty("tarih", out var trh) && trh.ValueKind == JsonValueKind.String && DateTime.TryParse(trh.GetString(), out var t)) h.Tarih = t;
            if (el.TryGetProperty("islem_turu", out var it) && it.ValueKind == JsonValueKind.String) h.IslemTuru = it.GetString();
            if (el.TryGetProperty("evrak_no", out var en) && en.ValueKind == JsonValueKind.String) h.EvrakNo = en.GetString();
            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) h.Aciklama = ac.GetString();
            if (el.TryGetProperty("borc", out var b)) h.Borc = ParseDecimal(b);
            if (el.TryGetProperty("alacak", out var a)) h.Alacak = ParseDecimal(a);
            if (el.TryGetProperty("fatura_id", out var fi)) h.FaturaId = ParseNullableInt(fi);
            if (el.TryGetProperty("vade_tarihi", out var vt) && vt.ValueKind == JsonValueKind.String && DateTime.TryParse(vt.GetString(), out var v)) h.Vade = v;
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(uu.GetString())) h.Uuid = uu.GetString()!;
            if (el.TryGetProperty("version", out var vr)) h.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) h.UpdatedAt = dtUa;
            return h;
        }

        private static Dictionary<string, object?> MapStokHareketToPayload(StokHareket sh)
        {
            decimal miktar = sh.Miktar != 0 ? sh.Miktar : (sh.Giren > 0 ? sh.Giren : sh.Cikan);
            decimal toplam = miktar * sh.Fiyat;
            return new()
            {
                ["id"] = sh.Id.ToString(),
                ["stok_id"] = sh.StokId.ToString(),
                ["tarih"] = sh.Tarih.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["hareket_turu"] = sh.IslemTuru ?? (sh.Giren > 0 ? "GİRİŞ" : "ÇIKIŞ"),
                ["hareket_tipi"] = sh.IslemTuru ?? (sh.Giren > 0 ? "GİRİŞ" : "ÇIKIŞ"),
                ["evrak_no"] = sh.EvrakNo ?? "",
                ["miktar"] = miktar,
                ["birim_fiyat"] = sh.Fiyat,
                ["toplam_tutar"] = toplam,
                ["fatura_id"] = sh.FaturaId?.ToString(),
                ["aciklama"] = sh.Aciklama ?? "",
                ["is_deleted"] = sh.IsDeleted,
                ["updated_at"] = sh.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["guncelleme_tarihi"] = sh.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["uuid"] = sh.Uuid,
                ["version"] = sh.Version
            };
        }

        private static StokHareket? MapPayloadToStokHareket(JsonElement el)
        {
            if (el.TryGetProperty("is_deleted", out var isDel) && (isDel.ValueKind == JsonValueKind.True || (isDel.ValueKind == JsonValueKind.Number && isDel.GetInt32() == 1)))
                return null;

            var sh = new StokHareket();
            if (el.TryGetProperty("id", out var id)) sh.Id = ParseInt(id);
            if (el.TryGetProperty("stok_id", out var si)) sh.StokId = ParseInt(si);
            if (el.TryGetProperty("tarih", out var trh) && trh.ValueKind == JsonValueKind.String && DateTime.TryParse(trh.GetString(), out var t)) sh.Tarih = t;
            if (el.TryGetProperty("hareket_turu", out var ht) && ht.ValueKind == JsonValueKind.String) sh.IslemTuru = ht.GetString();
            else if (el.TryGetProperty("hareket_tipi", out var hti) && hti.ValueKind == JsonValueKind.String) sh.IslemTuru = hti.GetString();
            if (el.TryGetProperty("evrak_no", out var en) && en.ValueKind == JsonValueKind.String) sh.EvrakNo = en.GetString();
            if (el.TryGetProperty("miktar", out var m))
            {
                sh.Miktar = ParseDecimal(m);
                if (sh.IslemTuru?.Contains("GİRİŞ") == true || sh.IslemTuru?.Contains("Giris") == true) sh.Giren = sh.Miktar;
                else sh.Cikan = sh.Miktar;
            }
            if (el.TryGetProperty("birim_fiyat", out var bf)) sh.Fiyat = ParseDecimal(bf);
            if (el.TryGetProperty("fatura_id", out var fi)) sh.FaturaId = ParseNullableInt(fi);
            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) sh.Aciklama = ac.GetString();
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(uu.GetString())) sh.Uuid = uu.GetString()!;
            if (el.TryGetProperty("version", out var vr)) sh.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) sh.UpdatedAt = dtUa;
            return sh;
        }

        private static Dictionary<string, object?> MapBankaToPayload(BankaKart b)
        {
            string? aciklama = null;
            if (b.KartTuru == "Kasa")
            {
                aciklama = "[KASA]" + (string.IsNullOrWhiteSpace(b.Yetkili) ? "" : $"|{b.Yetkili}");
            }
            else if (!string.IsNullOrWhiteSpace(b.Yetkili))
            {
                aciklama = b.Yetkili;
            }

            return new Dictionary<string, object?>
            {
                ["id"] = b.Id.ToString(),
                ["banka_adi"] = b.BankaAdi ?? "",
                ["sube_adi"] = b.SubeAdi ?? "",
                ["hesap_no"] = b.HesapNo ?? "",
                ["iban"] = b.IBAN ?? "",
                ["bakiye"] = b.Bakiye,
                ["guncel_bakiye"] = b.GuncelBakiye,
                ["acilis_bakiyesi"] = b.AcilisBakiyesi,
                ["para_birimi"] = b.DovizTuru ?? "TRY",
                ["kart_turu"] = b.KartTuru,
                ["telefon"] = b.Telefon,
                ["aciklama"] = aciklama,
                ["is_active"] = true,
                ["is_deleted"] = b.IsDeleted,
                ["updated_at"] = b.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["uuid"] = b.Uuid,
                ["version"] = b.Version
            };
        }

        private static Dictionary<string, object?> MapKasaToPayload(BankaKart b) => new()
        {
            ["id"] = b.Id.ToString(),
            ["kasa_kodu"] = b.HesapNo ?? $"KAS-{b.Id}",
            ["kasa_adi"] = b.BankaAdi ?? "",
            ["bakiye"] = b.Bakiye,
            ["para_birimi"] = b.DovizTuru ?? "TRY",
            ["aciklama"] = b.Yetkili ?? "",
            ["is_active"] = true,
            ["is_deleted"] = b.IsDeleted
        };

        private static BankaKart MapPayloadToKasa(JsonElement el)
        {
            var b = new BankaKart { KartTuru = "Kasa" };
            if (el.TryGetProperty("id", out var id)) b.Id = ParseInt(id);
            if (el.TryGetProperty("kasa_adi", out var ka) && ka.ValueKind == JsonValueKind.String) b.BankaAdi = ka.GetString();
            if (el.TryGetProperty("kasa_kodu", out var kk) && kk.ValueKind == JsonValueKind.String) b.HesapNo = kk.GetString();
            if (el.TryGetProperty("bakiye", out var bq)) b.Bakiye = ParseDecimal(bq);
            if (el.TryGetProperty("para_birimi", out var pb) && pb.ValueKind == JsonValueKind.String) b.DovizTuru = pb.GetString();
            if (el.TryGetProperty("aciklama", out var ac) && ac.ValueKind == JsonValueKind.String) b.Yetkili = ac.GetString();
            if (el.TryGetProperty("is_deleted", out var isDel) && isDel.ValueKind == JsonValueKind.True) b.IsDeleted = true;
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
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(uu.GetString())) b.Uuid = uu.GetString()!;
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
            ["id"] = bh.Id.ToString(),
            ["banka_id"] = bh.BankaId.ToString(),
            ["tarih"] = bh.Tarih.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["hareket_turu"] = bh.IslemTuru ?? "",
            ["evrak_no"] = bh.EvrakNo ?? "",
            ["aciklama"] = bh.Aciklama ?? "",
            ["yatan"] = bh.Giren,
            ["ceken"] = bh.Cikan,
            ["cari_id"] = bh.CariId?.ToString(),
            ["is_deleted"] = bh.IsDeleted,
            ["updated_at"] = bh.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["uuid"] = bh.Uuid,
            ["version"] = bh.Version
        };

        private static BankaHareket? MapPayloadToBankaHareket(JsonElement el)
        {
            if (el.TryGetProperty("is_deleted", out var isDel) && (isDel.ValueKind == JsonValueKind.True || (isDel.ValueKind == JsonValueKind.Number && isDel.GetInt32() == 1)))
                return null;

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
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(uu.GetString())) bh.Uuid = uu.GetString()!;
            if (el.TryGetProperty("version", out var vr)) bh.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) bh.UpdatedAt = dtUa;
            return bh;
        }

        private static Dictionary<string, object?> MapKasaHareketToPayload(KasaHareket kh) => new()
        {
            ["id"] = kh.Id.ToString(),
            ["kasa_id"] = kh.KasaId.ToString(),
            ["tarih"] = kh.Tarih.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["hareket_turu"] = kh.IslemTuru ?? "",
            ["evrak_no"] = kh.EvrakNo ?? "",
            ["aciklama"] = kh.Aciklama ?? "",
            ["gelir"] = kh.Giren,
            ["gider"] = kh.Cikan,
            ["cari_id"] = kh.CariId?.ToString(),
            ["is_deleted"] = kh.IsDeleted,
            ["updated_at"] = kh.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["uuid"] = kh.Uuid,
            ["version"] = kh.Version
        };

        private static KasaHareket? MapPayloadToKasaHareket(JsonElement el)
        {
            if (el.TryGetProperty("is_deleted", out var isDel) && (isDel.ValueKind == JsonValueKind.True || (isDel.ValueKind == JsonValueKind.Number && isDel.GetInt32() == 1)))
                return null;

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
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(uu.GetString())) kh.Uuid = uu.GetString()!;
            if (el.TryGetProperty("version", out var vr)) kh.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) kh.UpdatedAt = dtUa;
            return kh;
        }

        private static Dictionary<string, object?> MapSiparisToPayload(Siparis sp) => new()
        {
            ["id"] = sp.Id,
            ["siparis_no"] = sp.SiparisNo ?? "",
            ["siparis_turu"] = "Standart",
            ["cari_id"] = sp.CariId,
            ["cari_unvan"] = sp.CariUnvan ?? "",
            ["tarih"] = sp.Tarih.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["teslim_tarihi"] = sp.TeslimatTarihi?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["durum"] = sp.Durum ?? "Onaylandı",
            ["genel_toplam"] = sp.GenelToplam,
            ["aciklama"] = sp.Aciklama,
            ["pdf_notlar"] = sp.PdfNotlar,
            ["odeme_bilgisi"] = sp.OdemeBilgisi,
            ["oncelik"] = sp.Oncelik,
            ["baglanti_evrak_no"] = sp.BaglantiEvrakNo,
            ["is_deleted"] = sp.IsDeleted,
            ["updated_at"] = sp.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["uuid"] = sp.Uuid,
            ["version"] = sp.Version
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
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(uu.GetString())) sp.Uuid = uu.GetString()!;
            if (el.TryGetProperty("version", out var vr)) sp.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) sp.UpdatedAt = dtUa;
            return sp;
        }

        private static Dictionary<string, object?> MapTeklifToPayload(Teklif tk) => new()
        {
            ["id"] = tk.Id,
            ["teklif_no"] = tk.TeklifNo ?? "",
            ["teklif_turu"] = "Standart",
            ["cari_id"] = tk.CariId,
            ["cari_unvan"] = tk.CariUnvan ?? "",
            ["tarih"] = tk.Tarih.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["gecerlilik_tarihi"] = tk.GecerlilikTarihi?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["durum"] = tk.Durum ?? "Gönderildi",
            ["genel_toplam"] = tk.GenelToplam,
            ["aciklama"] = tk.Aciklama,
            ["odeme_bilgisi"] = tk.OdemeBilgisi,
            ["is_deleted"] = tk.IsDeleted,
            ["updated_at"] = tk.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["uuid"] = tk.Uuid,
            ["version"] = tk.Version
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
            if (el.TryGetProperty("uuid", out var uu) && uu.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(uu.GetString())) tk.Uuid = uu.GetString()!;
            if (el.TryGetProperty("version", out var vr)) tk.Version = ParseInt(vr, 1);
            if (el.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String && DateTime.TryParse(ua.GetString(), out var dtUa)) tk.UpdatedAt = dtUa;
            return tk;
        }

        // ==========================================
        // CARİ KARTLAR & CARİ HAREKETLER
        // ==========================================
        public async Task SyncCariAsync(CariKart cari) => await UpsertPayloadAsync("cariler", MapCariToPayload(cari));
        public async Task DeleteCariAsync(int id) => await DeleteAsync("cariler", id);
        public async Task<List<CariKart>?> PullCarilerAsync()
        {
            using var doc = await GetJsonAsync("cariler", "or=(is_deleted.is.null,is_deleted.eq.false)");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<CariKart>();
            foreach (var el in doc.RootElement.EnumerateArray()) list.Add(MapPayloadToCari(el));
            return list;
        }

        public async Task SyncCariHareketAsync(CariHareket hareket) => await UpsertPayloadAsync("cari_hareketler", MapCariHareketToPayload(hareket));
        public async Task DeleteCariHareketAsync(int id) => await DeleteAsync("cari_hareketler", id);
        public async Task<List<CariHareket>?> PullCariHareketlerAsync()
        {
            using var doc = await GetJsonAsync("cari_hareketler", "or=(is_deleted.is.null,is_deleted.eq.false)");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<CariHareket>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var h = MapPayloadToCariHareket(el);
                if (h != null) list.Add(h);
            }
            return list;
        }

        public async Task DeleteCariHareketByFaturaIdAsync(int faturaId, string? evrakNo = null)
        {
            if (faturaId > 0)
                await DeleteFilteredAsync("cari_hareketler", $"fatura_id=eq.{faturaId}");
            if (!string.IsNullOrEmpty(evrakNo))
            {
                await DeleteFilteredAsync("cari_hareketler", $"evrak_no=eq.{Uri.EscapeDataString(evrakNo)}");
                await DeleteFilteredAsync("cari_hareketler", $"evrak_no=eq.{Uri.EscapeDataString("KPL-" + evrakNo)}");
            }
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
        public async Task SyncStokAsync(StokKart stok) => await UpsertPayloadAsync("stoklar", MapStokToPayload(stok));
        public async Task DeleteStokAsync(int id) => await DeleteAsync("stoklar", id);
        public async Task<List<StokKart>?> PullStoklarAsync()
        {
            using var doc = await GetJsonAsync("stoklar", "or=(is_deleted.is.null,is_deleted.eq.false)");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<StokKart>();
            foreach (var el in doc.RootElement.EnumerateArray()) list.Add(MapPayloadToStok(el));
            return list;
        }

        public async Task SyncStokHareketAsync(StokHareket hareket) => await UpsertPayloadAsync("stok_hareketler", MapStokHareketToPayload(hareket));
        public async Task DeleteStokHareketAsync(int id) => await DeleteAsync("stok_hareketler", id);
        public async Task<List<StokHareket>?> PullStokHareketlerAsync()
        {
            using var doc = await GetJsonAsync("stok_hareketler", "or=(is_deleted.is.null,is_deleted.eq.false)");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<StokHareket>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var sh = MapPayloadToStokHareket(el);
                if (sh != null) list.Add(sh);
            }
            return list;
        }

        public async Task DeleteStokHareketByFaturaIdAsync(int faturaId, string? evrakNo = null)
        {
            if (faturaId > 0)
                await DeleteFilteredAsync("stok_hareketler", $"fatura_id=eq.{faturaId}");
            if (!string.IsNullOrEmpty(evrakNo))
                await DeleteFilteredAsync("stok_hareketler", $"evrak_no=eq.{Uri.EscapeDataString(evrakNo)}");
        }

        public async Task SyncStokGrupAsync(StokGrupDef grup) => await Task.CompletedTask;
        public async Task DeleteStokGrupAsync(int id) => await Task.CompletedTask;

        // ==========================================
        // FATURALAR & FATURA DETAYLAR
        // ==========================================
        public async Task SyncFaturaAsync(Fatura fatura) => await UpsertPayloadAsync("faturalar", MapFaturaToPayload(fatura));
        public async Task DeleteFaturaAsync(int id)
        {
            await DeleteFilteredAsync("fatura_detaylar", $"fatura_id=eq.{id}");
            await DeleteAsync("faturalar", id);
        }
        public async Task<List<Fatura>?> PullFaturalarAsync()
        {
            using var doc = await GetJsonAsync("faturalar", "or=(is_deleted.is.null,is_deleted.eq.false)");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<Fatura>();
            foreach (var el in doc.RootElement.EnumerateArray()) list.Add(MapPayloadToFatura(el));
            return list;
        }

        public async Task SyncFaturaDetaylarAsync(int faturaId, List<FaturaDetay> detaylar)
        {
            await SafeRun(async () =>
            {
                await DeleteFilteredAsync("fatura_detaylar", $"fatura_id=eq.{faturaId}");
                if (detaylar != null && detaylar.Count > 0)
                {
                    var payloads = new List<object>();
                    foreach (var d in detaylar)
                    {
                        d.FaturaId = faturaId;
                        payloads.Add(MapFaturaDetayToPayload(d));
                    }
                    await UpsertBatchPayloadAsync("fatura_detaylar", payloads);
                }
            });
        }

        public async Task DeleteFaturaDetaylarAsync(int faturaId)
        {
            await DeleteFilteredAsync("fatura_detaylar", $"fatura_id=eq.{faturaId}");
        }

        public async Task<List<FaturaDetay>> PullFaturaDetaylarAsync(int faturaId)
        {
            using var doc = await GetJsonAsync("fatura_detaylar", $"fatura_id=eq.{faturaId}");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new List<FaturaDetay>();
            var list = new List<FaturaDetay>();
            foreach (var el in doc.RootElement.EnumerateArray()) list.Add(MapPayloadToFaturaDetay(el));
            return list;
        }

        // ==========================================
        // SİPARİŞLER & TEKLİFLER
        // ==========================================
        public async Task SyncSiparisAsync(Siparis siparis) => await UpsertPayloadAsync("siparisler", MapSiparisToPayload(siparis));
        public async Task DeleteSiparisAsync(int id)
        {
            await DeleteFilteredAsync("siparis_detaylar", $"siparis_id=eq.{id}");
            await DeleteAsync("siparisler", id);
        }
        public async Task<List<Siparis>?> PullSiparislerAsync()
        {
            using var doc = await GetJsonAsync("siparisler", "or=(is_deleted.is.null,is_deleted.eq.false)");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<Siparis>();
            foreach (var el in doc.RootElement.EnumerateArray()) list.Add(MapPayloadToSiparis(el));
            return list;
        }

        public async Task SyncSiparisDetaylarAsync(int siparisId, List<SiparisDetay> detaylar)
        {
            await SafeRun(async () =>
            {
                await DeleteFilteredAsync("siparis_detaylar", $"siparis_id=eq.{siparisId}");
                if (detaylar != null && detaylar.Count > 0)
                {
                    var payloads = new List<object>();
                    foreach (var d in detaylar)
                    {
                        d.SiparisId = siparisId;
                        payloads.Add(new
                        {
                            id = d.Id,
                            siparis_id = siparisId,
                            stok_id = d.StokId,
                            stok_adi = d.StokAdi ?? "",
                            miktar = (decimal)d.Miktar,
                            birim_fiyat = d.BirimFiyat,
                            kdv_orani = (decimal)d.KdvOrani,
                            toplam_tutar = d.Tutar
                        });
                    }
                    await UpsertBatchPayloadAsync("siparis_detaylar", payloads);
                }
            });
        }

        public async Task DeleteSiparisDetaylarAsync(int siparisId)
        {
            await DeleteFilteredAsync("siparis_detaylar", $"siparis_id=eq.{siparisId}");
        }

        public async Task<List<SiparisDetay>> PullSiparisDetaylarAsync(int siparisId)
        {
            using var doc = await GetJsonAsync("siparis_detaylar", $"siparis_id=eq.{siparisId}");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new List<SiparisDetay>();
            var list = new List<SiparisDetay>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var sd = new SiparisDetay();
                if (el.TryGetProperty("id", out var id)) sd.Id = ParseInt(id);
                if (el.TryGetProperty("siparis_id", out var sid)) sd.SiparisId = ParseInt(sid);
                if (el.TryGetProperty("stok_id", out var stid)) sd.StokId = ParseInt(stid);
                if (el.TryGetProperty("stok_adi", out var sa) && sa.ValueKind == JsonValueKind.String) sd.StokAdi = sa.GetString();
                if (el.TryGetProperty("miktar", out var mq)) sd.Miktar = ParseDecimal(mq);
                if (el.TryGetProperty("birim_fiyat", out var bf)) sd.BirimFiyat = ParseDecimal(bf);
                if (el.TryGetProperty("kdv_orani", out var ko)) sd.KdvOrani = ParseDouble(ko);
                if (el.TryGetProperty("toplam_tutar", out var tt)) sd.Tutar = ParseDecimal(tt);
                list.Add(sd);
            }
            return list;
        }

        public async Task SyncTeklifAsync(Teklif teklif) => await UpsertPayloadAsync("teklifler", MapTeklifToPayload(teklif));
        public async Task DeleteTeklifAsync(int id)
        {
            await DeleteFilteredAsync("teklif_detaylar", $"teklif_id=eq.{id}");
            await DeleteAsync("teklifler", id);
        }
        public async Task<List<Teklif>?> PullTekliflerAsync()
        {
            using var doc = await GetJsonAsync("teklifler", "or=(is_deleted.is.null,is_deleted.eq.false)");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<Teklif>();
            foreach (var el in doc.RootElement.EnumerateArray()) list.Add(MapPayloadToTeklif(el));
            return list;
        }

        public async Task SyncTeklifDetaylarAsync(int teklifId, List<TeklifDetay> detaylar)
        {
            await SafeRun(async () =>
            {
                await DeleteFilteredAsync("teklif_detaylar", $"teklif_id=eq.{teklifId}");
                if (detaylar != null && detaylar.Count > 0)
                {
                    var payloads = new List<object>();
                    foreach (var d in detaylar)
                    {
                        d.TeklifId = teklifId;
                        payloads.Add(new
                        {
                            id = d.Id,
                            teklif_id = teklifId,
                            stok_id = d.StokId,
                            stok_adi = d.StokAdi ?? "",
                            miktar = (decimal)d.Miktar,
                            birim_fiyat = d.BirimFiyat,
                            kdv_orani = (decimal)d.KdvOrani,
                            toplam_tutar = d.Tutar
                        });
                    }
                    await UpsertBatchPayloadAsync("teklif_detaylar", payloads);
                }
            });
        }

        public async Task DeleteTeklifDetaylarAsync(int teklifId)
        {
            await DeleteFilteredAsync("teklif_detaylar", $"teklif_id=eq.{teklifId}");
        }

        public async Task<List<TeklifDetay>> PullTeklifDetaylarAsync(int teklifId)
        {
            using var doc = await GetJsonAsync("teklif_detaylar", $"teklif_id=eq.{teklifId}");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new List<TeklifDetay>();
            var list = new List<TeklifDetay>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var td = new TeklifDetay();
                if (el.TryGetProperty("id", out var id)) td.Id = ParseInt(id);
                if (el.TryGetProperty("teklif_id", out var tid)) td.TeklifId = ParseInt(tid);
                if (el.TryGetProperty("stok_id", out var stid)) td.StokId = ParseInt(stid);
                if (el.TryGetProperty("stok_adi", out var sa) && sa.ValueKind == JsonValueKind.String) td.StokAdi = sa.GetString();
                if (el.TryGetProperty("miktar", out var mq)) td.Miktar = ParseDecimal(mq);
                if (el.TryGetProperty("birim_fiyat", out var bf)) td.BirimFiyat = ParseDecimal(bf);
                if (el.TryGetProperty("kdv_orani", out var ko)) td.KdvOrani = ParseDouble(ko);
                if (el.TryGetProperty("toplam_tutar", out var tt)) td.Tutar = ParseDecimal(tt);
                list.Add(td);
            }
            return list;
        }

        // ==========================================
        // KASALAR & BANKALAR
        // ==========================================
        public async Task SyncKasaHareketAsync(KasaHareket hareket) => await UpsertPayloadAsync("kasa_hareketler", MapKasaHareketToPayload(hareket));
        public async Task DeleteKasaHareketAsync(int id) => await DeleteAsync("kasa_hareketler", id);
        public async Task<List<KasaHareket>?> PullKasaHareketlerAsync()
        {
            using var doc = await GetJsonAsync("kasa_hareketler", "or=(is_deleted.is.null,is_deleted.eq.false)");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<KasaHareket>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var kh = MapPayloadToKasaHareket(el);
                if (kh != null) list.Add(kh);
            }
            return list;
        }

        public async Task SyncBankaAsync(BankaKart banka)
        {
            await UpsertPayloadAsync("bankalar", MapBankaToPayload(banka));
            if (banka.KartTuru == "Kasa")
            {
                await UpsertPayloadAsync("kasalar", MapKasaToPayload(banka));
            }
        }

        public async Task DeleteBankaAsync(int id)
        {
            await DeleteAsync("bankalar", id);
            await DeleteAsync("kasalar", id);
        }

        public async Task<List<BankaKart>?> PullBankalarAsync()
        {
            var list = new List<BankaKart>();
            var seenIds = new HashSet<int>();

            // 1. Pull from kasalar table
            try
            {
                using var docKasa = await GetJsonAsync("kasalar", "or=(is_deleted.is.null,is_deleted.eq.false)");
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
                using var doc = await GetJsonAsync("bankalar", "or=(is_deleted.is.null,is_deleted.eq.false)");
                if (doc != null && doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        var b = MapPayloadToBanka(el);
                        if (seenIds.Contains(b.Id))
                        {
                            var existing = list.FirstOrDefault(x => x.Id == b.Id);
                            if (existing != null && existing.KartTuru != "Kasa" && b.KartTuru == "Kasa")
                            {
                                existing.KartTuru = "Kasa";
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

        public async Task SyncBankaHareketAsync(BankaHareket hareket) => await UpsertPayloadAsync("banka_hareketler", MapBankaHareketToPayload(hareket));
        public async Task DeleteBankaHareketAsync(int id) => await DeleteAsync("banka_hareketler", id);
        public async Task<List<BankaHareket>?> PullBankaHareketlerAsync()
        {
            using var doc = await GetJsonAsync("banka_hareketler", "or=(is_deleted.is.null,is_deleted.eq.false)");
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
        public async Task SyncFirmaProfiliAsync(FirmaProfili profil)
        {
            var payload = new Dictionary<string, object?>
            {
                ["id"] = 1,
                ["unvan"] = profil.FirmaAdi ?? "",
                ["vergi_dairesi"] = profil.VergiDairesi,
                ["vergi_no"] = profil.VergiNo,
                ["adres"] = profil.Adres,
                ["telefon"] = profil.Telefon,
                ["email"] = profil.Eposta,
                ["web_sitesi"] = profil.WebSitesi,
                ["logo_base64"] = profil.LogoBase64
            };
            await UpsertPayloadAsync("firma_profili", payload);
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

        public async Task SyncFaturaTasarimiAsync(FaturaTasarimi tasarim) => await Task.CompletedTask;

        // ==========================================
        // MALİ YILLAR SENKRONİZASYONU
        // ==========================================
        public async Task SyncMaliYilAsync(int year)
        {
            if (year <= 0) return;
            var payload = new Dictionary<string, object?>
            {
                ["yil"] = year,
                ["is_active"] = true,
                ["is_deleted"] = false,
                ["olusturma_tarihi"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };
            await UpsertPayloadAsync("mali_yillar", payload);
        }

        public async Task SyncMaliYillarAsync(IEnumerable<int> years)
        {
            if (!IsConnected || years == null || !years.Any()) return;
            var payloads = years.Where(y => y > 0).Distinct().Select(y => (object)new Dictionary<string, object?>
            {
                ["yil"] = y,
                ["is_active"] = true,
                ["is_deleted"] = false,
                ["olusturma_tarihi"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            }).ToList();
            if (payloads.Any())
            {
                await UpsertBatchPayloadAsync("mali_yillar", payloads);
            }
        }

        public async Task DeleteMaliYilAsync(int year)
        {
            if (!IsConnected || year <= 0) return;
            await DeleteFilteredAsync("mali_yillar", $"yil=eq.{year}");
        }

        public async Task<List<int>?> PullMaliYillarAsync()
        {
            using var doc = await GetJsonAsync("mali_yillar", "or=(is_deleted.is.null,is_deleted.eq.false)");
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<int>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.TryGetProperty("yil", out var y))
                {
                    int val = ParseInt(y);
                    if (val > 0) list.Add(val);
                }
            }
            return list.OrderBy(y => y).ToList();
        }

        public async Task SyncNoteAsync(Note note)
        {
            var payload = new Dictionary<string, object?>
            {
                ["id"] = note.Id.ToString(),
                ["title"] = note.Title ?? "",
                ["baslik"] = note.Title ?? "",
                ["content"] = note.Content ?? "",
                ["icerik"] = note.Content ?? "",
                ["color"] = note.Color ?? "#0061FF",
                ["renk"] = note.Color ?? "#0061FF",
                ["is_pinned"] = note.IsPinned,
                ["is_deleted"] = note.IsDeleted
            };
            await UpsertPayloadAsync("notlar", payload);
        }

        public async Task DeleteNoteAsync(int id) => await DeleteAsync("notlar", id);

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

        public async Task<List<KrediKartiIslem>> PullKrediKartlariAsync() => new();
        public async Task<List<EftIslem>> PullEftIslemleriAsync() => new();
        public async Task<List<Cek>> PullCeklerAsync() => new();
        public async Task<List<Senet>> PullSenetlerAsync() => new();
        public async Task<List<MusteriTakipKlasor>> PullMusteriTakipKlasorlerAsync() => new();
        public async Task<List<MusteriTakipDetay>> PullMusteriTakipDetaylarAsync() => new();
        public async Task SyncGenericAsync<T>(string table, T item, object? id = null) => await Task.CompletedTask;

        public async Task<List<User>> PullUsersAsync()
        {
            if (!IsConnected) return new();
            try
            {
                // 1. First try pulling from public.kullanicilar table
                using var userDoc = await GetJsonAsync("kullanicilar");
                if (userDoc != null && userDoc.RootElement.ValueKind == JsonValueKind.Array && userDoc.RootElement.GetArrayLength() > 0)
                {
                    var list = new List<User>();
                    foreach (var el in userDoc.RootElement.EnumerateArray())
                    {
                        var u = new User();
                        if (el.TryGetProperty("username", out var un) && un.ValueKind == JsonValueKind.String) u.Username = un.GetString()?.ToLower();
                        if (el.TryGetProperty("password_hash", out var ph) && ph.ValueKind == JsonValueKind.String) u.Password = ph.GetString();
                        if (el.TryGetProperty("password_salt", out var ps) && ps.ValueKind == JsonValueKind.String) u.PasswordSalt = ps.GetString();
                        if (el.TryGetProperty("email", out var em) && em.ValueKind == JsonValueKind.String) u.Email = em.GetString();
                        if (el.TryGetProperty("role", out var rl) && rl.ValueKind == JsonValueKind.String) u.Role = rl.GetString();
                        if (!string.IsNullOrEmpty(u.Username)) list.Add(u);
                    }
                    if (list.Count > 0) return list;
                }

                // 2. Fallback to notlar table id=999999
                using var req = CreateRequest(HttpMethod.Get, "notlar?id=eq.999999&select=*");
                using var res = await _http.SendAsync(req);
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                    {
                        var first = doc.RootElement[0];
                        string? userJson = null;
                        if (first.TryGetProperty("icerik", out var icerikElem) && icerikElem.ValueKind == JsonValueKind.String)
                            userJson = icerikElem.GetString();
                        else if (first.TryGetProperty("content", out var contentElem) && contentElem.ValueKind == JsonValueKind.String)
                            userJson = contentElem.GetString();

                        if (!string.IsNullOrEmpty(userJson))
                        {
                            return JsonSerializer.Deserialize<List<User>>(userJson, _jsonOpts) ?? new();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PullUsersAsync Error] {ex.Message}");
            }
            return new();
        }

        public async Task SyncUserAsync(User user)
        {
            if (!IsConnected || user == null) return;
            try
            {
                // 1. Save to public.kullanicilar table
                var userRow = new Dictionary<string, object?>
                {
                    ["id"] = (user.Username ?? "").ToLower(),
                    ["username"] = (user.Username ?? "").ToLower(),
                    ["kullanici_adi"] = (user.Username ?? "").ToLower(),
                    ["password_hash"] = user.Password ?? "",
                    ["sifre"] = user.Password ?? "",
                    ["password_salt"] = user.PasswordSalt ?? "",
                    ["email"] = user.Email,
                    ["role"] = user.Role ?? "Admin",
                    ["rol"] = user.Role ?? "Admin",
                    ["is_active"] = true,
                    ["aktif_mi"] = true
                };
                await UpsertPayloadAsync("kullanicilar", userRow);

                // 2. Fallback / mirror to notlar
                var currentUsers = await PullUsersAsync();
                var existing = currentUsers.FirstOrDefault(u => (u.Username ?? "").Equals(user.Username ?? "", StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Password = user.Password;
                    existing.PasswordSalt = user.PasswordSalt;
                    existing.Role = user.Role;
                    existing.Email = user.Email;
                    existing.TenantId = user.TenantId;
                }
                else
                {
                    currentUsers.Add(user);
                }

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

        public async Task SyncCekAsync(Cek cek) => await Task.CompletedTask;
        public async Task SyncSenetAsync(Senet senet) => await Task.CompletedTask;
        public async Task SyncKrediKartiIslemAsync(KrediKartiIslem islem) => await Task.CompletedTask;
        public async Task SyncEftIslemAsync(EftIslem islem) => await Task.CompletedTask;
        public async Task SyncStokSayimFisiAsync(StokSayimFisi fis) => await Task.CompletedTask;
        public async Task SyncStokSayimDetaylarAsync(int fisId, List<StokSayimDetay> detaylar) => await Task.CompletedTask;
        public async Task DeleteCekAsync(int id) => await Task.CompletedTask;
        public async Task DeleteSenetAsync(int id) => await Task.CompletedTask;
        public async Task DeleteKrediKartiIslemAsync(int id) => await Task.CompletedTask;
        public async Task DeleteEftIslemAsync(int id) => await Task.CompletedTask;
        public async Task DeleteStokSayimFisiAsync(int id) => await Task.CompletedTask;

        public async Task SyncGorevAsync(Gorev gorev) => await Task.CompletedTask;
        public async Task DeleteGorevAsync(int id) => await Task.CompletedTask;
        public async Task<List<Gorev>> PullGorevlerAsync() => new();

        public async Task SyncPersonelAsync(Personel personel) => await Task.CompletedTask;
        public async Task DeletePersonelAsync(int id) => await Task.CompletedTask;
        public async Task<List<Personel>> PullPersonellerAsync() => new();

        public async Task SyncSatisHedefiAsync(SatisHedefi hedef) => await Task.CompletedTask;
        public async Task<List<SatisHedefi>> PullSatisHedefleriAsync() => new();

        public async Task SyncHaftalikSatisHedefiAsync(HaftalikSatisHedefi hedef) => await Task.CompletedTask;
        public async Task<List<HaftalikSatisHedefi>> PullHaftalikSatisHedefleriAsync() => new();

        public async Task SyncYillikSatisHedefiAsync(YillikSatisHedefi hedef) => await Task.CompletedTask;
        public async Task<List<YillikSatisHedefi>> PullYillikSatisHedefleriAsync() => new();

        public async Task SyncPortfoyAsync(PortfoyKart portfoy) => await Task.CompletedTask;
        public async Task DeletePortfoyAsync(int id) => await Task.CompletedTask;
        public async Task<List<PortfoyKart>> PullPortfoyAsync() => new();

        public async Task<List<StokSayimFisi>> PullStokSayimlarAsync() => new();
        public async Task<List<StokSayimDetay>> PullStokSayimDetaylarAsync(int fisId) => new();
        public async Task<List<DovizKur>?> PullDovizKurlariAsync() => new();
        public async Task<List<BelgeArsiv>?> PullBelgeArsivAsync() => new();

        public async Task ClearCloudTablesAsync(string tenantId = "default")
        {
            if (!IsConnected) return;

            // Delete rows in reverse dependency order (detail/child tables first, parent tables last)
            // to satisfy foreign key constraints in PostgreSQL.
            // Note: 'firma_profili' and 'kullanicilar' are preserved to retain system & connection settings.
            var tablesToClear = new[]
            {
                // 1. Details & dependent transactional items (must be cleared before parent invoices/stocks/caris/banks)
                "fatura_detaylar",
                "siparis_detaylar",
                "teklif_detaylar",
                "stok_sayim_detaylari",
                "musteri_takip_detaylar",
                "kredi_karti_islemler",
                "eft_islemler",
                "cekler",
                "senetler",
                "cari_hareketler",
                "stok_hareketler",
                "banka_hareketler",
                "kasa_hareketler",
                "stok_sayim_fisileri",
                "musteri_takip_klasorler",
                "gorevler",
                "personeller",
                "satis_hedefleri",
                "haftalik_satis_hedefleri",
                "yillik_satis_hedefleri",
                "portfoy_kartlar",
                "belge_arsiv",
                "doviz_kurlari",

                // 2. Parent documents & master cards
                "faturalar",
                "siparisler",
                "teklifler",
                "stoklar",
                "cariler",
                "bankalar",
                "kasalar",
                "notlar",

                // 3. Fiscal years
                "mali_yillar"
            };

            // Multi-pass direct deletion to ensure any indirect foreign key dependencies are completely cleared
            for (int pass = 1; pass <= 3; pass++)
            {
                foreach (var table in tablesToClear)
                {
                    try
                    {
                        string filter = table == "mali_yillar" ? "yil=gte.0" : "id=not.is.null";
                        using var req = CreateRequest(HttpMethod.Delete, $"{table}?{filter}");
                        req.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
                        using var res = await _http.SendAsync(req);
                        if (!res.IsSuccessStatusCode)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CloudSync] ClearCloudTable '{table}' (Pass {pass}) returned status {res.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CloudSync] ClearCloudTable '{table}' (Pass {pass}) error: {ex.Message}");
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine("[CloudSync] All cloud business tables cleared successfully.");
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
                if (cariler?.Any() == true)
                {
                    var payloads = cariler.Select(MapCariToPayload).ToList<object>();
                    await UpsertBatchPayloadAsync("cariler", payloads);
                }
                if (stoklar?.Any() == true)
                {
                    var payloads = stoklar.Select(MapStokToPayload).ToList<object>();
                    await UpsertBatchPayloadAsync("stoklar", payloads);
                }
                if (faturalar?.Any() == true)
                {
                    var payloads = faturalar.Select(MapFaturaToPayload).ToList<object>();
                    await UpsertBatchPayloadAsync("faturalar", payloads);
                }
                if (faturaDetaylar?.Any() == true)
                {
                    var payloads = faturaDetaylar.Select(MapFaturaDetayToPayload).ToList<object>();
                    await UpsertBatchPayloadAsync("fatura_detaylar", payloads);
                }
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
                if (bankalar?.Any() == true)
                {
                    var payloads = bankalar.Select(MapBankaToPayload).ToList<object>();
                    await UpsertBatchPayloadAsync("bankalar", payloads);

                    var kasalar = bankalar.Where(b => b.KartTuru == "Kasa").Select(MapKasaToPayload).ToList<object>();
                    if (kasalar.Any())
                    {
                        await UpsertBatchPayloadAsync("kasalar", kasalar);
                    }
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
                if (notes?.Any() == true)
                {
                    var payloads = notes.Select(n => (object)new
                    {
                        id = n.Id,
                        baslik = n.Title ?? "",
                        icerik = n.Content ?? "",
                        renk = n.Color ?? "#0061FF"
                    }).ToList();
                    await UpsertBatchPayloadAsync("notlar", payloads);
                }
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
