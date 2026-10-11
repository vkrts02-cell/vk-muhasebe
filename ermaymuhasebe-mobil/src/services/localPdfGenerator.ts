import * as Print from 'expo-print';
import * as Sharing from 'expo-sharing';
import { Alert } from 'react-native';
import { loadFirmaProfili, cleanBase64Logo } from './pdfService';

const formatMoney = (val: number | string | undefined | null): string => {
  const num = typeof val === 'string' ? parseFloat(val.replace(/[^0-9.-]+/g, '')) || 0 : (val || 0);
  return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' }).format(num);
};

export interface LocalPdfOptions {
  title: string;
  subtitle?: string;
  headers?: string[];
  rows?: string[][];
  documentType?: 'fatura' | 'teklif' | 'siparis' | 'rapor' | 'heatmap';
  faturaData?: any;
  teklifData?: any;
  siparisData?: any;
}

/**
 * Cihaz üzerinde yerel olarak (sunucuya ve 5244 portuna ihtiyaç duymadan)
 * profesyonel QuestPDF kalitesinde HTML -> PDF üreten motor.
 */
export const generateLocalPdfAndShare = async (options: LocalPdfOptions): Promise<{ success: boolean; uri?: string }> => {
  try {
    const profil = await loadFirmaProfili();
    const logoBase64 = cleanBase64Logo(profil?.logoBase64 || profil?.LogoBase64);
    const firmaAdi = profil?.firmaAdi || profil?.FirmaAdi || 'VK ÖN MUHASEBE';
    const adres = profil?.adres || profil?.Adres || '';
    const telefon = profil?.telefon || profil?.Telefon || '';
    const vergiDairesi = profil?.vergiDairesi || profil?.VergiDairesi || '';
    const vergiNo = profil?.vergiNo || profil?.VergiNo || '';

    let htmlContent = '';

    if (options.documentType === 'fatura' && options.faturaData) {
      htmlContent = buildFaturaHtml(options.faturaData, { firmaAdi, adres, telefon, vergiDairesi, vergiNo, logoBase64 });
    } else if (options.documentType === 'teklif' && options.teklifData) {
      htmlContent = buildTeklifHtml(options.teklifData, { firmaAdi, adres, telefon, vergiDairesi, vergiNo, logoBase64 });
    } else if (options.documentType === 'siparis' && options.siparisData) {
      htmlContent = buildSiparisHtml(options.siparisData, { firmaAdi, adres, telefon, vergiDairesi, vergiNo, logoBase64 });
    } else if (options.documentType === 'heatmap' || options.title?.includes('Isı Haritası')) {
      htmlContent = buildHeatmapHtml(options, { firmaAdi, adres, telefon, logoBase64 });
    } else {
      htmlContent = buildGenericReportHtml(options, { firmaAdi, adres, telefon, logoBase64 });
    }

    const { uri } = await Print.printToFileAsync({
      html: htmlContent,
      base64: false,
    });

    if (await Sharing.isAvailableAsync()) {
      await Sharing.shareAsync(uri, {
        mimeType: 'application/pdf',
        dialogTitle: `${options.title || 'Belge'} Paylaş`,
        UTI: 'com.adobe.pdf',
      });
      return { success: true, uri };
    } else {
      Alert.alert('Bilgi', 'Paylaşım servisi bu cihazda desteklenmiyor.');
      return { success: false, uri };
    }
  } catch (error: any) {
    console.error('[localPdfGenerator] PDF üretme hatası:', error);
    Alert.alert('PDF Hatası', 'Belge oluşturulurken bir sorun meydana geldi: ' + error.message);
    return { success: false };
  }
};

