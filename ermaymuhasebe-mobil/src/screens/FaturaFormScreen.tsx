import React, { useState, useEffect, useRef } from 'react';
import { StyleSheet, Text, View, SafeAreaView, FlatList, TextInput, ActivityIndicator, TouchableOpacity, Modal, ScrollView, Alert, Share, Switch, KeyboardAvoidingView, Platform } from 'react-native';
import { Search, FileText, Plus, X, Save, Edit3, Trash2, Calendar, User, ShoppingBag, Share2, RefreshCw, Grid3X3, FileDown, Package, ChevronDown } from 'lucide-react-native';
import { subscribeToPath, writeData, readData, deleteData, mapAppToDatabase, splitAccounts, mergeKasalar, updateDataBatch } from '../services/firebase';
import { generateReportPdf } from '../services/pdfService';
import { exportToExcel } from '../services/excelService';
import { generateInt32Id } from '../utils/IdGenerator';
import { BlurView } from 'expo-blur';
import { deleteFaturaCascade } from '../services/transactionService';

const BIRIM_LISTESI = ['Adet', 'Kg', 'Mt', 'M2'];

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

export default function FaturaFormScreen({ route, navigation }: any) {
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
  const [selectedItemIndexForBirim, setSelectedItemIndexForBirim] = useState<number | null>(null);
  
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

      const items: any[] = [];
      for (const ftr of filtered) {
        const detaylar = faturaDetaylarMap[ftr.id] || await readData(`FaturaDetaylar/${ftr.id}`) || [];
        const listDetaylar = Array.isArray(detaylar) 
          ? detaylar.filter(Boolean) 
          : Object.keys(detaylar).map(key => ({ ...(detaylar as any)[key], id: parseInt(key) }));
        const mappedFatura = mapAppToDatabase('Faturalar', ftr);
        const mappedDetaylar = listDetaylar.map((d: any) => mapAppToDatabase('FaturaDetaylar', d));
        items.push({
          Fatura: mappedFatura,
          Detaylar: mappedDetaylar
        });
      }

      await generateReportPdf('fatura-batch', items, `Fatura_Listesi_${Date.now()}.pdf`);
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
    const rows = filtered.map((f: any) => ({
      'Fatura No': f.faturaNo || '',
      'Tarih': f.tarih || '',
      'Cari': f.cariUnvan || '',
      'Tür': f.tur || '',
      'Ara Toplam': f.araToplam ?? f.kdvHaricToplam ?? '',
      'KDV': f.kdvTutari ?? f.kdvOrani ?? '',
      'Genel Toplam': f.genelToplam ?? 0,
      'Vade Tarihi': f.vadeTarihi || '',
      'Ödenen': f.odenen ?? 0,
      'Kalan': (f.genelToplam ?? 0) - (f.odenen ?? 0),
    }));
    await exportToExcel(rows, 'Faturalar', `Faturalar_${new Date().getFullYear()}_${new Date().getMonth() + 1}`);
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
      navigation.setParams({ initialCari: undefined, initialTur: undefined });
    } else if (editFaturaId || route?.params?.faturaNo) {
      const loadFaturaForEdit = async () => {
        setLoading(true);
        try {
          const fId = editFaturaId ? (parseInt(editFaturaId) || editFaturaId) : undefined;
          let fData = null;
          
          if (fId) {
            fData = faturalar.find(f => String(f.id) === String(fId)) || await readData(`Faturalar/${fId}`);
          }
          
          // Eğer ID ile bulunamadıysa veya fId yoksa, FaturaNo ile ara
          if (!fData && route?.params?.faturaNo) {
            const tumFaturalarRaw = await readData('Faturalar') || {};
            const faturalarList = Array.isArray(tumFaturalarRaw)
              ? tumFaturalarRaw.filter(Boolean)
              : Object.keys(tumFaturalarRaw).map(key => ({ ...tumFaturalarRaw[key], id: parseInt(key) || key }));
              
            const bulunan = faturalarList.find(f => 
              (f.faturaNo || f.FaturaNo || '').trim() === route.params.faturaNo.trim()
            );
            if (bulunan) {
              fData = bulunan;
            }
          }

          if (fData) {
            const formatToInputDate = (dateStr: any): string => {
              if (!dateStr) return new Date().toISOString().split('T')[0];
              const str = String(dateStr).trim();
              if (str.includes('-')) {
                return str.split('T')[0];
              }
              if (str.includes('.')) {
                const parts = str.split(' ')[0].split('.');
                if (parts.length === 3) {
                  return `${parts[2]}-${parts[1].padStart(2, '0')}-${parts[0].padStart(2, '0')}`;
                }
              }
              const d = new Date(str);
              if (isNaN(d.getTime())) return new Date().toISOString().split('T')[0];
              return d.toISOString().split('T')[0];
            };

            const cleanFId = fData.id || fData.Id;
            setEditingId(cleanFId);
            setTur(fData.tur || fData.Tur || 'Satış');
            setFaturaNo(fData.faturaNo || fData.FaturaNo || '');
            setTarih(formatToInputDate(fData.tarih || fData.Tarih));
            setVadeTarihi(formatToInputDate(fData.vadeTarihi || fData.VadeTarihi));
            
            const cariIdVal = fData.cariId || fData.CariId;
            const cariRef = cariler.find(c => c.id === cariIdVal) || await readData(`Cariler/${cariIdVal}`);
            setSelectedCari(cariRef);
            
            setAciklama(fData.aciklama || fData.Aciklama || '');
            setDurum(fData.durum || fData.Durum || 'Açık');
            setOdemeSekli(fData.odemeSekli || fData.OdemeSekli || 'Açık Hesap');
            setKasaId(fData.kasaId !== undefined ? fData.kasaId : fData.KasaId || null);
            setBankaId(fData.bankaId !== undefined ? fData.bankaId : fData.BankaId || null);
            setDovizTuru(fData.dovizTuru || fData.DovizTuru || 'TL');
            setDovizKuru(fData.dovizKuru ? fData.dovizKuru.toString() : fData.DovizKuru ? fData.DovizKuru.toString() : '1');
            setBaglantiEvrakNo(fData.baglantiEvrakNo || fData.BaglantiEvrakNo || '');
            
            // Detayları oku
            const tumDetaylar = await readData(`FaturaDetaylar/${cleanFId}`) || [];
            const listDetaylar = Array.isArray(tumDetaylar) 
              ? tumDetaylar.filter(Boolean) 
              : Object.keys(tumDetaylar).map(key => ({ ...(tumDetaylar as any)[key], id: parseInt(key) }));
              
            setItems(listDetaylar.map(d => ({
              stokId: d.stokId || d.StokId,
              stokKodu: d.stokKodu || d.StokKodu || '',
              stokAdi: d.stokAdi || d.StokAdi,
              birim: d.birim || d.Birim || 'Adet',
              miktar: d.miktar || d.Miktar || 1,
              birimFiyat: d.birimFiyat || d.BirimFiyat || d.fiyat || d.Fiyat || 0,
              kdvOrani: d.kdvOrani !== undefined ? d.kdvOrani : d.KDVOrani !== undefined ? d.KDVOrani : 20,
              aciklama: d.aciklama || d.Aciklama || ''
            })));
            
            setIsFormOpen(true);
            navigation.setParams({ editFaturaId: undefined, faturaNo: undefined });
          }
        } catch (err) {
          Alert.alert('Hata', 'Fatura düzenleme modunda yüklenemedi.');
        } finally {
          setLoading(false);
        }
      };
        loadFaturaForEdit();
      } else if (rParams.initialTur) {
        setTur(rParams.initialTur);
        setFaturaNo(generateFaturaNo(rParams.initialTur));
        setTarih(new Date().toISOString().split('T')[0]);
        setVadeTarihi(new Date().toISOString().split('T')[0]);
        setAciklama('');
        setDurum('Açık');
        setItems([]);
        setIsFormOpen(true);
        navigation.setParams({ initialTur: undefined });
      }
    }, [route?.params?.initialCari, route?.params?.editFaturaId, route?.params?.faturaNo, route?.params?.initialTur]);

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
    } else if (field === 'birim') {
      newItems[index].birim = val;
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

    
    const batchUpdates: Record<string, any> = {};

    // 1. Prepare Fatura
    batchUpdates[`Faturalar/${currentId}`] = faturaData;

    // 2. Prepare FaturaDetaylar
    const detayItems = items.map((item, idx) => {
      const miktarNum = parseFloat(item.miktar) || 0;
      const fiyatNum = parseFloat(item.birimFiyat) || 0;
      const kdvOranNum = parseInt(item.kdvOrani) || 0;
      const tutarNum = miktarNum * fiyatNum;
      const kdvTutariNum = (tutarNum * kdvOranNum) / 100;
      return {
        id: generateInt32Id() + idx,
        faturaId: currentId,
        stokId: item.stokId,
        stokKodu: item.stokKodu || '',
        stokAdi: item.stokAdi || '',
        miktar: miktarNum,
        birim: item.birim || 'Adet',
        birimFiyat: fiyatNum,
        kdvOrani: kdvOranNum,
        kdvTutari: kdvTutariNum,
        toplamTutar: tutarNum,
        tutar: tutarNum,
        aciklama: item.aciklama || '',
      };
    });
    batchUpdates[`FaturaDetaylar/${currentId}`] = detayItems;

    // 2.5 Prepare StokHareketler
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
      batchUpdates[`StokHareketler/${shId}`] = stokHareket;
    }

    // 3. Update Cari Balance & CariHareketler
    const freshCari = await readData(`Cariler/${selectedCari.id}`);
    const cariRef = freshCari || cariler.find(c => String(c.id) === String(selectedCari.id));
    if (cariRef) {
      const updatedCari = { ...cariRef };
      if (isPaid) {
        updatedCari.borc = (parseFloat(updatedCari.borc) || 0) + grandTotal;
        updatedCari.alacak = (parseFloat(updatedCari.alacak) || 0) + grandTotal;
      } else {
        if (tur === 'Satış') {
          updatedCari.borc = (parseFloat(updatedCari.borc) || 0) + grandTotal;
        } else {
          updatedCari.alacak = (parseFloat(updatedCari.alacak) || 0) + grandTotal;
        }
      }
            updatedCari.bakiye = (parseFloat(updatedCari.borc) || 0) - (parseFloat(updatedCari.alacak) || 0);
      updatedCari.updatedAt = new Date().toISOString();
      updatedCari.version = (cariRef.version || 0) + 1;
      batchUpdates[`Cariler/${selectedCari.id}`] = updatedCari;

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
      batchUpdates[`CariHareketler/${nextHareketId1}`] = cariHareket1;

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
        batchUpdates[`CariHareketler/${nextHareketId2}`] = cariHareket2;
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
          batchUpdates[`KasaHareketler/${nowStr}`] = kasaHareket;

          const yeniKasaBakiye = tur === 'Satış' ? (kasaRef.bakiye || 0) + grandTotal : (kasaRef.bakiye || 0) - grandTotal;
          batchUpdates[`Bankalar/${kasaId}`] = {
            ...kasaRef,
            kartTuru: 'Kasa',
            bakiye: yeniKasaBakiye
          };
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
          batchUpdates[`BankaHareketler/${nowStr}`] = bankaHareket;

          const yeniBankaBakiye = tur === 'Satış' ? (bankaRef.bakiye || 0) + grandTotal : (bankaRef.bakiye || 0) - grandTotal;
          batchUpdates[`Bankalar/${bankaId}`] = {
            ...bankaRef,
            bakiye: yeniBankaBakiye
          };
        }
      }
    }

    // 5. Update Stock Quantities (Read all needed first)
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

        let guncellenecekStok: any = {
          ...baseStok,
          miktar: yeniMiktar,
          id: item.stokId
        };

        if (tur === 'Alış') {
          const girisMiktar = parseFloat(item.miktar) || 0;
          const birimFiyat = parseFloat(item.birimFiyat) || 0;
          const eskiMiktar = currentMiktar > 0 ? currentMiktar : 0;
          const eskiToplamDeger = eskiMiktar * (parseFloat(baseStok.alisFiyati || baseStok.ortAlisFiyati) || 0);
          const yeniGirisDeger = girisMiktar * birimFiyat;
          const yeniOrtAlisFiyati = (eskiMiktar + girisMiktar) > 0 ? (eskiToplamDeger + yeniGirisDeger) / (eskiMiktar + girisMiktar) : birimFiyat;
          guncellenecekStok.alisFiyati = birimFiyat || guncellenecekStok.alisFiyati || 0;
          guncellenecekStok.ortAlisFiyati = yeniOrtAlisFiyati;
          guncellenecekStok.satisFiyati = guncellenecekStok.satisFiyati || 0;
        } else {
          guncellenecekStok.satisFiyati = parseFloat(item.birimFiyat) || guncellenecekStok.satisFiyati || 0;
        }

        if (guncellenecekStok.isDeleted === undefined) {
          guncellenecekStok.isDeleted = false;
        }
        
        batchUpdates[`Stoklar/${item.stokId}`] = guncellenecekStok;
      } catch (stockErr) {
        console.error(`Stok ${item.stokId} hazirlanamadi:`, stockErr);
      }
    }

    // SEND ATOMIC BATCH UPDATE
    const batchSuccess = await updateDataBatch(batchUpdates);
    
    if (!batchSuccess) {
      Alert.alert('Hata', 'İşlem sırasında bir bağlantı hatası oluştu. Veriler kaydedilmedi.');
      setIsSaving(false);
      return;
    }

    setIsFormOpen(false);
    resetForm();
    navigation.setParams({ initialCari: undefined, initialTur: undefined, editFaturaId: undefined });
    navigation.goBack();
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
      const oldFatura = oldRaw && (oldRaw.id !== undefined || oldRaw.Id !== undefined || oldRaw.faturaNo || oldRaw.FaturaNo) ? oldRaw : (faturalar.find(f => String(f.id) === String(faturaId) || String(f.Id) === String(faturaId)) || null);
      if (!oldFatura) return false;

      const faturaIdVal = oldFatura.id ?? oldFatura.Id ?? faturaId;
      const faturaNo = String(oldFatura.faturaNo || oldFatura.FaturaNo || '').trim();
      const turLower = String(oldFatura.tur || oldFatura.Tur || '').toLowerCase();
      let isSatis = turLower.includes('sat') || turLower.includes('çık') || turLower.includes('cik');
      const isPaid = (oldFatura.odenen || oldFatura.Odenen || 0) > 0 || (oldFatura.odemeSekli && oldFatura.odemeSekli !== 'Açık' && oldFatura.odemeSekli !== 'Acik');
      const genelToplam = parseFloat(oldFatura.genelToplam ?? oldFatura.GenelToplam) || 0;
      const oldCariId = oldFatura.cariId ?? oldFatura.CariId;

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

      if (oldCariId) {
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
                    updatedCari.bakiye = (parseFloat(updatedCari.borc) || 0) - (parseFloat(updatedCari.alacak) || 0);
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

  const renderItem = ({ item }: { item: any }) => (
    <TouchableOpacity 
      style={styles.faturaCard}
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
    >
      <View style={styles.cardTopRow}>
        <View style={{ flex: 1 }}>
          <Text style={styles.faturaNo}>{item.faturaNo}</Text>
        </View>
        <Text style={styles.faturaTarih}>{formatFaturaTarih(item.tarih)}</Text>
      </View>
      <View style={styles.cardMiddleRow}>
        <View style={{ flex: 1, marginRight: 8 }}>
          <Text style={styles.faturaCari} numberOfLines={1}>{item.cariUnvan}</Text>
        </View>
        <View style={[styles.turPill, item.tur === 'Satış' ? styles.turSatis : styles.turAlis]}>
          <Text style={styles.turPillText}>{item.tur}</Text>
        </View>
      </View>
      <View style={styles.cardBottomRow}>
        <Text style={styles.faturaTutarLabel}>Tutar</Text>
        <View style={{ flexDirection: 'row', alignItems: 'baseline' }}>
          <Text style={styles.faturaTutar}>{formatMoney(item.genelToplam)}</Text>
          <Text style={styles.faturaTutarLira}>₺</Text>
        </View>
      </View>
    </TouchableOpacity>
  );

  return (
    <SafeAreaView style={styles.container}>
      {/* Header */}
      <View style={styles.header}>
        <View style={styles.headerTopRow}>
          <TouchableOpacity onPress={() => { navigation.setParams({ initialCari: undefined, initialTur: undefined, editFaturaId: undefined }); navigation.goBack(); }} style={{ padding: 8, marginRight: 8 }}>
            <X color="#FFF" size={24} />
          </TouchableOpacity>
          <Text style={styles.headerTitle}>{editingId ? `Faturayı Düzenle (${faturaNo})` : 'Yeni Fatura Oluştur'}</Text>
        </View>
      </View>

      {loading ? (
        <View style={styles.center}>
          <ActivityIndicator size="large" color="#0061FF" />
        </View>
      ) : (
        <KeyboardAvoidingView 
          style={{ flex: 1 }} 
          behavior={Platform.OS === 'ios' ? 'padding' : undefined}
          keyboardVerticalOffset={Platform.OS === 'ios' ? 80 : 0}
        >
          <ScrollView contentContainerStyle={styles.formScroll} keyboardShouldPersistTaps="handled">
              <View style={styles.inputGroup}>
                <Text style={styles.inputLabel}>Fatura Türü</Text>
                <View style={styles.toggleRow}>
                  <TouchableOpacity 
                    style={[styles.toggleBtn, tur === 'Satış' && styles.toggleBtnActive]}
                    onPress={() => { setTur('Satış'); setFaturaNo(generateFaturaNo('Satış')); }}
                  >
                    <Text style={[styles.toggleBtnText, tur === 'Satış' && styles.toggleBtnTextActive]}>Satış Faturası</Text>
                  </TouchableOpacity>
                  <TouchableOpacity 
                    style={[styles.toggleBtn, tur === 'Alış' && styles.toggleBtnActive]}
                    onPress={() => { setTur('Alış'); setFaturaNo(generateFaturaNo('Alış')); }}
                  >
                    <Text style={[styles.toggleBtnText, tur === 'Alış' && styles.toggleBtnTextActive]}>Alış Faturası</Text>
                  </TouchableOpacity>
                </View>
              </View>

              <TouchableOpacity style={styles.selectorCard} onPress={() => setIsCariOverlayOpen(true)}>
                <User color="#0061FF" size={20} />
                <Text style={styles.selectorText}>
                  {selectedCari ? selectedCari.unvan : 'Müşteri / Tedarikçi Seçin *'}
                </Text>
              </TouchableOpacity>

              <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                <View style={[styles.inputGroup, { width: '48%' }]}>
                  <Text style={styles.inputLabel}>Fatura Numarası</Text>
                  <TextInput 
                    style={styles.input} 
                    placeholder="FTR-..." 
                    placeholderTextColor="#64748B"
                    value={faturaNo}
                    onChangeText={setFaturaNo}
                  />
                </View>
                <View style={[styles.inputGroup, { width: '48%' }]}>
                  <Text style={styles.inputLabel}>Ödeme Şekli</Text>
                  <View style={styles.toggleRow}>
                    <TouchableOpacity 
                      style={[styles.toggleBtn, odemeSekli === 'Açık Hesap' && styles.toggleBtnActive, { paddingHorizontal: 6 }]}
                      onPress={() => { setOdemeSekli('Açık Hesap'); setKasaId(null); setBankaId(null); setDurum('Açık'); }}
                    >
                      <Text style={[styles.toggleBtnText, odemeSekli === 'Açık Hesap' && styles.toggleBtnTextActive, { fontSize: 10 }]}>Açık</Text>
                    </TouchableOpacity>
                    <TouchableOpacity 
                      style={[styles.toggleBtn, odemeSekli === 'Nakit' && styles.toggleBtnActive, { paddingHorizontal: 6 }]}
                      onPress={() => { setOdemeSekli('Nakit'); setBankaId(null); setDurum('Kapalı'); }}
                    >
                      <Text style={[styles.toggleBtnText, odemeSekli === 'Nakit' && styles.toggleBtnTextActive, { fontSize: 10 }]}>Nakit</Text>
                    </TouchableOpacity>
                    <TouchableOpacity 
                      style={[styles.toggleBtn, odemeSekli === 'Kredi Kartı' && styles.toggleBtnActive, { paddingHorizontal: 6 }]}
                      onPress={() => { setOdemeSekli('Kredi Kartı'); setKasaId(null); setDurum('Kapalı'); }}
                    >
                      <Text style={[styles.toggleBtnText, odemeSekli === 'Kredi Kartı' && styles.toggleBtnTextActive, { fontSize: 10 }]}>Kart</Text>
                    </TouchableOpacity>
                  </View>
                </View>
              </View>

              {odemeSekli === 'Nakit' && (
                <View style={styles.inputGroup}>
                  <Text style={styles.inputLabel}>Kasa Seçimi *</Text>
                  <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ flexDirection: 'row', paddingVertical: 4 }}>
                    {kasalar.map(k => (
                      <TouchableOpacity 
                        key={k.id} 
                        style={[styles.miniSelectCard, kasaId === k.id && styles.miniSelectCardActive]} 
                        onPress={() => setKasaId(k.id)}
                      >
                        <Text style={[styles.miniSelectText, kasaId === k.id && styles.miniSelectTextActive]}>{k.hesapAdi || k.bankaAdi || k.ad || k.isim || 'İsimsiz Kasa'}</Text>
                      </TouchableOpacity>
                    ))}
                  </ScrollView>
                </View>
              )}

              {odemeSekli === 'Kredi Kartı' && (
                <View style={styles.inputGroup}>
                  <Text style={styles.inputLabel}>Banka Seçimi *</Text>
                  <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ flexDirection: 'row', paddingVertical: 4 }}>
                    {bankalar.map(b => (
                      <TouchableOpacity 
                        key={b.id} 
                        style={[styles.miniSelectCard, bankaId === b.id && styles.miniSelectCardActive]} 
                        onPress={() => setBankaId(b.id)}
                      >
                        <Text style={[styles.miniSelectText, bankaId === b.id && styles.miniSelectTextActive]}>{b.hesapAdi || b.bankaAdi || b.isim || 'İsimsiz Banka'}</Text>
                      </TouchableOpacity>
                    ))}
                  </ScrollView>
                </View>
              )}

              <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                <View style={[styles.inputGroup, { width: '48%' }]}>
                  <Text style={styles.inputLabel}>Döviz Türü</Text>
                  <View style={styles.toggleRow}>
                    {['TL', 'USD', 'EUR'].map(curr => (
                      <TouchableOpacity 
                        key={curr} 
                        style={[styles.toggleBtn, dovizTuru === curr && styles.toggleBtnActive, { flex: 1 }]} 
                        onPress={() => { setDovizTuru(curr); if (curr === 'TL') setDovizKuru('1'); }}
                      >
                        <Text style={[styles.toggleBtnText, dovizTuru === curr && styles.toggleBtnTextActive]}>{curr}</Text>
                      </TouchableOpacity>
                    ))}
                  </View>
                </View>
                <View style={[styles.inputGroup, { width: '48%' }]}>
                  <Text style={styles.inputLabel}>Döviz Kuru</Text>
                  <TextInput 
                    style={styles.input} 
                    keyboardType="numeric" 
                    editable={dovizTuru !== 'TL'}
                    placeholder="1" 
                    placeholderTextColor="#64748B" 
                    value={dovizKuru} 
                    onChangeText={setDovizKuru} 
                  />
                </View>
              </View>

              <View style={[styles.inputGroup]}>
                <Text style={styles.inputLabel}>Bağlantı Evrak No</Text>
                <TextInput 
                  style={styles.input} 
                  placeholder="Sipariş/Teklif No..." 
                  placeholderTextColor="#64748B"
                  value={baglantiEvrakNo}
                  onChangeText={setBaglantiEvrakNo}
                />
              </View>

              <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                <View style={[styles.inputGroup, { width: '48%' }]}>
                  <Text style={styles.inputLabel}>Tarih</Text>
                  <TextInput 
                    style={styles.input} 
                    placeholder="YYYY-MM-DD" 
                    placeholderTextColor="#64748B"
                    value={tarih}
                    onChangeText={setTarih}
                  />
                </View>
                <View style={[styles.inputGroup, { width: '48%' }]}>
                  <Text style={styles.inputLabel}>Vade Tarihi</Text>
                  <TextInput 
                    style={styles.input} 
                    placeholder="YYYY-MM-DD" 
                    placeholderTextColor="#64748B"
                    value={vadeTarihi}
                    onChangeText={setVadeTarihi}
                  />
                </View>
              </View>

              {/* Fatura Detay Kalemleri */}
              <View style={styles.itemsSection}>
                <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
                  <Text style={styles.itemsTitle}>Fatura Kalemleri</Text>
                  <TouchableOpacity style={styles.addItemBtn} onPress={() => setIsStokOverlayOpen(true)}>
                    <Plus color="#00FF87" size={16} />
                    <Text style={styles.addItemBtnText}>Kalem Ekle</Text>
                  </TouchableOpacity>
                </View>

                {items.length === 0 ? (
                  <Text style={styles.emptyItemsText}>Henüz kalem eklenmedi.</Text>
                ) : (
                  items.map((item, index) => (
                    <View key={index} style={styles.itemCard}>
                      <View style={styles.itemCardHeader}>
                        <Text style={styles.itemRowTitle}>{item.stokAdi}</Text>
                        <TouchableOpacity onPress={() => handleRemoveItem(index)} style={styles.removeItemBtn}>
                          <Trash2 color="#EF4444" size={18} />
                        </TouchableOpacity>
                      </View>

                      <View style={styles.itemCardBody}>
                        <View style={[styles.fieldContainer, { flex: 1.2 }]}>
                          <Text style={styles.fieldLabel}>Miktar</Text>
                          <View style={styles.quantityContainer}>
                            <TextInput 
                              style={[styles.itemInput, { flex: 1, textAlign: 'left', backgroundColor: 'transparent' }]}
                              keyboardType="numeric"
                              placeholder="Miktar"
                              placeholderTextColor="#64748B"
                              value={String(item.miktar)}
                              onChangeText={(val) => handleItemChange(index, 'miktar', val)}
                            />
                            <TouchableOpacity 
                              style={styles.unitPickerBtn}
                              onPress={() => setSelectedItemIndexForBirim(index)}
                              activeOpacity={0.7}
                            >
                              <Text style={styles.unitPickerBtnText}>{item.birim || 'Adet'}</Text>
                              <ChevronDown color="#94A3B8" size={13} style={{ marginLeft: 3 }} />
                            </TouchableOpacity>
                          </View>
                        </View>

                        <View style={{ width: 12 }} />

                        <View style={[styles.fieldContainer, { flex: 1.8 }]}>
                          <Text style={styles.fieldLabel}>Birim Fiyatı</Text>
                          <TextInput 
                            style={[styles.itemInput, { textAlign: 'left', paddingHorizontal: 12 }]}
                            keyboardType="numeric"
                            placeholder="Fiyat"
                            placeholderTextColor="#64748B"
                            value={String(item.birimFiyat)}
                            onChangeText={(val) => handleItemChange(index, 'birimFiyat', val)}
                          />
                        </View>

                        <View style={{ width: 12 }} />

                        <View style={[styles.fieldContainer, { flex: 1 }]}>
                          <Text style={styles.fieldLabel}>KDV</Text>
                          <View style={styles.kdvReadOnlyBox}>
                            <Text style={styles.kdvReadOnlyText}>%{item.kdvOrani || 0}</Text>
                          </View>
                        </View>
                      </View>

                      <View style={{ marginTop: 10 }}>
                        <Text style={styles.fieldLabel}>Kalem Açıklaması (Opsiyonel)</Text>
                        <TextInput
                          style={[styles.itemInput, { width: '100%', textAlign: 'left', paddingHorizontal: 12 }]}
                          placeholder="Açıklama girin..."
                          placeholderTextColor="#64748B"
                          value={item.aciklama || ''}
                          onChangeText={(val) => handleItemChange(index, 'aciklama', val)}
                        />
                      </View>
                    </View>
                  ))
                )}
              </View>

              <View style={styles.inputGroup}>
                <Text style={styles.inputLabel}>Açıklama</Text>
                <TextInput 
                  style={[styles.input, { height: 60 }]} 
                  placeholder="Fatura hakkında not..." 
                  placeholderTextColor="#64748B"
                  multiline={true}
                  value={aciklama}
                  onChangeText={setAciklama}
                />
              </View>

              {/* Toplam Bilgi Kartı */}
              <View style={styles.summaryCard}>
                <View style={styles.summaryRow}>
                  <Text style={styles.summaryLabel}>Ara Toplam:</Text>
                  <Text style={styles.summaryVal}>{formatMoney(calculateSubtotal())} ₺</Text>
                </View>
                <View style={styles.summaryRow}>
                  <Text style={styles.summaryLabel}>KDV Toplamı:</Text>
                  <Text style={styles.summaryVal}>{formatMoney(calculateTotalKdv())} ₺</Text>
                </View>
                <View style={[styles.summaryRow, { borderTopWidth: 1, borderTopColor: 'rgba(255,255,255,0.05)', paddingTop: 8, marginTop: 8 }]}>
                  <Text style={[styles.summaryLabel, { fontWeight: 'bold', color: '#FFF' }]}>Genel Toplam:</Text>
                  <Text style={[styles.summaryVal, { fontWeight: 'bold', color: '#60A5FA', fontSize: 15 }]}>{formatMoney(calculateGrandTotal())} ₺</Text>
                </View>
              </View>

              <TouchableOpacity style={[styles.saveButton, isSaving && { opacity: 0.7 }]} disabled={isSaving} onPress={handleSave}>
                {isSaving ? <ActivityIndicator color="#FFF" size="small" /> : <Save color="#FFF" size={20} />}
                <Text style={styles.saveButtonText}>{isSaving ? 'Kaydediliyor...' : 'Faturayı Kaydet'}</Text>
              </TouchableOpacity>
            </ScrollView>
        </KeyboardAvoidingView>
          )}

            {/* Cari Seçici Absolute Overlay (Nested Modal yerine) */}
            {isCariOverlayOpen && (
              <View style={styles.absoluteOverlay}>
                <BlurView intensity={80} tint="dark" style={StyleSheet.absoluteFill} />
                <View style={styles.overlayHeader}>
                  <User color="#60A5FA" size={22} />
                  <Text style={styles.overlayTitle}>Cari Kart Seçin</Text>
                  <TouchableOpacity 
                    style={styles.overlayCloseBtn}
                    onPress={() => setIsCariOverlayOpen(false)}
                  >
                    <X color="#FFF" size={22} />
                  </TouchableOpacity>
                </View>
                <View style={styles.overlaySearchBox}>
                  <Search color="#94A3B8" size={20} />
                  <TextInput 
                    style={styles.overlaySearchInput}
                    placeholder="Cari ara..."
                    placeholderTextColor="#94A3B8"
                    value={cariSearch}
                    onChangeText={setCariSearch}
                  />
                </View>
                <View style={{ flexDirection: 'row', flexWrap: 'wrap', marginBottom: 12 }}>
                  <TouchableOpacity
                    style={[styles.grupChip, cariGrupFiltre === '' && styles.grupChipActive]}
                    onPress={() => setCariGrupFiltre('')}
                  >
                    <Text style={[styles.grupChipText, cariGrupFiltre === '' && styles.grupChipTextActive]}>Tümü</Text>
                  </TouchableOpacity>
                  {[...new Set(cariler.map(c => (c.grup || '').trim()).filter(Boolean))].map(g => (
                    <TouchableOpacity
                      key={g}
                      style={[styles.grupChip, cariGrupFiltre === g && styles.grupChipActive]}
                      onPress={() => setCariGrupFiltre(cariGrupFiltre === g ? '' : g)}
                    >
                      <Text style={[styles.grupChipText, cariGrupFiltre === g && styles.grupChipTextActive]}>{g}</Text>
                    </TouchableOpacity>
                  ))}
                </View>
                <FlatList initialNumToRender={20} maxToRenderPerBatch={20} windowSize={5} 
                  style={styles.overlayFlatlist}
                  contentContainerStyle={{ paddingBottom: 40 }}
                  data={cariler.filter(c =>
                    ((c.unvan || '').toLocaleLowerCase('tr-TR').includes(cariSearch.toLocaleLowerCase('tr-TR')) ||
                     (c.cariKod || '').toLocaleLowerCase('tr-TR').includes(cariSearch.toLocaleLowerCase('tr-TR'))) &&
                    (!cariGrupFiltre || (c.grup || '') === cariGrupFiltre)
                  )}
                  keyExtractor={(item, idx) => item.id?.toString() || idx.toString()}
                  renderItem={({ item }) => (
                    <TouchableOpacity 
                      style={styles.selectorItem}
                      onPress={() => { handleSelectCari(item); setIsCariOverlayOpen(false); }}
                    >
                      <Text style={styles.selectorItemText}>{item.unvan}</Text>
                      <Text style={styles.selectorItemSub}>{item.grup} • {formatMoney(((item.borc || 0) - (item.alacak || 0)))} ₺</Text>
                    </TouchableOpacity>
                  )}
                />
              </View>
            )}

            {/* Stok Seçici Absolute Overlay (Nested Modal yerine) */}
            {isStokOverlayOpen && (
              <View style={styles.absoluteOverlay}>
                <BlurView intensity={80} tint="dark" style={StyleSheet.absoluteFill} />
                <View style={styles.overlayHeader}>
                  <Package color="#3B82F6" size={22} />
                  <Text style={styles.overlayTitle}>Stok Seçin</Text>
                  <TouchableOpacity 
                    style={styles.overlayCloseBtn}
                    onPress={() => setIsStokOverlayOpen(false)}
                  >
                    <X color="#FFF" size={22} />
                  </TouchableOpacity>
                </View>
                <View style={styles.overlaySearchBox}>
                  <Search color="#94A3B8" size={20} />
                  <TextInput 
                    style={styles.overlaySearchInput}
                    placeholder="Stok adı veya kodu ile ara..."
                    placeholderTextColor="#94A3B8"
                    autoFocus
                    value={stokSearch}
                    onChangeText={setStokSearch}
                  />
                </View>
                <FlatList initialNumToRender={20} maxToRenderPerBatch={20} windowSize={5} 
                  style={styles.overlayFlatlist}
                  contentContainerStyle={{ paddingBottom: 40 }}
                  data={stoklar.filter(s => 
                    s.isDeleted !== true && (
                      (s.stokAdi || '').toLocaleLowerCase('tr-TR').includes(stokSearch.toLocaleLowerCase('tr-TR')) ||
                      (s.stokKodu || '').toLocaleLowerCase('tr-TR').includes(stokSearch.toLocaleLowerCase('tr-TR')) ||
                      (s.barkod || '').toLocaleLowerCase('tr-TR').includes(stokSearch.toLocaleLowerCase('tr-TR'))
                    )
                  )}
                  keyExtractor={(item, idx) => item.id?.toString() || idx.toString()}
                  ListEmptyComponent={
                    <View style={{ padding: 40, alignItems: 'center' }}>
                      <Package color="#334155" size={48} />
                      <Text style={{ color: '#64748B', marginTop: 12, textAlign: 'center' }}>
                        "{stokSearch}" için stok bulunamadı
                      </Text>
                    </View>
                  }
                  renderItem={({ item }) => {
                    const fiyat = tur === 'Satış'
                      ? (parseFloat(item.satisFiyati) || parseFloat(item.ortSatisFiyati) || 0)
                      : (parseFloat(item.alisFiyati) || parseFloat(item.ortAlisFiyati) || 0);
                    const miktar = parseFloat(item.miktar) || 0;
                    const durumRenk = miktar <= 0 ? '#EF4444' : miktar < (item.minSeviye || 5) ? '#F59E0B' : '#10B981';
                    return (
                      <TouchableOpacity 
                        style={styles.overlayItemCard}
                        onPress={() => handleAddItem(item)}
                      >
                        <View style={styles.overlayItemIconBox}>
                          <Package color="#3B82F6" size={22} />
                        </View>
                        <View style={{ flex: 1, marginLeft: 12 }}>
                          <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                            <Text style={styles.overlayItemTitle} numberOfLines={1}>
                              {item.stokAdi || 'Stok Adı Yok'}
                            </Text>
                            <View style={[styles.overlayItemPill, { backgroundColor: `${durumRenk}15`, borderColor: `${durumRenk}30` }]}>
                              <Text style={[styles.overlayItemPillText, { color: durumRenk }]}>
                                {miktar} {item.birim || 'Adet'}
                              </Text>
                            </View>
                          </View>
                          <View style={{ flexDirection: 'row', marginTop: 6, alignItems: 'center' }}>
                            <Text style={styles.overlayItemCode}>
                              {item.stokKodu || `STK-${item.id}`}
                            </Text>
                            {item.grup ? (
                              <>
                                <Text style={{ color: '#475569', marginHorizontal: 6 }}>•</Text>
                                <Text style={styles.overlayItemGroup}>{item.grup}</Text>
                              </>
                            ) : null}
                          </View>
                          <View style={{ marginTop: 8 }}>
                            <Text style={styles.overlayItemPriceLabel}>
                              {tur === 'Satış' ? 'Satış Fiyatı' : 'Alış Fiyatı'}
                            </Text>
                            <Text style={styles.overlayItemPriceValue}>
                              {formatMoney(fiyat)} <Text style={{ fontSize: 12, opacity: 0.7 }}>₺</Text>
                              {item.kdv ? (
                                <Text style={styles.overlayItemKdv}> +%{item.kdv} KDV</Text>
                              ) : null}
                            </Text>
                          </View>
                        </View>
                      </TouchableOpacity>
                    );
                  }}
                />
              </View>
            )}

            {/* Birim Seçici Modal */}
            <Modal
              visible={selectedItemIndexForBirim !== null}
              transparent={true}
              animationType="fade"
              onRequestClose={() => setSelectedItemIndexForBirim(null)}
            >
              <TouchableOpacity 
                style={styles.unitModalBackdrop}
                activeOpacity={1}
                onPress={() => setSelectedItemIndexForBirim(null)}
              >
                <View style={styles.unitModalContainer}>
                  <View style={styles.unitModalHeader}>
                    <Text style={styles.unitModalTitle}>Birim Seçin</Text>
                    <TouchableOpacity onPress={() => setSelectedItemIndexForBirim(null)}>
                      <X color="#FFF" size={20} />
                    </TouchableOpacity>
                  </View>
                  <View style={styles.unitGrid}>
                    {BIRIM_LISTESI.map((b) => {
                      const isSelected = selectedItemIndexForBirim !== null && items[selectedItemIndexForBirim]?.birim === b;
                      return (
                        <TouchableOpacity
                          key={b}
                          style={[styles.unitChip, isSelected && styles.unitChipActive]}
                          onPress={() => {
                            if (selectedItemIndexForBirim !== null) {
                              handleItemChange(selectedItemIndexForBirim, 'birim', b);
                            }
                            setSelectedItemIndexForBirim(null);
                          }}
                        >
                          <Text style={[styles.unitChipText, isSelected && styles.unitChipTextActive]}>{b}</Text>
                        </TouchableOpacity>
                      );
                    })}
                  </View>
                </View>
              </TouchableOpacity>
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
  listContent: {
    padding: 14,
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
  itemCard: {
    backgroundColor: 'rgba(255,255,255,0.03)',
    borderRadius: 12,
    padding: 12,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.08)',
  },
  itemCardHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingBottom: 8,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255,255,255,0.05)',
    marginBottom: 8,
  },
  removeItemBtn: {
    padding: 4,
  },
  itemCardBody: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  fieldContainer: {
    flexDirection: 'column',
  },
  fieldLabel: {
    color: '#94A3B8',
    fontSize: 10,
    fontWeight: '600',
    marginBottom: 4,
    textTransform: 'uppercase',
  },
  quantityContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#2A2A2A',
    borderRadius: 8,
    paddingRight: 8,
  },
  itemUnitLabel: {
    color: '#94A3B8',
    fontSize: 11,
    marginLeft: 4,
  },
  kdvReadOnlyBox: {
    backgroundColor: 'rgba(255,255,255,0.06)',
    borderRadius: 8,
    paddingVertical: 5,
    paddingHorizontal: 10,
    alignItems: 'center',
    justifyContent: 'center',
    height: 32,
  },
  kdvReadOnlyText: {
    color: '#10B981',
    fontSize: 12,
    fontWeight: 'bold',
  },
  itemInput: {
    backgroundColor: '#2A2A2A',
    borderRadius: 8,
    color: '#FFF',
    paddingHorizontal: 8,
    paddingVertical: 6,
    fontSize: 12,
    height: 32,
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
    backgroundColor: 'rgba(10, 10, 10, 0.4)',
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
  },
  unitPickerBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#334155',
    paddingHorizontal: 8,
    paddingVertical: 5,
    borderRadius: 6,
    borderWidth: 1,
    borderColor: '#475569',
    marginLeft: 4,
  },
  unitPickerBtnText: {
    color: '#F8FAFC',
    fontSize: 12,
    fontWeight: 'bold',
  },
  unitModalBackdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.7)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: 24,
  },
  unitModalContainer: {
    width: '100%',
    maxWidth: 360,
    backgroundColor: '#1E293B',
    borderRadius: 16,
    padding: 20,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.1)',
  },
  unitModalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 16,
    paddingBottom: 12,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255,255,255,0.08)',
  },
  unitModalTitle: {
    color: '#FFF',
    fontSize: 16,
    fontWeight: 'bold',
  },
  unitGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  unitChip: {
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderRadius: 8,
    backgroundColor: '#0F172A',
    borderWidth: 1,
    borderColor: '#334155',
    minWidth: '28%',
    alignItems: 'center',
  },
  unitChipActive: {
    backgroundColor: '#2563EB',
    borderColor: '#60A5FA',
  },
  unitChipText: {
    color: '#CBD5E1',
    fontSize: 13,
    fontWeight: '600',
  },
  unitChipTextActive: {
    color: '#FFF',
    fontWeight: 'bold',
  }
});

