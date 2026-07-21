import { useCallback, useEffect, useRef, useState } from "react";
import type { HubConnection } from "@microsoft/signalr";
import { HubConnectionState } from "@microsoft/signalr";
import { createTranscriptionConnection } from "../services/transcriptionConnection";
import { ApiError, getSessionSegments, getSessionStatus, uploadSession } from "../services/sessionsApi";
import type { HubStatusEvent, SessionStatus, TranscriptSegment } from "../types/transcription";
import { useMicrophoneRecorder } from "./useMicrophoneRecorder";
import type { RecordedChunk } from "./useMicrophoneRecorder";
import { bytesToBase64 } from "../utils/base64";

export type ConnectionState = "connecting" | "connected" | "reconnecting" | "offline";

const statusFromHubEvent: Record<HubStatusEvent, SessionStatus> = {
  processing: "Processing",
  completed: "Completed",
  error: "Failed",
};

function mergeSegments(current: TranscriptSegment[], incoming: TranscriptSegment[]): TranscriptSegment[] {
  const byId = new Map(current.map((segment) => [segment.id, segment]));
  for (const segment of incoming) {
    byId.set(segment.id, segment);
  }
  return [...byId.values()].sort((a, b) => a.sequenceNumber - b.sequenceNumber);
}

export function useTranscriptionSession() {
  const connectionRef = useRef<HubConnection | null>(null);
  const sessionIdRef = useRef<string | null>(null);
  // encadeia os envios de chunk ao vivo — garante que chegam em ordem no
  // servidor e que o último chunk termina de enviar antes do EndLiveSession
  const pendingSendRef = useRef<Promise<void>>(Promise.resolve());

  const [connectionState, setConnectionState] = useState<ConnectionState>("connecting");
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [status, setStatus] = useState<SessionStatus | "idle">("idle");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [segments, setSegments] = useState<TranscriptSegment[]>([]);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [isRateLimited, setIsRateLimited] = useState(false);
  const [micError, setMicError] = useState<string | null>(null);

  const syncFromServer = useCallback(async (id: string) => {
    const [statusResponse, segmentsResponse] = await Promise.all([
      getSessionStatus(id),
      getSessionSegments(id),
    ]);
    setStatus(statusResponse.status);
    setErrorMessage(statusResponse.errorMessage);
    setSegments((current) => mergeSegments(current, segmentsResponse));
  }, []);

  useEffect(() => {
    const connection = createTranscriptionConnection();
    connectionRef.current = connection;

    connection.on("ReceiveSegment", (segment: TranscriptSegment) => {
      setSegments((current) => mergeSegments(current, [segment]));
    });

    connection.on("SessionStatusChanged", (hubStatus: HubStatusEvent) => {
      setStatus(statusFromHubEvent[hubStatus] ?? "idle");
      // a mensagem de erro não vem no evento do hub, então busca no REST
      if (hubStatus === "error" && sessionIdRef.current) {
        void getSessionStatus(sessionIdRef.current).then((s) => setErrorMessage(s.errorMessage));
      }
    });

    connection.onreconnecting(() => setConnectionState("reconnecting"));
    connection.onclose(() => setConnectionState("offline"));
    connection.onreconnected(() => {
      setConnectionState("connected");
      // pode ter perdido segmentos enquanto a conexão caía — rebusca e reentra no grupo
      if (sessionIdRef.current) {
        void connection.invoke("JoinSession", sessionIdRef.current);
        void syncFromServer(sessionIdRef.current);
      }
    });

    connection
      .start()
      .then(() => setConnectionState("connected"))
      .catch(() => setConnectionState("offline"));

    return () => {
      void connection.stop();
    };
  }, [syncFromServer]);

  const upload = useCallback(
    async (file: File, languageCode: string) => {
      setIsUploading(true);
      setUploadError(null);
      setIsRateLimited(false);
      setSegments([]);
      setErrorMessage(null);

      try {
        const { sessionId: newSessionId } = await uploadSession(file, languageCode);
        sessionIdRef.current = newSessionId;
        setSessionId(newSessionId);
        setStatus("Processing");

        const connection = connectionRef.current;
        if (connection?.state === HubConnectionState.Connected) {
          await connection.invoke("JoinSession", newSessionId);
        }
      } catch (err) {
        if (err instanceof ApiError) {
          setUploadError(err.message);
          setIsRateLimited(err.status === 429);
        } else {
          setUploadError(err instanceof Error ? err.message : "Falha ao enviar o arquivo.");
        }
      } finally {
        setIsUploading(false);
      }
    },
    [],
  );

  const handleRecordedChunk = useCallback((chunk: RecordedChunk) => {
    const send = async () => {
      const connection = connectionRef.current;
      const sid = sessionIdRef.current;
      if (!connection || !sid || connection.state !== HubConnectionState.Connected) {
        return;
      }
      const base64 = bytesToBase64(chunk.wavBytes);
      await connection.invoke("SendAudioChunk", sid, base64, chunk.sequenceNumber, chunk.startMs, chunk.endMs);
    };
    // encadeia mesmo se o envio anterior falhar, pra não travar a fila
    pendingSendRef.current = pendingSendRef.current.then(send, send);
  }, []);

  const recorder = useMicrophoneRecorder({
    onChunk: handleRecordedChunk,
    onError: setMicError,
  });

  const startRecording = useCallback(
    async (languageCode: string) => {
      const connection = connectionRef.current;
      if (!connection || connection.state !== HubConnectionState.Connected) {
        setMicError("Sem conexão com o servidor. Aguarde reconectar e tente de novo.");
        return;
      }

      setMicError(null);
      setUploadError(null);
      setSegments([]);
      setErrorMessage(null);
      pendingSendRef.current = Promise.resolve();

      try {
        // pede permissão de microfone ANTES de criar a sessão no servidor —
        // se o usuário negar, não sobra uma sessão órfã esperando chunks
        // que nunca vão chegar
        await recorder.start();
      } catch {
        return;
      }

      try {
        const newSessionId: string = await connection.invoke("StartLiveSession", languageCode);
        sessionIdRef.current = newSessionId;
        setSessionId(newSessionId);
        setStatus("Processing");
      } catch (err) {
        recorder.stop();
        setMicError(err instanceof Error ? err.message : "Não foi possível iniciar a sessão ao vivo.");
      }
    },
    [recorder],
  );

  const stopRecording = useCallback(async () => {
    recorder.stop();
    await pendingSendRef.current;

    const connection = connectionRef.current;
    const sid = sessionIdRef.current;
    if (connection && sid && connection.state === HubConnectionState.Connected) {
      try {
        await connection.invoke("EndLiveSession", sid);
      } catch {
        // a conexão pode ter caído nesse meio tempo — o servidor finaliza
        // a sessão sozinho via OnDisconnectedAsync
      }
    }
  }, [recorder]);

  return {
    connectionState,
    sessionId,
    status,
    errorMessage,
    segments,
    isUploading,
    uploadError,
    isRateLimited,
    upload,
    isRecording: recorder.isRecording,
    micError,
    startRecording,
    stopRecording,
  };
}