const getBaseStyles = (isLandscape = false) => `
  <style>
    @page { margin: 15mm; size: A4 ${isLandscape ? 'landscape' : 'portrait'}; }
    * { box-sizing: border-box; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif; }
    body { margin: 0; padding: 0; color: #1E293B; font-size: 11px; line-height: 1.5; background: #FFF; }
    .header { display: flex; justify-content: space-between; align-items: flex-start; padding-bottom: 16px; border-bottom: 2px solid #2563EB; margin-bottom: 20px; }
    .company-title { font-size: 18px; font-weight: 800; color: #1E3A8A; margin: 0 0 4px 0; }
    .company-info { font-size: 10px; color: #64748B; line-height: 1.4; }
    .logo { max-height: 60px; max-width: 140px; object-fit: contain; }
    .doc-badge { text-align: right; }
    .doc-title { font-size: 20px; font-weight: 900; color: #0F172A; letter-spacing: 0.5px; margin: 0; text-transform: uppercase; }
    .doc-meta { font-size: 10px; color: #64748B; margin-top: 4px; }
    .party-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 20px; }
    .party-card { background: #F8FAFC; border: 1px solid #E2E8F0; border-radius: 8px; padding: 12px; }
    .party-card-title { font-size: 10px; font-weight: 700; color: #2563EB; text-transform: uppercase; margin-bottom: 6px; letter-spacing: 0.5px; }
    .party-card-name { font-size: 13px; font-weight: 700; color: #0F172A; margin-bottom: 4px; }
    .party-card-details { font-size: 10px; color: #475569; }
    table { width: 100%; border-collapse: collapse; margin-bottom: 20px; font-size: 10px; }
    th { background: #1E3A8A; color: #FFF; font-weight: 700; text-align: left; padding: 8px 10px; font-size: 10px; text-transform: uppercase; letter-spacing: 0.5px; }
    th.right, td.right { text-align: right; }
    th.center, td.center { text-align: center; }
    td { padding: 8px 10px; border-bottom: 1px solid #E2E8F0; color: #1E293B; }
    tr:nth-child(even) td { background-color: #F8FAFC; }
    .totals-area { display: flex; justify-content: flex-end; margin-top: 10px; }
    .totals-box { width: 280px; background: #F8FAFC; border: 1px solid #CBD5E1; border-radius: 8px; padding: 12px; }
    .totals-row { display: flex; justify-content: space-between; padding: 4px 0; font-size: 11px; color: #475569; }
    .totals-row.grand-total { font-size: 14px; font-weight: 800; color: #1E3A8A; border-top: 2px solid #2563EB; padding-top: 8px; margin-top: 4px; }
    .footer { position: fixed; bottom: 0; left: 0; right: 0; border-top: 1px solid #E2E8F0; padding-top: 8px; display: flex; justify-content: space-between; font-size: 9px; color: #94A3B8; }
    .badge { display: inline-block; padding: 2px 8px; border-radius: 12px; font-weight: 700; font-size: 9px; }
    .badge-success { background: #DCFCE7; color: #15803D; }
    .badge-danger { background: #FEE2E2; color: #B91C1C; }
    .badge-info { background: #DBEAFE; color: #1D4ED8; }
    .kpi-cards { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; margin-bottom: 20px; }
    .kpi-card { background: #F8FAFC; border-left: 4px solid #2563EB; border-radius: 6px; padding: 10px 12px; }
    .kpi-title { font-size: 9px; text-transform: uppercase; color: #64748B; font-weight: 600; }
    .kpi-value { font-size: 14px; font-weight: 800; color: #0F172A; margin-top: 2px; }
  </style>
`;

const buildHeader = (meta: { firmaAdi: string; adres: string; telefon: string; logoBase64?: string | null }, title: string, subtitle?: string) => `
  <div class="header">
    <div style="display: flex; gap: 14px; align-items: center;">
      ${meta.logoBase64 ? `<img src="data:image/png;base64,${meta.logoBase64}" class="logo" />` : ''}
      <div>
        <div class="company-title">${meta.firmaAdi}</div>
        <div class="company-info">
          ${meta.adres ? `<div>${meta.adres}</div>` : ''}
          ${meta.telefon ? `<div>Tel: ${meta.telefon}</div>` : ''}
        </div>
      </div>
    </div>
    <div class="doc-badge">
      <h1 class="doc-title">${title}</h1>
      <div class="doc-meta">${subtitle || `Tarih: ${new Date().toLocaleDateString('tr-TR')} ${new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })}`}</div>
    </div>
  </div>
`;

