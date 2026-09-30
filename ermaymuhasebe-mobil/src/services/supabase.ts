import { createClient, SupabaseClient } from '@supabase/supabase-js';
import { AppState } from 'react-native';
import AsyncStorage from './storage';
import { generateMobileRecordId } from '../utils/IdGenerator';

export interface SupabaseConfig {
  url: string;
  anonKey: string;
  tenantId?: string;
}

export interface FirebaseConfig {
  url: string;
  secret?: string;
  tenantId?: string;
}

export interface ExtendedFirebaseConfig extends FirebaseConfig {
  googleApiKey?: string;
  googleClientId?: string;
  googleClientSecret?: string;
  telegramBotToken?: string;
  telegramChatId?: string;
  smtpEmail?: string;
  smtpPass?: string;
}

export const DEFAULT_SUPABASE_URL = 'https://fqgbdymffknglqeqoogt.supabase.co';
export const DEFAULT_SUPABASE_KEY = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImZxZ2JkeW1mZmtuZ2xxZXFvb2d0Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODk1ODUzMDEsImV4cCI6MjEwNTE2MTMwMX0.pBeE2ivWpbkAd8KSN1y2pXNZPIr_1mGMLXXHYPzjTDg';

let cachedConfig: SupabaseConfig = {
  url: DEFAULT_SUPABASE_URL,
  anonKey: DEFAULT_SUPABASE_KEY,
  tenantId: 'default'
};

let cachedExtendedConfig: ExtendedFirebaseConfig = {
  url: DEFAULT_SUPABASE_URL,
  secret: DEFAULT_SUPABASE_KEY,
  tenantId: 'default'
};

let cachedYear: string = new Date().getFullYear().toString();
let cachedSessionUser: any = null;
let supabase: SupabaseClient = createClient(
  cachedConfig.url || 'https://placeholder.supabase.co',
  cachedConfig.anonKey || 'placeholder-key'
);

let configListeners: (() => void)[] = [];

export const getSupabaseClient = () => supabase;

// --- Helper Case Conversion ---
export const toSnakeCase = (obj: any): any => {
  if (!obj || typeof obj !== 'object') return obj;
  if (Array.isArray(obj)) return obj.map(toSnakeCase);
  const newObj: any = {};
  for (const key of Object.keys(obj)) {
    const snakeKey = key.replace(/[A-Z]/g, letter => `_${letter.toLowerCase()}`);
    newObj[snakeKey] = toSnakeCase(obj[key]);
  }
  return newObj;
};

export const toCamelCase = (obj: any): any => {
  if (!obj || typeof obj !== 'object') return obj;
  if (Array.isArray(obj)) return obj.map(toCamelCase);
  const newObj: any = {};
  for (const key of Object.keys(obj)) {
    const camelKey = key.replace(/_([a-z0-9])/g, (_, g) => g.toUpperCase());
    newObj[camelKey] = toCamelCase(obj[key]);
  }
  return newObj;
};

// --- Table Mapping ---
const TABLE_MAPPINGS: Record<string, string> = {
  'cariler': 'cariler',
  'carihareketler': 'cari_hareketler',
  'cari_hareketler': 'cari_hareketler',
  'stoklar': 'stoklar',
  'stokhareketler': 'stok_hareketler',
  'stok_hareketler': 'stok_hareketler',
  'stokgruplar': 'stok_gruplar',
  'stok_gruplar': 'stok_gruplar',
  'faturalar': 'faturalar',
  'faturadetaylar': 'fatura_detaylar',
  'fatura_detaylar': 'fatura_detaylar',
  'siparisler': 'siparisler',
  'siparisdetaylar': 'siparis_detaylar',
  'siparis_detaylar': 'siparis_detaylar',
  'teklifler': 'teklifler',
  'teklifdetaylar': 'teklif_detaylar',
  'teklif_detaylar': 'teklif_detaylar',
  'kasalar': 'kasalar',
  'kasahareketler': 'kasa_hareketler',
  'kasa_hareketler': 'kasa_hareketler',
  'bankalar': 'bankalar',
  'bankahareketler': 'banka_hareketler',
  'banka_hareketler': 'banka_hareketler',
  'firmaprofili': 'firma_profili',
  'firma_profili': 'firma_profili',
  'notes': 'notlar',
  'notlar': 'notlar',
  'maliyillar': 'mali_yillar',
  'mali_yillar': 'mali_yillar',
  'cekler': 'cekler',
  'senetler': 'senetler',
  'kredikartiislemler': 'kredi_karti_islemler',
  'kredi_karti_islemler': 'kredi_karti_islemler',
  'eftislemler': 'eft_islemler',
  'eft_islemler': 'eft_islemler',
  'dovizkurlari': 'doviz_kurlari',
  'doviz_kurlari': 'doviz_kurlari',
  'belgearsiv': 'belge_arsiv',
  'belge_arsiv': 'belge_arsiv',
  'gorevler': 'gorevler',
  'personeller': 'personeller',
  'satishedefleri': 'satis_hedefleri',
  'satis_hedefleri': 'satis_hedefleri',
  'haftaliksatishedefleri': 'haftalik_satis_hedefleri',
  'haftalik_satis_hedefleri': 'haftalik_satis_hedefleri',
  'yilliksatishedefleri': 'yillik_satis_hedefleri',
  'yillik_satis_hedefleri': 'yillik_satis_hedefleri',
  'stoksayimlar': 'stok_sayim_fisileri',
  'stoksayimfisileri': 'stok_sayim_fisileri',
  'stok_sayim_fisileri': 'stok_sayim_fisileri',
  'stoksayimdetaylar': 'stok_sayim_detaylari',
  'stoksayimdetaylari': 'stok_sayim_detaylari',
  'stok_sayim_detaylari': 'stok_sayim_detaylari',
  'portfoykartlar': 'portfoy_kartlar',
  'portfoykartlari': 'portfoy_kartlar',
  'portfoy_kartlar': 'portfoy_kartlar',
  'musteritakipklasorler': 'musteri_takip_klasorler',
  'musteri_takip_klasorler': 'musteri_takip_klasorler',
  'musteritakipdetaylar': 'musteri_takip_detaylar',
  'musteri_takip_detaylar': 'musteri_takip_detaylar',
  'kullanicilar': 'kullanicilar',
  'users': 'kullanicilar'
};

