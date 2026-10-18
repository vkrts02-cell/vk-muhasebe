export const normalizeTurkishSearch = (text?: string | null): string => {
  if (!text) return '';
  return text
    .trim()
    .replace(/[Iıİi]/g, 'i')
    .replace(/[Ğğ]/g, 'g')
    .replace(/[Üü]/g, 'u')
    .replace(/[Şş]/g, 's')
    .replace(/[Öö]/g, 'o')
    .replace(/[Çç]/g, 'c')
    .toLowerCase();
};

export const matchTurkishSearch = (source?: string | null, query?: string | null): boolean => {
  if (!query || !query.trim()) return true;
  if (!source || !source.trim()) return false;

  const normSource = normalizeTurkishSearch(source);
  const normQuery = normalizeTurkishSearch(query);

  const tokens = normQuery.split(/\s+/).filter(Boolean);
  if (tokens.length === 0) return true;

  return tokens.every(token => normSource.includes(token));
};