const buildGenericReportHtml = (options: LocalPdfOptions, company: any) => {
  const headers = options.headers || [];
  const rows = options.rows || [];
  const titleLower = (options.title || '').toLowerCase();
  const isLandscape = headers.length >= 8 || 
                      titleLower.includes('detaylı gelir ve maliyet') || 
                      titleLower.includes('detayli gelir ve maliyet') ||
                      titleLower.includes('aylık tahsilat ve ödeme') ||
                      titleLower.includes('aylik tahsilat ve odeme');

  return `
    <!DOCTYPE html>
    <html>
      <head>
        <meta charset="utf-8">
        <title>${options.title}</title>
        ${getBaseStyles(isLandscape)}
      </head>
      <body>
        ${buildHeader(company, options.title, options.subtitle)}

        <div class="kpi-cards">
          <div class="kpi-card" style="border-color: #2563EB;">
            <div class="kpi-title">Toplam Kayıt</div>
            <div class="kpi-value">${rows.length} Satır</div>
          </div>
          <div class="kpi-card" style="border-color: #10B981;">
            <div class="kpi-title">Rapor Durumu</div>
            <div class="kpi-value" style="color: #10B981;">Tamamlandı</div>
          </div>
          <div class="kpi-card" style="border-color: #6366F1;">
            <div class="kpi-title">Oluşturulma Tarihi</div>
            <div class="kpi-value">${new Date().toLocaleDateString('tr-TR')}</div>
          </div>
        </div>

        <table>
          <thead>
            <tr>
              ${headers.map((h, i) => {
                const isRight = h.toLowerCase().includes('tutar') || h.toLowerCase().includes('borç') || h.toLowerCase().includes('alacak') || h.toLowerCase().includes('bakiye') || h.toLowerCase().includes('fiyat') || h.toLowerCase().includes('miktar');
                const isCenter = h.toLowerCase() === 'no' || h.toLowerCase().includes('tarih') || h.toLowerCase().includes('durum');
                return `<th class="${isRight ? 'right' : isCenter ? 'center' : ''}">${h}</th>`;
              }).join('')}
            </tr>
          </thead>
          <tbody>
            ${rows.map(row => `
              <tr>
                ${row.map((cell, idx) => {
                  const headerName = (headers[idx] || '').toLowerCase();
                  const isRight = headerName.includes('tutar') || headerName.includes('borç') || headerName.includes('alacak') || headerName.includes('bakiye') || headerName.includes('fiyat') || headerName.includes('miktar');
                  const isCenter = headerName === 'no' || headerName.includes('tarih') || headerName.includes('durum');
                  return `<td class="${isRight ? 'right' : isCenter ? 'center' : ''}">${cell ?? ''}</td>`;
                }).join('')}
              </tr>
            `).join('')}
          </tbody>
        </table>

        <div class="footer">
          <div>VK Ön Muhasebe Yönetim Sistemi</div>
          <div>Sayfa 1 / 1</div>
        </div>
      </body>
    </html>
  `;
};

const buildHeatmapHtml = (options: LocalPdfOptions, company: any) => {
  const rows = options.rows || [];

  return `
    <!DOCTYPE html>
    <html>
      <head>
        <meta charset="utf-8">
        <title>Finansal Isı Haritası</title>
        ${getBaseStyles()}
        <style>
          .heatmap-grid { display: grid; grid-template-columns: repeat(6, 1fr); gap: 8px; margin-bottom: 24px; }
          .heatmap-day { border-radius: 8px; padding: 8px; text-align: center; border: 1px solid #E2E8F0; }
          .heatmap-day.positive { background: #ECFDF5; border-color: #A7F3D0; }
          .heatmap-day.negative { background: #FEF2F2; border-color: #FECACA; }
          .day-date { font-size: 9px; font-weight: 700; color: #475569; }
          .day-flow { font-size: 11px; font-weight: 800; margin-top: 4px; }
          .day-flow.pos { color: #059669; }
          .day-flow.neg { color: #DC2626; }
        </style>
      </head>
      <body>
        ${buildHeader(company, 'Finansal Isı Haritası', 'Son 30 Günlük Finansal Aktivite ve Net Nakit Akış Analizi')}

        <div class="kpi-cards">
          <div class="kpi-card" style="border-color: #10B981;">
            <div class="kpi-title">Analiz Aralığı</div>
            <div class="kpi-value">Son 30 Gün</div>
          </div>
          <div class="kpi-card" style="border-color: #2563EB;">
            <div class="kpi-title">İşlem Günü Sayısı</div>
            <div class="kpi-value">${rows.length} Gün</div>
          </div>
          <div class="kpi-card" style="border-color: #8B5CF6;">
            <div class="kpi-title">Analiz Türü</div>
            <div class="kpi-value">Net Akış Matrisi</div>
          </div>
        </div>

        <h3 style="font-size: 13px; color: #1E3A8A; margin: 0 0 10px 0;">30 Günlük Yoğunluk Tablosu</h3>
        <table>
          <thead>
            <tr>
              <th>Tarih</th>
              <th class="right">Satışlar</th>
              <th class="right">Alışlar</th>
              <th class="right">Kasa Giren</th>
              <th class="right">Kasa Çıkan</th>
              <th class="right">Net Akış</th>
            </tr>
          </thead>
          <tbody>
            ${rows.map(r => {
              const netText = r[5] || '0';
              const isNeg = netText.includes('-');
              return `
                <tr>
                  <td><strong>${r[0] || ''}</strong></td>
                  <td class="right">${r[1] || '0'}</td>
                  <td class="right">${r[2] || '0'}</td>
                  <td class="right" style="color: #059669;">${r[3] || '0'}</td>
                  <td class="right" style="color: #DC2626;">${r[4] || '0'}</td>
                  <td class="right" style="font-weight: 800; color: ${isNeg ? '#DC2626' : '#059669'};">
                    ${netText}
                  </td>
                </tr>
              `;
            }).join('')}
          </tbody>
        </table>

        <div class="footer">
          <div>VK Ön Muhasebe • Finansal Isı Haritası Motoru</div>
          <div>Rapor Tarihi: ${new Date().toLocaleDateString('tr-TR')}</div>
        </div>
      </body>
    </html>
  `;
};

