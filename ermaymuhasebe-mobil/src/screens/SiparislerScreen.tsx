import React, { useState, useEffect } from 'react';
import { StyleSheet, Text, View, SafeAreaView, FlatList, TextInput, ActivityIndicator, TouchableOpacity, Modal, ScrollView, Alert, Share, PanResponder } from 'react-native';
import { FlashList } from '@shopify/flash-list';
import { Search, FileSignature, Plus, X, Save, Edit3, Trash2, Calendar, User, ShoppingBag, Share2 } from 'lucide-react-native';
import { subscribeToPath, writeData, readData, mapAppToDatabase } from '../services/firebase';
import { generateInt32Id } from '../utils/IdGenerator';
import { generateReportPdf } from '../services/pdfService';

const formatMoney = (val: number) => {
  return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' }).format(val);
};

export default function SiparislerScreen({ route, navigation }: any) {
  const [siparisler, setSiparisler] = useState<any[]>([]);
  const [cariler, setCariler] = useState<any[]>([]);
  const [stoklar, setStoklar] = useState<any[]>([]);
  const [siparisDetaylarMap, setSiparisDetaylarMap] = useState<Record<string, any[]>>({});
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');

  // Modals state
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [isDetailOpen, setIsDetailOpen] = useState(false);
  const [isCariOverlayOpen, setIsCariOverlayOpen] = useState(false);
  const [isStokOverlayOpen, setIsStokOverlayOpen] = useState(false);
  
  // Saving guard to prevent double-tap double-save
  const [isSaving, setIsSaving] = useState(false);
  
  const [selectedSiparis, setSelectedSiparis] = useState<any | null>(null);
  const openedFromDashboardRef = React.useRef(false);

  React.useEffect(() => {
    if (route?.params?.openForm) {
      setIsFormOpen(true);
      openedFromDashboardRef.current = true;
      navigation.setParams({ openForm: undefined });
    }
  }, [route?.params?.openForm]);

  const handleCloseForm = () => {
    setIsFormOpen(false);
    if (openedFromDashboardRef.current) {
      openedFromDashboardRef.current = false;
      navigation.navigate('Dashboard');
    }
  };

  const panResponder = React.useRef(
    PanResponder.create({
      onStartShouldSetPanResponder: () => true,
      onStartShouldSetPanResponderCapture: () => true,
      onMoveShouldSetPanResponder: () => true,
      onMoveShouldSetPanResponderCapture: () => true,
      onPanResponderRelease: (evt, gestureState) => {
        if (gestureState.dx > 30) {
          handleCloseForm();
        }
      }
    })
  ).current;

  const handleShareSiparis = async (siparis: any) => {
    try {
      const detaylar = siparisDetaylarMap[siparis.id] || [];
      const cari = cariler.find(c => c.id === siparis.cariId) || { unvan: siparis.cariUnvan };
      
      const mappedSiparis = mapAppToDatabase('Siparisler', siparis);
      const mappedDetaylar = detaylar.map(d => mapAppToDatabase('SiparisDetaylar', d));
      const mappedCari = mapAppToDatabase('Cariler', cari);

      const payload = {
        Siparis: mappedSiparis,
        Detaylar: mappedDetaylar,
        Cari: mappedCari
      };

      const reportFileName = `Siparis_${siparis.siparisNo}.pdf`;
      await generateReportPdf('siparis', payload, reportFileName);
    } catch (error) {
      console.error('Paylaşım hatası:', error);
      Alert.alert('Hata', 'Sipariş PDF\'i paylaşılırken bir sorun oluştu.');
    }
  };

  // Form fields state
  const [editingId, setEditingId] = useState<number | null>(null);
  const [siparisNo, setSiparisNo] = useState('');
  const [tarih, setTarih] = useState(new Date().toISOString().split('T')[0]);
  const [teslimatTarihi, setTeslimatTarihi] = useState(new Date().toISOString().split('T')[0]);
  const [selectedCari, setSelectedCari] = useState<any | null>(null);
  const [aciklama, setAciklama] = useState('');
  const [durum, setDurum] = useState<'Bekliyor' | 'Faturalandı' | 'İptal'>('Bekliyor');
  const [odemeBilgisi, setOdemeBilgisi] = useState('');
  const [oncelik, setOncelik] = useState<'Düşük' | 'Orta' | 'Yüksek'>('Orta');
  const [baglantiEvrakNo, setBaglantiEvrakNo] = useState('');
  const [pdfNotlar, setPdfNotlar] = useState('');
  
  // Items state
  const [items, setItems] = useState<any[]>([]);
  
  // Search queries
  const [cariSearch, setCariSearch] = useState('');
  const [stokSearch, setStokSearch] = useState('');

  useEffect(() => {
    const unsubSiparis = subscribeToPath('Siparisler', (data) => {
      if (!data) {
        setSiparisler([]);
      } else {
        const list = Array.isArray(data) 
          ? data.filter(Boolean) 
          : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setSiparisler(list.filter(s => !s.isDeleted).sort((a, b) => new Date(b.tarih).getTime() - new Date(a.tarih).getTime()));
      }
    });

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

    const unsubDetaylar = subscribeToPath('SiparisDetaylar', (data) => {
      if (data) {
        setSiparisDetaylarMap(data);
      }
      setLoading(false);
    });

    return () => {
      unsubSiparis();
      unsubCariler();
      unsubStoklar();
      unsubDetaylar();
    };
  }, []);

  const filteredSiparisler = siparisler.filter(s => 
    (s.cariUnvan || '').toLocaleLowerCase('tr-TR').includes(searchQuery.toLocaleLowerCase('tr-TR')) ||
    (s.siparisNo || '').toLocaleLowerCase('tr-TR').includes(searchQuery.toLocaleLowerCase('tr-TR'))
  );

  const generateSiparisNo = () => {
    const random = Math.floor(100000 + Math.random() * 900000);
    return `SIP-${random}`;
  };

  const resetForm = () => {
    setEditingId(null);
    setSiparisNo(generateSiparisNo());
    setTarih(new Date().toISOString().split('T')[0]);
    setTeslimatTarihi(new Date().toISOString().split('T')[0]);
    setSelectedCari(null);
    setAciklama('');
    setDurum('Bekliyor');
    setOdemeBilgisi('');
    setOncelik('Orta');
    setBaglantiEvrakNo('');
    setPdfNotlar('');
    setItems([]);
  };

  const handleOpenAdd = () => {
    resetForm();
    setIsFormOpen(true);
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
      stokAdi: stok.stokAdi,
      birim: stok.birim || 'Adet',
      miktar: 1,
      birimFiyat: stok.satisFiyati || 0,
      kdvOrani: stok.kdv !== undefined ? stok.kdv : stok.kdvOrani !== undefined ? stok.kdvOrani : 20,
      aciklama: '',
      miktarAciklama: '',
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
    } else if (field === 'miktarAciklama') {
      newItems[index].miktarAciklama = val;
    }
    setItems(newItems);
  };

  const handleSave = async () => {
    if (isSaving) return;
    if (!selectedCari) {
      Alert.alert('Hata', 'Lütfen bir cari seçiniz.');
      return;
    }
    if (items.length === 0) {
      Alert.alert('Hata', 'Lütfen en az bir kalem ekleyiniz.');
      return;
    }

    const currentId = editingId || generateInt32Id();
    const subtotal = calculateSubtotal();
    const totalKdv = calculateTotalKdv();
    const grandTotal = calculateGrandTotal();

    const siparisData: any = {
      id: currentId,
      siparisNo: siparisNo.trim(),
      tarih,
      teslimatTarihi,
      cariId: selectedCari.id,
      cariUnvan: selectedCari.unvan,
      durum,
      odemeBilgisi: odemeBilgisi.trim(),
      oncelik,
      baglantiEvrakNo: baglantiEvrakNo.trim(),
      pdfNotlar: pdfNotlar.trim(),
      araToplam: subtotal,
      kdvToplam: totalKdv,
      genelToplam: grandTotal,
      aciklama: aciklama.trim(),
      isDeleted: false,
    };
    if (!editingId) {
      siparisData.kayitTarihi = new Date().toISOString();
    }

    setIsSaving(true);
    // 1. Save Siparis
    const sSuccess = await writeData(`Siparisler/${currentId}`, siparisData);
    if (!sSuccess) {
      Alert.alert('Hata', 'Sipariş kaydedilirken hata oluştu.');
      setIsSaving(false);
      return;
    }

    // 2. Save SiparisDetaylar
    const detayItems = items.map((item, idx) => ({
      id: idx + 1,
      siparisId: currentId,
      stokId: item.stokId,
      stokAdi: item.stokAdi,
      miktar: item.miktar,
      topMiktari: item.miktar,
      birim: item.birim,
      birimFiyat: item.birimFiyat,
      tutar: item.miktar * item.birimFiyat,
      kdvOrani: item.kdvOrani,
      aciklama: item.aciklama || '',
      miktarAciklama: item.miktarAciklama || '',
      paraBirimi: 'TL',
    }));
    const okDetay = await writeData(`SiparisDetaylar/${currentId}`, detayItems);
    if (!okDetay) {
      Alert.alert('Uyarı', 'Sipariş kaydedildi ancak detaylar eşitlenemedi. (Bağlantı sorunu — detaylar sıraya alındı.)');
    }

    setIsFormOpen(false);
    resetForm();
    Alert.alert('Başarılı', 'Sipariş başarıyla kaydedildi.');
    setIsSaving(false);
  };

  const handleOpenEdit = async (siparis: any) => {
    try {
      setEditingId(siparis.id);
      setSiparisNo(siparis.siparisNo || '');
      setTarih(siparis.tarih || new Date().toISOString().split('T')[0]);
      setTeslimatTarihi(siparis.teslimatTarihi || new Date().toISOString().split('T')[0]);
      setSelectedCari(cariler.find(c => c.id === siparis.cariId) || { id: siparis.cariId, unvan: siparis.cariUnvan });
      setAciklama(siparis.aciklama || '');
      setDurum(siparis.durum || 'Bekliyor');
      setOdemeBilgisi(siparis.odemeBilgisi || '');
      setOncelik(siparis.oncelik || 'Orta');
      setBaglantiEvrakNo(siparis.baglantiEvrakNo || '');
      setPdfNotlar(siparis.pdfNotlar || '');

      let detaylar = siparisDetaylarMap[siparis.id] || [];
      if (detaylar.length === 0) {
        const raw = await readData(`SiparisDetaylar/${siparis.id}`) || [];
        detaylar = Array.isArray(raw) ? raw.filter(Boolean) : Object.keys(raw).map(key => ({ ...(raw as any)[key], id: parseInt(key) }));
      }

      setItems(detaylar.map(d => ({
        stokId: d.stokId,
        stokAdi: d.stokAdi,
        birim: d.birim || 'Adet',
        miktar: d.miktar || 1,
        birimFiyat: d.birimFiyat || d.fiyat || 0,
        kdvOrani: d.kdvOrani || 20,
        aciklama: d.aciklama || '',
        miktarAciklama: d.miktarAciklama || '',
      })));

      setIsDetailOpen(false);
      setIsFormOpen(true);
    } catch (err) {
      console.error('Sipariş düzenleme hatası:', err);
      Alert.alert('Hata', 'Sipariş düzenleme modunda yüklenemedi.');
    }
  };

  const handleDeleteSiparis = (siparis: any) => {
    Alert.alert(
      'Silme Onayı',
      `${siparis.siparisNo} numaralı siparişi silmek istediğinize emin misiniz?`,
      [
        { text: 'Vazgeç', style: 'cancel' },
        {
          text: 'Sil',
          style: 'destructive',
          onPress: async () => {
            try {
              const ok = await writeData(`Siparisler/${siparis.id}`, { ...siparis, isDeleted: true });
              if (!ok) {
                Alert.alert('Hata', 'Sipariş silinemedi. (Bağlantı sorunu — kayıt eşitlenemedi.)');
                return;
              }
              setIsDetailOpen(false);
              Alert.alert('Başarılı', 'Sipariş başarıyla silindi.');
            } catch (err) {
              console.error('Sipariş silme hatası:', err);
              Alert.alert('Hata', 'Sipariş silinirken bir hata oluştu.');
            }
          }
        }
      ]
    );
  };

  const handleUpdateDurum = async (siparis: any, yeniDurum: string) => {
    try {
      const ok = await writeData(`Siparisler/${siparis.id}`, { ...siparis, durum: yeniDurum });
      if (!ok) {
        Alert.alert('Hata', 'Sipariş durumu güncellenemedi. (Bağlantı sorunu — kayıt sıraya alındı.)');
        return;
      }
      setSelectedSiparis({ ...siparis, durum: yeniDurum });
      Alert.alert('Başarılı', `Sipariş durumu "${yeniDurum}" olarak güncellendi.`);
    } catch (err) {
      console.error('Sipariş durum güncelleme hatası:', err);
      Alert.alert('Hata', 'Sipariş durumu güncellenemedi.');
    }
  };

  // Convert Order to Invoice
  const handleConvertToFatura = async (siparis: any) => {
    const detaylar = siparisDetaylarMap[siparis.id] || [];
    if (detaylar.length === 0) {
      Alert.alert('Hata', 'Siparişin detay kalemleri bulunamadı.');
      return;
    }

    // Faturalandır durumuna güncelle
    const updatedSiparis = { ...siparis, durum: 'Faturalandı' };
    const okSiparisDurum = await writeData(`Siparisler/${siparis.id}`, updatedSiparis);
    if (!okSiparisDurum) {
      Alert.alert('Hata', 'Sipariş durumu güncellenemedi. (Bağlantı sorunu — kayıt sıraya alındı.)');
      return;
    }

    // Faturalar düğümüne satış faturası olarak ekle
    const nextFaturaId = generateInt32Id();
    const faturaPayload = {
      id: nextFaturaId,
      faturaNo: `FTR-SP-${siparis.siparisNo.split('-')[1] || generateInt32Id().toString().substring(5)}`,
      tarih: new Date().toISOString().split('T')[0],
      vadeTarihi: new Date().toISOString().split('T')[0],
      cariId: siparis.cariId,
      cariUnvan: siparis.cariUnvan,
      tur: 'Satış',
      durum: 'Açık',
      araToplam: siparis.araToplam || siparis.brutToplam || 0,
      kdvToplam: siparis.kdvToplam || 0,
      genelToplam: siparis.genelToplam || 0,
      odenen: 0,
      aciklama: `${siparis.siparisNo} nolu siparişten otomatik faturaya dönüştürüldü.`,
      isDeleted: false,
    };
    const okFatura = await writeData(`Faturalar/${nextFaturaId}`, faturaPayload);
    if (!okFatura) {
      Alert.alert('Hata', 'Sipariş güncellendi ancak fatura oluşturulamadı. (Bağlantı sorunu — işlem sıraya alındı.)');
      return;
    }

    // FaturaDetaylar tablosuna detayları ekle
    const detayItems = detaylar.map((item, idx) => ({
      id: idx + 1,
      faturaId: nextFaturaId,
      stokId: item.stokId,
      stokAdi: item.stokAdi,
      miktar: item.miktar,
      birim: item.birim || 'Adet',
      birimFiyat: item.birimFiyat,
      toplamTutar: item.tutar || item.toplamTutar || (item.miktar * item.birimFiyat),
      kdvOrani: item.kdvOrani || 20,
    }));
    const okFaturaDetay = await writeData(`FaturaDetaylar/${nextFaturaId}`, detayItems);
    if (!okFaturaDetay) {
      Alert.alert('Uyarı', 'Fatura oluşturuldu ancak detaylar eşitlenemedi. (Bağlantı sorunu — detaylar sıraya alındı.)');
    }

    // Cari Borcunu Güncelle
    const activeCari = cariler.find(c => c.id === siparis.cariId);
    if (activeCari) {
      const updatedC = { ...activeCari, borc: (activeCari.borc || 0) + siparis.genelToplam };
      const okCari = await writeData(`Cariler/${siparis.cariId}`, updatedC);
      if (!okCari) {
        Alert.alert('Uyarı', 'Fatura oluşturuldu ancak cari bakiye eşitlenemedi. (Bağlantı sorunu — bakiye sıraya alındı.)');
      }
    }

    // Cari Hareket Kaydı
    const moveId = generateInt32Id();
    const movePayload = {
      id: moveId,
      cariId: siparis.cariId,
      tarih: new Date().toISOString().split('T')[0],
      islemTuru: 'Satış Faturası',
      evrakNo: faturaPayload.faturaNo,
      borc: siparis.genelToplam,
      alacak: 0,
      aciklama: `${siparis.siparisNo} nolu sipariş faturaya dönüştürüldü.`
    };
    const okMove = await writeData(`CariHareketler/${moveId}`, movePayload);
    if (!okMove) {
      Alert.alert('Uyarı', 'Fatura hareketi eşitlenemedi. (Bağlantı sorunu — hareket sıraya alındı.)');
    }

    setIsDetailOpen(false);
    Alert.alert('Başarılı', 'Sipariş faturaya dönüştürüldü, cari borçlandırıldı.');
  };

  const renderItem = ({ item }: { item: any }) => (
    <TouchableOpacity 
      style={styles.siparisCard}
      onPress={() => { setSelectedSiparis(item); setIsDetailOpen(true); }}
    >
      <View style={{ flex: 1 }}>
        <Text style={styles.siparisCari}>{item.cariUnvan}</Text>
        <Text style={styles.siparisDet}>Sipariş No: {item.siparisNo}</Text>
        <Text style={styles.siparisTarih}>Tarih: {item.tarih} • Teslimat: {item.teslimatTarihi || '-'}</Text>
      </View>
      <View style={{ alignItems: 'flex-end' }}>
        <Text style={styles.siparisTutar}>{formatMoney(item.genelToplam)}</Text>
        <Text style={[styles.siparisDurum, { color: item.durum === 'Faturalandı' ? '#10B981' : (item.durum === 'İptal' ? '#EF4444' : '#F59E0B') }]}>
          {item.durum || 'Bekliyor'}
        </Text>
      </View>
    </TouchableOpacity>
  );

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.header}>
        <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
          <Text style={styles.headerTitle}>Sipariş Yönetimi</Text>
          <TouchableOpacity style={styles.addButton} onPress={handleOpenAdd}>
            <Plus color="#FFF" size={20} />
            <Text style={styles.addButtonText}>Yeni Sipariş</Text>
          </TouchableOpacity>
        </View>
        <View style={[styles.searchBox, { marginTop: 14 }]}>
          <Search color="#64748B" size={20} />
          <TextInput 
            style={styles.searchInput}
            placeholder="Cari veya sipariş no ara..."
            placeholderTextColor="#64748B"
            value={searchQuery}
            onChangeText={setSearchQuery}
          />
        </View>
      </View>

      {loading ? (
        <View style={styles.center}>
          <ActivityIndicator size="large" color="#0061FF" />
        </View>
      ) : (
        <FlatList 
          data={filteredSiparisler}
          keyExtractor={(item, index) => String(item.firebaseKey || item.id || index)}
          renderItem={renderItem}
          contentContainerStyle={styles.listContent}
          removeClippedSubviews={false}
          initialNumToRender={15}
          maxToRenderPerBatch={15}
          windowSize={7}
          ListEmptyComponent={
            <Text style={styles.emptyText}>Hiç sipariş bulunamadı.</Text>
          }
        />
      )}

      {/* Sipariş Ekle Form Modalı */}
      

      {/* Detay Modalı */}
      <Modal visible={isDetailOpen} animationType="fade" transparent={true}>
        <SafeAreaView style={styles.modalOverlay}>
          <View style={styles.modalContent}>
            {selectedSiparis && (
              <>
                <View style={styles.modalHeader}>
                  <Text style={styles.modalTitle}>Sipariş Detayı</Text>
                  <TouchableOpacity onPress={() => setIsDetailOpen(false)}>
                    <X color="#FFF" size={24} />
                  </TouchableOpacity>
                </View>

                <ScrollView contentContainerStyle={styles.formScroll}>
                  <View style={styles.detailCard}>
                    <Text style={styles.detailUnvan}>{selectedSiparis.cariUnvan}</Text>
                    <Text style={styles.detailGrup}>Sipariş No: {selectedSiparis.siparisNo}</Text>
                    <Text style={styles.detailGrup}>Tarih: {selectedSiparis.tarih} • Teslimat: {selectedSiparis.teslimatTarihi || '-'}</Text>
                    <Text style={[styles.detailAmt, { color: '#00FF87', textAlign: 'center', marginTop: 14 }]}>
                      {formatMoney(selectedSiparis.genelToplam)}
                    </Text>
                  </View>

                  {/* Kalemler */}
                  <View style={styles.detailSection}>
                    <Text style={styles.detailSectionTitle}>Sipariş Kalemleri</Text>
                    {(siparisDetaylarMap[selectedSiparis.id] || []).map((item, idx) => (
                      <View key={idx} style={styles.detailItemRow}>
                        <View style={{ flex: 1 }}>
                          <Text style={styles.detailItemName}>{item.stokAdi}</Text>
                          <Text style={styles.detailItemQty}>
                            {item.miktar} {item.birim || 'Adet'} x {formatMoney(item.birimFiyat)}
                          </Text>
                        </View>
                        <Text style={styles.detailItemTotal}>
                          {formatMoney(item.miktar * item.birimFiyat)}
                        </Text>
                      </View>
                    ))}
                  </View>

                  <View style={styles.detailSection}>
                    <Text style={styles.detailSectionTitle}>Sipariş Detayları</Text>
                    <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginBottom: 8 }}>
                      <Text style={{ color: '#94A3B8', fontSize: 12 }}>Öncelik:</Text>
                      <Text style={{ color: '#FFF', fontSize: 12 }}>{selectedSiparis.oncelik || 'Orta'}</Text>
                    </View>
                    <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginBottom: 8 }}>
                      <Text style={{ color: '#94A3B8', fontSize: 12 }}>Ödeme Bilgisi:</Text>
                      <Text style={{ color: '#FFF', fontSize: 12 }}>{selectedSiparis.odemeBilgisi || '-'}</Text>
                    </View>
                    <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginBottom: 8 }}>
                      <Text style={{ color: '#94A3B8', fontSize: 12 }}>Bağlantı Evrak No:</Text>
                      <Text style={{ color: '#FFF', fontSize: 12 }}>{selectedSiparis.baglantiEvrakNo || '-'}</Text>
                    </View>
                    {selectedSiparis.pdfNotlar ? (
                      <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginBottom: 8 }}>
                        <Text style={{ color: '#94A3B8', fontSize: 12 }}>PDF Notu:</Text>
                        <Text style={{ color: '#FFF', fontSize: 12 }}>{selectedSiparis.pdfNotlar}</Text>
                      </View>
                    ) : null}
                  </View>

                  <View style={styles.detailSection}>
                    <Text style={styles.detailSectionTitle}>Açıklama</Text>
                    <Text style={{ color: '#94A3B8', fontSize: 13 }}>{selectedSiparis.aciklama || 'Açıklama girilmemiş.'}</Text>
                  </View>

                  {selectedSiparis.durum !== 'Faturalandı' && (
                    <TouchableOpacity 
                      style={[styles.saveButton, { backgroundColor: '#F59E0B', marginBottom: 12 }]}
                      onPress={() => handleConvertToFatura(selectedSiparis)}
                    >
                      <ShoppingBag color="#FFF" size={20} />
                      <Text style={[styles.saveButtonText, { color: '#FFF' }]}>Satış Faturasına Dönüştür</Text>
                    </TouchableOpacity>
                  )}

                  <View style={styles.detailSection}>
                    <Text style={styles.detailSectionTitle}>Durum Güncelle</Text>
                    <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
                      {['Bekliyor', 'Onaylandı', 'Tamamlandı', 'İptal'].map(st => (
                        <TouchableOpacity
                          key={st}
                          style={[styles.toggleBtn, selectedSiparis.durum === st && styles.toggleBtnActive]}
                          onPress={() => handleUpdateDurum(selectedSiparis, st)}
                        >
                          <Text style={[styles.toggleBtnText, selectedSiparis.durum === st && styles.toggleBtnTextActive]}>{st}</Text>
                        </TouchableOpacity>
                      ))}
                    </View>
                  </View>

                  <TouchableOpacity 
                    style={[styles.saveButton, { backgroundColor: '#10B981', marginBottom: 12 }]}
                    onPress={() => handleShareSiparis(selectedSiparis)}
                  >
                    <Share2 color="#FFF" size={20} />
                    <Text style={[styles.saveButtonText, { color: '#FFF' }]}>Siparişi Paylaş</Text>
                  </TouchableOpacity>

                  <TouchableOpacity 
                    style={[styles.saveButton, { backgroundColor: '#0061FF', marginBottom: 12 }]}
                    onPress={() => handleOpenEdit(selectedSiparis)}
                  >
                    <Edit3 color="#FFF" size={20} />
                    <Text style={[styles.saveButtonText, { color: '#FFF' }]}>Siparişi Düzenle</Text>
                  </TouchableOpacity>

                  <TouchableOpacity 
                    style={[styles.saveButton, { backgroundColor: '#EF4444', marginBottom: 20 }]}
                    onPress={() => handleDeleteSiparis(selectedSiparis)}
                  >
                    <Trash2 color="#FFF" size={20} />
                    <Text style={[styles.saveButtonText, { color: '#FFF' }]}>Siparişi Sil</Text>
                  </TouchableOpacity>
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
  addButton: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#0061FF',
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 12,
  },
  addButtonText: {
    color: '#FFF',
    fontWeight: 'bold',
    fontSize: 12,
    marginLeft: 6,
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
  listContent: {
    padding: 20,
  },
  siparisCard: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: '#0F0F0F',
    borderRadius: 16,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    borderWidth: 1,
    padding: 16,
    marginBottom: 10,
  },
  siparisCari: {
    color: '#FFF',
    fontSize: 14,
    fontWeight: 'bold',
  },
  siparisDet: {
    color: '#94A3B8',
    fontSize: 11,
    marginTop: 2,
  },
  siparisTarih: {
    color: '#64748B',
    fontSize: 11,
    marginTop: 4,
  },
  siparisTutar: {
    color: '#FFF',
    fontSize: 14,
    fontWeight: '900',
  },
  siparisDurum: {
    fontSize: 10,
    fontWeight: 'bold',
    marginTop: 4,
    textTransform: 'uppercase',
  },
  emptyText: {
    color: '#64748B',
    fontSize: 12,
    textAlign: 'center',
    paddingVertical: 40,
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
  selectorText: {
    color: '#FFF',
    fontSize: 14,
    marginLeft: 10,
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
  detailAmt: {
    fontSize: 24,
    fontWeight: '900',
  },
  detailSection: {
    backgroundColor: '#161616',
    borderRadius: 20,
    borderColor: 'rgba(255,255,255,0.08)',
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
  itemCard: {
    backgroundColor: '#1E1E1E',
    borderRadius: 12,
    padding: 12,
    marginBottom: 10,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
  },
  itemCardHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.05)',
    paddingBottom: 8,
    marginBottom: 8,
  },
  itemCardTitle: {
    color: '#FFF',
    fontSize: 13,
    fontWeight: 'bold',
    flex: 1,
  },
  itemDeleteBtn: {
    padding: 4,
  },
  itemCardBody: {},
  itemInputRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  itemInputCol: {},
  itemColLabel: {
    color: '#94A3B8',
    fontSize: 11,
    marginBottom: 4,
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
    fontSize: 12,
    marginLeft: 4,
  },
  itemInputFlat: {
    backgroundColor: '#2A2A2A',
    borderRadius: 8,
    color: '#FFF',
    paddingHorizontal: 8,
    paddingVertical: 6,
    fontSize: 12,
    flex: 1,
  },
  kdvChipGroup: {
    flexDirection: 'row',
    backgroundColor: 'rgba(255,255,255,0.05)',
    borderRadius: 8,
    overflow: 'hidden',
    alignSelf: 'flex-start',
  },
  kdvChipItem: {
    paddingHorizontal: 10,
    paddingVertical: 6,
  },
  kdvChipItemActive: {
    backgroundColor: '#0061FF',
  },
  kdvChipItemText: {
    color: '#94A3B8',
    fontSize: 11,
    fontWeight: 'bold',
  },
  kdvChipItemTextActive: {
    color: '#FFF',
  }
});
