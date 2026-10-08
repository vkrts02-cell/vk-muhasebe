import React, { useState, useEffect, useRef } from 'react';
import { StyleSheet, Text, View, SafeAreaView, FlatList, TextInput, ActivityIndicator, TouchableOpacity, Modal, ScrollView, Alert, Share, Switch } from 'react-native';
import { FlashList } from '@shopify/flash-list';
import { Search, FileText, Plus, X, Save, Edit3, Trash2, Calendar, User, ShoppingBag, Share2, RefreshCw, Grid3X3, FileDown, Package } from 'lucide-react-native';
import { subscribeToPath, writeData, readData, deleteData, mapAppToDatabase, splitAccounts, mergeKasalar } from '../services/firebase';
import { generateReportPdf } from '../services/pdfService';
import { exportToExcel } from '../services/excelService';
import { generateInt32Id } from '../utils/IdGenerator';
import { Mail as MailIcon } from 'lucide-react-native';
import { sendInvoiceMail } from '../services/mailService';
import { OfflineNetworkBar } from '../components/OfflineNetworkBar';
import { AppleListRow } from '../components/AppleGroupedList';
import { ShimmerCardList } from '../components/Shimmer';
import { AppleTheme } from '../theme/appleDesign';
import { deleteFaturaCascade } from '../services/transactionService';

const formatMoney = (val: number) => {
  return new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(val);
};

const formatDateInput = (d: Date) => {
  const day = String(d.getDate()).padStart(2, '0');
  const month = String(d.getMonth() + 1).padStart(2, '0');
  const year = d.getFullYear();
  return `${day}.${month}.${year}`;
};

const parseTurkishDate = (dateStr: string) => {
  try {
    if (dateStr.includes('/') || dateStr.includes('.')) {
      const parts = dateStr.split(/[\/.]/);
      const day = parseInt(parts[0]);
      const month = parseInt(parts[1]) - 1;
      const year = parseInt(parts[2]);
      return new Date(year, month, day);
    }
    return new Date(dateStr);
  } catch {
    return new Date();
  }
};

