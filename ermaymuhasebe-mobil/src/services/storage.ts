import AsyncStorage from '@react-native-async-storage/async-storage';
import * as SecureStore from 'expo-secure-store';
import * as SQLite from 'expo-sqlite';
import { Platform } from 'react-native';

const db = Platform.OS !== 'web' ? SQLite.openDatabaseSync('ermay_cache.db') : null;

if (db) {
  try {
    db.execSync(`
      CREATE TABLE IF NOT EXISTS key_value_cache (
        key TEXT PRIMARY KEY,
        value TEXT
      );
    `);
  } catch (e) {
    console.error("SQLite init error", e);
  }
}

const SECURE_KEYS = new Set([
  'ermay_firebase_id_token',
  'ermay_cloud_secret',
  'ermay_auth_token',
  'ermay_user_credentials',
  'ermay_supabase_url',
  'ermay_supabase_key'
]);

const StorageWrapper = {
  getItem: async (key: string): Promise<string | null> => {
    try {
      if (SECURE_KEYS.has(key) && Platform.OS !== 'web') {
        const secureVal = await SecureStore.getItemAsync(key);
        if (secureVal !== null) return secureVal;
      }
      if (db) {
        const row: any = await db.getFirstAsync('SELECT value FROM key_value_cache WHERE key = ?', [key]);
        if (row) return row.value;
      }
      return await AsyncStorage.getItem(key);
    } catch (e) {
      return null;
    }
  },
  setItem: async (key: string, value: string): Promise<void> => {
    try {
      if (SECURE_KEYS.has(key) && Platform.OS !== 'web') {
        await SecureStore.setItemAsync(key, value);
      }
      if (db) {
        await db.runAsync('INSERT OR REPLACE INTO key_value_cache (key, value) VALUES (?, ?)', [key, value]);
      } else {
        await AsyncStorage.setItem(key, value);
      }
    } catch (e) {}
  },
  removeItem: async (key: string): Promise<void> => {
    try {
      if (SECURE_KEYS.has(key) && Platform.OS !== 'web') {
        await SecureStore.deleteItemAsync(key);
      }
      if (db) {
        await db.runAsync('DELETE FROM key_value_cache WHERE key = ?', [key]);
      }
      await AsyncStorage.removeItem(key);
    } catch (e) {}
  },
  clear: async (): Promise<void> => {
    try {
      if (Platform.OS !== 'web') {
        for (const k of SECURE_KEYS) {
          try { await SecureStore.deleteItemAsync(k); } catch (_) {}
        }
      }
      if (db) {
        await db.runAsync('DELETE FROM key_value_cache');
      }
      await AsyncStorage.clear();
    } catch (e) {}
  },
  getAllKeys: async (): Promise<readonly string[]> => {
    try {
      if (db) {
        const rows: any[] = await db.getAllAsync('SELECT key FROM key_value_cache');
        return rows.map(r => r.key);
      }
      return await AsyncStorage.getAllKeys();
    } catch (e) {
      return [];
    }
  },
  multiRemove: async (keys: string[]): Promise<void> => {
    try {
      for (const k of keys) {
        await StorageWrapper.removeItem(k);
      }
    } catch (e) {}
  }
};

export default StorageWrapper;