const buildFaturaHtml = (data: any, company: any) => {
  const fatura = data.fatura || data;
  const detaylar = data.detaylar || fatura.detaylar || [];
  const cari = data.cari || fatura.cari || {};

  return `
    <!DOCTYPE html>
    <html>
      <head>
        <meta charset="utf-8">
        <title>Fatura - ${fatura.faturaNo || ''}</title>
        ${getBaseStyles()}
      </head>
      <body>
        ${buildHeader(company, `${fatura.tur || 'SATIŞ'} FATURASI`, `Fatura No: ${fatura.faturaNo || ''}`)}

        <div class="party-grid">
          <div class="party-card">
            <div class="party-card-title">Düzenleyen Firma</div>
            <div class="party-card-name">${company.firmaAdi}</div>
            <div class="party-card-details">
              ${company.adres ? `<div>${company.adres}</div>` : ''}
              ${company.telefon ? `<div>Tel: ${company.telefon}</div>` : ''}
              ${company.vergiDairesi ? `<div>V.D.: ${company.vergiDairesi} - No: ${company.vergiNo}</div>` : ''}
            </div>
          </div>
          <div class="party-card">
            <div class="party-card-title">Müşteri Bilgileri</div>
            <div class="party-card-name">${cari.unvan || fatura.cariUnvan || 'Sayın Müşteri'}</div>
            <div class="party-card-details">
              ${cari.adres || fatura.adres ? `<div>${cari.adres || fatura.adres}</div>` : ''}
              ${cari.telefon || fatura.telefon ? `<div>Tel: ${cari.telefon || fatura.telefon}</div>` : ''}
              ${cari.vergiDairesi ? `<div>V.D.: ${cari.vergiDairesi} - No: ${cari.vergiNo}</div>` : ''}
              <div><strong>Düzenleme Tarihi:</strong> ${fatura.tarih ? new Date(fatura.tarih).toLocaleDateString('tr-TR') : ''}</div>
              ${fatura.vadeTarihi ? `<div><strong>Vade Tarihi:</strong> ${new Date(fatura.vadeTarihi).toLocaleDateString('tr-TR')}</div>` : ''}
            </div>
          </div>
        </div>

        <table>
          <thead>
            <tr>
              <th style="width: 30px;" class="center">#</th>
              <th>Ürün / Hizmet Açıklaması</th>
              <th class="center" style="width: 60px;">Miktar</th>
              <th class="center" style="width: 50px;">Birim</th>
              <th class="right" style="width: 80px;">Birim Fiyat</th>
              <th class="center" style="width: 50px;">KDV</th>
              <th class="right" style="width: 90px;">Tutar</th>
            </tr>
          </thead>
          <tbody>
            ${detaylar.map((d: any, idx: number) => `
              <tr>
                <td class="center">${idx + 1}</td>
                <td><strong>${d.stokAdi || d.aciklama || ''}</strong></td>
                <td class="center">${d.miktar || 1}</td>
                <td class="center">${d.birim || 'Adet'}</td>
                <td class="right">${formatMoney(d.birimFiyat)}</td>
                <td class="center">%${d.kdvOrani || d.kdv || 20}</td>
                <td class="right" style="font-weight: 700;">${formatMoney(d.toplamTutar || (d.miktar * d.birimFiyat))}</td>
              </tr>
            `).join('')}
          </tbody>
        </table>

        <div class="totals-area">
          <div class="totals-box">
            <div class="totals-row">
              <span>Ara Toplam</span>
              <span>${formatMoney(fatura.araToplam)}</span>
            </div>
            ${(fatura.iskontoToplami || 0) > 0 ? `
              <div class="totals-row" style="color: #DC2626;">
                <span>İskonto</span>
                <span>-${formatMoney(fatura.iskontoToplami)}</span>
              </div>
            ` : ''}
            <div class="totals-row">
              <span>Hesaplanan KDV</span>
              <span>${formatMoney(fatura.kdvToplam || fatura.kdv)}</span>
            </div>
            <div class="totals-row grand-total">
              <span>GENEL TOPLAM</span>
              <span>${formatMoney(fatura.genelToplam)}</span>
            </div>
          </div>
        </div>

        <div class="footer">
          <div>VK Ön Muhasebe Belge Yönetimi</div>
          <div>Fatura No: ${fatura.faturaNo || ''} • ${new Date().toLocaleDateString('tr-TR')}</div>
        </div>
      </body>
    </html>
  `;
};

