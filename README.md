# LiveTranscribe

Transcrição de áudio com entrega progressiva, o texto aparece enquanto o áudio ainda está sendo processado, não só no final.

## O que é

Sobe um arquivo de áudio (ou grava pelo microfone) e a transcrição vai chegando em blocos de poucos segundos, ao vivo, em vez de esperar o processo inteiro terminar pra mostrar qualquer coisa. Pensado pros casos onde isso importa de verdade: legenda ao vivo de reunião ou aula, transcrição de entrevista, supervisor acompanhando uma call em andamento, ditado.

Sem perda de dado se a conexão cair, backpressure controlado se a transcrição atrasar, resiliência a queda de conexão.

## Como funciona

Duas partes desacopladas, como Slack, Teams ou Meet fazem:

- **`LiveTranscribe.Api`**: ASP.NET Core Web API. Recebe upload de arquivo (REST) e áudio ao vivo (Hub SignalR, conexão persistente; streaming não passa por request/response). Cada chunk de áudio é salvo em disco antes de ser processado, enfileirado com backpressure real (`Channel<T>` bounded) e transcrito via Groq (`whisper-large-v3-turbo`). O resultado volta ao cliente em partes: primeiro um parcial rápido, depois um final mais preciso reconciliando o trecho inteiro.
- **`livetranscribe-web`**: React + Vite. Upload de arquivo ou captura de microfone via `AudioWorklet`, indicador de conexão, feed de transcrição que vai preenchendo em tempo real.

Formatos de áudio aceitos no upload: WAV, MP3, M4A, OGG, FLAC, AAC (até 50 MB). Tudo passa por normalização com `ffmpeg` antes de entrar no pipeline.

## Decisões de arquitetura

**Front-end separado do back-end.** Streaming não passa pela REST API: REST é request/response, um ciclo HTTP fecha por chamada, não é usado pro áudio. Quem cuida disso é um Hub SignalR, que abre uma conexão persistente (WebSocket, com fallback SSE/long-polling) num endpoint próprio (`/hubs/transcription`), independente da API REST. Separar front de back não piora latência nem confiabilidade do streaming; o único custo extra é o handshake inicial da conexão, que acontece uma vez por sessão, não por chunk de áudio.

**O transporte já resolve perda de pacote, falta o nível de aplicação.** WebSocket roda sobre TCP: entrega ordenada e sem perda de pacote já é garantida pela pilha de rede. O que precisa de design explícito:

- **Perda de dado se a conexão cair**: buffer local no cliente com reenvio por número de sequência após reconectar.
- **Durabilidade**: persistir os chunks de áudio brutos em disco antes de processar (se o processo cair, o áudio não se perde).
- **Backpressure**: fila limitada (bounded channel) entre ingestão e processamento, pra não estourar memória se o Whisper atrasar.

**Expectativa de latência.** Whisper (local ou via Groq) não é um modelo de streaming token a token como Azure, Google ou Deepgram Speech-to-Text streaming: ele transcreve blocos de áudio. A entrega em tempo real aqui é progressiva por janelas (cerca de 3 a 4 segundos por chunk), não instantânea palavra por palavra. Latência esperada: 1 a 3 segundos por trecho usando Groq (`whisper-large-v3-turbo`, que processa mais rápido que tempo real). É consistente com como produtos reais de captioning ao vivo funcionam: texto parcial, depois corrigido por uma passada final mais precisa.

## Pipeline de áudio

