import React, { useState, useEffect } from 'react';
import { StyleSheet, Text, View, SafeAreaView, TextInput, TouchableOpacity, ScrollView, Alert, ActivityIndicator, Share, Image, Switch } from 'react-native';
import { Settings, Globe, Key, Calendar, Wifi, Save, Database, User, MapPin, Phone, Building, Lock, MonitorSmartphone, ArrowLeft, Trash2, Image as ImageIcon, UploadCloud, CheckSquare, Square, ArrowRight, Sparkles, RefreshCw } from 'lucide-react-native';
import * as ImagePicker from 'expo-image-picker';
import AsyncStorage from '../services/storage';
import { saveFirebaseConfig, saveActiveYear, getFirebaseConfig, loadConfigFromStorage, goOfflineMode, writeData, subscribeToPath, logoutUser, deleteData, readData, fetchAvailableYears } from '../services/firebase';
import { getLockSettings, savePin, setLockEnabled, clearLock, setLockTimeout, DEFAULT_LOCK_MINUTES } from '../services/lockService';
import { resetPdfServiceCache, cleanBase64Logo } from '../services/pdfService';

export default function AyarlarScreen() {
  const [dbUrl, setDbUrl] = useState('');
  const [secret, setSecret] = useState('');
  const [activeYear, setActiveYear] = useState('');
  const [availableYears, setAvailableYears] = useState<string[]>([]);
  const [pdfServerUrl, setPdfServerUrl] = useState('');
  const [loading, setLoading] = useState(true);

  // Firma Profili State
  const [firmaUnvan, setFirmaUnvan] = useState('');
  const [firmaYetkili, setFirmaYetkili] = useState('');
  const [firmaTelefon, setFirmaTelefon] = useState('');
  const [firmaAdres, setFirmaAdres] = useState('');
  const [firmaVergiDairesi, setFirmaVergiDairesi] = useState('');
  const [firmaVergiNo, setFirmaVergiNo] = useState('');
  const [firmaEposta, setFirmaEposta] = useState('');
  const [firmaWebSitesi, setFirmaWebSitesi] = useState('');

  // Logo & Belge Gösterim Tercihleri (Masaüstü ile Tam Senkron)
  const [logoBase64, setLogoBase64] = useState<string | null>(null);
  const [logoFatura, setLogoFatura] = useState(true);
  const [logoSiparis, setLogoSiparis] = useState(true);
  const [logoTeklif, setLogoTeklif] = useState(true);
  const [logoEkstre, setLogoEkstre] = useState(true);
  const [logoRaporlar, setLogoRaporlar] = useState(true);
  const [logoTahsilat, setLogoTahsilat] = useState(true);
  const [logoOdeme, setLogoOdeme] = useState(true);
  const [logoAcilisBakiye, setLogoAcilisBakiye] = useState(true);

  // Yedek Geri Yükleme JSON State
  const [backupJsonInput, setBackupJsonInput] = useState('');

  // Oturum Kilidi State
  const [lockEnabled, setLockEnabledState] = useState(false);
  const [lockHasPin, setLockHasPin] = useState(false);
  const [newPin, setNewPin] = useState('');
  const [lockTimeout, setLockTimeoutState] = useState('30');
  
  // Yıl Devir Sihirbazı State
  const [devirKaynakYil, setDevirKaynakYil] = useState((new Date().getFullYear() - 1).toString());
  const [devirHedefYil, setDevirHedefYil] = useState(new Date().getFullYear().toString());
  const [devirCariSecili, setDevirCariSecili] = useState(true);
  const [devirKasaSecili, setDevirKasaSecili] = useState(true);
  const [devirBankaSecili, setDevirBankaSecili] = useState(true);
  const [devirStokSecili, setDevirStokSecili] = useState(true);
  const [devirYukleniyor, setDevirYukleniyor] = useState(false);

  // Kategori Seçim State
  const [currentCategory, setCurrentCategory] = useState<string | null>(null);

  useEffect(() => {
    const fetchSettings = async () => {
      await loadConfigFromStorage();
      const config = getFirebaseConfig();
      if (config) {
        setDbUrl(config.url || '');
        setSecret(config.secret || '');
      }

      const years = await fetchAvailableYears();
      if (years && years.length > 0) setAvailableYears(years);
      const year = await AsyncStorage.getItem('ermay_active_year');
      setActiveYear(year || new Date().getFullYear().toString());

      const savedPdfUrl = await AsyncStorage.getItem('pdf_server_url');
      setPdfServerUrl((savedPdfUrl && !savedPdfUrl.includes('916435485627')) ? savedPdfUrl : 'https://ermay-pdf-api-390930978984.europe-west1.run.app');

      // Oturum kilidi ayarını yükle
      const lockSettings = await getLockSettings();
      setLockEnabledState(lockSettings.enabled);
      setLockHasPin(lockSettings.hasPin);
      setLockTimeoutState(lockSettings.timeoutMinutes.toString());

      // Firma Profilini Firebase'den çek
      const unsubProfile = subscribeToPath('FirmaProfili/1', (data) => {
        if (data) {
          setFirmaUnvan(data.firmaAdi || data.unvan || '');
          setFirmaYetkili(data.yetkili || '');
          setFirmaTelefon(data.telefon || '');
          setFirmaAdres(data.adres || '');
          setFirmaVergiDairesi(data.vergiDairesi || '');
          setFirmaVergiNo(data.vergiNo || '');
          setFirmaEposta(data.eposta || data.email || '');
          setFirmaWebSitesi(data.webSitesi || '');

          // Logo verisi
          const incomingLogo = data.logoBase64 || data.LogoBase64 || null;
          setLogoBase64(incomingLogo);
          if (incomingLogo) {
            AsyncStorage.setItem('ermay_cached_company_logo', String(incomingLogo)).catch(() => {});
          } else {
            AsyncStorage.removeItem('ermay_cached_company_logo').catch(() => {});
            resetPdfServiceCache();
          }

          // Belge Logo Bayrakları
          if (data.logoFatura !== undefined) setLogoFatura(Boolean(data.logoFatura));
          else if (data.LogoFatura !== undefined) setLogoFatura(Boolean(data.LogoFatura));

          if (data.logoSiparis !== undefined) setLogoSiparis(Boolean(data.logoSiparis));
          else if (data.LogoSiparis !== undefined) setLogoSiparis(Boolean(data.LogoSiparis));

          if (data.logoTeklif !== undefined) setLogoTeklif(Boolean(data.logoTeklif));
          else if (data.LogoTeklif !== undefined) setLogoTeklif(Boolean(data.LogoTeklif));

          if (data.logoEkstre !== undefined) setLogoEkstre(Boolean(data.logoEkstre));
          else if (data.LogoEkstre !== undefined) setLogoEkstre(Boolean(data.LogoEkstre));

          if (data.logoRaporlar !== undefined) setLogoRaporlar(Boolean(data.logoRaporlar));
          else if (data.LogoRaporlar !== undefined) setLogoRaporlar(Boolean(data.LogoRaporlar));

          if (data.logoTahsilat !== undefined) setLogoTahsilat(Boolean(data.logoTahsilat));
          else if (data.LogoTahsilat !== undefined) setLogoTahsilat(Boolean(data.LogoTahsilat));

          if (data.logoOdeme !== undefined) setLogoOdeme(Boolean(data.logoOdeme));
          else if (data.LogoOdeme !== undefined) setLogoOdeme(Boolean(data.LogoOdeme));

          if (data.logoAcilisBakiye !== undefined) setLogoAcilisBakiye(Boolean(data.logoAcilisBakiye));
          else if (data.LogoAcilisBakiye !== undefined) setLogoAcilisBakiye(Boolean(data.LogoAcilisBakiye));
        }
      });

      setLoading(false);
      return () => unsubProfile();
    };

    fetchSettings();
  }, []);

  const handlePickLogo = async () => {
    try {
      const { status } = await ImagePicker.requestMediaLibraryPermissionsAsync();
      if (status !== 'granted') {
        Alert.alert('İzin Gerekli', 'Logo seçmek için galeri erişim izni gereklidir.');
        return;
      }
      const res = await ImagePicker.launchImageLibraryAsync({
        mediaTypes: ImagePicker.MediaTypeOptions.Images,
        allowsEditing: true,
        quality: 0.8,
        base64: true,
      });
      if (!res.canceled && res.assets && res.assets.length > 0 && res.assets[0].base64) {
        const clean = cleanBase64Logo(res.assets[0].base64);
        setLogoBase64(clean);
        if (clean) {
          await AsyncStorage.setItem('ermay_cached_company_logo', clean);
          resetPdfServiceCache();
          // Supabase ve masaüstü ile anında senkronize et
          await writeData('firma_profili/1', { id: 1, logo_base64: clean });
          await writeData('FirmaProfili/1/logoBase64', clean);
          await writeData('FirmaProfili/1/LogoBase64', clean);
          await writeData('companies/default/FirmaProfili/1/logoBase64', clean);
          await writeData('companies/default/FirmaProfili/1/LogoBase64', clean);
          await writeData('companies/default/settings/company_logo', clean);
        }
        Alert.alert('Başarılı', 'Şirket logosu güncellendi ve masaüstüyle anında senkronize edildi.');
      }
    } catch (e) {
      Alert.alert('Hata', 'Logo seçilirken bir hata oluştu.');
    }
  };

  const handleRemoveLogo = () => {
    Alert.alert('Logo Kaldır', 'Şirket logosunu kaldırmak istediğinize emin misiniz?', [
      { text: 'Vazgeç', style: 'cancel' },
      { 
        text: 'Kaldır', 
        style: 'destructive', 
        onPress: async () => {
          setLogoBase64(null);
          await AsyncStorage.removeItem('ermay_cached_company_logo');
          resetPdfServiceCache();
          // Anında Firebase'den kaldırarak masaüstüyle anında senkronize et
          await writeData('FirmaProfili/1/logoBase64', null);
          await writeData('FirmaProfili/1/LogoBase64', null);
          await writeData('companies/default/FirmaProfili/1/logoBase64', null);
          await writeData('companies/default/FirmaProfili/1/LogoBase64', null);
          await writeData('companies/default/settings/company_logo', null);
          Alert.alert('Başarılı', 'Şirket logosu kaldırıldı ve masaüstüyle senkronize edildi.');
        } 
      }
    ]);
  };

  const handleSave = async () => {
    if (!dbUrl) {
      Alert.alert('Hata', 'Firebase URL alanı zorunludur.');
      return;
    }

    setLoading(true);
    try {
      await saveFirebaseConfig(dbUrl, secret);
      await saveActiveYear(activeYear);
      await AsyncStorage.setItem('pdf_server_url', pdfServerUrl);

      // Firma Profilini Firebase'e Kaydet (Logo ve Belge Ayarları Dahil)
      const profilePayload = {
        id: 1,
        firmaAdi: firmaUnvan,
        unvan: firmaUnvan,
        yetkili: firmaYetkili,
        telefon: firmaTelefon,
        adres: firmaAdres,
        vergiDairesi: firmaVergiDairesi,
        vergiNo: firmaVergiNo,
        eposta: firmaEposta,
        email: firmaEposta,
        webSitesi: firmaWebSitesi,
        logoBase64: logoBase64 || null,
        LogoBase64: logoBase64 || null,
        logoFatura,
        LogoFatura: logoFatura,
        logoSiparis,
        LogoSiparis: logoSiparis,
        logoTeklif,
        LogoTeklif: logoTeklif,
        logoEkstre,
        LogoEkstre: logoEkstre,
        logoRaporlar,
        LogoRaporlar: logoRaporlar,
        logoTahsilat,
        LogoTahsilat: logoTahsilat,
        logoOdeme,
        LogoOdeme: logoOdeme,
        logoAcilisBakiye,
        LogoAcilisBakiye: logoAcilisBakiye,
      };
      await writeData('firma_profili/1', profilePayload);
      const ok = await writeData('FirmaProfili/1', profilePayload);
      if (!ok) {
        Alert.alert('Hata', 'Ayarlar kaydedilemedi. (Bağlantı sorunu — kayıt sıraya alındı.)');
        return;
      }

      if (logoBase64) {
        await AsyncStorage.setItem('ermay_cached_company_logo', String(logoBase64));
        await writeData('companies/default/FirmaProfili/1/logoBase64', String(logoBase64));
        await writeData('companies/default/FirmaProfili/1/LogoBase64', String(logoBase64));
        await writeData('companies/default/settings/company_logo', String(logoBase64));
      } else {
        await AsyncStorage.removeItem('ermay_cached_company_logo');
        await writeData('FirmaProfili/1/logoBase64', null);
        await writeData('FirmaProfili/1/LogoBase64', null);
        await writeData('companies/default/FirmaProfili/1/logoBase64', null);
        await writeData('companies/default/FirmaProfili/1/LogoBase64', null);
        await writeData('companies/default/settings/company_logo', null);
      }
      resetPdfServiceCache();

      Alert.alert('Başarılı', 'Sistem ayarları, firma profili ve logo tercihleri kaydedildi.');
    } catch (e) {
      Alert.alert('Hata', 'Ayarlar kaydedilirken hata oluştu.');
    } finally {
      setLoading(false);
    }
  };

  // Veritabanı Yedeğini JSON Olarak Paylaş (Export Backup)
  const handleExportBackup = async () => {
    setLoading(true);
    try {
      Alert.alert('Bilgi', 'Veri tabanı yedek dosyası (JSON) oluşturuluyor...');
      const tables = [
        'FirmaProfili', 'users', 'Stoklar', 'Cariler', 'Faturalar', 
        'CariHareketler', 'StokHareketler', 'Siparisler', 'Teklifler', 
        'Bankalar', 'Cekler', 'Senetler', 'KrediKartlari', 'EftIslemleri', 
        'StokSayimlar', 'PortfoyKartlari', 'SatisHedefleri', 'Notes', 
        'Gorevler', 'BelgeArsiv'
      ];
      
      const backupData: any = {};
      for (const table of tables) {
        const raw = await readData(table);
        if (raw) {
          backupData[table] = raw;
        }
      }
      
      const jsonStr = JSON.stringify(backupData, null, 2);
      await Share.share({
        message: jsonStr,
        title: 'Ermay Muhasebe Sistem Yedeği',
      });
    } catch (error) {
      Alert.alert('Hata', 'Yedek dışa aktarılamadı.');
    } finally {
      setLoading(false);
    }
  };

  // JSON Yapıştırarak Veritabanı Yedeğini Yükle (Import Backup)
  const handleImportBackup = async () => {
    if (!backupJsonInput.trim()) {
      Alert.alert('Hata', 'Lütfen geçerli bir JSON yedek verisi yapıştırın.');
      return;
    }
    setLoading(true);
    try {
      const parsedData = JSON.parse(backupJsonInput);
      for (const key of Object.keys(parsedData)) {
        const ok = await writeData(key, parsedData[key]);
        if (!ok) {
          Alert.alert('Hata', `"${key}" geri yüklenemedi. (Bağlantı sorunu — tablo sıraya alındı.)`);
        }
      }
      setBackupJsonInput('');
      Alert.alert('Başarılı', 'Yedek veri tabanına başarıyla geri yüklendi.');
    } catch (e) {
      Alert.alert('Hata', 'Geçersiz JSON formatı. Lütfen yedek dosyasını doğru yapıştırdığınızdan emin olun.');
    } finally {
      setLoading(false);
    }
  };

  // Veri Temizliği (Soft-Deleted kalıcı temizleme)
  const handleDataCleanup = async () => {
    Alert.alert(
      'Veri Temizliği (Kalıcı Silme)',
      'Veritabanında "silindi" olarak işaretlenmiş (soft-deleted) tüm kayıtlar kalıcı olarak silinecektir. Bu işlem geri alınamaz. Emin misiniz?',
      [
        { text: 'İptal', style: 'cancel' },
        {
          text: 'Evet, Kalıcı Sil',
          style: 'destructive',
          onPress: async () => {
            setLoading(true);
            try {
              const tables = [
                'Stoklar', 'Cariler', 'Faturalar', 'CariHareketler', 'StokHareketler',
                'Siparisler', 'Teklifler', 'Bankalar', 'Cekler', 'Senetler',
                'KrediKartlari', 'EftIslemleri', 'StokSayimlar', 'PortfoyKartlari',
                'SatisHedefleri', 'Notes', 'Gorevler', 'BelgeArsiv'
              ];
              
              let deletedCount = 0;
              for (const table of tables) {
                const data = await readData(table);
                if (data) {
                  const keys = Object.keys(data);
                  for (const key of keys) {
                    const item = data[key];
                    if (item && (item.isDeleted === true || item.IsDeleted === true)) {
                      await deleteData(`${table}/${key}`);
                      deletedCount++;
                    }
                  }
                }
              }
              Alert.alert('Başarılı', `Veri temizliği tamamlandı. Toplam ${deletedCount} adet çöp kayıt kalıcı olarak temizlendi.`);
            } catch (e) {
              Alert.alert('Hata', 'Veri temizlenirken bir sorun oluştu.');
            } finally {
              setLoading(false);
            }
          }
        }
      ]
    );
  };
  

  const handleSaveLock = async () => {
    const trimmed = newPin.trim();
    if (trimmed.length < 4 || trimmed.length > 6) {
      Alert.alert('Hata', 'PIN 4-6 haneli olmalıdır.');
      return;
    }
    const timeoutVal = parseInt(lockTimeout, 10);
    if (isNaN(timeoutVal) || timeoutVal < 1 || timeoutVal > 1440) {
      Alert.alert('Hata', 'Zaman aşımı 1 ile 1440 dakika arasında olmalıdır.');
      return;
    }
    
    setLoading(true);
    try {
      await savePin(trimmed);
      await setLockEnabled(true);
      await setLockTimeout(timeoutVal);
      setLockEnabledState(true);
      setLockHasPin(true);
      setNewPin('');
      Alert.alert('Başarılı', `Oturum kilidi aktif. ${timeoutVal} dk arka planda kalınca PIN sorulur.`);
    } catch (e) {
      Alert.alert('Hata', 'Kilit ayarları kaydedilirken hata oluştu.');
    } finally {
      setLoading(false);
    }
  };

  const handleUpdateTimeoutOnly = async () => {
    const timeoutVal = parseInt(lockTimeout, 10);
    if (isNaN(timeoutVal) || timeoutVal < 1 || timeoutVal > 1440) {
      Alert.alert('Hata', 'Zaman aşımı 1 ile 1440 dakika arasında olmalıdır.');
      return;
    }
    setLoading(true);
    try {
      await setLockTimeout(timeoutVal);
      Alert.alert('Başarılı', `Zaman aşımı güncellendi. ${timeoutVal} dk arka planda kalınca PIN sorulur.`);
    } catch (e) {
      Alert.alert('Hata', 'Zaman aşımı kaydedilirken hata oluştu.');
    } finally {
      setLoading(false);
    }
  };

  const handleDisableLock = async () => {
    Alert.alert(
      'Oturum Kilidini Kaldır',
      'Mevcut PIN silinecek ve kilit özelliği kapatılacak. Emin misiniz?',
      [
        { text: 'İptal', style: 'cancel' },
        {
          text: 'Evet, Kaldır',
          style: 'destructive',
          onPress: async () => {
            await clearLock();
            setLockEnabledState(false);
            setLockHasPin(false);
            setNewPin('');
            Alert.alert('Başarılı', 'Oturum kilidi kaldırıldı.');
          }
        }
      ]
    );
  };

  // Veri Temizlik: yerel önbellek ve bekleyen yazma kuyruğunu temizle
  const handleClearLocalCache = async () => {
    Alert.alert(
      'Yerel Verileri Temizle',
      'Uygulamanın cihazdaki önbelleği (lastik veri anlık görüntüleri) ve bekleyen yazma kuyruğu silinecek. Bulut verileri etkilenmez. Emin misiniz?',
      [
        { text: 'İptal', style: 'cancel' },
        {
          text: 'Evet, Temizle',
          style: 'destructive',
          onPress: async () => {
            try {
              const keys = await AsyncStorage.getAllKeys();
              const toRemove = keys.filter(k => k.startsWith('ermay_cache_') || k === 'ermay_write_queue');
              if (toRemove.length > 0) await AsyncStorage.multiRemove(toRemove);
              Alert.alert('Başarılı', `${toRemove.length} önbellek kaydı temizlendi.`);
            } catch (e) {
              Alert.alert('Hata', 'Önbellek temizlenirken hata oluştu.');
            }
          }
        }
      ]
    );
  };

  const handleLogout = async () => {
    Alert.alert(
      'Yapılandırmayı Sıfırla',
      'Firebase yapılandırmasını silmek istediğinize emin misiniz? Uygulama offline moda geçecektir.',
      [
        { text: 'İptal', style: 'cancel' },
        { 
          text: 'Evet, Sıfırla', 
          style: 'destructive',
          onPress: async () => {
            await goOfflineMode();
            setDbUrl('');
            setSecret('');
            setActiveYear(new Date().getFullYear().toString());
            setPdfServerUrl('http://192.168.1.103:5244');
            Alert.alert('Başarılı', 'Ayarlar temizlendi.');
          }
        }
      ]
    );
  };

  const handleUserLogout = async () => {
    Alert.alert(
      'Oturumu Kapat',
      'Hesap oturumunuz kapatılacak. Emin misiniz?',
      [
        { text: 'İptal', style: 'cancel' },
        { 
          text: 'Evet, Kapat', 
          style: 'destructive',
          onPress: async () => {
            await logoutUser();
          }
        }
      ]
    );
  };

  const handleSwitchYear = async (newYear: string) => {
    if (newYear === activeYear) return;
    try {
      setLoading(true);
      await saveActiveYear(newYear);
      setActiveYear(newYear);
      Alert.alert('Başarılı', `Aktif çalışma yılı ${newYear} olarak değiştirildi. Tüm veriler yeni mali yıla göre yüklenecektir.`);
    } catch (e) {
      Alert.alert('Hata', 'Yıl değiştirilemedi.');
    } finally {
      setLoading(false);
    }
  };

  const handleYearTransfer = async () => {
    if (!devirKaynakYil || !devirHedefYil) {
      Alert.alert('Hata', 'Lütfen kaynak ve hedef yılları seçiniz.');
      return;
    }
    if (devirKaynakYil === devirHedefYil) {
      Alert.alert('Hata', 'Kaynak mali yıl ile hedef mali yıl aynı olamaz.');
      return;
    }

    Alert.alert(
      'Mali Yıl Devir Onayı',
      `${devirKaynakYil} yılı kapanış bakiyeleri (Cari, Kasa, Banka, Stok) ${devirHedefYil} yılına Açılış Devir Fişi olarak aktarılacaktır. Devam etmek istiyor musunuz?`,
      [
        { text: 'İptal', style: 'cancel' },
        {
          text: 'Devret',
          onPress: async () => {
            setDevirYukleniyor(true);
            try {
              let aktarilanCari = 0;
              let aktarilanKasa = 0;
              let aktarilanBanka = 0;
              let aktarilanStok = 0;

              // 1. CARİ DEVİR
              if (devirCariSecili) {
                const carilerRaw = await readData('Cariler');
                if (carilerRaw) {
                  const cariList = Array.isArray(carilerRaw) ? carilerRaw.filter(Boolean) : Object.keys(carilerRaw).map(k => ({ ...carilerRaw[k], firebaseKey: k }));
                  for (const c of cariList.filter(x => !x.isDeleted)) {
                    const bakiye = (Number(c.borc) || 0) - (Number(c.alacak) || 0);
                    if (bakiye !== 0) {
                      const devirBorc = bakiye > 0 ? bakiye : 0;
                      const devirAlacak = bakiye < 0 ? Math.abs(bakiye) : 0;
                      
                      // Hedef yılda cari kartı devir bakiyesiyle oluştur/güncelle
                      await writeData(`Cariler/${c.id}`, {
                        ...c,
                        devirBorc,
                        devirAlacak,
                        borc: 0,
                        alacak: 0,
                        bakiye: bakiye
                      });

                      // Devir Fişi Hareketi
                      await writeData(`CariHareketler/DEVIR_${c.id}_${devirHedefYil}`, {
                        id: `DEVIR_${c.id}_${devirHedefYil}`,
                        cariId: c.id,
                        cariUnvan: c.unvan,
                        islemTuru: 'Devir Fişi',
                        evrakNo: `DEVIR-${devirKaynakYil}`,
                        aciklama: `${devirKaynakYil} Yılı Kapanış Bakiye Devri`,
                        borc: devirBorc,
                        alacak: devirAlacak,
                        bakiye: bakiye,
                        tarih: `${devirHedefYil}-01-01T00:00:00.000Z`,
                        isDeleted: false
                      });
                      aktarilanCari++;
                    }
                  }
                }
              }

              // 2. KASA DEVİR
              if (devirKasaSecili) {
                const kasaRaw = await readData('KasaHareketler');
                if (kasaRaw) {
                  const kasaList = Array.isArray(kasaRaw) ? kasaRaw.filter(Boolean) : Object.keys(kasaRaw).map(k => ({ ...kasaRaw[k], firebaseKey: k }));
                  const totalGiren = kasaList.reduce((s, h) => s + (Number(h.giren) || 0), 0);
                  const totalCikan = kasaList.reduce((s, h) => s + (Number(h.cikan) || 0), 0);
                  const netKasa = totalGiren - totalCikan;

                  if (netKasa !== 0) {
                    await writeData(`KasaHareketler/DEVIR_KASA_${devirHedefYil}`, {
                      id: `DEVIR_KASA_${devirHedefYil}`,
                      islemTuru: 'Devir Fişi',
                      evrakNo: `DEVIR-KASA-${devirKaynakYil}`,
                      aciklama: `${devirKaynakYil} Yılı Kasa Açılış Nakit Devri`,
                      giren: netKasa > 0 ? netKasa : 0,
                      cikan: netKasa < 0 ? Math.abs(netKasa) : 0,
                      bakiye: netKasa,
                      tarih: `${devirHedefYil}-01-01T00:00:00.000Z`,
                      isDeleted: false
                    });
                    aktarilanKasa++;
                  }
                }
              }

              // 3. BANKA DEVİR
              if (devirBankaSecili) {
                const bankaRaw = await readData('Bankalar');
                if (bankaRaw) {
                  const bankaList = Array.isArray(bankaRaw) ? bankaRaw.filter(Boolean) : Object.keys(bankaRaw).map(k => ({ ...bankaRaw[k], firebaseKey: k }));
                  for (const b of bankaList.filter(x => !x.isDeleted)) {
                    const bakiye = Number(b.bakiye) || ((Number(b.giren) || 0) - (Number(b.cikan) || 0));
                    if (bakiye !== 0) {
                      await writeData(`BankaHareketler/DEVIR_BNK_${b.id}_${devirHedefYil}`, {
                        id: `DEVIR_BNK_${b.id}_${devirHedefYil}`,
                        bankaId: b.id,
                        bankaAdi: b.bankaAdi || b.hesapAdi || 'Banka',
                        islemTuru: 'Devir Fişi',
                        evrakNo: `DEVIR-BNK-${devirKaynakYil}`,
                        aciklama: `${devirKaynakYil} Yılı Banka Açılış Devri`,
                        giren: bakiye > 0 ? bakiye : 0,
                        cikan: bakiye < 0 ? Math.abs(bakiye) : 0,
                        bakiye: bakiye,
                        tarih: `${devirHedefYil}-01-01T00:00:00.000Z`,
                        isDeleted: false
                      });
                      aktarilanBanka++;
                    }
                  }
                }
              }

              // 4. STOK DEVİR
              if (devirStokSecili) {
                const stokRaw = await readData('Stoklar');
                if (stokRaw) {
                  const stokList = Array.isArray(stokRaw) ? stokRaw.filter(Boolean) : Object.keys(stokRaw).map(k => ({ ...stokRaw[k], firebaseKey: k }));
                  for (const s of stokList.filter(x => !x.isDeleted)) {
                    const miktar = Number(s.mevcutMiktar ?? s.miktar) || 0;
                    if (miktar !== 0) {
                      await writeData(`StokHareketler/DEVIR_STK_${s.id}_${devirHedefYil}`, {
                        id: `DEVIR_STK_${s.id}_${devirHedefYil}`,
                        stokId: s.id,
                        stokAdi: s.stokAdi,
                        islemTuru: 'Devir Fişi',
                        evrakNo: `DEVIR-STK-${devirKaynakYil}`,
                        aciklama: `${devirKaynakYil} Yılı Stok Sayım Devri`,
                        miktar: miktar,
                        girisMiktari: miktar > 0 ? miktar : 0,
                        cikisMiktari: miktar < 0 ? Math.abs(miktar) : 0,
                        tarih: `${devirHedefYil}-01-01T00:00:00.000Z`,
                        isDeleted: false
                      });

                      await writeData(`Stoklar/${s.id}`, {
                        ...s,
                        devirMiktari: miktar,
                        mevcutMiktar: miktar
                      });
                      aktarilanStok++;
                    }
                  }
                }
              }

              // Yıl listesini güncelle
              if (!availableYears.includes(devirHedefYil)) {
                setAvailableYears(prev => [...prev, devirHedefYil].sort());
              }

              Alert.alert(
                'Yıl Devri Tamamlandı',
                `${devirKaynakYil} yılından ${devirHedefYil} yılına devir işlemi başarıyla gerçekleşti:\n\n` +
                `• Cari Devir Fişi: ${aktarilanCari} cari\n` +
                `• Kasa Devir Fişi: ${aktarilanKasa} kasa\n` +
                `• Banka Devir Fişi: ${aktarilanBanka} banka\n` +
                `• Stok Devir Fişi: ${aktarilanStok} ürün\n\n` +
                `Şimdi ${devirHedefYil} çalışma yılına geçiş yapmak ister misiniz?`,
                [
                  { text: 'Hayır, Mevcut Yılda Kal', style: 'cancel' },
                  { 
                    text: `${devirHedefYil} Yılına Geç`, 
                    onPress: async () => {
                      await saveActiveYear(devirHedefYil);
                      setActiveYear(devirHedefYil);
                      Alert.alert('Bilgi', `Aktif çalışma yılı ${devirHedefYil} olarak ayarlandı.`);
                    }
                  }
                ]
              );
            } catch (err: any) {
              Alert.alert('Hata', 'Devir işlemi sırasında hata oluştu: ' + (err?.message || err));
            } finally {
              setDevirYukleniyor(false);
            }
          }
        }
    );
  };

  const handleReflectBalancesForward = async () => {
    const nextYear = (parseInt(activeYear) + 1).toString();
    Alert.alert(
      'Sonraki Yıla Bakiye Yansıt',
      `${activeYear} yılı güncel kapanış bakiyeleri (Cari, Kasa, Banka, Stok) ${nextYear} yılı devir bakiyelerine yansıtılacaktır. Onaylıyor musunuz?`,
      [
        { text: 'İptal', style: 'cancel' },
        {
          text: 'Yansıt',
          onPress: async () => {
            setDevirYukleniyor(true);
            try {
              let cariCount = 0;
              const carilerRaw = await readData('Cariler');
              if (carilerRaw) {
                const cariList = Array.isArray(carilerRaw) ? carilerRaw.filter(Boolean) : Object.keys(carilerRaw).map(k => ({ ...carilerRaw[k], firebaseKey: k }));
                for (const c of cariList.filter(x => !x.isDeleted)) {
                  const bakiye = (Number(c.borc) || 0) + (Number(c.devirBorc) || 0) - (Number(c.alacak) || 0) - (Number(c.devirAlacak) || 0);
                  const devirBorc = bakiye > 0 ? bakiye : 0;
                  const devirAlacak = bakiye < 0 ? Math.abs(bakiye) : 0;

                  await writeData(`Cariler/${c.id}`, {
                    ...c,
                    devirBorc,
                    devirAlacak,
                    bakiye: bakiye
                  });

                  await writeData(`CariHareketler/DEVIR_${c.id}_${nextYear}`, {
                    id: `DEVIR_${c.id}_${nextYear}`,
                    cariId: c.id,
                    cariUnvan: c.unvan,
                    islemTuru: 'Devir Fişi',
                    evrakNo: `DEVIR-${activeYear}`,
                    aciklama: `${activeYear} Yılı Güncellenen Bakiye Devri`,
                    borc: devirBorc,
                    alacak: devirAlacak,
                    bakiye: bakiye,
                    tarih: `${nextYear}-01-01T00:00:00.000Z`,
                    isDeleted: false
                  });
                  cariCount++;
                }
              }

              Alert.alert('Başarılı', `${activeYear} yılı güncel bakiyeleri ${nextYear} yılına başarıyla yansıtıldı (${cariCount} cari güncellendi).`);
            } catch (err: any) {
              Alert.alert('Hata', 'Bakiyeler yansıtılırken hata oluştu: ' + (err?.message || err));
            } finally {
              setDevirYukleniyor(false);
            }
          }
        }
      ]
    );
  };

  if (loading) {
    return (
      <SafeAreaView style={styles.container}>
        <View style={styles.center}>
          <ActivityIndicator size="large" color="#0061FF" />
        </View>
      </SafeAreaView>
    );
  }

  const categories = [
    { id: 'cloud', title: 'Bulut ve API', description: 'Firebase ve PDF servis bağlantılarını yönetin.', icon: Globe, color: '#F59E0B' },
    { id: 'profile', title: 'Firma Profili', description: 'Firma ünvanı, iletişim ve logo ayarları.', icon: Building, color: '#8B5CF6' },
    { id: 'security', title: 'Güvenlik ve Kilit', description: 'Oturum kilidi, PIN ve şifre ayarları.', icon: Lock, color: '#EF4444' },
    { id: 'backup', title: 'Yedekleme ve Bakım', description: 'Tüm veritabanını yedekleyin veya geri yükleyin.', icon: Database, color: '#10B981' },
    { id: 'devir', title: 'Mali Yıl & Devir Sihirbazı', description: 'Aktif çalışma yılı ve bakiyeleri yeni yıla devretme.', icon: Calendar, color: '#3B82F6' },
    { id: 'cleanup', title: 'Veri Temizlik', description: 'Silinmiş çöp kayıtları ve yerel önbelleği temizleyin.', icon: Trash2, color: '#64748B' }
  ];

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.header}>
        <View style={{ flexDirection: 'row', alignItems: 'center' }}>
          <Settings color="#0061FF" size={28} style={{ marginRight: 12 }} />
          <Text style={styles.headerTitle}>
            {currentCategory ? categories.find(c => c.id === currentCategory)?.title : 'Sistem Ayarları'}
          </Text>
        </View>
        <Text style={styles.headerSubtitle}>
          {currentCategory ? categories.find(c => c.id === currentCategory)?.description : 'Firebase sunucu ve firma profili yapılandırması'}
        </Text>
      </View>

      <ScrollView contentContainerStyle={styles.scrollContent}>
        {currentCategory === null ? (
          <View style={styles.categoriesGrid}>
            {categories.map((cat) => {
              const Icon = cat.icon;
              return (
                <TouchableOpacity
                  key={cat.id}
                  style={styles.categoryCard}
                  onPress={() => setCurrentCategory(cat.id)}
                >
                  <View style={[styles.categoryIconBox, { backgroundColor: `${cat.color}15` }]}>
                    <Icon color={cat.color} size={24} />
                  </View>
                  <Text style={styles.categoryTitle}>{cat.title}</Text>
                  <Text style={styles.categoryDesc} numberOfLines={2}>{cat.description}</Text>
                </TouchableOpacity>
              );
            })}
            
            {/* Alt İşlemler */}
            <View style={{ width: '100%', marginTop: 20 }}>
              <TouchableOpacity style={[styles.logoutButton, { borderColor: '#E2E8F0' }]} onPress={handleUserLogout}>
                <Text style={[styles.logoutButtonText, { color: '#E2E8F0' }]}>Oturumu Kapat (Çıkış Yap)</Text>
              </TouchableOpacity>
              <TouchableOpacity style={styles.logoutButton} onPress={handleLogout}>
                <Text style={styles.logoutButtonText}>Yapılandırmayı Sıfırla (Offline Mod)</Text>
              </TouchableOpacity>
            </View>
          </View>
        ) : (
          <>
            {/* 1. Bulut ve API */}
            {currentCategory === 'cloud' && (
              <View style={styles.card}>
                <View style={styles.cardHeader}>
                  <Database color="#0061FF" size={22} />
                  <Text style={styles.cardTitle}>Firebase Sunucu Ayarları</Text>
                </View>

                <View style={styles.inputGroup}>
                  <View style={{ flexDirection: 'row', alignItems: 'center', marginBottom: 8 }}>
                    <Globe color="#94A3B8" size={16} style={{ marginRight: 6 }} />
                    <Text style={styles.inputLabel}>Firebase Realtime Database URL</Text>
                  </View>
                  <TextInput 
                    style={styles.input}
                    placeholder="https://your-app-default-rtdb.firebaseio.com"
                    placeholderTextColor="#64748B"
                    value={dbUrl}
                    onChangeText={setDbUrl}
                    autoCapitalize="none"
                    autoCorrect={false}
                  />
                </View>

                <View style={styles.inputGroup}>
                  <View style={{ flexDirection: 'row', alignItems: 'center', marginBottom: 8 }}>
                    <Key color="#94A3B8" size={16} style={{ marginRight: 6 }} />
                    <Text style={styles.inputLabel}>Firebase Database Secret (Opsiyonel)</Text>
                  </View>
                  <TextInput 
                    style={styles.input}
                    placeholder="Database Secret Auth Token"
                    placeholderTextColor="#64748B"
                    value={secret}
                    onChangeText={setSecret}
                    secureTextEntry={true}
                    autoCapitalize="none"
                    autoCorrect={false}
                  />
                </View>

                <View style={styles.inputGroup}>
                  <View style={{ flexDirection: 'row', alignItems: 'center', marginBottom: 8 }}>
                    <Key color="#94A3B8" size={16} style={{ marginRight: 6 }} />
                    <Text style={styles.inputLabel}>PDF Rapor Sunucu Adresi</Text>
                  </View>
                  <TextInput 
                    style={styles.input}
                    placeholder="http://192.168.1.103:5244"
                    placeholderTextColor="#64748B"
                    value={pdfServerUrl}
                    onChangeText={setPdfServerUrl}
                    autoCapitalize="none"
                    autoCorrect={false}
                  />
                </View>

                <TouchableOpacity style={styles.saveButton} onPress={handleSave}>
                  <Save color="#FFF" size={20} style={{ marginRight: 8 }} />
                  <Text style={styles.saveButtonText}>Bağlantıları Kaydet</Text>
                </TouchableOpacity>
              </View>
            )}

            {/* 2. Firma Profili & Logo */}
            {currentCategory === 'profile' && (
              <View style={styles.card}>
                <View style={styles.cardHeader}>
                  <Building color="#8B5CF6" size={22} />
                  <Text style={styles.cardTitle}>Firma Profili & Logo Yönetimi</Text>
                </View>

                {/* Logo Önizleme & Yükleme Alanı */}
                <View style={[styles.inputGroup, { backgroundColor: '#0B1120', padding: 14, borderRadius: 12, borderWidth: 1, borderColor: '#1E293B', alignItems: 'center' }]}>
                  <Text style={[styles.inputLabel, { alignSelf: 'flex-start', color: '#EC4899', fontWeight: 'bold' }]}>
                    Şirket Logosu (Masaüstü & Mobil Ortak)
                  </Text>
                  
                  {logoBase64 ? (
                    <View style={{ width: '100%', alignItems: 'center', marginVertical: 10, padding: 10, backgroundColor: '#0F172A', borderRadius: 8, borderWidth: 1, borderColor: '#334155' }}>
                      <Image 
                        source={{ uri: logoBase64.startsWith('data:') ? logoBase64 : `data:image/png;base64,${logoBase64}` }} 
                        style={{ width: '100%', height: 75, resizeMode: 'contain' }} 
                      />
                    </View>
                  ) : (
                    <View style={{ width: '100%', height: 75, justifyContent: 'center', alignItems: 'center', backgroundColor: '#0F172A', borderRadius: 8, marginVertical: 10, borderWidth: 1, borderColor: '#1E293B', borderStyle: 'dashed' }}>
                      <ImageIcon color="#64748B" size={32} />
                      <Text style={{ color: '#64748B', fontSize: 12, marginTop: 4 }}>Henüz Şirket Logosu Eklenmedi</Text>
                    </View>
                  )}

                  <View style={{ flexDirection: 'row', gap: 10, width: '100%', marginTop: 6 }}>
                    <TouchableOpacity 
                      style={[styles.saveButton, { flex: 1, backgroundColor: '#8B5CF6', marginTop: 0, paddingVertical: 10 }]} 
                      onPress={handlePickLogo}
                    >
                      <UploadCloud color="#FFF" size={18} style={{ marginRight: 6 }} />
                      <Text style={[styles.saveButtonText, { fontSize: 13 }]}>{logoBase64 ? 'Logoyu Değiştir' : 'Logo Yükle'}</Text>
                    </TouchableOpacity>

                    {logoBase64 ? (
                      <TouchableOpacity 
                        style={[styles.saveButton, { flex: 0.8, backgroundColor: '#EF4444', marginTop: 0, paddingVertical: 10 }]} 
                        onPress={handleRemoveLogo}
                      >
                        <Trash2 color="#FFF" size={18} style={{ marginRight: 6 }} />
                        <Text style={[styles.saveButtonText, { fontSize: 13 }]}>Logoyu Sil</Text>
                      </TouchableOpacity>
                    ) : null}
                  </View>
                </View>

                {/* Belgelerde Logo Gösterimi Seçenekleri */}
                <View style={[styles.inputGroup, { backgroundColor: '#0B1120', padding: 14, borderRadius: 12, borderWidth: 1, borderColor: '#1E293B' }]}>
                  <Text style={[styles.inputLabel, { color: '#EC4899', fontWeight: 'bold', marginBottom: 12 }]}>
                    Belgelerde Logo Gösterimi
                  </Text>
                  
                  {[
                    { label: 'Faturalarda Göster', value: logoFatura, setter: setLogoFatura },
                    { label: 'Sipariş Belgelerinde Göster', value: logoSiparis, setter: setLogoSiparis },
                    { label: 'Teklif Belgelerinde Göster', value: logoTeklif, setter: setLogoTeklif },
                    { label: 'Cari Ekstrelerde Göster', value: logoEkstre, setter: setLogoEkstre },
                    { label: 'Genel Raporlarda Göster', value: logoRaporlar, setter: setLogoRaporlar },
                    { label: 'Tahsilat Makbuzlarında Göster', value: logoTahsilat, setter: setLogoTahsilat },
                    { label: 'Ödeme Makbuzlarında Göster', value: logoOdeme, setter: setLogoOdeme },
                    { label: 'Açılış Bakiye Fişlerinde Göster', value: logoAcilisBakiye, setter: setLogoAcilisBakiye },
                  ].map((item, idx) => (
                    <View key={idx} style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', paddingVertical: 8, borderBottomWidth: idx < 7 ? 1 : 0, borderBottomColor: '#1E293B' }}>
                      <Text style={{ color: '#E2E8F0', fontSize: 13.5 }}>{item.label}</Text>
                      <Switch 
                        value={item.value} 
                        onValueChange={item.setter} 
                        trackColor={{ false: '#334155', true: '#8B5CF6' }}
                        thumbColor={item.value ? '#FFF' : '#94A3B8'}
                      />
                    </View>
                  ))}
                </View>

                <View style={styles.inputGroup}>
                  <Text style={styles.inputLabel}>Firma Ünvanı</Text>
                  <TextInput style={styles.input} placeholder="Firma Tam Adı..." placeholderTextColor="#64748B" value={firmaUnvan} onChangeText={setFirmaUnvan} />
                </View>

                <View style={styles.inputGroup}>
                  <Text style={styles.inputLabel}>Firma Yetkilisi</Text>
                  <TextInput style={styles.input} placeholder="Yetkili Adı Soyadı..." placeholderTextColor="#64748B" value={firmaYetkili} onChangeText={setFirmaYetkili} />
                </View>

                <View style={styles.inputGroup}>
                  <Text style={styles.inputLabel}>Telefon</Text>
                  <TextInput style={styles.input} placeholder="Firma Telefonu..." placeholderTextColor="#64748B" value={firmaTelefon} onChangeText={setFirmaTelefon} />
                </View>

                <View style={styles.inputGroup}>
                  <Text style={styles.inputLabel}>Vergi Dairesi / No</Text>
                  <View style={{ flexDirection: 'row', gap: 10 }}>
                    <TextInput style={[styles.input, { flex: 1 }]} placeholder="Vergi Dairesi..." placeholderTextColor="#64748B" value={firmaVergiDairesi} onChangeText={setFirmaVergiDairesi} />
                    <TextInput style={[styles.input, { flex: 1 }]} placeholder="Vergi No..." placeholderTextColor="#64748B" value={firmaVergiNo} onChangeText={setFirmaVergiNo} />
                  </View>
                </View>

                <View style={styles.inputGroup}>
                  <Text style={styles.inputLabel}>E-Posta</Text>
                  <TextInput style={styles.input} placeholder="Firma E-Posta Adresi..." placeholderTextColor="#64748B" value={firmaEposta} onChangeText={setFirmaEposta} keyboardType="email-address" autoCapitalize="none" />
                </View>

                <View style={styles.inputGroup}>
                  <Text style={styles.inputLabel}>Web Sitesi</Text>
                  <TextInput style={styles.input} placeholder="Firma Web Sitesi..." placeholderTextColor="#64748B" value={firmaWebSitesi} onChangeText={setFirmaWebSitesi} keyboardType="url" autoCapitalize="none" />
                </View>

                <View style={styles.inputGroup}>
                  <Text style={styles.inputLabel}>Adres</Text>
                  <TextInput style={[styles.input, { height: 60 }]} multiline={true} placeholder="Firma Adresi..." placeholderTextColor="#64748B" value={firmaAdres} onChangeText={setFirmaAdres} />
                </View>

                <TouchableOpacity style={styles.saveButton} onPress={handleSave}>
                  <Save color="#FFF" size={20} style={{ marginRight: 8 }} />
                  <Text style={styles.saveButtonText}>Firma Profilini & Logoyu Kaydet</Text>
                </TouchableOpacity>
              </View>
            )}



            {/* 4. Güvenlik ve Kilit */}
            {currentCategory === 'security' && (
              <View style={styles.card}>
                <View style={styles.cardHeader}>
                  <Lock color="#EF4444" size={22} />
                  <Text style={styles.cardTitle}>Oturum Kilidi PIN Ayarları</Text>
                </View>
                <Text style={[styles.inputLabel, { fontWeight: 'normal' }]}>
                  Durum: <Text style={{ color: lockEnabled ? '#00FF87' : '#EF4444', fontWeight: 'bold' }}>{lockEnabled ? 'Kilit Aktif' : 'Kilit Kapalı'}</Text>
                </Text>
                <Text style={[styles.inputLabel, { fontWeight: 'normal', marginTop: 4, marginBottom: 12 }]}>
                  Uygulama arka planda {lockTimeout} dakika kaldığında PIN sorulur.
                </Text>

                <View style={styles.inputGroup}>
                  <View style={{ flexDirection: 'row', alignItems: 'center', marginBottom: 8 }}>
                    <Key color="#94A3B8" size={16} style={{ marginRight: 6 }} />
                    <Text style={styles.inputLabel}>Zaman Aşımı (Dakika)</Text>
                  </View>
                  <TextInput
                    style={styles.input}
                    placeholder="Örn: 30"
                    placeholderTextColor="#64748B"
                    value={lockTimeout}
                    onChangeText={setLockTimeoutState}
                    keyboardType="number-pad"
                    maxLength={4}
                  />
                </View>

                <View style={styles.inputGroup}>
                  <View style={{ flexDirection: 'row', alignItems: 'center', marginBottom: 8 }}>
                    <Key color="#94A3B8" size={16} style={{ marginRight: 6 }} />
                    <Text style={styles.inputLabel}>Yeni Kilit PIN (4-6 hane)</Text>
                  </View>
                  <TextInput
                    style={styles.input}
                    placeholder="PIN girin..."
                    placeholderTextColor="#64748B"
                    value={newPin}
                    onChangeText={setNewPin}
                    secureTextEntry
                    keyboardType="number-pad"
                    maxLength={6}
                  />
                </View>

                <TouchableOpacity style={[styles.btn, { backgroundColor: '#EF4444', marginBottom: 12 }]} onPress={handleSaveLock}>
                  <Text style={styles.btnText}>{lockHasPin ? 'PIN ve Süreyi Güncelle' : 'PIN Tanımla ve Aktifleştir'}</Text>
                </TouchableOpacity>

                {lockEnabled && (
                  <TouchableOpacity style={[styles.btn, { backgroundColor: '#3B82F6', marginBottom: 12 }]} onPress={handleUpdateTimeoutOnly}>
                    <Text style={styles.btnText}>Sadece Süreyi Güncelle</Text>
                  </TouchableOpacity>
                )}

                {lockEnabled && (
                  <TouchableOpacity style={[styles.btn, { backgroundColor: 'rgba(255,255,255,0.06)' }]} onPress={handleDisableLock}>
                    <Text style={[styles.btnText, { color: '#EF4444' }]}>Oturum Kilidini Kaldır</Text>
                  </TouchableOpacity>
                )}
              </View>
            )}

            {/* 5. Yedekleme ve Bakım */}
            {currentCategory === 'backup' && (
              <View style={styles.card}>
                <View style={styles.cardHeader}>
                  <Database color="#10B981" size={22} />
                  <Text style={styles.cardTitle}>Veritabanı Yedekleme & Geri Yükleme</Text>
                </View>

                <TouchableOpacity style={[styles.btn, { backgroundColor: '#10B981', marginBottom: 16 }]} onPress={handleExportBackup}>
                  <Text style={styles.btnText}>Yedeği Dışa Aktar (Export JSON)</Text>
                </TouchableOpacity>

                <View style={styles.inputGroup}>
                  <Text style={styles.inputLabel}>JSON Yedek Verisini Yapıştırın</Text>
                  <TextInput 
                    style={[styles.input, { height: 80 }]} 
                    placeholder="JSON yedek metnini buraya yapıştırın..." 
                    placeholderTextColor="#64748B"
                    multiline={true}
                    value={backupJsonInput}
                    onChangeText={setBackupJsonInput}
                  />
                </View>

                <TouchableOpacity style={[styles.btn, { backgroundColor: '#3B82F6' }]} onPress={handleImportBackup}>
                  <Text style={styles.btnText}>Yedeği Geri Yükle (Import JSON)</Text>
                </TouchableOpacity>
              </View>
            )}



            {/* 6. Mali Yıl & Yıl Devir Sihirbazı */}
            {currentCategory === 'devir' && (
              <>
                {/* Aktif Çalışma Yılı Kartı */}
                <View style={styles.card}>
                  <View style={styles.cardHeader}>
                    <Calendar color="#3B82F6" size={22} />
                    <Text style={styles.cardTitle}>Aktif Çalışma Yılı</Text>
                  </View>
                  <Text style={[styles.inputLabel, { fontWeight: 'normal', marginBottom: 12 }]}>
                    Uygulamanın fatura, hareket ve stok kayıtlarını işlediği aktif mali yıl.
                  </Text>
                  
                  <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 10, marginBottom: 10 }}>
                    {availableYears.map((yr) => (
                      <TouchableOpacity
                        key={yr}
                        style={[
                          styles.segmentBtn,
                          activeYear === yr ? styles.segmentBtnActive : { backgroundColor: '#1E293B' },
                          { paddingHorizontal: 16, flex: 0 }
                        ]}
                        onPress={() => handleSwitchYear(yr)}
                      >
                        <Text style={[styles.segmentBtnText, activeYear === yr && styles.segmentBtnTextActive]}>
                          {yr} {activeYear === yr ? '✓ (Aktif)' : ''}
                        </Text>
                      </TouchableOpacity>
                    ))}
                  </View>
                </View>

                {/* Yıl Devir Sihirbazı Kartı */}
                <View style={styles.card}>
                  <View style={styles.cardHeader}>
                    <Sparkles color="#10B981" size={22} />
                    <Text style={styles.cardTitle}>Yıl Devir Sihirbazı (Year Transfer)</Text>
                  </View>
                  <Text style={[styles.inputLabel, { fontWeight: 'normal', marginBottom: 14 }]}>
                    Önceki mali yılın cari borç/alacak, kasa nakit, banka ve depo stok kapanış bakiyelerini yeni mali yıla devir fişi olarak aktarır.
                  </Text>

                  {/* Kaynak ve Hedef Yıl Seçimi */}
                  <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16, backgroundColor: '#0B1120', padding: 12, borderRadius: 12, borderWidth: 1, borderColor: '#1E293B' }}>
                    <View style={{ flex: 1, alignItems: 'center' }}>
                      <Text style={{ color: '#94A3B8', fontSize: 11, marginBottom: 4 }}>Kaynak Mali Yıl</Text>
                      <TextInput
                        style={[styles.input, { width: 90, textAlign: 'center', fontWeight: 'bold' }]}
                        keyboardType="numeric"
                        value={devirKaynakYil}
                        onChangeText={setDevirKaynakYil}
                      />
                    </View>

                    <ArrowRight color="#3B82F6" size={24} style={{ marginHorizontal: 8 }} />

                    <View style={{ flex: 1, alignItems: 'center' }}>
                      <Text style={{ color: '#94A3B8', fontSize: 11, marginBottom: 4 }}>Hedef Mali Yıl</Text>
                      <TextInput
                        style={[styles.input, { width: 90, textAlign: 'center', fontWeight: 'bold' }]}
                        keyboardType="numeric"
                        value={devirHedefYil}
                        onChangeText={setDevirHedefYil}
                      />
                    </View>
                  </View>

                  {/* Devredilecek Modüller */}
                  <Text style={[styles.inputLabel, { color: '#E2E8F0', fontWeight: 'bold', marginBottom: 8 }]}>
                    Devredilecek Modüller
                  </Text>

                  <TouchableOpacity 
                    style={{ flexDirection: 'row', alignItems: 'center', paddingVertical: 8 }}
                    onPress={() => setDevirCariSecili(!devirCariSecili)}
                  >
                    {devirCariSecili ? <CheckSquare color="#10B981" size={20} /> : <Square color="#64748B" size={20} />}
                    <Text style={{ color: '#FFF', marginLeft: 10, fontSize: 13 }}>Cari Hesap Kapanış Bakiyeleri (Devir Fişi)</Text>
                  </TouchableOpacity>

                  <TouchableOpacity 
                    style={{ flexDirection: 'row', alignItems: 'center', paddingVertical: 8 }}
                    onPress={() => setDevirKasaSecili(!devirKasaSecili)}
                  >
                    {devirKasaSecili ? <CheckSquare color="#10B981" size={20} /> : <Square color="#64748B" size={20} />}
                    <Text style={{ color: '#FFF', marginLeft: 10, fontSize: 13 }}>Kasa Nakit Bakiyesi (Devir Fişi)</Text>
                  </TouchableOpacity>

                  <TouchableOpacity 
                    style={{ flexDirection: 'row', alignItems: 'center', paddingVertical: 8 }}
                    onPress={() => setDevirBankaSecili(!devirBankaSecili)}
                  >
                    {devirBankaSecili ? <CheckSquare color="#10B981" size={20} /> : <Square color="#64748B" size={20} />}
                    <Text style={{ color: '#FFF', marginLeft: 10, fontSize: 13 }}>Banka Hesap Bakiyeleri (Devir Fişi)</Text>
                  </TouchableOpacity>

                  <TouchableOpacity 
                    style={{ flexDirection: 'row', alignItems: 'center', paddingVertical: 8, marginBottom: 16 }}
                    onPress={() => setDevirStokSecili(!devirStokSecili)}
                  >
                    {devirStokSecili ? <CheckSquare color="#10B981" size={20} /> : <Square color="#64748B" size={20} />}
                    <Text style={{ color: '#FFF', marginLeft: 10, fontSize: 13 }}>Stok Depo Sayım Miktarları (Devir Fişi)</Text>
                  </TouchableOpacity>

                  <TouchableOpacity 
                    style={[styles.btn, { backgroundColor: '#10B981' }]} 
                    onPress={handleYearTransfer}
                    disabled={devirYukleniyor}
                  >
                    {devirYukleniyor ? (
                      <ActivityIndicator color="#FFF" />
                    ) : (
                      <Text style={styles.btnText}>Yıl Devir İşlemini Başlat</Text>
                    )}
                  </TouchableOpacity>

                  <TouchableOpacity 
                    style={[styles.btn, { backgroundColor: '#F59E0B', marginTop: 10 }]} 
                    onPress={handleReflectBalancesForward}
                    disabled={devirYukleniyor}
                  >
                    <Text style={styles.btnText}>Sonraki Yıla Bakiye Yansıt (Devri Yenile)</Text>
                  </TouchableOpacity>
                </View>
              </>
            )}

            {/* 7. Veri Temizlik */}
            {currentCategory === 'cleanup' && (
              <>
                <View style={styles.card}>
                  <View style={styles.cardHeader}>
                    <Trash2 color="#EF4444" size={22} />
                    <Text style={styles.cardTitle}>Veri Temizliği (Bulut / Firebase)</Text>
                  </View>
                  <Text style={[styles.inputLabel, { fontWeight: 'normal', marginBottom: 12 }]}>
                    Firebase Realtime Database üzerindeki silinmiş (soft-deleted / isDeleted: true) çöpleri kalıcı olarak siler.
                  </Text>
                  <TouchableOpacity style={[styles.btn, { backgroundColor: '#EF4444' }]} onPress={handleDataCleanup}>
                    <Text style={styles.btnText}>Silinen Kayıtları Kalıcı Sil</Text>
                  </TouchableOpacity>
                </View>

                <View style={styles.card}>
                  <View style={styles.cardHeader}>
                    <Database color="#64748B" size={22} />
                    <Text style={styles.cardTitle}>Yerel Önbellek Temizliği</Text>
                  </View>
                  <Text style={[styles.inputLabel, { fontWeight: 'normal', marginBottom: 12 }]}>
                    Cihazın yerel diskindeki çevrimdışı önbelleği ve bekleyen işlem kuyruklarını temizler.
                  </Text>
                  <TouchableOpacity style={[styles.btn, { backgroundColor: '#64748B' }]} onPress={handleClearLocalCache}>
                    <Text style={styles.btnText}>Cihaz Önbelleğini Sıfırla</Text>
                  </TouchableOpacity>
                </View>
              </>
            )}
            
            {/* Geri Dön (Alt) */}
            <TouchableOpacity 
              onPress={() => setCurrentCategory(null)} 
              style={[styles.logoutButton, { borderColor: '#E2E8F0', marginTop: 20 }]}
            >
              <Text style={[styles.logoutButtonText, { color: '#E2E8F0' }]}>Kategorilere Dön</Text>
            </TouchableOpacity>
          </>
        )}
      </ScrollView>
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
  headerSubtitle: {
    color: '#64748B',
    fontSize: 13,
    marginTop: 4,
  },
  scrollContent: {
    padding: 20,
    paddingBottom: 40,
  },
  card: {
    backgroundColor: '#161616',
    borderRadius: 20,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    padding: 16,
    marginBottom: 16,
  },
  cardHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 16,
  },
  cardTitle: {
    color: '#FFF',
    fontSize: 15,
    fontWeight: 'bold',
    marginLeft: 10,
  },
  statusText: {
    color: '#00FF87',
    fontSize: 14,
    fontWeight: 'bold',
  },
  inputGroup: {
    marginBottom: 14,
  },
  inputLabel: {
    color: '#94A3B8',
    fontSize: 12,
    fontWeight: 'bold',
    marginBottom: 6,
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
  btn: {
    padding: 14,
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
  },
  btnText: {
    color: '#FFF',
    fontWeight: 'bold',
    fontSize: 13,
  },
  saveButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#0061FF',
    padding: 16,
    borderRadius: 14,
    marginTop: 10,
  },
  saveButtonText: {
    color: '#FFF',
    fontWeight: 'bold',
    fontSize: 15,
  },
  logoutButton: {
    padding: 16,
    borderRadius: 14,
    borderWidth: 1,
    borderColor: '#EF4444',
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 12,
  },
  logoutButtonText: {
    color: '#EF4444',
    fontWeight: 'bold',
    fontSize: 14,
  },
  categoriesGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    justifyContent: 'space-between',
    padding: 2,
  },
  categoryCard: {
    width: '48%',
    backgroundColor: 'rgba(255, 255, 255, 0.03)',
    borderRadius: 20,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.05)',
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
  backButtonHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 20,
    backgroundColor: 'rgba(255,255,255,0.04)',
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
  },
  segmentBtn: {
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
  }
});
