import { useState } from "react";
import { UploadPanel } from "./components/UploadPanel";
import { LivePanel } from "./components/LivePanel";
import { SourceTabs } from "./components/SourceTabs";
import type { SourceMode } from "./components/SourceTabs";
import { StatusBadge } from "./components/StatusBadge";
import { ConnectionIndicator } from "./components/ConnectionIndicator";
import { ThemeToggle } from "./components/ThemeToggle";
import { TranscriptFeed } from "./components/TranscriptFeed";
import { useTranscriptionSession } from "./hooks/useTranscriptionSession";
import { useTheme } from "./hooks/useTheme";

export default function App() {
  const [sourceMode, setSourceMode] = useState<SourceMode>("upload");
  const { theme, toggleTheme } = useTheme();

  const {
    connectionState,
    sessionId,
    status,
    errorMessage,
    segments,
    isUploading,
    uploadError,
    isRateLimited,
    upload,
    isRecording,
    micError,
    startRecording,
    stopRecording,
  } = useTranscriptionSession();

  const busy = status === "Processing";

  return (
    <div className="min-h-screen bg-paper text-ink dark:bg-paper-night dark:text-ink-night">
      <div className="mx-auto flex min-h-screen max-w-6xl flex-col px-6 py-10 lg:px-10">
        <header className="mb-10 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 shrink-0 items-end justify-center gap-[3px] rounded-xl bg-accent px-2.5 py-2">
              <span className="w-[3px] rounded-full bg-white" style={{ height: "40%" }} />
              <span className="w-[3px] rounded-full bg-white" style={{ height: "90%" }} />
              <span className="w-[3px] rounded-full bg-white" style={{ height: "60%" }} />
              <span className="w-[3px] rounded-full bg-white" style={{ height: "100%" }} />
            </div>
            <div>
              <h1 className="font-display text-2xl font-extrabold tracking-tight">LiveTranscribe</h1>
              <p className="mt-0.5 text-sm text-ink-soft dark:text-ink-night-soft">
                Transcrição de áudio com entrega progressiva
              </p>
            </div>
          </div>
          <div className="flex items-center gap-3">
            <ConnectionIndicator state={connectionState} />
            <ThemeToggle theme={theme} onToggle={toggleTheme} />
          </div>
        </header>

        <main className="grid flex-1 gap-8 lg:grid-cols-[360px_1fr]">
          <section className="h-fit rounded-2xl border border-line bg-paper-soft p-6 shadow-sm dark:border-line-night dark:bg-paper-night-soft">
            <SourceTabs value={sourceMode} onChange={setSourceMode} disabled={busy || isRecording} />

            {sourceMode === "upload" ? (
              <UploadPanel
                isUploading={isUploading}
                uploadError={uploadError}
                isRateLimited={isRateLimited}
                disabled={busy}
                onSubmit={upload}
              />
            ) : (
              <LivePanel
                isRecording={isRecording}
                micError={micError}
                disabled={busy}
                onStart={startRecording}
                onStop={stopRecording}
              />
            )}
          </section>

          <section className="flex flex-col gap-4">
            <div className="flex items-center justify-between">
              <h2 className="font-display text-lg font-extrabold">Transcrição</h2>
              {sessionId && <StatusBadge status={status} />}
            </div>

            {status === "Failed" && errorMessage && (
              <div className="rounded-lg border border-danger/30 bg-danger/5 px-4 py-3 text-sm text-danger dark:border-danger-soft/30 dark:bg-danger-soft/10 dark:text-danger-soft">
                {errorMessage}
              </div>
            )}

            <TranscriptFeed segments={segments} isActive={status === "Processing"} />
          </section>
        </main>

        <footer className="mt-12 text-center text-xs text-ink-soft/60 dark:text-ink-night-soft/60">
          Upload de arquivo ou microfone ao vivo · entrega progressiva via SignalR
        </footer>
      </div>
    </div>
  );
}
