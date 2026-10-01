import React from 'react';
import { Platform } from 'react-native';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { NavigationContainer, DarkTheme } from '@react-navigation/native';
import { Home, Users, FileText, Settings, Package } from 'lucide-react-native';

// Screens
import DashboardScreen from '../screens/DashboardScreen';
import CarilerScreen from '../screens/CarilerScreen';
import FaturalarScreen from '../screens/FaturalarScreen';
import StoklarScreen from '../screens/StoklarScreen';
import DahaFazlaScreen from '../screens/DahaFazlaScreen';
import FinansScreen from '../screens/FinansScreen';
import SiparislerScreen from '../screens/SiparislerScreen';
import TekliflerScreen from '../screens/TekliflerScreen';
import VadeTakipScreen from '../screens/VadeTakipScreen';
import RaporlarScreen from '../screens/RaporlarScreen';
import RaporDetayScreen from '../screens/RaporDetayScreen';
import AyarlarScreen from '../screens/AyarlarScreen';
import KanbanScreen from '../screens/KanbanScreen';
import MaliyetHesaplamaScreen from '../screens/MaliyetHesaplamaScreen';
import AraclarScreen from '../screens/AraclarScreen';
import StokGrupScreen from '../screens/StokGrupScreen';
import FaturaFormScreen from '../screens/FaturaFormScreen';
import SiparisFormScreen from '../screens/SiparisFormScreen';
import TeklifFormScreen from '../screens/TeklifFormScreen';
import MusteriTakipScreen from '../screens/MusteriTakipScreen';
import MusteriTakipDetayScreen from '../screens/MusteriTakipDetayScreen';

import FaturaTasarimScreen from '../screens/FaturaTasarimScreen';

const RootStack = createNativeStackNavigator();
const Tab = createBottomTabNavigator();
const Stack = createNativeStackNavigator();

function MoreStack() {
  return (
    <Stack.Navigator
      screenOptions={{
        headerShown: false,
        animation: 'slide_from_right',
        contentStyle: { backgroundColor: '#0A0A0A' },
      }}
    >
      <Stack.Screen 
        name="DahaFazlaMain" 
        component={DahaFazlaScreen} 
      />
      <Stack.Screen name="MusteriTakip" component={MusteriTakipScreen} />
      <Stack.Screen name="MusteriTakipDetay" component={MusteriTakipDetayScreen} />
      <Stack.Screen name="Kanban" component={KanbanScreen} />
      <Stack.Screen name="MaliyetHesaplama" component={MaliyetHesaplamaScreen} />
      <Stack.Screen name="Araclar" component={AraclarScreen} />
      <Stack.Screen name="Finans" component={FinansScreen} />
      <Stack.Screen name="Siparisler" component={SiparislerScreen} />
      <Stack.Screen name="Teklifler" component={TekliflerScreen} />
      <Stack.Screen name="VadeTakip" component={VadeTakipScreen} />
      <Stack.Screen name="Raporlar" component={RaporlarScreen} />
      <Stack.Screen name="RaporDetay" component={RaporDetayScreen} />
      <Stack.Screen name="Ayarlar" component={AyarlarScreen} />
      <Stack.Screen name="StokGrup" component={StokGrupScreen} />
      <Stack.Screen name="FaturaTasarim" component={FaturaTasarimScreen} />
    </Stack.Navigator>
  );
}

function MainTabs() {
  return (
    <Tab.Navigator
      screenOptions={({ route }: any) => ({
        headerShown: false,
        sceneStyle: { backgroundColor: '#0A0A0A' },
        tabBarStyle: {
          backgroundColor: '#121216',
          borderTopColor: 'rgba(255, 255, 255, 0.08)',
          borderTopWidth: 1,
          height: Platform.OS === 'ios' ? 84 : 64,
          paddingBottom: Platform.OS === 'ios' ? 24 : 10,
          paddingTop: 8,
        },
        tabBarActiveTintColor: '#38BDF8',
        tabBarInactiveTintColor: '#8E8E93',
        tabBarLabelStyle: {
          fontSize: 10,
          fontWeight: '600',
        },
        tabBarIcon: ({ color, focused }: any) => {
          let IconComponent = Home;
          if (route.name === 'Dashboard') {
            IconComponent = Home;
          } else if (route.name === 'Cariler') {
            IconComponent = Users;
          } else if (route.name === 'Faturalar') {
            IconComponent = FileText;
          } else if (route.name === 'Stoklar') {
            IconComponent = Package;
          } else if (route.name === 'DahaFazla') {
            IconComponent = Settings;
          }
          return <IconComponent color={color} size={22} strokeWidth={focused ? 2.4 : 1.8} />;
        },
      })}
    >
      <Tab.Screen 
        name="Dashboard" 
        component={DashboardScreen} 
        options={{ tabBarLabel: 'Ana Sayfa' }}
      />
      <Tab.Screen 
        name="Cariler" 
        component={CarilerScreen} 
        options={{ tabBarLabel: 'Cariler' }}
      />
      <Tab.Screen 
        name="Faturalar" 
        component={FaturalarScreen} 
        options={{ tabBarLabel: 'Faturalar' }}
      />
      <Tab.Screen 
        name="Stoklar" 
        component={StoklarScreen} 
        options={{ tabBarLabel: 'Stoklar' }}
      />
      <Tab.Screen 
        name="DahaFazla" 
        component={MoreStack} 
        options={{ tabBarLabel: 'Daha Fazla' }}
      />
    </Tab.Navigator>
  );
}

const customDarkTheme = {
  ...DarkTheme,
  colors: {
    ...DarkTheme.colors,
    background: '#0A0A0A',
    card: '#0A0A0A',
  },
};

export default function AppNavigator() {
  return (
    <NavigationContainer theme={customDarkTheme}>
      <RootStack.Navigator screenOptions={{ headerShown: false, contentStyle: { backgroundColor: '#0A0A0A' } }}>
        <RootStack.Screen name="MainTabs" component={MainTabs} />
        <RootStack.Screen 
          name="FaturaForm" 
          component={FaturaFormScreen} 
          options={{ presentation: 'card', animation: 'slide_from_right', gestureEnabled: true }}
        />
        <RootStack.Screen 
          name="SiparisForm" 
          component={SiparisFormScreen} 
          options={{ presentation: 'card', animation: 'slide_from_right', gestureEnabled: true }}
        />
        <RootStack.Screen 
          name="TeklifForm" 
          component={TeklifFormScreen} 
          options={{ presentation: 'card', animation: 'slide_from_right', gestureEnabled: true }}
        />
      </RootStack.Navigator>
    </NavigationContainer>
  );
}