export const parsePath = (path: string): { table: string; id?: string; field?: string; isDetail?: boolean; foreignKey?: string } => {
  let clean = path.replace(/^companies\/[^\/]+\/(years\/[^\/]+\/)?/, '');
  clean = clean.replace(/^companies\/[^\/]+\//, '');
  clean = clean.replace(/^years\/[^\/]+\//, '');
  clean = clean.replace(/^\/+|\/+$/g, '');
  const parts = clean.split('/');

  const rawTable = (parts[0] || '').toLowerCase();
  const table = TABLE_MAPPINGS[rawTable] || rawTable;
  const id = parts[1];
  const field = parts[2];

  if (table === 'fatura_detaylar' && id) {
    return { table, id, isDetail: true, foreignKey: 'fatura_id' };
  }
  if (table === 'siparis_detaylar' && id) {
    return { table, id, isDetail: true, foreignKey: 'siparis_id' };
  }
  if (table === 'teklif_detaylar' && id) {
    return { table, id, isDetail: true, foreignKey: 'teklif_id' };
  }

  return { table, id, field };
};

// --- Storage & Config ---
export const cleanSupabaseUrl = (rawUrl?: string): string => {
  if (!rawUrl) return '';
  let clean = rawUrl.trim();
  while (clean.endsWith('/')) clean = clean.substring(0, clean.length - 1);
  if (clean.toLowerCase().endsWith('/rest/v1'))
    clean = clean.substring(0, clean.length - '/rest/v1'.length);
  while (clean.endsWith('/')) clean = clean.substring(0, clean.length - 1);
  return clean;
};

export const loadConfigFromStorage = async () => {
  try {
    const rawUrl = await AsyncStorage.getItem('ermay_supabase_url');
    const key = await AsyncStorage.getItem('ermay_supabase_key');
    const yr = await AsyncStorage.getItem('ermay_active_year');
    if (rawUrl && key) {
      const url = cleanSupabaseUrl(rawUrl);
      cachedConfig = { url, anonKey: key.trim(), tenantId: 'default' };
      supabase = createClient(url, key.trim());
    }
    if (yr) cachedYear = yr;
  } catch (e) {
    console.log('[Supabase] loadConfigFromStorage error:', e);
  }
};

export const saveSupabaseConfig = async (rawUrl: string, anonKey: string) => {
  const url = cleanSupabaseUrl(rawUrl);
  const cleanKey = (anonKey || '').trim();
  cachedConfig = { url, anonKey: cleanKey, tenantId: 'default' };
  supabase = createClient(url, cleanKey);
  await AsyncStorage.setItem('ermay_supabase_url', url);
  await AsyncStorage.setItem('ermay_supabase_key', cleanKey);
  notifyConfigListeners();
};

export const saveActiveYear = async (year: string) => {
  cachedYear = year;
  await AsyncStorage.setItem('ermay_active_year', year);
  notifyConfigListeners();
};

export const addConfigListener = (listener: () => void): (() => void) => {
  configListeners.push(listener);
  return () => {
    configListeners = configListeners.filter(l => l !== listener);
  };
};

export const notifyConfigListeners = () => {
  configListeners.forEach(l => {
    try { l(); } catch (e) {}
  });
};

// --- Compatibility Aliases for screens ---
export const getFirebaseConfig = (): FirebaseConfig | null => ({
  url: cachedConfig.url,
  secret: cachedConfig.anonKey,
  tenantId: cachedConfig.tenantId
});

export const getExtendedConfig = (): ExtendedFirebaseConfig | null => cachedExtendedConfig;

export const saveFirebaseConfig = async (
  url: string,
  secret?: string,
  tenantId: string = 'default',
  extra?: Partial<ExtendedFirebaseConfig>
) => {
  if (url && secret) {
    await saveSupabaseConfig(url, secret);
  }
  cachedExtendedConfig = {
    url,
    secret,
    tenantId,
    ...extra
  };
};

export const fetchAvailableYears = async (): Promise<string[]> => {
  try {
    // 1. Doğrudan mali_yillar tablosunu sorgula
    const { data, error } = await supabase
      .from('mali_yillar')
      .select('yil')
      .or('is_deleted.is.null,is_deleted.eq.false')
      .order('yil', { ascending: true });

    if (!error && data && data.length > 0) {
      const years = data
        .map((r: any) => String(r.yil))
        .filter((y: string) => /^\d{4}$/.test(y));
      if (years.length > 0) {
        return Array.from(new Set(years)).sort();
      }
    }

    // 2. Tablo yoksa veya henüz kayıt girilmemişse hareketler ve faturalardaki tarihlerden distinct yılları bul
    const distinctYears = new Set<string>();

    try {
      const { data: faturaData } = await supabase
        .from('faturalar')
        .select('tarih')
        .or('is_deleted.is.null,is_deleted.eq.false')
        .limit(200);

      if (faturaData) {
        faturaData.forEach((f: any) => {
          if (f.tarih) {
            const yr = new Date(f.tarih).getFullYear();
            if (yr >= 2000 && yr <= 2100) distinctYears.add(yr.toString());
          }
        });
      }
    } catch {}

    try {
      const { data: chData } = await supabase
        .from('cari_hareketler')
        .select('tarih')
        .or('is_deleted.is.null,is_deleted.eq.false')
        .limit(200);

      if (chData) {
        chData.forEach((h: any) => {
          if (h.tarih) {
            const yr = new Date(h.tarih).getFullYear();
            if (yr >= 2000 && yr <= 2100) distinctYears.add(yr.toString());
          }
        });
      }
    } catch {}

    if (distinctYears.size > 0) {
      return Array.from(distinctYears).sort();
    }

    // 3. Veritabanı tamamen boşsa: Kesinlikle uydurma 3 yıl dönme!
    // Boş liste dön ki kullanıcı masaüstündeki gibi "Yıl Oluştur" panelini görsün.
    return [];
  } catch (err) {
    console.warn('[Supabase] fetchAvailableYears error:', err);
    return [];
  }
};

export const createNewMaliYil = async (year: string): Promise<boolean> => {
  try {
    const yrNum = parseInt(year, 10);
    if (isNaN(yrNum) || yrNum < 2000 || yrNum > 2100) return false;

    const { error } = await supabase.from('mali_yillar').upsert({
      yil: yrNum,
      is_active: true,
      is_deleted: false,
      olusturma_tarihi: new Date().toISOString()
    });

    if (error) {
      console.warn('[Supabase] createNewMaliYil upsert error, fallback to insert:', error.message);
      await supabase.from('mali_yillar').insert({
        yil: yrNum,
        is_active: true,
        is_deleted: false,
        olusturma_tarihi: new Date().toISOString()
      });
    }

    await saveActiveYear(year);
    return true;
  } catch (e: any) {
    console.error('[Supabase] createNewMaliYil error:', e.message);
    return false;
  }
};

export const deleteMaliYil = async (year: string): Promise<boolean> => {
  try {
    const yrNum = parseInt(year, 10);
    if (isNaN(yrNum)) return false;

    // 1. mali_yillar tablosundan soft-delete ve kalıcı silme
    try {
      await supabase.from('mali_yillar').update({ is_deleted: true, is_active: false }).eq('yil', yrNum);
      await supabase.from('mali_yillar').delete().eq('yil', yrNum);
    } catch {}

    // 2. Eğer silinen yıl aktif yıl ise storage'ı temizle
    const currentActive = await AsyncStorage.getItem('ermay_active_year');
    if (currentActive === year) {
      await AsyncStorage.removeItem('ermay_active_year');
      cachedYear = '';
    }

    notifyConfigListeners();
    return true;
  } catch (e: any) {
    console.error('[Supabase] deleteMaliYil error:', e.message);
    return false;
  }
};

export const getIdToken = () => null;
export const saveIdToken = async () => {};
export const mapDatabaseToApp = (path: string, val: any): any => {
  if (!val || typeof val !== 'object') return val;
  if (Array.isArray(val)) return val.map(v => mapDatabaseToApp(path, v));
  const res: any = {};
  for (const k of Object.keys(val)) {
    let appKey = k.charAt(0).toLowerCase() + k.slice(1);
    if (k === 'Email') appKey = 'eposta';
    else if (k === 'IBAN') appKey = 'iban';
    else if (k === 'TCNo') appKey = 'tcNo';
    else if (k === 'Title') appKey = 'baslik';
    else if (k === 'Content') appKey = 'aciklama';
    else if (k === 'KDV' && path.toLowerCase().includes('faturadetaylar')) appKey = 'kdvOrani';
    else if (k === 'KDV') appKey = 'kdv';
    res[appKey] = mapDatabaseToApp(path, val[k]);
  }
  return res;
};

export const mapAppToDatabase = (path: string, val: any): any => {
  if (!val || typeof val !== 'object') return val;
  if (Array.isArray(val)) return val.map(v => mapAppToDatabase(path, v));
  const res: any = {};
  for (const k of Object.keys(val)) {
    let dbKey = k.charAt(0).toUpperCase() + k.slice(1);
    if (k === 'eposta') dbKey = 'Email';
    else if (k === 'iban') dbKey = 'IBAN';
    else if (k === 'tcNo') dbKey = 'TCNo';
    else if (k === 'kdvOrani' && path.toLowerCase().includes('faturadetaylar')) dbKey = 'KDVOrani';
    else if (k === 'kdv') dbKey = 'KDV';
    res[dbKey] = mapAppToDatabase(path, val[k]);
  }
  return res;
};

export const mapPathToDatabase = (path: string): string => {
  if (path.startsWith('companies/') || path.startsWith('users/') || path.startsWith('security_requests/') || path.toLowerCase().startsWith('firmaprofili')) return path;
  return `companies/${cachedConfig.tenantId || 'default'}/years/${cachedYear}/${path}`;
};

export const getCacheKey = (path: string): string => {
  return `ermay_cache_${path}`;
};
export const fetchWithTimeout = async (url: string, opts?: any, timeoutMs?: number): Promise<Response> => fetch(url, opts);
export const getAuthParam = (config?: any): string => '';

// --- Table Schema Sanitization & Normalization ---
export const sanitizePayloadForTable = (table: string, data: any): any => {
  if (!data || typeof data !== 'object') return data;
  const t = table.toLowerCase();

  if (t === 'cariler') {
    const p: any = {};
    if (data.id !== undefined) p.id = Number(data.id) || data.id;
    p.cari_kodu = data.cariKodu || data.cariKod || data.kod || data.cari_kodu || '';
    p.kod = p.cari_kodu;
    p.unvan = data.unvan || '';
    if (data.vergiDairesi !== undefined || data.vergi_dairesi !== undefined) p.vergi_dairesi = data.vergiDairesi ?? data.vergi_dairesi;
    if (data.vergiNo !== undefined || data.vergi_no !== undefined) p.vergi_no = data.vergiNo ?? data.vergi_no;
    if (data.tcNo !== undefined || data.tcKimlikNo !== undefined || data.tc_kimlik_no !== undefined) p.tc_kimlik_no = data.tcNo ?? data.tcKimlikNo ?? data.tc_kimlik_no;
    if (data.adres !== undefined) p.adres = data.adres;
    p.sehir = data.sehir || data.il || '';
    p.il = p.sehir;
    if (data.ilce !== undefined) p.ilce = data.ilce;
    if (data.telefon !== undefined) p.telefon = data.telefon;
    if (data.telefon2 !== undefined || data.cepTelefon !== undefined || data.cep_telefon !== undefined) p.telefon2 = data.telefon2 ?? data.cepTelefon ?? data.cep_telefon;
    p.yetkili_kisi = data.yetkiliKisi || data.yetkili || data.yetkili_kisi || '';
    p.email = data.email || data.eposta || '';
    p.eposta = p.email;
    p.web_sitesi = data.webSitesi || data.webAdresi || data.web_sitesi || '';
    if (data.notlar !== undefined || data.aciklama !== undefined) p.notlar = data.notlar ?? data.aciklama;
    p.bakiye = Number(data.bakiye) || 0;
    p.borc_tutari = Number(data.borcTutari ?? data.borc ?? data.borc_tutari) || 0;
    p.alacak_tutari = Number(data.alacakTutari ?? data.alacak ?? data.alacak_tutari) || 0;
    p.borc = p.borc_tutari;
    p.alacak = p.alacak_tutari;
    if (data.krediLimiti !== undefined || data.riskLimiti !== undefined || data.risk_limiti !== undefined) p.kredi_limiti = Number(data.krediLimiti ?? data.riskLimiti ?? data.risk_limiti) || 0;
    if (data.vadeGun !== undefined || data.vadeGunu !== undefined || data.vade_gunu !== undefined) p.vade_gun = Number(data.vadeGun ?? data.vadeGunu ?? data.vade_gunu) || 0;
    if (data.iskontoOrani !== undefined || data.iskonto_orani !== undefined) p.iskonto_orani = Number(data.iskontoOrani ?? data.iskonto_orani) || 0;
    if (data.grup !== undefined) p.grup = data.grup;
    if (data.sektor !== undefined) p.sektor = data.sektor;
    p.is_active = data.isActive !== false && data.is_active !== false && data.aktifMi !== false;
    p.is_deleted = data.isDeleted === true || data.is_deleted === true;
    p.guncelleme_tarihi = new Date().toISOString();
    return p;
  }

  if (t === 'stoklar') {
    const p: any = {};
    if (data.id !== undefined) p.id = Number(data.id) || data.id;
    p.stok_kodu = data.stokKodu || data.kod || data.stok_kodu || '';
    p.stok_adi = data.stokAdi || data.ad || data.stok_adi || '';
    if (data.barkod !== undefined) p.barkod = data.barkod;
    p.grup_adi = data.grupAdi || data.grup || data.kategori || data.grup_adi || '';
    p.grup = p.grup_adi;
    p.birim = data.birim || 'Adet';
    p.alis_fiyati = Number(data.alisFiyati ?? data.alis_fiyati) || 0;
    p.satis_fiyati = Number(data.satisFiyati ?? data.satis_fiyati) || 0;
    p.kdv_orani = Number(data.kdvOrani ?? data.kdv ?? data.kdv_orani) || 20;
    p.mevcut_miktar = Number(data.mevcutMiktar ?? data.miktar ?? data.mevcut_miktar) || 0;
    p.miktar = p.mevcut_miktar;
    p.kritik_seviye = Number(data.kritikSeviye ?? data.minSeviye ?? data.kritik_seviye) || 0;
    p.kritik_stok = p.kritik_seviye;
    if (data.aciklama !== undefined) p.aciklama = data.aciklama;
    if (data.resimUrl !== undefined || data.resim_url !== undefined) p.resim_url = data.resimUrl ?? data.resim_url;
    p.is_active = data.isActive !== false && data.is_active !== false;
    p.is_deleted = data.isDeleted === true || data.is_deleted === true;
    p.guncelleme_tarihi = new Date().toISOString();
    return p;
  }

  if (t === 'faturalar') {
    const p: any = {};
    if (data.id !== undefined) p.id = Number(data.id) || data.id;
    p.fatura_no = data.faturaNo || data.fatura_no || '';
    p.fatura_turu = data.faturaTuru || data.tur || data.fatura_turu || 'Satis';
    p.cari_id = Number(data.cariId ?? data.cari_id) || null;
    p.cari_unvan = data.cariUnvan || data.cari_unvan || '';
    p.tarih = data.tarih || new Date().toISOString();
    p.vade_tarihi = data.vadeTarihi || data.vade_tarihi || p.tarih;
    p.ara_toplam = Number(data.araToplam ?? data.ara_toplam) || 0;
    p.kdv_toplam = Number(data.kdvToplam ?? data.toplamKdv ?? data.kdv_toplam) || 0;
    p.iskonto_toplam = Number(data.iskontoToplam ?? data.iskonto_toplam) || 0;
    p.genel_toplam = Number(data.genelToplam ?? data.genel_toplam) || 0;
    if (data.aciklama !== undefined) p.aciklama = data.aciklama;
    p.is_kapali = data.isKapali === true || data.is_kapali === true || (Number(p.genel_toplam) > 0 && Number(data.odenen) >= Number(p.genel_toplam));
    if (data.kasaId !== undefined || data.kasa_id !== undefined) p.kasa_id = Number(data.kasaId ?? data.kasa_id) || null;
    if (data.bankaId !== undefined || data.banka_id !== undefined) p.banka_id = Number(data.bankaId ?? data.banka_id) || null;
    p.is_deleted = data.isDeleted === true || data.is_deleted === true;
    p.guncelleme_tarihi = new Date().toISOString();
    return p;
  }

  if (t === 'fatura_detaylar') {
    const p: any = {};
    if (data.id !== undefined) p.id = Number(data.id) || data.id;
    p.fatura_id = Number(data.faturaId ?? data.fatura_id) || 0;
    p.stok_id = Number(data.stokId ?? data.stok_id) || null;
    p.stok_kodu = data.stokKodu || data.stok_kodu || '';
    p.stok_adi = data.stokAdi || data.stok_adi || '';
    p.miktar = Number(data.miktar) || 0;
    p.birim = data.birim || 'Adet';
    p.birim_fiyat = Number(data.birimFiyat ?? data.birim_fiyat) || 0;
    p.kdv_orani = Number(data.kdvOrani ?? data.kdv ?? data.kdv_orani) || 0;
    p.kdv_tutari = Number(data.kdvTutari ?? data.kdv_tutari) || 0;
    p.iskonto_orani = Number(data.iskontoOrani ?? data.iskonto_orani) || 0;
    p.iskonto_tutari = Number(data.iskontoTutari ?? data.iskonto_tutari) || 0;
    p.toplam_tutar = Number(data.toplamTutar ?? data.tutar ?? data.toplam_tutar) || 0;
    if (data.aciklama !== undefined) p.aciklama = data.aciklama;
    return p;
  }

  if (t === 'cari_hareketler') {
    const p: any = {};
    if (data.id !== undefined) p.id = Number(data.id) || data.id;
    p.cari_id = Number(data.cariId ?? data.cari_id) || 0;
    p.tarih = data.tarih || new Date().toISOString();
    p.islem_turu = data.islemTuru || data.islem_turu || '';
    p.evrak_no = data.evrakNo || data.evrak_no || '';
    p.aciklama = data.aciklama || '';
    p.borc = Number(data.borc) || 0;
    p.alacak = Number(data.alacak) || 0;
    p.bakiye = Number(data.bakiye) || 0;
    if (data.faturaId !== undefined || data.fatura_id !== undefined) p.fatura_id = Number(data.faturaId ?? data.fatura_id) || null;
    p.is_deleted = data.isDeleted === true || data.is_deleted === true;
    return p;
  }

  if (t === 'stok_hareketler') {
    const p: any = {};
    if (data.id !== undefined) p.id = Number(data.id) || data.id;
    p.stok_id = Number(data.stokId ?? data.stok_id) || 0;
    p.tarih = data.tarih || new Date().toISOString();
    p.hareket_turu = data.hareketTuru || data.islemTuru || data.hareket_turu || (Number(data.giren) > 0 ? 'GİRİŞ' : 'ÇIKIŞ');
    p.evrak_no = data.evrakNo || data.evrak_no || '';
    p.miktar = Number(data.miktar ?? (Number(data.giren) > 0 ? data.giren : data.cikan)) || 0;
    p.birim_fiyat = Number(data.birimFiyat ?? data.fiyat ?? data.birim_fiyat) || 0;
    p.toplam_tutar = Number(data.toplamTutar ?? (p.miktar * p.birim_fiyat)) || 0;
    p.kdv_orani = Number(data.kdvOrani ?? data.kdv_orani) || 0;
    p.kdv_tutari = Number(data.kdvTutari ?? data.kdv_tutari) || 0;
    if (data.faturaId !== undefined || data.fatura_id !== undefined) p.fatura_id = Number(data.faturaId ?? data.fatura_id) || null;
    if (data.cariId !== undefined || data.cari_id !== undefined) p.cari_id = Number(data.cariId ?? data.cari_id) || null;
    if (data.aciklama !== undefined) p.aciklama = data.aciklama;
    return p;
  }

  if (t === 'bankalar') {
    const p: any = {};
    if (data.id !== undefined) p.id = Number(data.id) || data.id;
    p.banka_adi = data.bankaAdi || data.banka_adi || data.hesapAdi || data.ad || '';
    p.sube_adi = data.subeAdi || data.sube || data.sube_adi || '';
    p.hesap_no = data.hesapNo || data.hesap_no || '';
    p.iban = data.iban || data.iBAN || '';
    p.bakiye = Number(data.bakiye) || 0;
    p.para_birimi = data.paraBirimi || data.dovizTuru || data.para_birimi || 'TRY';
    let aciklama = data.aciklama;
    if (data.kartTuru === 'Kasa' || data.turu === 'Kasa') {
      aciklama = '[KASA]' + (data.yetkili ? `|${data.yetkili}` : (aciklama ? `|${aciklama}` : ''));
    } else if (data.yetkili && !aciklama) {
      aciklama = data.yetkili;
    }
    if (aciklama !== undefined) p.aciklama = aciklama;
    p.is_active = data.isActive !== false && data.is_active !== false;
    p.is_deleted = data.isDeleted === true || data.is_deleted === true;
    return p;
  }

  if (t === 'banka_hareketler') {
    const p: any = {};
    if (data.id !== undefined) p.id = Number(data.id) || data.id;
    p.banka_id = Number(data.bankaId ?? data.banka_id) || 0;
    p.tarih = data.tarih || new Date().toISOString();
    p.hareket_turu = data.hareketTuru || data.islemTuru || data.hareket_turu || (Number(data.yatan ?? data.giren) > 0 ? 'YATAN' : 'ÇEKEN');
    p.evrak_no = data.evrakNo || data.evrak_no || '';
    if (data.aciklama !== undefined) p.aciklama = data.aciklama;
    const tutar = Number(data.tutar) || 0;
    const isYatan = String(p.hareket_turu).toUpperCase().includes('YATAN') ||
                    String(p.hareket_turu).toUpperCase().includes('GELEN') ||
                    String(p.hareket_turu).toUpperCase().includes('TAHSİLAT') ||
                    String(p.hareket_turu).toUpperCase().includes('GİRİŞ');
    p.yatan = Number(data.yatan ?? data.giren) || (isYatan ? tutar : 0);
    p.ceken = Number(data.ceken ?? data.cikan) || (!isYatan ? tutar : 0);
    if (data.cariId !== undefined || data.cari_id !== undefined) p.cari_id = Number(data.cariId ?? data.cari_id) || null;
    if (data.faturaId !== undefined || data.fatura_id !== undefined) p.fatura_id = Number(data.faturaId ?? data.fatura_id) || null;
    p.is_deleted = data.isDeleted === true || data.is_deleted === true;
    return p;
  }

  if (t === 'kasalar') {
    const p: any = {};
    if (data.id !== undefined) p.id = Number(data.id) || data.id;
    p.kasa_kodu = data.kasaKodu || data.kod || data.kasa_kodu || '';
    p.kasa_adi = data.kasaAdi || data.ad || data.kasa_adi || '';
    p.bakiye = Number(data.bakiye) || 0;
    p.para_birimi = data.paraBirimi || data.para_birimi || 'TRY';
    if (data.aciklama !== undefined) p.aciklama = data.aciklama;
    p.is_active = data.isActive !== false && data.is_active !== false;
    p.is_deleted = data.isDeleted === true || data.is_deleted === true;
    return p;
  }

  if (t === 'kasa_hareketler') {
    const p: any = {};
    if (data.id !== undefined) p.id = Number(data.id) || data.id;
    p.kasa_id = Number(data.kasaId ?? data.kasa_id) || 0;
    p.tarih = data.tarih || new Date().toISOString();
    p.hareket_turu = data.hareketTuru || data.islemTuru || data.hareket_turu || (Number(data.gelir ?? data.giren) > 0 ? 'GELİR' : 'GİDER');
    p.evrak_no = data.evrakNo || data.evrak_no || '';
    if (data.aciklama !== undefined) p.aciklama = data.aciklama;
    const tutar = Number(data.tutar) || 0;
    const isGelir = String(p.hareket_turu).toUpperCase().includes('GELİR') ||
                    String(p.hareket_turu).toUpperCase().includes('TAHSİLAT') ||
                    String(p.hareket_turu).toUpperCase().includes('GİRİŞ');
    p.gelir = Number(data.gelir ?? data.giren) || (isGelir ? tutar : 0);
    p.gider = Number(data.gider ?? data.cikan) || (!isGelir ? tutar : 0);
    if (data.cariId !== undefined || data.cari_id !== undefined) p.cari_id = Number(data.cariId ?? data.cari_id) || null;
    if (data.faturaId !== undefined || data.fatura_id !== undefined) p.fatura_id = Number(data.faturaId ?? data.fatura_id) || null;
    p.is_deleted = data.isDeleted === true || data.is_deleted === true;
    return p;
  }

  if (t === 'firma_profili') {
    const p: any = {};
    if (data.id !== undefined) p.id = Number(data.id) || data.id;
    else p.id = 1;

    if (data.unvan !== undefined || data.firmaAdi !== undefined || data.firma_adi !== undefined) {
      p.unvan = data.unvan ?? data.firmaAdi ?? data.firma_adi;
    }
    if (data.vergi_dairesi !== undefined || data.vergiDairesi !== undefined) {
      p.vergi_dairesi = data.vergi_dairesi ?? data.vergiDairesi;
    }
    if (data.vergi_no !== undefined || data.vergiNo !== undefined) {
      p.vergi_no = data.vergi_no ?? data.vergiNo;
    }
    if (data.adres !== undefined) p.adres = data.adres;
    if (data.telefon !== undefined) p.telefon = data.telefon;
    if (data.email !== undefined || data.eposta !== undefined) {
      p.email = data.email ?? data.eposta;
    }
    if (data.web_sitesi !== undefined || data.web !== undefined || data.webSitesi !== undefined) {
      p.web_sitesi = data.web_sitesi ?? data.web ?? data.webSitesi;
    }
    if (data.logo_base64 !== undefined || data.logoBase64 !== undefined || data.LogoBase64 !== undefined) {
      p.logo_base64 = data.logo_base64 ?? data.logoBase64 ?? data.LogoBase64;
    }
    return p;
  }

  if (t === 'siparisler') {
    const p: any = {};
    if (data.id !== undefined) p.id = Number(data.id) || data.id;
    p.siparis_no = data.siparisNo || data.siparis_no || '';
    p.siparis_turu = data.siparisTuru || data.tur || data.siparis_turu || 'Siparis';
    p.cari_id = Number(data.cariId ?? data.cari_id) || null;
    p.cari_unvan = data.cariUnvan || data.cari_unvan || '';
    p.tarih = data.tarih || new Date().toISOString();
    p.teslim_tarihi = data.teslimTarihi || data.teslimatTarihi || data.teslim_tarihi || null;
    p.ara_toplam = Number(data.araToplam ?? data.ara_toplam) || 0;
    p.kdv_toplam = Number(data.kdvToplam ?? data.kdv_toplam) || 0;
    p.genel_toplam = Number(data.genelToplam ?? data.genel_toplam) || 0;
    p.durum = data.durum || 'Bekliyor';
    if (data.aciklama !== undefined) p.aciklama = data.aciklama;
    p.is_deleted = data.isDeleted === true || data.is_deleted === true;
    return p;
  }

  if (t === 'siparis_detaylar') {
    const p: any = {};
    if (data.id !== undefined) p.id = Number(data.id) || data.id;
    p.siparis_id = Number(data.siparisId ?? data.siparis_id) || 0;
    p.stok_id = Number(data.stokId ?? data.stok_id) || null;
    p.stok_kodu = data.stokKodu || data.stok_kodu || '';
    p.stok_adi = data.stokAdi || data.stok_adi || '';
    p.miktar = Number(data.miktar) || 0;
    p.birim = data.birim || 'Adet';
    p.birim_fiyat = Number(data.birimFiyat ?? data.fiyat ?? data.birim_fiyat) || 0;
    p.kdv_orani = Number(data.kdvOrani ?? data.kdv ?? data.kdv_orani) || 0;
    p.kdv_tutari = Number(data.kdvTutari ?? data.kdv_tutari) || 0;
    p.toplam_tutar = Number(data.toplamTutar ?? data.tutar ?? data.toplam_tutar) || 0;
    return p;
  }

  if (t === 'teklifler') {
    const p: any = {};
    if (data.id !== undefined) p.id = Number(data.id) || data.id;
    p.teklif_no = data.teklifNo || data.teklif_no || '';
    p.teklif_turu = data.teklifTuru || data.tur || data.teklif_turu || 'Teklif';
    p.cari_id = Number(data.cariId ?? data.cari_id) || null;
    p.cari_unvan = data.cariUnvan || data.cari_unvan || '';
    p.tarih = data.tarih || new Date().toISOString();
    p.gecerlilik_tarihi = data.gecerlilikTarihi || data.gecerlilik_tarihi || null;
    p.ara_toplam = Number(data.araToplam ?? data.ara_toplam) || 0;
    p.kdv_toplam = Number(data.kdvToplam ?? data.kdv_toplam) || 0;
    p.genel_toplam = Number(data.genelToplam ?? data.genel_toplam) || 0;
    p.durum = data.durum || 'Bekliyor';
    if (data.aciklama !== undefined) p.aciklama = data.aciklama;
    p.is_deleted = data.isDeleted === true || data.is_deleted === true;
    return p;
  }

  if (t === 'teklif_detaylar') {
    const p: any = {};
    if (data.id !== undefined) p.id = Number(data.id) || data.id;
    p.teklif_id = Number(data.teklifId ?? data.teklif_id) || 0;
    p.stok_id = Number(data.stokId ?? data.stok_id) || null;
    p.stok_kodu = data.stokKodu || data.stok_kodu || '';
    p.stok_adi = data.stokAdi || data.stok_adi || '';
    p.miktar = Number(data.miktar) || 0;
    p.birim = data.birim || 'Adet';
    p.birim_fiyat = Number(data.birimFiyat ?? data.fiyat ?? data.birim_fiyat) || 0;
    p.kdv_orani = Number(data.kdvOrani ?? data.kdv ?? data.kdv_orani) || 0;
    p.kdv_tutari = Number(data.kdvTutari ?? data.kdv_tutari) || 0;
    p.toplam_tutar = Number(data.toplamTutar ?? data.tutar ?? data.toplam_tutar) || 0;
    return p;
  }

  if (t === 'notlar') {
    const p: any = {};
    if (data.id !== undefined) p.id = Number(data.id) || data.id;
    p.baslik = data.baslik || data.title || '';
    p.icerik = data.icerik || data.content || '';
    p.renk = data.renk || data.color || '#0061FF';
    p.tarih = data.tarih || new Date().toISOString();
    p.is_deleted = data.isDeleted === true || data.is_deleted === true;
    return p;
  }

  return toSnakeCase(data);
};

export const normalizeRowFromSupabase = (table: string, row: any): any => {
  if (!row || typeof row !== 'object') return row;
  const t = table.toLowerCase();
  const r = toCamelCase(row);

  if (t === 'firma_profili') {
    return {
      ...r,
      id: 1,
      firmaAdi: row.unvan || r.unvan || '',
      unvan: row.unvan || r.unvan || '',
      vergiDairesi: row.vergi_dairesi || r.vergiDairesi || '',
      vergiNo: row.vergi_no || r.vergiNo || '',
      adres: row.adres || r.adres || '',
      telefon: row.telefon || r.telefon || '',
      eposta: row.email || r.email || '',
      email: row.email || r.email || '',
      webSitesi: row.web_sitesi || r.webSitesi || '',
      logoBase64: row.logo_base64 || r.logoBase64 || null
    };
  }

  if (t === 'cariler') {
    const kod = row.cari_kodu || row.kod || r.cariKod || `CARI-${row.id}`;
    const unvan = row.unvan || r.unvan || '';
    const sehir = row.sehir || row.il || r.sehir || r.il || '';
    const yetkili = row.yetkili_kisi || row.yetkili || r.yetkiliKisi || r.yetkili || '';
    const eposta = row.email || row.eposta || r.email || r.eposta || '';
    const web = row.web_sitesi || row.web_adresi || r.webSitesi || r.webAdresi || '';
    const tc = row.tc_kimlik_no || row.tc_no || r.tcKimlikNo || r.tcNo || '';
    const borc = Number(row.borc_tutari ?? row.borc ?? r.borcTutari ?? r.borc) || 0;
    const alacak = Number(row.alacak_tutari ?? row.alacak ?? r.alacakTutari ?? r.alacak) || 0;
    const bakiye = Number(row.bakiye ?? r.bakiye ?? (borc - alacak)) || 0;

    return {
      ...r,
      cariKod: kod,
      cariKodu: kod,
      unvan,
      il: sehir,
      sehir,
      yetkili,
      yetkiliKisi: yetkili,
      eposta,
      email: eposta,
      webAdresi: web,
      webSitesi: web,
      tcNo: tc,
      tcKimlikNo: tc,
      borc,
      borcTutari: borc,
      alacak,
      alacakTutari: alacak,
      bakiye
    };
  }

  if (t === 'stoklar') {
    const stokKodu = row.stok_kodu || r.stokKodu || '';
    const stokAdi = row.stok_adi || r.stokAdi || '';
    const grup = row.grup_adi || row.grup || r.grupAdi || r.grup || r.kategori || '';
    const miktar = Number(row.mevcut_miktar ?? row.miktar ?? r.mevcutMiktar ?? r.miktar) || 0;
    const minSeviye = Number(row.kritik_seviye ?? row.min_seviye ?? r.kritikSeviye ?? r.minSeviye) || 0;
    const kdv = Number(row.kdv_orani ?? row.kdv ?? r.kdvOrani ?? r.kdv) || 20;

    return {
      ...r,
      stokKodu,
      stokAdi,
      grup,
      grupAdi: grup,
      kategori: grup,
      miktar,
      mevcutMiktar: miktar,
      minSeviye,
      kritikSeviye: minSeviye,
      kdv,
      kdvOrani: kdv
    };
  }

  if (t === 'faturalar') {
    const faturaNo = row.fatura_no || r.faturaNo || '';
    const tur = row.fatura_turu || row.tur || r.faturaTuru || r.tur || 'Satış';
    const araToplam = Number(row.ara_toplam ?? r.araToplam) || 0;
    const kdvToplam = Number(row.kdv_toplam ?? r.kdvToplam ?? r.toplamKdv) || 0;
    const genelToplam = Number(row.genel_toplam ?? r.genelToplam) || 0;

    return {
      ...r,
      faturaNo,
      tur,
      faturaTuru: tur,
      araToplam,
      kdvToplam,
      toplamKdv: kdvToplam,
      genelToplam
    };
  }

  if (t === 'fatura_detaylar') {
    const tutar = Number(row.toplam_tutar ?? r.toplamTutar ?? r.tutar) || 0;
    return {
      ...r,
      faturaId: Number(row.fatura_id ?? r.faturaId),
      stokId: Number(row.stok_id ?? r.stokId),
      stokKodu: row.stok_kodu || r.stokKodu || '',
      stokAdi: row.stok_adi || r.stokAdi || '',
      birimFiyat: Number(row.birim_fiyat ?? r.birimFiyat) || 0,
      kdvOrani: Number(row.kdv_orani ?? r.kdvOrani) || 0,
      kdvTutari: Number(row.kdv_tutari ?? r.kdvTutari) || 0,
      toplamTutar: tutar,
      tutar
    };
  }

  if (t === 'cari_hareketler') {
    const islemTuru = row.islem_turu || r.islemTuru || '';
    return {
      ...r,
      islemTuru,
      evrakNo: row.evrak_no || r.evrakNo || '',
      borc: Number(row.borc ?? r.borc) || 0,
      alacak: Number(row.alacak ?? r.alacak) || 0,
      bakiye: Number(row.bakiye ?? r.bakiye) || 0,
      vade: row.vade_tarihi || r.vadeTarihi || r.vade,
      vadeTarihi: row.vade_tarihi || r.vadeTarihi || r.vade
    };
  }

  if (t === 'stok_hareketler') {
    const islemTuru = row.hareket_turu || r.hareketTuru || r.islemTuru || '';
    const miktar = Number(row.miktar ?? r.miktar) || 0;
    const isGiris = islemTuru.includes('GİRİŞ') || islemTuru.includes('Giris');
    return {
      ...r,
      islemTuru,
      hareketTuru: islemTuru,
      miktar,
      giren: isGiris ? miktar : 0,
      cikan: !isGiris ? miktar : 0,
      fiyat: Number(row.birim_fiyat ?? r.birimFiyat ?? r.fiyat) || 0,
      birimFiyat: Number(row.birim_fiyat ?? r.birimFiyat ?? r.fiyat) || 0
    };
  }

  if (t === 'bankalar') {
    const sube = row.sube_adi || row.sube || r.subeAdi || r.sube || '';
    const isKasa = (typeof row.aciklama === 'string' && row.aciklama.startsWith('[KASA]')) ||
                   (typeof r.aciklama === 'string' && r.aciklama.startsWith('[KASA]')) ||
                   r.kartTuru === 'Kasa' || r.turu === 'Kasa';
    let cleanAciklama = row.aciklama ?? r.aciklama;
    let yetkili = r.yetkili;
    if (typeof cleanAciklama === 'string' && cleanAciklama.startsWith('[KASA]')) {
      const parts = cleanAciklama.split('|');
      cleanAciklama = parts.slice(1).join('|');
      if (!yetkili && cleanAciklama) yetkili = cleanAciklama;
    }
    return {
      ...r,
      sube,
      subeAdi: sube,
      iban: row.iban || r.iban || r.iBAN || '',
      kartTuru: isKasa ? 'Kasa' : (r.kartTuru || 'Vadesiz'),
      yetkili: yetkili || r.yetkili,
      aciklama: cleanAciklama
    };
  }

  if (t === 'kasalar') {
    return {
      ...r,
      kasaKodu: row.kasa_kodu || r.kasaKodu || '',
      kasaAdi: row.kasa_adi || r.kasaAdi || '',
      hesapAdi: row.kasa_adi || r.kasaAdi || '',
      bankaAdi: row.kasa_adi || r.kasaAdi || '',
      ad: row.kasa_adi || r.kasaAdi || '',
      sube: '',
      subeAdi: '',
      bakiye: Number(row.bakiye ?? r.bakiye) || 0,
      kartTuru: 'Kasa'
    };
  }

  if (t === 'banka_hareketler') {
    const yatan = Number(row.yatan ?? row.giren ?? r.yatan ?? r.giren) || 0;
    const ceken = Number(row.ceken ?? row.cikan ?? r.ceken ?? r.cikan) || 0;
    const islemTuru = row.hareket_turu || r.islemTuru || r.hareketTuru || (yatan > 0 ? 'YATAN' : 'ÇEKİLEN');
    return {
      ...r,
      bankaId: row.banka_id || r.bankaId,
      evrakNo: row.evrak_no || r.evrakNo || '',
      islemTuru,
      hareketTuru: islemTuru,
      yatan,
      ceken,
      giren: yatan,
      cikan: ceken,
      tutar: yatan > 0 ? yatan : ceken
    };
  }

  if (t === 'kasa_hareketler') {
    const giren = Number(row.gelir ?? row.giren ?? r.gelir ?? r.giren) || 0;
    const cikan = Number(row.gider ?? row.ceken ?? r.gider ?? r.ceken) || 0;
    return {
      ...r,
      kasaId: row.kasa_id || r.kasaId,
      evrakNo: row.evrak_no || r.evrakNo || '',
      islemTuru: row.hareket_turu || r.islemTuru || r.hareketTuru || '',
      hareketTuru: row.hareket_turu || r.islemTuru || r.hareketTuru || '',
      gelir: giren,
      gider: cikan,
      giren,
      cikan,
      tutar: giren > 0 ? giren : cikan
    };
  }

  if (t === 'siparisler') {
    return {
      ...r,
      siparisNo: row.siparis_no || r.siparisNo || '',
      cariId: row.cari_id || r.cariId,
      cariUnvan: row.cari_unvan || r.cariUnvan || '',
      teslimTarihi: row.teslim_tarihi || r.teslimTarihi || r.teslimatTarihi,
      teslimatTarihi: row.teslim_tarihi || r.teslimTarihi || r.teslimatTarihi,
      genelToplam: Number(row.genel_toplam ?? r.genelToplam) || 0,
      durum: row.durum || r.durum || 'Bekliyor'
    };
  }

  if (t === 'siparis_detaylar') {
    return {
      ...r,
      siparisId: row.siparis_id || r.siparisId,
      stokId: row.stok_id || r.stokId,
      stokKodu: row.stok_kodu || r.stokKodu || '',
      stokAdi: row.stok_adi || r.stokAdi || '',
      miktar: Number(row.miktar ?? r.miktar) || 0,
      birimFiyat: Number(row.birim_fiyat ?? r.birimFiyat ?? r.fiyat) || 0,
      fiyat: Number(row.birim_fiyat ?? r.birimFiyat ?? r.fiyat) || 0,
      toplamTutar: Number(row.toplam_tutar ?? r.toplamTutar ?? r.tutar) || 0,
      tutar: Number(row.toplam_tutar ?? r.toplamTutar ?? r.tutar) || 0
    };
  }

  if (t === 'teklifler') {
    return {
      ...r,
      teklifNo: row.teklif_no || r.teklifNo || '',
      cariId: row.cari_id || r.cariId,
      cariUnvan: row.cari_unvan || r.cariUnvan || '',
      gecerlilikTarihi: row.gecerlilik_tarihi || r.gecerlilikTarihi,
      genelToplam: Number(row.genel_toplam ?? r.genelToplam) || 0,
      durum: row.durum || r.durum || 'Bekliyor'
    };
  }

  if (t === 'teklif_detaylar') {
    return {
      ...r,
      teklifId: row.teklif_id || r.teklifId,
      stokId: row.stok_id || r.stokId,
      stokKodu: row.stok_kodu || r.stokKodu || '',
      stokAdi: row.stok_adi || r.stokAdi || '',
      miktar: Number(row.miktar ?? r.miktar) || 0,
      birimFiyat: Number(row.birim_fiyat ?? r.birimFiyat ?? r.fiyat) || 0,
      fiyat: Number(row.birim_fiyat ?? r.birimFiyat ?? r.fiyat) || 0,
      toplamTutar: Number(row.toplam_tutar ?? r.toplamTutar ?? r.tutar) || 0,
      tutar: Number(row.toplam_tutar ?? r.toplamTutar ?? r.tutar) || 0
    };
  }

  if (t === 'notlar') {
    return {
      ...r,
      title: row.baslik || row.title || r.baslik || r.title || '',
      baslik: row.baslik || row.title || r.baslik || r.title || '',
      content: row.icerik || row.content || r.icerik || r.content || '',
      icerik: row.icerik || row.content || r.icerik || r.content || '',
      color: row.renk || row.color || r.renk || r.color || '#0061FF',
      renk: row.renk || row.color || r.renk || r.color || '#0061FF'
    };
  }

  return r;
};

// --- CRUD Operations ---
export const readData = async (path: string, timeoutMs = 7000): Promise<any> => {
  try {
    if (cachedConfig.url && cachedConfig.url.includes('firebaseio.com')) {
      const resp = await fetch(`${cachedConfig.url}/${path}.json`);
      if (!resp.ok) return null;
      return await resp.json();
    }

    const { table, id, isDetail, foreignKey } = parsePath(path);

    if (isDetail && foreignKey && id) {
      const { data, error } = await supabase
        .from(table)
        .select('*')
        .eq(foreignKey, id);

      if (error) {
        console.warn(`[Supabase] readData error on ${table}:`, error.message);
        return [];
      }
      return (data || []).map((row: any) => normalizeRowFromSupabase(table, row));
    }

    if (id) {
      const { data, error } = await supabase
        .from(table)
        .select('*')
        .eq('id', id)
        .maybeSingle();

      if (error) {
        console.warn(`[Supabase] readData single error on ${table}/${id}:`, error.message);
        return null;
      }
      if (data && (data.is_deleted === true || data.is_deleted === 1)) {
        return null;
      }
      return data ? normalizeRowFromSupabase(table, data) : null;
    }

    // List all
    let query = supabase.from(table).select('*');
    if (['cariler', 'stoklar', 'faturalar', 'kasalar', 'bankalar', 'siparisler', 'teklifler', 'notlar', 'cari_hareketler', 'stok_hareketler', 'kasa_hareketler', 'banka_hareketler', 'mali_yillar', 'cekler', 'senetler', 'kredi_karti_islemler', 'eft_islemler', 'belge_arsiv', 'doviz_kurlari', 'portfoy_kartlar', 'musteri_takip_klasorler', 'musteri_takip_detaylar'].includes(table)) {
      query = query.or('is_deleted.is.null,is_deleted.eq.false');
    }
    const { data, error } = await query;

    if (error) {
      console.warn(`[Supabase] readData list error on ${table}:`, error.message);
      return {};
    }

    const normList = (data || [])
      .filter((row: any) => !row || (row.is_deleted !== true && row.is_deleted !== 1))
      .map((row: any) => normalizeRowFromSupabase(table, row));

    // For detail tables when queried without specific ID, group by parent foreign key
    // so that screens can access them via detailsMap[parentId]
    if (table === 'fatura_detaylar' || table === 'siparis_detaylar' || table === 'teklif_detaylar' || table === 'stok_sayim_detaylari' || table === 'musteri_takip_detaylar') {
      const parentFk = table === 'fatura_detaylar' ? 'faturaId' : (table === 'siparis_detaylar' ? 'siparisId' : (table === 'teklif_detaylar' ? 'teklifId' : (table === 'stok_sayim_detaylari' ? 'fisiId' : 'klasorId')));
      const snakeFk = table === 'fatura_detaylar' ? 'fatura_id' : (table === 'siparis_detaylar' ? 'siparis_id' : (table === 'teklif_detaylar' ? 'teklif_id' : (table === 'stok_sayim_detaylari' ? 'fisi_id' : 'klasor_id')));
      const groupedMap: Record<string, any[]> = {};
      for (const item of normList) {
        if (!item) continue;
        const parentId = String(item[parentFk] || item[snakeFk] || '');
        if (parentId) {
          if (!groupedMap[parentId]) groupedMap[parentId] = [];
          groupedMap[parentId].push(item);
        }
      }
      return groupedMap;
    }

    const recordMap: Record<string, any> = {};
    for (const item of normList) {
      if (item) {
        const key = item.id !== undefined ? item.id.toString() : (item.yil !== undefined ? item.yil.toString() : '');
        if (key) {
          recordMap[key] = item;
        }
      }
    }
    return recordMap;
  } catch (err: any) {
    console.error(`[Supabase] readData exception on ${path}:`, err.message);
    return null;
  }
};

export const writeData = async (path: string, data: any): Promise<boolean> => {
  try {
    if (cachedConfig.url && cachedConfig.url.includes('firebaseio.com')) {
      const resp = await fetch(`${cachedConfig.url}/${path}.json`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data)
      });
      return resp.ok;
    }

    const { table, id, isDetail, foreignKey } = parsePath(path);

    if (isDetail && foreignKey && id) {
      if (Array.isArray(data)) {
        await supabase.from(table).delete().eq(foreignKey, id);
        if (data.length > 0) {
          const items = data.map((item: any) => ({
            ...sanitizePayloadForTable(table, item),
            [foreignKey]: id
          }));
          const { error } = await supabase.from(table).insert(items);
          if (error) throw error;
        }
        return true;
      }
    }

    if (data === null || data === undefined) {
      return await deleteData(path);
    }

    const { field } = parsePath(path);
    let payload: any;
    if (typeof data !== 'object' || Array.isArray(data)) {
      if (field) {
        const snakeField = field.replace(/[A-Z]/g, letter => `_${letter.toLowerCase()}`);
        payload = { id: Number(id) || id || 1, [snakeField]: data };
      } else {
        payload = data;
      }
    } else {
      payload = { ...data };
      if (id && payload.id === undefined) {
        payload.id = Number(id) || id;
      }
    }

    const sanitizedData = sanitizePayloadForTable(table, payload);
    const { error } = await supabase
      .from(table)
      .upsert(sanitizedData, { onConflict: 'id' });

    if (error) {
      console.error(`[Supabase] writeData error on ${table}:`, error.message);
      return false;
    }

    // Dual-write support for Kasalar & Bankalar parity between Mobile and Desktop
    if (table === 'bankalar' && (isKasaRecord(payload) || isKasaRecord(data) || data.kartTuru === 'Kasa')) {
      try {
        const kasaPayload = {
          id: payload.id,
          kasa_kodu: payload.hesap_no || data.hesapNo || data.kasaKodu || `KAS-${payload.id}`,
          kasa_adi: payload.banka_adi || data.hesapAdi || data.bankaAdi || data.ad,
          bakiye: Number(payload.bakiye ?? data.bakiye) || 0,
          para_birimi: payload.para_birimi || data.dovizTuru || 'TRY',
          aciklama: data.yetkili || data.aciklama || '',
          is_active: payload.is_active !== false,
          is_deleted: payload.is_deleted === true
        };
        await supabase.from('kasalar').upsert(kasaPayload, { onConflict: 'id' });
      } catch (kasaErr) {
        console.warn('[Supabase] Dual write to kasalar failed:', kasaErr);
      }
    } else if (table === 'kasalar') {
      try {
        const bankaPayload = {
          id: payload.id,
          banka_adi: payload.kasa_adi || data.kasaAdi || data.ad || '',
          sube_adi: '',
          hesap_no: payload.kasa_kodu || data.kasaKodu || '',
          iban: '',
          bakiye: Number(payload.bakiye ?? data.bakiye) || 0,
          para_birimi: payload.para_birimi || data.paraBirimi || 'TRY',
          aciklama: '[KASA]' + (data.aciklama ? `|${data.aciklama}` : ''),
          is_active: payload.is_active !== false,
          is_deleted: payload.is_deleted === true
        };
        await supabase.from('bankalar').upsert(bankaPayload, { onConflict: 'id' });
      } catch (bErr) {
        console.warn('[Supabase] Dual write to bankalar failed:', bErr);
      }
    }

    return true;
  } catch (err: any) {
    console.error(`[Supabase] writeData exception on ${path}:`, err.message);
    return false;
  }
};

