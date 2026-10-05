/**
 * transactionService.ts — Masaüstü `FinansService.SaveTransactionAsync` mobil eşleniği.
 *
 * RTDB'de gerçek "transaction" olmadığı için, tüm yazımlar sırayla yapılır ve
 * hata durumunda (net / ağ / 5xx) şimdiye kadar yazılan kayıtlar geri alınır
 * (best-effort rollback). Böylece masaüstündeki atomik finans mantığı korunur:
 *   1) CariHareket (ana)                    + Cari bakiye güncelle
 *   2) Kasa veya Banka hareketi             + hesap bakiye güncelle
 *   3) Kredi Kartı / EFT detay kaydı        (yönteme göre)
 *   4) Yönlendirilen tedarikçi (ciro)       + banka/kasa çıkış hareketi
 */
import { writeData, deleteData, readData } from './firebase';
import { generateInt32Id } from '../utils/IdGenerator';
import { sendToInbox } from './inboxService';

export type IslemTuru = 'Tahsilat' | 'Ödeme' | 'Alacak Dekontu' | 'Borç Dekontu';
export type OdemeYontemi = 'Nakit' | 'Kredi Kartı' | 'Havale/EFT' | 'Havale / EFT' | 'Çek' | 'Banka';

export interface FinancialTransactionRequest {
  id?: number | string;
  cari: { id: number | string; unvan: string };
  amount: number;
  date: string;
  dateTime?: Date;
  transactionType: IslemTuru;
  method: OdemeYontemi;
  description: string;
  /** Nakit -> kasa (Bankalar/kartTuru=Kasa), diğer -> banka hesabı */
  selectedHesap?: { id: number | string; kartTuru?: string; bakiye?: number } | null;
  bankaAdi?: string;
  kartHesapNo?: string;
  slipDekontPath?: string;
  directedSupplier?: { id: number | string; unvan: string } | null;
  yonlendirmeTarihi?: string;
  /** Çek yöntemi için opsiyonel çek detayları */
  cekDetay?: any;
}

const getEvrakNoPrefix = (method: string): string => {
  if (method.includes('Kredi')) return 'KK-';
  if (method.includes('EFT') || method.includes('Havale')) return 'EFT-';
  if (method.includes('Çek')) return 'CK-';
  return 'TS-';
};

const getMethodAbbr = (method: string): string => {
  if (method.includes('Kredi')) return 'KK';
  if (method.includes('EFT') || method.includes('Havale')) return 'EFT';
  return method;
};

const randomKey = () => Date.now().toString(36) + Math.random().toString(36).substring(2, 8) + Date.now().toString(36).slice(-4);

// writeData ağ/bağlantı sorunlarında throw yerine false döner (firebase.ts).
// Burada tüm kritik yazımları sarar; başarısız olursa üstteki catch/rollback tetiklenir.
const mustWrite = async (path: string, data: any) => {
  const ok = await writeData(path, data);
  if (!ok) throw new Error(`writeData failed: ${path}`);
};

