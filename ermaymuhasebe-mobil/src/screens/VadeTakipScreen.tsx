import React, { useState, useEffect } from 'react';
import { StyleSheet, Text, View, SafeAreaView, FlatList, TextInput, ActivityIndicator, TouchableOpacity, Modal, ScrollView, Alert, Image } from 'react-native';
import { Search, CalendarDays, Plus, X, Save, Edit3, Trash2, Calendar, User, Clock, AlertTriangle, CheckCircle2, Users, RefreshCw, Camera, ChevronDown, Download, Printer } from 'lucide-react-native';
import { generateInt32Id } from '../utils/IdGenerator';
import { subscribeToPath, writeData, mapAppToDatabase } from '../services/firebase';
import {
  KeyboardDoneAccessory,
  KEYBOARD_ACCESSORY_ID,
} from '../components/KeyboardDoneAccessory';
import * as ImagePicker from 'expo-image-picker';
import * as FileSystem from 'expo-file-system/legacy';
import * as Sharing from 'expo-sharing';
import { generateReportPdf } from '../services/pdfService';

const formatMoney = (val: number) => {
  return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' }).format(val);
};

export default function VadeTakipScreen() {
  const [faturalar, setFaturalar] = useState<any[]>([]);
  const [cekler, setCekler] = useState<any[]>([]);
  const [senetler, setSenetler] = useState<any[]>([]);
  const [cariler, setCariler] = useState<any[]>([]);
  const [cariHareketler, setCariHareketler] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  
  // Tabs
  const [timeFilter, setTimeFilter] = useState<'tümü' | 'bugün' | 'bu-hafta' | 'bu-ay' | 'gecikmiş'>('tümü');

  // Modals state
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [isFilterOverlayOpen, setIsFilterOverlayOpen] = useState(false);
  const [isCariOverlayOpen, setIsCariOverlayOpen] = useState(false);
  const [isDetailOpen, setIsDetailOpen] = useState(false);
  const [isCiroCariOverlayOpen, setIsCiroCariOverlayOpen] = useState(false);
  const [isStatusModalOpen, setIsStatusModalOpen] = useState(false);
  
  const [selectedEvrak, setSelectedEvrak] = useState<any | null>(null);
  const [isPostponeOpen, setIsPostponeOpen] = useState(false);
  const [newVadeDate, setNewVadeDate] = useState(new Date().toISOString().split('T')[0]);
  const [currentYear, setCurrentYear] = useState(new Date().getFullYear());
  const [currentMonth, setCurrentMonth] = useState(new Date().getMonth());

  // Form fields state
  const [evrakTipi, setEvrakTipi] = useState<'Cek' | 'Senet'>('Cek');
  const [evrakTuru, setEvrakTuru] = useState<'Alınan' | 'Verilen'>('Alınan');
  const [selectedCari, setSelectedCari] = useState<any | null>(null);
  const [portfoyNo, setPortfoyNo] = useState('');
  const [seriNo, setSeriNo] = useState('');
  const [tutar, setTutar] = useState('');
  const [borclu, setBorclu] = useState('');
  const [vadeTarihi, setVadeTarihi] = useState(new Date().toISOString().split('T')[0]);
  const [banka, setBanka] = useState('');
  const [sube, setSube] = useState('');
  const [hesapNo, setHesapNo] = useState('');
  const [aciklama, setAciklama] = useState('');
  const [gorselYoluOn, setGorselYoluOn] = useState('');
  const [gorselYoluArka, setGorselYoluArka] = useState('');
  
  // Status edit
  const [newStatus, setNewStatus] = useState('Ödendi');
  const [selectedCiroCari, setSelectedCiroCari] = useState<any | null>(null);

  // Search queries
  const [cariSearch, setCariSearch] = useState('');
  const [ciroSearch, setCiroSearch] = useState('');

  const pickImage = async (setter: (uri: string) => void) => {
    try {
      const { status } = await ImagePicker.requestMediaLibraryPermissionsAsync();
      if (status !== 'granted') {
        Alert.alert('İzin Gerekli', 'Galeriye erişim izni vermeniz gerekmektedir.');
        return;
      }
      const result = await ImagePicker.launchImageLibraryAsync({
        mediaTypes: ImagePicker.MediaTypeOptions.Images,
        allowsEditing: true,
        quality: 0.7,
        base64: true,
      });
      if (!result.canceled && result.assets && result.assets.length > 0) {
        const asset = result.assets[0];
        const base64Data = `data:${asset.mimeType || 'image/jpeg'};base64,${asset.base64}`;
        setter(base64Data);
      }
    } catch (error) {
      console.error('Görsel seçilemedi:', error);
      Alert.alert('Hata', 'Görsel seçilirken bir sorun oluştu.');
    }
  };

  useEffect(() => {
    const unsubFaturalar = subscribeToPath('Faturalar', (data) => {
      if (data) {
        const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setFaturalar(list.filter(f => !f.isDeleted));
      }
    });

    const unsubCekler = subscribeToPath('Cekler', (data) => {
      if (data) {
        const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setCekler(list.filter(c => !c.isDeleted));
      }
    });

    const unsubSenetler = subscribeToPath('Senetler', (data) => {
      if (data) {
        const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setSenetler(list.filter(s => !s.isDeleted));
      }
    });

    const unsubCariler = subscribeToPath('Cariler', (data) => {
      if (data) {
        const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setCariler(list.filter(c => !c.isDeleted));
      }
    });

    const unsubHareketler = subscribeToPath('CariHareketler', (data) => {
      if (data) {
        const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setCariHareketler(list.filter(h => !h.isDeleted));
      }
      setLoading(false);
    });

    return () => {
      unsubFaturalar();
      unsubCekler();
      unsubSenetler();
      unsubCariler();
      unsubHareketler();
    };
  }, []);

  const getKalanGun = (dateStr: string) => {
    if (!dateStr) return 0;
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const target = new Date(dateStr);
    if (isNaN(target.getTime())) return 0;
    target.setHours(0, 0, 0, 0);
    const diff = target.getTime() - today.getTime();
    return Math.round(diff / (1000 * 60 * 60 * 24));
  };

  const getKalanGunText = (dateStr: string) => {
    const days = getKalanGun(dateStr);
    if (days < 0) return `${Math.abs(days)} GÜN GECİKTİ`;
    if (days === 0) return 'BUGÜN';
    return `${days} Gün Kaldı`;
  };

  const getVadeItems = () => {
    const items: any[] = [];

    // Faturalar
    faturalar.forEach(f => {
      const kalan = (f.genelToplam || 0) - (f.odenen || 0);
      if (kalan > 0) {
        const isIncoming = f.tur === 'Satış' || f.tur === 'Satis';
        items.push({
          id: f.id,
          type: 'Fatura',
          title: f.cariUnvan,
          subtitle: `${isIncoming ? 'Alacak' : 'Borç'} (Fatura) • ${f.faturaNo}`,
          vadeTarihi: f.vadeTarihi || f.tarih,
          tutar: kalan,
          isIncoming,
          raw: f,
        });
      }
    });

    // Cekler
    cekler.forEach(c => {
      if (c.durum === 'Portföyde' || c.durum === 'Portfoyde' || c.cekTuru === 'Verilen' || c.type === 'Verilen') {
        items.push({
          id: c.id,
          type: 'Cek',
          title: c.cariUnvan || c.borclu || 'Çek Evrakı',
          subtitle: `${c.cekTuru === 'Verilen' || c.type === 'Verilen' ? 'Borç (Çek)' : 'Alacak (Çek)'} • Portföy No: ${c.portfoyNo || '-'} • Durum: ${c.durum}`,
          vadeTarihi: c.vadeTarihi,
          tutar: c.tutar,
          isIncoming: c.cekTuru !== 'Verilen' && c.type !== 'Verilen',
          raw: c,
        });
      }
    });

    // Senetler
    senetler.forEach(s => {
      if (s.durum === 'Portföyde' || s.durum === 'Portfoyde' || s.senetTuru === 'Verilen' || s.type === 'Verilen') {
        items.push({
          id: s.id,
          type: 'Senet',
          title: s.cariUnvan || s.borclu || 'Senet Evrakı',
          subtitle: `${s.senetTuru === 'Verilen' || s.type === 'Verilen' ? 'Borç (Senet)' : 'Alacak (Senet)'} • Portföy No: ${s.portfoyNo || '-'} • Durum: ${s.durum}`,
          vadeTarihi: s.vadeTarihi,
          tutar: s.tutar,
          isIncoming: s.senetTuru !== 'Verilen' && s.type !== 'Verilen',
          raw: s,
        });
      }
    });

    // Apply Time Filters
    return items.filter(item => {
      const remainingDays = getKalanGun(item.vadeTarihi);
      
      if (timeFilter === 'bugün' && remainingDays !== 0) return false;
      if (timeFilter === 'bu-hafta') {
        const today = new Date();
        today.setHours(0, 0, 0, 0);
        const dayOfWeek = today.getDay();
        const diffToMonday = dayOfWeek === 0 ? 6 : dayOfWeek - 1;
        const startOfWeek = new Date(today);
        startOfWeek.setDate(today.getDate() - diffToMonday);
        const endOfWeek = new Date(startOfWeek);
        endOfWeek.setDate(startOfWeek.getDate() + 7);
        const d = new Date(item.vadeTarihi);
        d.setHours(0, 0, 0, 0);
        if (d < startOfWeek || d >= endOfWeek) return false;
      }
      if (timeFilter === 'bu-ay') {
        const d = new Date(item.vadeTarihi);
        const today = new Date();
        if (d.getMonth() !== today.getMonth() || d.getFullYear() !== today.getFullYear()) return false;
      }
      if (timeFilter === 'gecikmiş' && remainingDays >= 0) return false;

      // Apply Search Filter
      return (item.title || '').toLocaleLowerCase('tr-TR').includes(searchQuery.toLocaleLowerCase('tr-TR')) ||
             (item.subtitle || '').toLocaleLowerCase('tr-TR').includes(searchQuery.toLocaleLowerCase('tr-TR'));
    }).sort((a, b) => new Date(a.vadeTarihi).getTime() - new Date(b.vadeTarihi).getTime());
  };

  const handleExportExcel = async () => {
    try {
      const items = getVadeItems();
      let csv = "\uFEFFEvrak Turu,Cari / Muhatap,Detay,Vade Tarihi,Kalan Gun,Tutar\n";
      items.forEach(item => {
        const remainingDays = getKalanGun(item.vadeTarihi);
        const gunText = remainingDays < 0 ? `${Math.abs(remainingDays)} GUN GECIKTI` : (remainingDays === 0 ? "BUGUN" : `${remainingDays} Gun Kaldi`);
        csv += `"${item.type}","${item.title}","${item.subtitle}","${item.vadeTarihi}","${gunText}",${item.tutar}\n`;
      });
      
      const fileUri = `${FileSystem.cacheDirectory}VadeTakip_${Date.now()}.csv`;
      await FileSystem.writeAsStringAsync(fileUri, csv, { encoding: FileSystem.EncodingType.UTF8 });
      
      if (await Sharing.isAvailableAsync()) {
        await Sharing.shareAsync(fileUri, { mimeType: 'text/csv', dialogTitle: 'Vade Takip Excel Paylaş' });
      }
    } catch (e: any) {
      Alert.alert('Hata', 'Excel dosyası oluşturulamadı: ' + e.message);
    }
  };

  const handlePrint = async () => {
    try {
      const items = getVadeItems();
      // Haritalama yapıp pdfService'e gönder
      const mappedItems = items.map(i => ({
        ...i,
        Miktar: i.tutar, // Format adaptasyonu için
        VadeTarihi: i.vadeTarihi,
        Tur: i.type,
        Durum: getKalanGunText(i.vadeTarihi)
      }));
      
      const payload = {
        Vadeler: mappedItems,
        ShowLogo: true
      };
      // 'vade-list' type should be handled by pdfService, or general report
      await generateReportPdf('vade-list', payload, `VadeTakipRaporu_${new Date().getFullYear()}.pdf`);
    } catch (e: any) {
      Alert.alert('Hata', 'PDF raporu oluşturulamadı: ' + e.message);
    }
  };

  const getKpiValues = () => {
    let alacak = 0;
    let borc = 0;
    let gecikmis = 0;

    faturalar.forEach(f => {
      const kalan = (f.genelToplam || 0) - (f.odenen || 0);
      if (kalan > 0) {
        const isIncoming = f.tur === 'Satış' || f.tur === 'Satis';
        if (isIncoming) alacak += kalan;
        else borc += kalan;
        
        const rem = getKalanGun(f.vadeTarihi || f.tarih);
        if (rem < 0) gecikmis++;
      }
    });

    cekler.forEach(c => {
      if (c.durum === 'Portföyde' || c.durum === 'Portfoyde' || c.cekTuru === 'Verilen' || c.type === 'Verilen') {
        const isIncoming = c.cekTuru !== 'Verilen' && c.type !== 'Verilen';
        if (isIncoming) alacak += c.tutar;
        else borc += c.tutar;
        
        const rem = getKalanGun(c.vadeTarihi);
        if (rem < 0) gecikmis++;
      }
    });

    senetler.forEach(s => {
      if (s.durum === 'Portföyde' || s.durum === 'Portfoyde' || s.senetTuru === 'Verilen' || s.type === 'Verilen') {
        const isIncoming = s.senetTuru !== 'Verilen' && s.type !== 'Verilen';
        if (isIncoming) alacak += s.tutar;
        else borc += s.tutar;
        
        const rem = getKalanGun(s.vadeTarihi);
        if (rem < 0) gecikmis++;
      }
    });

    return { alacak, borc, gecikmis };
  };

  const renderKpiCards = () => {
    const { alacak, borc, gecikmis } = getKpiValues();
    return (
      <View style={styles.kpiContainer}>
        <View style={[styles.kpiCard, { borderColor: 'rgba(16, 185, 129, 0.2)', backgroundColor: 'rgba(16, 185, 129, 0.05)' }]}>
          <Text style={[styles.kpiLabel, { color: '#10B981' }]}>BEKLENEN TAHSİLAT</Text>
          <Text style={styles.kpiValue} numberOfLines={1}>{formatMoney(alacak)}</Text>
        </View>
        
        <View style={[styles.kpiCard, { borderColor: 'rgba(251, 113, 133, 0.2)', backgroundColor: 'rgba(251, 113, 133, 0.05)' }]}>
          <Text style={[styles.kpiLabel, { color: '#FB7185' }]}>GELECEK ÖDEMELER</Text>
          <Text style={styles.kpiValue} numberOfLines={1}>{formatMoney(borc)}</Text>
        </View>
        
        <View style={[styles.kpiCard, { borderColor: 'rgba(245, 158, 11, 0.2)', backgroundColor: 'rgba(245, 158, 11, 0.05)' }]}>
          <Text style={[styles.kpiLabel, { color: '#F59E0B' }]}>GECİKMİŞ VADELER</Text>
          <Text style={styles.kpiValue}>{gecikmis} Adet</Text>
        </View>
      </View>
    );
  };

  const monthsList = [
    'Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran', 
    'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık'
  ];

  const getDaysInMonth = (month: number, year: number) => {
    const date = new Date(year, month, 1);
    const days = [];
    const firstDayIndex = date.getDay();
    const startOffset = firstDayIndex === 0 ? 6 : firstDayIndex - 1;

    for (let i = 0; i < startOffset; i++) {
      days.push(null);
    }

    const totalDays = new Date(year, month + 1, 0).getDate();
    for (let i = 1; i <= totalDays; i++) {
      days.push(i);
    }

    return days;
  };

  const handlePrevMonth = () => {
    if (currentMonth === 0) {
      setCurrentMonth(11);
      setCurrentYear(currentYear - 1);
    } else {
      setCurrentMonth(currentMonth - 1);
    }
  };

  const handleNextMonth = () => {
    if (currentMonth === 11) {
      setCurrentMonth(0);
      setCurrentYear(currentYear + 1);
    } else {
      setCurrentMonth(currentMonth + 1);
    }
  };

  const selectCalendarDay = (day: number | null) => {
    if (!day) return;
    const dateStr = `${currentYear}-${String(currentMonth + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
    setNewVadeDate(dateStr);
  };

  const renderCalendarWidget = () => {
    const days = getDaysInMonth(currentMonth, currentYear);
    const dayNames = ['Pz', 'Sa', 'Ça', 'Pe', 'Cu', 'Ct', 'Pa'];
    
    const selectedParts = newVadeDate.split('-');
    const isSelectedMonth = selectedParts.length === 3 && 
                            parseInt(selectedParts[0]) === currentYear && 
                            parseInt(selectedParts[1]) === (currentMonth + 1);
    const selectedDay = isSelectedMonth ? parseInt(selectedParts[2]) : null;

    return (
      <View style={styles.calWrapper}>
        <View style={styles.calHeader}>
          <TouchableOpacity onPress={handlePrevMonth} style={styles.calNavBtn}>
            <Text style={styles.calNavText}>{"<"}</Text>
          </TouchableOpacity>
          <Text style={styles.calTitleText}>{monthsList[currentMonth]} {currentYear}</Text>
          <TouchableOpacity onPress={handleNextMonth} style={styles.calNavBtn}>
            <Text style={styles.calNavText}>{">"}</Text>
          </TouchableOpacity>
        </View>

        <View style={styles.calWeekDaysRow}>
          {dayNames.map((name, i) => (
            <Text key={i} style={styles.calWeekDayCell}>{name}</Text>
          ))}
        </View>

        <View style={styles.calGrid}>
          {days.map((day, i) => {
            const isSelected = day === selectedDay;
            return (
              <TouchableOpacity 
                key={i} 
                style={[styles.calCell, isSelected && styles.calCellSelected]} 
                onPress={() => selectCalendarDay(day)}
                disabled={!day}
              >
                <Text style={[styles.calCellText, !day && { opacity: 0 }, isSelected && styles.calCellTextSelected]}>
                  {day}
                </Text>
              </TouchableOpacity>
            );
          })}
        </View>
      </View>
    );
  };

  const handlePostpone = async () => {
    if (!selectedEvrak) return;
    try {
      const type = selectedEvrak.type;
      const id = selectedEvrak.id;
      const path = type === 'Fatura' ? `Faturalar/${id}/vadeTarihi` : (type === 'Cek' ? `Cekler/${id}/vadeTarihi` : `Senetler/${id}/vadeTarihi`);
      
      const ok = await writeData(path, newVadeDate);
      if (!ok) {
        Alert.alert('Hata', 'Vade tarihi ertelenemedi. (Bağlantı sorunu — kayıt sıraya alındı.)');
        return;
      }
      
      setIsPostponeOpen(false);
      setIsDetailOpen(false);
      Alert.alert('Başarılı', 'Vade tarihi ertelendi.');
    } catch (e) {
      Alert.alert('Hata', 'Vade tarihi ertelenirken bir hata oluştu.');
    }
  };

  const handleUpdateStatus = async () => {
    if (!selectedEvrak) return;
    const item = selectedEvrak.raw;
    const type = selectedEvrak.type;

    try {
      const path = type === 'Cek' ? `Cekler/${item.id}` : `Senetler/${item.id}`;
      const updated = { ...item, durum: newStatus };
      const okDurum = await writeData(path, updated);
      if (!okDurum) {
        Alert.alert('Hata', 'Durum güncellenemedi. (Bağlantı sorunu — kayıt sıraya alındı.)');
        return;
      }

      if (newStatus === 'Ciro Edildi' && selectedCiroCari) {
        // Save ciro movement
        const moveId = generateInt32Id();
        const ciroPayload = {
          id: moveId,
          cariId: selectedCiroCari.id,
          tarih: new Date().toISOString().split('T')[0],
          islemTuru: `${type} Cirosu`,
          evrakNo: item.portfoyNo || item.id.toString(),
          borc: 0,
          alacak: item.tutar,
          aciklama: `${item.portfoyNo} nolu ${type === 'Cek' ? 'Çek' : 'Senet'} ciro edildi.`
        };
        const okCiro = await writeData(`CariHareketler/${moveId}`, ciroPayload);
        if (!okCiro) {
          Alert.alert('Uyarı', 'Durum güncellendi ancak ciro hareketi eşitlenemedi. (Bağlantı sorunu — hareket sıraya alındı.)');
        }

        // Update target Cari balance
        const targetCari = cariler.find(c => c.id === selectedCiroCari.id);
        if (targetCari) {
          const uC = { ...targetCari, alacak: (targetCari.alacak || 0) + item.tutar };
          const okCari = await writeData(`Cariler/${selectedCiroCari.id}`, uC);
          if (!okCari) {
            Alert.alert('Uyarı', 'Cari bakiye eşitlenemedi. (Bağlantı sorunu — bakiye sıraya alındı.)');
          }
        }
      }

      setIsStatusModalOpen(false);
      setIsDetailOpen(false);
      Alert.alert('Başarılı', 'İşlem durumu güncellendi.');
    } catch (e) {
      Alert.alert('Hata', 'Durum güncellenirken hata oluştu.');
    }
  };

  const handleSaveForm = async () => {
    const valTutar = parseFloat(tutar);
    if (!selectedCari || isNaN(valTutar) || valTutar <= 0) {
      Alert.alert('Hata', 'Lütfen cari ve tutarı doğru giriniz.');
      return;
    }

    const nextId = generateInt32Id();
    const payload: any = {
      id: nextId,
      cariId: selectedCari.id,
      cariUnvan: selectedCari.unvan,
      tutar: valTutar,
      vadeTarihi: vadeTarihi,
      banka: banka,
      sube: sube,
      hesapNo: hesapNo,
      seriNo: seriNo,
      borclu: borclu,
      portfoyNo: portfoyNo || `EVR-${Date.now().toString().substring(8)}`,
      durum: 'Portföyde',
      type: evrakTuru,
      aciklama: aciklama,
      isDeleted: false,
    };

    if (evrakTipi === 'Cek') {
      payload.cekTuru = evrakTuru;
      if (gorselYoluOn) payload.gorselYoluOn = gorselYoluOn;
      if (gorselYoluArka) payload.gorselYoluArka = gorselYoluArka;
    } else {
      payload.senetTuru = evrakTuru;
    }

    const path = evrakTipi === 'Cek' ? `Cekler/${nextId}` : `Senetler/${nextId}`;
    const success = await writeData(path, payload);
    if (success) {
      setIsFormOpen(false);
      Alert.alert('Başarılı', 'Evrak başarıyla kaydedildi.');
    } else {
      Alert.alert('Hata', 'Kayıt sırasında bir sorun oluştu.');
    }
  };

  const renderItem = ({ item }: { item: any }) => {
    const remainingDays = getKalanGun(item.vadeTarihi);
    const isOverdue = remainingDays < 0;

    return (
      <TouchableOpacity 
        style={styles.itemCard}
        onPress={() => { setSelectedEvrak(item); setIsDetailOpen(true); }}
      >
        <View style={{ flex: 1 }}>
          <Text style={styles.itemTitle}>{item.title}</Text>
          <Text style={styles.itemSubtitle}>{item.subtitle}</Text>
          <View style={{ flexDirection: 'row', alignItems: 'center', marginTop: 8 }}>
            <Clock color={isOverdue ? '#EF4444' : '#94A3B8'} size={14} style={{ marginRight: 4 }} />
            <Text style={[styles.itemDate, isOverdue && { color: '#EF4444' }]}>
              Vade: {item.vadeTarihi} ({getKalanGunText(item.vadeTarihi)})
            </Text>
          </View>
        </View>
        <Text style={[styles.itemTutar, { color: item.isIncoming ? '#00FF87' : '#EF4444' }]}>
          {formatMoney(item.tutar)}
        </Text>
      </TouchableOpacity>
    );
  };

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.header}>
        <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
          <Text style={styles.headerTitle}>Vade Takibi</Text>
          <TouchableOpacity style={styles.refreshBtnHeader} onPress={() => { /* Yenile butonu */ }}>
            <RefreshCw color="#FFF" size={16} />
            <Text style={styles.refreshBtnTextHeader}>Yenile</Text>
          </TouchableOpacity>
        </View>
        
        <View style={{ flexDirection: 'row', gap: 8, marginTop: 14 }}>
          <View style={[styles.searchBox, { flex: 1, marginTop: 0 }]}>
            <Search color="#64748B" size={20} />
            <TextInput 
              style={styles.searchInput}
              placeholder="Evrak veya cari ara..."
              placeholderTextColor="#64748B"
              value={searchQuery}
              onChangeText={setSearchQuery}
            />
          </View>
        </View>
        
        {/* Action Row */}
        <View style={{ flexDirection: 'row', gap: 8, marginTop: 10 }}>
          <TouchableOpacity style={styles.dropdownBtn} onPress={() => setIsFilterOverlayOpen(true)}>
            <Text style={styles.dropdownBtnText}>
              {timeFilter === 'tümü' ? 'Tümü' : timeFilter === 'bugün' ? 'Bugün' : timeFilter === 'bu-hafta' ? 'Bu Hafta' : timeFilter === 'bu-ay' ? 'Bu Ay' : 'Gecikmiş'}
            </Text>
            <ChevronDown color="#64748B" size={16} />
          </TouchableOpacity>
          <TouchableOpacity style={[styles.exportBtn, { backgroundColor: '#F59E0B' }]} onPress={handleExportExcel}>
            <Download color="#FFF" size={16} />
            <Text style={styles.exportBtnText}>Excel</Text>
          </TouchableOpacity>
          <TouchableOpacity style={[styles.exportBtn, { backgroundColor: '#0061FF' }]} onPress={handlePrint}>
            <Printer color="#FFF" size={16} />
            <Text style={styles.exportBtnText}>Yazdır</Text>
          </TouchableOpacity>
        </View>
      </View>

      {loading ? (
        <View style={styles.center}>
          <ActivityIndicator size="large" color="#0061FF" />
        </View>
      ) : (
        <FlatList initialNumToRender={20} maxToRenderPerBatch={20} windowSize={5} 
          data={getVadeItems()}
          keyExtractor={(item, index) => item.id?.toString() || index.toString()}
          renderItem={renderItem}
          contentContainerStyle={styles.listContent}
          ListHeaderComponent={renderKpiCards}
          ListEmptyComponent={
            <Text style={styles.emptyText}>Takip edilecek vadesi gelen evrak bulunamadı.</Text>
          }
        />
      )}

      {/* Yeni Evrak Modalı */}
      <Modal visible={isFormOpen} animationType="slide" transparent={true}>
        <SafeAreaView style={styles.modalOverlay}>
          <View style={styles.modalContent}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>Yeni Evrak Girişi</Text>
              <TouchableOpacity
                onPress={() => setIsFormOpen(false)}
                hitSlop={{ top: 12, bottom: 12, left: 12, right: 12 }}
              >
                <X color="#FFF" size={24} />
              </TouchableOpacity>
            </View>

            <ScrollView
              contentContainerStyle={{ paddingBottom: 40 }}
              automaticallyAdjustKeyboardInsets={true}
              keyboardShouldPersistTaps="handled"
            >
              <Text style={styles.label}>Evrak Tipi</Text>
              <View style={styles.segmentRow}>
                {['Cek', 'Senet'].map((t: any) => (
                  <TouchableOpacity key={t} style={[styles.segmentBtn, evrakTipi === t && styles.segmentBtnActive]} onPress={() => setEvrakTipi(t)}>
                    <Text style={[styles.segmentBtnText, evrakTipi === t && styles.segmentBtnTextActive]}>{t === 'Cek' ? 'Çek' : 'Senet'}</Text>
                  </TouchableOpacity>
                ))}
              </View>

              <Text style={styles.label}>Evrak Yönü</Text>
              <View style={styles.segmentRow}>
                {['Alınan', 'Verilen'].map((d: any) => (
                  <TouchableOpacity key={d} style={[styles.segmentBtn, evrakTuru === d && styles.segmentBtnActive]} onPress={() => setEvrakTuru(d)}>
                    <Text style={[styles.segmentBtnText, evrakTuru === d && styles.segmentBtnTextActive]}>{d}</Text>
                  </TouchableOpacity>
                ))}
              </View>

              <Text style={styles.label}>Cari Kart</Text>
              <TouchableOpacity style={styles.selector} onPress={() => setIsCariOverlayOpen(true)}>
                <Text style={styles.selectorText}>{selectedCari ? selectedCari.unvan : 'Cari Seçiniz...'}</Text>
              </TouchableOpacity>

              <Text style={styles.label}>Portföy No</Text>
              <TextInput style={styles.input} placeholder="CK-001..." placeholderTextColor="#64748B" value={portfoyNo} onChangeText={setPortfoyNo} />

              <Text style={styles.label}>Seri No</Text>
              <TextInput style={styles.input} placeholder="Seri Numarası..." placeholderTextColor="#64748B" value={seriNo} onChangeText={setSeriNo} />

              <Text style={styles.label}>Tutar (₺)</Text>
              <TextInput
                style={styles.input}
                keyboardType="numeric"
                inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                placeholder="0.00"
                placeholderTextColor="#64748B"
                value={tutar}
                onChangeText={setTutar}
              />

              <Text style={styles.label}>Vade Tarihi</Text>
              <TextInput style={styles.input} placeholder="YYYY-MM-DD" placeholderTextColor="#64748B" value={vadeTarihi} onChangeText={setVadeTarihi} />

              {evrakTipi === 'Cek' && (
                <>
                  <Text style={styles.label}>Asıl Borçlu</Text>
                  <TextInput style={styles.input} placeholder="Asıl Borçlu Adı..." placeholderTextColor="#64748B" value={borclu} onChangeText={setBorclu} />
                  <Text style={styles.label}>Banka</Text>
                  <TextInput style={styles.input} placeholder="Banka Adı..." placeholderTextColor="#64748B" value={banka} onChangeText={setBanka} />
                  <Text style={styles.label}>Şube</Text>
                  <TextInput style={styles.input} placeholder="Şube..." placeholderTextColor="#64748B" value={sube} onChangeText={setSube} />
                  <Text style={styles.label}>Hesap No</Text>
                  <TextInput style={styles.input} placeholder="Hesap Numarası..." placeholderTextColor="#64748B" value={hesapNo} onChangeText={setHesapNo} />

                  <Text style={styles.label}>Çek Görseli (Ön Yüz)</Text>
                  <View style={{ flexDirection: 'row', gap: 8, alignItems: 'center' }}>
                    <TextInput style={[styles.input, { flex: 1, marginTop: 0 }]} placeholder="Galeri seçin..." placeholderTextColor="#64748B" value={gorselYoluOn ? 'Görsel Seçildi ✓' : ''} editable={false} />
                    <TouchableOpacity style={styles.pickImageBtn} onPress={() => pickImage(setGorselYoluOn)}>
                      <Camera color="#FFF" size={18} />
                    </TouchableOpacity>
                  </View>
                  {gorselYoluOn ? <Image source={{ uri: gorselYoluOn }} style={{ width: 80, height: 80, borderRadius: 12, marginTop: 8 }} /> : null}

                  <Text style={styles.label}>Çek Görseli (Arka Yüz)</Text>
                  <View style={{ flexDirection: 'row', gap: 8, alignItems: 'center' }}>
                    <TextInput style={[styles.input, { flex: 1, marginTop: 0 }]} placeholder="Galeri seçin..." placeholderTextColor="#64748B" value={gorselYoluArka ? 'Görsel Seçildi ✓' : ''} editable={false} />
                    <TouchableOpacity style={styles.pickImageBtn} onPress={() => pickImage(setGorselYoluArka)}>
                      <Camera color="#FFF" size={18} />
                    </TouchableOpacity>
                  </View>
                  {gorselYoluArka ? <Image source={{ uri: gorselYoluArka }} style={{ width: 80, height: 80, borderRadius: 12, marginTop: 8 }} /> : null}
                </>
              )}

              <Text style={styles.label}>Açıklama</Text>
              <TextInput style={[styles.input, { height: 60 }]} multiline={true} placeholder="Detaylar..." placeholderTextColor="#64748B" value={aciklama} onChangeText={setAciklama} />

              <TouchableOpacity style={styles.saveBtn} onPress={handleSaveForm}>
                <Save color="#FFF" size={20} />
                <Text style={styles.saveBtnTxt}>Kaydet</Text>
              </TouchableOpacity>
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
                    <TouchableOpacity style={styles.selectorItem} onPress={() => { setSelectedCari(item); setIsCariOverlayOpen(false); }}>
                      <Text style={styles.selectorItemText}>{item.unvan}</Text>
                      <Text style={styles.selectorItemSub}>{item.grup}</Text>
                    </TouchableOpacity>
                  )}
                />
              </View>
            )}
          </View>
        </SafeAreaView>
      </Modal>

      {/* Detay Modalı */}
      <Modal visible={isDetailOpen} animationType="fade" transparent={true}>
        <SafeAreaView style={styles.modalOverlay}>
          <View style={styles.modalContent}>
            {selectedEvrak && (
              <>
                <View style={styles.modalHeader}>
                  <Text style={styles.modalTitle}>{selectedEvrak.type} Detayı</Text>
                  <TouchableOpacity onPress={() => setIsDetailOpen(false)}>
                    <X color="#FFF" size={24} />
                  </TouchableOpacity>
                </View>

                <ScrollView contentContainerStyle={{ paddingBottom: 20 }}>
                  <Text style={styles.detUnvan}>{selectedEvrak.title}</Text>
                  <Text style={styles.detSub}>{selectedEvrak.subtitle}</Text>
                  <Text style={[styles.detAmt, { color: selectedEvrak.isIncoming ? '#00FF87' : '#EF4444' }]}>{formatMoney(selectedEvrak.tutar)}</Text>

                  <View style={styles.detCard}>
                    <Text style={styles.detLabel}>Vade Tarihi:</Text>
                    <Text style={styles.detVal}>{selectedEvrak.vadeTarihi}</Text>
                  </View>

                  {selectedEvrak.type !== 'Fatura' && (
                    <View style={{ flexDirection: 'row', gap: 8, marginTop: 16 }}>
                      <TouchableOpacity style={[styles.detActionBtn, { backgroundColor: '#F59E0B' }]} onPress={() => setIsPostponeOpen(true)}>
                        <Calendar color="#FFF" size={16} />
                        <Text style={styles.detActionText}>Vade Ertele</Text>
                      </TouchableOpacity>
                      <TouchableOpacity style={[styles.detActionBtn, { backgroundColor: '#10B981' }]} onPress={() => setIsStatusModalOpen(true)}>
                        <Edit3 color="#FFF" size={16} />
                        <Text style={styles.detActionText}>Durum Güncelle</Text>
                      </TouchableOpacity>
                    </View>
                  )}
                </ScrollView>
              </>
            )}

            {/* Vade Erteleme Modal / View Overlay */}
            {isPostponeOpen && (
              <View style={styles.absoluteOverlay}>
                <View style={styles.modalHeader}>
                  <Text style={styles.modalTitle}>Vade Ertele</Text>
                  <TouchableOpacity onPress={() => setIsPostponeOpen(false)}>
                    <X color="#FFF" size={24} />
                  </TouchableOpacity>
                </View>
                <Text style={styles.label}>Yeni Vade Tarihi</Text>
                <TextInput style={styles.input} placeholder="YYYY-MM-DD" placeholderTextColor="#64748B" value={newVadeDate} onChangeText={setNewVadeDate} />
                {renderCalendarWidget()}
                <TouchableOpacity style={[styles.saveBtn, { marginTop: 16 }]} onPress={handlePostpone}>
                  <Save color="#FFF" size={20} />
                  <Text style={styles.saveBtnTxt}>Vadeyi Güncelle</Text>
                </TouchableOpacity>
              </View>
            )}

            {/* Durum Güncelleme Modal / View Overlay */}
            {isStatusModalOpen && (
              <View style={styles.absoluteOverlay}>
                <View style={styles.modalHeader}>
                  <Text style={styles.modalTitle}>Durum Güncelle</Text>
                  <TouchableOpacity onPress={() => setIsStatusModalOpen(false)}>
                    <X color="#FFF" size={24} />
                  </TouchableOpacity>
                </View>
                <Text style={styles.label}>Yeni Durum Seçin</Text>
                <View style={styles.segmentRow}>
                  {['Ödendi', 'Ciro Edildi', 'Karşılıksız'].map((st: any) => (
                    <TouchableOpacity key={st} style={[styles.segmentBtn, newStatus === st && styles.segmentBtnActive]} onPress={() => setNewStatus(st)}>
                      <Text style={[styles.segmentBtnText, newStatus === st && styles.segmentBtnTextActive]}>{st}</Text>
                    </TouchableOpacity>
                  ))}
                </View>

                {newStatus === 'Ciro Edildi' && (
                  <>
                    <Text style={styles.label}>Ciro Edilecek Tedarikçi</Text>
                    <TouchableOpacity style={styles.selector} onPress={() => setIsCiroCariOverlayOpen(true)}>
                      <Text style={styles.selectorText}>{selectedCiroCari ? selectedCiroCari.unvan : 'Tedarikçi Seçin...'}</Text>
                    </TouchableOpacity>
                  </>
                )}

                <TouchableOpacity style={[styles.saveBtn, { marginTop: 16 }]} onPress={handleUpdateStatus}>
                  <Save color="#FFF" size={20} />
                  <Text style={styles.saveBtnTxt}>Durumu Güncelle</Text>
                </TouchableOpacity>

                {/* Ciro Cari Seçim Overlay */}
                {isCiroCariOverlayOpen && (
                  <View style={[styles.absoluteOverlay, { zIndex: 100 }]}>
                    <View style={styles.modalHeader}>
                      <Text style={styles.modalTitle}>Tedarikçi Seçin</Text>
                      <TouchableOpacity onPress={() => setIsCiroCariOverlayOpen(false)}>
                        <X color="#FFF" size={24} />
                      </TouchableOpacity>
                    </View>
                    <View style={[styles.searchBox, { marginBottom: 16 }]}>
                      <Search color="#64748B" size={20} />
                      <TextInput style={styles.searchInput} placeholder="Tedarikçi ara..." placeholderTextColor="#64748B" value={ciroSearch} onChangeText={setCiroSearch} />
                    </View>
                    <FlatList initialNumToRender={20} maxToRenderPerBatch={20} windowSize={5} 
                      data={cariler.filter(c => (c.unvan || '').toLocaleLowerCase('tr-TR').includes(ciroSearch.toLocaleLowerCase('tr-TR')))}
                      keyExtractor={(item, idx) => item.id?.toString() || idx.toString()}
                      renderItem={({ item }) => (
                        <TouchableOpacity style={styles.selectorItem} onPress={() => { setSelectedCiroCari(item); setIsCiroCariOverlayOpen(false); }}>
                          <Text style={styles.selectorItemText}>{item.unvan}</Text>
                          <Text style={styles.selectorItemSub}>{item.grup}</Text>
                        </TouchableOpacity>
                      )}
                    />
                  </View>
                )}
              </View>
            )}
          </View>
        </SafeAreaView>
      </Modal>

      {/* Filter Overlay */}
      <Modal visible={isFilterOverlayOpen} animationType="fade" transparent={true}>
        <View style={styles.modalOverlay}>
          <View style={[styles.modalContent, { marginTop: 'auto', marginBottom: 'auto', padding: 20 }]}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>Zaman Filtresi</Text>
              <TouchableOpacity onPress={() => setIsFilterOverlayOpen(false)}>
                <X color="#FFF" size={24} />
              </TouchableOpacity>
            </View>
            {['tümü', 'bugün', 'bu-hafta', 'bu-ay', 'gecikmiş'].map((tf: any) => (
              <TouchableOpacity 
                key={tf}
                style={[styles.selectorItem, timeFilter === tf && { backgroundColor: '#2A2A2A', borderColor: '#0061FF', borderWidth: 1 }]}
                onPress={() => { setTimeFilter(tf); setIsFilterOverlayOpen(false); }}
              >
                <Text style={styles.selectorItemText}>
                  {tf === 'tümü' ? 'Tümü' : tf === 'bugün' ? 'Bugün' : tf === 'bu-hafta' ? 'Bu Hafta' : tf === 'bu-ay' ? 'Bu Ay' : 'Gecikmiş'}
                </Text>
              </TouchableOpacity>
            ))}
          </View>
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
  tabsContainer: {
    flexDirection: 'row',
    paddingHorizontal: 20,
    paddingVertical: 10,
    gap: 8,
  },
  tabBtn: {
    flex: 1,
    paddingVertical: 8,
    backgroundColor: 'rgba(255,255,255,0.04)',
    borderRadius: 10,
    alignItems: 'center',
  },
  tabBtnActive: {
    backgroundColor: '#0061FF',
  },
  tabText: {
    color: '#94A3B8',
    fontWeight: 'bold',
    fontSize: 11,
  },
  tabTextActive: {
    color: '#FFF',
  },
  timeFilters: {
    flexDirection: 'row',
    paddingHorizontal: 20,
    paddingBottom: 10,
    gap: 8,
  },
  timeBtn: {
    flex: 1,
    paddingVertical: 6,
    backgroundColor: 'rgba(255,255,255,0.04)',
    borderRadius: 8,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.08)',
  },
  timeBtnActive: {
    backgroundColor: '#10B981',
    borderColor: '#10B981',
  },
  timeText: {
    color: '#64748B',
    fontWeight: 'bold',
    fontSize: 10,
  },
  timeTextActive: {
    color: '#FFF',
  },
  listContent: {
    padding: 20,
  },
  itemCard: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: '#0F0F0F',
    borderRadius: 16,
    borderColor: 'rgba(255,255,255,0.08)',
    borderWidth: 1,
    padding: 16,
    marginBottom: 10,
  },
  itemTitle: {
    color: '#FFF',
    fontSize: 14,
    fontWeight: 'bold',
  },
  itemSubtitle: {
    color: '#94A3B8',
    fontSize: 11,
    marginTop: 2,
  },
  itemDate: {
    color: '#64748B',
    fontSize: 11,
  },
  itemTutar: {
    fontSize: 14,
    fontWeight: '900',
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
    borderBottomColor: 'rgba(255,255,255,0.08)',
    paddingBottom: 16,
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#FFF',
  },
  label: {
    color: '#94A3B8',
    fontSize: 11,
    fontWeight: 'bold',
    marginTop: 14,
    marginBottom: 6,
    letterSpacing: 0.5,
  },
  segmentRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    backgroundColor: '#2A2A2A',
    borderRadius: 12,
    padding: 4,
  },
  segmentBtn: {
    flex: 1,
    paddingVertical: 10,
    alignItems: 'center',
    borderRadius: 8,
  },
  segmentBtnActive: {
    backgroundColor: '#0061FF',
  },
  segmentBtnText: {
    color: '#64748B',
    fontSize: 11,
    fontWeight: 'bold',
  },
  segmentBtnTextActive: {
    color: '#FFF',
  },
  selector: {
    backgroundColor: '#2A2A2A',
    borderColor: '#444',
    borderWidth: 1,
    borderRadius: 12,
    padding: 14,
  },
  selectorText: {
    color: '#FFF',
    fontSize: 14,
  },
  input: {
    backgroundColor: '#2A2A2A',
    borderColor: '#444',
    borderWidth: 1,
    borderRadius: 12,
    color: '#FFF',
    padding: 12,
    fontSize: 14,
    marginTop: 4,
  },
  saveBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#10B981',
    padding: 16,
    borderRadius: 12,
    marginTop: 24,
  },
  saveBtnTxt: {
    color: '#FFF',
    fontWeight: 'bold',
    fontSize: 14,
    marginLeft: 8,
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
  selectorItem: {
    backgroundColor: '#161616',
    padding: 16,
    borderRadius: 12,
    marginBottom: 8,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.08)',
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
  detUnvan: {
    color: '#FFF',
    fontSize: 20,
    fontWeight: 'bold',
    textAlign: 'center',
    marginTop: 20,
  },
  detSub: {
    color: '#94A3B8',
    fontSize: 13,
    textAlign: 'center',
    marginTop: 4,
  },
  detAmt: {
    fontSize: 24,
    fontWeight: '900',
    textAlign: 'center',
    marginVertical: 16,
  },
  detCard: {
    backgroundColor: '#161616',
    borderRadius: 16,
    borderColor: 'rgba(255,255,255,0.08)',
    borderWidth: 1,
    padding: 16,
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  detLabel: {
    color: '#94A3B8',
    fontSize: 13,
  },
  detVal: {
    color: '#FFF',
    fontSize: 13,
    fontWeight: 'bold',
  },
  detActionBtn: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 12,
    borderRadius: 12,
    gap: 6,
  },
  detActionText: {
    color: '#FFF',
    fontWeight: 'bold',
    fontSize: 13,
  },
  kpiContainer: {
    flexDirection: 'row',
    gap: 8,
    marginBottom: 16,
    paddingHorizontal: 2,
  },
  kpiCard: {
    flex: 1,
    borderWidth: 1,
    borderRadius: 16,
    padding: 12,
    justifyContent: 'center',
  },
  kpiLabel: {
    fontSize: 9,
    fontWeight: 'bold',
    opacity: 0.8,
    marginBottom: 4,
  },
  kpiValue: {
    color: '#FFF',
    fontSize: 12,
    fontWeight: 'bold',
  },
  calWrapper: {
    backgroundColor: '#161616',
    borderRadius: 16,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    padding: 12,
    marginVertical: 12,
  },
  calHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 10,
  },
  calNavBtn: {
    padding: 8,
  },
  calNavText: {
    color: '#0061FF',
    fontSize: 18,
    fontWeight: 'bold',
  },
  calTitleText: {
    color: '#FFF',
    fontSize: 14,
    fontWeight: 'bold',
  },
  calWeekDaysRow: {
    flexDirection: 'row',
    marginBottom: 8,
  },
  calWeekDayCell: {
    flex: 1,
    color: '#64748B',
    fontSize: 11,
    fontWeight: 'bold',
    textAlign: 'center',
  },
  calGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
  },
  calCell: {
    width: '14.28%',
    height: 36,
    justifyContent: 'center',
    alignItems: 'center',
    borderRadius: 18,
  },
  calCellSelected: {
    backgroundColor: '#0061FF',
  },
  calCellText: {
    color: '#FFF',
    fontSize: 12,
  },
  calCellTextSelected: {
    fontWeight: 'bold',
  },
  pickImageBtn: {
    backgroundColor: '#0061FF',
    padding: 12,
    borderRadius: 12,
    justifyContent: 'center',
    alignItems: 'center',
    marginTop: 4,
  },
  refreshBtnHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(255,255,255,0.1)',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 8,
    gap: 6,
  },
  refreshBtnTextHeader: {
    color: '#FFF',
    fontSize: 13,
    fontWeight: 'bold',
  },
  dropdownBtn: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: '#2A2A2A',
    paddingHorizontal: 12,
    paddingVertical: 10,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: '#444',
  },
  dropdownBtnText: {
    color: '#FFF',
    fontSize: 13,
    fontWeight: 'bold',
  },
  exportBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 12,
    paddingVertical: 10,
    borderRadius: 12,
    gap: 6,
  },
  exportBtnText: {
    color: '#FFF',
    fontSize: 13,
    fontWeight: 'bold',
  }
});