export const deleteData = async (path: string): Promise<boolean> => {
  try {
    if (cachedConfig.url && cachedConfig.url.includes('firebaseio.com')) {
      const resp = await fetch(`${cachedConfig.url}/${path}.json`, {
        method: 'DELETE'
      });
      return resp.ok;
    }

    const { table, id, isDetail, foreignKey } = parsePath(path);
    if (isDetail && foreignKey && id) {
      const { error } = await supabase.from(table).delete().eq(foreignKey, id);
      return !error;
    }
    if (id) {
      const cleanId = (!isNaN(Number(id)) && String(Number(id)) === String(id).trim()) ? Number(id) : id;
      if (['cariler', 'stoklar', 'faturalar', 'kasalar', 'bankalar', 'siparisler', 'teklifler', 'notlar', 'cari_hareketler', 'stok_hareketler', 'kasa_hareketler', 'banka_hareketler'].includes(table)) {
        const updatePayload: any = { is_deleted: true };
        if (['cariler', 'stoklar', 'faturalar', 'firma_profili'].includes(table)) {
          updatePayload.guncelleme_tarihi = new Date().toISOString();
        }
        const { error: softErr } = await supabase
          .from(table)
          .update(updatePayload)
          .eq('id', cleanId);
        if (table === 'bankalar') {
          try { await supabase.from('kasalar').update({ is_deleted: true }).eq('id', cleanId); } catch {}
        } else if (table === 'kasalar') {
          try { await supabase.from('bankalar').update({ is_deleted: true }).eq('id', cleanId); } catch {}
        }
        if (!softErr) return true;
        console.warn(`[Supabase] soft delete error on ${table}/${id}:`, softErr.message);
      }
      const { error } = await supabase.from(table).delete().eq('id', cleanId);
      if (table === 'bankalar') {
        try { await supabase.from('kasalar').delete().eq('id', cleanId); } catch {}
      } else if (table === 'kasalar') {
        try { await supabase.from('bankalar').delete().eq('id', cleanId); } catch {}
      }
      return !error;
    }
    return false;
  } catch (err: any) {
    console.error(`[Supabase] deleteData error on ${path}:`, err.message);
    return false;
  }
};

