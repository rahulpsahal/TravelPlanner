export const airports = [
  { code: 'BLR', name: 'Bengaluru' }, { code: 'BOM', name: 'Mumbai' },
  { code: 'DEL', name: 'Delhi' }, { code: 'SIN', name: 'Singapore' },
] as const;

export const personas = [
  { name: 'Slow & Scenic', icon: '🌿', description: 'Misty estates, cafés and unhurried mornings' },
  { name: 'Adventure & Trails', icon: '⛰', description: 'Sunrise ridge treks and coastal routes' },
  { name: 'Food & Culture', icon: '🍜', description: 'Local kitchens and historic streets' },
  { name: 'Luxury & Wellness', icon: '✦', description: 'Private stays, spas and quiet escapes' },
] as const;

export function preferences(userId: string | undefined) {
  try {
    const saved = userId && localStorage.getItem(`wanderai.preferences.${userId}`);
    if (saved) {
      const value = JSON.parse(saved);
      if (airports.some(airport => airport.code === value.airport)
        && personas.some(persona => persona.name === value.persona)) return value as { airport: string; persona: string };
    }
  } catch { /* Browser storage may be unavailable. */ }
  return { airport: 'BLR', persona: 'Slow & Scenic' };
}

export function savePreferences(userId: string, airport: string, persona: string) {
  try {
    localStorage.setItem(`wanderai.preferences.${userId}`, JSON.stringify({ airport, persona }));
  } catch { /* Browsing can continue without saving preferences. */ }
}
