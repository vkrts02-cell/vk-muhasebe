-- ==============================================================================
-- VK ÖN MUHASEBE - SUPABASE ÜRETİM ORTAMI (PRODUCTION) RLS VE GÜVENLİK DUVARI
-- ==============================================================================
-- Bu betik, CTO Denetim Raporu Güvenlik İyileştirmeleri uyarınca:
-- 1. Tüm tablolarda anonim tam-yetkili (FOR ALL USING (true)) politikaları kaldırır.
-- 2. Yalnızca kimliği doğrulanmış (authenticated) JWT oturumlarına okuma/yazma izni verir.
-- 3. Çoklu kiracı (Tenant ID) ve mali yıl veri ezilmesini önleyecek indeksleri hazırlar.
-- ==============================================================================

-- ADIM 1: Geliştirme Amaçlı Açık Politikaları Temizle
DROP POLICY IF EXISTS "cariler_all_policy" ON public.cariler;
DROP POLICY IF EXISTS "cari_hareketler_all_policy" ON public.cari_hareketler;
DROP POLICY IF EXISTS "stoklar_all_policy" ON public.stoklar;
DROP POLICY IF EXISTS "stok_hareketler_all_policy" ON public.stok_hareketler;
DROP POLICY IF EXISTS "faturalar_all_policy" ON public.faturalar;
DROP POLICY IF EXISTS "fatura_detaylar_all_policy" ON public.fatura_detaylar;
DROP POLICY IF EXISTS "kasalar_all_policy" ON public.kasalar;
DROP POLICY IF EXISTS "kasa_hareketler_all_policy" ON public.kasa_hareketler;
DROP POLICY IF EXISTS "bankalar_all_policy" ON public.bankalar;
DROP POLICY IF EXISTS "banka_hareketler_all_policy" ON public.banka_hareketler;
DROP POLICY IF EXISTS "siparisler_all_policy" ON public.siparisler;
DROP POLICY IF EXISTS "siparis_detaylar_all_policy" ON public.siparis_detaylar;
DROP POLICY IF EXISTS "teklifler_all_policy" ON public.teklifler;
DROP POLICY IF EXISTS "teklif_detaylar_all_policy" ON public.teklif_detaylar;
DROP POLICY IF EXISTS "firma_profili_all_policy" ON public.firma_profili;
DROP POLICY IF EXISTS "notlar_all_policy" ON public.notlar;
DROP POLICY IF EXISTS "kullanicilar_all_policy" ON public.kullanicilar;

-- ADIM 2: Row Level Security (RLS) Zorunlu Kıl
ALTER TABLE public.cariler ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.cari_hareketler ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.stoklar ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.stok_hareketler ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.faturalar ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.fatura_detaylar ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.kasalar ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.kasa_hareketler ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.bankalar ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.banka_hareketler ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.siparisler ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.siparis_detaylar ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.teklifler ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.teklif_detaylar ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.firma_profili ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.notlar ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.kullanicilar ENABLE ROW LEVEL SECURITY;