export const updateDataBatch = async (updates: Record<string, any>): Promise<boolean> => {
  try {
    let success = true;
    for (const [path, val] of Object.entries(updates)) {
      const ok = await writeData(path, val);
      if (!ok) success = false;
    }
    return success;
  } catch (e: any) {
    console.error('[Supabase] updateDataBatch error:', e.message);
    return false;
  }
};

export const subscribeToPath = (path: string, callback: (data: any) => void): (() => void) => {
  try {
    const { table } = parsePath(path);
    if (!table) return () => {};

    let isActive = true;

    // Initial read
    readData(path)
      .then((data) => {
        if (isActive && data !== null) {
          callback(data);
        }
      })
      .catch((err) => {
        console.warn(`[Supabase] Initial read error on ${path}:`, err);
      });

    let debounceTimer: any = null;
    const debouncedRead = () => {
      if (!isActive) return;
      if (debounceTimer) clearTimeout(debounceTimer);
      debounceTimer = setTimeout(() => {
        if (!isActive) return;
        readData(path)
          .then((data) => {
            if (isActive && data !== null) {
              callback(data);
            }
          })
          .catch((err) => {
            console.warn(`[Supabase] Realtime read error on ${path}:`, err);
          });
      }, 250);
    };

    // Generate unique channel name per subscription to avoid "cannot add postgres_changes callbacks after subscribe()"
    const uniqueChannelName = `sub_${table}_${Date.now()}_${Math.random().toString(36).substring(2, 9)}`;
    const channel = supabase
      .channel(uniqueChannelName)
      .on(
        'postgres_changes',
        { event: '*', schema: 'public', table },
        () => {
          debouncedRead();
        }
      )
      .subscribe();

    let isAppActive = typeof AppState !== 'undefined' && AppState.currentState ? AppState.currentState === 'active' : true;
    const appStateSub = typeof AppState !== 'undefined' && AppState.addEventListener ? AppState.addEventListener('change', (nextState) => {
      isAppActive = nextState === 'active';
      if (isAppActive && isActive) {
        debouncedRead();
      }
    }) : null;

    // Fallback polling timer (every 15 seconds) to guarantee sync parity even if websocket misses an event
    const pollInterval = setInterval(() => {
      if (!isActive || !isAppActive) return;
      readData(path)
        .then((data) => {
          if (isActive && data !== null) {
            callback(data);
          }
        })
        .catch(() => {});
    }, 15000);

    return () => {
      isActive = false;
      if (debounceTimer) clearTimeout(debounceTimer);
      clearInterval(pollInterval);
      if (appStateSub && typeof appStateSub.remove === 'function') {
        appStateSub.remove();
      }
      try {
        supabase.removeChannel(channel);
      } catch (e) {
        console.warn(`[Supabase] removeChannel error on ${uniqueChannelName}:`, e);
      }
    };
  } catch (err) {
    console.error(`[Supabase] subscribeToPath exception on ${path}:`, err);
    return () => {};
  }
};

