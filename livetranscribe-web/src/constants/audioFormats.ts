export const ALLOWED_AUDIO_EXTENSIONS = [".wav", ".mp3", ".m4a", ".ogg", ".flac", ".aac"];

// espelha SessionsController.MaxUploadBytes no backend
export const MAX_FILE_SIZE_BYTES = 50 * 1024 * 1024;

// espelha o AddUploadRateLimiting(permitLimit: 5, window: 2h) no Program.cs
export const MAX_UPLOADS_PER_WINDOW = 5;
export const RATE_LIMIT_WINDOW_LABEL = "2h";

export const ACCEPT_ATTRIBUTE = [
  ...ALLOWED_AUDIO_EXTENSIONS,
  "audio/wav",
  "audio/mpeg",
  "audio/mp4",
  "audio/ogg",
  "audio/flac",
  "audio/aac",
].join(",");

export function hasAllowedExtension(fileName: string): boolean {
  const lower = fileName.toLowerCase();
  return ALLOWED_AUDIO_EXTENSIONS.some((ext) => lower.endsWith(ext));
}
