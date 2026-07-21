export type SessionStatus = "Processing" | "Completed" | "Failed";

export interface TranscriptSegment {
  id: string;
  sequenceNumber: number;
  startMs: number;
  endMs: number;
  text: string;
  isFinal: boolean;
}

export interface UploadSessionResponse {
  sessionId: string;
}

export interface SessionStatusResponse {
  sessionId: string;
  status: SessionStatus;
  errorMessage: string | null;
}

/** Espelha os valores de status que o Hub envia via SessionStatusChanged. */
export type HubStatusEvent = "processing" | "completed" | "error";
