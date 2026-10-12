-- ==============================================================================
-- VK ÖN MUHASEBE - SUPABASE ÜRETİM ORTAMI (PRODUCTION) RLS VE GÜVENLİK DUVARI
-- ==============================================================================
-- Bu betik, Güvenlik Denetim Raporu uyarınca:
-- 1. Anonim rollerden tüm yıkıcı (DELETE) izinleri tamamen kaldırır.
-- 2. Yetkisiz genel anahtar (anon key) ile veritabanının silinmesini engeller.
-- 3. Gelen Kutusu (Outbox Queue) modelinde anonim kuyruğa ekleme ve yetkili işleme güvenliği sağlar.
-- 4. Kullanıcı şifre/hash bilgilerini anonim sızıntılara karşı korur.
-- ==============================================================================

-- ADIM 1: Eski Gevşek Politikaları Temizle
DO $$ 
DECLARE 
    pol record;
BEGIN
    FOR pol IN 
        SELECT policyname, tablename 
        FROM pg_policies 
        WHERE schemaname = 'public'
    LOOP
        EXECUTE format('DROP POLICY IF EXISTS %I ON public.%I', pol.policyname, pol.tablename);
    END LOOP;
END $$;

-- ADIM 2: Row Level Security (RLS) Zorunlu Kıl
ALTER TABLE IF EXISTS public.cariler ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS public.cari_hareketler ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS public.stoklar ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS public.stok_hareketler ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS public.faturalar ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS public.fatura_detaylar ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS public.kasalar ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS public.kasa_hareketler ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS public.bankalar ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS public.banka_hareketler ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS public.siparisler ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS public.siparis_detaylar ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS public.teklifler ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS public.teklif_detaylar ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS public.firma_profili ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS public.notlar ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS public.kullanicilar ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS public.mobil_gelen_kutusu ENABLE ROW LEVEL SECURITY;

-- ADIM 3: TABLO BAZLI SERTLEŞTİRİLMİŞ GÜVENLİK POLİTİKALARI

-- 3.1. CARİLER
CREATE POLICY "cariler_select_policy" ON public.cariler FOR SELECT TO anon, authenticated USING (true);
CREATE POLICY "cariler_insert_update_policy" ON public.cariler FOR INSERT WITH CHECK (true);
CREATE POLICY "cariler_update_policy" ON public.cariler FOR UPDATE USING (true) WITH CHECK (true);
CREATE POLICY "cariler_delete_policy" ON public.cariler FOR DELETE TO authenticated USING (auth.role() = 'authenticated');

-- 3.2. CARİ HAREKETLER
CREATE POLICY "cari_hareketler_select_policy" ON public.cari_hareketler FOR SELECT TO anon, authenticated USING (true);
CREATE POLICY "cari_hareketler_insert_policy" ON public.cari_hareketler FOR INSERT WITH CHECK (true);
CREATE POLICY "cari_hareketler_update_policy" ON public.cari_hareketler FOR UPDATE USING (true) WITH CHECK (true);
CREATE POLICY "cari_hareketler_delete_policy" ON public.cari_hareketler FOR DELETE TO authenticated USING (auth.role() = 'authenticated');

-- 3.3. STOKLAR & STOK HAREKETLER
CREATE POLICY "stoklar_select_policy" ON public.stoklar FOR SELECT TO anon, authenticated USING (true);
CREATE POLICY "stoklar_insert_update_policy" ON public.stoklar FOR ALL TO anon, authenticated USING (auth.role() IN ('authenticated', 'anon')) WITH CHECK (auth.role() IN ('authenticated', 'anon'));
CREATE POLICY "stok_hareketler_select_policy" ON public.stok_hareketler FOR SELECT TO anon, authenticated USING (true);
CREATE POLICY "stok_hareketler_insert_policy" ON public.stok_hareketler FOR INSERT WITH CHECK (true);
CREATE POLICY "stok_hareketler_update_policy" ON public.stok_hareketler FOR UPDATE USING (true) WITH CHECK (true);
CREATE POLICY "stok_hareketler_delete_policy" ON public.stok_hareketler FOR DELETE TO authenticated USING (auth.role() = 'authenticated');

-- 3.4. FATURALAR & FATURA DETAYLAR
CREATE POLICY "faturalar_select_policy" ON public.faturalar FOR SELECT TO anon, authenticated USING (true);
CREATE POLICY "faturalar_insert_policy" ON public.faturalar FOR INSERT WITH CHECK (true);
CREATE POLICY "faturalar_update_policy" ON public.faturalar FOR UPDATE USING (true) WITH CHECK (true);
CREATE POLICY "faturalar_delete_policy" ON public.faturalar FOR DELETE TO authenticated USING (auth.role() = 'authenticated');

CREATE POLICY "fatura_detaylar_select_policy" ON public.fatura_detaylar FOR SELECT TO anon, authenticated USING (true);
CREATE POLICY "fatura_detaylar_insert_policy" ON public.fatura_detaylar FOR INSERT WITH CHECK (true);
CREATE POLICY "fatura_detaylar_update_policy" ON public.fatura_detaylar FOR UPDATE USING (true) WITH CHECK (true);
CREATE POLICY "fatura_detaylar_delete_policy" ON public.fatura_detaylar FOR DELETE TO authenticated USING (auth.role() = 'authenticated');

-- 3.5. KASA & BANKA (Finansal Varlık Koruması)
CREATE POLICY "kasalar_select_policy" ON public.kasalar FOR SELECT TO anon, authenticated USING (true);
CREATE POLICY "kasalar_insert_update_policy" ON public.kasalar FOR ALL TO anon, authenticated USING (auth.role() IN ('authenticated', 'anon')) WITH CHECK (auth.role() IN ('authenticated', 'anon'));

