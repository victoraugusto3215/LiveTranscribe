const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5280";

export const env = {
  apiBaseUrl,
  hubUrl: `${apiBaseUrl}/hubs/transcription`,
};
