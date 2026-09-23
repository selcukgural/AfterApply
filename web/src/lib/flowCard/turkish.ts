/**
 * The Turkish possessive suffix after a numeral, apostrophe included: "50'si", "47'si", "3'ü",
 * "86'sı", "10'u", "100'ü", "1000'i". The card's headline and caption put a count in that position
 * ("86 başvurumun 50'si cevapsız kaldı") and the suffix follows how the number is *read*, not how
 * it is written — so it is decided by the last spoken word: the ones digit, else the tens word,
 * else yüz / bin / milyon.
 */

// After the word for 0–9: vowel-final words take s + vowel (iki → -si), consonant-final ones just
// the vowel (üç → -ü). Index = digit.
const ONES = ["ı", "i", "si", "ü", "ü", "i", "sı", "si", "i", "u"];
// on, yirmi, otuz, kırk, elli, altmış, yetmiş, seksen, doksan. Index = tens digit.
const TENS = ["", "u", "si", "u", "ı", "si", "ı", "i", "i", "ı"];

export function turkishPossessiveSuffix(value: number): string {
  const n = Math.abs(Math.trunc(value));
  if (n === 0) return "'ı"; // sıfır
  if (n % 10 !== 0) return `'${ONES[n % 10]}`;
  if (n % 100 !== 0) return `'${TENS[(n % 100) / 10]}`;
  if (n % 1000 !== 0) return "'ü"; // yüz
  if (n % 1_000_000 !== 0) return "'i"; // bin
  return "'u"; // milyon
}
