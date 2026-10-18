import React, { useState, useEffect, useMemo } from 'react';
import {
  StyleSheet,
  Text,
  View,
  SafeAreaView,
  FlatList,
  TextInput,
  ActivityIndicator,
  TouchableOpacity,
  Modal,
  ScrollView,
  Alert,
  StatusBar,
  Platform,
  Keyboard,
  KeyboardAvoidingView,
} from 'react-native';
import { KeyboardDismissBar } from '../components/KeyboardDismissBar';
import {
  Folder,
  Search,
  Plus,
  Trash2,
  ArrowLeft,
  MessageSquare,
  Tag,
  Image as ImageIcon,
  FileText,
  Phone,
  User,
  Calendar,
  X,
  Check,
  ChevronRight,
  ChevronDown,
  FolderPlus,
  Users,
  Building,
} from 'lucide-react-native';
import { subscribeToPath, writeData, deleteData } from '../services/firebase';
import { triggerSelectionHaptic } from '../services/hapticsService';
import { generateInt32Id } from '../utils/IdGenerator';

export interface MusteriTakipKlasor {
  id: number;
  tenantId?: string;
  cariId: number;
  cariUnvan: string;
  cariKod?: string;
  telefon?: string;
  yetkili?: string;
  etiket?: string;
  renk?: string;
  olusturmaTarihi: string;
  sonIslemTarihi: string;
  aciklama?: string;
  isDeleted?: boolean;
}

export interface MusteriTakipDetay {
  id: number;
  tenantId?: string;
  klasorId: number;
  cariId: number;
  baslik: string;
  icerik?: string;
  tip: 'Gorusme' | 'Fiyat' | 'Gorsel' | 'Not';
  fiyatBilgisi?: number;
  paraBirimi?: string;
  dosyaYolu?: string;
  gorselBase64?: string;
  tarih: string;
  isDeleted?: boolean;
}

const ETIKETLER = ['Tümü', 'Sıcak Müşteri', 'Teklif Aşamasında', 'Önemli', 'Yeni İletişim', 'Takipte'];

