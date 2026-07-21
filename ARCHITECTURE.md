# LiveTranscribe — Plano de Arquitetura

## 1. Objetivo

Transcrição de áudio com entrega progressiva (texto aparece enquanto o áudio é processado, não só depois de terminar). Casos de uso reais que motivam as decisões abaixo: legendagem ao vivo de reunião/aula (acessibilidade), transcrição de entrevista, apoio a atendimento (supervisor acompanhando uma call em andamento), ditado médico/jurídico.


## 2. Decisões de arquitetura

### 2.1 Front-end separado do back-end (não monolito Blazor Server)

**Streaming não passa pela REST API.** REST é request/response, um ciclo HTTP fecha por chamada — não é usado pro áudio. O streaming usa um **Hub SignalR**, que abre uma conexão persistente (WebSocket, com fallback SSE/long-polling). É outro endpoint (`/hubs/transcription`), outro protocolo, independente da API REST. Separar front de back não piora latência nem confiabilidade do streaming — o único custo extra é o handshake inicial da conexão, que acontece uma vez por sessão, não por chunk de áudio.

### 2.2 Transporte já resolve perda de pacote — o que falta resolver é nível de aplicação

WebSocket roda sobre TCP: entrega ordenada e sem perda de pacote já é garantida pela pilha de rede, não é algo a implementar. O que precisa de design explícito:

- **Perda de dado se a conexão cair**: buffer local no cliente + reenvio por número de sequência após reconectar.
- **Durabilidade**: persistir os chunks de áudio brutos em disco antes de processar — se o processo cair, o áudio não se perde.
- **Backpressure**: fila limitada (bounded channel) entre ingestão e processamento, para não estourar memória se o Whisper atrasar.

### 2.3 Expectativa de latência

Whisper (local ou via Groq) não é um modelo de streaming token-a-token como Azure/Google/Deepgram Speech-to-Text streaming — ele transcreve blocos de áudio. A entrega "em tempo real" aqui é **progressiva por janelas** (~3-4s por chunk), não instantânea palavra-por-palavra. Latência esperada: 1-3s por trecho usando Groq (`whisper-large-v3-turbo`, processa mais rápido que tempo real). Isso é consistente com como produtos reais de captioning ao vivo funcionam (texto parcial, depois corrigido por uma passada final mais precisa).

## 3. Estrutura de pastas

```
LiveTranscribe/
├── LiveTranscribe.Api/          ASP.NET Core Web API + SignalR Hub
│   ├── Controllers/             REST: sessões, histórico, upload de arquivo
│   ├── Hubs/                    TranscriptionHub
│   ├── Services/                ITranscriptionProvider (Groq / faster-whisper local), Vad, ChunkStore
│   ├── Data/                    EF Core + SQLite (DbContextFactory)
│   ├── Models/ + Migrations/
├── LiveTranscribe.Tests/        xUnit
├── livetranscribe-web/          React + Vite (front-end separado)
├── Dockerfile                   multi-stage (padrão RachaContas)
└── fly.toml                     volume persistente pra SQLite + chunks de áudio
```

## 4. Contrato do Hub

```csharp
public interface ITranscriptionClient
{
    Task ReceivePartialTranscript(TranscriptSegment segment);
    Task ReceiveFinalTranscript(TranscriptSegment segment);
    Task ChunkAcknowledged(int sequenceNumber);
    Task SessionStatus(string status); // connected | processing | error | ended
}

public class TranscriptionHub : Hub<ITranscriptionClient>
{
    Task<Guid> StartSession(string languageCode);
    Task SendAudioChunk(Guid sessionId, byte[] wavBytes, int sequenceNumber, long startMs, long endMs);
    Task ResumeSession(Guid sessionId, int lastAckedSequence); // após reconexão
    Task EndSession(Guid sessionId);
}
```

Grupo SignalR por `sessionId` — permite futuramente mais de um cliente ouvindo a mesma sessão (ex.: supervisor acompanhando uma call).

## 5. Pipeline de áudio

**Normalização de formato (fase 1, upload de arquivo)**: o upload aceita os formatos mais comuns em gravação real — WAV, MP3, M4A, OGG, FLAC, AAC. Todo upload passa pelo `ffmpeg` antes de entrar no pipeline (`AudioNormalizer`, convertendo pra WAV PCM16 mono 16kHz — o formato que o Whisper espera de qualquer forma, o que também reduz o tamanho de cada chunk enviado pra Groq). O binário do `ffmpeg` não precisa estar instalado na máquina: `Xabe.FFmpeg.Downloader` baixa a versão certa pra `App_Data/ffmpeg` na primeira execução. Isso mantém o `WavSlicer` (fatiamento em C# puro) como única peça de chunking — ele só nunca vê nada além de WAV normalizado, não importa o que o usuário enviou.

**Captura** (fase 2, microfone ao vivo): `AudioWorklet` captura PCM bruto. Não usar `MediaRecorder` puro — os blobs webm/opus que ele gera não são independentemente decodificáveis (só o primeiro tem cabeçalho de container).

**Corte em janelas**: acumula PCM e corta a cada ~3-4s, preferindo silêncio (VAD simples por energia via `AnalyserNode`) para não cortar palavra no meio. Se não achar silêncio em até 6s, corta mesmo assim (limita o pior caso de latência). Cada corte é serializado como **WAV standalone** — arquivo de áudio completo e válido por chunk, sem depender de continuidade de container.

**Envio e processamento no servidor**:
1. Persiste o WAV em disco (`/data/sessions/{sessionId}/{seq:D6}.wav`) — durabilidade antes de processar.
2. Enfileira num `Channel<AudioChunk>` **bounded** por sessão (`BoundedChannelFullMode.Wait`) — se o Whisper atrasar, a escrita bloqueia, aplicando backpressure real em vez de estourar memória ou descartar áudio.
3. Um consumer único por sessão processa em ordem, chama `ITranscriptionProvider.TranscribeAsync`, distribui o resultado pro grupo via `ReceivePartialTranscript`.

**Reconciliação parcial → final**: cada janela de 3-4s gera um segmento "partial". Periodicamente (a cada ~15-20s ou numa pausa de fala), roda uma re-transcrição do trecho consolidado (janela maior, mais contexto) e substitui os parciais por um `ReceiveFinalTranscript` mais preciso — resolve também o problema de palavra cortada na borda entre chunks.

## 6. Reconexão sem perda

Cliente mantém ring buffer local dos chunks enviados mas não confirmados (`ChunkAcknowledged`). `HubConnectionBuilder().withAutomaticReconnect(...)`; no `onreconnected`, chama `ResumeSession(sessionId, lastAckedSequence)` e reenvia do buffer o que passou daquele número. Servidor guarda `lastProcessedSequence` por sessão pra descartar reenvios duplicados. Se a queda passar de ~60s, a sessão finaliza sozinha e roda a consolidação final sobre os WAVs já persistidos em disco.

## 7. Provider de transcrição (abstração Groq / local)

```csharp
public interface ITranscriptionProvider
{
    Task<TranscriptionResult> TranscribeAsync(byte[] wav, string languageCode, CancellationToken ct);
}
```

Começar com `GroqWhisperProvider` (`whisper-large-v3-turbo`, free tier, zero infra). Deixar `FasterWhisperLocalProvider` como segunda implementação opcional (sidecar local) — mostra maturidade de arquitetura sem exigir infra pesada de início.

