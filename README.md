# LiveTranscribe

Transcrição de áudio com entrega progressiva, o texto aparece enquanto o áudio ainda está sendo processado, não só no final.

## O que é

Sobe um arquivo de áudio (ou grava pelo microfone) e a transcrição vai chegando em blocos de poucos segundos, ao vivo, em vez de esperar o processo inteiro terminar pra mostrar qualquer coisa. Pensado pros casos onde isso importa de verdade: legenda ao vivo de reunião ou aula, transcrição de entrevista, supervisor acompanhando uma call em andamento, ditado.

Sem perda de dado se a conexão cair, backpressure controlado se a transcrição atrasar, resiliência a queda de conexão. O raciocínio completo por trás de cada decisão está em [ARCHITECTURE.md](ARCHITECTURE.md).

## Como funciona

Duas partes desacopladas, como Slack, Teams ou Meet fazem:

- **`LiveTranscribe.Api`**: ASP.NET Core Web API. Recebe upload de arquivo (REST) e áudio ao vivo (Hub SignalR, conexão persistente; streaming não passa por request/response). Cada chunk de áudio é salvo em disco antes de ser processado, enfileirado com backpressure real (`Channel<T>` bounded) e transcrito via Groq (`whisper-large-v3-turbo`). O resultado volta ao cliente em partes: primeiro um parcial rápido, depois um final mais preciso reconciliando o trecho inteiro.
- **`livetranscribe-web`**: React + Vite. Upload de arquivo ou captura de microfone via `AudioWorklet`, indicador de conexão, feed de transcrição que vai preenchendo em tempo real.

Formatos de áudio aceitos no upload: WAV, MP3, M4A, OGG, FLAC, AAC (até 50 MB). Tudo passa por normalização com `ffmpeg` antes de entrar no pipeline.

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
├── livetranscribe-web/      front-end React + Vite
└── ARCHITECTURE.md          decisões de arquitetura, com o porquê de cada uma
```

## Contribuindo

Não há um processo formal de contribuição, mas issues apontando bug, sugestão de arquitetura ou PR pequeno são bem-vindos. Antes de abrir um PR maior, vale abrir uma issue descrevendo o que pretende mudar e por quê, pra alinhar antes de escrever código. Ao propor mudança de arquitetura, justifique a decisão do mesmo jeito que o [ARCHITECTURE.md](ARCHITECTURE.md) faz: qual problema resolve, qual trade-off assume.
