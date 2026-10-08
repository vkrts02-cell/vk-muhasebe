import React, { useState, useEffect } from 'react';
import { StyleSheet, Text, View, SafeAreaView, ScrollView, TouchableOpacity, ActivityIndicator, TextInput, Alert, Modal, FlatList, KeyboardAvoidingView, Platform } from 'react-native';
import { ClipboardList, Plus, X, Save, CheckCircle2, RefreshCw } from 'lucide-react-native';
import { subscribeToPath, writeData, readData } from '../services/firebase';
import { generateInt32Id } from '../utils/IdGenerator';
import {
  KeyboardDoneAccessory,
  KEYBOARD_ACCESSORY_ID,
} from '../components/KeyboardDoneAccessory';

export default function StokSayimScreen() {
  const [stoklar, setStoklar] = useState<any[]>([]);
  const [fisler, setFisler] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  const [isFormOpen, setIsFormOpen] = useState(false);
  const [fisAciklama, setFisAciklama] = useState('');
  const [sayilan, setSayilan] = useState<Record<string, string>>({});
  const [expandedFis, setExpandedFis] = useState<string | null>(null);
  const [fisDetaylar, setFisDetaylar] = useState<Record<string, any[]>>({});

  useEffect(() => {
    const unsubS = subscribeToPath('Stoklar', (data) => {
      if (data) {
        const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map((key) => ({ ...data[key], firebaseKey: key }));
        setStoklar(list.filter((s) => !s.isDeleted));
      }
    });
    const unsubF = subscribeToPath('StokSayimlar', async (data) => {
      if (data) {
        const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map((key) => ({ ...data[key], firebaseKey: key }));
        setFisler(list.sort((a, b) => String(b.tarih || '').localeCompare(String(a.tarih || ''))));
        // detayları yükle
        const detayMap: Record<string, any[]> = {};
        for (const fis of list) {
          const d = await readData(`StokSayimDetaylar/${fis.id}`);
          detayMap[fis.id] = Array.isArray(d) ? d.filter(Boolean) : d ? Object.keys(d).map((k) => ({ ...d[k], firebaseKey: k })) : [];
        }
        setFisDetaylar(detayMap);
      }
      setLoading(false);
    });
    return () => { unsubS(); unsubF(); };
  }, []);

  const openForm = () => {
    const s: Record<string, string> = {};
    stoklar.forEach((st) => { s[st.id] = String(st.miktar ?? '0'); });
    setSayilan(s);
    setFisAciklama('');
    setIsFormOpen(true);
  };

  const handleSaveFis = async () => {
    const id = generateInt32Id().toString();
    const fis = {
      id,
      fisNo: `SYM-${id.slice(-6)}`,
      tarih: new Date().toISOString().split('T')[0],
      aciklama: fisAciklama.trim(),
      sayimYapan: 'Mobil',
      isApplied: false,
    };
    const detaylar = stoklar.map((st) => {
      const mevcut = st.miktar || 0;
      const sayilanVal = parseFloat(sayilan[st.id] || '0') || 0;
      return {
        fisId: id,
        stokId: st.id,
        stokKodu: st.stokKodu || '',
        stokAdi: st.urunAdi || st.ad || st.stokAdi || '',
        mevcutMiktar: mevcut,
        sayilanMiktar: sayilanVal,
        fark: sayilanVal - mevcut,
      };
    });
    const okFis = await writeData(`StokSayimlar/${id}`, fis);
    const okDetay = await writeData(`StokSayimDetaylar/${id}`, detaylar);
    if (!okFis || !okDetay) {
      Alert.alert('Uyarı', 'Sayım fişi kaydedilemedi. (Bağlantı sorunu — fiş sıraya alındı.)');
      return;
    }
    const dmap = { ...fisDetaylar, [id]: detaylar };
    setFisDetaylar(dmap);
    setIsFormOpen(false);
    setExpandedFis(id);
    Alert.alert('Başarılı', 'Sayım fişi kaydedildi. Uygula diyerek stoklara işleyebilirsiniz.');
  };

  const handleApply = async (fis: any) => {
    Alert.alert('Sayımı Uygula', 'Sayım sonuçları stok miktarlarına işlenecek. Devam edilsin mi?', [
      { text: 'İptal', style: 'cancel' },
      {
        text: 'Uygula',
        onPress: async () => {
          const detaylar = fisDetaylar[fis.id] || [];
          for (const d of detaylar) {
            const st = stoklar.find((s) => String(s.id) === String(d.stokId));
            if (st) {
              const ok = await writeData(`Stoklar/${d.stokId}`, {
                ...st,
                miktar: Math.max(0, d.sayilanMiktar),
              });
              if (!ok) {
                Alert.alert('Uyarı', `"${st.stokAdi || d.stokId}" stok miktarı işlenemedi. (Bağlantı sorunu — miktar sıraya alındı.)`);
              }
            }
          }
          const okUygula = await writeData(`StokSayimlar/${fis.id}`, { ...fis, isApplied: true });
          if (!okUygula) {
            Alert.alert('Uyarı', 'Sayım stoklara işlendi ancak fiş durumu güncellenemedi. (Bağlantı sorunu — kayıt sıraya alındı.)');
            return;
          }
          Alert.alert('Başarılı', 'Sayım stoklara işlendi.');
        },
      },
    ]);
  };

  const toggleFis = (id: string) => {
    setExpandedFis(expandedFis === id ? null : id);
  };

  const renderItem = ({ item }: any) => (
    <View style={styles.row}>
      <Text style={styles.rowAd}>{item.stokAdi} ({item.stokKodu})</Text>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
        <Text style={{ color: '#94A3B8', fontSize: 12 }}>Mevcut: {item.mevcutMiktar}</Text>
        <TextInput
          style={styles.sayimInput}
          keyboardType="numeric"
          inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
          value={sayilan[item.stokId] ?? String(item.mevcutMiktar ?? 0)}
          onChangeText={(v) => setSayilan((prev) => ({ ...prev, [item.stokId]: v }))}
          placeholderTextColor="#64748B"
        />
      </View>
    </View>
  );

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.header}>
        <View style={{ flexDirection: 'row', alignItems: 'center', marginBottom: 12 }}>
          <ClipboardList color="#00FF87" size={24} style={{ marginRight: 8 }} />
          <Text style={styles.headerTitle}>Stok Sayımı</Text>
        </View>
        <Text style={styles.headerSub}>{fisler.filter((f) => f.isApplied).length}/{fisler.length} fiş uygulandı</Text>
      </View>

      <ScrollView contentContainerStyle={styles.content}>
        <TouchableOpacity style={styles.addButton} onPress={openForm}>
          <Plus color="#FFF" size={20} />
          <Text style={styles.addButtonText}>Yeni Sayım Fişi</Text>
        </TouchableOpacity>

        {loading ? (
          <ActivityIndicator size="large" color="#00FF87" style={{ marginTop: 40 }} />
        ) : fisler.length === 0 ? (
          <Text style={styles.emptyText}>Henüz sayım fişi yok.</Text>
        ) : (
          fisler.map((fis, idx) => (
            <View key={idx} style={styles.card}>
              <TouchableOpacity style={styles.cardHeader} onPress={() => toggleFis(fis.id)}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.cardTitle}>{fis.fisNo || fis.id}</Text>
                  <Text style={styles.cardSub}>{fis.tarih} • {fis.aciklama || 'Açıklama yok'} • {(fisDetaylar[fis.id] || []).length} kalem</Text>
                </View>
                <View style={[styles.statusBadge, { backgroundColor: fis.isApplied ? 'rgba(0,255,135,0.12)' : 'rgba(245,158,11,0.12)' }]}>
                  <Text style={{ color: fis.isApplied ? '#00FF87' : '#F59E0B', fontSize: 11, fontWeight: 'bold' }}>
                    {fis.isApplied ? 'UYGULANDI' : 'BEKLEMEDE'}
                  </Text>
                </View>
              </TouchableOpacity>
              {expandedFis === fis.id && (
                <View style={{ marginTop: 10 }}>
                  {(fisDetaylar[fis.id] || []).slice(0, 8).map((d, i) => (
                    <View key={i} style={styles.detayRow}>
                      <Text style={{ color: '#94A3B8', fontSize: 12, flex: 1 }} numberOfLines={1}>{d.stokAdi}</Text>
                      <Text style={{ color: '#F59E0B', fontSize: 12 }}>{d.mevcutMiktar} → {d.sayilanMiktar}</Text>
                      <Text style={{ color: d.fark === 0 ? '#64748B' : d.fark > 0 ? '#00FF87' : '#EF4444', fontSize: 12, marginLeft: 8, width: 50, textAlign: 'right' }}>
                        {d.fark > 0 ? `+${d.fark}` : d.fark}
                      </Text>
                    </View>
                  ))}
                  {!fis.isApplied && (
                    <TouchableOpacity style={styles.applyButton} onPress={() => handleApply(fis)}>
                      <CheckCircle2 color="#FFF" size={18} />
                      <Text style={styles.applyText}>Stoklara Uygula</Text>
                    </TouchableOpacity>
                  )}
                </View>
              )}
            </View>
          ))
        )}
      </ScrollView>

      {/* Yeni Sayım Modalı */}
      <Modal visible={isFormOpen} animationType="slide" transparent>
        <SafeAreaView style={styles.modalOverlay}>
          <KeyboardAvoidingView
            style={{ flex: 1 }}
            behavior={Platform.OS === 'ios' ? 'padding' : undefined}
          >
            <View style={styles.modalContent}>
              <View style={styles.modalHeader}>
                <Text style={styles.modalTitle}>Yeni Sayım Fişi</Text>
                <TouchableOpacity
                  onPress={() => setIsFormOpen(false)}
                  hitSlop={{ top: 12, bottom: 12, left: 12, right: 12 }}
                >
                  <X color="#FFF" size={24} />
                </TouchableOpacity>
              </View>
              <Text style={styles.label}>Açıklama</Text>
              <TextInput style={styles.input} value={fisAciklama} onChangeText={setFisAciklama} placeholder="Sayım notu..." placeholderTextColor="#64748B" />
              <Text style={styles.label}>Sayılan Miktarlar ({stoklar.length} kalem)</Text>
              <FlatList initialNumToRender={20} maxToRenderPerBatch={20} windowSize={5}
                data={stoklar}
                keyExtractor={(it) => String(it.id)}
                renderItem={renderItem}
                contentContainerStyle={{ paddingBottom: 20 }}
              />
              <TouchableOpacity style={styles.saveButton} onPress={handleSaveFis}>
                <Save color="#FFF" size={18} />
                <Text style={styles.saveText}>Fişi Kaydet</Text>
              </TouchableOpacity>
            </View>
          </KeyboardAvoidingView>
        </SafeAreaView>
      </Modal>
      <KeyboardDoneAccessory />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#0A0A0A' },
  header: { padding: 20, paddingTop: 40, borderBottomWidth: 1, borderBottomColor: 'rgba(255,255,255,0.08)' },
  headerTitle: { fontSize: 20, fontWeight: '900', color: '#FFF', flex: 1 },
  headerSub: { color: '#64748B', fontSize: 13, marginTop: 4 },
  content: { padding: 20 },
  addButton: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', backgroundColor: '#10B981', borderRadius: 12, padding: 14, marginBottom: 16 },
  addButtonText: { color: '#FFF', fontWeight: 'bold', fontSize: 15, marginLeft: 8 },
  emptyText: { color: '#64748B', textAlign: 'center', marginTop: 40 },
  card: { backgroundColor: '#0F0F0F', borderRadius: 16, borderWidth: 1, borderColor: 'rgba(255,255,255,0.08)', padding: 16, marginBottom: 12 },
  cardHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  cardTitle: { fontSize: 15, fontWeight: 'bold', color: '#FFF' },
  cardSub: { color: '#64748B', fontSize: 12, marginTop: 2 },
  statusBadge: { paddingHorizontal: 10, paddingVertical: 4, borderRadius: 8 },
  detayRow: { flexDirection: 'row', alignItems: 'center', marginTop: 6 },
  applyButton: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', backgroundColor: '#10B981', borderRadius: 10, padding: 12, marginTop: 14 },
  applyText: { color: '#FFF', fontWeight: 'bold', fontSize: 13, marginLeft: 6 },
  modalOverlay: { flex: 1, backgroundColor: 'rgba(0,0,0,0.85)' },
  modalContent: { flex: 1, backgroundColor: '#0A0A0A', padding: 20 },
  modalHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 },
  modalTitle: { fontSize: 18, fontWeight: '900', color: '#FFF' },
  label: { color: '#94A3B8', fontSize: 13, marginBottom: 6, marginTop: 10 },
  input: { backgroundColor: '#2A2A2A', borderRadius: 12, padding: 14, color: '#FFF', fontSize: 15, borderWidth: 1, borderColor: '#444' },
  row: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', paddingVertical: 10, borderBottomWidth: 1, borderBottomColor: 'rgba(255,255,255,0.05)' },
  rowAd: { color: '#E2E8F0', fontSize: 14, flex: 1, marginRight: 6 },
  sayimInput: { backgroundColor: '#2A2A2A', borderRadius: 8, paddingHorizontal: 8, paddingVertical: 4, color: '#FFF', width: 70, textAlign: 'center', fontWeight: 'bold' },
  saveButton: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', backgroundColor: '#10B981', borderRadius: 12, padding: 14, marginBottom: 20 },
  saveText: { color: '#FFF', fontWeight: 'bold', fontSize: 15, marginLeft: 8 },
});