export const saveFinancialTransaction = async (req: FinancialTransactionRequest): Promise<boolean> => {
  if (!req.cari || req.amount <= 0) return false;

  const refId = randomKey();
  const evrakNo = getEvrakNoPrefix(req.method) + new Date().toISOString().replace(/[-:.TZ]/g, '').slice(0, 14);

  const isOdeme = req.transactionType === 'Ödeme' || req.transactionType === 'Borç Dekontu' || (req.transactionType as string) === 'Borçlandır' || (req.transactionType as string) === 'Borclandir';
  const chId = (req.id !== undefined && !isNaN(Number(req.id)) && Number(req.id) > 0) ? Number(req.id) : generateInt32Id();
  const mainCH = {
    id: chId,
    cariId: req.cari.id,
    cariUnvan: req.cari.unvan || '',
    tarih: req.date,
    evrakNo,
    refId,
    islemTuru: `${req.transactionType} (${getMethodAbbr(req.method)})`,
    aciklama: `[${req.method}] ${req.description}`.trim(),
    borc: isOdeme ? req.amount : 0,
    alacak: !isOdeme ? req.amount : 0,
    yonlendirilenCariId: req.directedSupplier?.id || undefined,
    yonlendirilenCariUnvan: req.directedSupplier?.unvan || undefined,
    isDeleted: false,
  };

  const written: string[] = [];
  const rollback: (() => Promise<void>)[] = [];

  try {
    // --- 1. Cari hareketi + cari bakiyesi ---
    const chKey = String(chId);
    await mustWrite(`CariHareketler/${chKey}`, mainCH);
    written.push(`CariHareketler/${chKey}`);

    const cariKey = String(req.cari.id);
    const cariRef = await readData(`Cariler/${cariKey}`);
    const prevCari = cariRef ? { ...cariRef } : null;
    const cari = cariRef || { id: req.cari.id, unvan: req.cari.unvan, borc: 0, alacak: 0 };
    cari.borc = (cari.borc || 0) + mainCH.borc;
    cari.alacak = (cari.alacak || 0) + mainCH.alacak;
    await mustWrite(`Cariler/${cariKey}`, cari);
    written.push(`Cariler/${cariKey}`);
    if (prevCari) rollback.push(async () => { await writeData(`Cariler/${cariKey}`, prevCari); });

    // --- 2. Kasa / Banka hareketi + hesap bakiyesi ---
    if (req.selectedHesap && req.selectedHesap.id !== undefined && req.selectedHesap.id !== null) {
      const isKasa = req.selectedHesap.kartTuru === 'Kasa' || (req.selectedHesap as any).turu === 'Kasa' || req.selectedHesap.kartTuru === undefined;
      const hesapTable = isKasa ? 'Kasalar' : 'Bankalar';
      const hesapKey = String(req.selectedHesap.id);
      let hesapRef = await readData(`${hesapTable}/${hesapKey}`);
      if (!hesapRef && isKasa) {
        hesapRef = await readData(`Bankalar/${hesapKey}`);
      }
      const prevHesap = hesapRef ? { ...hesapRef } : null;
      const hesap = hesapRef || { id: req.selectedHesap.id, hesapAdi: isKasa ? 'Kasa' : 'Banka', kartTuru: isKasa ? 'Kasa' : 'Banka', bakiye: 0 };

      const hareketId = generateInt32Id();
      const hareket = {
        id: hareketId,
        [isKasa ? 'kasaId' : 'bankaId']: req.selectedHesap.id,
        cariId: req.cari.id,
        cariUnvan: req.cari.unvan,
        tarih: req.date,
        evrakNo,
        refId,
        islemTuru: mainCH.islemTuru,
        aciklama: `${req.cari.unvan} - ${req.transactionType} (${req.description})`,
        giren: !isOdeme ? req.amount : 0,
        cikan: isOdeme ? req.amount : 0,
        tutar: req.amount,
        yonlendirilenCariId: req.directedSupplier?.id || undefined,
        yonlendirilenCariUnvan: req.directedSupplier?.unvan || undefined,
      };
      const hareketPath = isKasa ? 'KasaHareketler' : 'BankaHareketler';
      const hareketKey = String(hareketId);
      await mustWrite(`${hareketPath}/${hareketKey}`, hareket);
      written.push(`${hareketPath}/${hareketKey}`);

      const hesapBakiye = (hesap.bakiye || 0) + (hareket.giren - hareket.cikan);
      const hesapPayload = { ...hesap, kartTuru: isKasa ? 'Kasa' : hesap.kartTuru, bakiye: hesapBakiye };
      await mustWrite(`${hesapTable}/${hesapKey}`, hesapPayload);
      written.push(`${hesapTable}/${hesapKey}`);
      if (prevHesap) rollback.push(async () => { await writeData(`${hesapTable}/${hesapKey}`, prevHesap); });

      // --- 3. Kredi Kartı / EFT detay tabloları ---
      if (req.method.includes('Kredi')) {
        const kkId = generateInt32Id();
        const kk = {
          id: kkId,
          musteriId: req.cari.id,
          musteriUnvan: req.cari.unvan,
          tarih: req.date,
          tutar: req.amount,
          banka: req.bankaAdi || '',
          kartNo: req.kartHesapNo || '',
          onayKodu: refId,
          durum: req.directedSupplier ? 'Tedarikçiye Verildi' : 'Portföyde',
          aciklama: req.description,
          islemTuru: req.transactionType,
          slipDosyaYolu: req.slipDekontPath || '',
          yonlendirilenCariId: req.directedSupplier?.id || undefined,
          yonlendirilenCariUnvan: req.directedSupplier?.unvan || undefined,
        };
        const kkKey = String(kkId);
        await mustWrite(`KrediKartlari/${kkKey}`, kk);
        written.push(`KrediKartlari/${kkKey}`);
      } else if (req.method.includes('EFT') || req.method.includes('Havale')) {
        const eftId = generateInt32Id();
        const eft = {
          id: eftId,
          musteriId: req.cari.id,
          musteriUnvan: req.cari.unvan,
          tarih: req.date,
          tutar: req.amount,
          banka: req.bankaAdi || '',
          bankaId: req.selectedHesap.id,
          hesapNo: req.kartHesapNo || '',
          dekontNo: refId,
          durum: req.directedSupplier ? 'Tedarikçiye Yönlendirildi' : 'Tamamlandı',
          aciklama: req.description,
          islemTuru: req.transactionType,
          dekontPath: req.slipDekontPath || '',
          yonlendirilenCariId: req.directedSupplier?.id || undefined,
          yonlendirilenCariUnvan: req.directedSupplier?.unvan || undefined,
        };
        const eftKey = String(eftId);
        await mustWrite(`EftIslemleri/${eftKey}`, eft);
        written.push(`EftIslemleri/${eftKey}`);
      }
    }

    // --- 4. Yönlendirilen tedarikçi (ciro) ---
    if (req.directedSupplier && req.selectedHesap && req.selectedHesap.id !== undefined && req.selectedHesap.id !== null) {
      const supKey = String(req.directedSupplier.id);
      const supRef = await readData(`Cariler/${supKey}`);
      const prevSup = supRef ? { ...supRef } : null;
      const sup = supRef || { id: req.directedSupplier.id, unvan: req.directedSupplier.unvan, borc: 0, alacak: 0 };
      sup.borc = (sup.borc || 0) + req.amount;
      await mustWrite(`Cariler/${supKey}`, sup);
      written.push(`Cariler/${supKey}`);
      if (prevSup) rollback.push(async () => { await writeData(`Cariler/${supKey}`, prevSup); });

      const supChId = generateInt32Id();
      const supCH = {
        id: supChId,
        cariId: req.directedSupplier.id,
        cariUnvan: req.directedSupplier.unvan || '',
        tarih: req.yonlendirmeTarihi || req.date,
        evrakNo: evrakNo + '-SUP',
        refId: refId + '-SUP',
        islemTuru: `Ödeme (${getMethodAbbr(req.method)} Ciro)`,
        aciklama: `[Ciro] ${req.cari.unvan} üzerinden ciro edilen ${req.method} tahsilatı. (${req.description})`.trim(),
        borc: req.amount,
        alacak: 0,
      };
      const supChKey = String(supChId);
      await mustWrite(`CariHareketler/${supChKey}`, supCH);
      written.push(`CariHareketler/${supChKey}`);

      const isKasa = req.selectedHesap.kartTuru === 'Kasa' || (req.selectedHesap as any).turu === 'Kasa' || req.selectedHesap.kartTuru === undefined;
      const hesapTable = isKasa ? 'Kasalar' : 'Bankalar';
      const hesapKey = String(req.selectedHesap.id);
      let hesapRef = await readData(`${hesapTable}/${hesapKey}`);
      if (!hesapRef && isKasa) {
        hesapRef = await readData(`Bankalar/${hesapKey}`);
      }
      const prevHesap = hesapRef ? { ...hesapRef } : null;
      const hesap = hesapRef || { id: req.selectedHesap.id, hesapAdi: isKasa ? 'Kasa' : 'Banka', kartTuru: isKasa ? 'Kasa' : 'Banka', bakiye: 0 };
      const ciroHareketId = generateInt32Id();
      const ciroHareket = {
        id: ciroHareketId,
        [isKasa ? 'kasaId' : 'bankaId']: req.selectedHesap.id,
        cariId: req.directedSupplier.id,
        cariUnvan: req.directedSupplier.unvan,
        tarih: req.yonlendirmeTarihi || req.date,
        evrakNo: evrakNo + '-SUP',
        refId: refId + '-SUP',
        islemTuru: supCH.islemTuru,
        aciklama: `Ciro Çıkışı -> ${req.directedSupplier.unvan} (${req.cari.unvan} üzerinden)`,
        giren: 0,
        cikan: req.amount,
        tutar: req.amount,
      };
      const ciroPath = isKasa ? 'KasaHareketler' : 'BankaHareketler';
      const ciroKey = String(ciroHareketId);
      await mustWrite(`${ciroPath}/${ciroKey}`, ciroHareket);
      written.push(`${ciroPath}/${ciroKey}`);

      const ciroPayload = { ...hesap, bakiye: (hesap.bakiye || 0) - req.amount };
      await mustWrite(`${hesapTable}/${hesapKey}`, ciroPayload);
      written.push(`${hesapTable}/${hesapKey}`);
      if (prevHesap) rollback.push(async () => { await writeData(`${hesapTable}/${hesapKey}`, prevHesap); });
    }

    // Also forward to Desktop Authority Mobil Gelen Kutusu for official desktop processing & synchronization
    try {
      await sendToInbox({
        islemTuru: isOdeme ? 'Odeme' : 'Tahsilat',
        payload: {
          cariId: req.cari.id,
          cariUnvan: req.cari.unvan,
          amount: req.amount,
          method: req.method,
          description: req.description,
          date: req.date,
          selectedHesapId: req.selectedHesap?.id,
          bankaAdi: req.bankaAdi,
          kartHesapNo: req.kartHesapNo,
          yonlendirilenTedarikciId: req.directedSupplier?.id
        }
      });
    } catch (inboxErr) {
      console.warn('[transactionService] Mobil gelen kutusuna iletme uyarısı:', inboxErr);
    }

    return true;
  } catch (error) {
    console.error('[transactionService] İşlem başarısız, geri alınıyor:', error);
    // Rollback: yazılan kayıtları sil ve eski bakiyeleri geri yükle
    for (const r of rollback) { try { await r(); } catch {} }
    for (const p of written) { try { await deleteData(p); } catch {} }
    return false;
  }
};