export const updateFutureBalances = async (
  entityType: string,
  entityId: string | number,
  borcDelta: number,
  alacakDelta: number
) => {
  try {
    const { table } = parsePath(entityType);
    const { data: current } = await supabase
      .from(table)
      .select('borc_tutari, alacak_tutari, bakiye')
      .eq('id', entityId)
      .maybeSingle();

    if (current) {
      const newBorc = (Number(current.borc_tutari) || 0) + borcDelta;
      const newAlacak = (Number(current.alacak_tutari) || 0) + alacakDelta;
      const newBakiye = newBorc - newAlacak;

      await supabase
        .from(table)
        .update({
          borc_tutari: newBorc,
          alacak_tutari: newAlacak,
          bakiye: newBakiye
        })
        .eq('id', entityId);
    }
  } catch (err: any) {
    console.error('[Supabase] updateFutureBalances error:', err.message);
  }
};

export const updateCariBaseInfoInAllYears = async (cariId: number | string, baseInfo: any) => {
  try {
    const snake = toSnakeCase(baseInfo);
    await supabase.from('cariler').update(snake).eq('id', cariId);
  } catch (err: any) {
    console.error('[Supabase] updateCariBaseInfoInAllYears error:', err.message);
  }
};

export const pushData = async (path: string, data: any): Promise<string | null> => {
  const newId = generateMobileRecordId();
  const ok = await writeData(`${path}/${newId}`, { ...data, id: newId });
  return ok ? newId.toString() : null;
};