export default function MusteriTakipScreen({ navigation }: any) {
  const [klasorler, setKlasorler] = useState<MusteriTakipKlasor[]>([]);
  const [detaylar, setDetaylar] = useState<MusteriTakipDetay[]>([]);
  const [cariler, setCariler] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  // Arama ve filtreler
  const [searchString, setSearchString] = useState('');
  const [selectedEtiket, setSelectedEtiket] = useState('Tümü');
  const [isEtiketDropdownOpen, setIsEtiketDropdownOpen] = useState(false);

  // Cari Seçim Modalı
  const [isCariModalOpen, setIsCariModalOpen] = useState(false);
  const [cariSearchText, setCariSearchText] = useState('');

  useEffect(() => {
    // 1. Klasörleri dinle
    const unsubKlasor = subscribeToPath('MusteriTakipKlasorler', (data) => {
      if (!data) {
        setKlasorler([]);
      } else {
        const list: MusteriTakipKlasor[] = Array.isArray(data)
          ? data.filter(Boolean)
          : Object.keys(data).map((k) => ({ ...data[k], id: Number(data[k].id || k) }));
        setKlasorler(list.filter((x) => !x.isDeleted));
      }
      setLoading(false);
    });

    // 2. Detayları dinle
    const unsubDetay = subscribeToPath('MusteriTakipDetaylar', (data) => {
      if (!data) {
        setDetaylar([]);
      } else {
        const list: MusteriTakipDetay[] = Array.isArray(data)
          ? data.filter(Boolean)
          : Object.keys(data).map((k) => ({ ...data[k], id: Number(data[k].id || k) }));
        setDetaylar(list.filter((x) => !x.isDeleted));
      }
    });

    // 3. Carileri dinle
    const unsubCari = subscribeToPath('Cariler', (data) => {
      if (!data) {
        setCariler([]);
      } else {
        const list = Array.isArray(data)
          ? data.filter(Boolean)
          : Object.keys(data).map((k) => ({ ...data[k], id: Number(data[k].id || k) }));
        setCariler(list);
      }
    });

    return () => {
      unsubKlasor();
      unsubDetay();
      unsubCari();
    };
  }, []);

  // Klasör sayaç haritası (klasorId -> { gorusme, fiyat, gorsel, not })
  const statsMap = useMemo(() => {
    const map: Record<number, { gorusme: number; fiyat: number; gorsel: number; not: number }> = {};
    for (const d of detaylar) {
      if (!map[d.klasorId]) {
        map[d.klasorId] = { gorusme: 0, fiyat: 0, gorsel: 0, not: 0 };
      }
      if (d.tip === 'Gorusme') map[d.klasorId].gorusme++;
      else if (d.tip === 'Fiyat') map[d.klasorId].fiyat++;
      else if (d.tip === 'Gorsel') map[d.klasorId].gorsel++;
      else if (d.tip === 'Not') map[d.klasorId].not++;
    }
    return map;
  }, [detaylar]);

  // Filtrelenmiş klasörler
  const filteredKlasorler = useMemo(() => {
    return klasorler.filter((k) => {
      if (selectedEtiket !== 'Tümü' && k.etiket !== selectedEtiket) {
        return false;
      }
      if (searchString.trim()) {
        const s = searchString.trim().toLowerCase();
        const unvan = (k.cariUnvan || '').toLowerCase();
        const kod = (k.cariKod || '').toLowerCase();
        const tel = (k.telefon || '').toLowerCase();
        const yetkili = (k.yetkili || '').toLowerCase();
        const aciklama = (k.aciklama || '').toLowerCase();
        return (
          unvan.includes(s) ||
          kod.includes(s) ||
          tel.includes(s) ||
          yetkili.includes(s) ||
          aciklama.includes(s)
        );
      }
      return true;
    }).sort((a, b) => {
      const dateA = new Date(a.sonIslemTarihi || a.olusturmaTarihi || 0).getTime();
      const dateB = new Date(b.sonIslemTarihi || b.olusturmaTarihi || 0).getTime();
      return dateB - dateA;
    });
  }, [klasorler, selectedEtiket, searchString]);

  // Takipte olan müşteri (CariId) kümesi
  const existingCariIds = useMemo(() => {
    const set = new Set<number>();
    for (const k of klasorler) {
      if (!k.isDeleted && k.cariId) {
        set.add(Number(k.cariId));
      }
    }
    return set;
  }, [klasorler]);

  // Filtrelenmiş cariler - Takipte olan müşteriler listeden gizlenir
  const filteredCariler = useMemo(() => {
    const available = cariler.filter((c) => !existingCariIds.has(Number(c.id)));
    if (!cariSearchText.trim()) return available;
    const s = cariSearchText.trim().toLowerCase();
    return available.filter((c) => {
      const unvan = (c.unvan || '').toLowerCase();
      const kod = (c.cariKod || '').toLowerCase();
      const tel = (c.telefon || '').toLowerCase();
      return unvan.includes(s) || kod.includes(s) || tel.includes(s);
    });
  }, [cariler, cariSearchText, existingCariIds]);

  // Yeni Müşteri Klasörü Oluşturma veya Mevcut Olanı Açma
  const handleSelectCariAndCreate = async (cari: any) => {
    triggerSelectionHaptic();
    setIsCariModalOpen(false);

    // Zaten bu cari için bir klasör var mı?
    const existing = klasorler.find((k) => k.cariId === cari.id && !k.isDeleted);
    if (existing) {
      navigation.navigate('MusteriTakipDetay', { klasor: existing });
      return;
    }

    // Yeni klasör oluştur
    const newId = generateInt32Id();
    const now = new Date().toISOString();
    const newKlasor: MusteriTakipKlasor = {
      id: newId,
      tenantId: 'default',
      cariId: cari.id,
      cariUnvan: cari.unvan || 'İsimsiz Müşteri',
      cariKod: cari.cariKod || '',
      telefon: cari.telefon || '',
      yetkili: cari.yetkili || '',
      etiket: 'Yeni İletişim',
      renk: '#3B82F6',
      olusturmaTarihi: now,
      sonIslemTarihi: now,
      aciklama: '',
      isDeleted: false,
    };

    // Seçilen müşteriyi anında listeden gizlemek ve klasörlere eklemek için yerel state'i güncelle
    setKlasorler((prev) => [newKlasor, ...prev.filter((k) => k.cariId !== cari.id)]);

    try {
      await writeData(`MusteriTakipKlasorler/${newId}`, newKlasor);
      navigation.navigate('MusteriTakipDetay', { klasor: newKlasor });
    } catch (e) {
      Alert.alert('Hata', 'Klasör oluşturulamadı: ' + (e as any)?.message);
    }
  };

  // Klasör Silme
  const handleDeleteKlasor = (klasor: MusteriTakipKlasor) => {
    triggerSelectionHaptic();
    Alert.alert(
      'Klasörü Sil',
      `"${klasor.cariUnvan}" müşterisine ait takip klasörünü ve tüm ilişkili kayıtları silmek istediğinize emin misiniz?`,
      [
        { text: 'Vazgeç', style: 'cancel' },
        {
          text: 'Sil',
          style: 'destructive',
          onPress: async () => {
            try {
              // Mantıksal silme (IsDeleted = true)
              await writeData(`MusteriTakipKlasorler/${klasor.id}/isDeleted`, true);
              // Detayları da mantıksal sil
              const kDetaylar = detaylar.filter((d) => d.klasorId === klasor.id);
              for (const d of kDetaylar) {
                await writeData(`MusteriTakipDetaylar/${d.id}/isDeleted`, true);
              }
            } catch (e) {
              Alert.alert('Hata', 'Silme işlemi başarısız oldu.');
            }
          },
        },
      ]
    );
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

  return (
    <SafeAreaView style={styles.container}>
      <StatusBar barStyle="light-content" backgroundColor="#0A0A0A" />

      {/* 1. ÜST SAYFA BAŞLIĞI */}
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

        <View style={styles.headerTitles}>
          <Text style={styles.title}>Müşteri Takip & Klasörleme</Text>
          <Text style={styles.subtitle} numberOfLines={2}>
            Müşterilerinize ait klasörleri listeleyin; görüşme, fiyat ve evrakları takip edin.
          </Text>
        </View>
      </View>

      {/* 2. ARAÇ ÇUBUĞU (TOOLBAR) - Masaüstü ile Birebir */}
      <View style={styles.toolbarCard}>
        {/* Arama Kutusu ve Etiket Filtresi Açılır Menüsü (ComboBox) */}
        <View style={styles.searchRow}>
          <View style={styles.searchBox}>
            <Search color="#64748B" size={17} />
            <TextInput
              style={styles.searchInput}
              placeholder="Müşteri adı, yetkili veya telefon ara..."
              placeholderTextColor="#64748B"
              value={searchString}
              onChangeText={setSearchString}
              returnKeyType="search"
              onSubmitEditing={() => Keyboard.dismiss()}
            />
            {searchString.length > 0 && (
              <TouchableOpacity
                onPress={() => {
                  setSearchString('');
                  Keyboard.dismiss();
                }}
                hitSlop={{ top: 8, bottom: 8, left: 8, right: 8 }}
              >
                <X color="#94A3B8" size={16} />
              </TouchableOpacity>
            )}
          </View>

          {/* 2. GÖRSELDEKİ ÖZELLİK: ETİKET COMBOBOX'I (AÇILIR LİSTE) */}
          <TouchableOpacity
            style={styles.dropdownBtn}
            onPress={() => {
              triggerSelectionHaptic();
              setIsEtiketDropdownOpen(true);
            }}
            activeOpacity={0.8}
          >
            <Text style={styles.dropdownBtnText} numberOfLines={1}>
              {selectedEtiket}
            </Text>
            <ChevronDown color="#94A3B8" size={16} />
          </TouchableOpacity>
        </View>

        {/* YENİ MÜŞTERİ AKTAR BUTONU */}
        <TouchableOpacity
          style={styles.addMusteriBtn}
          onPress={() => {
            triggerSelectionHaptic();
            setCariSearchText('');
            setIsCariModalOpen(true);
          }}
          activeOpacity={0.85}
        >
          <Plus color="#FFFFFF" size={18} />
          <Text style={styles.addMusteriBtnText}>+ YENİ MÜŞTERİ AKTAR</Text>
        </TouchableOpacity>
      </View>

      {/* Ana Liste */}
      {loading ? (
        <View style={styles.centerBox}>
          <ActivityIndicator size="large" color="#0A84FF" />
          <Text style={styles.loadingText}>Klasörler yükleniyor...</Text>
        </View>
      ) : filteredKlasorler.length === 0 ? (
        <View style={styles.emptyContainer}>
          <View style={styles.emptyIconCircle}>
            <Folder color="#64748B" size={44} />
          </View>
          <Text style={styles.emptyTitle}>Henüz müşteri klasörü yok</Text>
          <Text style={styles.emptyDesc}>
            Cari hesaplarınızdan müşteri seçerek yeni takip klasörü oluşturabilirsiniz.
          </Text>
          <TouchableOpacity
            style={styles.emptyActionBtn}
            onPress={() => {
              triggerSelectionHaptic();
              setCariSearchText('');
              setIsCariModalOpen(true);
            }}
            activeOpacity={0.8}
          >
            <FolderPlus color="#FFFFFF" size={18} />
            <Text style={styles.emptyActionBtnText}>Cari Hesaplardan Müşteri Seç</Text>
          </TouchableOpacity>
        </View>
      ) : (
        <FlatList
          data={filteredKlasorler}
          keyExtractor={(item) => item.id.toString()}
          contentContainerStyle={styles.listContent}
          showsVerticalScrollIndicator={false}
          renderItem={({ item }) => {
            const stats = statsMap[item.id] || { gorusme: 0, fiyat: 0, gorsel: 0, not: 0 };
            const folderColor = item.renk || '#0A84FF';

            return (
              <TouchableOpacity
                style={styles.card}
                activeOpacity={0.8}
                onPress={() => {
                  triggerSelectionHaptic();
                  navigation.navigate('MusteriTakipDetay', { klasor: item });
                }}
              >
                {/* Üst Kısım: Klasör İkonu, Müşteri Adı, Silme */}
                <View style={styles.cardHeader}>
                  <View style={[styles.folderIconBox, { backgroundColor: `${folderColor}22` }]}>
                    <Folder color={folderColor} size={22} />
                  </View>

                  <View style={styles.cardHeaderInfo}>
                    <Text style={styles.cariUnvan} numberOfLines={1}>
                      {item.cariUnvan}
                    </Text>
                    {item.etiket ? (
                      <View style={styles.etiketBadge}>
                        <Text style={styles.etiketBadgeText}>{item.etiket}</Text>
                      </View>
                    ) : null}
                  </View>

                  <TouchableOpacity
                    style={styles.cardDeleteBtn}
                    onPress={() => handleDeleteKlasor(item)}
                    hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
                  >
                    <Trash2 color="#FF453A" size={16} />
                  </TouchableOpacity>
                </View>

                {/* Yetkili & Telefon Bilgileri */}
                <View style={styles.cardBody}>
                  {item.yetkili ? (
                    <View style={styles.metaRow}>
                      <User color="#94A3B8" size={13} />
                      <Text style={styles.metaText} numberOfLines={1}>
                        {item.yetkili}
                      </Text>
                    </View>
                  ) : null}
                  {item.telefon ? (
                    <View style={styles.metaRow}>
                      <Phone color="#94A3B8" size={13} />
                      <Text style={styles.metaText}>{item.telefon}</Text>
                    </View>
                  ) : null}
                </View>

                {/* Alt Kısım: Sayaç Rozetleri & Son İşlem Tarihi */}
                <View style={styles.cardFooter}>
                  <View style={styles.badgesRow}>
                    {/* Görüşme */}
                    <View style={[styles.badge, { backgroundColor: 'rgba(10, 132, 255, 0.15)' }]}>
                      <MessageSquare color="#0A84FF" size={13} />
                      <Text style={[styles.badgeText, { color: '#0A84FF' }]}>{stats.gorusme}</Text>
                    </View>

                    {/* Fiyat */}
                    <View style={[styles.badge, { backgroundColor: 'rgba(48, 209, 88, 0.15)' }]}>
                      <Tag color="#30D158" size={13} />
                      <Text style={[styles.badgeText, { color: '#30D158' }]}>{stats.fiyat}</Text>
                    </View>

                    {/* Görsel */}
                    <View style={[styles.badge, { backgroundColor: 'rgba(191, 90, 242, 0.15)' }]}>
                      <ImageIcon color="#BF5AF2" size={13} />
                      <Text style={[styles.badgeText, { color: '#BF5AF2' }]}>{stats.gorsel}</Text>
                    </View>

                    {/* Not */}
                    <View style={[styles.badge, { backgroundColor: 'rgba(255, 159, 10, 0.15)' }]}>
                      <FileText color="#FF9F0A" size={13} />
                      <Text style={[styles.badgeText, { color: '#FF9F0A' }]}>{stats.not}</Text>
                    </View>
                  </View>

                  <Text style={styles.dateText}>
                    {formatDate(item.sonIslemTarihi || item.olusturmaTarihi)}
                  </Text>
                </View>
              </TouchableOpacity>
            );
          }}
        />
      )}

      {/* 2. GÖRSELDEKİ ÖZELLİK: ETİKET SEÇİM AÇILIR MENÜSÜ (DROPDOWN / COMBOBOX) */}
      <Modal
        visible={isEtiketDropdownOpen}
        transparent={true}
        animationType="fade"
        onRequestClose={() => setIsEtiketDropdownOpen(false)}
      >
        <TouchableOpacity
          style={styles.dropdownOverlay}
          activeOpacity={1}
          onPress={() => setIsEtiketDropdownOpen(false)}
        >
          <View style={styles.dropdownModalBox}>
            <View style={styles.dropdownModalHeader}>
              <Text style={styles.dropdownModalTitle}>Durum Filtresi</Text>
              <TouchableOpacity
                onPress={() => setIsEtiketDropdownOpen(false)}
                hitSlop={{ top: 8, bottom: 8, left: 8, right: 8 }}
              >
                <X color="#94A3B8" size={18} />
              </TouchableOpacity>
            </View>

            {ETIKETLER.map((etiket) => {
              const isSelected = selectedEtiket === etiket;
              return (
                <TouchableOpacity
                  key={etiket}
                  style={[
                    styles.dropdownOptionItem,
                    isSelected && styles.dropdownOptionItemActive,
                  ]}
                  onPress={() => {
                    triggerSelectionHaptic();
                    setSelectedEtiket(etiket);
                    setIsEtiketDropdownOpen(false);
                  }}
                  activeOpacity={0.7}
                >
                  <Text
                    style={[
                      styles.dropdownOptionText,
                      isSelected && styles.dropdownOptionTextActive,
                    ]}
                  >
                    {etiket}
                  </Text>
                  {isSelected && <Check color="#0A84FF" size={18} />}
                </TouchableOpacity>
              );
            })}
          </View>
        </TouchableOpacity>
      </Modal>

      {/* CARİ HESAP SEÇİM MODALI */}
      <Modal
        visible={isCariModalOpen}
        animationType="slide"
        transparent={true}
        onRequestClose={() => {
          Keyboard.dismiss();
          setIsCariModalOpen(false);
        }}
      >
        <KeyboardAvoidingView
          style={styles.modalOverlay}
          behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        >
          <SafeAreaView style={styles.modalContent}>
            {/* Modal Header */}
            <View style={styles.modalHeader}>
              <View style={styles.modalHeaderTitleRow}>
                <View style={styles.modalIconBox}>
                  <Users color="#0A84FF" size={20} />
                </View>
                <View>
                  <Text style={styles.modalTitle}>Müşteri Seç & Aktar</Text>
                  <Text style={styles.modalSubtitle}>Takip klasörü oluşturulacak cariyi seçin</Text>
                </View>
              </View>
              <TouchableOpacity
                style={styles.modalCloseBtn}
                onPress={() => {
                  Keyboard.dismiss();
                  setIsCariModalOpen(false);
                }}
              >
                <X color="#94A3B8" size={20} />
              </TouchableOpacity>
            </View>

            {/* Modal Arama Çubuğu */}
            <View style={styles.modalSearchBox}>
              <Search color="#64748B" size={18} />
              <TextInput
                style={styles.modalSearchInput}
                placeholder="Cari Ünvanı, Kodu veya Telefon ile ara..."
                placeholderTextColor="#64748B"
                value={cariSearchText}
                onChangeText={setCariSearchText}
                autoFocus={false}
                returnKeyType="search"
                onSubmitEditing={() => Keyboard.dismiss()}
              />
              {cariSearchText.length > 0 && (
                <TouchableOpacity
                  onPress={() => {
                    setCariSearchText('');
                    Keyboard.dismiss();
                  }}
                  hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
                >
                  <X color="#94A3B8" size={18} />
                </TouchableOpacity>
              )}
            </View>

            {/* Cari Listesi */}
            <FlatList
              data={filteredCariler}
              keyExtractor={(c) => c.id?.toString() || c.cariKod || Math.random().toString()}
              contentContainerStyle={styles.modalListContent}
              keyboardShouldPersistTaps="handled"
              keyboardDismissMode="on-drag"
              ListEmptyComponent={
                <View style={{ alignItems: 'center', justifyContent: 'center', paddingVertical: 40, paddingHorizontal: 20 }}>
                  <Users color="#64748B" size={40} style={{ marginBottom: 12, opacity: 0.7 }} />
                  <Text style={{ color: '#E2E8F0', fontWeight: 'bold', fontSize: 15, marginBottom: 6, textAlign: 'center' }}>
                    Aktarılacak Cari Bulunamadı
                  </Text>
                  <Text style={{ color: '#64748B', fontSize: 12, textAlign: 'center', lineHeight: 18 }}>
                    {cariSearchText.trim()
                      ? 'Aramanıza uygun cari kaydı bulunamadı.'
                      : 'Tüm kayıtlı cariler zaten müşteri takip listesine aktarılmış.'}
                  </Text>
                </View>
              }
              renderItem={({ item }) => (
                <TouchableOpacity
                  style={styles.cariItem}
                  onPress={() => {
                    Keyboard.dismiss();
                    handleSelectCariAndCreate(item);
                  }}
                  activeOpacity={0.7}
                >
                  <View style={styles.cariItemLeft}>
                    <Text style={styles.cariItemUnvan} numberOfLines={1}>
                      {item.unvan}
                    </Text>
                    <View style={styles.cariItemMetaRow}>
                      {item.cariKod ? (
                        <Text style={styles.cariItemCode}>#{item.cariKod}</Text>
                      ) : null}
                      {item.telefon ? (
                        <Text style={styles.cariItemPhone}>📞 {item.telefon}</Text>
                      ) : null}
                    </View>
                  </View>

                  <View style={styles.cariItemAction}>
                    <Text style={styles.cariItemActionText}>Seç & Aktar</Text>
                    <ChevronRight color="#30D158" size={16} />
                  </View>
                </TouchableOpacity>
              )}
            />
          </SafeAreaView>
          <KeyboardDismissBar inModal={true} />
        </KeyboardAvoidingView>
      </Modal>
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
    marginRight: 12,
  },
  headerTitles: {
    flex: 1,
  },
  title: {
    fontSize: 18,
    fontWeight: '800',
    color: '#FFFFFF',
  },
  subtitle: {
    fontSize: 12,
    color: '#94A3B8',
    marginTop: 2,
  },
  // Araç Çubuğu (Toolbar - Masaüstü ile 1:1)
  toolbarCard: {
    backgroundColor: 'rgba(24, 24, 28, 0.85)',
    borderRadius: 16,
    padding: 12,
    marginHorizontal: 16,
    marginTop: 12,
    marginBottom: 6,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    gap: 10,
  },
  searchRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  searchBox: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.06)',
    borderRadius: 10,
    paddingHorizontal: 10,
    height: 40,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
  },
  searchInput: {
    flex: 1,
    marginLeft: 6,
    color: '#FFFFFF',
    fontSize: 13,
    paddingVertical: 0,
  },
  dropdownBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: 'rgba(255, 255, 255, 0.06)',
    borderRadius: 10,
    paddingHorizontal: 10,
    height: 40,
    minWidth: 115,
    maxWidth: 145,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.1)',
    gap: 4,
  },
  dropdownBtnText: {
    color: '#FFFFFF',
    fontSize: 12,
    fontWeight: '600',
    flex: 1,
  },
  addMusteriBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#30D158',
    height: 40,
    borderRadius: 10,
    gap: 6,
  },
  addMusteriBtnText: {
    color: '#FFFFFF',
    fontWeight: '700',
    fontSize: 13,
    letterSpacing: 0.3,
  },

  // 2. Görseldeki Açılır Liste (Dropdown ComboBox) Stilleri
  dropdownOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.75)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: 24,
  },
  dropdownModalBox: {
    width: '100%',
    maxWidth: 320,
    backgroundColor: '#161922',
    borderRadius: 16,
    padding: 8,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.15)',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 10 },
    shadowOpacity: 0.5,
    shadowRadius: 20,
    elevation: 10,
  },
  dropdownModalHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.08)',
    marginBottom: 4,
  },
  dropdownModalTitle: {
    fontSize: 12,
    fontWeight: '700',
    color: '#94A3B8',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  dropdownOptionItem: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingVertical: 11,
    paddingHorizontal: 14,
    borderRadius: 10,
  },
  dropdownOptionItemActive: {
    backgroundColor: 'rgba(10, 132, 255, 0.15)',
  },
  dropdownOptionText: {
    fontSize: 13.5,
    color: '#CBD5E1',
    fontWeight: '500',
  },
  dropdownOptionTextActive: {
    color: '#FFFFFF',
    fontWeight: '700',
  },
  centerBox: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  loadingText: {
    color: '#94A3B8',
    marginTop: 12,
    fontSize: 14,
  },
  emptyContainer: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 32,
  },
  emptyIconCircle: {
    width: 80,
    height: 80,
    borderRadius: 40,
    backgroundColor: 'rgba(255, 255, 255, 0.05)',
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 16,
  },
  emptyTitle: {
    fontSize: 17,
    fontWeight: '700',
    color: '#E2E8F0',
    marginBottom: 6,
  },
  emptyDesc: {
    fontSize: 13,
    color: '#64748B',
    textAlign: 'center',
    marginBottom: 20,
    lineHeight: 18,
  },
  emptyActionBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#0A84FF',
    paddingVertical: 10,
    paddingHorizontal: 18,
    borderRadius: 12,
    gap: 8,
  },
  emptyActionBtnText: {
    color: '#FFFFFF',
    fontWeight: '700',
    fontSize: 13,
  },
  listContent: {
    padding: 16,
    paddingBottom: 90,
    gap: 12,
  },
  card: {
    backgroundColor: 'rgba(24, 24, 28, 0.85)',
    borderRadius: 16,
    padding: 15,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
  },
  cardHeader: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  folderIconBox: {
    width: 38,
    height: 38,
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: 10,
  },
  cardHeaderInfo: {
    flex: 1,
    marginRight: 8,
  },
  cariUnvan: {
    fontSize: 15,
    fontWeight: '700',
    color: '#FFFFFF',
  },
  etiketBadge: {
    alignSelf: 'flex-start',
    backgroundColor: 'rgba(10, 132, 255, 0.15)',
    paddingHorizontal: 7,
    paddingVertical: 2.5,
    borderRadius: 6,
    marginTop: 4,
  },
  etiketBadgeText: {
    fontSize: 10.5,
    fontWeight: '700',
    color: '#0A84FF',
  },
  cardDeleteBtn: {
    width: 32,
    height: 32,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 8,
    backgroundColor: 'rgba(255, 69, 58, 0.1)',
  },
  cardBody: {
    marginTop: 10,
    gap: 4,
  },
  metaRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  metaText: {
    fontSize: 12,
    color: '#CBD5E1',
  },
  cardFooter: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    borderTopWidth: 1,
    borderTopColor: 'rgba(255, 255, 255, 0.06)',
    paddingTop: 10,
    marginTop: 10,
  },
  badgesRow: {
    flexDirection: 'row',
    gap: 6,
  },
  badge: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 7,
    paddingVertical: 3,
    borderRadius: 6,
    gap: 4,
  },
  badgeText: {
    fontSize: 11,
    fontWeight: '700',
  },
  dateText: {
    fontSize: 10,
    color: '#64748B',
  },

  // Modal Styles
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.85)',
  },
  modalContent: {
    flex: 1,
    backgroundColor: '#121216',
    borderTopLeftRadius: 20,
    borderTopRightRadius: 20,
    marginTop: Platform.OS === 'ios' ? 50 : 25,
    overflow: 'hidden',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
  },
  modalHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: 16,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.08)',
  },
  modalHeaderTitleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  modalIconBox: {
    width: 36,
    height: 36,
    borderRadius: 10,
    backgroundColor: 'rgba(10, 132, 255, 0.15)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  modalTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: '#FFFFFF',
  },
  modalSubtitle: {
    fontSize: 11,
    color: '#94A3B8',
  },
  modalCloseBtn: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  modalSearchBox: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.06)',
    borderRadius: 12,
    marginHorizontal: 16,
    marginVertical: 12,
    paddingHorizontal: 12,
    height: 42,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
  },
  modalSearchInput: {
    flex: 1,
    marginLeft: 8,
    color: '#FFFFFF',
    fontSize: 13,
  },
  modalListContent: {
    paddingHorizontal: 16,
    paddingBottom: 20,
    gap: 8,
  },
  cariItem: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: 'rgba(28, 28, 34, 0.8)',
    padding: 13,
    borderRadius: 14,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.06)',
  },
  cariItemLeft: {
    flex: 1,
    marginRight: 10,
  },
  cariItemUnvan: {
    fontSize: 14,
    fontWeight: '700',
    color: '#FFFFFF',
  },
  cariItemMetaRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    marginTop: 3,
  },
  cariItemCode: {
    fontSize: 11,
    color: '#0A84FF',
    fontWeight: '600',
  },
  cariItemPhone: {
    fontSize: 11,
    color: '#94A3B8',
  },
  cariItemAction: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(48, 209, 88, 0.15)',
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: 8,
    gap: 4,
  },
  cariItemActionText: {
    fontSize: 11,
    fontWeight: '700',
    color: '#30D158',
  },
});
