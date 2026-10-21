import React from 'react';
import { StyleSheet, Text, View, SafeAreaView, ScrollView, TouchableOpacity } from 'react-native';
import { 
  Settings, 
  BarChart3, 
  CreditCard, 
  Box, 
  PieChart, 
  Info, 
  CalendarDays, 
  LayoutGrid, 
  Calculator, 
  Wrench, 
  Folder, 
  ChevronRight,
  FileText,
  Package
} from 'lucide-react-native';
import { useNavigation } from '@react-navigation/native';

export default function DahaFazlaScreen() {
  const navigation = useNavigation<any>();

  const menuItems = [
    { title: 'Faturalar', route: 'Faturalar', icon: FileText, color: '#30D158' },
    { title: 'Stoklar', route: 'Stoklar', icon: Package, color: '#0A84FF' },
    { title: 'Müşteri Takip', route: 'MusteriTakip', icon: Folder, color: '#3B82F6' },
    { title: 'Finans', route: 'Finans', icon: CreditCard, color: '#FF416C' },
    { title: 'Vade Takibi', route: 'VadeTakip', icon: CalendarDays, color: '#8B5CF6' },
    { title: 'Raporlar', route: 'Raporlar', icon: PieChart, color: '#0061FF' },
    { title: 'Görev Panosu', route: 'Kanban', icon: LayoutGrid, color: '#00FF87' },
    { title: 'Hesap Makinası', route: 'MaliyetHesaplama', icon: Calculator, color: '#0D9488' },
    { title: 'Araçlar', route: 'Araclar', icon: Wrench, color: '#F59E0B' },
    { title: 'Ayarlar', route: 'Ayarlar', icon: Settings, color: '#94A3B8' },
  ];

  return (
    <SafeAreaView style={styles.container}>
      <ScrollView contentContainerStyle={styles.content} showsVerticalScrollIndicator={false}>
        <Text style={styles.headerTitle}>Diğer Modüller</Text>
        <View style={styles.menuContainer}>
          {menuItems.map((item, idx) => {
            const Icon = item.icon;
            const isLast = idx === menuItems.length - 1;
            return (
              <TouchableOpacity 
                key={idx} 
                style={[styles.menuItem, isLast && { borderBottomWidth: 0 }]}
                onPress={() => navigation.navigate(item.route)}
                activeOpacity={0.7}
              >
                <View style={styles.menuItemLeft}>
                  <View style={[styles.iconBox, { backgroundColor: `${item.color}15` }]}>
                    <Icon color={item.color} size={22} />
                  </View>
                  <Text style={styles.menuTitle}>{item.title}</Text>
                </View>
                <ChevronRight color="#64748B" size={18} />
              </TouchableOpacity>
            );
          })}
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#000000',
  },
  content: {
    padding: 16,
    paddingTop: 30,
    paddingBottom: 110,
  },
  headerTitle: {
    fontSize: 24,
    fontWeight: '900',
    color: '#FFFFFF',
    letterSpacing: -0.5,
    marginBottom: 20,
    paddingHorizontal: 4,
  },
  menuContainer: {
    backgroundColor: '#121216',
    borderRadius: 20,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    overflow: 'hidden',
  },
  menuItem: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingVertical: 14,
    paddingHorizontal: 16,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.06)',
  },
  menuItemLeft: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  iconBox: {
    width: 38,
    height: 38,
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: 14,
  },
  menuTitle: {
    fontSize: 15,
    fontWeight: '600',
    color: '#E2E8F0',
  },
});