export const deleteFaturaCascade = async (faturaIdOrNo: number | string): Promise<boolean> => {
  if (!faturaIdOrNo) return false;

  try {
    const toKeyList = (raw: any) => {
      if (!raw) return [];
      if (Array.isArray(raw)) {
        return raw.map((item, idx) => item ? ({ ...item, firebaseKey: String(item?.id ?? idx) }) : null).filter(Boolean);
      }
      return Object.keys(raw).map((k) => ({ ...raw[k], firebaseKey: k }));
    };

    // 1. Locate the invoice
    let oldFatura: any = null;
    const cleanNo = String(faturaIdOrNo).startsWith('KPL-') ? String(faturaIdOrNo).substring(4).trim() : String(faturaIdOrNo).trim();

    if (typeof faturaIdOrNo === 'number' || (!isNaN(Number(faturaIdOrNo)) && Number(faturaIdOrNo) > 0)) {
      const numId = Number(faturaIdOrNo);
      const raw = await readData(`Faturalar/${numId}`);
      if (raw && (raw.id !== undefined || raw.Id !== undefined || raw.faturaNo || raw.FaturaNo)) {
        oldFatura = { ...raw, id: raw.id ?? raw.Id ?? numId };
      }
    }

    if (!oldFatura) {
      const allFaturalarRaw = (await readData('Faturalar')) || {};
      const allFaturalar = toKeyList(allFaturalarRaw);
      oldFatura = allFaturalar.find((f: any) =>
        (f.id !== undefined && String(f.id) === cleanNo) ||
        (f.Id !== undefined && String(f.Id) === cleanNo) ||
        ((f.faturaNo || f.FaturaNo) && String(f.faturaNo || f.FaturaNo).trim().toLowerCase() === cleanNo.toLowerCase())
      );
    }

    if (!oldFatura) {
      console.warn(`[transactionService] Fatura bulunamadı: ${faturaIdOrNo}`);
      return false;
    }

    const faturaId = oldFatura.id ?? oldFatura.Id ?? faturaIdOrNo;
    const faturaNo = String(oldFatura.faturaNo || oldFatura.FaturaNo || '').trim();
    const turLower = String(oldFatura.tur || oldFatura.Tur || '').toLowerCase();
    let isSatis = turLower.includes('sat') || turLower.includes('çık') || turLower.includes('cik');
    const genelToplam = parseFloat(oldFatura.genelToplam ?? oldFatura.GenelToplam) || 0;
    const isPaid = (oldFatura.odenen || oldFatura.Odenen || 0) > 0 || (oldFatura.odemeSekli && oldFatura.odemeSekli !== 'Açık' && oldFatura.odemeSekli !== 'Acik');
    const oldCariId = oldFatura.cariId ?? oldFatura.CariId;

    // 2. Prepare Details & Movements for stock rollback
    const detayRaw = (await readData(`FaturaDetaylar/${faturaId}`)) || [];
    let detaylar = Array.isArray(detayRaw)
      ? detayRaw.filter(Boolean)
      : Object.keys(detayRaw).map(key => ({ ...(detayRaw as any)[key], id: parseInt(key) || key }));

    const shRaw = (await readData('StokHareketler')) || {};
    const shList = toKeyList(shRaw);
    const matchSh = shList.filter((h: any) =>
      (h.faturaId !== undefined && (String(h.faturaId) === String(faturaId) || String(h.faturaId) === cleanNo)) ||
      (h.FaturaId !== undefined && (String(h.FaturaId) === String(faturaId) || String(h.FaturaId) === cleanNo)) ||
      (faturaNo && h.evrakNo && String(h.evrakNo).trim().toLowerCase() === faturaNo.toLowerCase())
    );

    if (!detaylar.length && matchSh.length > 0) {
      detaylar = matchSh.map((m: any) => {
        const mGiren = parseFloat(m.giren) || 0;
        const mCikan = parseFloat(m.cikan) || 0;
        const mMiktar = parseFloat(m.miktar) || 0;
        if (!isSatis && mCikan > 0 && mGiren === 0) {
          isSatis = true;
        }
        return {
          stokId: m.stokId,
          miktar: mMiktar > 0 ? mMiktar : (mCikan > 0 ? mCikan : (mGiren > 0 ? mGiren : 0))
        };
      });
    }

    // Identify all affected stock IDs
    const affectedStokIds = new Set<string>();
    for (const d of detaylar) {
      if (d.stokId) affectedStokIds.add(String(d.stokId));
    }
    for (const m of matchSh) {
      if (m.stokId) affectedStokIds.add(String(m.stokId));
    }

    // 3. Revert Cari balance
    if (oldCariId) {
      try {
        const freshCari = await readData(`Cariler/${oldCariId}`);
        if (freshCari) {
          const updatedCari = { ...freshCari };
          if (isPaid) {
            updatedCari.borc = Math.max(0, (parseFloat(updatedCari.borc) || 0) - genelToplam);
            updatedCari.alacak = Math.max(0, (parseFloat(updatedCari.alacak) || 0) - genelToplam);
          } else if (isSatis) {
            updatedCari.borc = Math.max(0, (parseFloat(updatedCari.borc) || 0) - genelToplam);
          } else {
            updatedCari.alacak = Math.max(0, (parseFloat(updatedCari.alacak) || 0) - genelToplam);
          }
          await writeData(`Cariler/${oldCariId}`, updatedCari);
        }
      } catch (e) {
        console.error(`[transactionService] Cari ${oldCariId} bakiye geri alınamadı:`, e);
      }
    }

    // 4. Delete StokHareketler first
    for (const h of matchSh) {
      const k = h.firebaseKey || h.id;
      if (k) { try { await deleteData(`StokHareketler/${k}`); } catch {} }
    }

    // 5. Revert & verify Stock balances (supports both firebaseKey and numeric ID, recalculates from remaining movements)
    const allStoklarRaw = (await readData('Stoklar')) || {};
    const allStoklar = toKeyList(allStoklarRaw);
    const remainingShRaw = (await readData('StokHareketler')) || {};
    const remainingShList = toKeyList(remainingShRaw);

    for (const sid of affectedStokIds) {
      try {
        let stokObj = allStoklar.find((s: any) => String(s.id) === sid || s.firebaseKey === sid);
        if (!stokObj) {
          stokObj = await readData(`Stoklar/${sid}`);
        }
        if (!stokObj) continue;
        const stokTargetKey = stokObj.firebaseKey || sid;

        const myRemainingSh = remainingShList.filter((h: any) =>
          String(h.stokId) === sid &&
          !matchSh.some((m: any) => (m.firebaseKey && h.firebaseKey && m.firebaseKey === h.firebaseKey) || (m.id && h.id && String(m.id) === String(h.id)))
        );
        let ortAlis = 0;
        let ortSatis = 0;
        let netMiktar = 0;
        if (myRemainingSh.length > 0) {
          let totPurVal = 0, totPurQty = 0;
          let totSaleVal = 0, totSaleQty = 0;
          for (const h of myRemainingSh) {
            const g = parseFloat(h.giren) || 0;
            const c = parseFloat(h.cikan) || 0;
            const m = parseFloat(h.miktar) || 0;
            const f = parseFloat(h.fiyat) || 0;
            netMiktar += (g - c);
            const isGiris = g > 0 || (h.islemTuru && (h.islemTuru.includes('Giriş') || h.islemTuru.includes('Alış') || h.islemTuru.includes('Açılış')));
            if (isGiris) {
              const q = g > 0 ? g : m;
              totPurVal += q * f;
              totPurQty += q;
            } else {
              const q = c > 0 ? c : m;
              totSaleVal += q * f;
              totSaleQty += q;
            }
          }
          ortAlis = totPurQty > 0 ? totPurVal / totPurQty : 0;
          ortSatis = totSaleQty > 0 ? totSaleVal / totSaleQty : 0;
        } else {
          const relatedD = detaylar.filter((d: any) => String(d.stokId) === sid);
          const totalMiktar = relatedD.reduce((acc: number, d: any) => acc + (parseFloat(d.miktar) || 0), 0);
          const currentMiktar = parseFloat(stokObj.miktar ?? stokObj.Miktar ?? 0) || 0;
          netMiktar = isSatis ? (currentMiktar + totalMiktar) : (currentMiktar - totalMiktar);
        }
        await writeData(`Stoklar/${stokTargetKey}`, { 
          ...stokObj, 
          miktar: netMiktar, 
          Miktar: netMiktar,
          ortalamaAlisFiyati: Number(ortAlis.toFixed(2)),
          ortalamaSatisFiyati: Number(ortSatis.toFixed(2)),
          ortAlisFiyati: Number(ortAlis.toFixed(2)),
          ortSatisFiyati: Number(ortSatis.toFixed(2)),
          OrtalamaAlisFiyati: Number(ortAlis.toFixed(2)),
          OrtalamaSatisFiyati: Number(ortSatis.toFixed(2)),
          alisFiyati: stokObj.alisFiyati,
          satisFiyati: stokObj.satisFiyati,
          id: stokObj.id ?? (isNaN(Number(sid)) ? sid : parseInt(sid)) 
        });
      } catch (e) {
        console.error(`[transactionService] Stok ${sid} bakiye geri alınamadı:`, e);
      }
    }

    // 6. Delete CariHareketler (including KPL- closing movement)
    const chRaw = (await readData('CariHareketler')) || {};
    const chList = toKeyList(chRaw);
    for (const h of chList) {
      const hEvrak = String(h.evrakNo || h.EvrakNo || '').trim().toLowerCase();
      const hFId = h.faturaId !== undefined ? h.faturaId : h.FaturaId;
      const match =
        (hFId !== undefined && (String(hFId) === String(faturaId) || String(hFId) === cleanNo)) ||
        (faturaNo && (hEvrak === faturaNo.toLowerCase() || hEvrak === `kpl-${faturaNo.toLowerCase()}`));
      if (match) {
        const k = h.firebaseKey || h.id;
        if (k) { try { await deleteData(`CariHareketler/${k}`); } catch {} }
      }
    }

    // 7. Delete Kasa / Banka movements if closed
    const khRaw = (await readData('KasaHareketler')) || {};
    const khList = toKeyList(khRaw);
    for (const kh of khList) {
      const khAcik = kh.aciklama || '';
      const khEvrak = kh.evrakNo || '';
      if (faturaNo && (khEvrak === faturaNo || khAcik.includes(faturaNo))) {
        try {
          const kasaId = kh.kasaId || kh.hesapId;
          if (kasaId) {
            const kRef = await readData(`Bankalar/${kasaId}`);
            if (kRef) {
              const giren = kh.tur === 'Giriş' ? (kh.tutar || 0) : (kh.giren || 0);
              const cikan = kh.tur === 'Çıkış' ? (kh.tutar || 0) : (kh.cikan || 0);
              const yeniBakiye = (kRef.bakiye || 0) - giren + cikan;
              await writeData(`Bankalar/${kasaId}`, { ...kRef, kartTuru: 'Kasa', bakiye: yeniBakiye });
            }
          }
          const k = kh.firebaseKey || kh.id;
          if (k) await deleteData(`KasaHareketler/${k}`);
        } catch {}
      }
    }

    const bhRaw = (await readData('BankaHareketler')) || {};
    const bhList = toKeyList(bhRaw);
    for (const bh of bhList) {
      const bhAcik = bh.aciklama || '';
      const bhEvrak = bh.evrakNo || '';
      if (faturaNo && (bhEvrak === faturaNo || bhAcik.includes(faturaNo))) {
        try {
          const bankaId = bh.bankaId || bh.hesapId;
          if (bankaId) {
            const bRef = await readData(`Bankalar/${bankaId}`);
            if (bRef) {
              const giren = bh.giren || bh.borc || 0;
              const cikan = bh.cikan || bh.alacak || 0;
              const yeniBakiye = (bRef.bakiye || 0) - giren + cikan;
              await writeData(`Bankalar/${bankaId}`, { ...bRef, bakiye: yeniBakiye });
            }
          }
          const k = bh.firebaseKey || bh.id;
          if (k) await deleteData(`BankaHareketler/${k}`);
        } catch {}
      }
    }

    // 8. Delete FaturaDetaylar & mark Fatura isDeleted
    try { await deleteData(`FaturaDetaylar/${faturaId}`); } catch {}
    try { await deleteData(`Faturalar/${faturaId}`); } catch {}
    try {
      await writeData(`Faturalar/${faturaId}`, { ...oldFatura, isDeleted: true });
    } catch {}

    return true;
  } catch (err) {
    console.error('[transactionService] deleteFaturaCascade hatası:', err);
    return false;
  }
};