// --- Account / Kasa Helper Functions ---
export const KASA_KART_TURU = 'Kasa';

export const isKasaRecord = (record: any): boolean => {
  if (!record) return false;
  if (record.kartTuru === 'Kasa' || record.turu === 'Kasa') return true;
  if (typeof record.aciklama === 'string' && record.aciklama.startsWith('[KASA]')) return true;
  return false;
};

export const splitAccounts = (bankalar: any[]): { kasalar: any[]; bankalar: any[] } => {
  const kList: any[] = [];
  const bList: any[] = [];
  (bankalar || []).forEach(b => {
    if (isKasaRecord(b)) kList.push(b);
    else bList.push(b);
  });
  return { kasalar: kList, bankalar: bList };
};

export const mergeKasalar = (bankKasa: any[], legacyKasa: any[]): any[] => {
  const map = new Map<string, any>();
  (bankKasa || []).forEach(k => { if (k?.id) map.set(k.id.toString(), k); });
  (legacyKasa || []).forEach(k => { if (k?.id && !map.has(k.id.toString())) map.set(k.id.toString(), k); });
  return Array.from(map.values());
};

export const getKasaWritePath = (id: number | string): string => `Kasalar/${id}`;

export const toKasaRecord = (kasa: any): any => ({
  id: kasa.id,
  kasaKodu: kasa.kasaKodu || kasa.kod || '',
  kasaAdi: kasa.kasaAdi || kasa.ad || '',
  bakiye: Number(kasa.bakiye) || 0,
  paraBirimi: kasa.paraBirimi || 'TRY',
  aciklama: kasa.aciklama || '',
  isActive: kasa.isActive !== false,
  isDeleted: kasa.isDeleted === true
});

