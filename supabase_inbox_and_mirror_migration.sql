-- ==============================================================================
-- VK ÖN MUHASEBE - SUPABASE GELEN KUTUSU (INBOX) VE AYNA (MIRROR) MİGRASYONU
-- ==============================================================================
-- Bu betik, senkronizasyon mimarisini 'Masaüstü Otorite + Mobil Gelen Kutusu (Inbox)'
-- modeline geçirir.
-- 1. Mobil doğrudan cari/fatura/stok tablolarına INSERT/UPDATE yapmaz.
-- 2. Mobil tüm yeni işlem taleplerini (Tahsilat, Ödeme, Sipariş, Taslak Fatura, Yeni Cari)
--    UUID ile `mobil_gelen_kutusu` tablosuna yazar.
-- 3. Masaüstü bu gelen kutusundaki bekleyen kayıtları çeker, doğrular, resmi evrak
--    numarasını üretip SQLite'a yazar ve 'Islendi' olarak işaretler.
-- 4. Tüm tablolar `mali_yil`, `uuid`, `updated_at` ve `version` kolonlarıyla standartlaştırılır.
-- ==============================================================================

-- 1. MOBİL GELEN KUTUSU TABLOSU
CREATE TABLE IF NOT EXISTS public.mobil_gelen_kutusu (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    islem_turu TEXT NOT NULL, -- 'Tahsilat', 'Odeme', 'TaslakFatura', 'Siparis', 'Teklif', 'YeniCari', 'ZiyaretNotu'
    kaynak_cihaz TEXT,
    payload JSONB NOT NULL,
    durum TEXT NOT NULL DEFAULT 'Bekliyor', -- 'Bekliyor', 'Islendi', 'Reddedildi', 'Hata'
    hata_mesaji TEXT,
    resmi_evrak_no TEXT,
    mali_yil INT NOT NULL DEFAULT EXTRACT(YEAR FROM CURRENT_DATE),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    islenme_tarihi TIMESTAMPTZ
);

ALTER TABLE public.mobil_gelen_kutusu ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS "mobil_gelen_kutusu_auth_policy" ON public.mobil_gelen_kutusu;
CREATE POLICY "mobil_gelen_kutusu_auth_policy" ON public.mobil_gelen_kutusu 
    FOR ALL TO authenticated 
    USING (true) 
    WITH CHECK (true);

CREATE INDEX IF NOT EXISTS idx_gelen_kutusu_durum ON public.mobil_gelen_kutusu(durum);
CREATE INDEX IF NOT EXISTS idx_gelen_kutusu_mali_yil ON public.mobil_gelen_kutusu(mali_yil);
CREATE INDEX IF NOT EXISTS idx_gelen_kutusu_created_at ON public.mobil_gelen_kutusu(created_at);

-- 2. OTOMATİK GÜNCELLEME ZAMAN DAMGASI TETİKLEYİCİSİ (SERVER-SIDE UPDATED_AT)
CREATE OR REPLACE FUNCTION public.trigger_set_timestamp()
RETURNS TRIGGER AS $$
BEGIN
  NEW.updated_at = NOW();
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- 3. AYNA TABLOLARINA MALİ YIL, UUID VE STANDART KOLONLARI EKLEME
DO $$
DECLARE
    tbl text;
    tables text[] := ARRAY[
        'cariler', 'cari_hareketler', 
        'stoklar', 'stok_hareketler', 'stok_gruplar',
        'faturalar', 'fatura_detaylar', 
        'kasalar', 'kasa_hareketler', 
        'bankalar', 'banka_hareketler', 
        'siparisler', 'siparis_detaylar', 
        'teklifler', 'teklif_detaylar'
    ];
BEGIN
    FOREACH tbl IN ARRAY tables LOOP
        IF EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = tbl) THEN
            -- mali_yil kolonu
            EXECUTE format('ALTER TABLE public.%I ADD COLUMN IF NOT EXISTS mali_yil INT DEFAULT EXTRACT(YEAR FROM CURRENT_DATE);', tbl);
            -- uuid kolonu
            EXECUTE format('ALTER TABLE public.%I ADD COLUMN IF NOT EXISTS uuid TEXT;', tbl);
            -- version kolonu
            EXECUTE format('ALTER TABLE public.%I ADD COLUMN IF NOT EXISTS version BIGINT DEFAULT 1;', tbl);
            -- is_deleted kolonu
            EXECUTE format('ALTER TABLE public.%I ADD COLUMN IF NOT EXISTS is_deleted BOOLEAN DEFAULT FALSE;', tbl);
            -- updated_at kolonu
            EXECUTE format('ALTER TABLE public.%I ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ DEFAULT NOW();', tbl);
            
            -- İndeksler
            EXECUTE format('CREATE INDEX IF NOT EXISTS idx_%I_mali_yil ON public.%I(mali_yil);', tbl, tbl);
            EXECUTE format('CREATE INDEX IF NOT EXISTS idx_%I_updated_at ON public.%I(updated_at);', tbl, tbl);
            EXECUTE format('CREATE INDEX IF NOT EXISTS idx_%I_uuid ON public.%I(uuid);', tbl, tbl);

            -- updated_at trigger
            EXECUTE format('DROP TRIGGER IF EXISTS set_timestamp_%I ON public.%I;', tbl, tbl);
            EXECUTE format('CREATE TRIGGER set_timestamp_%I BEFORE UPDATE ON public.%I FOR EACH ROW EXECUTE PROCEDURE public.trigger_set_timestamp();', tbl, tbl);
        END IF;
    END LOOP;
END $$;

COMMENT ON TABLE public.mobil_gelen_kutusu IS 'Mobil cihazlardan masaüstüne aktarılacak işlem gelen kutusu (Inbox)';