const buildTeklifHtml = (data: any, company: any) => {
  const teklif = data.teklif || data;
  const detaylar = data.detaylar || teklif.detaylar || [];
  const cari = data.cari || teklif.cari || {};

  return `
    <!DOCTYPE html>
    <html>
      <head>
        <meta charset="utf-8">
        <title>Fiyat Teklifi - ${teklif.teklifNo || ''}</title>
        ${getBaseStyles()}
      </head>
      <body>
        ${buildHeader(company, 'FİYAT TEKLİFİ', `Teklif No: ${teklif.teklifNo || ''}`)}

        <div class="party-grid">
          <div class="party-card">
            <div class="party-card-title">Teklifi Veren</div>
            <div class="party-card-name">${company.firmaAdi}</div>
            <div class="party-card-details">
              ${company.adres ? `<div>${company.adres}</div>` : ''}
              ${company.telefon ? `<div>Tel: ${company.telefon}</div>` : ''}
            </div>
          </div>
          <div class="party-card">
            <div class="party-card-title">Teklif Sunulan Müşteri</div>
            <div class="party-card-name">${cari.unvan || teklif.cariUnvan || 'Sayın Yetkili'}</div>
            <div class="party-card-details">
              ${cari.adres ? `<div>${cari.adres}</div>` : ''}
              ${cari.telefon ? `<div>Tel: ${cari.telefon}</div>` : ''}
              <div><strong>Teklif Tarihi:</strong> ${teklif.tarih ? new Date(teklif.tarih).toLocaleDateString('tr-TR') : ''}</div>
              ${teklif.gecerlilikTarihi ? `<div><strong>Geçerlilik Tarihi:</strong> ${new Date(teklif.gecerlilikTarihi).toLocaleDateString('tr-TR')}</div>` : ''}
            </div>
          </div>
        </div>

        <table>
          <thead>
            <tr>
              <th style="width: 30px;" class="center">#</th>
              <th>Ürün / Hizmet Kalemi</th>
              <th class="center" style="width: 60px;">Miktar</th>
              <th class="center" style="width: 50px;">Birim</th>
              <th class="right" style="width: 80px;">Birim Fiyat</th>
              <th class="right" style="width: 90px;">Tutar</th>
            </tr>
          </thead>
          <tbody>
            ${detaylar.map((d: any, idx: number) => `
              <tr>
                <td class="center">${idx + 1}</td>
                <td><strong>${d.stokAdi || d.aciklama || ''}</strong></td>
                <td class="center">${d.miktar || 1}</td>
                <td class="center">${d.birim || 'Adet'}</td>
                <td class="right">${formatMoney(d.birimFiyat)}</td>
                <td class="right" style="font-weight: 700;">${formatMoney(d.toplamTutar || (d.miktar * d.birimFiyat))}</td>
              </tr>
            `).join('')}
          </tbody>
        </table>

        <div class="totals-area">
          <div class="totals-box">
            <div class="totals-row">
              <span>Ara Toplam</span>
              <span>${formatMoney(teklif.araToplam)}</span>
            </div>
            <div class="totals-row">
              <span>KDV Toplamı</span>
              <span>${formatMoney(teklif.kdvToplam)}</span>
            </div>
            <div class="totals-row grand-total">
              <span>GENEL TOPLAM</span>
              <span>${formatMoney(teklif.genelToplam)}</span>
            </div>
          </div>
        </div>

        <div class="footer">
          <div>VK Ön Muhasebe • Teklif Belgesi</div>
          <div>Geçerlilik: ${teklif.gecerlilikTarihi ? new Date(teklif.gecerlilikTarihi).toLocaleDateString('tr-TR') : '15 Gün'}</div>
        </div>
      </body>
    </html>
  `;
};