// --- Auth Compatibility & Hybrid Session Management ---
import * as Crypto from 'expo-crypto';

export const hashPasswordAsync = async (password: string, salt?: string): Promise<string> => {
  try {
    const textToHash = salt ? password + salt : password;
    return await Crypto.digestStringAsync(
      Crypto.CryptoDigestAlgorithm.SHA256,
      textToHash,
      { encoding: Crypto.CryptoEncoding.BASE64 }
    );
  } catch (e) {
    return password;
  }
};

export const registerInitialUser = async (username: string, password: string, email?: string): Promise<boolean> => {
  try {
    const cleanUser = username.trim();
    const cleanPass = password.trim();
    const userEmail = email && email.includes('@') ? email : `${cleanUser.toLowerCase()}@ermay.local`;

    try {
      await supabase.auth.signUp({
        email: userEmail,
        password: cleanPass,
        options: {
          data: {
            username: cleanUser.toLowerCase(),
            role: 'Admin',
            fullName: cleanUser
          }
        }
      });
    } catch (authErr) {
      console.warn('[registerInitialUser] Supabase auth signup warning:', authErr);
    }

    // public.kullanicilar tablosuna da kaydet (güvenli hash ile)
    try {
      const generatedSalt = Math.random().toString(36).substring(2, 15) + Math.random().toString(36).substring(2, 15);
      const hashedPassword = await hashPasswordAsync(cleanPass, generatedSalt);
      await supabase.from('kullanicilar').upsert({
        kullanici_adi: cleanUser.toLowerCase(),
        username: cleanUser.toLowerCase(),
        sifre: hashedPassword,
        password_hash: hashedPassword,
        password_salt: generatedSalt,
        email: userEmail,
        rol: 'Admin',
        role: 'Admin',
        ad_soyad: cleanUser,
        aktif_mi: true,
        is_active: true
      }, { onConflict: 'kullanici_adi' });
    } catch (dbErr) {
      console.warn('[registerInitialUser] kullanicilar upsert warning:', dbErr);
    }

    // Yerel belleğe ve oturum durumuna güvenli şekilde kaydet
    await AsyncStorage.setItem('ermay_saved_username', cleanUser);
    await AsyncStorage.setItem('ermay_saved_password', cleanPass);
    
    const sessionUser = {
      id: cleanUser,
      email: userEmail,
      username: cleanUser,
      user_metadata: { role: 'Admin', fullName: cleanUser }
    };
    cachedSessionUser = sessionUser;
    await AsyncStorage.setItem('ermay_active_session_user', JSON.stringify(sessionUser));
    return true;
  } catch (e) {
    console.error('[registerInitialUser error]:', e);
    return false;
  }
};

