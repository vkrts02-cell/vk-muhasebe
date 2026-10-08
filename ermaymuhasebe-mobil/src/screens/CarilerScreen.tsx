import React, { useState, useEffect, useRef } from 'react';
import { StyleSheet, Text, View, SafeAreaView, FlatList, TextInput, ActivityIndicator, TouchableOpacity, Modal, ScrollView, Alert, Switch, PanResponder, Platform } from 'react-native';
import { FlashList } from '@shopify/flash-list';
import { Search, UserCircle2, Plus, X, Save, Edit3, Trash2, Phone, Mail, MapPin, Coins, ArrowUpRight, ArrowDownRight, FileText, Table, BarChart4 } from 'lucide-react-native';
import { subscribeToPath, writeData, deleteData, readData, mapAppToDatabase, splitAccounts, mergeKasalar, updateCariBaseInfoInAllYears } from '../services/firebase';
import { saveFinancialTransaction, deleteFinancialTransaction } from '../services/transactionService';
import { generateReportPdf } from '../services/pdfService';
import * as FileSystem from 'expo-file-system/legacy';
import * as Sharing from 'expo-sharing';
import { generateInt32Id } from '../utils/IdGenerator';
import { getCurrentGpsLocation } from '../services/locationService';
import { pickContactForCari } from '../services/contactService';
import { OfflineNetworkBar } from '../components/OfflineNetworkBar';
import { cariSchema } from '../utils/validationSchema';
import { AppleListRow, AppleGroupedCard } from '../components/AppleGroupedList';
import { AppleTheme } from '../theme/appleDesign';
import { ShimmerCardList } from '../components/Shimmer';
import { KeyboardDoneAccessory, KEYBOARD_ACCESSORY_ID } from '../components/KeyboardDoneAccessory';

const formatMoney = (val: number) => {
  return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' }).format(val);
};

