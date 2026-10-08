import React, { useState, useEffect } from 'react';
import { StyleSheet, Text, View, SafeAreaView, TextInput, TouchableOpacity, ScrollView, ActivityIndicator, Alert, FlatList, Modal, Share, Image } from 'react-native';
import { Wrench, TrendingUp, Landmark, Target, RefreshCw, Save, Users, Layers, Search, X, Activity, Percent, AlertTriangle, Briefcase, Plus, Phone, Trash2, Edit3, Share2, Paperclip, Clock, FileText, Lock, Palette, Folder, ArrowLeft, ChevronDown, ChevronUp, Sparkles } from 'lucide-react-native';
import { subscribeToPath, writeData, deleteData, readData } from '../services/firebase';
import { generateInt32Id } from '../utils/IdGenerator';
import { hesapKur, hesapOptimalFiyat } from '../services/analizUtils';
import * as Device from 'expo-device';
import * as ImagePicker from 'expo-image-picker';
import * as FileSystem from 'expo-file-system/legacy';
import MusteriLimitScreen from './MusteriLimitScreen';
import {
  KeyboardDoneAccessory,
  KEYBOARD_ACCESSORY_ID,
} from '../components/KeyboardDoneAccessory';

const formatMoney = (val: number) => {
  return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' }).format(val);
};

const appStartTime = Date.now();

const formatUptime = (ms: number) => {
  const sec = Math.floor(ms / 1000);
  const d = Math.floor(sec / 86400);
  const h = Math.floor((sec % 86400) / 3600);
  const m = Math.floor((sec % 3600) / 60);
  const s = sec % 60;
  return `${d} g ${h} s ${m} dk ${s} sn`;
};

