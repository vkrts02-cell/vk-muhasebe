import { getSupabaseClient } from './supabase';
import { generateSyncGuid } from '../utils/IdGenerator';

export type InboxIslemTuru = 
  | 'Tahsilat'
  | 'Odeme'
  | 'TaslakFatura'
  | 'Siparis'
  | 'Teklif'
  | 'YeniCari'
  | 'ZiyaretNotu';

export interface SendToInboxRequest {
  islemTuru: InboxIslemTuru;
  kaynakCihaz?: string;
  payload: any;
  maliYil?: number;
}

export interface InboxResponse {
  success: boolean;
  id?: string;
  error?: string;
}

/**
 * sendToInbox: Mobil işlemleri doğrudan ana tablolara yazmak yerine,
 * masaüstü otorite modelinde çalışan `mobil_gelen_kutusu` tablosuna iletir.
 * Çakışma, numara sırası sapması ve Smart Merge veri kaybı risklerini sıfırlar.
 */
export const sendToInbox = async (req: SendToInboxRequest): Promise<InboxResponse> => {
  try {
    const supabase = getSupabaseClient();
    const id = generateSyncGuid();
    const currentYear = req.maliYil || new Date().getFullYear();

    const record = {
      id,
      islem_turu: req.islemTuru,
      kaynak_cihaz: req.kaynakCihaz || 'mobil-app',
      payload: req.payload,
      durum: 'Bekliyor',
      mali_yil: currentYear,
      created_at: new Date().toISOString()
    };

    const { error } = await supabase
      .from('mobil_gelen_kutusu')
      .insert(record);

    if (error) {
      console.warn('[InboxService] Supabase insert error:', error.message);
      return { success: false, error: error.message };
    }

    return { success: true, id };
  } catch (err: any) {
    console.error('[InboxService] Failed to send to inbox:', err);
    return { success: false, error: err?.message || 'Bilinmeyen hata' };
  }
};

/**
 * getInboxStatus: Mobilde gönderilen bir işlemin masaüstü tarafından işlenip işlenmediğini sorgular.
 */
export const getInboxStatus = async (id: string): Promise<{ durum: string; resmiEvrakNo?: string; hataMesaji?: string } | null> => {
  try {
    const supabase = getSupabaseClient();
    const { data, error } = await supabase
      .from('mobil_gelen_kutusu')
      .select('durum, resmi_evrak_no, hata_mesaji')
      .eq('id', id)
      .single();

    if (error || !data) return null;
    return {
      durum: data.durum,
      resmiEvrakNo: data.resmi_evrak_no,
      hataMesaji: data.hata_mesaji
    };
  } catch {
    return null;
  }
};