export default function CarilerScreen({ route, navigation }: any) {
  const [cariler, setCariler] = useState<any[]>([]);
  const [cariHareketler, setCariHareketler] = useState<any[]>([]);
  const [kasalar, setKasalar] = useState<any[]>([]);
  const [bankalar, setBankalar] = useState<any[]>([]);
  const bankKasalarRef = useRef<any[]>([]);
  const legacyKasalarRef = useRef<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  
  // Modals state
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [isDetailOpen, setIsDetailOpen] = useState(false);
  const [selectedCari, setSelectedCari] = useState<any | null>(null);
  const [isTransactionOpen, setIsTransactionOpen] = useState(false);
  const [isYaslandirmaOpen, setIsYaslandirmaOpen] = useState(false);
  const [isPaymentPlanDropdownOpen, setIsPaymentPlanDropdownOpen] = useState(false);

  // Saving guards to prevent double-tap double-save
  const [isSaving, setIsSaving] = useState(false);
  const [isSavingTransaction, setIsSavingTransaction] = useState(false);

  // Quick transaction form fields
  const [transType, setTransType] = useState<'Tahsilat' | 'Ödeme'>('Tahsilat');
  const [transAmount, setTransAmount] = useState('');
  const [transDesc, setTransDesc] = useState('');
  const [transDate, setTransDate] = useState(new Date().toISOString().split('T')[0]);
  const [transMethod, setTransMethod] = useState<'Nakit' | 'Kredi Kartı' | 'Havale/EFT'>('Nakit');
  const [kkBanka, setKkBanka] = useState('');
  const [kkKartNo, setKkKartNo] = useState('');
  const [transTarget, setTransTarget] = useState<'Kasa' | 'Banka'>('Kasa');
  const [selectedAccount, setSelectedAccount] = useState<any | null>(null);
  const [isAccountModalOpen, setIsAccountModalOpen] = useState(false);

  // Form fields state
  const [editingId, setEditingId] = useState<number | null>(null);
  const [unvan, setUnvan] = useState('');
  const [cariKod, setCariKod] = useState('');
  const [yetkili, setYetkili] = useState('');
  const [acilisBakiye, setAcilisBakiye] = useState('0');
  const [lat, setLat] = useState('');
  const [lng, setLng] = useState('');
  const [tur, setTur] = useState<'Alici' | 'Satici'>('Alici');
  const [grup, setGrup] = useState('Müşteri');
  const [telefon, setTelefon] = useState('');
  const [cepTelefon, setCepTelefon] = useState('');
  const [eposta, setEposta] = useState('');
  const [webAdresi, setWebAdresi] = useState('');
  const [vergiDairesi, setVergiDairesi] = useState('');
  const [vergiNo, setVergiNo] = useState('');
  const [iban, setIban] = useState('');
  const [adres, setAdres] = useState('');
  const [sevkAdresi, setSevkAdresi] = useState('');
  const [il, setIl] = useState('');
  const [ilce, setIlce] = useState('');
  const [postaKodu, setPostaKodu] = useState('');
  const [ulke, setUlke] = useState('Türkiye');
  const [vadeGunu, setVadeGunu] = useState('0');
  const [riskLimiti, setRiskLimiti] = useState('0');
  const [odemePlani, setOdemePlani] = useState('');
  const [tcNo, setTcNo] = useState('');
  const [ticaretSicilNo, setTicaretSicilNo] = useState('');
  const [riskTakibiYapilsin, setRiskTakibiYapilsin] = useState(false);
  const [vadeGecmisteEngelle, setVadeGecmisteEngelle] = useState(false);
  const [faturadaRiskKontrolu, setFaturadaRiskKontrolu] = useState(true);
  const [aktifMi, setAktifMi] = useState(true);
  const [aciklama, setAciklama] = useState('');

  const detailPanResponder = useRef(
    PanResponder.create({
      onStartShouldSetPanResponder: () => false,
      onMoveShouldSetPanResponder: (evt, gestureState) => {
        return gestureState.dx > 40 && Math.abs(gestureState.dy) < 25;
      },
      onPanResponderRelease: (evt, gestureState) => {
        if (gestureState.dx > 120) {
          setIsDetailOpen(false);
        }
      },
    })
  ).current;

  const formPanResponder = useRef(
    PanResponder.create({
      onStartShouldSetPanResponder: () => false,
      onMoveShouldSetPanResponder: (evt, gestureState) => {
        return gestureState.dx > 40 && Math.abs(gestureState.dy) < 25;
      },
      onPanResponderRelease: (evt, gestureState) => {
        if (gestureState.dx > 120) {
          setIsFormOpen(false);
        }
      },
    })
  ).current;

  const transPanResponder = useRef(
    PanResponder.create({
      onStartShouldSetPanResponder: () => false,
      onMoveShouldSetPanResponder: (evt, gestureState) => {
        return gestureState.dx > 40 && Math.abs(gestureState.dy) < 25;
      },
      onPanResponderRelease: (evt, gestureState) => {
        if (gestureState.dx > 120) {
          setIsTransactionOpen(false);
          setIsDetailOpen(true);
        }
      },
    })
  ).current;

  useEffect(() => {
    if (route?.params?.reOpenCariId && cariler.length > 0) {
      const cariToReopen = cariler.find(c => c.id === route.params.reOpenCariId);
      if (cariToReopen) {
        setSelectedCari(cariToReopen);
        setIsDetailOpen(true);
        // Clear param so it doesn't trigger again
        navigation.setParams({ reOpenCariId: undefined });
      }
    }
  }, [route?.params?.reOpenCariId, cariler]);

  // Keep selectedCari dynamically in sync with the latest cariler list
  useEffect(() => {
    if (selectedCari && cariler.length > 0) {
      const fresh = cariler.find(c => String(c.id) === String(selectedCari.id) || String(c.firebaseKey || '') === String(selectedCari.firebaseKey || selectedCari.id));
      if (fresh && JSON.stringify(fresh) !== JSON.stringify(selectedCari)) {
        setSelectedCari(fresh);
      }
    }
  }, [cariler]);

  useEffect(() => {
    const unsubCariler = subscribeToPath('Cariler', (data) => {
      if (data) {
        const list = Array.isArray(data)
          ? data.map((item, idx) => item ? ({ ...item, firebaseKey: String(item?.id ?? idx) }) : null).filter(Boolean)
          : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setCariler(list.filter(c => !c.isDeleted));
      } else {
        setCariler([]);
      }
    });

    const unsubHareketler = subscribeToPath('CariHareketler', (data) => {
      if (data) {
        const list = Array.isArray(data)
          ? data.map((item, idx) => item ? ({ ...item, firebaseKey: String(item?.id ?? idx) }) : null).filter(Boolean)
          : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setCariHareketler(list.filter(h => !h.isDeleted));
      } else {
        setCariHareketler([]);
      }
    });

    const unsubKasalar = subscribeToPath('Kasalar', (data) => {
      if (data) {
        const list = Array.isArray(data)
          ? data.map((item, idx) => item ? ({ ...item, firebaseKey: String(item?.id ?? idx) }) : null).filter(Boolean)
          : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        legacyKasalarRef.current = list.filter(k => k && k.isDeleted !== true);
        setKasalar(mergeKasalar(bankKasalarRef.current, legacyKasalarRef.current));
      } else {
        legacyKasalarRef.current = [];
        setKasalar(mergeKasalar(bankKasalarRef.current, legacyKasalarRef.current));
      }
    });

    const unsubBankalar = subscribeToPath('Bankalar', (data) => {
      if (data) {
        const list = Array.isArray(data)
          ? data.map((item, idx) => item ? ({ ...item, firebaseKey: String(item?.id ?? idx) }) : null).filter(Boolean)
          : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        const { kasalar, bankalar } = splitAccounts(list.filter(b => b && b.isDeleted !== true));
        bankKasalarRef.current = kasalar;
        setKasalar(mergeKasalar(bankKasalarRef.current, legacyKasalarRef.current));
        setBankalar(bankalar);
      } else {
        bankKasalarRef.current = [];
        setKasalar(mergeKasalar(bankKasalarRef.current, legacyKasalarRef.current));
        setBankalar([]);
      }
    });

    setLoading(false);

    return () => {
      unsubCariler();
      unsubHareketler();
      unsubKasalar();
      unsubBankalar();
    };
  }, []);

  const getCariNetBakiye = (item: any): number => {
    const borc = (Number(item?.borc) || Number(item?.borcTutari) || Number(item?.borc_tutari) || 0) + (Number(item?.devirBorc) || Number(item?.devir_borc) || 0);
    const alacak = (Number(item?.alacak) || Number(item?.alacakTutari) || Number(item?.alacak_tutari) || 0) + (Number(item?.devirAlacak) || Number(item?.devir_alacak) || 0);
    if (borc !== 0 || alacak !== 0) {
      return borc - alacak;
    }
    return Number(item?.bakiye) || 0;
  };

  const [sortByBakiye, setSortByBakiye] = useState<boolean>(false);
  const [bakiyeSortDesc, setBakiyeSortDesc] = useState<boolean>(true);

  const handleToggleBakiyeSort = () => {
    if (!sortByBakiye) {
      setSortByBakiye(true);
      setBakiyeSortDesc(true); // Kullanıcı Bakiye'ye ilk tıkladığında en yüksek (+ bakiye) cariden başlayıp eksiye doğru sıralar
    } else {
      setBakiyeSortDesc(prev => !prev);
    }
  };

  const filteredCariler = cariler.filter(c => 
    (c.unvan || '').toLocaleLowerCase('tr-TR').includes(searchQuery.toLocaleLowerCase('tr-TR')) ||
    (c.cariKod || '').toLocaleLowerCase('tr-TR').includes(searchQuery.toLocaleLowerCase('tr-TR'))
  ).sort((a, b) => {
    if (sortByBakiye) {
      const bakiyeA = getCariNetBakiye(a);
      const bakiyeB = getCariNetBakiye(b);
      return bakiyeSortDesc ? (bakiyeB - bakiyeA) : (bakiyeA - bakiyeB);
    }
    return (a.unvan || '').localeCompare(b.unvan || '', 'tr-TR');
  });

  const resetForm = () => {
    setEditingId(null);
    setUnvan('');
    setCariKod('');
    setYetkili('');
    setAcilisBakiye('0');
    setLat('');
    setLng('');
    setTur('Alici');
    setGrup('Müşteri');
    setTelefon('');
    setCepTelefon('');
    setEposta('');
    setWebAdresi('');
    setVergiDairesi('');
    setVergiNo('');
    setIban('');
    setAdres('');
    setSevkAdresi('');
    setIl('');
    setIlce('');
    setPostaKodu('');
    setUlke('Türkiye');
    setVadeGunu('0');
    setRiskLimiti('0');
    setOdemePlani('');
    setIsPaymentPlanDropdownOpen(false);
    setTcNo('');
    setTicaretSicilNo('');
    setRiskTakibiYapilsin(false);
    setVadeGecmisteEngelle(false);
    setFaturadaRiskKontrolu(true);
    setAktifMi(true);
    setAciklama('');
  };

  const handleGetGpsLocation = async () => {
    const loc = await getCurrentGpsLocation();
    if (loc) {
      setLat(String(loc.latitude));
      setLng(String(loc.longitude));
      if (loc.address) {
        setAdres(loc.address);
      }
      Alert.alert('GPS Konumu Alındı', `Enlem: ${loc.latitude.toFixed(4)}, Boylam: ${loc.longitude.toFixed(4)}\nAdres: ${loc.address || ''}`);
    } else {
      Alert.alert('Uyarı', 'GPS konum bilgisi alınamadı veya konum izni verilmedi.');
    }
  };

  const handleImportContact = async () => {
    const contact = await pickContactForCari();
    if (contact) {
      if (contact.name) setUnvan(contact.name);
      if (contact.phone) setTelefon(contact.phone);
      if (contact.email) setEposta(contact.email);
      Alert.alert('Rehberden Aktarıldı', `${contact.name} kişisi başarıyla forma aktarıldı.`);
    } else {
      Alert.alert('Uyarı', 'Rehberden kişi seçilmedi veya izin verilmedi.');
    }
  };

  const handleOpenAdd = () => {
    resetForm();
    setIsFormOpen(true);
  };

  const handleOpenEdit = (cari: any) => {
    setEditingId(cari.id);
    setUnvan(cari.unvan || '');
    setCariKod(cari.cariKod || '');
    setYetkili(cari.yetkili || '');
    setAcilisBakiye('0');
    setLat(cari.lat !== undefined ? String(cari.lat) : '');
    setLng(cari.lng !== undefined ? String(cari.lng) : '');
    setTur(cari.tur || 'Alici');
    setGrup(cari.grup || 'Müşteri');
    setTelefon(cari.telefon || '');
    setCepTelefon(cari.cepTelefon || '');
    setEposta(cari.eposta || '');
    setWebAdresi(cari.webAdresi || '');
    setVergiDairesi(cari.vergiDairesi || '');
    setVergiNo(cari.vergiNo || '');
    setIban(cari.iban || '');
    setAdres(cari.adres || '');
    setSevkAdresi(cari.sevkAdresi || '');
    setIl(cari.il || '');
    setIlce(cari.ilce || '');
    setPostaKodu(cari.postaKodu || '');
    setUlke(cari.ulke || 'Türkiye');
    setVadeGunu(cari.vadeGunu ? cari.vadeGunu.toString() : '0');
    setRiskLimiti(cari.riskLimiti ? cari.riskLimiti.toString() : '0');
    setOdemePlani(cari.odemePlani || '');
    setTcNo(cari.tcNo || '');
    setTicaretSicilNo(cari.ticaretSicilNo || '');
    setRiskTakibiYapilsin(!!cari.riskTakibiYapilsin);
    setVadeGecmisteEngelle(!!cari.vadeGecmisteEngelle);
    setFaturadaRiskKontrolu(cari.faturadaRiskKontrolu !== undefined ? !!cari.faturadaRiskKontrolu : true);
    setAktifMi(cari.aktifMi !== undefined ? !!cari.aktifMi : true);
    setAciklama(cari.aciklama || '');
    setIsDetailOpen(false);
    setIsFormOpen(true);
  };

  const handleSave = async () => {
    if (isSaving) return;
    if (!unvan.trim()) {
      Alert.alert('Hata', 'Lütfen cari unvanını giriniz.');
      return;
    }

    const currentId = editingId || generateInt32Id();
    const existing = cariler.find(c => c.id === currentId);

    const docData = {
      id: currentId,
      unvan: unvan.trim(),
      cariKod: cariKod.trim() || `CARI-${currentId}`,
      yetkili: yetkili.trim(),
      lat: lat ? parseFloat(lat) : (existing?.lat ?? null),
      lng: lng ? parseFloat(lng) : (existing?.lng ?? null),
      tur: (grup === 'Tedarikçi' ? 'Satici' : 'Alici') as 'Alici' | 'Satici',
      grup: grup.trim(),
      telefon: telefon.trim(),
      cepTelefon: cepTelefon.trim(),
      eposta: eposta.trim(),
      webAdresi: webAdresi.trim(),
      vergiDairesi: vergiDairesi.trim(),
      vergiNo: vergiNo.trim(),
      iban: iban.trim(),
      adres: adres.trim(),
      sevkAdresi: sevkAdresi.trim(),
      il: il.trim(),
      ilce: ilce.trim(),
      postaKodu: postaKodu.trim(),
      ulke: ulke.trim(),
      vadeGunu: parseInt(vadeGunu) || 0,
      riskLimiti: parseFloat(riskLimiti) || 0,
      odemePlani: odemePlani.trim(),
      tcNo: tcNo.trim(),
      ticaretSicilNo: ticaretSicilNo.trim(),
      riskTakibiYapilsin: !!riskTakibiYapilsin,
      vadeGecmisteEngelle: !!vadeGecmisteEngelle,
      faturadaRiskKontrolu: faturadaRiskKontrolu !== false,
      aktifMi: aktifMi !== false,
      aciklama: aciklama.trim(),
      borc: existing ? (existing.borc || 0) : 0,
      alacak: existing ? (existing.alacak || 0) : 0,
      isDeleted: false
    };

    try {
      await cariSchema.validate({
        unvan: docData.unvan,
        email: docData.eposta,
        tckn: docData.tcNo,
        vkn: docData.vergiNo,
        vergiDairesi: docData.vergiDairesi
      });
    } catch (err: any) {
      Alert.alert('Doğrulama Hatası', err.message);
      return;
    }

    setIsSaving(true);
    const success = await writeData(`Cariler/${currentId}`, docData);
    if (success) {
      // Temel bilgileri diğer yıllara da yansıt (borç/alacak hariç)
      updateCariBaseInfoInAllYears(currentId, docData);

      setIsFormOpen(false);
      resetForm();
      Alert.alert('Başarılı', 'Cari kart başarıyla kaydedildi.');
    } else {
      Alert.alert('Hata', 'Cari kart kaydedilemedi.');
    }
    setIsSaving(false);
  };

  const handleDelete = async (cari: any) => {
    const hasMovements = cariHareketler.some(h => h.cariId === cari.id && !h.isDeleted && ((h.borc && Number(h.borc) !== 0) || (h.alacak && Number(h.alacak) !== 0)));
    if (hasMovements) {
      Alert.alert('Uyarı', 'Hareket görmüş bir cariyi silemezsiniz! Lütfen önce hareketleri silin veya başka bir cariyle birleştirin.');
      return;
    }

    Alert.alert(
      'Cariyi Sil',
      `"${cari.unvan}" cari kartını silmek istediğinize emin misiniz?`,
      [
        { text: 'İptal', style: 'cancel' },
        {
          text: 'Evet, Sil',
          style: 'destructive',
          onPress: async () => {
            const success = await deleteData(`Cariler/${cari.id}`);
            if (success) {
              setIsDetailOpen(false);
              Alert.alert('Başarılı', 'Cari kart başarıyla silindi.');
            }
          }
        }
      ]
    );
  };

  const handleExportEkstre = async (detail = false) => {
    try {
      const hareketler = cariHareketler.filter(h => h.cariId === selectedCari.id).sort((a,b) => new Date(a.tarih).getTime() - new Date(b.tarih).getTime());
      
      const mappedCari = mapAppToDatabase('Cariler', selectedCari);
      const mappedHareketler = hareketler.map(h => mapAppToDatabase('CariHareketler', h));
      
      const cleanCode = (selectedCari.cariKod || 'Cari').replace(/[^a-zA-Z0-9]/g, '_');
      const reportFileName = `${detail ? 'DetayliEkstre' : 'Ekstre'}_${cleanCode}.pdf`;
      if (detail) {
        // Fatura detaylarını Firebase'den oku
        const faturaIds = hareketler

          .filter(h => h.faturaId)

          .map(h => h.faturaId);

          

        const uniqueFaturaIds = [...new Set(faturaIds)];

        const detayDictionary: Record<number, any[]> = {};

        

        if (uniqueFaturaIds.length > 0) {
          for (const fid of uniqueFaturaIds) {
            const fDetaylarRaw = await readData(`FaturaDetaylar/${fid}`) || [];
            const fDetayList = Array.isArray(fDetaylarRaw) 
              ? fDetaylarRaw.filter(Boolean) 
              : Object.keys(fDetaylarRaw).map(key => ({ ...(fDetaylarRaw as any)[key], id: parseInt(key) }));
            detayDictionary[fid] = fDetayList.map(d => mapAppToDatabase('FaturaDetaylar', d));
          }
        }

        

        const payload = {

          Cari: mappedCari,

          Hareketler: mappedHareketler,

          DetayDictionary: detayDictionary

        };

        await generateReportPdf('ekstre-detayli', payload, reportFileName);

      } else {

        const payload = {

          Cari: mappedCari,

          Hareketler: mappedHareketler

        };

        await generateReportPdf('ekstre', payload, reportFileName);

      }

    } catch (e: any) {

      Alert.alert('Hata', 'Ekstre oluşturulamadı: ' + e.message);

    }

  };

  const handleExportExcel = async () => {

    try {

      const hareketler = cariHareketler.filter(h => h.cariId === selectedCari.id).sort((a,b) => new Date(a.tarih).getTime() - new Date(b.tarih).getTime());

      let csv = "\uFEFFTarih,Islem Turu,Evrak No,Aciklama,Borc,Alacak\n";

      hareketler.forEach(h => {

        csv += `${h.tarih},${h.islemTuru || ''},${h.evrakNo || ''},"${h.aciklama || ''}",${h.borc || 0},${h.alacak || 0}\n`;

      });

      

      const cleanCode = (selectedCari.cariKod || 'Cari').replace(/[^a-zA-Z0-9]/g, '_');

      const fileUri = `${(FileSystem as any).cacheDirectory}Cari_Ekstre_${cleanCode}.csv`;

      await FileSystem.writeAsStringAsync(fileUri, csv, { encoding: 'utf8' });

      

      if (await Sharing.isAvailableAsync()) {

        await Sharing.shareAsync(fileUri, { mimeType: 'text/csv', dialogTitle: 'Ekstre Excel Paylaş' });

      }

    } catch (e: any) {

      Alert.alert('Hata', 'Excel dosyası paylaşılamadı: ' + e.message);

    }

  };

  const getCariYaslandirma = () => {

    const bugun = new Date();

    bugun.setHours(0,0,0,0);

    

    const cHareketler = cariHareketler

      .filter(h => h.cariId === selectedCari?.id)

      .sort((a, b) => new Date(a.tarih).getTime() - new Date(b.tarih).getTime());

      

    const toplamAlacak = cHareketler.reduce((sum, h) => sum + (h.alacak || 0), 0);

    const borcHareketleri = cHareketler

      .filter(h => (h.borc || 0) > 0)

      .sort((a, b) => {

        const dateA = new Date(a.vade || a.tarih).getTime();

        const dateB = new Date(b.vade || b.tarih).getTime();

        return dateA - dateB;

      });

      

    const odenmemisFaturalar: any[] = [];

    let harcananAlacak = toplamAlacak;

    

    borcHareketleri.forEach(b => {

      const borcTutar = b.borc || 0;

      if (harcananAlacak >= borcTutar) {

        harcananAlacak -= borcTutar;

      } else {

        const kalanBakiye = borcTutar - harcananAlacak;

        harcananAlacak = 0;

        odenmemisFaturalar.push({ hareket: b, kalanBakiye });

      }

    });

    let g0_30 = 0, g31_60 = 0, g61_90 = 0, g90_plus = 0, vadesiGelmemis = 0;

    

    odenmemisFaturalar.forEach(vf => {

      const vDate = new Date(vf.hareket.vade || vf.hareket.tarih);

      vDate.setHours(0,0,0,0);

      const diffTime = bugun.getTime() - vDate.getTime();

      const diffDays = Math.floor(diffTime / (1000 * 60 * 60 * 24));

      

      if (diffDays <= 0) {

        vadesiGelmemis += vf.kalanBakiye;

      } else if (diffDays <= 30) {

        g0_30 += vf.kalanBakiye;

      } else if (diffDays <= 60) {

        g31_60 += vf.kalanBakiye;

      } else if (diffDays <= 90) {

        g61_90 += vf.kalanBakiye;

      } else {

        g90_plus += vf.kalanBakiye;

      }

    });

    return { g0_30, g31_60, g61_90, g90_plus, vadesiGelmemis };

  };

  const handleExportYaslandirmaPdf = async () => {

    try {

      const bugun = new Date();

      bugun.setHours(0,0,0,0);

      

      const hareketler = cariHareketler.filter(h => h.cariId === selectedCari.id);

      

      // ((($1.borc || 0) + ($1.devirBorc || 0) - ($2.alacak || 0) - ($2.devirAlacak || 0))) > 0 olan borç hareketleri

      const borcHareketler = hareketler

        .filter(h => ((h.borc || 0) - (h.alacak || 0)) > 0)

        .sort((a, b) => new Date(a.tarih).getTime() - new Date(b.tarih).getTime());

        

      const headers = ["Vade Tarihi", "Açıklama", "0-30 Gün", "31-60 Gün", "61-90 Gün", "90+ Gün", "Borç Bakiyesi"];

      const rows: string[][] = [];

      

      borcHareketler.forEach(h => {

        const bakiye = (h.borc || 0) - (h.alacak || 0);

        const transDate = new Date(h.tarih);

        const diffTime = bugun.getTime() - transDate.getTime();

        const days = Math.floor(diffTime / (1000 * 60 * 60 * 24));

        

        const d0_30 = days <= 30 ? formatMoney(bakiye) : "-";

        const d31_60 = (days > 30 && days <= 60) ? formatMoney(bakiye) : "-";

        const d61_90 = (days > 60 && days <= 90) ? formatMoney(bakiye) : "-";

        const d90plus = days > 90 ? formatMoney(bakiye) : "-";

        

        const vadeTarihiFormatted = h.vade 

          ? new Date(h.vade).toLocaleDateString('tr-TR') 

          : new Date(h.tarih).toLocaleDateString('tr-TR');

          

        rows.push([

          vadeTarihiFormatted,

          h.aciklama || "-",

          d0_30, d31_60, d61_90, d90plus,

          formatMoney(bakiye)

        ]);

      });

      

      // Summary row

      let t0_30 = 0, t31_60 = 0, t61_90 = 0, t90plus = 0;

      borcHareketler.forEach(h => {

        const bakiye = (h.borc || 0) - (h.alacak || 0);

        const transDate = new Date(h.tarih);

        const diffTime = bugun.getTime() - transDate.getTime();

        const days = Math.floor(diffTime / (1000 * 60 * 60 * 24));

        

        if (days <= 30) t0_30 += bakiye;

        else if (days > 30 && days <= 60) t31_60 += bakiye;

        else if (days > 60 && days <= 90) t61_90 += bakiye;

        else t90plus += bakiye;

      });

      

      const grandTotal = t0_30 + t31_60 + t61_90 + t90plus;

      

      rows.push([

        "TOPLAM",

        "",

        formatMoney(t0_30),

        formatMoney(t31_60),

        formatMoney(t61_90),

        formatMoney(t90plus),

        formatMoney(grandTotal)

      ]);

      

      const payload = {

        title: `${selectedCari.unvan} - Yaşlandırma Raporu`,

        headers: headers,

        rows: rows

      };

      

      const cleanCode = (selectedCari.cariKod || 'Cari').replace(/[^a-zA-Z0-9]/g, '_');

      const reportFileName = `Yaslandirma_${cleanCode}.pdf`;

      await generateReportPdf('generic', payload, reportFileName);

    } catch (e: any) {

      Alert.alert('Hata', 'Yaşlandırma PDF raporu oluşturulamadı: ' + e.message);

    }

  };

  const parseAndFormatDate = (dateStr: any): string => {
    if (!dateStr) return new Date().toISOString();
    const str = String(dateStr).trim();
    if (str.includes('-')) {
      const d = new Date(str);
      return isNaN(d.getTime()) ? new Date().toISOString() : d.toISOString();
    }
    if (str.includes('.')) {
      const parts = str.split(' ')[0].split('.');
      if (parts.length === 3) {
        const parsedDate = new Date(parseInt(parts[2]), parseInt(parts[1]) - 1, parseInt(parts[0]), 12, 0, 0);
        if (!isNaN(parsedDate.getTime())) {
          return parsedDate.toISOString();
        }
      }
    }
    const d = new Date(str);
    return isNaN(d.getTime()) ? new Date().toISOString() : d.toISOString();
  };

  const handleViewHareketPdf = async (h: any) => {

    try {

      const rawFaturaId = h.faturaId || h.FaturaId;
      if (h.islemTuru.includes('Fatura')) {

        let fatura = null;
        let cleanFaturaId = rawFaturaId;

        if (rawFaturaId) {
          const parsedId = parseInt(rawFaturaId);
          cleanFaturaId = isNaN(parsedId) ? rawFaturaId : parsedId;
          fatura = await readData(`Faturalar/${cleanFaturaId}`);
        }

        // Eğer ID ile bulunamadıysa FaturaNo (EvrakNo) eşleşmesi ile buluttan ara
        if (!fatura && h.evrakNo) {
          const tumFaturalarRaw = await readData('Faturalar') || {};
          const faturalarList = Array.isArray(tumFaturalarRaw)
            ? tumFaturalarRaw.filter(Boolean)
            : Object.keys(tumFaturalarRaw).map(key => ({ ...tumFaturalarRaw[key], id: parseInt(key) || key }));

          const bulunan = faturalarList.find(f => 
            (f.faturaNo || f.FaturaNo || '').trim() === h.evrakNo.trim()
          );

          if (bulunan) {
            fatura = bulunan;
            cleanFaturaId = bulunan.id || bulunan.Id;
          }
        }

        if (!fatura) {

          Alert.alert('Hata', 'Fatura bilgisi bulunamadı.');

          return;

        }

        const cleanFatura = {
          ...fatura,
          tarih: parseAndFormatDate(fatura.tarih || fatura.Tarih),
          vadeTarihi: parseAndFormatDate(fatura.vadeTarihi || fatura.VadeTarihi),
          kayitTarihi: parseAndFormatDate(fatura.kayitTarihi || fatura.KayitTarihi)
        };

        // Fatura detaylarını oku
        const detaylarRaw = await readData(`FaturaDetaylar/${cleanFaturaId}`) || [];
        const detaylar = Array.isArray(detaylarRaw) 
          ? detaylarRaw.filter(Boolean) 
          : Object.keys(detaylarRaw).map(key => ({ ...detaylarRaw[key], id: parseInt(key) }));

        const mappedFatura = mapAppToDatabase('Faturalar', cleanFatura);
        const mappedDetaylar = detaylar.map(d => mapAppToDatabase('FaturaDetaylar', d));

        const payload = {
          Fatura: mappedFatura,
          Detaylar: mappedDetaylar
        };

        await generateReportPdf('fatura', payload, `Fatura_${fatura.faturaNo || fatura.FaturaNo || cleanFaturaId}.pdf`);

      } else {

        // Makbuz / Dekont PDF üret

        const payload = {

          MakbuzTipi: h.islemTuru || 'Peşin İşlem',

          CariUnvan: selectedCari?.unvan || 'Peşin İşlem',

          Tarih: h.tarih || new Date().toISOString(),

          Tutar: (Number(h.borc) > 0 ? Number(h.borc) : Number(h.alacak)) || 0,

          Aciklama: h.aciklama || `${h.islemTuru || 'İşlem'} yapıldı`,

          EvrakNo: h.evrakNo || '',

          CariKod: selectedCari?.cariKod || ''

        };

        await generateReportPdf('makbuz', payload, `Makbuz_${h.id}.pdf`);

      }

    } catch (e: any) {

      Alert.alert('Hata', 'PDF belgesi açılamadı: ' + e.message);

    }

  };

  const handleHareketOptions = (h: any) => {

    Alert.alert(

      'İşlem Seçenekleri',

      `${h.islemTuru} - ${h.aciklama}\nTutar: ${formatMoney(h.borc > 0 ? h.borc : h.alacak)}`,

      [

        {

          text: 'Görüntüle (PDF Aç)',

          onPress: () => handleViewHareketPdf(h)

        },

        {

          text: 'Düzenle',
          onPress: () => {
            setIsDetailOpen(false);
            const rawFaturaId = h.faturaId || h.FaturaId;
            const cleanEvrakNo = String(h.evrakNo || '').trim();
            const turUpper = String(h.islemTuru || '').toUpperCase();
            const isFatura = turUpper.includes('FATURA') ||
                             turUpper.includes('SATIŞ') ||
                             turUpper.includes('SATIS') ||
                             turUpper.includes('ALIŞ') ||
                             turUpper.includes('ALIS') ||
                             (rawFaturaId && Number(rawFaturaId) > 0) ||
                             cleanEvrakNo.startsWith('FAT') ||
                             cleanEvrakNo.startsWith('SF') ||
                             cleanEvrakNo.startsWith('AF') ||
                             cleanEvrakNo.startsWith('FTR-') ||
                             cleanEvrakNo.startsWith('KPL-');

            if (isFatura && (rawFaturaId || h.evrakNo)) {
              const cleanFaturaNo = h.evrakNo ? String(h.evrakNo).replace(/^KPL-/, '').trim() : undefined;
              navigation.navigate('FaturaForm', { 
                editFaturaId: rawFaturaId ? (parseInt(rawFaturaId) || rawFaturaId) : undefined, 
                faturaNo: cleanFaturaNo, 
                reOpenCariId: selectedCari?.id 
              });
            } else {
              navigation.navigate('Finans', { editHareketId: h.id || h.firebaseKey, reOpenCariId: selectedCari?.id });
            }
          }

        },

        {

          text: 'Sil',
          style: 'destructive',
          onPress: () => {
            const rawFaturaId = h.faturaId || h.FaturaId;
            const cleanEvrakNo = String(h.evrakNo || '').trim();
            const turUpper = String(h.islemTuru || '').toUpperCase();
            const isFatura = turUpper.includes('FATURA') ||
                             turUpper.includes('SATIŞ') ||
                             turUpper.includes('SATIS') ||
                             turUpper.includes('ALIŞ') ||
                             turUpper.includes('ALIS') ||
                             (rawFaturaId && Number(rawFaturaId) > 0) ||
                             cleanEvrakNo.startsWith('FAT') ||
                             cleanEvrakNo.startsWith('SF') ||
                             cleanEvrakNo.startsWith('AF') ||
                             cleanEvrakNo.startsWith('FTR-') ||
                             cleanEvrakNo.startsWith('KPL-');

            const confirmTitle = isFatura ? 'Faturayı ve Hareketi Sil' : 'Silme Onayı';
            const confirmMsg = isFatura
              ? 'Bu işlem bir faturaya aittir. Fatura, faturaya bağlı stok hareketleri ve stok bakiyeleri de geri alınıp silinecektir. Emin misiniz?'
              : 'Bu işlemi silmek istediğinize emin misiniz?';

            Alert.alert(
              confirmTitle,
              confirmMsg,
              [
                { text: 'İptal', style: 'cancel' },
                {
                  text: 'Sil',
                  style: 'destructive',
                  onPress: async () => {
                    const success = await deleteFinancialTransaction(h);
                    if (success) {
                      setCariHareketler(prev => prev.filter(item => 
                        String(item.id) !== String(h.id) && 
                        String(item.firebaseKey || '') !== String(h.firebaseKey || h.id)
                      ));
                      Alert.alert('Başarılı', isFatura ? 'Fatura ve ilişkili tüm hareketler başarıyla silindi.' : 'Cari hareketi başarıyla silindi.');
                    } else {
                      Alert.alert('Hata', 'Silme işlemi gerçekleştirilemedi.');
                    }
                  }
                }
              ]
            );
          }
        },
        { text: 'Kapat', style: 'cancel' }

      ]

    );

  };

  const handleSaveTransaction = async () => {

    if (isSavingTransaction) return;

    const amount = parseFloat(transAmount) || 0;

    if (amount <= 0) {

      Alert.alert('Hata', 'Lütfen geçerli bir tutar girin.');

      return;

    }

    if (!selectedAccount) {

      Alert.alert('Hata', `Lütfen işlem yapılacak ${transTarget.toLocaleLowerCase('tr-TR')} hesabını seçin.`);

      return;

    }

    const nowStr = new Date().toISOString();

    setIsSavingTransaction(true);

    const ok = await saveFinancialTransaction({

      cari: selectedCari,

      amount,

      date: transDate || nowStr.split('T')[0],

      transactionType: transType,

      method: transMethod,

      description: transDesc.trim() || `${transType} işlemi`,

      selectedHesap: {

        id: selectedAccount.id,

        kartTuru: transMethod === 'Nakit' ? 'Kasa' : selectedAccount.kartTuru,

        bakiye: selectedAccount.bakiye || 0,

      },

      bankaAdi: kkBanka,

      kartHesapNo: transMethod !== 'Nakit' ? kkKartNo : '',

    });

    if (ok) {

      setTransAmount('');

      setTransDesc('');

      setTransDate(new Date().toISOString().split('T')[0]);

      setTransMethod('Nakit');

      setKkBanka('');

      setKkKartNo('');

      setSelectedAccount(null);

      setIsTransactionOpen(false);

      setIsDetailOpen(true);

      Alert.alert('Başarılı', 'Finansal işlem başarıyla kaydedildi.');

    } else {

      Alert.alert('Hata', 'İşlem kaydedilemedi. (Bağlantı sorunu — işlem geri alındı.)');

    }

    setIsSavingTransaction(false);

  };

  const currentCariHareketler = cariHareketler.filter(h => 
    !h.isDeleted && 
    (String(h.cariId) === String(selectedCari?.id || 0) || Number(h.cariId) === Number(selectedCari?.id || 0))
  );

  const renderItem = ({ item, index }: { item: any; index: number }) => {
    const netBakiye = getCariNetBakiye(item);
    const isPositive = netBakiye > 0;
    const isNegative = netBakiye < 0;

    // Görseldeki gibi her kategoriye canlı Apple ikon kutucuğu rengi
    const iconColors = ['#FF9500', '#AF52DE', '#007AFF', '#34C759', '#FF2D55'];
    const assignedColor = iconColors[index % iconColors.length];

    return (
      <AppleListRow
        icon={<UserCircle2 color="#FFFFFF" size={22} />}
        iconBgColor={assignedColor}
        title={item.unvan}
        subtitle={`${item.cariKod || `CARI-${item.id}`} • ${item.yetkili || 'Yetkili Yok'}`}
        tag={item.grup || 'Müşteri'}
        tagColor={item.grup === 'Tedarikçi' ? AppleTheme.colors.warning : AppleTheme.colors.primary}
        value={formatMoney(Math.abs(netBakiye))}
        valueSub={isPositive ? 'Alacak' : isNegative ? 'Borç' : 'Sıfır'}
        valueColor={isPositive ? AppleTheme.colors.success : isNegative ? AppleTheme.colors.danger : AppleTheme.colors.textSecondary}
        isFirst={index === 0}
        isLast={index === filteredCariler.length - 1}
        onPress={() => {
          setSelectedCari(item);
          setIsDetailOpen(true);
        }}
      />
    );
  };

  return (

    <SafeAreaView style={styles.container}>
      <OfflineNetworkBar />

      {/* Standard Header */}
      <View style={styles.header}>
        <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
          <View>
            <Text style={styles.headerTitle}>Cariler</Text>
            <Text style={{ color: '#94A3B8', fontSize: 13, marginTop: 2 }}>Müşteri ve Tedarikçi Listesi</Text>
          </View>
          <TouchableOpacity style={styles.addButton} onPress={handleOpenAdd}>
            <Plus color="#FFF" size={20} />
            <Text style={styles.addButtonText}>Yeni Cari</Text>
          </TouchableOpacity>
        </View>

        <View style={styles.searchBox}>
          <Search color="#94A3B8" size={18} />
          <TextInput 
            style={styles.searchInput} 
            placeholder="Cari unvanı, kodu veya yetkili ara..." 
            placeholderTextColor="#64748B"
            value={searchQuery}
            onChangeText={setSearchQuery}
          />
          {searchQuery ? (
            <TouchableOpacity onPress={() => setSearchQuery('')}>
              <X color="#94A3B8" size={18} />
            </TouchableOpacity>
          ) : null}
        </View>

        {/* Kolon Başlıkları & Bakiye Sıralama Butonu (Masaüstü 2. Görsel ile Birebir) */}
        <View style={styles.tableHeaderRow}>
          <TouchableOpacity 
            style={{ flexDirection: 'row', alignItems: 'center' }} 
            onPress={() => setSortByBakiye(false)}
            activeOpacity={0.7}
          >
            <Text style={styles.tableHeaderTitle}>Ünvan / Cari</Text>
            {!sortByBakiye && <Text style={styles.tableHeaderSub}> (A-Z)</Text>}
          </TouchableOpacity>

          <TouchableOpacity 
            style={[styles.bakiyeSortHeaderBtn, sortByBakiye && styles.bakiyeSortHeaderBtnActive]} 
            onPress={handleToggleBakiyeSort}
            activeOpacity={0.7}
          >
            <Text style={[styles.bakiyeSortHeaderText, sortByBakiye && styles.bakiyeSortHeaderTextActive]}>
              Bakiye
            </Text>
            {sortByBakiye ? (
              <Text style={styles.bakiyeSortIndicator}>
                {bakiyeSortDesc ? ' ▼ (+/-)' : ' ▲ (-/+)'}
              </Text>
            ) : (
              <Text style={{ color: '#64748B', fontSize: 12, marginLeft: 4 }}>⇅</Text>
            )}
          </TouchableOpacity>
        </View>
      </View>

      {loading ? (
        <ShimmerCardList count={6} />
      ) : (
        <FlatList 
          data={filteredCariler}
          keyExtractor={(item, index) => String(item.firebaseKey || item.id || index)}
          renderItem={renderItem}
          contentContainerStyle={{ padding: 16, paddingBottom: 100 }}
          removeClippedSubviews={false}
          initialNumToRender={15}
          maxToRenderPerBatch={15}
          windowSize={7}
          ListEmptyComponent={
            <Text style={styles.emptyText}>Hiç cari bulunamadı.</Text>
          }
        />
      )}

      {/* Cari Ekle/Düzenle Form Modalı */}

      <Modal visible={isFormOpen} animationType="slide" transparent={true}>

        <SafeAreaView style={styles.modalOverlay} {...formPanResponder.panHandlers}>

          <View style={styles.modalContent}>

            <View style={styles.modalHeader}>

              <Text style={styles.modalTitle}>{editingId ? 'Cari Kartı Düzenle' : 'Yeni Cari Kartı'}</Text>

              <TouchableOpacity onPress={() => setIsFormOpen(false)} style={styles.closeButton}>

                <X color="#FFF" size={24} />

              </TouchableOpacity>

            </View>

            <ScrollView contentContainerStyle={styles.formScroll}>
              {/* Quick Actions: Rehberden Aktar & GPS Konumu Al */}
              <View style={{ flexDirection: 'row', gap: 10, marginBottom: 16 }}>
                <TouchableOpacity
                  onPress={handleImportContact}
                  style={{ flex: 1, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 6, backgroundColor: 'rgba(0, 255, 135, 0.12)', borderWidth: 1, borderColor: 'rgba(0, 255, 135, 0.3)', paddingVertical: 10, borderRadius: 10 }}
                >
                  <Phone color="#00FF87" size={14} />
                  <Text style={{ color: '#00FF87', fontSize: 12, fontWeight: '700' }}>Rehberden Aktar</Text>
                </TouchableOpacity>

                <TouchableOpacity
                  onPress={handleGetGpsLocation}
                  style={{ flex: 1, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 6, backgroundColor: 'rgba(0, 97, 255, 0.12)', borderWidth: 1, borderColor: 'rgba(0, 97, 255, 0.3)', paddingVertical: 10, borderRadius: 10 }}
                >
                  <MapPin color="#0061FF" size={14} />
                  <Text style={{ color: '#0061FF', fontSize: 12, fontWeight: '700' }}>GPS Konumu Al</Text>
                </TouchableOpacity>
              </View>

              <View style={styles.inputGroup}>

                <Text style={styles.inputLabel}>Cari Unvanı *</Text>

                <TextInput style={styles.input} placeholder="örn. Ermay Tekstil Ltd. Şti." placeholderTextColor="#64748B" value={unvan} onChangeText={setUnvan} />

              </View>

              <View style={styles.inputGroup}>

                <Text style={styles.inputLabel}>Yetkili Kişi</Text>

                <TextInput style={styles.input} placeholder="örn. Ahmet Yılmaz" placeholderTextColor="#64748B" value={yetkili} onChangeText={setYetkili} />

              </View>

              <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>

                <View style={[styles.inputGroup, { width: '48%' }]}>

                  <Text style={styles.inputLabel}>Cari Kodu (Opsiyonel)</Text>

                  <TextInput style={styles.input} placeholder="örn. CARI-001" placeholderTextColor="#64748B" value={cariKod} onChangeText={setCariKod} />

                </View>

                <View style={[styles.inputGroup, { width: '48%' }]}>

                  <Text style={styles.inputLabel}>Cari Tipi</Text>

                  <View style={styles.toggleRow}>

                    <TouchableOpacity 
                      style={[styles.toggleBtn, grup === 'Müşteri' && styles.toggleBtnActive]} 
                      onPress={() => {
                        setGrup('Müşteri');
                        setTur('Alici');
                      }}
                    >

                      <Text style={[styles.toggleBtnText, grup === 'Müşteri' && styles.toggleBtnTextActive]}>Müşteri</Text>

                    </TouchableOpacity>

                    <TouchableOpacity 
                      style={[styles.toggleBtn, grup === 'Tedarikçi' && styles.toggleBtnActive]} 
                      onPress={() => {
                        setGrup('Tedarikçi');
                        setTur('Satici');
                      }}
                    >

                      <Text style={[styles.toggleBtnText, grup === 'Tedarikçi' && styles.toggleBtnTextActive]}>Tedarikçi</Text>

                    </TouchableOpacity>

                  </View>

                </View>

              </View>

              <View style={styles.inputGroup}>
                <Text style={styles.inputLabel}>T.C. Kimlik No</Text>
                <TextInput style={styles.input} placeholder="11 haneli TC No" placeholderTextColor="#64748B" keyboardType="numeric" inputAccessoryViewID={KEYBOARD_ACCESSORY_ID} maxLength={11} value={tcNo} onChangeText={setTcNo} />
              </View>

              <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                <View style={[styles.inputGroup, { width: '48%' }]}>
                  <Text style={styles.inputLabel}>Vergi Dairesi</Text>
                  <TextInput style={styles.input} placeholder="İkitelli VD" placeholderTextColor="#64748B" value={vergiDairesi} onChangeText={setVergiDairesi} />
                </View>
                <View style={[styles.inputGroup, { width: '48%' }]}>
                  <Text style={styles.inputLabel}>Vergi Numarası</Text>
                  <TextInput style={styles.input} placeholder="10 haneli Vergi No" placeholderTextColor="#64748B" keyboardType="numeric" inputAccessoryViewID={KEYBOARD_ACCESSORY_ID} maxLength={10} value={vergiNo} onChangeText={setVergiNo} />
                </View>
              </View>

              <View style={styles.inputGroup}>
                <Text style={styles.inputLabel}>IBAN</Text>
                <TextInput style={styles.input} placeholder="TR00..." placeholderTextColor="#64748B" value={iban} onChangeText={setIban} />
              </View>

              <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                <View style={[styles.inputGroup, { width: '48%' }]}>
                  <Text style={styles.inputLabel}>Telefon</Text>
                  <TextInput style={styles.input} placeholder="0212..." placeholderTextColor="#64748B" keyboardType="phone-pad" value={telefon} onChangeText={setTelefon} />
                </View>
                <View style={[styles.inputGroup, { width: '48%' }]}>
                  <Text style={styles.inputLabel}>Cep Telefonu</Text>
                  <TextInput style={styles.input} placeholder="0532..." placeholderTextColor="#64748B" keyboardType="phone-pad" value={cepTelefon} onChangeText={setCepTelefon} />
                </View>
              </View>

              <View style={styles.inputGroup}>
                <Text style={styles.inputLabel}>E-Posta</Text>
                <TextInput style={styles.input} placeholder="bilgi@ermay.com" placeholderTextColor="#64748B" keyboardType="email-address" value={eposta} onChangeText={setEposta} />
              </View>

              <View style={{ flexDirection: 'row', justifyContent: 'space-between', zIndex: 10 }}>
                <View style={[styles.inputGroup, { width: '48%', position: 'relative' }]}>
                  <Text style={styles.inputLabel}>Ödeme Planı</Text>
                  <TouchableOpacity style={styles.selectorCard} onPress={() => setIsPaymentPlanDropdownOpen(!isPaymentPlanDropdownOpen)}>
                    <Text style={styles.selectorText}>
                      {odemePlani || 'Seçin...'}
                    </Text>
                  </TouchableOpacity>

                  {isPaymentPlanDropdownOpen && (
                    <View style={styles.inlineDropdown}>
                      <ScrollView nestedScrollEnabled={true} style={{ maxHeight: 180 }}>
                        {['Peşin', '7 Gün', '15 Gün', '30 Gün', '45 Gün', '60 Gün', '90 Gün', 'Haftalık', 'Aylık'].map((item) => (
                          <TouchableOpacity 
                            key={item}
                            style={styles.inlineDropdownItem}
                            onPress={() => {
                              setOdemePlani(item);
                              setIsPaymentPlanDropdownOpen(false);
                            }}
                          >
                            <Text style={styles.inlineDropdownText}>{item}</Text>
                          </TouchableOpacity>
                        ))}
                      </ScrollView>
                    </View>
                  )}
                </View>
                <View style={[styles.inputGroup, { width: '48%' }]}>
                  <Text style={styles.inputLabel}>Vade Günü</Text>
                  <TextInput style={styles.input} placeholder="0" placeholderTextColor="#64748B" keyboardType="numeric" inputAccessoryViewID={KEYBOARD_ACCESSORY_ID} value={vadeGunu} onChangeText={setVadeGunu} />
                </View>
              </View>

              <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                <View style={[styles.inputGroup, { width: '48%' }]}>
                  <Text style={styles.inputLabel}>Risk Limiti (TL)</Text>
                  <TextInput style={styles.input} placeholder="0.00" placeholderTextColor="#64748B" keyboardType="numeric" inputAccessoryViewID={KEYBOARD_ACCESSORY_ID} value={riskLimiti} onChangeText={setRiskLimiti} />
                </View>
                <View style={[styles.inputGroup, { width: '48%' }]}>
                  <Text style={styles.inputLabel}>Ülke</Text>
                  <TextInput style={styles.input} placeholder="Türkiye" placeholderTextColor="#64748B" value={ulke} onChangeText={setUlke} />
                </View>
              </View>

              <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                <View style={[styles.inputGroup, { width: '48%' }]}>
                  <Text style={styles.inputLabel}>İl</Text>
                  <TextInput style={styles.input} placeholder="İstanbul" placeholderTextColor="#64748B" value={il} onChangeText={setIl} />
                </View>
                <View style={[styles.inputGroup, { width: '48%' }]}>
                  <Text style={styles.inputLabel}>İlçe</Text>
                  <TextInput style={styles.input} placeholder="Başakşehir" placeholderTextColor="#64748B" value={ilce} onChangeText={setIlce} />
                </View>
              </View>

              <View style={styles.inputGroup}>
                <Text style={styles.inputLabel}>Adres</Text>
                <TextInput style={[styles.input, { height: 80 }]} placeholder="Detaylı adres bilgisi..." placeholderTextColor="#64748B" multiline={true} numberOfLines={3} value={adres} onChangeText={setAdres} />
              </View>

              <View style={styles.inputGroup}>
                <Text style={styles.inputLabel}>Sevk Adresi</Text>
                <TextInput style={[styles.input, { height: 80 }]} placeholder="Sevk adresi bilgisi..." placeholderTextColor="#64748B" multiline={true} numberOfLines={3} value={sevkAdresi} onChangeText={setSevkAdresi} />
              </View>

              <View style={styles.inputGroup}>
                <Text style={styles.inputLabel}>Açıklama</Text>
                <TextInput style={[styles.input, { height: 60 }]} placeholder="Cari hakkında not..." placeholderTextColor="#64748B" multiline={true} value={aciklama} onChangeText={setAciklama} />
              </View>

              <TouchableOpacity style={[styles.saveButton, isSaving && { opacity: 0.7 }]} disabled={isSaving} onPress={handleSave}>

                {isSaving ? <ActivityIndicator color="#FFF" size="small" /> : <Save color="#FFF" size={20} />}

                <Text style={styles.saveButtonText}>{isSaving ? 'Kaydediliyor...' : 'Kaydet'}</Text>

              </TouchableOpacity>
            </ScrollView>


          </View>
        </SafeAreaView>
      </Modal>

      {/* Cari Detay Modalı */}

      <Modal visible={isDetailOpen} animationType="fade" transparent={true}>

        <SafeAreaView style={styles.modalOverlay} {...detailPanResponder.panHandlers}>

          <View style={styles.modalContent}>

            {selectedCari && (

              <>

                <View style={styles.modalHeader}>

                  <Text style={styles.modalTitle}>Cari Detayı</Text>

                  <TouchableOpacity onPress={() => setIsDetailOpen(false)} style={styles.closeButton}>

                    <X color="#FFF" size={24} />

                  </TouchableOpacity>

                </View>

                <ScrollView contentContainerStyle={styles.formScroll}>

                  <View style={styles.detailCard}>

                    <UserCircle2 color="#0061FF" size={64} style={{ alignSelf: 'center', marginBottom: 12 }} />

                    <Text style={styles.detailUnvan}>{selectedCari.unvan}</Text>

                    <Text style={styles.detailGrup}>{selectedCari.tur === 'Alici' ? 'Alıcı' : 'Satıcı'}{selectedCari.grup ? ` • ${selectedCari.grup}` : ''} • {selectedCari.cariKod}</Text>

                    <View style={styles.detailBakiyeBox}>

                      <View style={{ alignItems: 'center' }}>

                        <Text style={styles.detailBakiyeLabel}>Borç</Text>

                        <Text style={[styles.detailBakiyeVal, { color: '#EF4444' }]}>{formatMoney(selectedCari.borc || 0)}</Text>

                      </View>

                      <View style={{ width: 1, backgroundColor: 'rgba(255,255,255,0.1)' }} />

                      <View style={{ alignItems: 'center' }}>

                        <Text style={styles.detailBakiyeLabel}>Alacak</Text>

                        <Text style={[styles.detailBakiyeVal, { color: '#10B981' }]}>{formatMoney(selectedCari.alacak || 0)}</Text>

                      </View>

                    </View>

                  </View>

                  {/* Cari Hızlı İşlemleri */}

                  <View style={styles.detailSection}>

                    <Text style={styles.detailSectionTitle}>HIZLI İŞLEMLER</Text>

                    <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8, justifyContent: 'space-between', marginTop: 4 }}>

                      <TouchableOpacity style={[styles.actionGridBtn, { backgroundColor: '#2563EB' }]} onPress={() => { setIsDetailOpen(false); navigation.navigate('FaturaForm', { initialCari: selectedCari, initialTur: 'Satış', reOpenCariId: selectedCari.id }); }}>

                        <ArrowUpRight color="#FFF" size={14} />

                        <Text style={styles.actionGridText}>SATIŞ</Text>

                      </TouchableOpacity>

                      <TouchableOpacity style={[styles.actionGridBtn, { backgroundColor: '#D97706' }]} onPress={() => { setIsDetailOpen(false); navigation.navigate('FaturaForm', { initialCari: selectedCari, initialTur: 'Alış', reOpenCariId: selectedCari.id }); }}>

                        <ArrowDownRight color="#FFF" size={14} />

                        <Text style={styles.actionGridText}>ALIŞ</Text>

                      </TouchableOpacity>

                      <TouchableOpacity style={[styles.actionGridBtn, { backgroundColor: '#10B981' }]} onPress={() => { setIsDetailOpen(false); setTransType('Tahsilat'); setIsTransactionOpen(true); }}>

                        <ArrowDownRight color="#FFF" size={14} />

                        <Text style={styles.actionGridText}>TAHSİLAT</Text>
                      </TouchableOpacity>
                      <TouchableOpacity style={[styles.actionGridBtn, { backgroundColor: '#EF4444' }]} onPress={() => { setIsDetailOpen(false); setTransType('Ödeme'); setIsTransactionOpen(true); }}>
                        <ArrowUpRight color="#FFF" size={14} />
                        <Text style={styles.actionGridText}>ÖDEME</Text>
                      </TouchableOpacity>
                      <TouchableOpacity style={[styles.actionGridBtn, { backgroundColor: '#8B5CF6' }]} onPress={() => { setIsDetailOpen(false); navigation.navigate('DahaFazla', { screen: 'Finans', params: { initialCari: selectedCari, initialIslemTuru: 'Alacak Dekontu' } }); }}>
                        <Plus color="#FFF" size={14} />
                        <Text style={styles.actionGridText}>ALACAK</Text>
                      </TouchableOpacity>
                      <TouchableOpacity style={[styles.actionGridBtn, { backgroundColor: '#EC4899' }]} onPress={() => { setIsDetailOpen(false); navigation.navigate('DahaFazla', { screen: 'Finans', params: { initialCari: selectedCari, initialIslemTuru: 'Borç Dekontu' } }); }}>
                        <Plus color="#FFF" size={14} />
                        <Text style={styles.actionGridText}>BORÇ</Text>
                      </TouchableOpacity>
                      <TouchableOpacity style={[styles.actionGridBtn, { backgroundColor: '#2A2A2A' }]} onPress={() => handleExportEkstre(false)}>
                        <FileText color="#FFF" size={14} />
                        <Text style={styles.actionGridText}>Ekstre (PDF)</Text>
                      </TouchableOpacity>
                      <TouchableOpacity style={[styles.actionGridBtn, { backgroundColor: '#2A2A2A' }]} onPress={() => handleExportEkstre(true)}>
                        <FileText color="#FFF" size={14} />
                        <Text style={styles.actionGridText}>Det. Ekstre</Text>
                      </TouchableOpacity>
                      <TouchableOpacity style={[styles.actionGridBtn, { backgroundColor: '#2A2A2A' }]} onPress={handleExportExcel}>
                        <Table color="#FFF" size={14} />
                        <Text style={styles.actionGridText}>Excel (CSV)</Text>
                      </TouchableOpacity>
                      <TouchableOpacity style={[styles.actionGridBtn, { backgroundColor: '#2A2A2A' }]} onPress={() => { setIsDetailOpen(false); setIsYaslandirmaOpen(true); }}>
                        <BarChart4 color="#FFF" size={14} />
                        <Text style={styles.actionGridText}>Yaşlandırma</Text>
                      </TouchableOpacity>
                    </View>
                  </View>

                  {/* Cari Hareketleri (Ekstre) */}
                  <View style={styles.detailSection}>
                    <Text style={styles.detailSectionTitle}>Cari Hareketler (Son İşlemler)</Text>
                    {currentCariHareketler.length === 0 ? (
                      <Text style={styles.emptyText}>Henüz hareket bulunmuyor.</Text>
                    ) : (
                      currentCariHareketler.map((h, idx) => (
                        <TouchableOpacity key={idx} style={styles.hareketRow} onPress={() => handleHareketOptions(h)}>
                          <View style={{ flex: 1 }}>
                            <Text style={styles.hareketType}>{h.islemTuru} • {h.aciklama}</Text>
                            <Text style={styles.hareketDate}>{h.tarih}</Text>
                          </View>
                          <Text style={[styles.hareketVal, { color: h.borc > 0 ? '#EF4444' : '#10B981' }]}>
                            {h.borc > 0 ? `-${formatMoney(h.borc)}` : `+${formatMoney(h.alacak)}`}
                          </Text>
                        </TouchableOpacity>
                      ))
                    )}
                  </View>

                  <View style={styles.detailSection}>
                    <Text style={styles.detailSectionTitle}>İletişim Bilgileri</Text>
                    {selectedCari.yetkili && (
                      <View style={styles.detailRow}>
                        <UserCircle2 color="#64748B" size={18} />
                        <Text style={styles.detailText}>Yetkili: {selectedCari.yetkili}</Text>
                      </View>
                    )}
                    {selectedCari.telefon && (
                      <View style={styles.detailRow}>
                        <Phone color="#64748B" size={18} />
                        <Text style={styles.detailText}>{selectedCari.telefon}</Text>
                      </View>
                    )}
                    {selectedCari.cepTelefon && (
                      <View style={styles.detailRow}>
                        <Phone color="#64748B" size={18} />
                        <Text style={styles.detailText}>{selectedCari.cepTelefon}</Text>
                      </View>
                    )}
                    {selectedCari.eposta && (
                      <View style={styles.detailRow}>
                        <Mail color="#64748B" size={18} />
                        <Text style={styles.detailText}>{selectedCari.eposta}</Text>
                      </View>
                    )}
                    {selectedCari.webAdresi && (
                      <View style={styles.detailRow}>
                        <Mail color="#64748B" size={18} />
                        <Text style={styles.detailText}>Web: {selectedCari.webAdresi}</Text>
                      </View>
                    )}
                    {(selectedCari.il || selectedCari.adres) && (
                      <View style={styles.detailRow}>
                        <MapPin color="#64748B" size={18} />
                        <Text style={styles.detailText}>
                          Adres: {selectedCari.adres} {selectedCari.ilce}/{selectedCari.il} {selectedCari.postaKodu || ''} {selectedCari.ulke || ''}
                        </Text>
                      </View>
                    )}
                    {selectedCari.sevkAdresi && (
                      <View style={styles.detailRow}>
                        <MapPin color="#64748B" size={18} />
                        <Text style={styles.detailText}>Sevk Adresi: {selectedCari.sevkAdresi}</Text>
                      </View>
                    )}
                    {(selectedCari.lat !== undefined && selectedCari.lat !== null && selectedCari.lat !== '') && (
                      <View style={styles.detailRow}>
                        <MapPin color="#64748B" size={18} />
                        <Text style={styles.detailText}>Koordinat: {selectedCari.lat}, {selectedCari.lng ?? '-'}</Text>
                      </View>
                    )}
                  </View>

                  <View style={styles.detailSection}>
                    <Text style={styles.detailSectionTitle}>Finansal & Vergi Bilgileri</Text>
                    {selectedCari.tcNo && (
                      <View style={styles.detailInfoRow}>
                        <Text style={styles.detailInfoLabel}>T.C. Kimlik No:</Text>
                        <Text style={styles.detailInfoValue}>{selectedCari.tcNo}</Text>
                      </View>
                    )}
                    <View style={styles.detailInfoRow}>
                      <Text style={styles.detailInfoLabel}>Vergi Dairesi/No:</Text>
                      <Text style={styles.detailInfoValue}>{selectedCari.vergiDairesi || '-'} / {selectedCari.vergiNo || '-'}</Text>
                    </View>
                    {selectedCari.ticaretSicilNo && (
                      <View style={styles.detailInfoRow}>
                        <Text style={styles.detailInfoLabel}>Ticaret Sicil No:</Text>
                        <Text style={styles.detailInfoValue}>{selectedCari.ticaretSicilNo}</Text>
                      </View>
                    )}
                    {selectedCari.iban && (
                      <View style={styles.detailInfoRow}>
                        <Text style={styles.detailInfoLabel}>IBAN:</Text>
                        <Text style={styles.detailInfoValue}>{selectedCari.iban}</Text>
                      </View>
                    )}
                    <View style={styles.detailInfoRow}>
                      <Text style={styles.detailInfoLabel}>Ödeme Planı / Vade Günü:</Text>
                      <Text style={styles.detailInfoValue}>{selectedCari.odemePlani || '-'} / {selectedCari.vadeGunu || '0'} Gün</Text>
                    </View>
                    <View style={styles.detailInfoRow}>
                      <Text style={styles.detailInfoLabel}>Risk Limiti:</Text>
                      <Text style={styles.detailInfoValue}>{formatMoney(selectedCari.riskLimiti || 0)}</Text>
                    </View>
                  </View>

                  <View style={styles.detailSection}>
                    <Text style={styles.detailSectionTitle}>Risk & Durum Seçenekleri</Text>
                    <View style={styles.detailInfoRow}>
                      <Text style={styles.detailInfoLabel}>Risk Takibi Aktif Mi:</Text>
                      <Text style={styles.detailInfoValue}>{selectedCari.riskTakibiYapilsin ? 'Evet' : 'Hayır'}</Text>
                    </View>
                    <View style={styles.detailInfoRow}>
                      <Text style={styles.detailInfoLabel}>Geçmiş Vadede Engelle:</Text>
                      <Text style={styles.detailInfoValue}>{selectedCari.vadeGecmisteEngelle ? 'Evet' : 'Hayır'}</Text>
                    </View>
                    <View style={styles.detailInfoRow}>
                      <Text style={styles.detailInfoLabel}>Faturada Risk Kontrolü:</Text>
                      <Text style={styles.detailInfoValue}>{selectedCari.faturadaRiskKontrolu !== false ? 'Evet' : 'Hayır'}</Text>
                    </View>
                    <View style={styles.detailInfoRow}>
                      <Text style={styles.detailInfoLabel}>Kart Durumu:</Text>
                      <Text style={[styles.detailInfoValue, { color: selectedCari.aktifMi !== false ? '#10B981' : '#EF4444', fontWeight: 'bold' }]}>
                        {selectedCari.aktifMi !== false ? 'Aktif' : 'Pasif'}
                      </Text>
                    </View>
                  </View>

                  <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginTop: 10, paddingBottom: 20 }}>
                    <TouchableOpacity style={[styles.editButton, { flex: 1, marginRight: 8 }]} onPress={() => handleOpenEdit(selectedCari)}>
                      <Edit3 color="#FFF" size={18} />
                      <Text style={styles.editButtonText}>Düzenle</Text>
                    </TouchableOpacity>
                    <TouchableOpacity style={[styles.deleteButton, { flex: 1 }]} onPress={() => handleDelete(selectedCari)}>
                      <Trash2 color="#FFF" size={18} />
                      <Text style={styles.deleteButtonText}>Sil</Text>
                    </TouchableOpacity>
                  </View>
                </ScrollView>
              </>
            )}
          </View>
        </SafeAreaView>
      </Modal>

      {/* Hızlı Finans Tahsilat / Ödeme Giriş Modalı */}
      <Modal visible={isTransactionOpen} transparent={true} animationType="slide">
        <SafeAreaView style={styles.modalOverlay} {...transPanResponder.panHandlers}>
          <View style={styles.modalContent}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>{transType} Ekle</Text>
              <TouchableOpacity onPress={() => { setIsTransactionOpen(false); setIsDetailOpen(true); }}>
                <X color="#FFF" size={24} />
              </TouchableOpacity>
            </View>

            <ScrollView contentContainerStyle={styles.formScroll}>
              <View style={styles.inputGroup}>
                <Text style={styles.inputLabel}>Ödeme Yöntemi</Text>
                <View style={styles.toggleRow}>
                  {(['Nakit', 'Kredi Kartı', 'Havale/EFT'] as const).map(m => (
                    <TouchableOpacity
                      key={m}
                      style={[styles.toggleBtn, transMethod === m && styles.toggleBtnActive]}
                      onPress={() => { setTransMethod(m); setSelectedAccount(null); setTransTarget(m === 'Nakit' ? 'Kasa' : 'Banka'); }}
                    >
                      <Text style={[styles.toggleBtnText, transMethod === m && styles.toggleBtnTextActive]}>{m === 'Havale/EFT' ? 'EFT/Havale' : m}</Text>
                    </TouchableOpacity>
                  ))}
                </View>
              </View>

              <View style={styles.inputGroup}>
                <Text style={styles.inputLabel}>Hesap Seçimi *</Text>
                <TouchableOpacity style={styles.selectorCard} onPress={() => setIsAccountModalOpen(true)}>
                  <Text style={styles.selectorText}>
                    {selectedAccount ? selectedAccount.ad || selectedAccount.isim || selectedAccount.hesapAdi : 'Hesap Seçin...'}
                  </Text>
                </TouchableOpacity>
              </View>

              <View style={styles.inputGroup}>
                <Text style={styles.inputLabel}>İşlem Tarihi</Text>
                <TextInput style={styles.input} placeholder="YYYY-MM-DD" placeholderTextColor="#64748B" value={transDate} onChangeText={setTransDate} />
              </View>

              <View style={styles.inputGroup}>
                <Text style={styles.inputLabel}>Tutar (TL) *</Text>
                <TextInput style={styles.input} keyboardType="numeric" inputAccessoryViewID={KEYBOARD_ACCESSORY_ID} placeholder="0.00" placeholderTextColor="#64748B" value={transAmount} onChangeText={setTransAmount} />
              </View>

              {transMethod !== 'Nakit' && (
                <View style={{ marginBottom: 12 }}>
                  <View style={styles.inputGroup}>
                    <Text style={styles.inputLabel}>{transMethod === 'Kredi Kartı' ? 'Banka (KK)' : 'Banka (EFT)'}</Text>
                    <TextInput style={styles.input} placeholder="örn. İş Bankası" placeholderTextColor="#64748B" value={kkBanka} onChangeText={setKkBanka} />
                  </View>
                  <View style={styles.inputGroup}>
                    <Text style={styles.inputLabel}>{transMethod === 'Kredi Kartı' ? 'Kart No (Son 4 Hane)' : 'IBAN / Hesap No'}</Text>
                    <TextInput style={styles.input} placeholder={transMethod === 'Kredi Kartı' ? '1234' : 'TR00 ...'} placeholderTextColor="#64748B" value={kkKartNo} onChangeText={setKkKartNo} />
                  </View>
                </View>
              )}

              <View style={styles.inputGroup}>
                <Text style={styles.inputLabel}>Açıklama</Text>
                <TextInput style={styles.input} placeholder="İşlem açıklaması..." placeholderTextColor="#64748B" value={transDesc} onChangeText={setTransDesc} />
              </View>

              <TouchableOpacity style={[styles.saveButton, isSavingTransaction && { opacity: 0.7 }]} disabled={isSavingTransaction} onPress={handleSaveTransaction}>
                {isSavingTransaction ? <ActivityIndicator color="#FFF" size="small" /> : <Save color="#FFF" size={20} />}
                <Text style={styles.saveButtonText}>{isSavingTransaction ? 'Kaydediliyor...' : 'Kaydet'}</Text>
              </TouchableOpacity>
            </ScrollView>
          </View>
        </SafeAreaView>
      </Modal>

      {/* Kasa / Banka Seçim Modalı */}
      <Modal visible={isAccountModalOpen} transparent={true} animationType="slide">
        <SafeAreaView style={styles.modalOverlay}>
          <View style={styles.modalContent}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>{transTarget} Seçin</Text>
              <TouchableOpacity onPress={() => setIsAccountModalOpen(false)}>
                <X color="#FFF" size={24} />
              </TouchableOpacity>
            </View>

            <FlashList 
              data={transTarget === 'Kasa' ? kasalar : bankalar}
              keyExtractor={(item, index) => item.firebaseKey || index.toString()}
              renderItem={({ item }) => (
                <TouchableOpacity 
                  style={styles.selectorItem}
                  onPress={() => {
                    setSelectedAccount(item);
                    setIsAccountModalOpen(false);
                  }}
                >
                  <Text style={styles.selectorItemText}>{item.ad || item.isim}</Text>
                  <Text style={styles.selectorItemSub}>Bakiye: {formatMoney(item.bakiye || 0)}</Text>
                </TouchableOpacity>
              )}
            />
          </View>
        </SafeAreaView>
      </Modal>

      {/* Yaşlandırma Analizi Modalı */}
      <Modal visible={isYaslandirmaOpen} transparent={true} animationType="fade">
        <SafeAreaView style={styles.modalOverlay}>
          <View style={[styles.modalContent, { justifyContent: 'center', alignItems: 'center', backgroundColor: 'rgba(0,0,0,0.5)' }]}>
            <View style={{ backgroundColor: '#161616', width: '90%', borderRadius: 24, padding: 24, borderWidth: 1, borderColor: 'rgba(255,255,255,0.08)' }}>
              <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
                <Text style={{ color: '#FFF', fontSize: 16, fontWeight: 'bold' }}>Cari Borç Yaşlandırma Analizi</Text>
                <TouchableOpacity onPress={() => { setIsYaslandirmaOpen(false); setIsDetailOpen(true); }}>
                  <X color="#FFF" size={24} />
                </TouchableOpacity>
              </View>
              {selectedCari && (() => {
                const ageData = getCariYaslandirma();
                const totalAge = ageData.g0_30 + ageData.g31_60 + ageData.g61_90 + ageData.g90_plus;
                return (
                  <View>
                    <View style={{ marginBottom: 12 }}>
                      <Text style={{ color: '#94A3B8', fontSize: 11, fontWeight: 'bold', textTransform: 'uppercase' }}>Vadesine Göre Dağılım</Text>
                    </View>
                    <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginVertical: 8 }}>
                      <Text style={{ color: '#94A3B8', fontSize: 13 }}>Vadesi Gelmemiş:</Text>
                      <Text style={{ color: '#10B981', fontSize: 13, fontWeight: 'bold' }}>{formatMoney(ageData.vadesiGelmemis)}</Text>
                    </View>
                    <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginVertical: 8 }}>
                      <Text style={{ color: '#FFF', fontSize: 13 }}>0 - 30 Gün Geciken:</Text>
                      <Text style={{ color: '#FFF', fontSize: 13, fontWeight: 'bold' }}>{formatMoney(ageData.g0_30)}</Text>
                    </View>
                    <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginVertical: 8 }}>
                      <Text style={{ color: '#FFF', fontSize: 13 }}>31 - 60 Gün Geciken:</Text>
                      <Text style={{ color: '#FFF', fontSize: 13, fontWeight: 'bold' }}>{formatMoney(ageData.g31_60)}</Text>
                    </View>
                    <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginVertical: 8 }}>
                      <Text style={{ color: '#FFF', fontSize: 13 }}>61 - 90 Gün Geciken:</Text>
                      <Text style={{ color: '#FFF', fontSize: 13, fontWeight: 'bold' }}>{formatMoney(ageData.g61_90)}</Text>
                    </View>
                    <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginVertical: 8 }}>
                      <Text style={{ color: '#FFF', fontSize: 13 }}>90+ Gün Geciken:</Text>
                      <Text style={{ color: '#EF4444', fontSize: 13, fontWeight: 'bold' }}>{formatMoney(ageData.g90_plus)}</Text>
                    </View>
                    <View style={{ height: 1, backgroundColor: 'rgba(255,255,255,0.05)', marginVertical: 12 }} />
                    <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginVertical: 4 }}>
                      <Text style={{ color: '#FFF', fontSize: 14, fontWeight: 'bold' }}>Toplam Geciken Borç:</Text>
                      <Text style={{ color: '#EF4444', fontSize: 14, fontWeight: 'bold' }}>{formatMoney(totalAge)}</Text>
                    </View>
                    <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginBottom: 16 }}>
                      <Text style={{ color: '#FFF', fontSize: 14, fontWeight: 'bold' }}>Toplam Açık Bakiye:</Text>
                      <Text style={{ color: '#FFF', fontSize: 14, fontWeight: 'bold' }}>{formatMoney(totalAge + ageData.vadesiGelmemis)}</Text>
                    </View>
                    <TouchableOpacity style={[styles.saveButton, { backgroundColor: '#10B981', marginTop: 10 }]} onPress={handleExportYaslandirmaPdf}>
                      <FileText color="#FFF" size={20} />
                      <Text style={styles.saveButtonText}>PDF Rapor Paylaş</Text>
                    </TouchableOpacity>
                  </View>
                );
              })()}
            </View>
          </View>
        </SafeAreaView>
      </Modal>

      {/* iOS Sayısal Klavye Bitti Butonu */}
      <KeyboardDoneAccessory />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#070709',
  },
  center: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  appleHigHeader: {
    paddingHorizontal: 16,
    paddingTop: 10,
    paddingBottom: 14,
    backgroundColor: '#000000',
  },
  segmentedBar: {
    flexDirection: 'row',
    gap: 8,
    paddingBottom: 12,
  },
  segmentedTab: {
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
    paddingHorizontal: 14,
    paddingVertical: 7,
    borderRadius: 18,
  },
  segmentedTabActive: {
    backgroundColor: '#FFFFFF',
  },
  segmentedTabText: {
    color: '#8E8E93',
    fontSize: 13,
    fontWeight: '600',
  },
  segmentedTabTextActive: {
    color: '#000000',
    fontWeight: '700',
  },
  appleDateHeaderRow: {
    marginBottom: 14,
  },
  appleMonthTitle: {
    fontSize: 26,
    fontWeight: '800',
    color: '#FFFFFF',
    letterSpacing: -0.5,
  },
  appleBigBalance: {
    fontSize: 20,
    fontWeight: '700',
    color: '#8E8E93',
    letterSpacing: -0.4,
  },
  appleSectionLabel: {
    fontSize: 13,
    fontWeight: '600',
    color: '#8E8E93',
    marginBottom: 8,
    marginLeft: 4,
  },
  appleFloatingPill: {
    position: 'absolute',
    bottom: 24,
    alignSelf: 'center',
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#FFFFFF',
    paddingHorizontal: 20,
    paddingVertical: 12,
    borderRadius: 25,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 6 },
    shadowOpacity: 0.35,
    shadowRadius: 10,
    elevation: 8,
    gap: 6,
  },
  appleFloatingPillText: {
    color: '#000000',
    fontSize: 15,
    fontWeight: '700',
  },
  appleSearchBox: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
    borderRadius: 14,
    paddingHorizontal: 14,
    height: 44,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.06)',
  },
  appleSearchInput: {
    flex: 1,
    paddingVertical: 10,
    paddingHorizontal: 10,
    color: '#FFFFFF',
    fontSize: 15,
  },
  header: {
    backgroundColor: '#070709',
    zIndex: 10,
    paddingHorizontal: 16,
    paddingTop: Platform.OS === 'ios' ? 12 : 28,
    paddingBottom: 14,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.08)',
  },
  headerTitle: {
    fontSize: 24,
    fontWeight: '800',
    color: '#FFFFFF',
    letterSpacing: -0.4,
  },
  addButton: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#0061FF',
    paddingHorizontal: 15,
    paddingVertical: 8,
    borderRadius: 12,
  },
  addButtonText: {
    color: '#FFF',
    fontWeight: 'bold',
    marginLeft: 6,
    fontSize: 14,
  },
  searchBox: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
    borderRadius: 12,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    paddingHorizontal: 12,
    height: 44,
  },
  searchInput: {
    flex: 1,
    paddingVertical: 10,
    paddingHorizontal: 8,
    color: '#FFFFFF',
    fontSize: 15,
  },
  tableHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: 4,
    paddingTop: 12,
    paddingBottom: 2,
  },
  tableHeaderTitle: {
    color: '#94A3B8',
    fontSize: 12,
    fontWeight: '700',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  tableHeaderSub: {
    color: '#64748B',
    fontSize: 11,
    fontWeight: '500',
  },
  bakiyeSortHeaderBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 10,
    paddingVertical: 5,
    borderRadius: 8,
    backgroundColor: 'rgba(255, 255, 255, 0.05)',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.1)',
  },
  bakiyeSortHeaderBtnActive: {
    backgroundColor: 'rgba(0, 97, 255, 0.18)',
    borderColor: '#0061FF',
  },
  bakiyeSortHeaderText: {
    color: '#94A3B8',
    fontSize: 12,
    fontWeight: '700',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  bakiyeSortHeaderTextActive: {
    color: '#38BDF8',
    fontWeight: '800',
  },
  bakiyeSortIndicator: {
    color: '#38BDF8',
    fontSize: 11,
    fontWeight: '800',
    marginLeft: 3,
  },
  appleListWrapper: {
    flex: 1,
    paddingHorizontal: 16,
    paddingTop: 12,
  },
  listContent: {
    backgroundColor: '#1C1C1E',
    borderRadius: 14,
    overflow: 'hidden',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.06)',
    paddingBottom: 20,
  },
  emptyText: {
    color: '#64748B',
    textAlign: 'center',
    marginTop: 40,
  },
  card: {
    backgroundColor: '#0F0F0F',
    borderRadius: 16,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    padding: 16,
    marginBottom: 12,
  },
  appleCard: {
    marginBottom: 12,
  },
  appleAvatarBox: {
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: 'rgba(10, 132, 255, 0.12)',
    borderWidth: 1,
    borderColor: 'rgba(10, 132, 255, 0.25)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  appleCardTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: '#FFFFFF',
    letterSpacing: -0.2,
  },
  appleCardSubtitle: {
    fontSize: 13,
    color: 'rgba(235, 235, 245, 0.6)',
    marginTop: 2,
    fontWeight: '500',
  },
  appleCardFooter: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginTop: 14,
    paddingTop: 12,
    borderTopWidth: 1,
    borderTopColor: 'rgba(255, 255, 255, 0.06)',
  },
  cardHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.08)',
    paddingBottom: 12,
    marginBottom: 12,
  },
  avatarBox: {
    width: 40,
    height: 40,
    borderRadius: 10,
    backgroundColor: 'rgba(0, 97, 255, 0.15)',
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: 12,
  },
  cardInfo: {
    flex: 1,
  },
  cardTitle: {
    fontSize: 15,
    fontWeight: 'bold',
    color: '#FFFFFF',
  },
  cardSubtitle: {
    fontSize: 11,
    color: '#94A3B8',
    marginTop: 2,
  },
  cardFooter: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  footerLabel: {
    color: '#64748B',
    fontSize: 10,
    fontWeight: '600',
    marginBottom: 4,
  },
  footerValue: {
    fontSize: 14,
    fontWeight: 'bold',
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
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 20,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.08)',
    paddingBottom: 16,
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#FFF',
  },
  closeButton: {
    padding: 4,
  },
  formScroll: {
    paddingBottom: 40,
  },
  inputGroup: {
    marginBottom: 16,
  },
  inputLabel: {
    color: '#94A3B8',
    fontSize: 13,
    fontWeight: '600',
    marginBottom: 8,
  },
  input: {
    backgroundColor: '#2A2A2A',
    borderRadius: 10,
    padding: 14,
    color: '#FFF',
    fontSize: 15,
    borderWidth: 1,
    borderColor: '#444',
  },
  toggleRow: {
    flexDirection: 'row',
    backgroundColor: '#2A2A2A',
    borderRadius: 12,
    padding: 4,
    height: 48,
    alignItems: 'center',
  },
  toggleBtn: {
    flex: 1,
    height: '100%',
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 8,
  },
  toggleBtnActive: {
    backgroundColor: '#0061FF',
  },
  toggleBtnText: {
    color: '#94A3B8',
    fontWeight: 'bold',
    fontSize: 13,
  },
  toggleBtnTextActive: {
    color: '#FFF',
  },
  saveButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#10B981',
    borderRadius: 12,
    padding: 16,
    marginTop: 20,
  },
  saveButtonText: {
    color: '#FFF',
    fontWeight: 'bold',
    fontSize: 16,
    marginLeft: 8,
  },
  detailCard: {
    backgroundColor: '#161616',
    borderRadius: 20,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    padding: 24,
    marginBottom: 20,
  },
  detailUnvan: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#FFF',
    textAlign: 'center',
  },
  detailGrup: {
    fontSize: 12,
    color: '#64748B',
    textAlign: 'center',
    marginTop: 4,
  },
  detailBakiyeBox: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    marginTop: 20,
    borderTopWidth: 1,
    borderTopColor: 'rgba(255, 255, 255, 0.08)',
    paddingTop: 16,
  },
  detailBakiyeLabel: {
    color: '#64748B',
    fontSize: 11,
    fontWeight: '600',
    marginBottom: 6,
  },
  detailBakiyeVal: {
    fontSize: 15,
    fontWeight: '900',
  },
  detailSection: {
    backgroundColor: '#161616',
    borderRadius: 20,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    padding: 16,
    marginBottom: 20,
  },
  detailSectionTitle: {
    fontSize: 14,
    fontWeight: 'bold',
    color: '#FFF',
    marginBottom: 16,
  },
  detailRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 12,
  },
  detailText: {
    color: '#94A3B8',
    fontSize: 14,
    marginLeft: 12,
  },
  detailInfoRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 10,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255,255,255,0.03)',
    paddingBottom: 8,
  },
  detailInfoLabel: {
    color: '#64748B',
    fontSize: 13,
  },
  detailInfoValue: {
    color: '#FFF',
    fontSize: 13,
    fontWeight: '500',
  },
  editButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
    borderRadius: 12,
    padding: 14,
  },
  editButtonText: {
    color: '#FFF',
    fontWeight: 'bold',
    marginLeft: 8,
  },
  deleteButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(239, 68, 68, 0.1)',
    borderRadius: 12,
    padding: 14,
  },
  deleteButtonText: {
    color: '#EF4444',
    fontWeight: 'bold',
    marginLeft: 8,
  },
  quickActionBtn: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 14,
    borderRadius: 12,
    marginHorizontal: 4,
  },
  quickActionBtnText: {
    color: '#FFF',
    fontWeight: 'bold',
    marginLeft: 8,
    fontSize: 13,
  },
  hareketRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 10,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255,255,255,0.03)',
  },
  hareketType: {
    color: '#FFF',
    fontSize: 13,
    fontWeight: '600',
  },
  hareketDate: {
    color: '#64748B',
    fontSize: 11,
    marginTop: 2,
  },
  hareketVal: {
    fontSize: 13,
    fontWeight: 'bold',
  },
  selectorCard: {
    backgroundColor: '#2A2A2A',
    borderRadius: 12,
    padding: 16,
    borderWidth: 1,
    borderColor: '#444',
  },
  selectorText: {
    color: '#FFF',
    fontSize: 14,
    fontWeight: '600',
  },
  selectorItem: {
    backgroundColor: 'rgba(255,255,255,0.02)',
    padding: 16,
    borderRadius: 12,
    marginBottom: 8,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.05)',
  },
  selectorItemText: {
    color: '#FFF',
    fontSize: 15,
    fontWeight: 'bold',
  },
  selectorItemSub: {
    color: '#64748B',
    fontSize: 12,
    marginTop: 4,
  },
  switchGroup: {
    backgroundColor: 'rgba(255,255,255,0.02)',
    padding: 16,
    borderRadius: 12,
    marginBottom: 20,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.05)',
  },
  switchRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 8,
  },
  switchLabel: {
    color: '#FFF',
    fontSize: 14,
    fontWeight: '600',
  },
  actionGridBtn: {
    width: '48%',
    height: 40,
    borderRadius: 10,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    marginVertical: 2,
  },
  actionGridText: {
    color: '#FFF',
    fontSize: 11,
    fontWeight: 'bold',
  },
  inlineDropdown: {
    position: 'absolute',
    top: 75,
    left: 0,
    right: 0,
    backgroundColor: '#1E1E1E',
    borderRadius: 10,
    borderWidth: 1,
    borderColor: '#444',
    zIndex: 999,
  },
  inlineDropdownItem: {
    padding: 12,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255,255,255,0.05)',
  },
  inlineDropdownText: {
    color: '#FFF',
    fontSize: 14,
  }
});
