import React, { useState, useEffect, useMemo } from 'react';
import {
  StyleSheet,
  Text,
  View,
  SafeAreaView,
  ScrollView,
  TextInput,
  TouchableOpacity,
  Alert,
  Image,
  Modal,
  ActivityIndicator,
  StatusBar,
  Platform,
  Dimensions,
} from 'react-native';
import {
  ArrowLeft,
  MessageSquare,
  Tag,
  Image as ImageIcon,
  FileText,
  Phone,
  User,
  Calendar,
  Plus,
  Trash2,
  Share2,
  FileDown,
  Camera,
  UploadCloud,
  X,
  Check,
  Save,
  Palette,
  ExternalLink,
} from 'lucide-react-native';
import * as ImagePicker from 'expo-image-picker';
import * as Print from 'expo-print';
import * as Sharing from 'expo-sharing';
import { subscribeToPath, writeData, deleteData } from '../services/firebase';
import { triggerSelectionHaptic } from '../services/hapticsService';
import { generateInt32Id } from '../utils/IdGenerator';
import { MusteriTakipKlasor, MusteriTakipDetay } from './MusteriTakipScreen';
import {
  KeyboardDoneAccessory,
  KEYBOARD_ACCESSORY_ID,
} from '../components/KeyboardDoneAccessory';

const { width: SCREEN_WIDTH } = Dimensions.get('window');
const ETIKETLER = ['Sıcak Müşteri', 'Teklif Aşamasında', 'Önemli', 'Yeni İletişim', 'Takipte'];
const RENKLER = ['#0A84FF', '#30D158', '#BF5AF2', '#FF9F0A', '#FF453A', '#64D2FF', '#FF375F'];
const PARA_BIRIMLERI = ['₺', '$', '€'];

