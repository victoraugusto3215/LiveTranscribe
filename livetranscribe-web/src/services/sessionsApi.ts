import { env } from "../config/env";
import type {
  SessionStatusResponse,
  TranscriptSegment,
  UploadSessionResponse,
} from "../types/transcription";

export class ApiError extends Error {
  status: number;

  constructor(message: string, status: number) {
    super(message);
    this.status = status;
  }
}

async function parseErrorOrThrow(response: Response): Promise<never> {
  const body = await response.text();
  throw new ApiError(body || response.statusText, response.status);
}

export async function uploadSession(file: File, languageCode: string): Promise<UploadSessionResponse> {
  const formData = new FormData();
  formData.append("file", file);
  formData.append("languageCode", languageCode);

  const response = await fetch(`${env.apiBaseUrl}/api/sessions/upload`, {
    method: "POST",
    body: formData,
  });

  if (!response.ok) {
    await parseErrorOrThrow(response);
  }

  return response.json();
}

export async function getSessionStatus(sessionId: string): Promise<SessionStatusResponse> {
  const response = await fetch(`${env.apiBaseUrl}/api/sessions/${sessionId}`);
  if (!response.ok) {
    await parseErrorOrThrow(response);
  }
  return response.json();
}

export async function getSessionSegments(sessionId: string): Promise<TranscriptSegment[]> {
  const response = await fetch(`${env.apiBaseUrl}/api/sessions/${sessionId}/segments`);
  if (!response.ok) {
    await parseErrorOrThrow(response);
  }
  return response.json();
}
