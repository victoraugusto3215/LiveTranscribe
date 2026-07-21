import { useCallback, useEffect, useRef, useState } from "react";
import { encodePcm16Wav, float32ToPcm16, mergeFloat32Arrays } from "../utils/wavEncoder";

export interface RecordedChunk {
  wavBytes: Uint8Array;
  sequenceNumber: number;
  startMs: number;
  endMs: number;
}

interface UseMicrophoneRecorderOptions {
  onChunk: (chunk: RecordedChunk) => void;
  onError?: (message: string) => void;
}

// janela de corte: nunca menor que MIN, nunca maior que MAX — dentro
// desse intervalo, corta assim que achar ~400ms de silêncio (VAD simples
// por energia RMS), pra não partir uma palavra ao meio
const MIN_CHUNK_MS = 2000;
const MAX_CHUNK_MS = 6000;
const SILENCE_RMS_THRESHOLD = 0.012;
const SILENCE_HOLD_MS = 400;

export function useMicrophoneRecorder({ onChunk, onError }: UseMicrophoneRecorderOptions) {
  const [isRecording, setIsRecording] = useState(false);

  const onChunkRef = useRef(onChunk);
  const onErrorRef = useRef(onError);
  onChunkRef.current = onChunk;
  onErrorRef.current = onError;

  const audioContextRef = useRef<AudioContext | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const workletNodeRef = useRef<AudioWorkletNode | null>(null);
  const sourceRef = useRef<MediaStreamAudioSourceNode | null>(null);

  const bufferRef = useRef<Float32Array[]>([]);
  const bufferedSamplesRef = useRef(0);
  const silentSamplesRef = useRef(0);
  const totalSamplesRef = useRef(0);
  const chunkStartSampleRef = useRef(0);
  const sequenceRef = useRef(0);

  const flushChunk = useCallback((sampleRate: number, isFinal: boolean) => {
    if (bufferedSamplesRef.current === 0 && !isFinal) {
      return;
    }
    if (bufferedSamplesRef.current === 0) {
      return;
    }

    const merged = mergeFloat32Arrays(bufferRef.current);
    const pcm16 = float32ToPcm16(merged);
    const wavBytes = encodePcm16Wav(pcm16, sampleRate);

    const startMs = Math.round((chunkStartSampleRef.current / sampleRate) * 1000);
    const endMs = Math.round((totalSamplesRef.current / sampleRate) * 1000);

    onChunkRef.current({ wavBytes, sequenceNumber: sequenceRef.current, startMs, endMs });

    sequenceRef.current += 1;
    bufferRef.current = [];
    bufferedSamplesRef.current = 0;
    silentSamplesRef.current = 0;
    chunkStartSampleRef.current = totalSamplesRef.current;
  }, []);

  const stop = useCallback(() => {
    const audioContext = audioContextRef.current;
    if (audioContext && bufferedSamplesRef.current > 0) {
      flushChunk(audioContext.sampleRate, true);
    }

    workletNodeRef.current?.port.close();
    workletNodeRef.current?.disconnect();
    sourceRef.current?.disconnect();
    streamRef.current?.getTracks().forEach((track) => track.stop());
    void audioContextRef.current?.close();

    workletNodeRef.current = null;
    sourceRef.current = null;
    streamRef.current = null;
    audioContextRef.current = null;

    setIsRecording(false);
  }, [flushChunk]);

  const start = useCallback(async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true },
      });
      streamRef.current = stream;

      const audioContext = new AudioContext();
      audioContextRef.current = audioContext;
      await audioContext.audioWorklet.addModule("/worklets/pcm-recorder-worklet.js");

      bufferRef.current = [];
      bufferedSamplesRef.current = 0;
      silentSamplesRef.current = 0;
      totalSamplesRef.current = 0;
      chunkStartSampleRef.current = 0;
      sequenceRef.current = 0;

      const source = audioContext.createMediaStreamSource(stream);
      sourceRef.current = source;
      const workletNode = new AudioWorkletNode(audioContext, "pcm-recorder-processor");
      workletNodeRef.current = workletNode;

      workletNode.port.onmessage = (event: MessageEvent<Float32Array>) => {
        const samples = event.data;
        bufferRef.current.push(samples);
        bufferedSamplesRef.current += samples.length;
        totalSamplesRef.current += samples.length;

        let sumSquares = 0;
        for (let i = 0; i < samples.length; i++) {
          sumSquares += samples[i] * samples[i];
        }
        const rms = Math.sqrt(sumSquares / samples.length);
        silentSamplesRef.current = rms < SILENCE_RMS_THRESHOLD ? silentSamplesRef.current + samples.length : 0;

        const sampleRate = audioContext.sampleRate;
        const bufferedMs = (bufferedSamplesRef.current / sampleRate) * 1000;
        const silentMs = (silentSamplesRef.current / sampleRate) * 1000;

        if (bufferedMs >= MAX_CHUNK_MS || (bufferedMs >= MIN_CHUNK_MS && silentMs >= SILENCE_HOLD_MS)) {
          flushChunk(sampleRate, false);
        }
      };

      // nunca conecta o worklet em audioContext.destination — não queremos
      // o microfone ecoando de volta nas caixas de som do usuário
      source.connect(workletNode);

      setIsRecording(true);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Não foi possível acessar o microfone.";
      onErrorRef.current?.(message);
      throw new Error(message);
    }
  }, [flushChunk]);

  useEffect(() => {
    return () => {
      if (audioContextRef.current) {
        stop();
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return { isRecording, start, stop };
}
