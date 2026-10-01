import React, { useState, useEffect } from 'react';
import { View, Text, TextInput, TouchableOpacity, ScrollView, StyleSheet, Switch, Alert, ActivityIndicator } from 'react-native';
import { useNavigation } from '@react-navigation/native';
import { Feather } from '@expo/vector-icons';
import { readData, writeData } from '../services/firebase';

export default function FaturaTasarimScreen() {
  const navigation = useNavigation();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [tasarim, setTasarim] = useState({
    kagitBoyutu: 'A4',
    yon: 'Dikey',
    ustBaslik: '',
    altBilgi: '',
    logoyuGoster: true,
    imzayiGoster: true,
    faturaNoGoster: true,
    barkodGoster: false
  });

  useEffect(() => {
    loadTasarim();
  }, []);

  const loadTasarim = async () => {
    try {
      const data = await readData('FaturaTasarimi');
      if (data) {
        setTasarim({ ...tasarim, ...data });
      }
    } catch (error) {
      console.warn("Tasarım yüklenemedi:", error);
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      await writeData('FaturaTasarimi', tasarim);
      Alert.alert('Başarılı', 'Fatura tasarım ayarları kaydedildi.');
    } catch (error) {
      Alert.alert('Hata', 'Kaydedilirken bir hata oluştu.');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <View style={[styles.container, { justifyContent: 'center', alignItems: 'center' }]}>
        <ActivityIndicator size="large" color="#0061FF" />
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <TouchableOpacity style={styles.backButton} onPress={() => navigation.goBack()}>
          <Feather name="arrow-left" size={24} color="#F8FAFC" />
        </TouchableOpacity>
        <Text style={styles.headerTitle}>Fatura Tasarımı</Text>
        <TouchableOpacity style={styles.saveButton} onPress={handleSave} disabled={saving}>
          {saving ? <ActivityIndicator size="small" color="#FFF" /> : <Feather name="check" size={24} color="#F8FAFC" />}
        </TouchableOpacity>
      </View>

      <ScrollView style={styles.content}>
        <Text style={styles.sectionTitle}>Görünüm ve Boyut</Text>
        
        <View style={styles.card}>
          <Text style={styles.label}>Kağıt Boyutu</Text>
          <View style={styles.row}>
            {['A4', 'A5'].map((size) => (
              <TouchableOpacity
                key={size}
                style={[styles.segmentButton, tasarim.kagitBoyutu === size && styles.segmentButtonActive]}
                onPress={() => setTasarim({ ...tasarim, kagitBoyutu: size })}
              >
                <Text style={[styles.segmentText, tasarim.kagitBoyutu === size && styles.segmentTextActive]}>{size}</Text>
              </TouchableOpacity>
            ))}
          </View>
        </View>

        <View style={styles.card}>
          <Text style={styles.label}>Sayfa Yönü</Text>
          <View style={styles.row}>
            {['Dikey', 'Yatay'].map((dir) => (
              <TouchableOpacity
                key={dir}
                style={[styles.segmentButton, tasarim.yon === dir && styles.segmentButtonActive]}
                onPress={() => setTasarim({ ...tasarim, yon: dir })}
              >
                <Text style={[styles.segmentText, tasarim.yon === dir && styles.segmentTextActive]}>{dir}</Text>
              </TouchableOpacity>
            ))}
          </View>
        </View>

        <Text style={styles.sectionTitle}>İçerik ve Metinler</Text>

        <View style={styles.card}>
          <Text style={styles.label}>Üst Başlık (Örn: FİRMA ÜNVANI)</Text>
          <TextInput
            style={styles.input}
            value={tasarim.ustBaslik}
            onChangeText={(t) => setTasarim({ ...tasarim, ustBaslik: t })}
            placeholder="Faturanın en üstünde çıkacak metin"
            placeholderTextColor="#64748B"
          />
        </View>

        <View style={styles.card}>
          <Text style={styles.label}>Alt Bilgi / Not</Text>
          <TextInput
            style={[styles.input, { height: 80, textAlignVertical: 'top' }]}
            value={tasarim.altBilgi}
            onChangeText={(t) => setTasarim({ ...tasarim, altBilgi: t })}
            placeholder="Banka hesap bilgileri vb."
            placeholderTextColor="#64748B"
            multiline
          />
        </View>

        <Text style={styles.sectionTitle}>Gösterim Ayarları</Text>

        <View style={styles.card}>
          <View style={styles.switchRow}>
            <Text style={styles.switchLabel}>Firma Logosu Göster</Text>
            <Switch
              value={tasarim.logoyuGoster}
              onValueChange={(v) => setTasarim({ ...tasarim, logoyuGoster: v })}
              trackColor={{ false: '#334155', true: '#2563EB' }}
              thumbColor="#F8FAFC"
            />
          </View>
          <View style={styles.switchRow}>
            <Text style={styles.switchLabel}>Fatura Numarası Göster</Text>
            <Switch
              value={tasarim.faturaNoGoster}
              onValueChange={(v) => setTasarim({ ...tasarim, faturaNoGoster: v })}
              trackColor={{ false: '#334155', true: '#2563EB' }}
              thumbColor="#F8FAFC"
            />
          </View>
          <View style={styles.switchRow}>
            <Text style={styles.switchLabel}>Barkod / QR Göster</Text>
            <Switch
              value={tasarim.barkodGoster}
              onValueChange={(v) => setTasarim({ ...tasarim, barkodGoster: v })}
              trackColor={{ false: '#334155', true: '#2563EB' }}
              thumbColor="#F8FAFC"
            />
          </View>
          <View style={styles.switchRow}>
            <Text style={styles.switchLabel}>Kaşe/İmza Göster</Text>
            <Switch
              value={tasarim.imzayiGoster}
              onValueChange={(v) => setTasarim({ ...tasarim, imzayiGoster: v })}
              trackColor={{ false: '#334155', true: '#2563EB' }}
              thumbColor="#F8FAFC"
            />
          </View>
        </View>
        
        <View style={{ height: 40 }} />
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#0A0A0A' },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 20,
    paddingTop: 50,
    paddingBottom: 20,
    backgroundColor: '#0F172A',
    borderBottomWidth: 1,
    borderBottomColor: '#1E293B'
  },
  backButton: { padding: 8, marginLeft: -8 },
  headerTitle: { fontSize: 20, fontWeight: 'bold', color: '#F8FAFC' },
  saveButton: { padding: 8, marginRight: -8 },
  content: { flex: 1, padding: 20 },
  sectionTitle: { fontSize: 14, fontWeight: '600', color: '#64748B', marginTop: 15, marginBottom: 10, textTransform: 'uppercase' },
  card: { backgroundColor: '#1E293B', borderRadius: 12, padding: 15, marginBottom: 15, borderWidth: 1, borderColor: '#334155' },
  label: { fontSize: 14, color: '#94A3B8', marginBottom: 10 },
  row: { flexDirection: 'row', gap: 10 },
  segmentButton: { flex: 1, paddingVertical: 10, alignItems: 'center', backgroundColor: '#0F172A', borderRadius: 8, borderWidth: 1, borderColor: '#334155' },
  segmentButtonActive: { backgroundColor: '#2563EB', borderColor: '#3B82F6' },
  segmentText: { color: '#94A3B8', fontWeight: '500' },
  segmentTextActive: { color: '#F8FAFC', fontWeight: 'bold' },
  input: { backgroundColor: '#0F172A', color: '#F8FAFC', borderRadius: 8, padding: 12, borderWidth: 1, borderColor: '#334155', fontSize: 15 },
  switchRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingVertical: 10, borderBottomWidth: 1, borderBottomColor: '#0F172A' },
  switchLabel: { fontSize: 15, color: '#F8FAFC' }
});