export const loginUser = async (usernameOrEmail: string, password: string): Promise<{ success: boolean; error?: string; user?: any }> => {
  try {
    const cleanUser = usernameOrEmail.trim();
    const cleanPass = password.trim();
    const targetEmail = cleanUser.includes('@') ? cleanUser : `${cleanUser.toLowerCase()}@ermay.local`;

    console.log('[loginUser] Giriş denemesi başlatılıyor:', cleanUser);

    // 0. public.kullanicilar Tablosunu Kontrol Et
    try {
      const { data: dbUsers, error: userFetchErr } = await supabase
        .from('kullanicilar')
        .select('*');

      if (!userFetchErr && dbUsers && dbUsers.length > 0) {
        const matchUser = dbUsers.find((u: any) =>
          (u.kullanici_adi && u.kullanici_adi.toLowerCase() === cleanUser.toLowerCase()) ||
          (u.email && u.email.toLowerCase() === cleanUser.toLowerCase())
        );

        if (matchUser) {
          let passwordMatched = false;
          if (matchUser.sifre === cleanPass) {
            passwordMatched = true;
          } else {
            const computedHash = await hashPasswordAsync(cleanPass, matchUser.password_salt || matchUser.sifre_salt);
            if (computedHash === matchUser.sifre) {
              passwordMatched = true;
            }
          }

          if (passwordMatched) {
            console.log('[loginUser] kullanicilar tablosu eşleşmesi başarılı:', matchUser.kullanici_adi);
            const sessionUser = {
              id: matchUser.kullanici_adi,
              email: matchUser.email || targetEmail,
              username: matchUser.kullanici_adi,
              user_metadata: { role: matchUser.rol || 'Admin', fullName: matchUser.ad_soyad || matchUser.kullanici_adi }
            };
            cachedSessionUser = sessionUser;
            await AsyncStorage.setItem('ermay_active_session_user', JSON.stringify(sessionUser));
            await AsyncStorage.setItem('ermay_saved_username', matchUser.kullanici_adi);
            await AsyncStorage.setItem('ermay_saved_password', cleanPass);
            return { success: true, user: sessionUser };
          }
        }
      }
    } catch (dbUserErr) {
      console.warn('[loginUser] kullanicilar tablosu kontrol uyarısı:', dbUserErr);
    }

    // 1. Buluttaki Sistem Kullanıcı Kaydını Kontrol Et (notlar tablosu id: 999999)
    try {
      const { data: cloudNote } = await supabase
        .from('notlar')
        .select('*')
        .eq('id', 999999)
        .maybeSingle();

      if (cloudNote && cloudNote.icerik) {
        let usersList: any[] = [];
        try {
          usersList = JSON.parse(cloudNote.icerik);
        } catch {}

        if (Array.isArray(usersList) && usersList.length > 0) {
          const matchUser = usersList.find((u: any) =>
            (u.username && u.username.toLowerCase() === cleanUser.toLowerCase()) ||
            (u.email && u.email.toLowerCase() === cleanUser.toLowerCase())
          );

          if (matchUser) {
            let passwordMatched = false;
            // Düz şifre eşleşmesi
            if (matchUser.password === cleanPass) {
              passwordMatched = true;
            } else {
              // SHA-256 Hash doğrulaması
              const computedHash = await hashPasswordAsync(cleanPass, matchUser.password_salt || matchUser.passwordSalt);
              if (computedHash === matchUser.password) {
                passwordMatched = true;
              }
            }

            if (passwordMatched) {
              console.log('[loginUser] Bulut kullanıcı eşleşmesi başarılı:', matchUser.username);
              const sessionUser = {
                id: matchUser.username,
                email: matchUser.email || targetEmail,
                username: matchUser.username,
                user_metadata: { role: matchUser.role || 'Admin', fullName: matchUser.username }
              };
              cachedSessionUser = sessionUser;
              await AsyncStorage.setItem('ermay_active_session_user', JSON.stringify(sessionUser));
              await AsyncStorage.setItem('ermay_saved_username', matchUser.username);
              await AsyncStorage.setItem('ermay_saved_password', cleanPass);
              return { success: true, user: sessionUser };
            }
          }
        }
      }
    } catch (cloudErr) {
      console.warn('[loginUser] Bulut kontrol uyarısı:', cloudErr);
    }

    // 2. Yerelde kurulumda / önceki girişte saklanan kullanıcıyı kontrol et
    const localUsername = await AsyncStorage.getItem('ermay_saved_username');
    const localPassword = await AsyncStorage.getItem('ermay_saved_password');
    if (localUsername && localPassword) {
      const isUserMatch = cleanUser.toLowerCase() === localUsername.toLowerCase() || targetEmail.toLowerCase() === `${localUsername.toLowerCase()}@ermay.local`;
      if (isUserMatch && cleanPass === localPassword) {
        console.log('[loginUser] Yerel kullanıcı doğrulandı:', localUsername);
        const sessionUser = {
          id: localUsername,
          email: targetEmail,
          username: localUsername,
          user_metadata: { role: 'Admin', fullName: localUsername }
        };
        cachedSessionUser = sessionUser;
        await AsyncStorage.setItem('ermay_active_session_user', JSON.stringify(sessionUser));
        return { success: true, user: sessionUser };
      }
    }

    // 3. Supabase Auth ile dene (Eğer aktifse)
    try {
      const { data } = await supabase.auth.signInWithPassword({
        email: targetEmail,
        password: cleanPass
      });

      if (data?.user) {
        console.log('[loginUser] Supabase Auth başarılı:', data.user.email);
        cachedSessionUser = data.user;
        await AsyncStorage.setItem('ermay_active_session_user', JSON.stringify(data.user));
        return { success: true, user: data.user };
      }
    } catch {}

    // 4. İLK AÇILIŞ / OTOMATİK İLK YÖNETİCİ EŞLEŞMESİ (Auto-Provisioning)
    // Eğer telefonda henüz hiçbir yerel kullanıcı kaydedilmemişse, girilen bilgileri ilk yönetici olarak kabul et
    if (!localUsername) {
      console.log('[loginUser] İlk açılış tespit edildi, kullanıcı ilk yönetici olarak kaydediliyor:', cleanUser);
      await AsyncStorage.setItem('ermay_saved_username', cleanUser);
      await AsyncStorage.setItem('ermay_saved_password', cleanPass);
      const sessionUser = {
        id: cleanUser,
        email: targetEmail,
        username: cleanUser,
        user_metadata: { role: 'Admin', fullName: cleanUser }
      };
      cachedSessionUser = sessionUser;
      await AsyncStorage.setItem('ermay_active_session_user', JSON.stringify(sessionUser));
      return { success: true, user: sessionUser };
    }

    return { success: false, error: 'Geçersiz kullanıcı adı veya şifre' };
  } catch (err: any) {
    console.error('[loginUser error]:', err);
    return { success: false, error: err.message || 'Giriş yapılamadı.' };
  }
};

export const logoutUser = async () => {
  try {
    cachedSessionUser = null;
    await AsyncStorage.removeItem('ermay_active_session_user');
    await supabase.auth.signOut();
  } catch (e) {}
};

export const getLoggedUser = async (): Promise<any | null> => {
  try {
    // 1. Önce bellekteki aktif oturuma bak
    if (cachedSessionUser) return cachedSessionUser;

    // 2. AsyncStorage'da saklanan aktif oturuma bak
    const stored = await AsyncStorage.getItem('ermay_active_session_user');
    if (stored) {
      try {
        cachedSessionUser = JSON.parse(stored);
        return cachedSessionUser;
      } catch {}
    }

    // 3. Supabase Auth oturumuna bak
    const { data } = await supabase.auth.getUser();
    if (data?.user) {
      cachedSessionUser = data.user;
      return data.user;
    }

    return null;
  } catch (e) {
    return null;
  }
};

export const initFirebase = () => {};
export const startAutoFlush = () => {};
export const stopAutoFlush = () => {};
export const flushPendingWrites = async () => 0;
export const getPendingWriteCount = async () => 0;
export const goOfflineMode = async () => {};
export const goOnlineMode = () => {};

export default supabase;