export default function AraclarScreen() {
  const [activeTab, setActiveTab] = useState<'kurlar' | 'fiyat' | 'hedef' | 'cariBir' | 'urunBir' | 'sistem' | 'faiz' | 'risk' | 'portfoy' | 'belge' | 'limit' | 'kur' | 'optimal' | 'evrak' | null>(null);
  const [cariler, setCariler] = useState<any[]>([]);
  const [stoklar, setStoklar] = useState<any[]>([]);
  const [portfoyler, setPortfoyler] = useState<any[]>([]);
  const [belgeler, setBelgeler] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  // 1. Döviz Kurları (Canlı TCMB / API Entegrasyonu)
  const [usdRate, setUsdRate] = useState(33.45);
  const [eurRate, setEurRate] = useState(36.12);
  const [gbpRate, setGbpRate] = useState(42.30);
  const [kurLoader, setKurLoader] = useState(false);
  const [sonKurGuncelleme, setSonKurGuncelleme] = useState('Henüz güncellenmedi');

  // 2. Toplu Fiyat Güncelleme
  const [guncellemeYonu, setGuncellemeYonu] = useState<'Artış' | 'Azalış'>('Artış');
  const [yuzdeOran, setYuzdeOran] = useState('');

  // 3. Hedef Yönetimi & Gelişmiş Bütçe Dağılımı
  const [aylikHedef, setAylikHedef] = useState('');
  const [hedefLoader, setHedefLoader] = useState(false);
  const [faturalar, setFaturalar] = useState<any[]>([]);
  const [selectedHedefYear, setSelectedHedefYear] = useState<number>(new Date().getFullYear());
  const [aylikHedefler, setAylikHedefler] = useState<any[]>([]);
  const [selectedEditMonth, setSelectedEditMonth] = useState<number | null>(null);
  const [isDagitimModalOpen, setIsDagitimModalOpen] = useState(false);
  const [yillikHedefInput, setYillikHedefInput] = useState('');
  const [dagitimTuru, setDagitimTuru] = useState<'esit' | 'trend'>('esit');
  const [trendBuyumeOrani, setTrendBuyumeOrani] = useState('15');
  const [expandedWeeksMonth, setExpandedWeeksMonth] = useState<number | null>(null);

  // 4. Cari Birleştirme State
  const [sourceCari, setSourceCari] = useState<any | null>(null);
  const [targetCari, setTargetCari] = useState<any | null>(null);
  const [isCariOverlayOpen, setIsCariOverlayOpen] = useState(false);
  const [cariSelectorType, setCariSelectorType] = useState<'source' | 'target'>('source');
  const [cariSearch, setCariSearch] = useState('');

  // 5. Ürün Birleştirme State
  const [sourceStok, setSourceStok] = useState<any | null>(null);
  const [targetStok, setTargetStok] = useState<any | null>(null);
  const [isStokOverlayOpen, setIsStokOverlayOpen] = useState(false);
  const [stokSelectorType, setStokSelectorType] = useState<'source' | 'target'>('source');
  const [stokSearch, setStokSearch] = useState('');

  // 6. Sistem Sağlığı State (gerçek cihaz metrikleri)
  const [deviceModel, setDeviceModel] = useState('Yükleniyor...');
  const [deviceOs, setDeviceOs] = useState('');
  const [totalRam, setTotalRam] = useState(0);
  const [deviceUptime, setDeviceUptime] = useState(0);
  const [appUptime, setAppUptime] = useState(0);
  const [isRealDevice, setIsRealDevice] = useState(true);

  // 7. Gecikme Faizi State
  const [anaPara, setAnaPara] = useState('50000');
  const [faizOrani, setFaizOrani] = useState('48');
  const [gecikmeGunu, setGecikmeGunu] = useState('30');

  // 7b. Kur Çevirici State
  const [kurKaynak, setKurKaynak] = useState<'TRY' | 'USD' | 'EUR' | 'GBP'>('USD');
  const [kurHedef, setKurHedef] = useState<'TRY' | 'USD' | 'EUR' | 'GBP'>('TRY');
  const [kurMiktar, setKurMiktar] = useState('100');

  // 7c. Optimal Fiyat State
  const [optAlisFiyati, setOptAlisFiyati] = useState('100');
  const [optKarOrani, setOptKarOrani] = useState('25');
  const [optKdvOrani, setOptKdvOrani] = useState('20');
  const [optEkMaliyet, setOptEkMaliyet] = useState('0');

  // 7d. Evrak No / Seri Yönetimi State
  const [seriTanimlar, setSeriTanimlar] = useState<any[]>([]);
  const [seriYukleme, setSeriYukleme] = useState(false);

  // 8. Risk Puanlayıcı State
  const [riskList, setRiskList] = useState<any[]>([]);

  // 9. Portföy Yönetimi (CRM) State
  const [isPortfoyModalOpen, setIsPortfoyModalOpen] = useState(false);
  const [editingPortfoyId, setEditingPortfoyId] = useState<string | null>(null);
  const [leadFirma, setLeadFirma] = useState('');
  const [leadYetkili, setLeadYetkili] = useState('');
  const [leadGsm, setLeadGsm] = useState('');
  const [leadIl, setLeadIl] = useState('');
  const [leadTutar, setLeadTutar] = useState('0');
  const [leadAciklama, setLeadAciklama] = useState('');

  // 10. Belge Arşivi State
  const [isDocFormOpen, setIsDocFormOpen] = useState(false);
  const [isDocDetailOpen, setIsDocDetailOpen] = useState(false);
  const [selectedDoc, setSelectedDoc] = useState<any | null>(null);
  const [docAd, setDocAd] = useState('');
  const [docKategori, setDocKategori] = useState('Genel');
  const [docAciklama, setDocAciklama] = useState('');
  const [docSearch, setDocSearch] = useState('');
  const [docDataUri, setDocDataUri] = useState<string | null>(null);
  const [docFileName, setDocFileName] = useState('');
  const [docFileSize, setDocFileSize] = useState(0);

  useEffect(() => {
    // Gerçek cihaz bilgileri (expo-device)
    setDeviceModel(Device.modelName || Device.brand || 'Bilinmiyor');
    setDeviceOs(`${Device.osName || ''} ${Device.osVersion || ''}`.trim());
    setIsRealDevice(Device.isDevice);
    if (Device.totalMemory) setTotalRam(Device.totalMemory);
    Device.getUptimeAsync().then(setDeviceUptime).catch(() => {});

    return () => {};
  }, []);

  useEffect(() => {
    const t = setInterval(() => setAppUptime(Date.now() - appStartTime), 1000);
    return () => clearInterval(t);
  }, []);

  useEffect(() => {
    const unsubCariler = subscribeToPath('Cariler', (data) => {
      if (data) {
        const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setCariler(list.filter(c => !c.isDeleted));
      }
    });

    const unsubStoklar = subscribeToPath('Stoklar', (data) => {
      if (data) {
        const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setStoklar(list.filter(s => !s.isDeleted));
      }
    });

    const unsubPortfoy = subscribeToPath('PortfoyKartlari', (data) => {
      if (data) {
        const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setPortfoyler(list);
      } else {
        setPortfoyler([]);
      }
    });

    const unsubBelgeler = subscribeToPath('BelgeArsiv', (data) => {
      if (data) {
        const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setBelgeler(list.filter(b => !b.isDeleted));
      } else {
        setBelgeler([]);
      }
    });

    const unsubSeriler = subscribeToPath('EvrakSerileri', (data) => {
      if (data) {
        const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setSeriTanimlar(list.filter(s => !s.isDeleted));
      } else {
        setSeriTanimlar([]);
      }
      setSeriYukleme(false);
    });

    const unsubFaturalar = subscribeToPath('Faturalar', (data) => {
      if (data) {
        const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setFaturalar(list.filter(f => !f.isDeleted));
      } else {
        setFaturalar([]);
      }
      setLoading(false);
    });

    return () => {
      unsubCariler();
      unsubStoklar();
      unsubPortfoy();
      unsubBelgeler();
      unsubSeriler();
      unsubFaturalar();
    };
  }, []);

  // Ciro hedefleri ve gerçekleşen ciroların reaktif hesaplanması
  useEffect(() => {
    const aylar = [
      'Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran',
      'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık'
    ];

    const fetchHedefler = subscribeToPath('SatisHedefleri', (hedefData) => {
      const dbHedefler = hedefData 
        ? (Array.isArray(hedefData) 
            ? hedefData.filter(Boolean) 
            : Object.keys(hedefData).map(k => ({ ...hedefData[k], firebaseKey: k })))
        : [];

      const filteredInvoices = faturalar.filter(f => {
        const date = new Date(f.tarih || f.Tarih);
        return date.getFullYear() === selectedHedefYear && (f.tur === 'Satis' || f.tur === 'Satış' || f.Tur === 'Satis' || f.Tur === 'Satış');
      });

      const list = aylar.map((ayAdi, index) => {
        const ayNum = index + 1;
        const dbHedef = dbHedefler.find(h => 
          (h.yil === selectedHedefYear && h.ay === ayNum) || 
          h.firebaseKey === `${selectedHedefYear}_${ayNum}` ||
          h.firebaseKey === `${selectedHedefYear * 100 + ayNum}`
        );

        const hedefTutari = dbHedef ? (dbHedef.hedefTutari ?? dbHedef.tutar ?? 0) : 0;
        
        const gerceklesen = filteredInvoices
          .filter(f => {
            const date = new Date(f.tarih || f.Tarih);
            return date.getMonth() + 1 === ayNum;
          })
          .reduce((sum, f) => sum + (f.genelToplam || f.GenelToplam || 0), 0);

        const yuzde = hedefTutari > 0 ? (gerceklesen / hedefTutari) * 100 : 0;
        const renk = yuzde >= 100 ? '#10B981' : (yuzde >= 70 ? '#3B82F6' : (yuzde >= 50 ? '#F59E0B' : '#EF4444'));

        const fark = gerceklesen - hedefTutari;
        return {
          ay: ayNum,
          ayAdi,
          hedef: hedefTutari,
          gerceklesen,
          fark,
          yuzde,
          renk,
          dbId: dbHedef?.firebaseKey || `${selectedHedefYear * 100 + ayNum}`
        };
      });

      setAylikHedefler(list);
    });

    return () => fetchHedefler();
  }, [faturalar, selectedHedefYear]);

  // Risk Puanı Hesaplama
  useEffect(() => {
    const activeCaris = cariler.filter(c => !c.isDeleted);
    const computed = activeCaris.map(c => {
      const bakiye = (c.borc || 0) - (c.alacak || 0);
      let riskScore = 50; // default medium risk

      if (bakiye > 100000) riskScore += 15;
      if (bakiye < 0) riskScore -= 20; // credit

      const status = riskScore >= 75 ? 'Yüksek Risk' : (riskScore <= 40 ? 'Düşük Risk' : 'Normal Risk');
      return { ...c, riskScore, status };
    });
    setRiskList(computed);
  }, [cariler]);

  // Döviz Kur Çekme (TCMB XML → Frankfurter yedeği)
  const handleRefreshKurlar = async () => {
    setKurLoader(true);
    try {
      let usd = 0, eur = 0, gbp = 0;
      try {
        // 1. TCMB resmi günlük kur (HTTPS)
        const res = await fetch('https://www.tcmb.gov.tr/kurlar/today.xml');
        if (res.ok) {
          const xml = (await res.text()).replace(/\s+/g, ' ');
          const getRate = (kod: string) => {
            const m = xml.match(new RegExp(`<Currency[^>]*Kodu="${kod}"[^>]*>.*?<ForexSelling>([^<]+)</ForexSelling>`, 'i'));
            return m ? parseFloat(m[1]) : 0;
          };
          usd = getRate('USD');
          eur = getRate('EUR');
          gbp = getRate('GBP');
        }
      } catch (e) {
        // TCMB erişilemezse boş geç, yedeğe düş
      }

      if (!usd || !eur || !gbp) {
        // 2. Frankfurter yedeği: 1 TRY = x USD/EUR/GBP → kur = 1/x
        const res = await fetch('https://api.frankfurter.app/latest?from=TRY&to=USD,EUR,GBP');
        if (res.ok) {
          const data = await res.json();
          if (data && data.rates) {
            if (data.rates.USD) usd = 1 / data.rates.USD;
            if (data.rates.EUR) eur = 1 / data.rates.EUR;
            if (data.rates.GBP) gbp = 1 / data.rates.GBP;
          }
        }
      }

      if (usd && eur && gbp) {
        setUsdRate(parseFloat(usd.toFixed(4)));
        setEurRate(parseFloat(eur.toFixed(4)));
        setGbpRate(parseFloat(gbp.toFixed(4)));
        const now = new Date().toLocaleTimeString('tr-TR');
        setSonKurGuncelleme(now);

        // DovizKurlari düğümüne kaydet (masaüstü paritesi)
        const kurId = new Date().toISOString().split('T')[0];
        const okKur = await writeData(`DovizKurlari/${kurId}`, {
          id: kurId,
          tarih: kurId,
          usd: parseFloat(usd.toFixed(4)),
          eur: parseFloat(eur.toFixed(4)),
          gbp: parseFloat(gbp.toFixed(4)),
        });
        if (!okKur) {
          Alert.alert('Uyarı', 'Kurlar çekildi ancak kaydedilemedi. (Bağlantı sorunu — kayıt sıraya alındı.)');
          return;
        }

        Alert.alert('Başarılı', `Döviz kurları güncellendi (${now}). Kaynak: TCMB / Frankfurter`);
      } else {
        Alert.alert('Hata', 'Döviz kurlarına ulaşılamadı. İnternet bağlantısını kontrol edin.');
      }
    } catch (e) {
      Alert.alert('Hata', 'Kurlar çekilirken hata oluştu.');
    } finally {
      setKurLoader(false);
    }
  };

  // Toplu Fiyat Zam / İndirim Entegrasyonu
  const handleUpdatePrices = async () => {
    const rate = parseFloat(yuzdeOran);
    if (isNaN(rate) || rate <= 0) {
      Alert.alert('Hata', 'Lütfen geçerli bir yüzde giriniz.');
      return;
    }

    Alert.alert(
      'Fiyat Güncelleme Onayı',
      `Tüm stok kartlarının satış fiyatlarını %${rate} oranında ${guncellemeYonu === 'Artış' ? 'artırmak' : 'azaltmak'} istediğinize emin misiniz?`,
      [
        { text: 'İptal', style: 'cancel' },
        { 
          text: 'Onayla ve Güncelle', 
          onPress: async () => {
            const multiplier = guncellemeYonu === 'Artış' ? (1 + rate / 100) : (1 - rate / 100);
            for (const stok of stoklar) {
              const updatedStok = {
                ...stok,
                satisFiyati: parseFloat(((stok.satisFiyati || 0) * multiplier).toFixed(2))
              };
              const ok = await writeData(`Stoklar/${stok.id}`, updatedStok);
              if (!ok) {
                Alert.alert('Uyarı', `"${stok.stokAdi || stok.id}" fiyatı güncellenemedi. (Bağlantı sorunu — kayıt sıraya alındı.)`);
              }
            }
            setYuzdeOran('');
            Alert.alert('Başarılı', 'Tüm ürün fiyatları başarıyla güncellendi.');
          }
        }
      ]
    );
  };

  // Cari Birleştirme Motoru (Tüm hareketler taşınır)
  const handleCariBirlestir = async () => {
    if (!sourceCari || !targetCari) {
      Alert.alert('Hata', 'Lütfen kaynak ve hedef carileri seçin.');
      return;
    }
    if (sourceCari.id === targetCari.id) {
      Alert.alert('Hata', 'Kaynak ve hedef cari aynı olamaz.');
      return;
    }

    Alert.alert(
      'Cari Birleştirme',
      `DIKKAT! "${sourceCari.unvan}" carisinin tüm borç/alacak ve hareket kayıtları "${targetCari.unvan}" carisine aktarılacak ve kaynak cari silinecektir. Bu işlem geri alınamaz!`,
      [
        { text: 'İptal', style: 'cancel' },
        {
          text: 'Onayla ve Birleştir',
          onPress: async () => {
            const sourceKey = sourceCari.firebaseKey || sourceCari.id;
            const targetKey = targetCari.firebaseKey || targetCari.id;
            const targetUnvan = targetCari.unvan || '';

            // Bir koleksiyondaki ilgili kayıtları taşıyan yardımcı
            const moveRecords = async (resource: string, predicate: (r: any) => boolean, update: (r: any) => any) => {
              try {
                const data = await readData(resource);
                if (!data) return;
                const list = Array.isArray(data)
                  ? data.filter(Boolean)
                  : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
                for (const rec of list.filter(predicate)) {
                  const recKey = rec.firebaseKey || rec.id;
                  const ok = await writeData(`${resource}/${recKey}`, update(rec));
                  if (!ok) {
                    Alert.alert('Uyarı', `Cari birleştirme sırasında "${resource}" kaydı güncellenemedi. (Bağlantı sorunu — kayıt sıraya alındı.)`);
                  }
                }
              } catch (e) {
                console.error(`Cari birleştirme hatası (${resource}):`, e);
              }
            };

            // 1. CariHareketler (cari ve yönlendirilen)
            await moveRecords('CariHareketler', h => h.cariId === sourceCari.id, h => ({ ...h, cariId: targetCari.id, cariUnvan: targetUnvan }));
            await moveRecords('CariHareketler', h => h.yonlendirilenCariId === sourceCari.id, h => ({ ...h, yonlendirilenCariId: targetCari.id, yonlendirilenCariUnvan: targetUnvan }));

            // 2. Faturalar
            await moveRecords('Faturalar', f => f.cariId === sourceCari.id, f => ({ ...f, cariId: targetCari.id }));

            // 3. Siparişler & Teklifler
            await moveRecords('Siparisler', s => s.cariId === sourceCari.id, s => ({ ...s, cariId: targetCari.id, cariUnvan: targetUnvan }));
            await moveRecords('Teklifler', t => t.cariId === sourceCari.id, t => ({ ...t, cariId: targetCari.id, cariUnvan: targetUnvan }));

            // 4. Çek & Senet (cari ve yönlendirilen)
            await moveRecords('Cekler', c => c.cariId === sourceCari.id, c => ({ ...c, cariId: targetCari.id, cariUnvan: targetUnvan }));
            await moveRecords('Cekler', c => c.yonlendirilenCariId === sourceCari.id, c => ({ ...c, yonlendirilenCariId: targetCari.id, yonlendirilenCariUnvan: targetUnvan }));
            await moveRecords('Senetler', s => s.cariId === sourceCari.id, s => ({ ...s, cariId: targetCari.id, cariUnvan: targetUnvan }));

            // 5. Kasa & Banka Hareketleri
            await moveRecords('KasaHareketler', k => k.cariId === sourceCari.id, k => ({ ...k, cariId: targetCari.id, cariUnvan: targetUnvan }));
            await moveRecords('KasaHareketler', k => k.yonlendirilenCariId === sourceCari.id, k => ({ ...k, yonlendirilenCariId: targetCari.id, yonlendirilenCariUnvan: targetUnvan }));
            await moveRecords('BankaHareketler', b => b.cariId === sourceCari.id, b => ({ ...b, cariId: targetCari.id, cariUnvan: targetUnvan }));
            await moveRecords('BankaHareketler', b => b.yonlendirilenCariId === sourceCari.id, b => ({ ...b, yonlendirilenCariId: targetCari.id, yonlendirilenCariUnvan: targetUnvan }));

            // 6. Kredi Kartı & EFT İşlemleri
            await moveRecords('KrediKartlari', k => k.musteriId === sourceCari.id, k => ({ ...k, musteriId: targetCari.id, musteriUnvan: targetUnvan }));
            await moveRecords('KrediKartlari', k => k.yonlendirilenCariId === sourceCari.id, k => ({ ...k, yonlendirilenCariId: targetCari.id, yonlendirilenCariUnvan: targetUnvan }));
            await moveRecords('EftIslemleri', e => e.musteriId === sourceCari.id, e => ({ ...e, musteriId: targetCari.id, musteriUnvan: targetUnvan }));
            await moveRecords('EftIslemleri', e => e.yonlendirilenCariId === sourceCari.id, e => ({ ...e, yonlendirilenCariId: targetCari.id, yonlendirilenCariUnvan: targetUnvan }));

            // 7. Target cari bakiyelerini güncelle
            const updatedTarget = {
              ...targetCari,
              borc: (targetCari.borc || 0) + (sourceCari.borc || 0),
              alacak: (targetCari.alacak || 0) + (sourceCari.alacak || 0),
            };
            const okTarget = await writeData(`Cariler/${targetKey}`, updatedTarget);
            if (!okTarget) {
              Alert.alert('Uyarı', 'Hedef cari bakiyesi güncellenemedi. (Bağlantı sorunu — bakiye sıraya alındı.)');
            }

            // 8. Kaynak cariyi sil
            await deleteData(`Cariler/${sourceKey}`);
            setSourceCari(null);
            setTargetCari(null);
            Alert.alert('Başarılı', 'Cari hesaplar başarıyla birleştirildi.');
          }
        }
      ]
    );
  };

  // Ürün Birleştirme Motoru
  const handleUrunBirlestir = async () => {
    if (!sourceStok || !targetStok) {
      Alert.alert('Hata', 'Lütfen kaynak ve hedef stok kartlarını seçin.');
      return;
    }
    if (sourceStok.id === targetStok.id) {
      Alert.alert('Hata', 'Kaynak ve hedef ürün aynı olamaz.');
      return;
    }

    Alert.alert(
      'Ürün Birleştirme',
      `"${sourceStok.stokAdi}" ürünü "${targetStok.stokAdi}" ile birleştirilecektir. Stok miktarı aktarılıp kaynak ürün silinecektir. Onaylıyor musunuz?`,
      [
        { text: 'İptal', style: 'cancel' },
        {
          text: 'Birleştir',
          onPress: async () => {
            const sourceKey = sourceStok.firebaseKey || sourceStok.id;
            const targetKey = targetStok.firebaseKey || targetStok.id;

            // Stok hareketlerini hedef ürüne taşı
            try {
              const data = await readData('StokHareketler');
              if (data) {
                const list = Array.isArray(data)
                  ? data.filter(Boolean)
                  : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
                for (const h of list.filter(x => x.stokId === sourceStok.id)) {
                  const ok = await writeData(`StokHareketler/${h.firebaseKey || h.id}`, {
                    ...h,
                    stokId: targetStok.id,
                    stokAdi: targetStok.stokAdi,
                    stokKodu: targetStok.stokKodu,
                  });
                  if (!ok) {
                    Alert.alert('Uyarı', 'Ürün birleştirme sırasında stok hareketi güncellenemedi. (Bağlantı sorunu — kayıt sıraya alındı.)');
                  }
                }
              }
            } catch (e) {
              console.error('Stok hareket taşıma hatası:', e);
            }

            const updatedTarget = {
              ...targetStok,
              miktar: (targetStok.miktar || 0) + (sourceStok.miktar || 0)
            };
            const okTargetStok = await writeData(`Stoklar/${targetKey}`, updatedTarget);
            if (!okTargetStok) {
              Alert.alert('Hata', 'Hedef ürün güncellenemedi. (Bağlantı sorunu — kayıt sıraya alındı.)');
              return;
            }
            await deleteData(`Stoklar/${sourceKey}`);
            setSourceStok(null);
            setTargetStok(null);
            Alert.alert('Başarılı', 'Ürün kartları başarıyla birleştirildi.');
          }
        }
      ]
    );
  };

  // Bütçe Hedef Kaydı (masaüstü SatisHedefi şeması: yil/ay/hedefTutari)
  const handleSaveHedef = async (ay: number, yeniHedef: string) => {
    const val = parseFloat(yeniHedef);
    if (isNaN(val) || val < 0) {
      Alert.alert('Hata', 'Lütfen geçerli bir bütçe girin.');
      return;
    }
    setHedefLoader(true);
    const id = selectedHedefYear * 100 + ay;
    try {
      const ok = await writeData(`SatisHedefleri/${id}`, {
        id,
        yil: selectedHedefYear,
        ay,
        hedefTutari: val,
        isDeleted: false
      });
      if (!ok) {
        Alert.alert('Hata', 'Ciro hedefi kaydedilemedi. (Bağlantı sorunu — kayıt sıraya alındı.)');
        return;
      }
      setSelectedEditMonth(null);
      setAylikHedef('');
      Alert.alert('Başarılı', 'Ciro hedefi güncellendi.');
    } catch (e) {
      Alert.alert('Hata', 'Kayıt başarısız oldu.');
    } finally {
      setHedefLoader(false);
    }
  };

  // Gelişmiş Bütçe Dağılım Sihirbazı (12 aya eşit veya geçen yıl trendine göre ağırlıklı)
  const handleAutoDagitim = async () => {
    const total = parseFloat(yillikHedefInput);
    if (isNaN(total) || total <= 0) {
      Alert.alert('Hata', 'Lütfen geçerli bir yıllık hedef tutarı giriniz.');
      return;
    }

    setHedefLoader(true);
    try {
      if (dagitimTuru === 'esit') {
        const aylikPay = Math.round(total / 12);
        for (let m = 1; m <= 12; m++) {
          const id = selectedHedefYear * 100 + m;
          await writeData(`SatisHedefleri/${id}`, {
            id,
            yil: selectedHedefYear,
            ay: m,
            hedefTutari: aylikPay,
            isDeleted: false
          });
        }
      } else {
        // Trend bazlı dağıtım: Bir önceki yılın gerçekleşen satış trendi
        const prevYear = selectedHedefYear - 1;
        const prevInvoices = faturalar.filter(f => {
          const d = new Date(f.tarih || f.Tarih);
          return d.getFullYear() === prevYear && (f.tur === 'Satis' || f.tur === 'Satış' || f.Tur === 'Satis' || f.Tur === 'Satış');
        });
        const prevMonthly = Array.from({ length: 12 }, (_, i) => {
          const m = i + 1;
          return prevInvoices.filter(f => (new Date(f.tarih || f.Tarih).getMonth() + 1) === m)
                             .reduce((sum, f) => sum + (f.genelToplam || f.GenelToplam || 0), 0);
        });
        const prevTotal = prevMonthly.reduce((a, b) => a + b, 0);

        for (let m = 1; m <= 12; m++) {
          const ratio = prevTotal > 0 ? (prevMonthly[m - 1] / prevTotal) : (1 / 12);
          const pay = Math.round(total * ratio);
          const id = selectedHedefYear * 100 + m;
          await writeData(`SatisHedefleri/${id}`, {
            id,
            yil: selectedHedefYear,
            ay: m,
            hedefTutari: pay,
            isDeleted: false
          });
        }
      }

      setIsDagitimModalOpen(false);
      setYillikHedefInput('');
      Alert.alert('Başarılı', `${selectedHedefYear} yılı için 12 aylık bütçe hedefi ${dagitimTuru === 'esit' ? 'eşit olarak' : 'geçen yıl trendine göre'} başarıyla dağıtıldı.`);
    } catch (e) {
      Alert.alert('Hata', 'Hedef dağıtımı sırasında hata oluştu.');
    } finally {
      setHedefLoader(false);
    }
  };

  // Seçili ay için haftalık gerçekleşme ve hedef dökümü
  const getWeeklyBreakdown = (ay: number, ayHedef: number) => {
    const daysInMonth = new Date(selectedHedefYear, ay, 0).getDate();
    const filteredInvoices = faturalar.filter(f => {
      const date = new Date(f.tarih || f.Tarih);
      return date.getFullYear() === selectedHedefYear && 
             (date.getMonth() + 1 === ay) &&
             (f.tur === 'Satis' || f.tur === 'Satış' || f.Tur === 'Satis' || f.Tur === 'Satış');
    });

    const weeks = [
      { num: 1, label: '1. Hafta (1-7)', startDay: 1, endDay: 7 },
      { num: 2, label: '2. Hafta (8-14)', startDay: 8, endDay: 14 },
      { num: 3, label: '3. Hafta (15-21)', startDay: 15, endDay: 21 },
      { num: 4, label: `4. Hafta (22-${daysInMonth})`, startDay: 22, endDay: daysInMonth }
    ];

    const weekTarget = ayHedef > 0 ? Math.round(ayHedef / 4) : 0;
    return weeks.map(w => {
      const actual = filteredInvoices.filter(f => {
        const d = new Date(f.tarih || f.Tarih).getDate();
        return d >= w.startDay && d <= w.endDay;
      }).reduce((sum, f) => sum + (f.genelToplam || f.GenelToplam || 0), 0);
      const pct = weekTarget > 0 ? (actual / weekTarget) * 100 : 0;
      const renk = pct >= 100 ? '#10B981' : (pct >= 70 ? '#3B82F6' : (pct >= 50 ? '#F59E0B' : '#EF4444'));
      return { ...w, target: weekTarget, actual, pct, renk, fark: actual - weekTarget };
    });
  };

  // Gecikme Faizi Hesaplama Formülü
  const calculateFaiz = () => {
    const p = parseFloat(anaPara) || 0;
    const r = parseFloat(faizOrani) || 0;
    const d = parseFloat(gecikmeGunu) || 0;
    const faizTutar = (p * (r / 100) * d) / 365;
    return formatMoney(faizTutar);
  };

  // Kur Çevirici (çapraz kur hesabı)
  const calculateKur = () => {
    const rateMap: Record<string, number> = { TRY: 1, USD: parseFloat(usdRate as any) || 1, EUR: parseFloat(eurRate as any) || 1, GBP: parseFloat(gbpRate as any) || 1 };
    const sonuc = hesapKur({ miktar: parseFloat(kurMiktar) || 0, kaynak: kurKaynak, hedef: kurHedef, kurlar: rateMap });
    return `${sonuc.toLocaleString('tr-TR', { maximumFractionDigits: 2 })} ${kurHedef}`;
  };

  // Optimal Fiyat (desktop OptimalFiyatViewModel mantığı)
  const calculateOptimal = () => hesapOptimalFiyat({
    alisFiyati: parseFloat(optAlisFiyati) || 0,
    ekMaliyet: parseFloat(optEkMaliyet) || 0,
    karOrani: parseFloat(optKarOrani) || 0,
    kdvOrani: parseFloat(optKdvOrani) || 0
  });

  // Evrak Seri / Sıradaki No Güncelleme (desktop EvrakNoDuzenleView şeması)
  const handleSaveSeri = async (item: any) => {
    const siradaki = parseInt(item.siradakiNo, 10) || 0;
    const id = item.firebaseKey || item.id || `${item.evrakTipi}-${generateInt32Id()}`;
    const ok = await writeData(`EvrakSerileri/${id}`, {
      id,
      evrakTipi: item.evrakTipi,
      seri: item.seri,
      siradakiNo: siradaki,
      isDeleted: false
    });
    if (!ok) {
      Alert.alert('Hata', 'Seri ayarları güncellenemedi. (Bağlantı sorunu — kayıt sıraya alındı.)');
      return;
    }
    Alert.alert('Başarılı', `${item.evrakTipi} seri ayarları güncellendi.`);
  };

  // CRM Fırsat Ekle/Kaydet
  const handleSavePortfoy = async () => {
    if (!leadFirma || !leadYetkili) {
      Alert.alert('Hata', 'Lütfen firma ve yetkili kişi bilgilerini girin.');
      return;
    }
    const id = editingPortfoyId || generateInt32Id().toString();
    const payload = {
      id,
      firma: leadFirma,
      yetkili: leadYetkili,
      gsm: leadGsm,
      il: leadIl,
      tutar: parseFloat(leadTutar) || 0,
      aciklama: leadAciklama
    };
    const okPortfoy = await writeData(`PortfoyKartlari/${id}`, payload);
    if (!okPortfoy) {
      Alert.alert('Hata', 'Portföy kartı kaydedilemedi. (Bağlantı sorunu — kayıt sıraya alındı.)');
      return;
    }
    setIsPortfoyModalOpen(false);
    setEditingPortfoyId(null);
    setLeadFirma('');
    setLeadYetkili('');
    setLeadGsm('');
    setLeadIl('');
    setLeadTutar('0');
    setLeadAciklama('');
    Alert.alert('Başarılı', 'Portföy kartı kaydedildi.');
  };

  // Belge Arşivi — Gerçek dosya yükleme (expo-image-picker → base64)
  const handlePickDoc = async () => {
    try {
      const { status } = await ImagePicker.requestMediaLibraryPermissionsAsync();
      if (status !== 'granted') {
        Alert.alert('İzin Gerekli', 'Galeri erişimi için izin gerekiyor.');
        return;
      }
      const result = await ImagePicker.launchImageLibraryAsync({
        mediaTypes: ImagePicker.MediaTypeOptions.Images,
        quality: 0.6,
        base64: true,
      });
      if (!result.canceled && result.assets && result.assets.length > 0) {
        const asset = result.assets[0];
        setDocDataUri(`data:${asset.mimeType || 'image/jpeg'};base64,${asset.base64}`);
        setDocFileName(asset.fileName || `belge_${Date.now()}.jpg`);
        setDocFileSize(asset.fileSize || 0);
        if (!docAd) setDocAd(asset.fileName || 'Belge');
      }
    } catch (e) {
      Alert.alert('Hata', 'Dosya seçilemedi.');
    }
  };

  // Belge Arşivi Kaydetme (gerçek görsel verisi)
  const handleSaveDoc = async () => {
    if (!docAd) {
      Alert.alert('Hata', 'Lütfen belge adı girin.');
      return;
    }
    if (!docDataUri) {
      Alert.alert('Uyarı', 'Lütfen önce galeriden bir görsel seçin.');
      return;
    }
    const id = generateInt32Id().toString();
    const payload = {
      id,
      name: docAd,
      kategori: docKategori,
      size: docFileSize > 0 ? `${(docFileSize / 1024).toFixed(1)} KB` : '— KB',
      type: 'image',
      date: new Date().toLocaleString('tr-TR'),
      data: docAciklama,
      dataUri: docDataUri,
      isDeleted: false
    };
    const okBelge = await writeData(`BelgeArsiv/${id}`, payload);
    if (!okBelge) {
      Alert.alert('Hata', 'Belge arşive kaydedilemedi. (Bağlantı sorunu — kayıt sıraya alındı.)');
      return;
    }
    setIsDocFormOpen(false);
    setDocAd('');
    setDocAciklama('');
    setDocDataUri(null);
    setDocFileName('');
    setDocFileSize(0);
    Alert.alert('Başarılı', 'Belge arşive kaydedildi.');
  };

  const handleShareDoc = async (doc: any) => {
    try {
      if (doc.dataUri && typeof doc.dataUri === 'string' && doc.dataUri.startsWith('data:image')) {
        const mime = doc.dataUri.split(';')[0].split(':')[1] || 'image/jpeg';
        const base64 = doc.dataUri.split(',')[1];
        const fileUri = `${(FileSystem as any).cacheDirectory}${(doc.name || 'belge').replace(/[^a-zA-Z0-9.]/g, '_')}`;
        await FileSystem.writeAsStringAsync(fileUri, base64, {
          encoding: (FileSystem as any).EncodingType.Base64,
        });
        await Share.share({ url: fileUri, message: `${doc.name}\n\n${doc.data || ''}` });
        return;
      }
      await Share.share({
        message: `${doc.name} - Kategori: ${doc.kategori}\n\nNot: ${doc.data}`,
      });
    } catch (e) {
      Alert.alert('Hata', 'Paylaşım yapılamadı.');
    }
  };

  const toolsList = [
    { id: 'sistem', title: 'Sistem Sağlığı ve Bakımı', description: 'Sistem durumu izleme, temizlik ve yedekleme.', icon: Activity, color: '#10B981' },
    { id: 'belge', title: 'Belge Arşivleme', description: 'Dijital evrak saklama.', icon: Folder, color: '#8B5CF6' },
    { id: 'kurlar', title: 'Döviz Kurları & Otomasyon', description: 'Canlı kur ekranı ve otomatik çekme ayarları.', icon: Landmark, color: '#F59E0B' },
    { id: 'cariBir', title: 'Cari Birleştirme', description: 'Mükerrer cari kartları birleştir.', icon: Users, color: '#3B82F6' },
    { id: 'urunBir', title: 'Ürün Birleştirme', description: 'Mükerrer kartları birleştir.', icon: Layers, color: '#EC4899' },
    { id: 'faiz', title: 'Gecikme Faizi', description: 'Faiz hesaplama aracı.', icon: Percent, color: '#EF4444' },
    { id: 'risk', title: 'Risk Puanlayıcı', description: 'Ödeme analizi paneli.', icon: AlertTriangle, color: '#F59E0B' },
    { id: 'fiyat', title: 'Toplu Fiyat Güncelleme', description: 'Tüm ürünlere toplu zam/indirim.', icon: TrendingUp, color: '#10B981' },
    { id: 'limit', title: 'Limit Yönetimi', description: 'Risk ve kredi limitleri.', icon: Lock, color: '#EF4444' },
    { id: 'hedef', title: 'Bütçe Planlama Merkezi', description: 'Yıllık, aylık ve haftalık hedef yönetimi.', icon: Target, color: '#60A5FA' },
    { id: 'portfoy', title: 'Portföy Listesi', description: 'Varlık yönetimi paneli.', icon: Briefcase, color: '#8B5CF6' }
  ];

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.header}>
        <View style={{ flexDirection: 'row', alignItems: 'center' }}>
          <Wrench color="#0061FF" size={28} style={{ marginRight: 12 }} />
          <Text style={styles.headerTitle}>
            {activeTab ? toolsList.find(t => t.id === activeTab)?.title : 'Araçlar ve Modüller'}
          </Text>
        </View>
        <Text style={styles.headerSubtitle}>
          {activeTab ? toolsList.find(t => t.id === activeTab)?.description : 'İşletmenizin dijital dönüşümünde size yardımcı olacak interaktif araçlar.'}
        </Text>
      </View>

      <ScrollView contentContainerStyle={{ padding: 20, paddingBottom: 40 }}>
        {activeTab === null ? (
          <View style={styles.categoriesGrid}>
            {toolsList.map((tool) => {
              const Icon = tool.icon;
              return (
                <TouchableOpacity
                  key={tool.id}
                  style={styles.categoryCard}
                  onPress={() => setActiveTab(tool.id as any)}
                >
                  <View style={[styles.categoryIconBox, { backgroundColor: `${tool.color}15` }]}>
                    <Icon color={tool.color} size={24} />
                  </View>
                  <Text style={styles.categoryTitle}>{tool.title}</Text>
                  <Text style={styles.categoryDesc} numberOfLines={2}>{tool.description}</Text>
                </TouchableOpacity>
              );
            })}
          </View>
        ) : (
          <>
            {/* Geri Dön Butonu */}
            <TouchableOpacity onPress={() => setActiveTab(null)} style={styles.backButtonHeader}>
              <ArrowLeft color="#E2E8F0" size={16} />
              <Text style={styles.backButtonText}>Araçlar Listesine Dön</Text>
            </TouchableOpacity>
        {/* Tab 1: Canlı Kurlar */}
        {activeTab === 'kurlar' && (
          <View style={styles.card}>
            <View style={styles.cardHeader}>
              <Landmark color="#F59E0B" size={22} />
              <Text style={styles.cardTitle}>Döviz Otomasyonu (Canlı)</Text>
            </View>
            <View style={styles.kurRow}>
              <Text style={styles.kurLabel}>USD / TRY</Text>
              <Text style={styles.kurVal}>{usdRate} ₺</Text>
            </View>
            <View style={styles.kurRow}>
              <Text style={styles.kurLabel}>EUR / TRY</Text>
              <Text style={styles.kurVal}>{eurRate} ₺</Text>
            </View>
            <View style={styles.kurRow}>
              <Text style={styles.kurLabel}>GBP / TRY</Text>
              <Text style={styles.kurVal}>{gbpRate} ₺</Text>
            </View>
            <Text style={[styles.kurLabel, { marginTop: 4, fontSize: 11, color: '#64748B' }]}>Son güncelleme: {sonKurGuncelleme}</Text>
            <TouchableOpacity style={styles.btnPremium} onPress={handleRefreshKurlar} disabled={kurLoader}>
              {kurLoader ? <ActivityIndicator size="small" color="#FFF" /> : <Text style={styles.btnText}>Kurları Güncelle (TCMB)</Text>}
            </TouchableOpacity>
          </View>
        )}

        {/* Tab 2: Toplu Fiyat */}
        {activeTab === 'fiyat' && (
          <View style={styles.card}>
            <View style={styles.cardHeader}>
              <TrendingUp color="#10B981" size={22} />
              <Text style={styles.cardTitle}>Toplu Fiyat Zam / İndirim</Text>
            </View>
            <Text style={styles.label}>Güncelleme Yönü</Text>
            <View style={styles.segmentRow}>
              {['Artış', 'Azalış'].map(d => (
                <TouchableOpacity key={d} style={[styles.segmentBtn, guncellemeYonu === d && styles.segmentBtnActive]} onPress={() => setGuncellemeYonu(d as any)}>
                  <Text style={[styles.segmentBtnText, guncellemeYonu === d && styles.segmentBtnTextActive]}>{d === 'Artış' ? 'Zam (Artış)' : 'İndirim (Azalış)'}</Text>
                </TouchableOpacity>
              ))}
            </View>
            <Text style={styles.label}>Yüzde Oranı (%)</Text>
            <TextInput
              style={styles.input}
              keyboardType="numeric"
              inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
              placeholder="Örn: 15"
              placeholderTextColor="#64748B"
              value={yuzdeOran}
              onChangeText={setYuzdeOran}
            />
            <TouchableOpacity style={[styles.btnPremium, { backgroundColor: '#10B981', marginTop: 16 }]} onPress={handleUpdatePrices}>
              <Text style={styles.btnText}>Fiyatları Toplu Güncelle</Text>
            </TouchableOpacity>
          </View>
        )}

        {/* Tab 3: Ciro Hedefi (Bütçe Planlama - Gelişmiş Dağılım) */}
        {activeTab === 'hedef' && (() => {
          const yillikToplamHedef = aylikHedefler.reduce((sum, item) => sum + (item.hedef || 0), 0);
          const yillikToplamGerceklesen = aylikHedefler.reduce((sum, item) => sum + (item.gerceklesen || 0), 0);
          const yillikOran = yillikToplamHedef > 0 ? (yillikToplamGerceklesen / yillikToplamHedef) * 100 : 0;
          const yillikFark = yillikToplamGerceklesen - yillikToplamHedef;

          return (
            <View style={styles.card}>
              <View style={styles.cardHeader}>
                <Target color="#3B82F6" size={22} />
                <Text style={styles.cardTitle}>Bütçe Planlama & Satış Hedefi</Text>
              </View>

              {/* Yıl Seçimi & Otomatik Dağılım Butonu */}
              <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16 }}>
                <View style={styles.yearSelectorContainer}>
                  <TouchableOpacity 
                    style={styles.yearArrow} 
                    onPress={() => setSelectedHedefYear(prev => prev - 1)}
                  >
                    <Text style={styles.yearArrowText}>{"<"}</Text>
                  </TouchableOpacity>
                  <Text style={styles.yearText}>{selectedHedefYear}</Text>
                  <TouchableOpacity 
                    style={styles.yearArrow} 
                    onPress={() => setSelectedHedefYear(prev => prev + 1)}
                  >
                    <Text style={styles.yearArrowText}>{">"}</Text>
                  </TouchableOpacity>
                </View>

                <TouchableOpacity 
                  style={[styles.btnPremium, { backgroundColor: '#2563EB', paddingHorizontal: 12, paddingVertical: 8, flexDirection: 'row', alignItems: 'center' }]}
                  onPress={() => setIsDagitimModalOpen(true)}
                >
                  <Sparkles color="#FFF" size={16} style={{ marginRight: 6 }} />
                  <Text style={[styles.btnText, { fontSize: 12 }]}>Dağılım Sihirbazı</Text>
                </TouchableOpacity>
              </View>

              {/* Yıllık Özet KPI Kartları */}
              <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginBottom: 16 }}>
                <View style={[styles.kpiBox, { width: '48%', borderLeftColor: '#3B82F6' }]}>
                  <Text style={styles.kpiBoxTitle}>Yıllık Hedef</Text>
                  <Text style={[styles.kpiBoxVal, { color: '#3B82F6' }]}>{formatMoney(yillikToplamHedef)}</Text>
                </View>
                <View style={[styles.kpiBox, { width: '48%', borderLeftColor: '#10B981' }]}>
                  <Text style={styles.kpiBoxTitle}>Yıllık Gerçekleşen</Text>
                  <Text style={[styles.kpiBoxVal, { color: '#10B981' }]}>{formatMoney(yillikToplamGerceklesen)}</Text>
                </View>
                <View style={[styles.kpiBox, { width: '48%', borderLeftColor: yillikOran >= 100 ? '#10B981' : (yillikOran >= 70 ? '#3B82F6' : '#EF4444') }]}>
                  <Text style={styles.kpiBoxTitle}>Hedef Gerçekleşme</Text>
                  <Text style={[styles.kpiBoxVal, { color: yillikOran >= 100 ? '#10B981' : (yillikOran >= 70 ? '#3B82F6' : '#EF4444') }]}>
                    %{yillikOran.toFixed(1)}
                  </Text>
                </View>
                <View style={[styles.kpiBox, { width: '48%', borderLeftColor: yillikFark >= 0 ? '#10B981' : '#EF4444' }]}>
                  <Text style={styles.kpiBoxTitle}>Bütçe Sapması (Fark)</Text>
                  <Text style={[styles.kpiBoxVal, { color: yillikFark >= 0 ? '#10B981' : '#EF4444' }]}>
                    {yillikFark >= 0 ? '+' : ''}{formatMoney(yillikFark)}
                  </Text>
                </View>
              </View>

              {/* 12 Ay Listesi */}
              <ScrollView style={styles.hedefListScroll} nestedScrollEnabled={true}>
                {aylikHedefler.map((item) => {
                  const isExpanded = expandedWeeksMonth === item.ay;
                  const weeklyData = isExpanded ? getWeeklyBreakdown(item.ay, item.hedef) : [];

                  return (
                    <View key={item.ay} style={styles.hedefRowContainer}>
                      <View style={styles.hedefRowHeader}>
                        <Text style={styles.hedefMonthName}>{item.ayAdi}</Text>
                        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
                          <TouchableOpacity 
                            onPress={() => setExpandedWeeksMonth(isExpanded ? null : item.ay)}
                            style={[styles.editIconBtn, { backgroundColor: isExpanded ? '#3B82F625' : 'transparent' }]}
                          >
                            <Text style={[styles.editIconBtnText, { color: '#60A5FA' }]}>
                              {isExpanded ? 'Haftaları Gizle' : 'Haftalık'}
                            </Text>
                            {isExpanded ? <ChevronUp color="#60A5FA" size={14} style={{ marginLeft: 2 }} /> : <ChevronDown color="#60A5FA" size={14} style={{ marginLeft: 2 }} />}
                          </TouchableOpacity>
                          <TouchableOpacity 
                            onPress={() => {
                              setSelectedEditMonth(item.ay);
                              setAylikHedef(item.hedef > 0 ? String(item.hedef) : '');
                            }}
                            style={styles.editIconBtn}
                          >
                            <Text style={styles.editIconBtnText}>Düzenle</Text>
                          </TouchableOpacity>
                        </View>
                      </View>

                      {/* Detay Bilgileri */}
                      <View style={styles.hedefDetailsRow}>
                        <Text style={styles.hedefDetailVal}>
                          Hedef: <Text style={{fontWeight: 'bold', color: '#FFF'}}>{formatMoney(item.hedef)}</Text>
                        </Text>
                        <Text style={styles.hedefDetailVal}>
                          Gerçekleşen: <Text style={{fontWeight: 'bold', color: '#FFF'}}>{formatMoney(item.gerceklesen)}</Text>
                        </Text>
                      </View>
                      <View style={[styles.hedefDetailsRow, { marginTop: 2 }]}>
                        <Text style={styles.hedefDetailVal}>
                          Fark: <Text style={{fontWeight: 'bold', color: item.fark >= 0 ? '#10B981' : '#EF4444'}}>
                            {item.fark >= 0 ? '+' : ''}{formatMoney(item.fark)}
                          </Text>
                        </Text>
                      </View>

                      {/* İlerleme Çubuğu */}
                      <View style={styles.progressContainer}>
                        <View style={styles.progressBarBg}>
                          <View 
                            style={[
                              styles.progressBarFill, 
                              { 
                                width: `${Math.min(item.yuzde, 100)}%`, 
                                backgroundColor: item.renk 
                              }
                            ]} 
                          />
                        </View>
                        <Text style={[styles.progressPct, { color: item.renk }]}>
                          %{item.yuzde.toFixed(1)}
                        </Text>
                      </View>

                      {/* Haftalık Kırılım (ISO Weeks) */}
                      {isExpanded && (
                        <View style={{ backgroundColor: '#0B1120', borderRadius: 10, padding: 10, marginVertical: 8, borderWidth: 1, borderColor: '#1E293B' }}>
                          <Text style={{ color: '#93C5FD', fontSize: 11, fontWeight: '700', marginBottom: 8 }}>
                            📅 {item.ayAdi} Ayı Haftalık Dağılım & Gerçekleşme
                          </Text>
                          {weeklyData.map((w) => (
                            <View key={w.num} style={{ marginBottom: 8, paddingBottom: 6, borderBottomWidth: 1, borderBottomColor: '#1E293B' }}>
                              <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginBottom: 3 }}>
                                <Text style={{ color: '#CBD5E1', fontSize: 11, fontWeight: '600' }}>{w.label}</Text>
                                <Text style={{ color: w.renk, fontSize: 11, fontWeight: '700' }}>%{w.pct.toFixed(0)}</Text>
                              </View>
                              <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                                <Text style={{ color: '#64748B', fontSize: 10 }}>Hedef: {formatMoney(w.target)}</Text>
                                <Text style={{ color: '#E2E8F0', fontSize: 10, fontWeight: '600' }}>Gerçekleşen: {formatMoney(w.actual)}</Text>
                              </View>
                            </View>
                          ))}
                        </View>
                      )}

                      {/* Seçili Ay Düzenleme Formu */}
                      {selectedEditMonth === item.ay && (
                        <View style={styles.editTargetForm}>
                          <Text style={styles.editTargetFormLabel}>{item.ayAdi} Ayı Ciro Hedefi (₺)</Text>
                          <View style={styles.editTargetFormRow}>
                            <TextInput
                              style={[styles.input, { flex: 1, marginBottom: 0 }]}
                              keyboardType="numeric"
                              inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                              placeholder="Hedef ciro tutarı..."
                              placeholderTextColor="#64748B"
                              value={aylikHedef}
                              onChangeText={setAylikHedef}
                            />
                            <TouchableOpacity 
                              style={[styles.btnPremium, { backgroundColor: '#10B981', marginLeft: 8, paddingVertical: 12 }]} 
                              onPress={() => handleSaveHedef(item.ay, aylikHedef)}
                              disabled={hedefLoader}
                            >
                              {hedefLoader ? <ActivityIndicator size="small" color="#FFF" /> : <Text style={styles.btnText}>Kaydet</Text>}
                            </TouchableOpacity>
                            <TouchableOpacity 
                              style={[styles.btnPremium, { backgroundColor: '#64748B', marginLeft: 8, paddingVertical: 12 }]} 
                              onPress={() => setSelectedEditMonth(null)}
                            >
                              <Text style={styles.btnText}>İptal</Text>
                            </TouchableOpacity>
                          </View>
                        </View>
                      )}
                      
                      <View style={styles.rowDivider} />
                    </View>
                  );
                })}
              </ScrollView>

              {/* Gelişmiş Dağılım Sihirbazı Modalı */}
              <Modal visible={isDagitimModalOpen} transparent animationType="slide">
                <View style={styles.modalOverlay}>
                  <View style={[styles.modalContent, { maxHeight: 520, borderRadius: 16 }]}>
                    <View style={styles.modalHeader}>
                      <View style={{ flexDirection: 'row', alignItems: 'center' }}>
                        <Sparkles color="#3B82F6" size={20} style={{ marginRight: 8 }} />
                        <Text style={styles.modalTitle}>Bütçe Dağılım Sihirbazı</Text>
                      </View>
                      <TouchableOpacity onPress={() => setIsDagitimModalOpen(false)}>
                        <X color="#FFF" size={22} />
                      </TouchableOpacity>
                    </View>

                    <Text style={{ color: '#94A3B8', fontSize: 12, marginBottom: 14 }}>
                      {selectedHedefYear} mali yılı için yıllık toplam satış hedefinizi belirleyin ve 12 aya otomatik paylaştırın.
                    </Text>

                    <Text style={styles.label}>Yıllık Toplam Ciro Hedefi (₺)</Text>
                    <TextInput 
                      style={styles.input} 
                      keyboardType="numeric" 
                      inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                      placeholder="Örn: 2400000" 
                      placeholderTextColor="#64748B"
                      value={yillikHedefInput} 
                      onChangeText={setYillikHedefInput} 
                    />

                    <Text style={[styles.label, { marginTop: 12 }]}>Dağıtım Yöntemi</Text>
                    <View style={styles.segmentRow}>
                      <TouchableOpacity 
                        style={[styles.segmentBtn, dagitimTuru === 'esit' && styles.segmentBtnActive]}
                        onPress={() => setDagitimTuru('esit')}
                      >
                        <Text style={[styles.segmentBtnText, dagitimTuru === 'esit' && styles.segmentBtnTextActive]}>
                          12 Aya Eşit
                        </Text>
                      </TouchableOpacity>
                      <TouchableOpacity 
                        style={[styles.segmentBtn, dagitimTuru === 'trend' && styles.segmentBtnActive]}
                        onPress={() => setDagitimTuru('trend')}
                      >
                        <Text style={[styles.segmentBtnText, dagitimTuru === 'trend' && styles.segmentBtnTextActive]}>
                          Geçen Yıl Trendine Göre
                        </Text>
                      </TouchableOpacity>
                    </View>

                    <Text style={{ color: '#64748B', fontSize: 11, marginTop: 8, marginBottom: 16 }}>
                      {dagitimTuru === 'esit' 
                        ? 'Toplam hedef 12 aya eşit olarak paylaştırılır.'
                        : `Bir önceki yılın (${selectedHedefYear - 1}) gerçekleşen satış ağırlıklarına göre mevsimsel oranlarda dağıtılır.`}
                    </Text>

                    <TouchableOpacity 
                      style={[styles.btnPremium, { backgroundColor: '#10B981', paddingVertical: 14 }]}
                      onPress={handleAutoDagitim}
                      disabled={hedefLoader}
                    >
                      {hedefLoader ? <ActivityIndicator color="#FFF" /> : <Text style={styles.btnText}>Dağıtımı Başlat ve Kaydet</Text>}
                    </TouchableOpacity>
                  </View>
                </View>
              </Modal>
            </View>
          );
        })()}

        {/* Tab 4: Cari Birleştir */}
        {activeTab === 'cariBir' && (
          <View style={styles.card}>
            <View style={styles.cardHeader}>
              <Users color="#8B5CF6" size={22} />
              <Text style={styles.cardTitle}>Mükerrer Cari Birleştirme</Text>
            </View>
            <Text style={styles.label}>Kaynak Cari (Silinecek ve Aktarılacak)</Text>
            <TouchableOpacity style={styles.selector} onPress={() => { setCariSelectorType('source'); setIsCariOverlayOpen(true); }}>
              <Text style={styles.selectorText}>{sourceCari ? sourceCari.unvan : 'Cari Seçin...'}</Text>
            </TouchableOpacity>

            <Text style={styles.label}>Hedef Cari (Birleştirilecek Ana Kart)</Text>
            <TouchableOpacity style={styles.selector} onPress={() => { setCariSelectorType('target'); setIsCariOverlayOpen(true); }}>
              <Text style={styles.selectorText}>{targetCari ? targetCari.unvan : 'Cari Seçin...'}</Text>
            </TouchableOpacity>

            <TouchableOpacity style={[styles.btnPremium, { backgroundColor: '#8B5CF6', marginTop: 20 }]} onPress={handleCariBirlestir}>
              <Text style={styles.btnText}>Cari Hesapları Birleştir</Text>
            </TouchableOpacity>
          </View>
        )}

        {/* Tab 5: Ürün Birleştir */}
        {activeTab === 'urunBir' && (
          <View style={styles.card}>
            <View style={styles.cardHeader}>
              <Layers color="#EC4899" size={22} />
              <Text style={styles.cardTitle}>Mükerrer Ürün Birleştirme</Text>
            </View>
            <Text style={styles.label}>Kaynak Ürün (Miktarı Aktarılacak ve Silinecek)</Text>
            <TouchableOpacity style={styles.selector} onPress={() => { setStokSelectorType('source'); setIsStokOverlayOpen(true); }}>
              <Text style={styles.selectorText}>{sourceStok ? sourceStok.stokAdi : 'Ürün Seçin...'}</Text>
            </TouchableOpacity>

            <Text style={styles.label}>Hedef Ürün (Ana Kart)</Text>
            <TouchableOpacity style={styles.selector} onPress={() => { setStokSelectorType('target'); setIsStokOverlayOpen(true); }}>
              <Text style={styles.selectorText}>{targetStok ? targetStok.stokAdi : 'Ürün Seçin...'}</Text>
            </TouchableOpacity>

            <TouchableOpacity style={[styles.btnPremium, { backgroundColor: '#EC4899', marginTop: 20 }]} onPress={handleUrunBirlestir}>
              <Text style={styles.btnText}>Ürün Kartlarını Birleştir</Text>
            </TouchableOpacity>
          </View>
        )}

        {/* Tab 6: Sistem Sağlığı */}
        {activeTab === 'sistem' && (
          <View style={styles.card}>
            <View style={styles.cardHeader}>
              <Activity color="#10B981" size={22} />
              <Text style={styles.cardTitle}>Sistem Sağlığı & Cihaz Bilgileri</Text>
            </View>
            <View style={styles.kurRow}>
              <Text style={styles.kurLabel}>Cihaz</Text>
              <Text style={[styles.kurVal, { color: '#00FF87' }]}>{deviceModel}</Text>
            </View>
            <View style={styles.kurRow}>
              <Text style={styles.kurLabel}>İşletim Sistemi</Text>
              <Text style={styles.kurVal}>{deviceOs || '-'}</Text>
            </View>
            <View style={styles.kurRow}>
              <Text style={styles.kurLabel}>Ortam</Text>
              <Text style={styles.kurVal}>{isRealDevice ? 'Gerçek Cihaz' : 'Simülatör / Emülatör'}</Text>
            </View>
            <View style={styles.kurRow}>
              <Text style={styles.kurLabel}>Toplam RAM</Text>
              <Text style={styles.kurVal}>{totalRam > 0 ? `${(totalRam / 1024 / 1024 / 1024).toFixed(1)} GB` : 'Bilinmiyor'}</Text>
            </View>
            <View style={styles.kurRow}>
              <Text style={styles.kurLabel}>Cihaz Uptime</Text>
              <Text style={styles.kurVal}>{deviceUptime > 0 ? formatUptime(deviceUptime) : 'Ölçülüyor...'}</Text>
            </View>
            <View style={styles.kurRow}>
              <Text style={styles.kurLabel}>Uygulama Oturumu</Text>
              <Text style={styles.kurVal}>{formatUptime(appUptime)}</Text>
            </View>
          </View>
        )}

        {/* Tab 7: Gecikme Faizi */}
        {activeTab === 'faiz' && (
          <View style={styles.card}>
            <View style={styles.cardHeader}>
              <Percent color="#EF4444" size={22} />
              <Text style={styles.cardTitle}>Gecikme Faizi Hesaplayıcı</Text>
            </View>
            <Text style={styles.label}>Ana Para (₺)</Text>
            <TextInput style={styles.input} keyboardType="numeric" inputAccessoryViewID={KEYBOARD_ACCESSORY_ID} value={anaPara} onChangeText={setAnaPara} />
            <Text style={styles.label}>Yıllık Faiz Oranı (%)</Text>
            <TextInput style={styles.input} keyboardType="numeric" inputAccessoryViewID={KEYBOARD_ACCESSORY_ID} value={faizOrani} onChangeText={setFaizOrani} />
            <Text style={styles.label}>Gecikme Günü</Text>
            <TextInput style={styles.input} keyboardType="numeric" inputAccessoryViewID={KEYBOARD_ACCESSORY_ID} value={gecikmeGunu} onChangeText={setGecikmeGunu} />
            <View style={[styles.kurRow, { marginTop: 16 }]}>
              <Text style={[styles.kurLabel, { fontWeight: 'bold' }]}>Toplam Faiz Yükü:</Text>
              <Text style={[styles.kurVal, { color: '#EF4444', fontWeight: 'bold', fontSize: 16 }]}>{calculateFaiz()}</Text>
            </View>
          </View>
        )}

        {/* Tab 8: Risk Puanı */}
        {activeTab === 'risk' && (
          <View style={styles.card}>
            <View style={styles.cardHeader}>
              <AlertTriangle color="#F59E0B" size={22} />
              <Text style={styles.cardTitle}>Cari Risk Analiz Karnesi</Text>
            </View>
            {riskList.map((c, idx) => (
              <View key={idx} style={styles.kurRow}>
                <Text style={styles.kurLabel}>{c.unvan}</Text>
                <Text style={[styles.kurVal, { color: c.riskScore >= 65 ? '#EF4444' : '#10B981' }]}>{c.status} ({c.riskScore} Puan)</Text>
              </View>
            ))}
          </View>
        )}

        {/* Tab 9: CRM Portföy */}
        {activeTab === 'portfoy' && (
          <View style={{ gap: 10 }}>
            <TouchableOpacity style={styles.addButton} onPress={() => setIsPortfoyModalOpen(true)}>
              <Plus color="#FFF" size={20} />
              <Text style={styles.addButtonText}>Yeni Fırsat Ekle</Text>
            </TouchableOpacity>

            {portfoyler.map((p, idx) => (
              <View key={idx} style={styles.card}>
                <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                  <Text style={styles.cardTitle}>{p.firma}</Text>
                  <Text style={{ color: '#00FF87', fontWeight: 'bold' }}>{formatMoney(p.tutar)}</Text>
                </View>
                <Text style={{ color: '#94A3B8', fontSize: 12, marginTop: 4 }}>Yetkili: {p.yetkili} • GSM: {p.gsm}</Text>
                <Text style={{ color: '#64748B', fontSize: 11, marginTop: 2 }}>{p.aciklama}</Text>
              </View>
            ))}
          </View>
        )}

        {/* Tab 10: Belge Arşivi */}
        {activeTab === 'belge' && (
          <View style={{ gap: 10 }}>
            <TouchableOpacity style={styles.addButton} onPress={() => setIsDocFormOpen(true)}>
              <Plus color="#FFF" size={20} />
              <Text style={styles.addButtonText}>Yeni Belge Yükle</Text>
            </TouchableOpacity>

            {belgeler.map((doc, idx) => (
              <View key={idx} style={styles.card}>
                <View style={{ flexDirection: 'row', alignItems: 'center' }}>
                  {doc.dataUri && typeof doc.dataUri === 'string' && doc.dataUri.startsWith('data:image') ? (
                    <Image source={{ uri: doc.dataUri }} style={{ width: 52, height: 52, borderRadius: 8, marginRight: 12 }} />
                  ) : (
                    <View style={{ width: 52, height: 52, borderRadius: 8, backgroundColor: 'rgba(255,255,255,0.05)', marginRight: 12, alignItems: 'center', justifyContent: 'center' }}>
                      <Paperclip color="#0061FF" size={22} />
                    </View>
                  )}
                  <View style={{ flex: 1 }}>
                    <Text style={styles.cardTitle}>{doc.name}</Text>
                    <Text style={{ color: '#94A3B8', fontSize: 11, marginTop: 2 }}>Kategori: {doc.kategori} • {doc.size} • Tarih: {doc.date}</Text>
                  </View>
                  <TouchableOpacity onPress={() => handleShareDoc(doc)} style={{ padding: 8 }}>
                    <Share2 color="#0061FF" size={20} />
                  </TouchableOpacity>
                </View>
              </View>
            ))}
          </View>
        )}

        {/* Tab 11: Kur Çevirici */}
        {activeTab === 'kur' && (
          <View style={styles.card}>
            <View style={styles.cardHeader}>
              <Landmark color="#0061FF" size={22} />
              <Text style={styles.cardTitle}>Döviz Çevirici (Çapraz Kur)</Text>
            </View>
            <Text style={styles.label}>Miktar</Text>
            <TextInput style={styles.input} keyboardType="numeric" inputAccessoryViewID={KEYBOARD_ACCESSORY_ID} placeholder="100" placeholderTextColor="#64748B" value={kurMiktar} onChangeText={setKurMiktar} />
            <Text style={styles.label}>Kaynak Birim</Text>
            <View style={styles.segmentRow}>
              {['TRY', 'USD', 'EUR', 'GBP'].map(d => (
                <TouchableOpacity key={d} style={[styles.segmentBtn, kurKaynak === d && styles.segmentBtnActive]} onPress={() => setKurKaynak(d as any)}>
                  <Text style={[styles.segmentBtnText, kurKaynak === d && styles.segmentBtnTextActive]}>{d}</Text>
                </TouchableOpacity>
              ))}
            </View>
            <Text style={styles.label}>Hedef Birim</Text>
            <View style={styles.segmentRow}>
              {['TRY', 'USD', 'EUR', 'GBP'].map(d => (
                <TouchableOpacity key={d} style={[styles.segmentBtn, kurHedef === d && styles.segmentBtnActive]} onPress={() => setKurHedef(d as any)}>
                  <Text style={[styles.segmentBtnText, kurHedef === d && styles.segmentBtnTextActive]}>{d}</Text>
                </TouchableOpacity>
              ))}
            </View>
            <View style={[styles.kurRow, { marginTop: 16 }]}>
              <Text style={[styles.kurLabel, { fontWeight: 'bold' }]}>Sonuç:</Text>
              <Text style={[styles.kurVal, { color: '#10B981', fontWeight: 'bold', fontSize: 17 }]}>{calculateKur()}</Text>
            </View>
            <Text style={[styles.kurLabel, { marginTop: 8, fontSize: 11, color: '#64748B' }]}>
              Kurlar TCMB güncel değerleridir. Son güncelleme: {sonKurGuncelleme}
            </Text>
          </View>
        )}

        {/* Tab 12: Optimal Fiyat */}
        {activeTab === 'optimal' && (
          <View style={styles.card}>
            <View style={styles.cardHeader}>
              <TrendingUp color="#8B5CF6" size={22} />
              <Text style={styles.cardTitle}>Optimal Satış Fiyatı Önerisi</Text>
            </View>
            <Text style={styles.label}>Alış Fiyatı (₺)</Text>
            <TextInput style={styles.input} keyboardType="numeric" inputAccessoryViewID={KEYBOARD_ACCESSORY_ID} value={optAlisFiyati} onChangeText={setOptAlisFiyati} />
            <Text style={styles.label}>Ek Maliyet (₺)</Text>
            <TextInput style={styles.input} keyboardType="numeric" inputAccessoryViewID={KEYBOARD_ACCESSORY_ID} value={optEkMaliyet} onChangeText={setOptEkMaliyet} />
            <Text style={styles.label}>Hedef Kâr Oranı (%)</Text>
            <TextInput style={styles.input} keyboardType="numeric" inputAccessoryViewID={KEYBOARD_ACCESSORY_ID} value={optKarOrani} onChangeText={setOptKarOrani} />
            <Text style={styles.label}>KDV Oranı (%)</Text>
            <TextInput style={styles.input} keyboardType="numeric" inputAccessoryViewID={KEYBOARD_ACCESSORY_ID} value={optKdvOrani} onChangeText={setOptKdvOrani} />
            {(() => {
              const o = calculateOptimal();
              return (
                <View style={{ marginTop: 14, gap: 8 }}>
                  <View style={styles.kurRow}>
                    <Text style={styles.kurLabel}>Önerilen Satış Fiyatı</Text>
                    <Text style={[styles.kurVal, { color: '#00FF87', fontWeight: 'bold', fontSize: 16 }]}>{formatMoney(o.satisFiyati)}</Text>
                  </View>
                  <View style={styles.kurRow}>
                    <Text style={styles.kurLabel}>Net Kâr</Text>
                    <Text style={styles.kurVal}>{formatMoney(o.netKar)}</Text>
                  </View>
                  <View style={styles.kurRow}>
                    <Text style={styles.kurLabel}>KDV Tutarı</Text>
                    <Text style={styles.kurVal}>{formatMoney(o.kdv)}</Text>
                  </View>
                </View>
              );
            })()}
          </View>
        )}

        {/* Tab 13: Evrak No Düzenleme */}
        {activeTab === 'evrak' && (
          <View style={{ gap: 10 }}>
            <View style={styles.card}>
              <View style={styles.cardHeader}>
                <FileText color="#6366F1" size={22} />
                <Text style={styles.cardTitle}>Evrak Seri & Numara Yönetimi</Text>
              </View>
              <Text style={[styles.kurLabel, { fontSize: 11, color: '#64748B', marginBottom: 4 }]}>
                Faturalama ve finans işlemlerinde kullanılan seri ön ekleri ile sıradaki numaraları düzenleyin.
              </Text>
            </View>
            {seriYukleme ? (
              <ActivityIndicator size="large" color="#0061FF" style={{ marginTop: 30 }} />
            ) : seriTanimlar.length === 0 ? (
              <View style={styles.card}>
                <Text style={{ color: '#94A3B8', fontSize: 13 }}>Henüz seri tanımı yok. Aşağıdaki şablon ile başlayın.</Text>
                {[
                  { evrakTipi: 'Satış Faturası', seri: 'FAT', siradakiNo: '1' },
                  { evrakTipi: 'Tahsilat Makbuzu', seri: 'THS', siradakiNo: '1' },
                  { evrakTipi: 'Ödeme Makbuzu', seri: 'ODM', siradakiNo: '1' },
                  { evrakTipi: 'Teklif Formu', seri: 'TKF', siradakiNo: '1' }
                ].map((s, idx) => (
                  <TouchableOpacity key={idx} style={[styles.btnPremium, { backgroundColor: '#6366F1', marginTop: 10 }]} onPress={() => handleSaveSeri(s)}>
                    <Text style={styles.btnText}>{s.evrakTipi} ({s.seri}-{s.siradakiNo}) Başlat</Text>
                  </TouchableOpacity>
                ))}
              </View>
            ) : (
              seriTanimlar.map((s, idx) => (
                <View key={idx} style={styles.card}>
                  <Text style={styles.cardTitle}>{s.evrakTipi}</Text>
                  <Text style={styles.label}>Seri Ön Eki</Text>
                  <TextInput style={styles.input} placeholder="FAT" placeholderTextColor="#64748B" value={s.seri || ''}
                    onChangeText={(v) => {
                      const updated = seriTanimlar.map((x, i) => (i === idx ? { ...x, seri: v } : x));
                      setSeriTanimlar(updated);
                    }} />
                  <Text style={styles.label}>Sıradaki Numara</Text>
                  <TextInput style={styles.input} keyboardType="numeric" inputAccessoryViewID={KEYBOARD_ACCESSORY_ID} placeholder="1024" placeholderTextColor="#64748B" value={String(s.siradakiNo || '')}
                    onChangeText={(v) => {
                      const updated = seriTanimlar.map((x, i) => (i === idx ? { ...x, siradakiNo: v } : x));
                      setSeriTanimlar(updated);
                    }} />
                  <TouchableOpacity style={[styles.btnPremium, { backgroundColor: '#6366F1', marginTop: 14 }]} onPress={() => handleSaveSeri(s)}>
                    <Text style={styles.btnText}>Kaydet</Text>
                  </TouchableOpacity>
                </View>
              ))
            )}
          </View>
        )}

        {activeTab === 'limit' && <MusteriLimitScreen isTab={true} />}
          </>
        )}
      </ScrollView>

      {/* Cari Seçici Absolute Overlay (Nested Modal yerine) */}
      {isCariOverlayOpen && (
        <View style={styles.absoluteOverlay}>
          <View style={styles.modalHeader}>
            <Text style={styles.modalTitle}>Cari Seçin</Text>
            <TouchableOpacity onPress={() => setIsCariOverlayOpen(false)}>
              <X color="#FFF" size={24} />
            </TouchableOpacity>
          </View>
          <View style={[styles.searchBox, { marginBottom: 16 }]}>
            <Search color="#64748B" size={20} />
            <TextInput style={styles.searchInput} placeholder="Cari ara..." placeholderTextColor="#64748B" value={cariSearch} onChangeText={setCariSearch} />
          </View>
          <FlatList initialNumToRender={20} maxToRenderPerBatch={20} windowSize={5} 
            data={cariler.filter(c => (c.unvan || '').toLocaleLowerCase('tr-TR').includes(cariSearch.toLocaleLowerCase('tr-TR')))}
            keyExtractor={(item, idx) => item.id?.toString() || idx.toString()}
            renderItem={({ item }) => (
              <TouchableOpacity 
                style={styles.selectorItem} 
                onPress={() => { 
                  if (cariSelectorType === 'source') setSourceCari(item);
                  else setTargetCari(item);
                  setIsCariOverlayOpen(false); 
                }}
              >
                <Text style={styles.selectorItemText}>{item.unvan}</Text>
                <Text style={styles.selectorItemSub}>{item.grup}</Text>
              </TouchableOpacity>
            )}
          />
        </View>
      )}

      {/* Ürün Seçici Absolute Overlay (Nested Modal yerine) */}
      {isStokOverlayOpen && (
        <View style={styles.absoluteOverlay}>
          <View style={styles.modalHeader}>
            <Text style={styles.modalTitle}>Ürün Seçin</Text>
            <TouchableOpacity onPress={() => setIsStokOverlayOpen(false)}>
              <X color="#FFF" size={24} />
            </TouchableOpacity>
          </View>
          <View style={[styles.searchBox, { marginBottom: 16 }]}>
            <Search color="#64748B" size={20} />
            <TextInput style={styles.searchInput} placeholder="Ürün ara..." placeholderTextColor="#64748B" value={stokSearch} onChangeText={setStokSearch} />
          </View>
          <FlatList initialNumToRender={20} maxToRenderPerBatch={20} windowSize={5} 
            data={stoklar.filter(s => (s.stokAdi || '').toLocaleLowerCase('tr-TR').includes(stokSearch.toLocaleLowerCase('tr-TR')))}
            keyExtractor={(item, idx) => item.id?.toString() || idx.toString()}
            renderItem={({ item }) => (
              <TouchableOpacity 
                style={styles.selectorItem} 
                onPress={() => { 
                  if (stokSelectorType === 'source') setSourceStok(item);
                  else setTargetStok(item);
                  setIsStokOverlayOpen(false); 
                }}
              >
                <Text style={styles.selectorItemText}>{item.stokAdi}</Text>
                <Text style={styles.selectorItemSub}>Mevcut: {item.miktar}</Text>
              </TouchableOpacity>
            )}
          />
        </View>
      )}

      {/* CRM Fırsat Ekle Modal */}
      <Modal visible={isPortfoyModalOpen} animationType="slide" transparent={true}>
        <SafeAreaView style={styles.modalOverlay}>
          <View style={styles.modalContent}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>Yeni Fırsat (CRM)</Text>
              <TouchableOpacity onPress={() => setIsPortfoyModalOpen(false)}>
                <X color="#FFF" size={24} />
              </TouchableOpacity>
            </View>
            <ScrollView contentContainerStyle={{ paddingBottom: 40 }}>
              <Text style={styles.label}>Firma Adı</Text>
              <TextInput style={styles.input} value={leadFirma} onChangeText={setLeadFirma} placeholder="Firma..." placeholderTextColor="#64748B" />
              <Text style={styles.label}>Yetkili Kişi</Text>
              <TextInput style={styles.input} value={leadYetkili} onChangeText={setLeadYetkili} placeholder="Ad Soyad..." placeholderTextColor="#64748B" />
              <Text style={styles.label}>Telefon (GSM)</Text>
              <TextInput style={styles.input} value={leadGsm} onChangeText={setLeadGsm} placeholder="0555..." placeholderTextColor="#64748B" />
              <Text style={styles.label}>Şehir (İl)</Text>
              <TextInput style={styles.input} value={leadIl} onChangeText={setLeadIl} placeholder="İstanbul..." placeholderTextColor="#64748B" />
              <Text style={styles.label}>Tahmini Fırsat Değeri (₺)</Text>
              <TextInput style={styles.input} keyboardType="numeric" inputAccessoryViewID={KEYBOARD_ACCESSORY_ID} value={leadTutar} onChangeText={setLeadTutar} />
              <Text style={styles.label}>Fırsat Detayları</Text>
              <TextInput style={[styles.input, { height: 60 }]} multiline={true} value={leadAciklama} onChangeText={setLeadAciklama} />
              <TouchableOpacity style={styles.btnPremium} onPress={handleSavePortfoy}>
                <Text style={styles.btnText}>Fırsatı Kaydet</Text>
              </TouchableOpacity>
            </ScrollView>
          </View>
        </SafeAreaView>
      </Modal>

      {/* Belge Ekle Modal */}
      <Modal visible={isDocFormOpen} animationType="slide" transparent={true}>
        <SafeAreaView style={styles.modalOverlay}>
          <View style={styles.modalContent}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>Arşive Yeni Belge Ekle</Text>
              <TouchableOpacity onPress={() => setIsDocFormOpen(false)}>
                <X color="#FFF" size={24} />
              </TouchableOpacity>
            </View>
            <ScrollView contentContainerStyle={{ paddingBottom: 40 }}>
              <Text style={styles.label}>Belge Adı</Text>
              <TextInput style={styles.input} value={docAd} onChangeText={setDocAd} placeholder="Fatura, Sözleşme vb..." placeholderTextColor="#64748B" />
              <Text style={styles.label}>Kategori</Text>
              <TextInput style={styles.input} value={docKategori} onChangeText={setDocKategori} placeholder="Genel, Finans vb..." placeholderTextColor="#64748B" />
              <Text style={styles.label}>Belge Notu / İçerik</Text>
              <TextInput style={[styles.input, { height: 80 }]} multiline={true} value={docAciklama} onChangeText={setDocAciklama} placeholder="Belge hakkında notlar..." placeholderTextColor="#64748B" />
              <Text style={styles.label}>Belge Görseli</Text>
              <TouchableOpacity style={[styles.btnPremium, { backgroundColor: '#2A2A2A' }]} onPress={handlePickDoc}>
                <Text style={[styles.btnText, { color: '#0061FF' }]}>Galeriden Görsel Seç</Text>
              </TouchableOpacity>
              {docDataUri ? (
                <View style={{ marginTop: 12 }}>
                  <Image source={{ uri: docDataUri }} style={{ width: '100%', height: 140, borderRadius: 10 }} resizeMode="cover" />
                  <Text style={{ color: '#64748B', fontSize: 11, marginTop: 4 }}>{docFileName} • {docFileSize > 0 ? `${(docFileSize / 1024).toFixed(1)} KB` : ''}</Text>
                </View>
              ) : (
                <Text style={{ color: '#64748B', fontSize: 12, marginTop: 6 }}>Henüz görsel seçilmedi.</Text>
              )}
              <TouchableOpacity style={[styles.btnPremium, { backgroundColor: '#0061FF' }]} onPress={handleSaveDoc}>
                <Text style={styles.btnText}>Belgeyi Arşivle</Text>
              </TouchableOpacity>
            </ScrollView>
          </View>
        </SafeAreaView>
      </Modal>
      <KeyboardDoneAccessory />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  yearSelectorContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    marginVertical: 12,
    backgroundColor: 'rgba(255, 255, 255, 0.02)',
    paddingVertical: 10,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.05)',
  },
  yearArrow: {
    paddingHorizontal: 20,
    paddingVertical: 4,
  },
  yearArrowText: {
    color: '#3B82F6',
    fontSize: 20,
    fontWeight: 'bold',
  },
  yearText: {
    color: '#FFF',
    fontSize: 18,
    fontWeight: 'bold',
  },
  hedefListScroll: {
    maxHeight: 450,
    marginTop: 10,
  },
  hedefRowContainer: {
    marginBottom: 10,
  },
  hedefRowHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 4,
  },
  hedefMonthName: {
    fontSize: 15,
    fontWeight: 'bold',
    color: '#E2E8F0',
  },
  editIconBtn: {
    paddingVertical: 4,
    paddingHorizontal: 10,
    borderRadius: 8,
    backgroundColor: 'rgba(59, 130, 246, 0.1)',
  },
  editIconBtnText: {
    color: '#3B82F6',
    fontSize: 12,
    fontWeight: '600',
  },
  hedefDetailsRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 8,
  },
  hedefDetailVal: {
    fontSize: 12,
    color: '#94A3B8',
  },
  progressContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  progressBarBg: {
    flex: 1,
    height: 8,
    backgroundColor: 'rgba(255, 255, 255, 0.05)',
    borderRadius: 4,
    marginRight: 10,
    overflow: 'hidden',
  },
  progressBarFill: {
    height: '100%',
    borderRadius: 4,
  },
  progressPct: {
    fontSize: 12,
    fontWeight: 'bold',
    width: 45,
    textAlign: 'right',
  },
  editTargetForm: {
    backgroundColor: '#161616',
    padding: 12,
    borderRadius: 12,
    marginTop: 10,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
  },
  editTargetFormLabel: {
    fontSize: 12,
    color: '#94A3B8',
    marginBottom: 6,
    fontWeight: '600',
  },
  editTargetFormRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  rowDivider: {
    height: 1,
    backgroundColor: 'rgba(255, 255, 255, 0.03)',
    marginVertical: 12,
  },
  container: {
    flex: 1,
    backgroundColor: '#0A0A0A',
  },
  header: {
    padding: 20,
    paddingTop: 40,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.08)',
  },
  headerTitle: {
    fontSize: 22,
    fontWeight: '900',
    color: '#FFFFFF',
  },
  headerSubtitle: {
    color: '#64748B',
    fontSize: 12,
    marginTop: 4,
  },
  tabScrollBox: {
    backgroundColor: '#161616',
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255,255,255,0.08)',
  },
  tabContainer: {
    paddingHorizontal: 12,
    paddingVertical: 10,
  },
  tabButton: {
    paddingHorizontal: 14,
    paddingVertical: 8,
    borderRadius: 10,
    marginRight: 6,
    backgroundColor: 'rgba(255,255,255,0.04)',
  },
  tabButtonActive: {
    backgroundColor: '#0061FF',
  },
  tabText: {
    color: '#94A3B8',
    fontWeight: 'bold',
    fontSize: 12,
  },
  tabTextActive: {
    color: '#FFF',
  },
  card: {
    backgroundColor: '#161616',
    borderRadius: 20,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    padding: 16,
    marginBottom: 14,
  },
  cardHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 14,
  },
  cardTitle: {
    color: '#FFF',
    fontSize: 14,
    fontWeight: 'bold',
    marginLeft: 8,
  },
  kurRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: 8,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255,255,255,0.05)',
  },
  kurLabel: {
    color: '#94A3B8',
    fontSize: 13,
  },
  kurVal: {
    color: '#FFF',
    fontSize: 13,
    fontWeight: 'bold',
  },
  btnPremium: {
    backgroundColor: '#F59E0B',
    padding: 14,
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 14,
  },
  btnText: {
    color: '#FFF',
    fontWeight: 'bold',
    fontSize: 13,
  },
  label: {
    color: '#94A3B8',
    fontSize: 11,
    fontWeight: 'bold',
    marginTop: 10,
    marginBottom: 6,
  },
  segmentRow: {
    flexDirection: 'row',
    backgroundColor: '#2A2A2A',
    padding: 4,
    borderRadius: 12,
  },
  segmentBtn: {
    flex: 1,
    paddingVertical: 10,
    alignItems: 'center',
    borderRadius: 8,
  },
  segmentBtnActive: {
    backgroundColor: '#10B981',
  },
  segmentBtnText: {
    color: '#64748B',
    fontSize: 12,
    fontWeight: 'bold',
  },
  segmentBtnTextActive: {
    color: '#FFF',
  },
  input: {
    backgroundColor: '#2A2A2A',
    borderColor: '#444',
    borderWidth: 1,
    borderRadius: 12,
    color: '#FFF',
    padding: 12,
    fontSize: 14,
  },
  selector: {
    backgroundColor: '#2A2A2A',
    borderColor: '#444',
    borderWidth: 1,
    borderRadius: 12,
    padding: 14,
    marginBottom: 10,
  },
  selectorText: {
    color: '#FFF',
    fontSize: 14,
  },
  addButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#0061FF',
    padding: 14,
    borderRadius: 12,
    marginBottom: 10,
  },
  addButtonText: {
    color: '#FFF',
    fontWeight: 'bold',
    fontSize: 13,
    marginLeft: 6,
  },
  absoluteOverlay: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    backgroundColor: '#0A0A0A',
    zIndex: 99,
    padding: 20,
  },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 20,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255,255,255,0.08)',
    paddingBottom: 16,
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#FFF',
  },
  searchBox: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#2A2A2A',
    borderRadius: 12,
    borderWidth: 1,
    borderColor: '#444',
    paddingHorizontal: 12,
  },
  searchInput: {
    flex: 1,
    paddingVertical: 12,
    paddingHorizontal: 8,
    color: '#FFFFFF',
    fontSize: 15,
  },
  selectorItem: {
    backgroundColor: '#161616',
    padding: 14,
    borderRadius: 12,
    marginBottom: 8,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.08)',
  },
  selectorItemText: {
    color: '#FFF',
    fontSize: 14,
    fontWeight: 'bold',
  },
  selectorItemSub: {
    color: '#64748B',
    fontSize: 11,
    marginTop: 4,
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.85)',
  },
  modalContent: {
    flex: 1,
    backgroundColor: '#0A0A0A',
    padding: 20,
  },
  categoriesGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    justifyContent: 'space-between',
    padding: 2,
  },
  categoryCard: {
    width: '48%',
    backgroundColor: '#161616',
    borderRadius: 20,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    padding: 16,
    marginBottom: 16,
    height: 140,
    justifyContent: 'center',
  },
  categoryIconBox: {
    width: 44,
    height: 44,
    borderRadius: 14,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 12,
  },
  categoryTitle: {
    color: '#FFF',
    fontSize: 13,
    fontWeight: 'bold',
    marginBottom: 4,
  },
  categoryDesc: {
    color: '#64748B',
    fontSize: 10,
    lineHeight: 14,
  },
  kpiBox: {
    backgroundColor: '#161616',
    borderRadius: 10,
    padding: 10,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    borderLeftWidth: 3,
  },
  kpiBoxTitle: {
    color: '#94A3B8',
    fontSize: 10,
    fontWeight: '600',
    textTransform: 'uppercase',
  },
  kpiBoxVal: {
    fontSize: 13,
    fontWeight: '800',
    marginTop: 3,
  },
  backButtonHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 20,
    backgroundColor: '#161616',
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.08)',
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderRadius: 10,
    alignSelf: 'flex-start',
  },
  backButtonText: {
    color: '#E2E8F0',
    fontSize: 13,
    fontWeight: 'bold',
    marginLeft: 6,
  }
});