export const deleteFinancialTransaction = async (cariHareket: any): Promise<boolean> => {
  if (!cariHareket) return false;

  // Check if this cariHareket is linked to an invoice (Fatura)
  const rawFaturaId = cariHareket.faturaId || cariHareket.FaturaId;
  const cleanEvrak = String(cariHareket.evrakNo || cariHareket.EvrakNo || '').trim();
  const isInvoice = (cariHareket.islemTuru && (cariHareket.islemTuru.includes('Fatura') || cariHareket.islemTuru.includes('Satış') || cariHareket.islemTuru.includes('Alış'))) ||
                    (rawFaturaId && Number(rawFaturaId) > 0) ||
                    cleanEvrak.startsWith('FAT') ||
                    cleanEvrak.startsWith('SF') ||
                    cleanEvrak.startsWith('AF') ||
                    cleanEvrak.startsWith('FTR') ||
                    cleanEvrak.startsWith('KPL-');

  if (isInvoice) {
    const fId = rawFaturaId || cariHareket.evrakNo;
    const okCascade = await deleteFaturaCascade(fId);
    if (okCascade) {
      const chKey = cariHareket.firebaseKey || cariHareket.id || cariHareket.Id;
      if (chKey) {
        try { await deleteData(`CariHareketler/${chKey}`); } catch {}
      }
      return true;
    }

    // Fallback: If invoice document was not found, still cleanup any orphan stock movements linked to this invoice
    try {
      const shRaw = (await readData('StokHareketler')) || {};
      const toKeyListFallback = (raw: any) => {
        if (!raw) return [];
        if (Array.isArray(raw)) return raw.map((item, idx) => item ? ({ ...item, firebaseKey: String(item?.id ?? idx) }) : null).filter(Boolean);
        return Object.keys(raw).map((k) => ({ ...raw[k], firebaseKey: k }));
      };
      const shList = toKeyListFallback(shRaw);
      const cleanEvrak = String(cariHareket.evrakNo || '').replace(/^KPL-/, '').trim().toLowerCase();
      const fIdStr = String(rawFaturaId || '');
      const orphanSh = shList.filter((h: any) =>
        (fIdStr && (String(h.faturaId) === fIdStr)) ||
        (cleanEvrak && h.evrakNo && String(h.evrakNo).trim().toLowerCase() === cleanEvrak)
      );

      if (orphanSh.length > 0) {
        const orphanStokIds = new Set<string>();
        for (const h of orphanSh) {
          if (h.stokId) orphanStokIds.add(String(h.stokId));
          const k = h.firebaseKey || h.id;
          if (k) await deleteData(`StokHareketler/${k}`);
        }

        const allStoklarRaw = (await readData('Stoklar')) || {};
        const allStoklar = toKeyListFallback(allStoklarRaw);
        const remainingShRaw = (await readData('StokHareketler')) || {};
        const remainingShList = toKeyListFallback(remainingShRaw);

        for (const sid of orphanStokIds) {
          const stokObj = allStoklar.find((s: any) => String(s.id) === sid || s.firebaseKey === sid);
          if (!stokObj) continue;
          const stokTargetKey = stokObj.firebaseKey || sid;
          const mySh = remainingShList.filter((h: any) => String(h.stokId) === sid);
          let ortAlis = 0;
          let ortSatis = 0;
          let netMiktar = 0;
          if (mySh.length > 0) {
            let totPurVal = 0, totPurQty = 0;
            let totSaleVal = 0, totSaleQty = 0;
            for (const h of mySh) {
              const g = parseFloat(h.giren) || 0;
              const c = parseFloat(h.cikan) || 0;
              const m = parseFloat(h.miktar) || 0;
              const f = parseFloat(h.fiyat) || 0;
              netMiktar += (g - c);
              const isGiris = g > 0 || (h.islemTuru && (h.islemTuru.includes('Giriş') || h.islemTuru.includes('Alış') || h.islemTuru.includes('Açılış')));
              if (isGiris) {
                const q = g > 0 ? g : m;
                totPurVal += q * f;
                totPurQty += q;
              } else {
                const q = c > 0 ? c : m;
                totSaleVal += q * f;
                totSaleQty += q;
              }
            }
            ortAlis = totPurQty > 0 ? totPurVal / totPurQty : 0;
            ortSatis = totSaleQty > 0 ? totSaleVal / totSaleQty : 0;
          }
          await writeData(`Stoklar/${stokTargetKey}`, { 
            ...stokObj, 
            miktar: netMiktar, 
            ortalamaAlisFiyati: Number(ortAlis.toFixed(2)),
            ortalamaSatisFiyati: Number(ortSatis.toFixed(2)),
            ortAlisFiyati: Number(ortAlis.toFixed(2)),
            ortSatisFiyati: Number(ortSatis.toFixed(2)),
            OrtalamaAlisFiyati: Number(ortAlis.toFixed(2)),
            OrtalamaSatisFiyati: Number(ortSatis.toFixed(2)),
            alisFiyati: stokObj.alisFiyati,
            satisFiyati: stokObj.satisFiyati,
            id: stokObj.id ?? (isNaN(Number(sid)) ? sid : parseInt(sid)) 
          });
        }
      }
    } catch (e) {
      console.warn('[transactionService] Orphan stock cleanup error:', e);
    }
  }

  const refId = cariHareket.refId || cariHareket.RefId;
  const evrakNo = cariHareket.evrakNo || cariHareket.EvrakNo;
  const cariHareketId = cariHareket.id ?? cariHareket.Id;
  if (!refId && !evrakNo && !cariHareketId) return false;

  const baseRefId = refId ? (refId.endsWith('-SUP') ? refId.substring(0, refId.length - 4) : refId) : '';

  try {
    const toKeyList = (raw: any) => {
      if (!raw) return [];
      if (Array.isArray(raw)) {
        return raw.map((item, idx) => item ? ({ ...item, firebaseKey: String(item?.id ?? idx) }) : null).filter(Boolean);
      }
      return Object.keys(raw).map((k) => ({ ...raw[k], firebaseKey: k }));
    };

    // Cari harekete bağlı kayıtları topla
    const cariH = (await readData('CariHareketler')) || {};
    const cariHList = toKeyList(cariH);
    const kasaH = (await readData('KasaHareketler')) || {};
    const kasaHList = toKeyList(kasaH);
    const bankaH = (await readData('BankaHareketler')) || {};
    const bankaHList = toKeyList(bankaH);
    const kk = (await readData('KrediKartlari')) || {};
    const kkList = toKeyList(kk);
    const eft = (await readData('EftIslemleri')) || {};
    const eftList = toKeyList(eft);

    const matchHareket = (h: any) => {
      const hRef = h.refId || h.RefId;
      if (refId && hRef && (hRef === baseRefId || hRef === refId + '-SUP' || hRef === refId)) return true;
      const hEvrak = String(h.evrakNo || h.EvrakNo || '').trim().toLowerCase();
      if (evrakNo && hEvrak && hEvrak === String(evrakNo).trim().toLowerCase()) return true;
      const hId = h.id ?? h.Id;
      if (cariHareketId && (String(hId) === String(cariHareketId) || (h.firebaseKey && String(h.firebaseKey) === String(cariHareketId)))) return true;
      if (cariHareket.firebaseKey && (h.firebaseKey === cariHareket.firebaseKey || String(hId) === String(cariHareket.firebaseKey))) return true;
      return false;
    };

    const supReads: string[] = [];
    const farkCariler: any[] = [];
    let revertFailed = false;

    for (const h of cariHList.filter(matchHareket)) {
      const k = h.firebaseKey || h.id;
      if (k && supReads.indexOf(String(k)) === -1) supReads.push(String(k));
      const cid = h.cariId ?? h.CariId;
      if (cid) {
        farkCariler.push({ cariId: cid, borc: parseFloat(h.borc ?? h.Borc) || 0, alacak: parseFloat(h.alacak ?? h.Alacak) || 0 });
      }
    }

    if (cariHareket.firebaseKey && supReads.indexOf(String(cariHareket.firebaseKey)) === -1) {
      supReads.push(String(cariHareket.firebaseKey));
    }
    if (cariHareket.id && supReads.indexOf(String(cariHareket.id)) === -1) {
      supReads.push(String(cariHareket.id));
    }

    // Fallback: If no match in list, use cariHareket directly
    if (farkCariler.length === 0 && (cariHareket.cariId || cariHareket.CariId)) {
      farkCariler.push({
        cariId: cariHareket.cariId || cariHareket.CariId,
        borc: parseFloat(cariHareket.borc ?? cariHareket.Borc) || 0,
        alacak: parseFloat(cariHareket.alacak ?? cariHareket.Alacak) || 0,
      });
    }

    // Cari bakiyeleri geri al (tüm farkları cari bazında toplayarak düşür)
    const totalFarkByCari: Record<string, { borc: number, alacak: number }> = {};
    for (const fc of farkCariler) {
      if (!fc.cariId) continue;
      const cid = String(fc.cariId);
      if (!totalFarkByCari[cid]) totalFarkByCari[cid] = { borc: 0, alacak: 0 };
      totalFarkByCari[cid].borc += (parseFloat(fc.borc) || 0);
      totalFarkByCari[cid].alacak += (parseFloat(fc.alacak) || 0);
    }

    for (const [cid, fark] of Object.entries(totalFarkByCari)) {
      const cariRef = await readData(`Cariler/${cid}`);
      if (cariRef) {
        const okRevert = await writeData(`Cariler/${cid}`, {
          ...cariRef,
          borc: Math.max(0, (parseFloat(cariRef.borc ?? cariRef.Borc) || 0) - fark.borc),
          alacak: Math.max(0, (parseFloat(cariRef.alacak ?? cariRef.Alacak) || 0) - fark.alacak),
        });
        if (!okRevert) revertFailed = true;
      }
    }

    // Kasa hareketlerini sil + bakiye geri
    for (const h of kasaHList.filter(matchHareket)) {
      if (h.kasaId !== undefined && h.kasaId !== null) {
        const kRef = await readData(`Bankalar/${h.kasaId}`);
        if (kRef) {
          const okRevert = await writeData(`Bankalar/${h.kasaId}`, { ...kRef, bakiye: (kRef.bakiye || 0) - ((h.giren || 0) - (h.cikan || 0)) });
          if (!okRevert) revertFailed = true;
        }
      }
      const kKey = h.firebaseKey || h.id;
      if (kKey) { try { await deleteData(`KasaHareketler/${kKey}`); } catch {} }
    }

    for (const h of bankaHList.filter(matchHareket)) {
      if (h.bankaId !== undefined && h.bankaId !== null) {
        const bRef = await readData(`Bankalar/${h.bankaId}`);
        if (bRef) {
          const okRevert = await writeData(`Bankalar/${h.bankaId}`, { ...bRef, bakiye: (bRef.bakiye || 0) - ((h.giren || 0) - (h.cikan || 0)) });
          if (!okRevert) revertFailed = true;
        }
      }
      const bKey = h.firebaseKey || h.id;
      if (bKey) { try { await deleteData(`BankaHareketler/${bKey}`); } catch {} }
    }

    // KK / EFT kayıtlarını sil
    for (const k of kkList.filter((x: any) => (baseRefId && x.onayKodu === baseRefId) || (evrakNo && x.evrakNo === evrakNo))) {
      const kkKey = k.firebaseKey || k.id;
      if (kkKey) { try { await deleteData(`KrediKartlari/${kkKey}`); } catch {} }
    }
    for (const e of eftList.filter((x: any) => (baseRefId && x.dekontNo === baseRefId) || (evrakNo && x.evrakNo === evrakNo))) {
      const eftKey = e.firebaseKey || e.id;
      if (eftKey) { try { await deleteData(`EftIslemleri/${eftKey}`); } catch {} }
    }

    // Cari hareketleri sil
    for (const key of supReads) {
      try { await deleteData(`CariHareketler/${key}`); } catch {}
    }
    if (revertFailed) {
      console.warn('[transactionService] Bakiye geri alma yazımları sıraya alındı (offline/kısmi).');
      return false;
    }
    return true;
  } catch (error) {
    console.error('[transactionService] Silme işlemi başarısız:', error);
    return false;
  }
};
