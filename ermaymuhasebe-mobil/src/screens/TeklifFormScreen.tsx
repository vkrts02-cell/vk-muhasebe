import React, { useState, useEffect } from 'react';
import { KeyboardAvoidingView, Platform,  StyleSheet, Text, View, SafeAreaView, FlatList, TextInput, ActivityIndicator, TouchableOpacity, Modal, ScrollView, Alert, Share, PanResponder  } from 'react-native';
import { Search, FileKey2, Plus, X, Save, Edit3, Trash2, Calendar, User, ShoppingBag, Share2, ChevronDown } from 'lucide-react-native';
import { subscribeToPath, writeData, readData, mapAppToDatabase } from '../services/firebase';
import { generateInt32Id } from '../utils/IdGenerator';
import { generateReportPdf } from '../services/pdfService';
import { KeyboardDoneAccessory, KEYBOARD_ACCESSORY_ID } from '../components/KeyboardDoneAccessory';

const BIRIM_LISTESI = ['Adet', 'Kg', 'Mt', 'M2'];

const formatMoney = (val: number) => {
  return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' }).format(val);
};

export default function TeklifFormScreen({ route, navigation }: any) {
  const [teklifler, setTeklifler] = useState<any[]>([]);
  const [cariler, setCariler] = useState<any[]>([]);
  const [stoklar, setStoklar] = useState<any[]>([]);
  const [teklifDetaylarMap, setTeklifDetaylarMap] = useState<Record<string, any[]>>({});
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');

  // Modals state
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [isDetailOpen, setIsDetailOpen] = useState(false);
  const [isCariOverlayOpen, setIsCariOverlayOpen] = useState(false);
  const [isStokOverlayOpen, setIsStokOverlayOpen] = useState(false);
  
  // Saving guard to prevent double-tap double-save
  const [isSaving, setIsSaving] = useState(false);
  const [selectedItemIndexForBirim, setSelectedItemIndexForBirim] = useState<number | null>(null);
  
  const [selectedTeklif, setSelectedTeklif] = useState<any | null>(null);
  const openedFromDashboardRef = React.useRef(false);

  

  const handleCloseForm = () => {
    navigation.goBack();
  };

  

  const handleShareTeklif = async (teklif: any) => {
    try {
      const detaylar = teklifDetaylarMap[teklif.id] || [];
      const cari = cariler.find(c => c.id === teklif.cariId) || { unvan: teklif.cariUnvan };
      
      const mappedTeklif = mapAppToDatabase('Teklifler', teklif);
      const mappedDetaylar = detaylar.map(d => mapAppToDatabase('TeklifDetaylar', d));
      const mappedCari = mapAppToDatabase('Cariler', cari);

      const payload = {
        Teklif: mappedTeklif,
        Detaylar: mappedDetaylar,
        Cari: mappedCari
      };

      const reportFileName = `Teklif_${teklif.teklifNo}.pdf`;
      await generateReportPdf('teklif', payload, reportFileName);
    } catch (error) {
      console.error('Paylaşım hatası:', error);
      Alert.alert('Hata', 'Teklif PDF\'i paylaşılırken bir sorun oluştu.');
    }
  };

  // Form fields state
  const [editingId, setEditingId] = useState<number | null>(null);
  const [teklifNo, setTeklifNo] = useState('');
  const [tarih, setTarih] = useState(new Date().toISOString().split('T')[0]);
  const [gecerlilikTarihi, setGecerlilikTarihi] = useState(new Date().toISOString().split('T')[0]);
  const [selectedCari, setSelectedCari] = useState<any | null>(null);
  const [aciklama, setAciklama] = useState('');
  const [durum, setDurum] = useState<'Bekliyor' | 'Siparişleşti' | 'Reddedildi'>('Bekliyor');
  const [odemeBilgisi, setOdemeBilgisi] = useState('');
  
  // Items state
  const [items, setItems] = useState<any[]>([]);
  
  // Search queries
  const [cariSearch, setCariSearch] = useState('');
  const [stokSearch, setStokSearch] = useState('');

  useEffect(() => {
    const unsubTeklif = subscribeToPath('Teklifler', (data) => {
      if (!data) {
        setTeklifler([]);
      } else {
        const list = Array.isArray(data) 
          ? data.filter(Boolean) 
          : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setTeklifler(list.filter(t => !t.isDeleted).sort((a, b) => new Date(b.tarih).getTime() - new Date(a.tarih).getTime()));
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

    const unsubDetaylar = subscribeToPath('TeklifDetaylar', (data) => {
      if (data) {
        setTeklifDetaylarMap(data);
      }
      setLoading(false);
    });

    return () => {
      unsubTeklif();
      unsubCariler();
      unsubStoklar();
      unsubDetaylar();
    };
  }, []);

  const filteredTeklifler = teklifler.filter(t => 
    (t.cariUnvan || '').toLocaleLowerCase('tr-TR').includes(searchQuery.toLocaleLowerCase('tr-TR')) ||
    (t.teklifNo || '').toLocaleLowerCase('tr-TR').includes(searchQuery.toLocaleLowerCase('tr-TR'))
  );

  const generateTeklifNo = () => {
    const random = Math.floor(100000 + Math.random() * 900000);
    return `TKF-${random}`;
  };

  const resetForm = () => {
    setEditingId(null);
    setTeklifNo(generateTeklifNo());
    setTarih(new Date().toISOString().split('T')[0]);
    setGecerlilikTarihi(new Date().toISOString().split('T')[0]);
    setSelectedCari(null);
    setAciklama('');
    setDurum('Bekliyor');
    setOdemeBilgisi('');
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

    const teklifData: any = {
      id: currentId,
      teklifNo: teklifNo.trim(),
      tarih,
      gecerlilikTarihi,
      cariId: selectedCari.id,
      cariUnvan: selectedCari.unvan,
      durum,
      odemeBilgisi: odemeBilgisi.trim(),
      araToplam: subtotal,
      kdvToplam: totalKdv,
      genelToplam: grandTotal,
      aciklama: aciklama.trim(),
      isDeleted: false,
    };
    if (!editingId) {
      teklifData.kayitTarihi = new Date().toISOString();
    }

    setIsSaving(true);
    // 1. Save Teklif
    const tSuccess = await writeData(`Teklifler/${currentId}`, teklifData);
    if (!tSuccess) {
      Alert.alert('Hata', 'Teklif kaydedilirken hata oluştu.');
      setIsSaving(false);
      return;
    }

    // 2. Save TeklifDetaylar
    const detayItems = items.map((item, idx) => ({
      id: idx + 1,
      teklifId: currentId,
      stokId: item.stokId,
      stokAdi: item.stokAdi,
      miktar: item.miktar,
      topMiktari: item.miktar,
      birim: item.birim,
      birimFiyat: item.birimFiyat,
      tutar: item.miktar * item.birimFiyat,
      kdvOrani: item.kdvOrani,
      aciklama: item.aciklama || '',
      paraBirimi: 'TL',
    }));
    const okDetay = await writeData(`TeklifDetaylar/${currentId}`, detayItems);
    if (!okDetay) {
      Alert.alert('Uyarı', 'Teklif kaydedildi ancak detaylar eşitlenemedi. (Bağlantı sorunu — detaylar sıraya alındı.)');
    }

    setIsFormOpen(false);
    resetForm();
    Alert.alert('Başarılı', 'Teklif başarıyla kaydedildi.');
    setIsSaving(false);
  };

  const handleOpenEdit = async (teklif: any) => {
    try {
      setEditingId(teklif.id);
      setTeklifNo(teklif.teklifNo || '');
      setTarih(teklif.tarih || new Date().toISOString().split('T')[0]);
      setGecerlilikTarihi(teklif.gecerlilikTarihi || new Date().toISOString().split('T')[0]);
      setSelectedCari(cariler.find(c => c.id === teklif.cariId) || { id: teklif.cariId, unvan: teklif.cariUnvan });
      setAciklama(teklif.aciklama || '');
      setDurum(teklif.durum || 'Bekliyor');
      setOdemeBilgisi(teklif.odemeBilgisi || '');

      let detaylar = teklifDetaylarMap[teklif.id] || [];
      if (detaylar.length === 0) {
        const raw = await readData(`TeklifDetaylar/${teklif.id}`) || [];
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
      })));

      setIsDetailOpen(false);
      setIsFormOpen(true);
    } catch (err) {
      console.error('Teklif düzenleme hatası:', err);
      Alert.alert('Hata', 'Teklif düzenleme modunda yüklenemedi.');
    }
  };

  const handleDeleteTeklif = (teklif: any) => {
    Alert.alert(
      'Silme Onayı',
      `${teklif.teklifNo} numaralı teklifi silmek istediğinize emin misiniz?`,
      [
        { text: 'Vazgeç', style: 'cancel' },
        {
          text: 'Sil',
          style: 'destructive',
          onPress: async () => {
            try {
              const ok = await writeData(`Teklifler/${teklif.id}`, { ...teklif, isDeleted: true });
              if (!ok) {
                Alert.alert('Hata', 'Teklif silinemedi. (Bağlantı sorunu — kayıt eşitlenemedi.)');
                return;
              }
              setIsDetailOpen(false);
              Alert.alert('Başarılı', 'Teklif başarıyla silindi.');
            } catch (err) {
              console.error('Teklif silme hatası:', err);
              Alert.alert('Hata', 'Teklif silinirken bir hata oluştu.');
            }
          }
        }
      ]
    );
  };

  // Convert Quote to Order
  const handleConvertToSiparis = async (teklif: any) => {
    const detaylar = teklifDetaylarMap[teklif.id] || [];
    if (detaylar.length === 0) {
      Alert.alert('Hata', 'Teklifin detay kalemleri bulunamadı.');
      return;
    }

    // Siparişleşti durumuna güncelle
    const updatedTeklif = { ...teklif, durum: 'Siparişleşti' };
    const okTeklif = await writeData(`Teklifler/${teklif.id}`, updatedTeklif);
    if (!okTeklif) {
      Alert.alert('Hata', 'Teklif durumu güncellenemedi. (Bağlantı sorunu — kayıt sıraya alındı.)');
      return;
    }

    // Siparişler düğümüne ekle
    const nextSiparisId = generateInt32Id();
    const siparisPayload = {
      id: nextSiparisId,
      siparisNo: `SIP-TKF-${teklif.teklifNo.split('-')[1] || generateInt32Id().toString().substring(5)}`,
      tarih: new Date().toISOString().split('T')[0],
      teslimatTarihi: new Date().toISOString().split('T')[0],
      cariId: teklif.cariId,
      cariUnvan: teklif.cariUnvan,
      durum: 'Bekliyor',
      odemeBilgisi: teklif.odemeBilgisi || '',
      araToplam: teklif.araToplam || teklif.brutToplam || 0,
      kdvToplam: teklif.kdvToplam || 0,
      genelToplam: teklif.genelToplam || 0,
      aciklama: `${teklif.teklifNo} nolu teklif kabul edilerek otomatik siparişe dönüştürüldü.`,
      isDeleted: false,
    };
    const okSiparis = await writeData(`Siparisler/${nextSiparisId}`, siparisPayload);
    if (!okSiparis) {
      Alert.alert('Hata', 'Teklif güncellendi ancak sipariş oluşturulamadı. (Bağlantı sorunu — işlem sıraya alındı.)');
      return;
    }

    // SiparisDetaylar tablosuna detayları ekle
    const detayItems = detaylar.map((item, idx) => ({
      id: idx + 1,
      siparisId: nextSiparisId,
      stokId: item.stokId,
      stokAdi: item.stokAdi,
      miktar: item.miktar,
      birim: item.birim || 'Adet',
      birimFiyat: item.birimFiyat,
      tutar: item.tutar || item.toplamTutar || (item.miktar * item.birimFiyat),
      kdvOrani: item.kdvOrani || 20,
    }));
    const okSiparisDetay = await writeData(`SiparisDetaylar/${nextSiparisId}`, detayItems);
    if (!okSiparisDetay) {
      Alert.alert('Uyarı', 'Sipariş oluşturuldu ancak detaylar eşitlenemedi. (Bağlantı sorunu — detaylar sıraya alındı.)');
    }

    setIsDetailOpen(false);
    Alert.alert('Başarılı', 'Teklif başarıyla Siparişe dönüştürüldü.');
  };

  const renderItem = ({ item }: { item: any }) => (
    <TouchableOpacity 
      style={styles.teklifCard}
      onPress={() => { setSelectedTeklif(item); setIsDetailOpen(true); }}
    >
      <View style={{ flex: 1 }}>
        <Text style={styles.teklifCari}>{item.cariUnvan}</Text>
        <Text style={styles.teklifDet}>Teklif No: {item.teklifNo}</Text>
        <Text style={styles.teklifTarih}>Tarih: {item.tarih} • Geçerlilik: {item.gecerlilikTarihi || '-'}</Text>
      </View>
      <View style={{ alignItems: 'flex-end' }}>
        <Text style={styles.teklifTutar}>{formatMoney(item.genelToplam)}</Text>
        <Text style={[styles.teklifDurum, { color: item.durum === 'Siparişleşti' ? '#10B981' : (item.durum === 'Reddedildi' ? '#EF4444' : '#F59E0B') }]}>
          {item.durum || 'Bekliyor'}
        </Text>
      </View>
    </TouchableOpacity>
  );

  return (
    <SafeAreaView style={styles.container}>
      <KeyboardAvoidingView 
        behavior={Platform.OS === 'ios' ? 'height' : undefined} 
        keyboardVerticalOffset={Platform.OS === 'ios' ? 44 : 0}
        style={{ flex: 1 }}
      >
          <View style={styles.modalContent}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>Yeni Teklif Oluştur</Text>
              <TouchableOpacity onPress={handleCloseForm} style={styles.closeButton} hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}>
                <X color="#FFF" size={24} />
              </TouchableOpacity>
            </View>

            <ScrollView 
              contentContainerStyle={styles.formScroll} 
              keyboardShouldPersistTaps="handled"
              automaticallyAdjustKeyboardInsets={true}
              showsVerticalScrollIndicator={false}
            >
              <TouchableOpacity style={styles.selectorCard} onPress={() => setIsCariOverlayOpen(true)}>
                <User color="#0061FF" size={20} />
                <Text style={styles.selectorText}>
                  {selectedCari ? selectedCari.unvan : 'Cari Kart Seçin *'}
                </Text>
              </TouchableOpacity>

              <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                <View style={[styles.inputGroup, { width: '48%' }]}>
                  <Text style={styles.inputLabel}>Teklif No</Text>
                  <TextInput 
                    style={styles.input} 
                    value={teklifNo}
                    onChangeText={setTeklifNo}
                  />
                </View>
                <View style={[styles.inputGroup, { width: '48%' }]}>
                  <Text style={styles.inputLabel}>Durum</Text>
                  <View style={styles.toggleRow}>
                    {['Bekliyor', 'Reddedildi'].map((st: any) => (
                      <TouchableOpacity 
                        key={st}
                        style={[styles.toggleBtn, durum === st && styles.toggleBtnActive]}
                        onPress={() => setDurum(st)}
                      >
                        <Text style={[styles.toggleBtnText, durum === st && styles.toggleBtnTextActive]}>{st}</Text>
                      </TouchableOpacity>
                    ))}
                  </View>
                </View>
              </View>

              <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                <View style={[styles.inputGroup, { width: '48%' }]}>
                  <Text style={styles.inputLabel}>Teklif Tarihi</Text>
                  <TextInput 
                    style={styles.input} 
                    placeholder="YYYY-MM-DD" 
                    placeholderTextColor="#64748B"
                    value={tarih}
                    onChangeText={setTarih}
                  />
                </View>
                <View style={[styles.inputGroup, { width: '48%' }]}>
                  <Text style={styles.inputLabel}>Geçerlilik Tarihi</Text>
                  <TextInput 
                    style={styles.input} 
                    placeholder="YYYY-MM-DD" 
                    placeholderTextColor="#64748B"
                    value={gecerlilikTarihi}
                    onChangeText={setGecerlilikTarihi}
                  />
                </View>
              </View>

              <View style={styles.inputGroup}>
                <Text style={styles.inputLabel}>Ödeme Bilgisi</Text>
                <TextInput 
                  style={styles.input} 
                  placeholder="örn. 30 Gün Vade veya Peşin Ödeme..." 
                  placeholderTextColor="#64748B"
                  value={odemeBilgisi}
                  onChangeText={setOdemeBilgisi}
                />
              </View>

              {/* Teklif Kalemleri */}
              <View style={styles.itemsSection}>
                <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
                  <Text style={styles.itemsTitle}>Teklif Kalemleri</Text>
                  <TouchableOpacity style={styles.addItemBtn} onPress={() => setIsStokOverlayOpen(true)} hitSlop={{ top: 8, bottom: 8, left: 8, right: 8 }}>
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
                        <Text style={styles.itemCardTitle}>{item.stokAdi}</Text>
                        <TouchableOpacity onPress={() => handleRemoveItem(index)} style={styles.itemDeleteBtn}>
                          <Trash2 color="#EF4444" size={16} />
                        </TouchableOpacity>
                      </View>
                      
                      <View style={styles.itemCardBody}>
                        {/* Miktar & Birim & Fiyat Satırı */}
                        <View style={styles.itemInputRow}>
                          <View style={[styles.itemInputCol, { flex: 1, marginRight: 8 }]}>
                            <Text style={styles.itemColLabel}>Miktar</Text>
                            <View style={styles.quantityContainer}>
                              <TextInput 
                                style={styles.itemInputFlat}
                                keyboardType="numeric"
                                inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                                placeholder="0"
                                value={String(item.miktar)}
                                onChangeText={(val) => handleItemChange(index, 'miktar', val)}
                              />
                              <TouchableOpacity 
                                style={styles.unitPickerBtn}
                                onPress={() => setSelectedItemIndexForBirim(index)}
                                activeOpacity={0.7}
                                hitSlop={{ top: 8, bottom: 8, left: 8, right: 8 }}
                              >
                                <Text style={styles.unitPickerBtnText}>{item.birim || 'Adet'}</Text>
                                <ChevronDown color="#94A3B8" size={13} style={{ marginLeft: 3 }} />
                              </TouchableOpacity>
                            </View>
                          </View>
                          
                          <View style={[styles.itemInputCol, { flex: 1.5 }]}>
                            <Text style={styles.itemColLabel}>Birim Fiyat (₺)</Text>
                            <TextInput 
                              style={styles.itemInputFlat}
                              keyboardType="numeric"
                              inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                              placeholder="0.00"
                              value={String(item.birimFiyat)}
                              onChangeText={(val) => handleItemChange(index, 'birimFiyat', val)}
                            />
                          </View>
                        </View>

                        {/* KDV ve Açıklama Satırı */}
                        <View style={[styles.itemInputRow, { marginTop: 12 }]}>
                          <View style={[styles.itemInputCol, { flex: 1.2, marginRight: 8 }]}>
                            <Text style={styles.itemColLabel}>KDV</Text>
                            <View style={{
                              backgroundColor: 'rgba(255,255,255,0.06)',
                              borderRadius: 8,
                              height: 36,
                              alignItems: 'center',
                              justifyContent: 'center',
                              borderWidth: 1,
                              borderColor: 'rgba(255,255,255,0.08)'
                            }}>
                              <Text style={{ color: '#10B981', fontSize: 13, fontWeight: 'bold' }}>%{item.kdvOrani || 0}</Text>
                            </View>
                          </View>
                          
                          <View style={[styles.itemInputCol, { flex: 1.8 }]}>
                            <Text style={styles.itemColLabel}>Kalem Açıklaması</Text>
                            <TextInput
                              style={styles.itemInputFlat}
                              placeholder="Kalem notu..."
                              placeholderTextColor="#64748B"
                              value={item.aciklama || ''}
                              onChangeText={(val) => handleItemChange(index, 'aciklama', val)}
                            />
                          </View>
                        </View>
                      </View>
                    </View>
                  ))
                )}
              </View>

              <View style={styles.inputGroup}>
                <Text style={styles.inputLabel}>Açıklama</Text>
                <TextInput 
                  style={[styles.input, { height: 60 }]} 
                  placeholder="Teklif şartları, notlar..." 
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
                  <Text style={styles.summaryVal}>{formatMoney(calculateSubtotal())}</Text>
                </View>
                <View style={styles.summaryRow}>
                  <Text style={styles.summaryLabel}>KDV Toplamı:</Text>
                  <Text style={styles.summaryVal}>{formatMoney(calculateTotalKdv())}</Text>
                </View>
                <View style={[styles.summaryRow, { borderTopWidth: 1, borderTopColor: 'rgba(255,255,255,0.05)', paddingTop: 8, marginTop: 8 }]}>
                  <Text style={[styles.summaryLabel, { fontWeight: 'bold', color: '#FFF' }]}>Genel Toplam:</Text>
                  <Text style={[styles.summaryVal, { fontWeight: 'bold', color: '#00FF87' }]}>{formatMoney(calculateGrandTotal())}</Text>
                </View>
              </View>

              <TouchableOpacity style={[styles.saveButton, isSaving && { opacity: 0.7 }]} disabled={isSaving} onPress={handleSave}>
                {isSaving ? <ActivityIndicator color="#FFF" size="small" /> : <Save color="#FFF" size={20} />}
                <Text style={styles.saveButtonText}>{isSaving ? 'Kaydediliyor...' : 'Teklifi Kaydet'}</Text>
              </TouchableOpacity>
            </ScrollView>

            {/* Cari Seçici Absolute Overlay (Nested Modal yerine) */}
            {isCariOverlayOpen && (
              <View style={styles.absoluteOverlay}>
                <View style={styles.modalHeader}>
                  <Text style={styles.modalTitle}>Cari Kart Seçin</Text>
                  <TouchableOpacity onPress={() => setIsCariOverlayOpen(false)}>
                    <X color="#FFF" size={24} />
                  </TouchableOpacity>
                </View>
                <View style={[styles.searchBox, { marginBottom: 16 }]}>
                  <Search color="#64748B" size={20} />
                  <TextInput 
                    style={styles.searchInput}
                    placeholder="Cari ara..."
                    placeholderTextColor="#64748B"
                    value={cariSearch}
                    onChangeText={setCariSearch}
                  />
                </View>
                <FlatList initialNumToRender={20} maxToRenderPerBatch={20} windowSize={5} 
                  data={cariler.filter(c => (c.unvan || '').toLocaleLowerCase('tr-TR').includes(cariSearch.toLocaleLowerCase('tr-TR')))}
                  keyExtractor={(item, idx) => item.id?.toString() || idx.toString()}
                  renderItem={({ item }) => (
                    <TouchableOpacity 
                      style={styles.selectorItem}
                      onPress={() => { setSelectedCari(item); setIsCariOverlayOpen(false); }}
                    >
                      <Text style={styles.selectorItemText}>{item.unvan}</Text>
                      <Text style={styles.selectorItemSub}>{item.grup}</Text>
                    </TouchableOpacity>
                  )}
                />
              </View>
            )}

            {/* Stok Seçici Absolute Overlay (Nested Modal yerine) */}
            {isStokOverlayOpen && (
              <View style={styles.absoluteOverlay}>
                <View style={styles.modalHeader}>
                  <Text style={styles.modalTitle}>Stok Seçin</Text>
                  <TouchableOpacity onPress={() => setIsStokOverlayOpen(false)}>
                    <X color="#FFF" size={24} />
                  </TouchableOpacity>
                </View>
                <View style={[styles.searchBox, { marginBottom: 16 }]}>
                  <Search color="#64748B" size={20} />
                  <TextInput 
                    style={styles.searchInput}
                    placeholder="Stok ara..."
                    placeholderTextColor="#64748B"
                    autoFocus={false}
                    value={stokSearch}
                    onChangeText={setStokSearch}
                  />
                </View>
                <FlatList initialNumToRender={20} maxToRenderPerBatch={20} windowSize={5} 
                  data={stoklar.filter(s => (s.stokAdi || '').toLocaleLowerCase('tr-TR').includes(stokSearch.toLocaleLowerCase('tr-TR')))}
                  keyExtractor={(item, idx) => item.id?.toString() || idx.toString()}
                  renderItem={({ item }) => (
                    <TouchableOpacity 
                      style={styles.selectorItem}
                      onPress={() => handleAddItem(item)}
                    >
                      <Text style={styles.selectorItemText}>{item.stokAdi}</Text>
                      <Text style={styles.selectorItemSub}>Miktar: {item.miktar} {item.birim} • Fiyat: {formatMoney(item.satisFiyati)}</Text>
                    </TouchableOpacity>
                  )}
                />
              </View>
            )}
          </View>
        </KeyboardAvoidingView>

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
      {/* iOS Sayısal Klavye Bitti Butonu */}
      <KeyboardDoneAccessory />
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
  teklifCard: {
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
  teklifCari: {
    color: '#FFF',
    fontSize: 14,
    fontWeight: 'bold',
  },
  teklifDet: {
    color: '#94A3B8',
    fontSize: 11,
    marginTop: 2,
  },
  teklifTarih: {
    color: '#64748B',
    fontSize: 11,
    marginTop: 4,
  },
  teklifTutar: {
    color: '#FFF',
    fontSize: 14,
    fontWeight: '900',
  },
  teklifDurum: {
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
  kdvChip: {
    paddingHorizontal: 10,
    paddingVertical: 6,
  },
  kdvChipActive: {
    backgroundColor: '#0061FF',
  },
  kdvChipText: {
    color: '#64748B',
    fontSize: 12,
    fontWeight: 'bold',
  },
  kdvChipTextActive: {
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