export default function MusteriTakipDetayScreen({ route, navigation }: any) {
  const initialKlasor: MusteriTakipKlasor = route.params?.klasor;

  const [klasor, setKlasor] = useState<MusteriTakipKlasor>(initialKlasor);
  const [detaylar, setDetaylar] = useState<MusteriTakipDetay[]>([]);
  const [activeTab, setActiveTab] = useState<'Gorusmeler' | 'Fiyatlar' | 'Gorseller' | 'Notlar'>('Gorusmeler');
  const [loading, setLoading] = useState(true);
  const [isPdfGenerating, setIsPdfGenerating] = useState(false);

  // Klasör düzenleme state'i
  const [klasorEtiket, setKlasorEtiket] = useState(initialKlasor?.etiket || 'Yeni İletişim');
  const [klasorRenk, setKlasorRenk] = useState(initialKlasor?.renk || '#0A84FF');
  const [isEtiketModalOpen, setIsEtiketModalOpen] = useState(false);

  // Görüşme Formu State
  const [gorusmeBaslik, setGorusmeBaslik] = useState('');
  const [gorusmeIcerik, setGorusmeIcerik] = useState('');
  const [gorusmeTarih, setGorusmeTarih] = useState(new Date().toISOString().split('T')[0]);

  // Fiyat Formu State
  const [fiyatBaslik, setFiyatBaslik] = useState('');
  const [fiyatTutar, setFiyatTutar] = useState('');
  const [fiyatParaBirimi, setFiyatParaBirimi] = useState('₺');
  const [fiyatAciklama, setFiyatAciklama] = useState('');
  const [fiyatTarih, setFiyatTarih] = useState(new Date().toISOString().split('T')[0]);

  // Görsel Formu State
  const [gorselBaslik, setGorselBaslik] = useState('');
  const [selectedImageBase64, setSelectedImageBase64] = useState<string | null>(null);
  const [selectedImageUri, setSelectedImageUri] = useState<string | null>(null);

  // Not Formu State
  const [notBaslik, setNotBaslik] = useState('');
  const [notIcerik, setNotIcerik] = useState('');
  const [notTarih, setNotTarih] = useState(new Date().toISOString().split('T')[0]);

  const getTodayStr = () => new Date().toISOString().split('T')[0];
  const getYesterdayStr = () => {
    const d = new Date();
    d.setDate(d.getDate() - 1);
    return d.toISOString().split('T')[0];
  };

  // Tam ekran görsel önizleme modalı
  const [previewImage, setPreviewImage] = useState<string | null>(null);

  useEffect(() => {
    if (!initialKlasor?.id) return;

    // Klasör güncellemelerini dinle
    const unsubKlasor = subscribeToPath(`MusteriTakipKlasorler/${initialKlasor.id}`, (data) => {
      if (data && typeof data === 'object') {
        setKlasor((prev) => ({ ...prev, ...data }));
        if (data.etiket) setKlasorEtiket(data.etiket);
        if (data.renk) setKlasorRenk(data.renk);
      }
    });

    // Detayları dinle
    const unsubDetaylar = subscribeToPath('MusteriTakipDetaylar', (data) => {
      if (!data) {
        setDetaylar([]);
      } else {
        const list: MusteriTakipDetay[] = Array.isArray(data)
          ? data.filter(Boolean)
          : Object.keys(data).map((k) => ({ ...data[k], id: Number(data[k].id || k) }));
        const filtered = list.filter(
          (d) => Number(d.klasorId) === Number(initialKlasor.id) && !d.isDeleted
        );
        // Tarihe göre yeniden eskiye sırala
        filtered.sort((a, b) => new Date(b.tarih).getTime() - new Date(a.tarih).getTime());
        setDetaylar(filtered);
      }
      setLoading(false);
    });

    return () => {
      unsubKlasor();
      unsubDetaylar();
    };
  }, [initialKlasor?.id]);

  // Tab bazlı listeler
  const gorusmeler = useMemo(() => detaylar.filter((d) => d.tip === 'Gorusme'), [detaylar]);
  const fiyatlar = useMemo(() => detaylar.filter((d) => d.tip === 'Fiyat'), [detaylar]);
  const gorseller = useMemo(() => detaylar.filter((d) => d.tip === 'Gorsel'), [detaylar]);
  const notlar = useMemo(() => detaylar.filter((d) => d.tip === 'Not'), [detaylar]);

  // Klasör Bilgilerini Kaydet (Etiket / Renk)
  const handleSaveKlasorInfo = async () => {
    triggerSelectionHaptic();
    try {
      const now = new Date().toISOString();
      await writeData(`MusteriTakipKlasorler/${klasor.id}/etiket`, klasorEtiket);
      await writeData(`MusteriTakipKlasorler/${klasor.id}/renk`, klasorRenk);
      await writeData(`MusteriTakipKlasorler/${klasor.id}/sonIslemTarihi`, now);
      setIsEtiketModalOpen(false);
      Alert.alert('Başarılı', 'Klasör bilgileri güncellendi.');
    } catch (e) {
      Alert.alert('Hata', 'Klasör güncellenemedi.');
    }
  };

  // --- GÖRÜŞME EKLEME ---
  const handleAddGorusme = async () => {
    if (!gorusmeBaslik.trim() && !gorusmeIcerik.trim()) {
      Alert.alert('Uyarı', 'Lütfen görüşme konusu veya detay giriniz.');
      return;
    }
    triggerSelectionHaptic();

    const newId = generateInt32Id();
    const finalTarih = gorusmeTarih ? new Date(gorusmeTarih).toISOString() : new Date().toISOString();
    const newDetay: MusteriTakipDetay = {
      id: newId,
      tenantId: 'default',
      klasorId: klasor.id,
      cariId: klasor.cariId,
      tip: 'Gorusme',
      baslik: gorusmeBaslik.trim() || 'Müşteri Görüşmesi',
      icerik: gorusmeIcerik.trim(),
      tarih: finalTarih,
      isDeleted: false,
    };

    try {
      await writeData(`MusteriTakipDetaylar/${newId}`, newDetay);
      await writeData(`MusteriTakipKlasorler/${klasor.id}/sonIslemTarihi`, finalTarih);
      setGorusmeBaslik('');
      setGorusmeIcerik('');
      setGorusmeTarih(getTodayStr());
    } catch (e) {
      Alert.alert('Hata', 'Görüşme eklenemedi.');
    }
  };

  // --- FİYAT EKLEME ---
  const handleAddFiyat = async () => {
    if (!fiyatBaslik.trim() && !fiyatTutar.trim()) {
      Alert.alert('Uyarı', 'Lütfen teklif başlığı ve tutar giriniz.');
      return;
    }
    triggerSelectionHaptic();

    const cleanTutar = parseFloat(fiyatTutar.replace(',', '.'));
    const newId = generateInt32Id();
    const finalTarih = fiyatTarih ? new Date(fiyatTarih).toISOString() : new Date().toISOString();
    const newDetay: MusteriTakipDetay = {
      id: newId,
      tenantId: 'default',
      klasorId: klasor.id,
      cariId: klasor.cariId,
      tip: 'Fiyat',
      baslik: fiyatBaslik.trim() || 'Fiyat Teklifi',
      fiyatBilgisi: isNaN(cleanTutar) ? 0 : cleanTutar,
      paraBirimi: fiyatParaBirimi,
      icerik: fiyatAciklama.trim(),
      tarih: finalTarih,
      isDeleted: false,
    };

    try {
      await writeData(`MusteriTakipDetaylar/${newId}`, newDetay);
      await writeData(`MusteriTakipKlasorler/${klasor.id}/sonIslemTarihi`, finalTarih);
      setFiyatBaslik('');
      setFiyatTutar('');
      setFiyatAciklama('');
      setFiyatTarih(getTodayStr());
    } catch (e) {
      Alert.alert('Hata', 'Fiyat teklifi eklenemedi.');
    }
  };

  // --- GÖRSEL / EVRAK SEÇME & EKLEME ---
  const handlePickImage = async (useCamera: boolean) => {
    triggerSelectionHaptic();
    try {
      const options: ImagePicker.ImagePickerOptions = {
        mediaTypes: ImagePicker.MediaTypeOptions.Images,
        allowsEditing: true,
        quality: 0.7,
        base64: true,
      };

      const result = useCamera
        ? await ImagePicker.launchCameraAsync(options)
        : await ImagePicker.launchImageLibraryAsync(options);

      if (!result.canceled && result.assets && result.assets.length > 0) {
        const asset = result.assets[0];
        setSelectedImageUri(asset.uri);
        setSelectedImageBase64(asset.base64 || null);
      }
    } catch (e) {
      Alert.alert('Hata', 'Görsel seçilirken bir sorun oluştu.');
    }
  };

  const handleAddGorsel = async () => {
    if (!selectedImageBase64 && !selectedImageUri) {
      Alert.alert('Uyarı', 'Lütfen önce fotoğraf çekin veya galeriden seçin.');
      return;
    }
    triggerSelectionHaptic();

    const newId = generateInt32Id();
    const now = new Date().toISOString();
    const newDetay: MusteriTakipDetay = {
      id: newId,
      tenantId: 'default',
      klasorId: klasor.id,
      cariId: klasor.cariId,
      tip: 'Gorsel',
      baslik: gorselBaslik.trim() || 'Evrak / Fotoğraf',
      gorselBase64: selectedImageBase64 || undefined,
      dosyaYolu: selectedImageUri || undefined,
      tarih: now,
      isDeleted: false,
    };

    try {
      await writeData(`MusteriTakipDetaylar/${newId}`, newDetay);
      await writeData(`MusteriTakipKlasorler/${klasor.id}/sonIslemTarihi`, now);
      setGorselBaslik('');
      setSelectedImageBase64(null);
      setSelectedImageUri(null);
    } catch (e) {
      Alert.alert('Hata', 'Görsel kaydedilemedi.');
    }
  };

  // --- NOT EKLEME ---
  const handleAddNot = async () => {
    if (!notBaslik.trim() && !notIcerik.trim()) {
      Alert.alert('Uyarı', 'Lütfen not başlığı veya içerik giriniz.');
      return;
    }
    triggerSelectionHaptic();

    const newId = generateInt32Id();
    const finalTarih = notTarih ? new Date(notTarih).toISOString() : new Date().toISOString();
    const newDetay: MusteriTakipDetay = {
      id: newId,
      tenantId: 'default',
      klasorId: klasor.id,
      cariId: klasor.cariId,
      tip: 'Not',
      baslik: notBaslik.trim() || 'Genel Not',
      icerik: notIcerik.trim(),
      tarih: finalTarih,
      isDeleted: false,
    };

    try {
      await writeData(`MusteriTakipDetaylar/${newId}`, newDetay);
      await writeData(`MusteriTakipKlasorler/${klasor.id}/sonIslemTarihi`, finalTarih);
      setNotBaslik('');
      setNotIcerik('');
      setNotTarih(getTodayStr());
    } catch (e) {
      Alert.alert('Hata', 'Not kaydedilemedi.');
    }
  };

  // --- KAYIT SİLME ---
  const handleDeleteDetay = (detay: MusteriTakipDetay) => {
    triggerSelectionHaptic();
    Alert.alert(
      'Kaydı Sil',
      `"${detay.baslik}" başlıklı kaydı silmek istediğinize emin misiniz?`,
      [
        { text: 'Vazgeç', style: 'cancel' },
        {
          text: 'Sil',
          style: 'destructive',
          onPress: async () => {
            try {
              await writeData(`MusteriTakipDetaylar/${detay.id}/isDeleted`, true);
            } catch (e) {
              Alert.alert('Hata', 'Silme işlemi yapılamadı.');
            }
          },
        },
      ]
    );
  };

  // --- PDF RAPORU OLUŞTURMA & PAYLAŞMA ---
  const handleExportPdf = async () => {
    triggerSelectionHaptic();
    setIsPdfGenerating(true);

    try {
      const nowFormatted = formatDate(new Date().toISOString());

      // HTML Şablonu (Masaüstü QuestPDF çıktısıyla birebir tasarım)
      const html = `
        <!DOCTYPE html>
        <html>
        <head>
          <meta charset="utf-8">
          <title>Müşteri Takip Raporu - ${klasor.cariUnvan}</title>
          <style>
            body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; margin: 30px; color: #1E293B; }
            .header { border-bottom: 2px solid #2563EB; padding-bottom: 12px; margin-bottom: 20px; }
            .title { font-size: 22px; font-weight: bold; color: #1E3A8A; margin: 0; }
            .unvan { font-size: 16px; font-weight: bold; color: #0F172A; margin-top: 6px; }
            .meta { font-size: 12px; color: #475569; margin-top: 4px; }
            .right-meta { float: right; text-align: right; font-size: 12px; color: #64748B; }
            .badge { display: inline-block; background: #DBEAFE; color: #1E40AF; padding: 3px 8px; border-radius: 6px; font-weight: bold; font-size: 12px; }
            .section-title { font-size: 14px; font-weight: bold; color: #1E40AF; margin-top: 25px; margin-bottom: 8px; border-bottom: 1px solid #E2E8F0; padding-bottom: 4px; text-transform: uppercase; }
            table { width: 100%; border-collapse: collapse; margin-top: 6px; font-size: 12px; }
            th { background: #F1F5F9; border-bottom: 1px solid #CBD5E1; padding: 8px; text-align: left; font-weight: 600; color: #334155; }
            td { padding: 8px; border-bottom: 1px solid #F1F5F9; vertical-align: top; }
            .empty { color: #94A3B8; font-style: italic; padding: 8px 0; font-size: 12px; }
            .price-tag { color: #059669; font-weight: bold; }
            .gallery { display: flex; flex-wrap: wrap; gap: 15px; margin-top: 10px; }
            .gallery-item { width: 220px; border: 1px solid #E2E8F0; border-radius: 8px; overflow: hidden; }
            .gallery-img { width: 100%; height: 160px; object-fit: cover; }
            .gallery-title { padding: 6px; font-size: 11px; font-weight: bold; background: #F8FAFC; color: #1E293B; }
            .footer { margin-top: 40px; border-top: 1px solid #E2E8F0; padding-top: 10px; text-align: center; font-size: 10px; color: #94A3B8; }
          </style>
        </head>
        <body>
          <div class="header">
            <div class="right-meta">
              <div>Rapor Tarihi: ${nowFormatted}</div>
              <div style="margin-top: 4px;">Durum: <span class="badge">${klasorEtiket}</span></div>
            </div>
            <div class="title">MÜŞTERİ TAKİP &amp; GÖRÜŞME RAPORU</div>
            <div class="unvan">Müşteri / Ünvan: ${klasor.cariUnvan}</div>
            <div class="meta">
              ${klasor.yetkili ? `Yetkili: ${klasor.yetkili} &nbsp;|&nbsp; ` : ''}
              ${klasor.telefon ? `Telefon: ${klasor.telefon} &nbsp;|&nbsp; ` : ''}
              ${klasor.cariKod ? `Cari Kod: ${klasor.cariKod}` : ''}
            </div>
          </div>

          <!-- 1. GÖRÜŞME NOTLARI -->
          <div class="section-title">1. Görüşme ve Toplantı Notları</div>
          ${
            gorusmeler.length === 0
              ? '<div class="empty">Kayıtlı görüşme bulunmamaktadır.</div>'
              : `
            <table>
              <thead>
                <tr>
                  <th style="width: 130px;">Tarih</th>
                  <th style="width: 200px;">Görüşme Konusu</th>
                  <th>Detay ve Anlaşmalar</th>
                </tr>
              </thead>
              <tbody>
                ${gorusmeler
                  .map(
                    (g) => `
                  <tr>
                    <td>${formatDate(g.tarih)}</td>
                    <td><strong>${g.baslik || '-'}</strong></td>
                    <td>${g.icerik || '-'}</td>
                  </tr>
                `
                  )
                  .join('')}
              </tbody>
            </table>
          `
          }

          <!-- 2. VERİLEN FİYAT TEKLİFLERİ -->
          <div class="section-title">2. Verilen Fiyat Teklifleri</div>
          ${
            fiyatlar.length === 0
              ? '<div class="empty">Kayıtlı fiyat teklifi bulunmamaktadır.</div>'
              : `
            <table>
              <thead>
                <tr>
                  <th style="width: 130px;">Tarih</th>
                  <th style="width: 200px;">Teklif / Ürün Başlığı</th>
                  <th style="width: 140px;">Verilen Fiyat</th>
                  <th>Açıklama</th>
                </tr>
              </thead>
              <tbody>
                ${fiyatlar
                  .map(
                    (f) => `
                  <tr>
                    <td>${formatDate(f.tarih)}</td>
                    <td><strong>${f.baslik || '-'}</strong></td>
                    <td class="price-tag">${(f.fiyatBilgisi || 0).toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ${f.paraBirimi || '₺'}</td>
                    <td>${f.icerik || '-'}</td>
                  </tr>
                `
                  )
                  .join('')}
              </tbody>
            </table>
          `
          }

          <!-- 3. YÜKLENEN GÖRSELLER & EVRAKLAR -->
          <div class="section-title">3. Yüklenen Görseller ve Evraklar</div>
          ${
            gorseller.length === 0
              ? '<div class="empty">Kayıtlı görsel veya evrak bulunmamaktadır.</div>'
              : `
            <div class="gallery">
              ${gorseller
                .map(
                  (img) => `
                <div class="gallery-item">
                  ${
                    img.gorselBase64
                      ? `<img class="gallery-img" src="data:image/jpeg;base64,${img.gorselBase64}" />`
                      : `<div style="height: 120px; background: #F1F5F9; display: flex; align-items: center; justify-content: center; color: #94A3B8;">Önizleme Yok</div>`
                  }
                  <div class="gallery-title">${img.baslik || 'Görsel'} - ${formatDate(img.tarih)}</div>
                </div>
              `
                )
                .join('')}
            </div>
          `
          }

          <!-- 4. ÖZEL NOTLAR -->
          <div class="section-title">4. Özel Notlar</div>
          ${
            notlar.length === 0
              ? '<div class="empty">Kayıtlı not bulunmamaktadır.</div>'
              : `
            <table>
              <thead>
                <tr>
                  <th style="width: 130px;">Tarih</th>
                  <th style="width: 200px;">Not Başlığı</th>
                  <th>Not İçeriği</th>
                </tr>
              </thead>
              <tbody>
                ${notlar
                  .map(
                    (n) => `
                  <tr>
                    <td>${formatDate(n.tarih)}</td>
                    <td><strong>${n.baslik || '-'}</strong></td>
                    <td>${n.icerik || '-'}</td>
                  </tr>
                `
                  )
                  .join('')}
              </tbody>
            </table>
          `
          }

          <div class="footer">
            Ermay Muhasebe &copy; ${new Date().getFullYear()} - Bu rapor sistem tarafından otomatik olarak oluşturulmuştur.
          </div>
        </body>
        </html>
      `;

      const { uri } = await Print.printToFileAsync({ html });
      if (await Sharing.isAvailableAsync()) {
        await Sharing.shareAsync(uri, {
          mimeType: 'application/pdf',
          dialogTitle: `Müşteri Takip Raporu - ${klasor.cariUnvan}`,
          UTI: 'com.adobe.pdf',
        });
      } else {
        Alert.alert('Başarılı', 'PDF oluşturuldu: ' + uri);
      }
    } catch (e) {
      Alert.alert('Hata', 'PDF raporu oluşturulurken bir sorun çıktı: ' + (e as any)?.message);
    } finally {
      setIsPdfGenerating(false);
    }
  };

  const formatDate = (isoStr: string) => {
    if (!isoStr) return '-';
    try {
      const d = new Date(isoStr);
      if (isNaN(d.getTime())) return '-';
      const day = String(d.getDate()).padStart(2, '0');
      const month = String(d.getMonth() + 1).padStart(2, '0');
      const year = d.getFullYear();
      const hours = String(d.getHours()).padStart(2, '0');
      const mins = String(d.getMinutes()).padStart(2, '0');
      return `${day}.${month}.${year} ${hours}:${mins}`;
    } catch {
      return '-';
    }
  };

  const tabs = [
    {
      key: 'Gorusmeler' as const,
      title: 'Görüşmeler',
      count: gorusmeler.length,
      icon: MessageSquare,
      color: '#0A84FF',
      activeBg: 'rgba(10, 132, 255, 0.15)',
      activeBorder: 'rgba(10, 132, 255, 0.35)',
    },
    {
      key: 'Fiyatlar' as const,
      title: 'Fiyatlar',
      count: fiyatlar.length,
      icon: Tag,
      color: '#30D158',
      activeBg: 'rgba(48, 209, 88, 0.15)',
      activeBorder: 'rgba(48, 209, 88, 0.35)',
    },
    {
      key: 'Gorseller' as const,
      title: 'Evrak',
      count: gorseller.length,
      icon: ImageIcon,
      color: '#BF5AF2',
      activeBg: 'rgba(191, 90, 242, 0.15)',
      activeBorder: 'rgba(191, 90, 242, 0.35)',
    },
    {
      key: 'Notlar' as const,
      title: 'Notlar',
      count: notlar.length,
      icon: FileText,
      color: '#FF9F0A',
      activeBg: 'rgba(255, 159, 10, 0.15)',
      activeBorder: 'rgba(255, 159, 10, 0.35)',
    },
  ];

  return (
    <SafeAreaView style={styles.container}>
      <StatusBar barStyle="light-content" backgroundColor="#0A0A0A" />

      {/* 1. ÜST HEADER */}
      <View style={styles.header}>
        <TouchableOpacity
          style={styles.backBtn}
          onPress={() => {
            triggerSelectionHaptic();
            navigation.goBack();
          }}
          activeOpacity={0.7}
        >
          <ArrowLeft color="#FFFFFF" size={22} />
        </TouchableOpacity>

        <View style={styles.headerInfo}>
          <Text style={styles.cariUnvan} numberOfLines={1}>
            {klasor.cariUnvan}
          </Text>
          <View style={styles.headerSubRow}>
            {klasor.yetkili ? (
              <Text style={styles.headerMeta}>👤 {klasor.yetkili}</Text>
            ) : null}
            {klasor.telefon ? (
              <Text style={styles.headerMeta}>📞 {klasor.telefon}</Text>
            ) : null}
          </View>
        </View>

        {/* PDF Raporu Al Butonu */}
        <TouchableOpacity
          style={styles.pdfBtn}
          onPress={handleExportPdf}
          disabled={isPdfGenerating}
          activeOpacity={0.8}
        >
          {isPdfGenerating ? (
            <ActivityIndicator size="small" color="#FF453A" />
          ) : (
            <>
              <FileDown color="#FF453A" size={17} />
              <Text style={styles.pdfBtnText}>PDF</Text>
            </>
          )}
        </TouchableOpacity>

        {/* Etiket / Renk Düzenle Butonu */}
        <TouchableOpacity
          style={[styles.etiketBtn, { borderColor: `${klasorRenk}60` }]}
          onPress={() => {
            triggerSelectionHaptic();
            setIsEtiketModalOpen(true);
          }}
          activeOpacity={0.8}
        >
          <View style={[styles.colorDot, { backgroundColor: klasorRenk }]} />
          <Text style={styles.etiketBtnText} numberOfLines={1}>
            {klasorEtiket}
          </Text>
        </TouchableOpacity>
      </View>

      {/* 2. MODERN SEKME ÇUBUĞU */}
      <View style={styles.tabBarWrapper}>
        <ScrollView
          horizontal
          showsHorizontalScrollIndicator={false}
          contentContainerStyle={styles.tabScrollContent}
        >
          {tabs.map((tab) => {
            const isActive = activeTab === tab.key;
            const IconComp = tab.icon;
            return (
              <TouchableOpacity
                key={tab.key}
                style={[
                  styles.tabItem,
                  isActive
                    ? {
                        backgroundColor: tab.activeBg,
                        borderColor: tab.activeBorder,
                      }
                    : styles.tabItemInactive,
                ]}
                onPress={() => {
                  triggerSelectionHaptic();
                  setActiveTab(tab.key);
                }}
                activeOpacity={0.7}
              >
                <IconComp
                  color={isActive ? tab.color : '#64748B'}
                  size={16}
                />
                <Text
                  style={[
                    styles.tabItemText,
                    isActive && styles.tabItemTextActive,
                  ]}
                >
                  {tab.title}
                </Text>
                <View
                  style={[
                    styles.tabBadge,
                    {
                      backgroundColor: isActive
                        ? tab.color
                        : 'rgba(255, 255, 255, 0.08)',
                    },
                  ]}
                >
                  <Text
                    style={[
                      styles.tabBadgeText,
                      isActive && styles.tabBadgeTextActive,
                    ]}
                  >
                    {tab.count}
                  </Text>
                </View>
              </TouchableOpacity>
            );
          })}
        </ScrollView>
      </View>

      {/* 3. SEKME İÇERİKLERİ */}
      <ScrollView
        style={styles.contentScroll}
        contentContainerStyle={styles.contentContainer}
        showsVerticalScrollIndicator={false}
        automaticallyAdjustKeyboardInsets={true}
        keyboardShouldPersistTaps="handled"
      >
        {/* --- 1. GÖRÜŞMELER PANELİ --- */}
        {activeTab === 'Gorusmeler' && (
          <View style={styles.panelContainer}>
            {/* Ekleme Kartı */}
            <View style={styles.addCard}>
              <Text style={styles.addCardTitle}>Yeni Görüşme Kaydet</Text>
              
              {/* Tarih Seçimi */}
              <View style={{ flexDirection: 'row', alignItems: 'center', marginBottom: 10, gap: 8 }}>
                <View style={{ flex: 1, flexDirection: 'row', alignItems: 'center', backgroundColor: '#1E293B', borderRadius: 8, paddingHorizontal: 10, height: 42, borderWidth: 1, borderColor: '#334155' }}>
                  <Calendar color="#38BDF8" size={16} style={{ marginRight: 8 }} />
                  <TextInput
                    style={{ flex: 1, color: '#F8FAFC', fontSize: 13, padding: 0 }}
                    placeholder="YYYY-AA-GG (Örn: 2026-09-11)"
                    placeholderTextColor="#64748B"
                    value={gorusmeTarih}
                    onChangeText={setGorusmeTarih}
                  />
                </View>
                <TouchableOpacity
                  style={{ backgroundColor: '#334155', paddingHorizontal: 12, height: 42, borderRadius: 8, justifyContent: 'center', alignItems: 'center' }}
                  onPress={() => setGorusmeTarih(getTodayStr())}
                >
                  <Text style={{ color: '#F8FAFC', fontSize: 12, fontWeight: '600' }}>Bugün</Text>
                </TouchableOpacity>
                <TouchableOpacity
                  style={{ backgroundColor: '#1E293B', borderWidth: 1, borderColor: '#334155', paddingHorizontal: 12, height: 42, borderRadius: 8, justifyContent: 'center', alignItems: 'center' }}
                  onPress={() => setGorusmeTarih(getYesterdayStr())}
                >
                  <Text style={{ color: '#94A3B8', fontSize: 12, fontWeight: '600' }}>Dün</Text>
                </TouchableOpacity>
              </View>

              <TextInput
                style={styles.formInput}
                placeholder="Görüşme Konusu (Örn: Fiyat Revizesi İstendi, Toplantı...)"
                placeholderTextColor="#64748B"
                value={gorusmeBaslik}
                onChangeText={setGorusmeBaslik}
              />
              <TextInput
                style={[styles.formInput, styles.formTextArea]}
                placeholder="Konuşulan detaylar, alınan kararlar..."
                placeholderTextColor="#64748B"
                value={gorusmeIcerik}
                onChangeText={setGorusmeIcerik}
                multiline
                numberOfLines={3}
              />
              <TouchableOpacity
                style={[styles.actionSubmitBtn, { backgroundColor: '#0A84FF' }]}
                onPress={handleAddGorusme}
                activeOpacity={0.8}
              >
                <Plus color="#FFFFFF" size={18} />
                <Text style={styles.actionSubmitBtnText}>GÖRÜŞME EKLE</Text>
              </TouchableOpacity>
            </View>

            {/* Görüşmeler Listesi */}
            <View style={styles.itemsList}>
              {gorusmeler.length === 0 ? (
                <Text style={styles.emptyListText}>Henüz kayıtlı görüşme bulunmuyor.</Text>
              ) : (
                gorusmeler.map((item) => (
                  <View key={item.id} style={styles.itemCard}>
                    <View style={styles.itemCardHeader}>
                      <View style={styles.itemCardTitleRow}>
                        <MessageSquare color="#0A84FF" size={17} />
                        <Text style={styles.itemCardTitle}>{item.baslik}</Text>
                      </View>
                      <TouchableOpacity
                        style={styles.itemDeleteBtn}
                        onPress={() => handleDeleteDetay(item)}
                      >
                        <Trash2 color="#FF453A" size={15} />
                      </TouchableOpacity>
                    </View>
                    <Text style={styles.itemDateText}>{formatDate(item.tarih)}</Text>
                    {item.icerik ? (
                      <Text style={styles.itemCardBodyText}>{item.icerik}</Text>
                    ) : null}
                  </View>
                ))
              )}
            </View>
          </View>
        )}

        {/* --- 2. VERİLEN FİYATLAR PANELİ --- */}
        {activeTab === 'Fiyatlar' && (
          <View style={styles.panelContainer}>
            {/* Ekleme Kartı */}
            <View style={styles.addCard}>
              <Text style={styles.addCardTitle}>Yeni Fiyat Teklifi Ekle</Text>

              {/* Tarih Seçimi */}
              <View style={{ flexDirection: 'row', alignItems: 'center', marginBottom: 10, gap: 8 }}>
                <View style={{ flex: 1, flexDirection: 'row', alignItems: 'center', backgroundColor: '#1E293B', borderRadius: 8, paddingHorizontal: 10, height: 42, borderWidth: 1, borderColor: '#334155' }}>
                  <Calendar color="#34D399" size={16} style={{ marginRight: 8 }} />
                  <TextInput
                    style={{ flex: 1, color: '#F8FAFC', fontSize: 13, padding: 0 }}
                    placeholder="YYYY-AA-GG (Örn: 2026-09-11)"
                    placeholderTextColor="#64748B"
                    value={fiyatTarih}
                    onChangeText={setFiyatTarih}
                  />
                </View>
                <TouchableOpacity
                  style={{ backgroundColor: '#334155', paddingHorizontal: 12, height: 42, borderRadius: 8, justifyContent: 'center', alignItems: 'center' }}
                  onPress={() => setFiyatTarih(getTodayStr())}
                >
                  <Text style={{ color: '#F8FAFC', fontSize: 12, fontWeight: '600' }}>Bugün</Text>
                </TouchableOpacity>
                <TouchableOpacity
                  style={{ backgroundColor: '#1E293B', borderWidth: 1, borderColor: '#334155', paddingHorizontal: 12, height: 42, borderRadius: 8, justifyContent: 'center', alignItems: 'center' }}
                  onPress={() => setFiyatTarih(getYesterdayStr())}
                >
                  <Text style={{ color: '#94A3B8', fontSize: 12, fontWeight: '600' }}>Dün</Text>
                </TouchableOpacity>
              </View>

              <TextInput
                style={styles.formInput}
                placeholder="Teklif Başlığı / Ürün Adı..."
                placeholderTextColor="#64748B"
                value={fiyatBaslik}
                onChangeText={setFiyatBaslik}
              />
              <View style={styles.priceInputRow}>
                <TextInput
                  style={[styles.formInput, { flex: 1 }]}
                  placeholder="Tutar (Örn: 15000)"
                  placeholderTextColor="#64748B"
                  keyboardType="numeric"
                  inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                  value={fiyatTutar}
                  onChangeText={setFiyatTutar}
                />
                <View style={styles.currencySelector}>
                  {PARA_BIRIMLERI.map((b) => (
                    <TouchableOpacity
                      key={b}
                      style={[
                        styles.currencyBtn,
                        fiyatParaBirimi === b && styles.currencyBtnActive,
                      ]}
                      onPress={() => {
                        triggerSelectionHaptic();
                        setFiyatParaBirimi(b);
                      }}
                    >
                      <Text
                        style={[
                          styles.currencyBtnText,
                          fiyatParaBirimi === b && styles.currencyBtnTextActive,
                        ]}
                      >
                        {b}
                      </Text>
                    </TouchableOpacity>
                  ))}
                </View>
              </View>
              <TextInput
                style={[styles.formInput, styles.formTextArea]}
                placeholder="Teklif detayları, geçerlilik süresi vb..."
                placeholderTextColor="#64748B"
                value={fiyatAciklama}
                onChangeText={setFiyatAciklama}
                multiline
                numberOfLines={2}
              />
              <TouchableOpacity
                style={[styles.actionSubmitBtn, { backgroundColor: '#30D158' }]}
                onPress={handleAddFiyat}
                activeOpacity={0.8}
              >
                <Plus color="#FFFFFF" size={18} />
                <Text style={styles.actionSubmitBtnText}>FİYAT EKLE</Text>
              </TouchableOpacity>
            </View>

            {/* Fiyatlar Listesi */}
            <View style={styles.itemsList}>
              {fiyatlar.length === 0 ? (
                <Text style={styles.emptyListText}>Henüz kayıtlı fiyat teklifi bulunmuyor.</Text>
              ) : (
                fiyatlar.map((item) => (
                  <View key={item.id} style={styles.itemCard}>
                    <View style={styles.itemCardHeader}>
                      <View style={styles.itemCardTitleRow}>
                        <Tag color="#30D158" size={17} />
                        <Text style={styles.itemCardTitle}>{item.baslik}</Text>
                      </View>
                      <View style={styles.priceRowRight}>
                        <View style={styles.priceBadge}>
                          <Text style={styles.priceBadgeText}>
                            {(item.fiyatBilgisi || 0).toLocaleString('tr-TR', {
                              minimumFractionDigits: 2,
                            })}{' '}
                            {item.paraBirimi || '₺'}
                          </Text>
                        </View>
                        <TouchableOpacity
                          style={styles.itemDeleteBtn}
                          onPress={() => handleDeleteDetay(item)}
                        >
                          <Trash2 color="#FF453A" size={15} />
                        </TouchableOpacity>
                      </View>
                    </View>
                    <Text style={styles.itemDateText}>{formatDate(item.tarih)}</Text>
                    {item.icerik ? (
                      <Text style={styles.itemCardBodyText}>{item.icerik}</Text>
                    ) : null}
                  </View>
                ))
              )}
            </View>
          </View>
        )}

        {/* --- 3. GÖRSELLER & EVRAKLAR PANELİ --- */}
        {activeTab === 'Gorseller' && (
          <View style={styles.panelContainer}>
            {/* Ekleme Kartı */}
            <View style={styles.addCard}>
              <Text style={styles.addCardTitle}>Yeni Görsel / Evrak Yükle</Text>
              <TextInput
                style={styles.formInput}
                placeholder="Görsel Açıklaması (Örn: Saha Keşfi, Sözleşme...)"
                placeholderTextColor="#64748B"
                value={gorselBaslik}
                onChangeText={setGorselBaslik}
              />

              {/* Fotoğraf Seçim Butonları */}
              <View style={styles.imagePickerBtnsRow}>
                <TouchableOpacity
                  style={styles.imagePickerBtn}
                  onPress={() => handlePickImage(false)}
                  activeOpacity={0.8}
                >
                  <UploadCloud color="#BF5AF2" size={20} />
                  <Text style={styles.imagePickerBtnText}>Galeriden Seç</Text>
                </TouchableOpacity>

                <TouchableOpacity
                  style={styles.imagePickerBtn}
                  onPress={() => handlePickImage(true)}
                  activeOpacity={0.8}
                >
                  <Camera color="#BF5AF2" size={20} />
                  <Text style={styles.imagePickerBtnText}>Fotoğraf Çek</Text>
                </TouchableOpacity>
              </View>

              {/* Seçilen Fotoğraf Önizlemesi */}
              {selectedImageUri && (
                <View style={styles.selectedImgPreviewBox}>
                  <Image source={{ uri: selectedImageUri }} style={styles.selectedImgPreview} />
                  <TouchableOpacity
                    style={styles.selectedImgRemoveBtn}
                    onPress={() => {
                      setSelectedImageUri(null);
                      setSelectedImageBase64(null);
                    }}
                  >
                    <X color="#FFFFFF" size={16} />
                  </TouchableOpacity>
                </View>
              )}

              <TouchableOpacity
                style={[styles.actionSubmitBtn, { backgroundColor: '#BF5AF2' }]}
                onPress={handleAddGorsel}
                activeOpacity={0.8}
              >
                <Plus color="#FFFFFF" size={18} />
                <Text style={styles.actionSubmitBtnText}>GÖRSELİ KAYDET</Text>
              </TouchableOpacity>
            </View>

            {/* Görseller Izgarası */}
            <View style={styles.galleryGrid}>
              {gorseller.length === 0 ? (
                <Text style={styles.emptyListText}>Henüz yüklenen evrak veya görsel yok.</Text>
              ) : (
                gorseller.map((item) => {
                  const imageSource = item.gorselBase64
                    ? { uri: `data:image/jpeg;base64,${item.gorselBase64}` }
                    : item.dosyaYolu
                    ? { uri: item.dosyaYolu }
                    : null;

                  return (
                    <View key={item.id} style={styles.galleryCard}>
                      <TouchableOpacity
                        style={styles.galleryImgTouch}
                        onPress={() => {
                          if (imageSource) {
                            setPreviewImage(imageSource.uri);
                          }
                        }}
                        activeOpacity={0.9}
                      >
                        {imageSource ? (
                          <Image source={imageSource} style={styles.galleryImg} />
                        ) : (
                          <View style={styles.noImgBox}>
                            <ImageIcon color="#64748B" size={32} />
                          </View>
                        )}
                      </TouchableOpacity>

                      <View style={styles.galleryCardBottom}>
                        <View style={{ flex: 1, marginRight: 6 }}>
                          <Text style={styles.galleryCardTitle} numberOfLines={1}>
                            {item.baslik}
                          </Text>
                          <Text style={styles.galleryCardDate}>
                            {formatDate(item.tarih)}
                          </Text>
                        </View>
                        <TouchableOpacity
                          style={styles.galleryCardDelete}
                          onPress={() => handleDeleteDetay(item)}
                        >
                          <Trash2 color="#FF453A" size={14} />
                        </TouchableOpacity>
                      </View>
                    </View>
                  );
                })
              )}
            </View>
          </View>
        )}

        {/* --- 4. NOTLAR PANELİ --- */}
        {activeTab === 'Notlar' && (
          <View style={styles.panelContainer}>
            {/* Ekleme Kartı */}
            <View style={styles.addCard}>
              <Text style={styles.addCardTitle}>Yeni Özel Not Ekle</Text>

              {/* Tarih Seçimi */}
              <View style={{ flexDirection: 'row', alignItems: 'center', marginBottom: 10, gap: 8 }}>
                <View style={{ flex: 1, flexDirection: 'row', alignItems: 'center', backgroundColor: '#1E293B', borderRadius: 8, paddingHorizontal: 10, height: 42, borderWidth: 1, borderColor: '#334155' }}>
                  <Calendar color="#FBBF24" size={16} style={{ marginRight: 8 }} />
                  <TextInput
                    style={{ flex: 1, color: '#F8FAFC', fontSize: 13, padding: 0 }}
                    placeholder="YYYY-AA-GG (Örn: 2026-09-11)"
                    placeholderTextColor="#64748B"
                    value={notTarih}
                    onChangeText={setNotTarih}
                  />
                </View>
                <TouchableOpacity
                  style={{ backgroundColor: '#334155', paddingHorizontal: 12, height: 42, borderRadius: 8, justifyContent: 'center', alignItems: 'center' }}
                  onPress={() => setNotTarih(getTodayStr())}
                >
                  <Text style={{ color: '#F8FAFC', fontSize: 12, fontWeight: '600' }}>Bugün</Text>
                </TouchableOpacity>
                <TouchableOpacity
                  style={{ backgroundColor: '#1E293B', borderWidth: 1, borderColor: '#334155', paddingHorizontal: 12, height: 42, borderRadius: 8, justifyContent: 'center', alignItems: 'center' }}
                  onPress={() => setNotTarih(getYesterdayStr())}
                >
                  <Text style={{ color: '#94A3B8', fontSize: 12, fontWeight: '600' }}>Dün</Text>
                </TouchableOpacity>
              </View>

              <TextInput
                style={styles.formInput}
                placeholder="Not Başlığı (Örn: Sevkiyat Hatırlatması, Ödeme Talebi...)"
                placeholderTextColor="#64748B"
                value={notBaslik}
                onChangeText={setNotBaslik}
              />
              <TextInput
                style={[styles.formInput, styles.formTextArea]}
                placeholder="Notunuzu buraya yazın..."
                placeholderTextColor="#64748B"
                value={notIcerik}
                onChangeText={setNotIcerik}
                multiline
                numberOfLines={3}
              />
              <TouchableOpacity
                style={[styles.actionSubmitBtn, { backgroundColor: '#FF9F0A' }]}
                onPress={handleAddNot}
                activeOpacity={0.8}
              >
                <Plus color="#FFFFFF" size={18} />
                <Text style={styles.actionSubmitBtnText}>NOT EKLE</Text>
              </TouchableOpacity>
            </View>

            {/* Notlar Listesi */}
            <View style={styles.itemsList}>
              {notlar.length === 0 ? (
                <Text style={styles.emptyListText}>Henüz kayıtlı not bulunmuyor.</Text>
              ) : (
                notlar.map((item) => (
                  <View key={item.id} style={styles.itemCard}>
                    <View style={styles.itemCardHeader}>
                      <View style={styles.itemCardTitleRow}>
                        <FileText color="#FF9F0A" size={17} />
                        <Text style={styles.itemCardTitle}>{item.baslik}</Text>
                      </View>
                      <TouchableOpacity
                        style={styles.itemDeleteBtn}
                        onPress={() => handleDeleteDetay(item)}
                      >
                        <Trash2 color="#FF453A" size={15} />
                      </TouchableOpacity>
                    </View>
                    <Text style={styles.itemDateText}>{formatDate(item.tarih)}</Text>
                    {item.icerik ? (
                      <Text style={styles.itemCardBodyText}>{item.icerik}</Text>
                    ) : null}
                  </View>
                ))
              )}
            </View>
          </View>
        )}
      </ScrollView>

      {/* ETİKET & RENK AYARLAMA MODALI */}
      <Modal
        visible={isEtiketModalOpen}
        animationType="fade"
        transparent={true}
        onRequestClose={() => setIsEtiketModalOpen(false)}
      >
        <View style={styles.modalOverlay}>
          <View style={styles.modalBox}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>Klasör Ayarları</Text>
              <TouchableOpacity onPress={() => setIsEtiketModalOpen(false)}>
                <X color="#94A3B8" size={20} />
              </TouchableOpacity>
            </View>

            {/* Durum Etiketi Seçimi */}
            <Text style={styles.modalSectionLabel}>Durum Etiketi:</Text>
            <View style={styles.etiketOptions}>
              {ETIKETLER.map((et) => {
                const isSelected = klasorEtiket === et;
                return (
                  <TouchableOpacity
                    key={et}
                    style={[styles.etiketOptionBtn, isSelected && styles.etiketOptionBtnActive]}
                    onPress={() => {
                      triggerSelectionHaptic();
                      setKlasorEtiket(et);
                    }}
                  >
                    <Text
                      style={[
                        styles.etiketOptionBtnText,
                        isSelected && styles.etiketOptionBtnTextActive,
                      ]}
                    >
                      {et}
                    </Text>
                  </TouchableOpacity>
                );
              })}
            </View>

            {/* Klasör Renk Seçimi */}
            <Text style={[styles.modalSectionLabel, { marginTop: 16 }]}>Klasör Rengi:</Text>
            <View style={styles.colorOptions}>
              {RENKLER.map((clr) => {
                const isSelected = klasorRenk === clr;
                return (
                  <TouchableOpacity
                    key={clr}
                    style={[
                      styles.colorCircle,
                      { backgroundColor: clr },
                      isSelected && styles.colorCircleActive,
                    ]}
                    onPress={() => {
                      triggerSelectionHaptic();
                      setKlasorRenk(clr);
                    }}
                  >
                    {isSelected && <Check color="#FFFFFF" size={16} />}
                  </TouchableOpacity>
                );
              })}
            </View>

            {/* Kaydet Butonu */}
            <TouchableOpacity
              style={styles.modalSaveBtn}
              onPress={handleSaveKlasorInfo}
              activeOpacity={0.8}
            >
              <Save color="#FFFFFF" size={18} />
              <Text style={styles.modalSaveBtnText}>KAYDET</Text>
            </TouchableOpacity>
          </View>
        </View>
      </Modal>

      {/* TAM EKRAN GÖRSEL ÖNİZLEME MODALI */}
      <Modal
        visible={!!previewImage}
        animationType="fade"
        transparent={true}
        onRequestClose={() => setPreviewImage(null)}
      >
        <View style={styles.fullImageOverlay}>
          <TouchableOpacity
            style={styles.fullImageCloseBtn}
            onPress={() => setPreviewImage(null)}
          >
            <X color="#FFFFFF" size={24} />
          </TouchableOpacity>
          {previewImage && (
            <Image
              source={{ uri: previewImage }}
              style={styles.fullImage}
              resizeMode="contain"
            />
          )}
        </View>
      </Modal>
      <KeyboardDoneAccessory />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0A0A0A',
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingTop: Platform.OS === 'android' ? 36 : 10,
    paddingBottom: 14,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.08)',
  },
  backBtn: {
    width: 38,
    height: 38,
    borderRadius: 10,
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: 10,
  },
  headerInfo: {
    flex: 1,
    marginRight: 8,
  },
  cariUnvan: {
    fontSize: 16,
    fontWeight: '800',
    color: '#FFFFFF',
  },
  headerSubRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    marginTop: 2,
  },
  headerMeta: {
    fontSize: 11,
    color: '#94A3B8',
  },
  pdfBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(255, 69, 58, 0.15)',
    borderWidth: 1,
    borderColor: 'rgba(255, 69, 58, 0.3)',
    paddingVertical: 7,
    paddingHorizontal: 10,
    borderRadius: 8,
    marginRight: 8,
    gap: 4,
  },
  pdfBtnText: {
    color: '#FF453A',
    fontSize: 12,
    fontWeight: '700',
  },
  etiketBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.06)',
    borderWidth: 1,
    paddingVertical: 7,
    paddingHorizontal: 10,
    borderRadius: 8,
    gap: 6,
    maxWidth: 120,
  },
  colorDot: {
    width: 8,
    height: 8,
    borderRadius: 4,
  },
  etiketBtnText: {
    fontSize: 11,
    fontWeight: '600',
    color: '#E2E8F0',
  },

  // Sekmeler Barı
  tabBarWrapper: {
    marginTop: 10,
    marginBottom: 2,
  },
  tabScrollContent: {
    paddingHorizontal: 16,
    gap: 8,
    flexDirection: 'row',
    alignItems: 'center',
  },
  tabItem: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 8,
    paddingHorizontal: 14,
    borderRadius: 12,
    borderWidth: 1,
    gap: 6,
  },
  tabItemInactive: {
    backgroundColor: 'rgba(255, 255, 255, 0.04)',
    borderColor: 'rgba(255, 255, 255, 0.08)',
  },
  tabItemText: {
    fontSize: 12.5,
    fontWeight: '600',
    color: '#94A3B8',
  },
  tabItemTextActive: {
    color: '#FFFFFF',
    fontWeight: '700',
  },
  tabBadge: {
    paddingHorizontal: 6,
    paddingVertical: 1.5,
    borderRadius: 8,
    minWidth: 18,
    alignItems: 'center',
    justifyContent: 'center',
  },
  tabBadgeText: {
    fontSize: 10.5,
    fontWeight: '700',
    color: '#94A3B8',
  },
  tabBadgeTextActive: {
    color: '#FFFFFF',
    fontWeight: '800',
  },

  // İçerik Alanı
  contentScroll: {
    flex: 1,
  },
  contentContainer: {
    padding: 16,
    paddingBottom: 90,
  },
  panelContainer: {
    gap: 16,
  },

  // Ekleme Kartı
  addCard: {
    backgroundColor: 'rgba(24, 24, 28, 0.85)',
    borderRadius: 14,
    padding: 14,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    gap: 10,
  },
  addCardTitle: {
    fontSize: 14,
    fontWeight: '700',
    color: '#FFFFFF',
    marginBottom: 2,
  },
  formInput: {
    backgroundColor: 'rgba(255, 255, 255, 0.06)',
    borderRadius: 8,
    paddingHorizontal: 12,
    height: 42,
    color: '#FFFFFF',
    fontSize: 13,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
  },
  formTextArea: {
    height: 70,
    paddingTop: 10,
    textAlignVertical: 'top',
  },
  priceInputRow: {
    flexDirection: 'row',
    gap: 8,
  },
  currencySelector: {
    flexDirection: 'row',
    backgroundColor: 'rgba(255, 255, 255, 0.06)',
    borderRadius: 8,
    padding: 3,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
  },
  currencyBtn: {
    width: 36,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 6,
  },
  currencyBtnActive: {
    backgroundColor: '#30D158',
  },
  currencyBtnText: {
    color: '#94A3B8',
    fontSize: 14,
    fontWeight: '700',
  },
  currencyBtnTextActive: {
    color: '#FFFFFF',
  },
  actionSubmitBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 10,
    borderRadius: 8,
    gap: 6,
    marginTop: 4,
  },
  actionSubmitBtnText: {
    color: '#FFFFFF',
    fontWeight: '700',
    fontSize: 13,
  },

  // Fotoğraf Butonları
  imagePickerBtnsRow: {
    flexDirection: 'row',
    gap: 10,
  },
  imagePickerBtn: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(191, 90, 242, 0.15)',
    borderWidth: 1,
    borderColor: 'rgba(191, 90, 242, 0.3)',
    borderRadius: 8,
    paddingVertical: 10,
    gap: 6,
  },
  imagePickerBtnText: {
    color: '#BF5AF2',
    fontSize: 12,
    fontWeight: '700',
  },
  selectedImgPreviewBox: {
    position: 'relative',
    height: 120,
    borderRadius: 8,
    overflow: 'hidden',
    backgroundColor: '#000',
  },
  selectedImgPreview: {
    width: '100%',
    height: '100%',
    resizeMode: 'cover',
  },
  selectedImgRemoveBtn: {
    position: 'absolute',
    top: 6,
    right: 6,
    backgroundColor: 'rgba(0,0,0,0.7)',
    borderRadius: 12,
    width: 24,
    height: 24,
    alignItems: 'center',
    justifyContent: 'center',
  },

  // Liste Elemanları
  itemsList: {
    gap: 10,
  },
  itemCard: {
    backgroundColor: 'rgba(24, 24, 28, 0.85)',
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
  },
  itemCardHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  itemCardTitleRow: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    marginRight: 8,
  },
  itemCardTitle: {
    fontSize: 14,
    fontWeight: '700',
    color: '#FFFFFF',
    flex: 1,
  },
  itemDeleteBtn: {
    width: 28,
    height: 28,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 6,
    backgroundColor: 'rgba(255, 69, 58, 0.1)',
  },
  itemDateText: {
    fontSize: 11,
    color: '#64748B',
    marginTop: 4,
  },
  itemCardBodyText: {
    fontSize: 13,
    color: '#CBD5E1',
    marginTop: 8,
    lineHeight: 18,
  },
  emptyListText: {
    color: '#64748B',
    textAlign: 'center',
    paddingVertical: 20,
    fontSize: 13,
  },
  priceRowRight: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  priceBadge: {
    backgroundColor: 'rgba(48, 209, 88, 0.15)',
    borderWidth: 1,
    borderColor: 'rgba(48, 209, 88, 0.3)',
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 6,
  },
  priceBadgeText: {
    color: '#30D158',
    fontSize: 12,
    fontWeight: '700',
  },

  // Galeri Izgarası
  galleryGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 12,
  },
  galleryCard: {
    width: (SCREEN_WIDTH - 44) / 2,
    backgroundColor: 'rgba(24, 24, 28, 0.85)',
    borderRadius: 12,
    overflow: 'hidden',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
  },
  galleryImgTouch: {
    width: '100%',
    height: 140,
    backgroundColor: '#101014',
  },
  galleryImg: {
    width: '100%',
    height: '100%',
    resizeMode: 'cover',
  },
  noImgBox: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  galleryCardBottom: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 8,
  },
  galleryCardTitle: {
    fontSize: 12,
    fontWeight: '700',
    color: '#FFFFFF',
  },
  galleryCardDate: {
    fontSize: 10,
    color: '#64748B',
    marginTop: 2,
  },
  galleryCardDelete: {
    width: 26,
    height: 26,
    borderRadius: 6,
    backgroundColor: 'rgba(255, 69, 58, 0.1)',
    alignItems: 'center',
    justifyContent: 'center',
  },

  // Modal Stilleri
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.8)',
    alignItems: 'center',
    justifyContent: 'center',
    padding: 20,
  },
  modalBox: {
    width: '100%',
    maxWidth: 360,
    backgroundColor: '#121216',
    borderRadius: 16,
    padding: 20,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.1)',
  },
  modalHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 16,
  },
  modalTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: '#FFFFFF',
  },
  modalSectionLabel: {
    fontSize: 12,
    fontWeight: '600',
    color: '#94A3B8',
    marginBottom: 8,
  },
  etiketOptions: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  etiketOptionBtn: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 8,
    backgroundColor: 'rgba(255, 255, 255, 0.06)',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
  },
  etiketOptionBtnActive: {
    backgroundColor: '#0A84FF',
    borderColor: '#0A84FF',
  },
  etiketOptionBtnText: {
    fontSize: 12,
    color: '#94A3B8',
  },
  etiketOptionBtnTextActive: {
    color: '#FFFFFF',
    fontWeight: '700',
  },
  colorOptions: {
    flexDirection: 'row',
    gap: 12,
  },
  colorCircle: {
    width: 32,
    height: 32,
    borderRadius: 16,
    alignItems: 'center',
    justifyContent: 'center',
  },
  colorCircleActive: {
    borderWidth: 2,
    borderColor: '#FFFFFF',
  },
  modalSaveBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#0A84FF',
    paddingVertical: 12,
    borderRadius: 10,
    gap: 6,
    marginTop: 20,
  },
  modalSaveBtnText: {
    color: '#FFFFFF',
    fontWeight: '700',
    fontSize: 13,
  },

  // Tam Ekran Görsel
  fullImageOverlay: {
    flex: 1,
    backgroundColor: '#000000',
    alignItems: 'center',
    justifyContent: 'center',
  },
  fullImageCloseBtn: {
    position: 'absolute',
    top: 40,
    right: 20,
    zIndex: 10,
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: 'rgba(255, 255, 255, 0.2)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  fullImage: {
    width: '100%',
    height: '80%',
  },
});