const buildSiparisHtml = (data: any, company: any) => {
  const siparis = data.siparis || data;
  const detaylar = data.detaylar || siparis.detaylar || [];
  const cari = data.cari || siparis.cari || {};

  return `
    <!DOCTYPE html>
    <html>
      <head>
        <meta charset="utf-8">
        <title>Sipariş Formu - ${siparis.siparisNo || ''}</title>
        ${getBaseStyles()}
      </head>
      <body>
        ${buildHeader(company, `${siparis.tur || 'MÜŞTERİ'} SİPARİŞİ`, `Sipariş No: ${siparis.siparisNo || ''}`)}

        <div class="party-grid">
          <div class="party-card">
            <div class="party-card-title">Firma Bilgileri</div>
            <div class="party-card-name">${company.firmaAdi}</div>
            <div class="party-card-details">
              ${company.adres ? `<div>${company.adres}</div>` : ''}
              ${company.telefon ? `<div>Tel: ${company.telefon}</div>` : ''}
            </div>
          </div>
          <div class="party-card">
            <div class="party-card-title">Sipariş Veren</div>
            <div class="party-card-name">${cari.unvan || siparis.cariUnvan || 'Sayın Müşteri'}</div>
            <div class="party-card-details">
              ${cari.adres ? `<div>${cari.adres}</div>` : ''}
              ${cari.telefon ? `<div>Tel: ${cari.telefon}</div>` : ''}
              <div><strong>Sipariş Tarihi:</strong> ${siparis.tarih ? new Date(siparis.tarih).toLocaleDateString('tr-TR') : ''}</div>
              ${siparis.teslimTarihi ? `<div><strong>Teslim Tarihi:</strong> ${new Date(siparis.teslimTarihi).toLocaleDateString('tr-TR')}</div>` : ''}
            </div>
          </div>
        </div>

        <table>
          <thead>
            <tr>
              <th style="width: 30px;" class="center">#</th>
              <th>Ürün Açıklaması</th>
              <th class="center" style="width: 60px;">Miktar</th>
              <th class="center" style="width: 50px;">Birim</th>
              <th class="right" style="width: 80px;">Birim Fiyat</th>
              <th class="right" style="width: 90px;">Tutar</th>
            </tr>
          </thead>
          <tbody>
            ${detaylar.map((d: any, idx: number) => `
              <tr>
                <td class="center">${idx + 1}</td>
                <td><strong>${d.stokAdi || d.aciklama || ''}</strong></td>
                <td class="center">${d.miktar || 1}</td>
                <td class="center">${d.birim || 'Adet'}</td>
                <td class="right">${formatMoney(d.birimFiyat)}</td>
                <td class="right" style="font-weight: 700;">${formatMoney(d.toplamTutar || (d.miktar * d.birimFiyat))}</td>
              </tr>
            `).join('')}
          </tbody>
        </table>

        <div class="totals-area">
          <div class="totals-box">
            <div class="totals-row">
              <span>Ara Toplam</span>
              <span>${formatMoney(siparis.araToplam)}</span>
            </div>
            <div class="totals-row">
              <span>KDV Toplamı</span>
              <span>${formatMoney(siparis.kdvToplam)}</span>
            </div>
            <div class="totals-row grand-total">
              <span>GENEL TOPLAM</span>
              <span>${formatMoney(siparis.genelToplam)}</span>
            </div>
          </div>
        </div>

        <div class="footer">
          <div>VK Ön Muhasebe • Sipariş Onay Belgesi</div>
          <div>Teslimat: ${siparis.teslimTarihi ? new Date(siparis.teslimTarihi).toLocaleDateString('tr-TR') : 'Belirtilmedi'}</div>
        </div>
      </body>
    </html>
  `;
};
