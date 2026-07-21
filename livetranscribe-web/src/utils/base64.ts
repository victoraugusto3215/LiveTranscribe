/**
 * O protocolo JSON do SignalR espera byte[] como string base64 (é assim que
 * o System.Text.Json do backend desserializa). Passar um Uint8Array puro
 * pro invoke() serializaria como {"0":1,"1":2,...} e quebraria o binding.
 * Processa em blocos pra não estourar o limite de argumentos do spread em
 * arrays grandes (chunk de áudio de alguns segundos passa de 300KB).
 */
export function bytesToBase64(bytes: Uint8Array): string {
  const CHUNK_SIZE = 0x8000;
  let binary = "";
  for (let i = 0; i < bytes.length; i += CHUNK_SIZE) {
    const chunk = bytes.subarray(i, i + CHUNK_SIZE);
    binary += String.fromCharCode(...chunk);
  }
  return btoa(binary);
}
