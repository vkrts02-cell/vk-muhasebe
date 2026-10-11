// Custom Base epoch (Jan 1, 2024 UTC = 1704067200) to keep IDs compact, safe, and positive within signed 32-bit int
const APP_EPOCH_OFFSET = 1704067200;
const MOBILE_ID_BASE = 100000000; // 100M+ range (above desktop auto-increments, strictly below 2.14B)
const MAX_SAFE_INT32 = 2147483600;

let lastGeneratedId = MOBILE_ID_BASE;

/**
 * Mobil ortamda üretilen ID'ler 100,000,000 ile 2,147,483,600 aralığındaki güvenli havuzdan tahsis edilir.
 * Masaüstü SQLite veritabanı autoincrement olarak 1, 2, 3... sayılarından başladığı için,
 * mobil çevrimdışıyken veya masaüstüyle eşzamanlı yeni kayıt eklendiğinde ID ÇAKIŞMASI KESİNLİKLE ENGELLENİR.
 */
export const generateInt32Id = (): number => {
  const epochSec = Math.floor(Date.now() / 1000);
  const delta = Math.max(0, epochSec - APP_EPOCH_OFFSET);
  let newId = MOBILE_ID_BASE + delta;

  if (newId <= lastGeneratedId) {
    newId = lastGeneratedId + 1;
  }

  if (newId >= MAX_SAFE_INT32) {
    newId = MOBILE_ID_BASE;
  }

  lastGeneratedId = newId;
  return newId;
};

export const generateMobileRecordId = (): number => {
  return generateInt32Id();
};

/**
 * Platformlar arası tam tekillik ve bulutta veri ezilmesini önlemek için GUID üretici.
 */
export const generateSyncGuid = (): string => {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
};