O `AudioNormalizer` converte todo upload para WAV PCM16 mono 16kHz (o formato que o Whisper espera de qualquer forma, o que também reduz o tamanho de cada chunk enviado pra Groq). Isso mantém o `WavSlicer` (fatiamento em C# puro) como única peça de chunking: ele nunca vê nada além de WAV normalizado, não importa o que o usuário enviou.

Na captura ao vivo pelo microfone, o `AudioWorklet` captura PCM bruto. Não dá pra usar `MediaRecorder` puro porque os blobs webm/opus que ele gera não são independentemente decodificáveis (só o primeiro tem cabeçalho de container).

O corte em janelas acumula PCM e corta a cada 3 a 4 segundos, preferindo silêncio (VAD simples por energia via `AnalyserNode`) pra não cortar palavra no meio. Se não achar silêncio em até 6 segundos, corta mesmo assim, o que limita o pior caso de latência. Cada corte é serializado como WAV standalone: arquivo de áudio completo e válido por chunk, sem depender de continuidade de container.

No servidor, o envio e processamento seguem três passos:

1. Persiste o WAV em disco (`/data/sessions/{sessionId}/{seq:D6}.wav`), durabilidade antes de processar.
2. Enfileira num `Channel<AudioChunk>` bounded por sessão (`BoundedChannelFullMode.Wait`); se o Whisper atrasar, a escrita bloqueia, aplicando backpressure real em vez de estourar memória ou descartar áudio.
3. Um consumer único por sessão processa em ordem, chama `ITranscriptionProvider.TranscribeAsync` e distribui o resultado pro grupo via `ReceivePartialTranscript`.

Cada janela de 3 a 4 segundos gera um segmento parcial. Periodicamente (a cada 15 a 20 segundos ou numa pausa de fala), roda uma re-transcrição do trecho consolidado, com janela maior e mais contexto, e substitui os parciais por um `ReceiveFinalTranscript` mais preciso. Resolve também o problema de palavra cortada na borda entre chunks.

## Contrato do Hub

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

Grupo SignalR por `sessionId`: permite futuramente mais de um cliente ouvindo a mesma sessão, como um supervisor acompanhando uma call.

## Reconexão sem perda

Cliente mantém ring buffer local dos chunks enviados mas não confirmados (`ChunkAcknowledged`). Com `HubConnectionBuilder().withAutomaticReconnect(...)`, no `onreconnected` chama `ResumeSession(sessionId, lastAckedSequence)` e reenvia do buffer o que passou daquele número. Servidor guarda `lastProcessedSequence` por sessão pra descartar reenvios duplicados. Se a queda passar de 60 segundos, a sessão finaliza sozinha e roda a consolidação final sobre os WAVs já persistidos em disco.

## Provider de transcrição

```csharp
public interface ITranscriptionProvider
{
    Task<TranscriptionResult> TranscribeAsync(byte[] wav, string languageCode, CancellationToken ct);
}
```

Começa com `GroqWhisperProvider` (`whisper-large-v3-turbo`, free tier, zero infra). `FasterWhisperLocalProvider` fica como segunda implementação opcional (sidecar local): mostra maturidade de arquitetura sem exigir infra pesada de início.

## Stack

- Backend: .NET 10, ASP.NET Core, SignalR, EF Core + SQLite, Xabe.FFmpeg
- Frontend: React 19, Vite, TypeScript, Tailwind
- Transcrição: Groq (Whisper large-v3-turbo)
- Testes: xUnit

## Como rodar localmente

Pré-requisitos: .NET 10 SDK, Node 20+, uma chave de API da [Groq](https://console.groq.com) (tier gratuito já é suficiente).

### Backend

```bash
cd LiveTranscribe.Api
dotnet user-secrets set "Groq:ApiKey" "sua-chave-aqui"
dotnet run
```

A API sobe em `http://localhost:5127` (ver `Properties/launchSettings.json`). O `ffmpeg` é baixado automaticamente em `App_Data/ffmpeg` na primeira execução, não precisa instalar nada à parte. O banco SQLite é criado e migrado sozinho no startup.

### Frontend

```bash
cd livetranscribe-web
npm install
npm run dev
```

Confira `VITE_API_BASE_URL` em `.env.development`: precisa apontar pra porta em que o backend subiu. O front sobe em `http://localhost:5173`, já liberado no CORS do backend por padrão.

### Testes

```bash
dotnet test
```

## Estrutura

```
LiveTranscribe/
├── LiveTranscribe.Api/      API + Hub SignalR + pipeline de áudio
├── LiveTranscribe.Tests/    testes xUnit
└── livetranscribe-web/      front-end React + Vite
```

## Contribuindo

Não há um processo formal de contribuição, mas issues apontando bug, sugestão de arquitetura ou PR pequeno são bem-vindos. Antes de abrir um PR maior, vale abrir uma issue descrevendo o que pretende mudar e por quê, pra alinhar antes de escrever código. Ao propor mudança de arquitetura, justifique a decisão da mesma forma que a seção "Decisões de arquitetura" faz: qual problema resolve, qual trade-off assume.