export default function FaturalarScreen({ route, navigation }: any) {
  const [faturalar, setFaturalar] = useState<any[]>([]);
  const [cariler, setCariler] = useState<any[]>([]);
  const [stoklar, setStoklar] = useState<any[]>([]);
  const [faturaDetaylarMap, setFaturaDetaylarMap] = useState<Record<string, any[]>>({});
  const [faturaItems, setFaturaItems] = useState<any[]>([]);
  const [loadingDetay, setLoadingDetay] = useState(false);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');

  const today = new Date();
  const firstOfMonth = new Date(today.getFullYear(), today.getMonth(), 1);
  const [startDateStr, setStartDateStr] = useState(formatDateInput(firstOfMonth));
  const [endDateStr, setEndDateStr] = useState(formatDateInput(today));

  // Modals state
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [isDetailOpen, setIsDetailOpen] = useState(false);
  const [isCariOverlayOpen, setIsCariOverlayOpen] = useState(false);
  const [isStokOverlayOpen, setIsStokOverlayOpen] = useState(false);
  
  // Saving guard to prevent double-tap double-save
  const [isSaving, setIsSaving] = useState(false);
  const isSavingRef = useRef(false);
  
  const [selectedFatura, setSelectedFatura] = useState<any | null>(null);

  const handleShareFatura = async (fatura: any) => {
    try {
      const detaylar = faturaDetaylarMap[fatura.id] || [];
      const mappedFatura = mapAppToDatabase('Faturalar', fatura);
      const mappedDetaylar = detaylar.map(d => mapAppToDatabase('FaturaDetaylar', d));
      
      const payload = {
        Fatura: mappedFatura,
        Detaylar: mappedDetaylar
      };

      const reportFileName = `Fatura_${fatura.faturaNo}.pdf`;
      await generateReportPdf('fatura', payload, reportFileName);
    } catch (error) {
      console.error('Paylaşım hatası:', error);
      Alert.alert('Hata', 'Fatura PDF\'i paylaşılırken bir sorun oluştu.');
    }
  };

  const handleExportAllToPdf = async () => {
    try {
      const filtered = getFilteredFaturalar();
      if (filtered.length === 0) {
        Alert.alert('Uyarı', 'Dışa aktarılacak fatura bulunamadı.');
        return;
      }

      const mappedFaturalar = filtered.map(f => mapAppToDatabase('Faturalar', f));

      let sDate = new Date(new Date().getFullYear(), 0, 1);
      let eDate = new Date();
      try {
         sDate = parseTurkishDate(startDateStr);
         eDate = parseTurkishDate(endDateStr);
      } catch {}

      const payload = {
        Items: mappedFaturalar,
        StartDate: sDate.toISOString(),
        EndDate: eDate.toISOString()
      };

      await generateReportPdf('fatura-list', payload, `Fatura_Listesi_${Date.now()}.pdf`);
    } catch (err) {
      console.error('Toplu PDF hatası:', err);
      Alert.alert('Hata', 'Toplu PDF oluşturulurken hata oluştu.');
    }
  };

  const handleExportToExcel = async () => {
    const filtered = getFilteredFaturalar().filter(f => !f.isDeleted);
    if (filtered.length === 0) {
      Alert.alert('Uyarı', 'Dışa aktarılacak fatura bulunamadı.');
      return;
    }
    const mappedRows = filtered.map(f => mapAppToDatabase('Faturalar', f));
    await exportToExcel(mappedRows, 'Faturalar', `Faturalar_${new Date().getFullYear()}_${new Date().getMonth() + 1}`);
  };

  const handleRefresh = () => {
    setLoading(true);
    setTimeout(() => setLoading(false), 300);
  };

  const handleOpenCreate = (type: 'Satis' | 'Alis') => {
    navigation.navigate('FaturaForm', { initialTur: type === 'Satis' ? 'Satış' : 'Alış' });
  };

  // Form fields state
  const [editingId, setEditingId] = useState<number | null>(null);
  const [draftIds, setDraftIds] = useState<{ faturaId: number; hareketId1: number; hareketId2: number } | null>(null);
  const [tur, setTur] = useState<'Satış' | 'Alış'>('Satış');
  const [faturaNo, setFaturaNo] = useState('');
  const [tarih, setTarih] = useState(new Date().toISOString().split('T')[0]);
  const [vadeTarihi, setVadeTarihi] = useState(new Date().toISOString().split('T')[0]);
  const [selectedCari, setSelectedCari] = useState<any | null>(null);
  const [aciklama, setAciklama] = useState('');
  const [durum, setDurum] = useState<'Açık' | 'Kapalı'>('Açık');
  const [odemeSekli, setOdemeSekli] = useState<'Açık Hesap' | 'Nakit' | 'Kredi Kartı'>('Açık Hesap');
  const [kasaId, setKasaId] = useState<number | null>(null);
  const [bankaId, setBankaId] = useState<number | null>(null);
  const [dovizTuru, setDovizTuru] = useState<string>('TL');
  const [dovizKuru, setDovizKuru] = useState<string>('1');
  const [baglantiEvrakNo, setBaglantiEvrakNo] = useState<string>('');
  
  // Accounts & balances states
  const [kasalar, setKasalar] = useState<any[]>([]);
  const [bankalar, setBankalar] = useState<any[]>([]);
  const [kasaHareketler, setKasaHareketler] = useState<any[]>([]);
  const [bankaHareketler, setBankaHareketler] = useState<any[]>([]);
  const [cariHareketler, setCariHareketler] = useState<any[]>([]);
  const bankKasalarRef = useRef<any[]>([]);
  const legacyKasalarRef = useRef<any[]>([]);
  const appliedRouteSignatureRef = useRef<string>('');

  // Invoice items state
  const [items, setItems] = useState<any[]>([]);
  
  // Search queries for selectors
  const [cariSearch, setCariSearch] = useState('');
  const [cariGrupFiltre, setCariGrupFiltre] = useState('');
  const [stokSearch, setStokSearch] = useState('');

  useEffect(() => {
    const unsubFaturalar = subscribeToPath('Faturalar', (data) => {
      if (!data) {
        setFaturalar([]);
      } else {
        const list = Array.isArray(data) 
          ? data.filter(Boolean) 
          : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setFaturalar(list.filter(f => f.isDeleted !== true).sort((a, b) => new Date(b.tarih).getTime() - new Date(a.tarih).getTime()));
      }
    });

    const unsubCariler = subscribeToPath('Cariler', (data) => {
      if (data) {
        const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setCariler(list.filter(c => c.isDeleted !== true));
      }
    });

    const unsubStoklar = subscribeToPath('Stoklar', (data) => {
      if (data) {
        const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setStoklar(list.filter(s => s.isDeleted !== true));
      }
    });

    const unsubDetaylar = subscribeToPath('FaturaDetaylar', (data) => {
      if (data) {
        setFaturaDetaylarMap(data);
      }
    });

    const unsubKasalar = subscribeToPath('Kasalar', (data) => {
      if (data) {
        const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        legacyKasalarRef.current = list.filter(k => k && k.isDeleted !== true);
        setKasalar(mergeKasalar(bankKasalarRef.current, legacyKasalarRef.current));
      } else {
        legacyKasalarRef.current = [];
        setKasalar(mergeKasalar(bankKasalarRef.current, legacyKasalarRef.current));
      }
    });

    const unsubBankalar = subscribeToPath('Bankalar', (data) => {
      if (data) {
        const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
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

    const unsubKasaH = subscribeToPath('KasaHareketler', (data) => {
      if (data) {
        const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setKasaHareketler(list);
      }
    });

    const unsubBankaH = subscribeToPath('BankaHareketler', (data) => {
      if (data) {
        const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setBankaHareketler(list);
      }
    });

    const unsubCariH = subscribeToPath('CariHareketler', (data) => {
      if (data) {
        const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setCariHareketler(list);
      }
      setLoading(false);
    });

    return () => {
      unsubFaturalar();
      unsubCariler();
      unsubStoklar();
      unsubDetaylar();
      unsubKasalar();
      unsubBankalar();
      unsubKasaH();
      unsubBankaH();
      unsubCariH();
    };
  }, []);

  useEffect(() => {
    const rParams = route?.params || {};
    const initialCari = rParams.initialCari;
    const editFaturaId = rParams.editFaturaId;

    if (initialCari) {
      const selected = initialCari;
      setSelectedCari(selected);
      if (rParams.initialTur) {
        setTur(rParams.initialTur);
      }
      setFaturaNo(generateFaturaNo(rParams.initialTur || 'Satış'));
      setTarih(new Date().toISOString().split('T')[0]);
      setVadeTarihi(new Date().toISOString().split('T')[0]);
      setAciklama('');
      setDurum('Açık');
      setItems([]);
      if (selected && (selected.vadeGunu || 0) > 0) {
        const vadeDate = new Date(Date.now() + (selected.vadeGunu * 24 * 60 * 60 * 1000));
        setVadeTarihi(vadeDate.toISOString().split('T')[0]);
      }
      setIsFormOpen(true);
    } else if (editFaturaId) {
      const loadFaturaForEdit = async () => {
        setLoading(true);
        try {
          const fId = editFaturaId;
          const fData = faturalar.find(f => String(f.id) === String(fId)) || await readData(`Faturalar/${fId}`);
          if (fData) {
            setEditingId(fId);
            setTur(fData.tur || 'Satış');
            setFaturaNo(fData.faturaNo || '');
            setTarih(fData.tarih || '');
            setVadeTarihi(fData.vadeTarihi || '');
            
            const cariRef = cariler.find(c => c.id === fData.cariId) || await readData(`Cariler/${fData.cariId}`);
            setSelectedCari(cariRef);
            
            setAciklama(fData.aciklama || '');
            setDurum(fData.durum || 'Açık');
            setOdemeSekli(fData.odemeSekli || 'Açık Hesap');
            setKasaId(fData.kasaId || null);
            setBankaId(fData.bankaId || null);
            setDovizTuru(fData.dovizTuru || 'TL');
            setDovizKuru(fData.dovizKuru ? fData.dovizKuru.toString() : '1');
            setBaglantiEvrakNo(fData.baglantiEvrakNo || '');
            
            // Detayları oku
            const tumDetaylar = await readData(`FaturaDetaylar/${fId}`) || [];
            const listDetaylar = Array.isArray(tumDetaylar) 
              ? tumDetaylar.filter(Boolean) 
              : Object.keys(tumDetaylar).map(key => ({ ...(tumDetaylar as any)[key], id: parseInt(key) }));
              
            setItems(listDetaylar.map(d => ({
              stokId: d.stokId,
              stokKodu: d.stokKodu || '',
              stokAdi: d.stokAdi,
              birim: d.birim || 'Adet',
              miktar: d.miktar || 1,
              birimFiyat: d.birimFiyat || d.fiyat || 0,
              kdvOrani: d.kdvOrani || 20,
              aciklama: d.aciklama || ''
            })));
            
            setIsFormOpen(true);
          }
        } catch (err) {
          Alert.alert('Hata', 'Fatura düzenleme modunda yüklenemedi.');
        } finally {
          setLoading(false);
        }
      };
      loadFaturaForEdit();
    }
  }, [route?.params, faturalar, cariler]);

  const getFilteredFaturalar = () => {
    const startDate = parseTurkishDate(startDateStr);
    const endDate = parseTurkishDate(endDateStr);
    endDate.setHours(23, 59, 59, 999);

    return faturalar.filter(f => {
      const matchSearch = (f.cariUnvan || '').toLocaleLowerCase('tr-TR').includes(searchQuery.toLocaleLowerCase('tr-TR')) ||
        (f.faturaNo || '').toLocaleLowerCase('tr-TR').includes(searchQuery.toLocaleLowerCase('tr-TR'));

      const faturaDate = new Date(f.tarih);
      const matchDate = faturaDate >= startDate && faturaDate <= endDate;

      return matchSearch && matchDate;
    });
  };

  const filteredFaturalar = getFilteredFaturalar();

  const generateFaturaNo = (type: 'Satış' | 'Alış') => {
    const prefix = type === 'Satış' ? 'FTR' : 'ALF';
    const random = Math.floor(100000 + Math.random() * 900000);
    return `${prefix}-${random}`;
  };

  const resetForm = () => {
    setEditingId(null);
    setDraftIds(null);
    setTur('Satış');
    setFaturaNo(generateFaturaNo('Satış'));
    setTarih(new Date().toISOString().split('T')[0]);
    setVadeTarihi(new Date().toISOString().split('T')[0]);
    setSelectedCari(null);
    setAciklama('');
    setDurum('Açık');
    setOdemeSekli('Açık Hesap');
    setKasaId(null);
    setBankaId(null);
    setDovizTuru('TL');
    setDovizKuru('1');
    setBaglantiEvrakNo('');
    setItems([]);
  };

  const handleOpenAdd = () => {
    navigation.navigate('FaturaForm');
  };

  // Cari seçildiğinde vade tarihini cari VadeGunu'na göre otomatik hesapla (masaüstü paritesi)
  const handleSelectCari = (cari: any) => {
    setSelectedCari(cari);
    if (cari && (cari.vadeGunu || 0) > 0) {
      const baseDate = tarih ? new Date(tarih) : new Date();
      if (!isNaN(baseDate.getTime())) {
        const vadeDate = new Date(baseDate.getTime() + (cari.vadeGunu * 24 * 60 * 60 * 1000));
        setVadeTarihi(vadeDate.toISOString().split('T')[0]);
      }
    }
  };

  const calculateSubtotal = () => {
    return items.reduce((sum, item) => sum + (item.miktar * item.birimFiyat), 0);
  };

  const calculateTotalKdv = () => {
    return items.reduce((sum, item) => sum + (item.miktar * item.birimFiyat * (item.kdvOrani || 20) / 100), 0);
  };

  const calculateGrandTotal = () => {
    return calculateSubtotal() + calculateTotalKdv();
  };

  const handleAddItem = (stok: any) => {
    const newItem = {
      stokId: stok.id,
      stokKodu: stok.stokKodu || stok.kodu || '',
      stokAdi: stok.stokAdi,
      birim: stok.birim || 'Adet',
      miktar: 1,
      birimFiyat: tur === 'Satış' ? (stok.satisFiyati || 0) : (stok.alisFiyati || 0),
      kdvOrani: stok.kdv || 20,
      aciklama: '',
    };
    setItems([...items, newItem]);
    setIsStokOverlayOpen(false);
  };

  const handleRemoveItem = (index: number) => {
    const newItems = [...items];
    newItems.splice(index, 1);
    setItems(newItems);
  };

  const handleItemChange = (index: number, field: string, val: string) => {
    const newItems = [...items];
    if (field === 'miktar') {
      newItems[index].miktar = parseFloat(val) || 0;
    } else if (field === 'birimFiyat') {
      newItems[index].birimFiyat = parseFloat(val) || 0;
    } else if (field === 'kdvOrani') {
      newItems[index].kdvOrani = parseInt(val) || 0;
    } else if (field === 'aciklama') {
      newItems[index].aciklama = val;
    }
    setItems(newItems);
  };

  const handleSave = async () => {
    if (isSavingRef.current) return;
    isSavingRef.current = true;
    setIsSaving(true);
    
    try {
      if (!selectedCari) {
        Alert.alert('Hata', 'Lütfen bir cari seçiniz.');
        return;
      }
    if (items.length === 0) {
      Alert.alert('Hata', 'Lütfen en az bir stok kalemi ekleyiniz.');
      return;
    }

    if (odemeSekli === 'Nakit' && !kasaId) {
      Alert.alert('Hata', 'Lütfen işlem yapılacak Kasayı seçiniz.');
      return;
    }
    if (odemeSekli === 'Kredi Kartı' && !bankaId) {
      Alert.alert('Hata', 'Lütfen işlem yapılacak Banka/Hesabı seçiniz.');
      return;
    }

    const isPaid = odemeSekli !== 'Açık Hesap';
    const finalDurum = isPaid ? 'Kapalı' : durum;

    const currentId = editingId || (draftIds ? draftIds.faturaId : generateInt32Id());

    // Düzenleme durumunda eski fatura etkilerini geri al (çift sayımı önler)
    if (editingId) {
      const reverted = await revertFaturaEffects(editingId);
      if (!reverted) {
        Alert.alert('Hata', 'Mevcut fatura etkileri geri alınamadı. Lütfen tekrar deneyin.');
        return;
      }
    }

    const subtotal = calculateSubtotal();
    const totalKdv = calculateTotalKdv();
    const grandTotal = calculateGrandTotal();

    // Müşteri/Tedarikçi risk limiti kontrolü (masaüstü FaturaOlustur paritesi)
    if ((selectedCari.riskTakibiYapilsin || selectedCari.faturadaRiskKontrolu) && (selectedCari.riskLimiti || 0) > 0) {
      const isMusteri = !['Alış', 'Alis'].includes(String(tur));
      const projected = isMusteri
        ? ((selectedCari.borc || 0) - (selectedCari.alacak || 0)) + grandTotal
        : ((selectedCari.alacak || 0) - (selectedCari.borc || 0)) + grandTotal;
      if (projected > selectedCari.riskLimiti) {
        if (selectedCari.faturadaRiskKontrolu === true) {
          Alert.alert('RİSK LİMİTİ AŞILDI!', `İşlem engellendi.\nLimit: ${formatMoney(selectedCari.riskLimiti)}, Tahmini kalan: ${formatMoney(projected)}`);
          return;
        }
        Alert.alert('UYARI', `Dikkat: Risk limiti aşıldı! (Limit: ${formatMoney(selectedCari.riskLimiti)})`);
      }
    }

    const faturaData = {
      id: currentId,
      faturaNo: faturaNo.trim(),
      tarih,
      vadeTarihi,
      cariId: selectedCari.id,
      cariUnvan: selectedCari.unvan,
      tur,
      durum: finalDurum,
      odemeSekli,
      kasaId: odemeSekli === 'Nakit' ? kasaId : null,
      bankaId: odemeSekli === 'Kredi Kartı' ? bankaId : null,
      dovizTuru,
      dovizKuru: parseFloat(dovizKuru) || 1,
      baglantiEvrakNo: baglantiEvrakNo.trim(),
      araToplam: subtotal,
      kdvToplam: totalKdv,
      genelToplam: grandTotal,
      odenen: isPaid ? grandTotal : 0,
      isDeleted: false,
      aciklama: aciklama.trim(),
    };

    // 1. Save Fatura
    const fSuccess = await writeData(`Faturalar/${currentId}`, faturaData);
    if (!fSuccess) {
      Alert.alert('Hata', 'Fatura kaydedilirken bir hata oluştu.');
      setIsSaving(false);
      return;
    }

    // 2. Save FaturaDetaylar
    const detayItems = items.map((item, idx) => ({
      id: generateInt32Id() + idx,
      faturaId: currentId,
      stokId: item.stokId,
      stokAdi: item.stokAdi,
      miktar: item.miktar,
      birim: item.birim,
      birimFiyat: item.birimFiyat,
      toplamTutar: item.miktar * item.birimFiyat,
      kdvOrani: item.kdvOrani,
      aciklama: item.aciklama || '',
    }));
    const okDetay = await writeData(`FaturaDetaylar/${currentId}`, detayItems);
    if (!okDetay) {
      Alert.alert('Hata', 'Fatura kaydedildi ancak detaylar eşitlenemedi. (Bağlantı sorunu — detaylar sıraya alındı.)');
    }

    const isSatisFatura = tur === 'Satış';
    for (let idx = 0; idx < items.length; idx++) {
      const item = items[idx];
      const shId = generateInt32Id() + idx;
      const stokHareket = {
        id: shId,
        stokId: item.stokId,
        stokKodu: item.stokKodu || '',
        stokAdi: item.stokAdi || '',
        tarih,
        islemTuru: isSatisFatura ? 'Satış Faturası' : 'Alış Faturası',
        evrakNo: faturaNo.trim(),
        faturaId: currentId,
        miktar: parseFloat(item.miktar) || 0,
        fiyat: parseFloat(item.birimFiyat) || 0,
        giren: isSatisFatura ? 0 : (parseFloat(item.miktar) || 0),
        cikan: isSatisFatura ? (parseFloat(item.miktar) || 0) : 0,
        aciklama: `Fatura No: ${faturaNo.trim()}`,
        isDeleted: false
      };
      const okSh = await writeData(`StokHareketler/${shId}`, stokHareket);
      if (!okSh) {
        Alert.alert('Uyarı', 'Fatura kaydedildi ancak bazı stok hareketleri eşitlenemedi. (Bağlantı sorunu — hareketler sıraya alındı.)');
      }
    }

    // 3. Update Cari Balance & CariHareketler
    const freshCari = await readData(`Cariler/${selectedCari.id}`);
    const cariRef = freshCari || cariler.find(c => String(c.id) === String(selectedCari.id));
    if (cariRef) {
      const updatedCari = { ...cariRef };
      if (isPaid) {
        // Kapalı faturalarda cari bakiye değişmez ama borç ve alacak aynı miktarda artar
        updatedCari.borc = (parseFloat(updatedCari.borc ?? updatedCari.Borc) || 0) + grandTotal;
        updatedCari.alacak = (parseFloat(updatedCari.alacak ?? updatedCari.Alacak) || 0) + grandTotal;
      } else {
        // Açık faturalarda Satış ise borçlanır, Alış ise alacaklanır
        if (tur === 'Satış') {
          updatedCari.borc = (parseFloat(updatedCari.borc ?? updatedCari.Borc) || 0) + grandTotal;
        } else {
          updatedCari.alacak = (parseFloat(updatedCari.alacak ?? updatedCari.Alacak) || 0) + grandTotal;
        }
      }
            updatedCari.bakiye = (parseFloat(updatedCari.borc ?? updatedCari.Borc) || 0) - (parseFloat(updatedCari.alacak ?? updatedCari.Alacak) || 0);
      updatedCari.updatedAt = new Date().toISOString();
      updatedCari.version = (cariRef.version || 0) + 1;
      const okCari = await writeData(`Cariler/${selectedCari.id}`, updatedCari);
      if (!okCari) {
        Alert.alert('Uyarı', 'Fatura kaydedildi ancak cari bakiye eşitlenemedi. (Bağlantı sorunu — bakiye sıraya alındı.)');
      }

      // CariHareket 1: Fatura Hareketi
      const nextHareketId1 = draftIds ? draftIds.hareketId1 : generateInt32Id();
      const cariHareket1 = {
        id: nextHareketId1,
        cariId: selectedCari.id,
        cariUnvan: selectedCari.unvan,
        tarih,
        islemTuru: tur === 'Satış' ? 'Satış Faturası' : 'Alış Faturası',
        evrakNo: faturaNo.trim(),
        borc: tur === 'Satış' ? grandTotal : 0,
        alacak: tur === 'Alış' ? grandTotal : 0,
        aciklama: aciklama.trim() || `${tur} faturası`,
        isDeleted: false,
        faturaId: currentId
      };
      const okCH1 = await writeData(`CariHareketler/${nextHareketId1}`, cariHareket1);
      if (!okCH1) {
        Alert.alert('Uyarı', 'Fatura hareketi eşitlenemedi. (Bağlantı sorunu — hareket sıraya alındı.)');
      }

      // CariHareket 2 (Kapalı Fatura ise): Ödeme / Tahsilat Hareketi
      if (isPaid) {
        const nextHareketId2 = draftIds ? draftIds.hareketId2 : nextHareketId1 + 1;
        const cariHareket2 = {
          id: nextHareketId2,
          cariId: selectedCari.id,
          cariUnvan: selectedCari.unvan,
          tarih,
          islemTuru: tur === 'Satış' ? 'Tahsilat' : 'Ödeme',
          evrakNo: `KPL-${faturaNo.trim()}`,
          borc: tur === 'Alış' ? grandTotal : 0,
          alacak: tur === 'Satış' ? grandTotal : 0,
          aciklama: `${odemeSekli} ile Fatura Kapatma`,
          isDeleted: false,
          faturaId: currentId
        };
        const okCH2 = await writeData(`CariHareketler/${nextHareketId2}`, cariHareket2);
        if (!okCH2) {
          Alert.alert('Uyarı', 'Fatura kapatma hareketi eşitlenemedi. (Bağlantı sorunu — hareket sıraya alındı.)');
        }
      }
    }

    // 4. Kasa / Banka Hareket Entegrasyonu (Kapalı Fatura ise)
    if (isPaid) {
      const nowStr = generateInt32Id();
      if (odemeSekli === 'Nakit' && kasaId) {
        const kasaRef = kasalar.find(k => k.id === kasaId);
        if (kasaRef) {
          const kasaHareket = {
            id: nowStr,
            hesapId: kasaId,
            tarih,
            tur: tur === 'Satış' ? 'Giriş' : 'Çıkış',
            tutar: grandTotal,
            aciklama: `${selectedCari.unvan} - FTR: ${faturaNo.trim()}`,
            cariId: selectedCari.id,
            isDeleted: false
          };
          const okKasaHareket = await writeData(`KasaHareketler/${nowStr}`, kasaHareket);
          if (!okKasaHareket) {
            Alert.alert('Uyarı', 'Fatura kaydedildi ancak kasa hareketi eşitlenemedi. (Bağlantı sorunu — hareket sıraya alındı.)');
          }

          // Kasa bakiyesini güncelle
          const yeniKasaBakiye = tur === 'Satış' ? (kasaRef.bakiye || 0) + grandTotal : (kasaRef.bakiye || 0) - grandTotal;
          const okKasaBakiye = await writeData(`Bankalar/${kasaId}`, {
            ...kasaRef,
            kartTuru: 'Kasa',
            bakiye: yeniKasaBakiye
          });
          if (!okKasaBakiye) {
            Alert.alert('Uyarı', 'Fatura kaydedildi ancak kasa bakiyesi eşitlenemedi. (Bağlantı sorunu — bakiye sıraya alındı.)');
          }
        }
      } else if (odemeSekli === 'Kredi Kartı' && bankaId) {
        const bankaRef = bankalar.find(b => b.id === bankaId);
        if (bankaRef) {
          const bankaHareket = {
            id: nowStr,
            bankaId,
            tarih,
            aciklama: `${selectedCari.unvan} - FTR: ${faturaNo.trim()}`,
            borc: tur === 'Satış' ? grandTotal : 0,
            alacak: tur === 'Alış' ? grandTotal : 0,
            isDeleted: false
          };
          const okBankaHareket = await writeData(`BankaHareketler/${nowStr}`, bankaHareket);
          if (!okBankaHareket) {
            Alert.alert('Uyarı', 'Fatura kaydedildi ancak banka hareketi eşitlenemedi. (Bağlantı sorunu — hareket sıraya alındı.)');
          }

          // Banka bakiyesini güncelle
          const yeniBankaBakiye = tur === 'Satış' ? (bankaRef.bakiye || 0) + grandTotal : (bankaRef.bakiye || 0) - grandTotal;
          const okBankaBakiye = await writeData(`Bankalar/${bankaId}`, {
            ...bankaRef,
            bakiye: yeniBankaBakiye
          });
          if (!okBankaBakiye) {
            Alert.alert('Uyarı', 'Fatura kaydedildi ancak banka bakiyesi eşitlenemedi. (Bağlantı sorunu — bakiye sıraya alındı.)');
          }
        }
      }
    }

    // 5. Update Stock Quantities (READ-FRESH → MODIFY → WRITE pattern to avoid data loss)
    for (const item of items) {
      try {
        const freshStok = await readData(`Stoklar/${item.stokId}`);
        const baseStok = freshStok || stoklar.find(s => s.id === item.stokId) || {};
        const currentMiktar = parseFloat(baseStok.miktar) || 0;

        let yeniMiktar: number;
        if (tur === 'Satış') {
          yeniMiktar = currentMiktar - (parseFloat(item.miktar) || 0);
        } else {
          yeniMiktar = currentMiktar + (parseFloat(item.miktar) || 0);
        }

        // WAC / weighted average cost calculation for Alis (desktop compatible)
        let guncellenecekStok: any = {
          ...baseStok,
          miktar: yeniMiktar,
          id: item.stokId
        };

        if (tur === 'Alış') {
          const eskiToplamDeger = currentMiktar * (parseFloat(baseStok.alisFiyati || baseStok.ortAlisFiyati) || 0);
          const yeniGirisDeger = (parseFloat(item.miktar) || 0) * (parseFloat(item.birimFiyat) || 0);
          const yeniOrtAlisFiyati = yeniMiktar > 0 ? (eskiToplamDeger + yeniGirisDeger) / yeniMiktar : (parseFloat(item.birimFiyat) || 0);
          guncellenecekStok.alisFiyati = parseFloat(item.birimFiyat) || guncellenecekStok.alisFiyati || 0;
          guncellenecekStok.ortAlisFiyati = yeniOrtAlisFiyati;
          guncellenecekStok.satisFiyati = guncellenecekStok.satisFiyati || 0;
        } else {
          guncellenecekStok.satisFiyati = parseFloat(item.birimFiyat) || guncellenecekStok.satisFiyati || 0;
        }

        // Ensure deletion flag is never accidentally dropped
        if (guncellenecekStok.isDeleted === undefined) {
          guncellenecekStok.isDeleted = false;
        }

        const okStokGuncelle = await writeData(`Stoklar/${item.stokId}`, guncellenecekStok);
        if (!okStokGuncelle) {
          Alert.alert('Uyarı', `"${guncellenecekStok.stokAdi || item.stokId}" stok miktarı eşitlenemedi. (Bağlantı sorunu — miktar sıraya alındı.)`);
        }
      } catch (stockErr) {
        console.error(`Stok ${item.stokId} güncellenemedi:`, stockErr);
      }
    }

    setIsFormOpen(false);
    resetForm();
    navigation.setParams({ initialCari: undefined, initialTur: undefined, editFaturaId: undefined });
      if (route?.params?.initialCari || route?.params?.editFaturaId) {
        navigation.goBack();
      }
    Alert.alert('Başarılı', 'Fatura başarıyla kaydedildi.');
    } finally {
      isSavingRef.current = false;
      setIsSaving(false);
    }
  };

  // Fatura etkilerini geri alır (silme + düzenleme). Masaüstü FaturaRepository paritesi.
  const revertFaturaEffects = async (faturaId: number): Promise<boolean> => {
    try {
      const oldRaw = await readData(`Faturalar/${faturaId}`);
      const oldFatura = oldRaw && (oldRaw.id !== undefined || oldRaw.Id !== undefined || oldRaw.faturaNo || oldRaw.FaturaNo) ? oldRaw : (faturalar.find(f => String(f.id) === String(faturaId) || String((f as any).Id) === String(faturaId)) || null);
      if (!oldFatura) return false;

      const faturaIdVal = oldFatura.id ?? oldFatura.Id ?? faturaId;
      const faturaNo = String(oldFatura.faturaNo || oldFatura.FaturaNo || '').trim();
      const oldCariId = oldFatura.cariId ?? oldFatura.CariId;
      const turLower = String(oldFatura.tur || oldFatura.Tur || '').toLowerCase();
      let isSatis = turLower.includes('sat') || turLower.includes('çık') || turLower.includes('cik');
      const isPaid = (oldFatura.odenen || oldFatura.Odenen || 0) > 0 || (oldFatura.odemeSekli && oldFatura.odemeSekli !== 'Açık' && oldFatura.odemeSekli !== 'Acik');
      const genelToplam = parseFloat(oldFatura.genelToplam ?? oldFatura.GenelToplam) || 0;

      const toKeyList = (raw: any) => {
        if (!raw) return [];
        if (Array.isArray(raw)) return raw.map((item, idx) => item ? ({ ...item, firebaseKey: String(item?.id ?? idx) }) : null).filter(Boolean);
        return Object.keys(raw).map((k) => ({ ...raw[k], firebaseKey: k }));
      };

      let detayRaw = await readData(`FaturaDetaylar/${faturaId}`) || [];
      let detaylar = Array.isArray(detayRaw)
        ? detayRaw.filter(Boolean)
        : Object.keys(detayRaw).map(key => ({ ...(detayRaw as any)[key], id: parseInt(key) }));

      const shRaw = await readData('StokHareketler') || {};
      const shList = toKeyList(shRaw);
      const matchSh = shList.filter((h: any) =>
        (h.faturaId !== undefined && (String(h.faturaId) === String(faturaId) || String(h.faturaId) === String(faturaIdVal))) ||
        (h.FaturaId !== undefined && (String(h.FaturaId) === String(faturaId) || String(h.FaturaId) === String(faturaIdVal))) ||
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

      const affectedStokIds = new Set<string>();
      for (const d of detaylar) {
        if (d.stokId) affectedStokIds.add(String(d.stokId));
      }
      for (const m of matchSh) {
        if (m.stokId) affectedStokIds.add(String(m.stokId));
      }

      // Stok hareketlerini sil
      for (const h of matchSh) {
        try { await deleteData(`StokHareketler/${h.firebaseKey || h.id}`); } catch (e) { console.error('StokHareket silme hatası:', e); }
      }

      // Kalan hareketlerden net bakiye teyidi
      const freshAllStoklarRaw = await readData('Stoklar') || {};
      const allStoklarList = toKeyList(freshAllStoklarRaw);
      const remainingShRaw = await readData('StokHareketler') || {};
      const remainingShList = toKeyList(remainingShRaw);

      for (const sid of affectedStokIds) {
        try {
          const stok = allStoklarList.find((s: any) => String(s.id) === sid || s.firebaseKey === sid) || stoklar.find(s => String(s.id) === sid);
          if (stok) {
            const stokTargetKey = stok.firebaseKey || sid;
            const mySh = remainingShList.filter((h: any) => String(h.stokId) === sid);
            if (mySh.length > 0) {
              const netMiktar = mySh.reduce((acc: number, h: any) => {
                const g = parseFloat(h.giren) || 0;
                const c = parseFloat(h.cikan) || 0;
                return acc + (g - c);
              }, 0);
              await writeData(`Stoklar/${stokTargetKey}`, { ...stok, miktar: netMiktar, id: stok.id ?? (isNaN(Number(sid)) ? sid : parseInt(sid)) });
            } else {
              const relatedD = detaylar.filter((d: any) => String(d.stokId) === sid);
              const totalMiktar = relatedD.reduce((acc: number, d: any) => acc + (parseFloat(d.miktar) || 0), 0);
              const currentMiktar = parseFloat(stok.miktar) || 0;
              const yeniMiktar = isSatis ? (currentMiktar + totalMiktar) : (currentMiktar - totalMiktar);
              await writeData(`Stoklar/${stokTargetKey}`, { ...stok, miktar: yeniMiktar, id: stok.id ?? (isNaN(Number(sid)) ? sid : parseInt(sid)) });
            }
          }
        } catch (e) {
          console.error(`Stok ${sid} bakiye geri alınamadı:`, e);
        }
      }

      if (oldCariId !== undefined && oldCariId !== null) {
        const freshCari = await readData(`Cariler/${oldCariId}`);
        const cariRef = freshCari || cariler.find(c => String(c.id) === String(oldCariId));
        if (cariRef) {
          const updatedCari = { ...cariRef };
          if (isPaid) {
            updatedCari.borc = Math.max(0, (parseFloat(updatedCari.borc ?? updatedCari.Borc) || 0) - genelToplam);
            updatedCari.alacak = Math.max(0, (parseFloat(updatedCari.alacak ?? updatedCari.Alacak) || 0) - genelToplam);
          } else if (isSatis) {
            updatedCari.borc = Math.max(0, (parseFloat(updatedCari.borc ?? updatedCari.Borc) || 0) - genelToplam);
          } else {
            updatedCari.alacak = Math.max(0, (parseFloat(updatedCari.alacak ?? updatedCari.Alacak) || 0) - genelToplam);
          }
                    updatedCari.bakiye = (parseFloat(updatedCari.borc ?? updatedCari.Borc) || 0) - (parseFloat(updatedCari.alacak ?? updatedCari.Alacak) || 0);
          updatedCari.updatedAt = new Date().toISOString();
          updatedCari.version = (cariRef.version || 0) + 1;
          const okCariRevert = await writeData(`Cariler/${oldCariId}`, updatedCari);
          if (!okCariRevert) {
            Alert.alert('Uyarı', 'Cari bakiye geri alınamadı. (Bağlantı sorunu — bakiye sıraya alındı.)');
          }
        }
      }

      const chRaw = await readData('CariHareketler') || {};
      const chList = toKeyList(chRaw);
      for (const h of chList) {
        const hEvrak = String(h.evrakNo || h.EvrakNo || '').trim().toLowerCase();
        const hFId = h.faturaId !== undefined ? h.faturaId : h.FaturaId;
        const match =
          (hFId !== undefined && (String(hFId) === String(faturaId) || String(hFId) === String(faturaIdVal))) ||
          (faturaNo && (hEvrak === faturaNo.toLowerCase() || hEvrak === `kpl-${faturaNo.toLowerCase()}`));
        if (match) {
          const k = h.firebaseKey || h.id;
          if (k) {
            try { await deleteData(`CariHareketler/${k}`); } catch (e) { console.error('CariHareket silme hatası:', e); }
          }
        }
      }

      const khRaw = await readData('KasaHareketler') || {};
      const khList = Array.isArray(khRaw) ? khRaw.filter(Boolean) : Object.keys(khRaw).map(key => ({ ...(khRaw as any)[key], firebaseKey: key }));
      for (const kh of khList) {
        const khAcik = kh.aciklama || kh.Aciklama || '';
        if (faturaNo && khAcik.includes(faturaNo)) {
          try {
            const kasaIdNum = kh.hesapId || kh.kasaId || kh.KasaId;
            const kasa = kasalar.find(k => k.id === kasaIdNum);
            const giren = kh.tur === 'Giriş' ? (kh.tutar || 0) : (kh.giren || 0);
            const cikan = kh.tur === 'Çıkış' ? (kh.tutar || 0) : (kh.cikan || 0);
            if (kasa) {
              const yeniBakiye = (kasa.bakiye || 0) - giren + cikan;
              const okKasaRevert = await writeData(`Bankalar/${kasa.id}`, { ...kasa, kartTuru: 'Kasa', bakiye: yeniBakiye });
              if (!okKasaRevert) {
                Alert.alert('Uyarı', 'Kasa bakiyesi geri alınamadı. (Bağlantı sorunu — bakiye sıraya alındı.)');
              }
            }
            await deleteData(`KasaHareketler/${kh.firebaseKey || kh.id}`);
          } catch (e) { console.error('Kasa hareket silme hatası:', e); }
        }
      }

      const bhRaw = await readData('BankaHareketler') || {};
      const bhList = Array.isArray(bhRaw) ? bhRaw.filter(Boolean) : Object.keys(bhRaw).map(key => ({ ...(bhRaw as any)[key], firebaseKey: key }));
      for (const bh of bhList) {
        const bhAcik = bh.aciklama || bh.Aciklama || '';
        if (faturaNo && bhAcik.includes(faturaNo)) {
          try {
            const bankaIdNum = bh.bankaId || bh.hesapId || bh.BankaId;
            const banka = bankalar.find(b => b.id === bankaIdNum);
            const giren = bh.giren || bh.borc || 0;
            const cikan = bh.cikan || bh.alacak || 0;
            if (banka) {
              const yeniBakiye = (banka.bakiye || 0) - giren + cikan;
              const okBankaRevert = await writeData(`Bankalar/${banka.id}`, { ...banka, bakiye: yeniBakiye });
              if (!okBankaRevert) {
                Alert.alert('Uyarı', 'Banka bakiyesi geri alınamadı. (Bağlantı sorunu — bakiye sıraya alındı.)');
              }
            }
            await deleteData(`BankaHareketler/${bh.firebaseKey || bh.id}`);
          } catch (e) { console.error('Banka hareket silme hatası:', e); }
        }
      }

      try { await deleteData(`FaturaDetaylar/${faturaId}`); } catch {}

      return true;
    } catch (err) {
      console.error('Fatura etkileri geri alınamadı:', err);
      return false;
    }
  };

  const handleDeleteFatura = async (fatura: any) => {
    Alert.alert(
      'Silme Onayı',
      `${fatura.faturaNo} numaralı faturayı silmek istediğinize emin misiniz? Stok, cari ve kasa/banka hareketleri geri alınacaktır.`,
      [
        { text: 'Vazgeç', style: 'cancel' },
        {
          text: 'Sil',
          style: 'destructive',
          onPress: async () => {
            try {
              const okCascade = await deleteFaturaCascade(fatura.id || fatura.faturaNo);
              if (!okCascade) {
                await revertFaturaEffects(fatura.id);
                const okSil = await writeData(`Faturalar/${fatura.id}`, { ...fatura, isDeleted: true });
                if (!okSil) {
                  Alert.alert('Hata', 'Fatura silinemedi. (Bağlantı sorunu — kayıt eşitlenemedi.)');
                  return;
                }
              }
              Alert.alert('Başarılı', 'Fatura ve ilişkili hareketler başarıyla silindi.');
            } catch (err) {
              console.error('Fatura silme hatası:', err);
              Alert.alert('Hata', 'Fatura silinirken bir hata oluştu.');
            }
          }
        }
      ]
    );
  };

  const formatFaturaTarih = (tarihStr: string) => {
    try {
      const d = new Date(tarihStr);
      const day = String(d.getDate()).padStart(2, '0');
      const month = String(d.getMonth() + 1).padStart(2, '0');
      const year = d.getFullYear();
      return `${day}.${month}.${year}`;
    } catch {
      return tarihStr;
    }
  };

  const renderItem = ({ item, index }: { item: any; index: number }) => {
    const isSatis = item.tur === 'Satış' || item.tur === 'Satis';

    return (
      <AppleListRow
        icon={<FileText color={isSatis ? AppleTheme.colors.success : AppleTheme.colors.warning} size={20} />}
        title={item.faturaNo || 'Fatura'}
        subtitle={`${item.cariUnvan || 'Cari Yok'} • ${formatFaturaTarih(item.tarih)}`}
        tag={item.tur || 'Satış'}
        tagColor={isSatis ? AppleTheme.colors.success : AppleTheme.colors.warning}
        value={`${formatMoney(item.genelToplam)} ₺`}
        valueColor={isSatis ? AppleTheme.colors.success : AppleTheme.colors.textPrimary}
        isFirst={index === 0}
        isLast={index === filteredFaturalar.length - 1}
        onPress={async () => {
          setSelectedFatura(item);
          setIsDetailOpen(true);
          setLoadingDetay(true);
          try {
            const detaylar = await readData(`FaturaDetaylar/${item.id}`) || [];
            const listDetaylar = Array.isArray(detaylar) 
              ? detaylar.filter(Boolean) 
              : Object.keys(detaylar).map(key => ({ ...(detaylar as any)[key], id: parseInt(key) }));
            setFaturaItems(listDetaylar);
          } catch (err) {
            console.error('Fatura detaylari yuklenemedi:', err);
          } finally {
            setLoadingDetay(false);
          }
        }}
      />
    );
  };

  const handleSendInvoiceMail = async (fatura: any) => {
    const success = await sendInvoiceMail({
      recipientEmail: fatura?.eposta || 'bilgi@musteri.com',
      subject: `E-Fatura: ${fatura?.faturaNo || 'FAT-001'} - Ermay Muhasebe`,
      body: `Sayın ${fatura?.cariUnvan || 'Müşterimiz'},\n\n${fatura?.faturaNo || 'FAT-001'} numaralı faturanız ekte yer almaktadır.`,
    });
    if (success) {
      Alert.alert('E-Posta Gönderildi', 'Fatura iOS Mail servisi ile başarıyla e-posta olarak gönderildi.');
    } else {
      Alert.alert('E-Posta Taslağı Açıldı', 'iOS Mail uygulaması fatura taslağı ile açıldı.');
    }
  };

  return (
    <SafeAreaView style={styles.container}>
      <OfflineNetworkBar />
      {/* Header (DESKTOP 1:1 TASARIM) */}
      <View style={styles.header}>
        <View style={styles.headerTopRow}>
          <Text style={styles.headerTitle}>Faturalar</Text>
        </View>

        {/* Search + Date Filter Row */}
        <View style={styles.searchDateRow}>
          <View style={styles.searchBox}>
            <Search color="#64748B" size={18} />
            <TextInput 
              style={styles.searchInput}
              placeholder="Fatura / Cari Ara..."
              placeholderTextColor="#64748B"
              value={searchQuery}
              onChangeText={setSearchQuery}
            />
          </View>

          <View style={styles.dateFilterRow}>
            <View style={styles.dateInputBox}>
              <TextInput 
                style={styles.dateInput}
                placeholder="01.08.2026"
                placeholderTextColor="rgba(255,255,255,0.4)"
                value={startDateStr}
                onChangeText={setStartDateStr}
              />
            </View>
            <Text style={styles.dateSeparator}>_</Text>
            <View style={styles.dateInputBox}>
              <TextInput 
                style={styles.dateInput}
                placeholder="08.08.2026"
                placeholderTextColor="rgba(255,255,255,0.4)"
                value={endDateStr}
                onChangeText={setEndDateStr}
              />
            </View>
            <TouchableOpacity style={styles.refreshBtn} onPress={handleRefresh}>
              <RefreshCw color="rgba(255,255,255,0.6)" size={16} />
            </TouchableOpacity>
          </View>
        </View>

        {/* Action Buttons Row */}
        <View style={styles.actionButtonsRow}>
          <TouchableOpacity style={[styles.actionBtn, styles.btnSatis]} onPress={() => navigation.navigate('FaturaForm', { initialTur: 'Satış' })}>
            <Text style={styles.actionBtnPlus}>+</Text>
            <Text style={styles.actionBtnText}>Yeni Satış</Text>
          </TouchableOpacity>
          <TouchableOpacity style={[styles.actionBtn, styles.btnAlis]} onPress={() => navigation.navigate('FaturaForm', { initialTur: 'Alış' })}>
            <Text style={styles.actionBtnPlus}>+</Text>
            <Text style={styles.actionBtnText}>Yeni Alış</Text>
          </TouchableOpacity>
          <View style={{ flex: 1 }} />
          <TouchableOpacity style={[styles.actionBtn, styles.btnExcel]} onPress={handleExportToExcel}>
            <Grid3X3 color="#FFF" size={14} />
            <Text style={[styles.actionBtnText, { marginLeft: 4 }]}>Excel</Text>
          </TouchableOpacity>
          <TouchableOpacity style={[styles.actionBtn, styles.btnPdf]} onPress={handleExportAllToPdf}>
            <FileDown color="#FFF" size={14} />
            <Text style={[styles.actionBtnText, { marginLeft: 4 }]}>PDF</Text>
          </TouchableOpacity>
          <TouchableOpacity style={[styles.actionBtn, { backgroundColor: '#8B5CF6' }]} onPress={() => handleSendInvoiceMail(selectedFatura || faturalar[0])}>
            <MailIcon color="#FFF" size={14} />
            <Text style={[styles.actionBtnText, { marginLeft: 4 }]}>iOS Mail</Text>
          </TouchableOpacity>
        </View>
      </View>

      {/* Fatura Listesi */}
      {loading ? (
        <ShimmerCardList count={6} />
      ) : (
        <View style={styles.appleListWrapper}>
          <FlatList 
            data={filteredFaturalar}
            keyExtractor={(item, index) => String(item.firebaseKey || item.id || index)}
            renderItem={renderItem}
            contentContainerStyle={styles.listContent}
            removeClippedSubviews={false}
            initialNumToRender={15}
            maxToRenderPerBatch={15}
            windowSize={7}
            ListEmptyComponent={
              <View style={styles.emptyContainer}>
                <FileText color="rgba(255,255,255,0.1)" size={64} style={{ alignSelf: 'center', marginBottom: 16 }} />
                <Text style={styles.emptyText}>Herhangi bir fatura bulunamadı.</Text>
              </View>
            }
          />
        </View>
      )}





      {/* Fatura Detay Modalı */}
      <Modal visible={isDetailOpen} animationType="fade" transparent={true}>
        <SafeAreaView style={styles.modalOverlay}>
          <View style={styles.modalContent}>
            {selectedFatura && (
              <>
                <View style={styles.modalHeader}>
                  <Text style={styles.modalTitle}>Fatura Detayı</Text>
                  <TouchableOpacity onPress={() => setIsDetailOpen(false)} style={styles.closeButton}>
                    <X color="#FFF" size={24} />
                  </TouchableOpacity>
                </View>

                <ScrollView contentContainerStyle={styles.formScroll}>
                  <View style={styles.detailCard}>
                    <FileText color={selectedFatura.tur === 'Satış' ? '#3B82F6' : '#F97316'} size={64} style={{ alignSelf: 'center', marginBottom: 12 }} />
                    <Text style={styles.detailUnvan}>{selectedFatura.cariUnvan}</Text>
                    <Text style={styles.detailGrup}>{selectedFatura.tur} Faturası • {selectedFatura.faturaNo}</Text>

                    <View style={styles.detailBakiyeBox}>
                      <View style={{ alignItems: 'center' }}>
                        <Text style={styles.detailBakiyeLabel}>Tarih</Text>
                        <Text style={[styles.detailBakiyeVal, { color: '#FFF' }]}>{formatFaturaTarih(selectedFatura.tarih)}</Text>
                      </View>
                      <View style={{ width: 1, backgroundColor: 'rgba(255,255,255,0.1)' }} />
                      <View style={{ alignItems: 'center' }}>
                        <Text style={styles.detailBakiyeLabel}>Vade</Text>
                        <Text style={[styles.detailBakiyeVal, { color: '#FFF' }]}>{formatFaturaTarih(selectedFatura.vadeTarihi || selectedFatura.tarih)}</Text>
                      </View>
                      <View style={{ width: 1, backgroundColor: 'rgba(255,255,255,0.1)' }} />
                      <View style={{ alignItems: 'center' }}>
                        <Text style={styles.detailBakiyeLabel}>Tür</Text>
                        <View style={[styles.turPillMini, selectedFatura.tur === 'Satış' ? styles.turSatis : styles.turAlis]}>
                          <Text style={styles.turPillTextMini}>{selectedFatura.tur}</Text>
                        </View>
                      </View>
                    </View>
                  </View>

                  {/* Kalem Listesi */}
                  <View style={styles.detailSection}>
                    <Text style={styles.detailSectionTitle}>Fatura Kalemleri</Text>
                    {loadingDetay ? (
                      <ActivityIndicator size="small" color="#0061FF" style={{ padding: 12 }} />
                    ) : faturaItems.length === 0 ? (
                      <Text style={styles.emptyText}>Fatura kalemi bulunmuyor.</Text>
                    ) : faturaItems.map((item: any, idx: number) => (
                      <View key={idx} style={styles.detailItemRow}>
                        <View style={{ flex: 1 }}>
                          <Text style={styles.detailItemName}>{item.stokAdi}</Text>
                          <Text style={styles.detailItemQty}>
                            {item.miktar} {item.birim || 'Adet'} x {formatMoney(item.birimFiyat || item.fiyat || 0)} ₺ (KDV %{item.kdvOrani || 20})
                          </Text>
                        </View>
                        <Text style={styles.detailItemTotal}>
                          {formatMoney((item.miktar || 0) * (item.birimFiyat || item.fiyat || 0))} ₺
                        </Text>
                      </View>
                    ))}
                  </View>

                  <View style={styles.detailSection}>
                    <Text style={styles.detailSectionTitle}>Finansal Özet & Detaylar</Text>
                    <View style={styles.detailInfoRow}>
                      <Text style={styles.detailInfoLabel}>Ödeme Şekli / Durum:</Text>
                      <Text style={styles.detailInfoValue}>{selectedFatura.odemeSekli || 'Açık Hesap'} / {selectedFatura.durum || 'Açık'}</Text>
                    </View>
                    {selectedFatura.odemeSekli === 'Nakit' && (
                      <View style={styles.detailInfoRow}>
                        <Text style={styles.detailInfoLabel}>Ödenen Kasa:</Text>
                        <Text style={styles.detailInfoValue}>
                          {kasalar.find(k => k.id === selectedFatura.kasaId)?.hesapAdi || kasalar.find(k => k.id === selectedFatura.kasaId)?.bankaAdi || kasalar.find(k => k.id === selectedFatura.kasaId)?.ad || kasalar.find(k => k.id === selectedFatura.kasaId)?.isim || 'Nakit Kasa'}
                        </Text>
                      </View>
                    )}
                    {selectedFatura.odemeSekli === 'Kredi Kartı' && (
                      <View style={styles.detailInfoRow}>
                        <Text style={styles.detailInfoLabel}>Ödenen Banka:</Text>
                        <Text style={styles.detailInfoValue}>
                          {bankalar.find(b => b.id === selectedFatura.bankaId)?.hesapAdi || 'Banka Hesabı'}
                        </Text>
                      </View>
                    )}
                    <View style={styles.detailInfoRow}>
                      <Text style={styles.detailInfoLabel}>Döviz / Kur:</Text>
                      <Text style={styles.detailInfoValue}>{selectedFatura.dovizTuru || 'TL'} / {selectedFatura.dovizKuru || '1.00'}</Text>
                    </View>
                    {selectedFatura.baglantiEvrakNo ? (
                      <View style={styles.detailInfoRow}>
                        <Text style={styles.detailInfoLabel}>Bağlantı Evrak No:</Text>
                        <Text style={styles.detailInfoValue}>{selectedFatura.baglantiEvrakNo}</Text>
                      </View>
                    ) : null}
                    <View style={styles.detailInfoRow}>
                      <Text style={styles.detailInfoLabel}>Ara Toplam:</Text>
                      <Text style={styles.detailInfoValue}>{formatMoney(selectedFatura.araToplam || 0)} ₺</Text>
                    </View>
                    <View style={styles.detailInfoRow}>
                      <Text style={styles.detailInfoLabel}>KDV Toplamı:</Text>
                      <Text style={styles.detailInfoValue}>{formatMoney(selectedFatura.kdvToplam || 0)} ₺</Text>
                    </View>
                    <View style={[styles.detailInfoRow, { borderTopWidth: 1, borderTopColor: 'rgba(255,255,255,0.05)', paddingTop: 10, marginTop: 4 }]}>
                      <Text style={[styles.detailInfoLabel, { fontWeight: 'bold', color: '#FFF' }]}>Genel Toplam:</Text>
                      <Text style={[styles.detailInfoValue, { color: '#60A5FA', fontWeight: 'bold', fontSize: 15 }]}>{formatMoney(selectedFatura.genelToplam || 0)} ₺</Text>
                    </View>
                  </View>

                  {selectedFatura.aciklama && (
                    <View style={styles.detailSection}>
                      <Text style={styles.detailSectionTitle}>Açıklama</Text>
                      <Text style={{ color: '#94A3B8', fontSize: 13 }}>{selectedFatura.aciklama}</Text>
                    </View>
                  )}

                  {/* Action Buttons */}
                  <View style={{ flexDirection: 'row', gap: 10, marginTop: 10, marginBottom: 20 }}>
                    <TouchableOpacity 
                      style={[styles.actionBtn, { flex: 1, backgroundColor: '#3B82F6' }]} 
                      onPress={() => { handleShareFatura(selectedFatura); }}
                    >
                      <FileText color="#FFF" size={18} />
                      <Text style={[styles.actionBtnText, { marginLeft: 6 }]}>PDF Paylaş</Text>
                    </TouchableOpacity>
                    <TouchableOpacity 
                      style={[styles.actionBtn, { flex: 1, backgroundColor: '#10B981' }]} 
                      onPress={() => {
                        setIsDetailOpen(false);
                        navigation.navigate('FaturaForm', { editFaturaId: selectedFatura.id });
                      }}
                    >
                      <Edit3 color="#FFF" size={18} />
                      <Text style={[styles.actionBtnText, { marginLeft: 6 }]}>Düzenle</Text>
                    </TouchableOpacity>
                    <TouchableOpacity 
                      style={[styles.actionBtn, { backgroundColor: '#EF4444', paddingHorizontal: 18 }]} 
                      onPress={() => {
                        const ftr = selectedFatura;
                        setIsDetailOpen(false);
                        setTimeout(() => handleDeleteFatura(ftr), 300);
                      }}
                    >
                      <Trash2 color="#FFF" size={18} />
                    </TouchableOpacity>
                  </View>
                </ScrollView>
              </>
            )}
          </View>
        </SafeAreaView>
      </Modal>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0A0A0A',
  },
  center: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  // ===== HEADER - DESKTOP 1:1 =====
  header: {
    padding: 20,
    paddingTop: 40,
    paddingBottom: 14,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.08)',
  },
  headerTopRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 12,
  },
  headerTitle: {
    fontSize: 18,
    fontWeight: '800',
    color: '#FFFFFF',
    marginRight: 15,
  },
  searchDateRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    marginBottom: 12,
  },
  searchBox: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#2A2A2A',
    borderWidth: 1,
    borderColor: '#444',
    borderRadius: 999,
    paddingHorizontal: 14,
    flex: 1,
  },
  searchInput: {
    flex: 1,
    paddingVertical: 10,
    paddingHorizontal: 8,
    color: '#FFFFFF',
    fontSize: 13,
    fontWeight: '500',
  },
  dateFilterRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
  },
  dateInputBox: {
    backgroundColor: '#2A2A2A',
    borderWidth: 1,
    borderColor: '#444',
    borderRadius: 999,
    paddingHorizontal: 10,
    paddingVertical: 5,
  },
  dateInput: {
    backgroundColor: 'transparent',
    color: 'white',
    width: 72,
    textAlign: 'center',
    outlineStyle: 'none' as any,
    fontSize: 11,
    fontWeight: '500',
  },
  dateSeparator: {
    color: 'rgba(255,255,255,0.4)',
    fontSize: 14,
    paddingHorizontal: 2,
  },
  refreshBtn: {
    backgroundColor: 'rgba(0,0,0,0.3)',
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.1)',
    borderRadius: 999,
    width: 32,
    height: 32,
    alignItems: 'center',
    justifyContent: 'center',
    marginLeft: 2,
  },
  actionButtonsRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  actionBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 9,
    paddingHorizontal: 14,
    borderRadius: 999,
  },
  actionBtnPlus: {
    fontSize: 16,
    color: '#FFF',
    fontWeight: '600',
    marginRight: 3,
  },
  actionBtnText: {
    color: '#FFF',
    fontSize: 11,
    fontWeight: '700',
  },
  btnSatis: {
    backgroundColor: '#10B981',
  },
  btnAlis: {
    backgroundColor: '#D97706',
  },
  btnExcel: {
    backgroundColor: '#10B981',
  },
  btnPdf: {
    backgroundColor: '#EF4444',
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
  faturaCard: {
    backgroundColor: '#0F0F0F',
    borderRadius: 14,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    padding: 14,
    marginBottom: 10,
  },
  cardTopRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 8,
  },
  faturaNo: {
    color: '#9CA3AF',
    fontSize: 12,
    fontWeight: '500',
  },
  faturaTarih: {
    color: '#FFFFFF',
    fontSize: 13,
    fontWeight: '500',
  },
  cardMiddleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 10,
  },
  faturaCari: {
    color: '#FFFFFF',
    fontSize: 14,
    fontWeight: '500',
    flex: 1,
  },
  appleFaturaCard: {
    marginBottom: 12,
  },
  appleFaturaNo: {
    color: '#0A84FF',
    fontSize: 14,
    fontWeight: '700',
    letterSpacing: -0.2,
  },
  appleFaturaCari: {
    color: '#FFFFFF',
    fontSize: 15,
    fontWeight: '600',
    flex: 1,
    letterSpacing: -0.2,
  },
  appleTurPill: {
    paddingHorizontal: 12,
    paddingVertical: 4,
    borderRadius: 999,
    alignItems: 'center',
    justifyContent: 'center',
  },
  appleTurSatis: {
    backgroundColor: 'rgba(10, 132, 255, 0.2)',
    borderWidth: 1,
    borderColor: 'rgba(10, 132, 255, 0.4)',
  },
  appleTurAlis: {
    backgroundColor: 'rgba(255, 159, 10, 0.2)',
    borderWidth: 1,
    borderColor: 'rgba(255, 159, 10, 0.4)',
  },
  appleTurPillText: {
    color: '#FFF',
    fontSize: 11,
    fontWeight: '700',
  },
  appleFaturaTutar: {
    color: '#FFFFFF',
    fontSize: 17,
    fontWeight: '800',
    letterSpacing: -0.3,
  },
  turPill: {
    paddingHorizontal: 14,
    paddingVertical: 4,
    borderRadius: 999,
    alignItems: 'center',
    justifyContent: 'center',
    minWidth: 58,
  },
  turPillMini: {
    paddingHorizontal: 10,
    paddingVertical: 2,
    borderRadius: 999,
    alignItems: 'center',
    justifyContent: 'center',
    minWidth: 44,
    marginTop: 4,
  },
  turSatis: {
    backgroundColor: '#3B82F6',
  },
  turAlis: {
    backgroundColor: '#F97316',
  },
  turPillText: {
    color: '#FFF',
    fontSize: 11,
    fontWeight: '700',
  },
  turPillTextMini: {
    color: '#FFF',
    fontSize: 9,
    fontWeight: '700',
  },
  cardBottomRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingTop: 8,
    borderTopWidth: 1,
    borderTopColor: 'rgba(255,255,255,0.03)',
  },
  faturaTutarLabel: {
    color: '#64748B',
    fontSize: 11,
    fontWeight: '500',
  },
  faturaTutar: {
    color: '#60A5FA',
    fontSize: 15,
    fontWeight: '800',
  },
  faturaTutarLira: {
    color: '#60A5FA',
    fontSize: 12,
    opacity: 0.7,
    marginLeft: 3,
    fontWeight: '700',
  },
  emptyContainer: {
    paddingVertical: 60,
    alignItems: 'center',
  },
  emptyText: {
    color: 'rgba(255,255,255,0.2)',
    fontSize: 13,
    textAlign: 'center',
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
    marginBottom: 16,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.08)',
    paddingBottom: 16,
  },
  modalTitle: {
    color: '#FFF',
    fontSize: 18,
    fontWeight: 'bold',
  },
  closeBtn: {
    backgroundColor: 'rgba(255,255,255,0.05)',
    borderRadius: 999,
    padding: 6,
  },
  formScroll: {
    flexGrow: 1,
    paddingBottom: 60,
  },
  formRow: {
    flexDirection: 'row',
    gap: 12,
    marginBottom: 14,
  },
  formGroup: {
    marginBottom: 14,
  },
  formLabel: {
    color: '#94A3B8',
    fontSize: 12,
    fontWeight: 'bold',
    marginBottom: 6,
  },
  closeButton: {
    backgroundColor: 'rgba(255,255,255,0.05)',
    borderRadius: 999,
    padding: 6,
  },
  inputGroup: {
    marginBottom: 14,
  },
  inputLabel: {
    color: '#94A3B8',
    fontSize: 11,
    fontWeight: 'bold',
    marginBottom: 6,
    textTransform: 'uppercase',
  },
  toggleRow: {
    flexDirection: 'row',
    backgroundColor: '#2A2A2A',
    padding: 4,
    borderRadius: 12,
  },
  toggleBtn: {
    flex: 1,
    paddingVertical: 10,
    alignItems: 'center',
    borderRadius: 8,
  },
  toggleBtnActive: {
    backgroundColor: '#0061FF',
  },
  toggleBtnText: {
    color: '#64748B',
    fontSize: 11,
    fontWeight: 'bold',
  },
  toggleBtnTextActive: {
    color: '#FFF',
  },
  kdvChip: {
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: 8,
    backgroundColor: '#2A2A2A',
    marginRight: 6,
  },
  kdvChipActive: {
    backgroundColor: '#0061FF',
  },
  kdvChipText: {
    color: '#94A3B8',
    fontSize: 12,
    fontWeight: 'bold',
  },
  kdvChipTextActive: {
    color: '#FFF',
  },
  grupChip: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 8,
    backgroundColor: '#2A2A2A',
    marginRight: 8,
    marginBottom: 8,
  },
  grupChipActive: {
    backgroundColor: '#0061FF',
  },
  grupChipText: {
    color: '#94A3B8',
    fontSize: 12,
    fontWeight: '600',
  },
  grupChipTextActive: {
    color: '#FFF',
  },
  selectorCard: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#2A2A2A',
    borderColor: '#444',
    borderWidth: 1,
    borderRadius: 16,
    padding: 16,
    marginBottom: 14,
  },
  selectorBtn: {
    backgroundColor: '#2A2A2A',
    borderColor: '#444',
    borderWidth: 1,
    borderRadius: 12,
    padding: 12,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  selectorText: {
    color: '#FFF',
    fontSize: 13,
    fontWeight: 'bold',
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
  itemsSection: {
    marginBottom: 14,
    backgroundColor: '#161616',
    borderRadius: 16,
    padding: 12,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.08)',
  },
  itemsTitle: {
    color: '#FFF',
    fontSize: 13,
    fontWeight: 'bold',
  },
  addItemBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
  },
  addItemBtnText: {
    color: '#00FF87',
    fontSize: 12,
    fontWeight: 'bold',
  },
  emptyItemsText: {
    color: '#64748B',
    fontSize: 12,
    textAlign: 'center',
    paddingVertical: 12,
  },
  itemRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingVertical: 10,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255,255,255,0.05)',
  },
  itemRowTitle: {
    color: '#FFF',
    fontSize: 13,
    fontWeight: 'bold',
  },
  itemInput: {
    backgroundColor: '#2A2A2A',
    borderRadius: 8,
    color: '#FFF',
    paddingHorizontal: 8,
    paddingVertical: 4,
    fontSize: 12,
    textAlign: 'center',
  },
  itemUnit: {
    color: '#94A3B8',
    fontSize: 12,
    marginHorizontal: 4,
  },
  summaryCard: {
    backgroundColor: '#161616',
    borderRadius: 16,
    padding: 16,
    marginBottom: 14,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.08)',
  },
  summaryRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 6,
  },
  summaryLabel: {
    color: '#94A3B8',
    fontSize: 12,
  },
  summaryVal: {
    color: '#FFF',
    fontSize: 12,
    fontWeight: 'bold',
  },
  saveButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#10B981',
    padding: 16,
    borderRadius: 12,
  },
  saveButtonText: {
    color: '#FFF',
    fontWeight: 'bold',
    fontSize: 14,
    marginLeft: 8,
  },
  selectorItem: {
    borderColor: 'rgba(255,255,255,0.05)',
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
  overlayHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 16,
  },
  overlayTitle: {
    color: '#FFF',
    fontSize: 20,
    fontWeight: 'bold',
    flex: 1,
    marginLeft: 12,
  },
  overlayCloseBtn: {
    backgroundColor: 'rgba(255,255,255,0.05)',
    borderRadius: 12,
    padding: 8,
  },
  overlaySearchBox: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#2A2A2A',
    borderRadius: 14,
    paddingHorizontal: 12,
    marginBottom: 16,
    borderWidth: 1,
    borderColor: '#444',
  },
  overlaySearchInput: {
    flex: 1,
    paddingVertical: 14,
    marginLeft: 8,
    color: '#FFF',
    fontSize: 14,
  },
  overlayFlatlist: {
    flex: 1,
  },
  overlayItemCard: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#161616',
    borderRadius: 14,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.08)',
    padding: 12,
    marginBottom: 8,
  },
  overlayItemIconBox: {
    width: 42,
    height: 42,
    borderRadius: 12,
    backgroundColor: '#0061FF15',
    alignItems: 'center',
    justifyContent: 'center',
  },
  overlayItemTitle: {
    color: '#FFF',
    fontSize: 13,
    fontWeight: 'bold',
    flex: 1,
    marginRight: 8,
  },
  overlayItemPill: {
    borderRadius: 8,
    borderWidth: 1,
    paddingHorizontal: 8,
    paddingVertical: 3,
    marginLeft: 8,
  },
  overlayItemPillText: {
    fontSize: 10,
    fontWeight: 'bold',
  },
  overlayItemCode: {
    color: '#64748B',
    fontSize: 11,
    fontWeight: '600',
  },
  overlayItemGroup: {
    color: '#94A3B8',
    fontSize: 11,
  },
  overlayItemPriceLabel: {
    color: '#64748B',
    fontSize: 10,
  },
  overlayItemPriceValue: {
    color: '#FFF',
    fontSize: 14,
    fontWeight: 'bold',
    marginTop: 2,
  },
  overlayItemKdv: {
    color: '#F59E0B',
    fontSize: 11,
    fontWeight: '600',
  },
  detailCard: {
    backgroundColor: '#161616',
    borderRadius: 24,
    borderColor: 'rgba(255,255,255,0.08)',
    borderWidth: 1,
    padding: 24,
    marginBottom: 20,
  },
  detailUnvan: {
    color: '#FFF',
    fontSize: 18,
    fontWeight: 'bold',
    textAlign: 'center',
  },
  detailGrup: {
    color: '#94A3B8',
    fontSize: 12,
    textAlign: 'center',
    marginTop: 4,
  },
  detailBakiyeBox: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    marginTop: 20,
    backgroundColor: 'rgba(255,255,255,0.03)',
    borderRadius: 16,
    padding: 12,
  },
  detailBakiyeLabel: {
    color: '#64748B',
    fontSize: 11,
  },
  detailBakiyeVal: {
    fontSize: 13,
    fontWeight: 'bold',
    marginTop: 2,
  },
  detailSection: {
    backgroundColor: 'rgba(255,255,255,0.01)',
    borderRadius: 20,
    borderColor: 'rgba(255,255,255,0.05)',
    borderWidth: 1,
    padding: 16,
    marginBottom: 16,
  },
  detailSectionTitle: {
    color: '#FFF',
    fontSize: 13,
    fontWeight: 'bold',
    marginBottom: 12,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255,255,255,0.05)',
    paddingBottom: 8,
  },
  detailItemRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: 8,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255,255,255,0.03)',
  },
  detailItemName: {
    color: '#FFF',
    fontSize: 12,
    fontWeight: 'bold',
  },
  detailItemQty: {
    color: '#64748B',
    fontSize: 11,
    marginTop: 2,
  },
  detailItemTotal: {
    color: '#FFF',
    fontSize: 12,
    fontWeight: 'bold',
  },
  detailInfoRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 8,
  },
  detailInfoLabel: {
    color: '#94A3B8',
    fontSize: 12,
  },
  detailInfoValue: {
    color: '#FFF',
    fontSize: 12,
  },
  miniSelectCard: {
    backgroundColor: '#2A2A2A',
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderRadius: 8,
    marginRight: 8,
    borderWidth: 1,
    borderColor: '#444',
  },
  miniSelectCardActive: {
    backgroundColor: '#0061FF',
    borderColor: '#0061FF',
  },
  miniSelectText: {
    color: '#94A3B8',
    fontSize: 12,
    fontWeight: '600',
  },
  miniSelectTextActive: {
    color: '#FFF',
  },
  switchRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  }
});