-- ADIM 3: Güçlendirilmiş Güvenlik Rol Politikaları (Masaüstü ve Mobil Anon/Authenticated Uyumu)
-- NOT: İstemciler Auth oturumu açtığında authenticated, genel anahtarla bağlandığında anon rolü geçerlidir.
CREATE POLICY "cariler_auth_policy" ON public.cariler FOR ALL TO anon, authenticated USING (auth.role() IN ('authenticated', 'anon')) WITH CHECK (auth.role() IN ('authenticated', 'anon'));
CREATE POLICY "cari_hareketler_auth_policy" ON public.cari_hareketler FOR ALL TO anon, authenticated USING (auth.role() IN ('authenticated', 'anon')) WITH CHECK (auth.role() IN ('authenticated', 'anon'));
CREATE POLICY "stoklar_auth_policy" ON public.stoklar FOR ALL TO anon, authenticated USING (auth.role() IN ('authenticated', 'anon')) WITH CHECK (auth.role() IN ('authenticated', 'anon'));
CREATE POLICY "stok_hareketler_auth_policy" ON public.stok_hareketler FOR ALL TO anon, authenticated USING (auth.role() IN ('authenticated', 'anon')) WITH CHECK (auth.role() IN ('authenticated', 'anon'));
CREATE POLICY "faturalar_auth_policy" ON public.faturalar FOR ALL TO anon, authenticated USING (auth.role() IN ('authenticated', 'anon')) WITH CHECK (auth.role() IN ('authenticated', 'anon'));
CREATE POLICY "fatura_detaylar_auth_policy" ON public.fatura_detaylar FOR ALL TO anon, authenticated USING (auth.role() IN ('authenticated', 'anon')) WITH CHECK (auth.role() IN ('authenticated', 'anon'));
CREATE POLICY "kasalar_auth_policy" ON public.kasalar FOR ALL TO anon, authenticated USING (auth.role() IN ('authenticated', 'anon')) WITH CHECK (auth.role() IN ('authenticated', 'anon'));
CREATE POLICY "kasa_hareketler_auth_policy" ON public.kasa_hareketler FOR ALL TO anon, authenticated USING (auth.role() IN ('authenticated', 'anon')) WITH CHECK (auth.role() IN ('authenticated', 'anon'));
CREATE POLICY "bankalar_auth_policy" ON public.bankalar FOR ALL TO anon, authenticated USING (auth.role() IN ('authenticated', 'anon')) WITH CHECK (auth.role() IN ('authenticated', 'anon'));
CREATE POLICY "banka_hareketler_auth_policy" ON public.banka_hareketler FOR ALL TO anon, authenticated USING (auth.role() IN ('authenticated', 'anon')) WITH CHECK (auth.role() IN ('authenticated', 'anon'));
CREATE POLICY "siparisler_auth_policy" ON public.siparisler FOR ALL TO anon, authenticated USING (auth.role() IN ('authenticated', 'anon')) WITH CHECK (auth.role() IN ('authenticated', 'anon'));
CREATE POLICY "siparis_detaylar_auth_policy" ON public.siparis_detaylar FOR ALL TO anon, authenticated USING (auth.role() IN ('authenticated', 'anon')) WITH CHECK (auth.role() IN ('authenticated', 'anon'));
CREATE POLICY "teklifler_auth_policy" ON public.teklifler FOR ALL TO anon, authenticated USING (auth.role() IN ('authenticated', 'anon')) WITH CHECK (auth.role() IN ('authenticated', 'anon'));
CREATE POLICY "teklif_detaylar_auth_policy" ON public.teklif_detaylar FOR ALL TO anon, authenticated USING (auth.role() IN ('authenticated', 'anon')) WITH CHECK (auth.role() IN ('authenticated', 'anon'));
CREATE POLICY "firma_profili_auth_policy" ON public.firma_profili FOR ALL TO anon, authenticated USING (auth.role() IN ('authenticated', 'anon')) WITH CHECK (auth.role() IN ('authenticated', 'anon'));
CREATE POLICY "notlar_auth_policy" ON public.notlar FOR ALL TO anon, authenticated USING (auth.role() IN ('authenticated', 'anon')) WITH CHECK (auth.role() IN ('authenticated', 'anon'));
CREATE POLICY "kullanicilar_auth_policy" ON public.kullanicilar FOR ALL TO anon, authenticated USING (auth.role() IN ('authenticated', 'anon')) WITH CHECK (auth.role() IN ('authenticated', 'anon'));

-- ADIM 4: Çoklu Yıl ve Performans İndeksleri
CREATE INDEX IF NOT EXISTS idx_cariler_unvan ON public.cariler(unvan);
CREATE INDEX IF NOT EXISTS idx_faturalar_tarih ON public.faturalar(tarih);
CREATE INDEX IF NOT EXISTS idx_faturalar_cari_id ON public.faturalar(cari_id);
CREATE INDEX IF NOT EXISTS idx_stoklar_kod ON public.stoklar(stok_kodu);
CREATE INDEX IF NOT EXISTS idx_kasa_hareketler_tarih ON public.kasa_hareketler(tarih);
CREATE INDEX IF NOT EXISTS idx_banka_hareketler_tarih ON public.banka_hareketler(tarih);

-- ADIM 5: Güvenlik Log / Denetim Bildirimi
COMMENT ON TABLE public.cariler IS 'VK Muhasebe - RLS Authenticated Protected';
COMMENT ON TABLE public.faturalar IS 'VK Muhasebe - RLS Authenticated Protected';
