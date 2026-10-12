import React from 'react';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { NavigationContainer, DarkTheme } from '@react-navigation/native';
import FloatingTabBar from '../components/FloatingTabBar';

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
        contentStyle: { backgroundColor: '#000000' },
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
      tabBar={(props) => <FloatingTabBar {...props} />}
      screenOptions={{
        headerShown: false,
        sceneStyle: { backgroundColor: '#000000' },
      }}
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
    background: '#000000',
    card: '#000000',
    border: 'transparent',
  },
};

export default function AppNavigator() {
  return (
    <NavigationContainer theme={customDarkTheme}>
      <RootStack.Navigator screenOptions={{ headerShown: false, contentStyle: { backgroundColor: '#000000' } }}>
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