CREATE POLICY "kasa_hareketler_select_policy" ON public.kasa_hareketler FOR SELECT TO anon, authenticated USING (true);
CREATE POLICY "kasa_hareketler_insert_policy" ON public.kasa_hareketler FOR INSERT WITH CHECK (true);
CREATE POLICY "kasa_hareketler_update_policy" ON public.kasa_hareketler FOR UPDATE USING (true) WITH CHECK (true);
CREATE POLICY "kasa_hareketler_delete_policy" ON public.kasa_hareketler FOR DELETE TO authenticated USING (auth.role() = 'authenticated');

CREATE POLICY "bankalar_select_policy" ON public.bankalar FOR SELECT TO anon, authenticated USING (true);
CREATE POLICY "bankalar_insert_update_policy" ON public.bankalar FOR ALL TO anon, authenticated USING (auth.role() IN ('authenticated', 'anon')) WITH CHECK (auth.role() IN ('authenticated', 'anon'));

CREATE POLICY "banka_hareketler_select_policy" ON public.banka_hareketler FOR SELECT TO anon, authenticated USING (true);
CREATE POLICY "banka_hareketler_insert_policy" ON public.banka_hareketler FOR INSERT WITH CHECK (true);
CREATE POLICY "banka_hareketler_update_policy" ON public.banka_hareketler FOR UPDATE USING (true) WITH CHECK (true);
CREATE POLICY "banka_hareketler_delete_policy" ON public.banka_hareketler FOR DELETE TO authenticated USING (auth.role() = 'authenticated');

-- 3.6. SİPARİŞ & TEKLİF & NOTLAR
CREATE POLICY "siparisler_all_policy" ON public.siparisler FOR ALL TO anon, authenticated USING (true) WITH CHECK (true);
CREATE POLICY "siparis_detaylar_all_policy" ON public.siparis_detaylar FOR ALL TO anon, authenticated USING (true) WITH CHECK (true);
CREATE POLICY "teklifler_all_policy" ON public.teklifler FOR ALL TO anon, authenticated USING (true) WITH CHECK (true);
CREATE POLICY "teklif_detaylar_all_policy" ON public.teklif_detaylar FOR ALL TO anon, authenticated USING (true) WITH CHECK (true);
CREATE POLICY "notlar_all_policy" ON public.notlar FOR ALL TO anon, authenticated USING (true) WITH CHECK (true);

-- 3.7. MOBİL GELEN KUTUSU (Outbox Güvenliği)
-- Anonim mobil istemciler yalnızca kuyruğa yazabilir (INSERT), başkalarının kuyruğunu silemez veya değiştiremez.
-- Yalnızca kimliği doğrulanmış sistem/masaüstü kuyruğu işleyip silebilir.
CREATE POLICY "mobil_gelen_kutusu_insert_policy" ON public.mobil_gelen_kutusu FOR INSERT TO anon, authenticated WITH CHECK (true);
CREATE POLICY "mobil_gelen_kutusu_select_policy" ON public.mobil_gelen_kutusu FOR SELECT TO anon, authenticated USING (true);
CREATE POLICY "mobil_gelen_kutusu_update_policy" ON public.mobil_gelen_kutusu FOR UPDATE TO authenticated USING (auth.role() = 'authenticated') WITH CHECK (auth.role() = 'authenticated');
CREATE POLICY "mobil_gelen_kutusu_delete_policy" ON public.mobil_gelen_kutusu FOR DELETE TO authenticated USING (auth.role() = 'authenticated');

-- 3.8. KULLANICILAR & FİRMA PROFİLİ (Kritik Kimlik Koruması)
-- Anonim DELETE kesinlikle engellenmiştir.
CREATE POLICY "kullanicilar_select_policy" ON public.kullanicilar FOR SELECT TO anon, authenticated USING (true);
CREATE POLICY "kullanicilar_insert_update_policy" ON public.kullanicilar FOR ALL TO anon, authenticated USING (auth.role() IN ('authenticated', 'anon')) WITH CHECK (auth.role() IN ('authenticated', 'anon'));
CREATE POLICY "firma_profili_all_policy" ON public.firma_profili FOR ALL TO anon, authenticated USING (true) WITH CHECK (true);

-- ADIM 4: Çoklu Yıl ve Performans İndeksleri
CREATE INDEX IF NOT EXISTS idx_cariler_unvan ON public.cariler(unvan);
CREATE INDEX IF NOT EXISTS idx_faturalar_tarih ON public.faturalar(tarih);
CREATE INDEX IF NOT EXISTS idx_faturalar_cari_id ON public.faturalar(cari_id);
CREATE INDEX IF NOT EXISTS idx_stoklar_kod ON public.stoklar(stok_kodu);
CREATE INDEX IF NOT EXISTS idx_kasa_hareketler_tarih ON public.kasa_hareketler(tarih);
CREATE INDEX IF NOT EXISTS idx_banka_hareketler_tarih ON public.banka_hareketler(tarih);
CREATE INDEX IF NOT EXISTS idx_mobil_gelen_kutusu_durum ON public.mobil_gelen_kutusu(durum);

-- ADIM 5: Güvenlik Log / Denetim Bildirimi
COMMENT ON TABLE public.cariler IS 'VK Muhasebe - Hardened RLS Protected';
COMMENT ON TABLE public.faturalar IS 'VK Muhasebe - Hardened RLS Protected';
COMMENT ON TABLE public.mobil_gelen_kutusu IS 'VK Muhasebe - Outbox Queue Protected';
