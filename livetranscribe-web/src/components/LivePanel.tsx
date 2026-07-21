import { useEffect, useState } from "react";
import { LanguageSelect } from "./LanguageSelect";
import { formatTimestamp } from "../utils/format";

interface LivePanelProps {
  isRecording: boolean;
  micError: string | null;
  disabled: boolean;
  onStart: (languageCode: string) => void;
  onStop: () => void;
}

export function LivePanel({ isRecording, micError, disabled, onStart, onStop }: LivePanelProps) {
  const [languageCode, setLanguageCode] = useState("pt");
  const [elapsedMs, setElapsedMs] = useState(0);

  useEffect(() => {
    if (!isRecording) {
      setElapsedMs(0);
      return;
    }
    const startedAt = Date.now();
    const interval = window.setInterval(() => setElapsedMs(Date.now() - startedAt), 250);
    return () => window.clearInterval(interval);
  }, [isRecording]);

  return (
    <div className="flex flex-col items-center gap-4 py-2">
      <div className="relative">
        {isRecording && (
          <span className="absolute inset-0 -m-2 animate-ping rounded-full bg-danger/30" />
        )}
        <button
          type="button"
          disabled={disabled && !isRecording}
          onClick={() => (isRecording ? onStop() : onStart(languageCode))}
          aria-label={isRecording ? "Parar gravação" : "Começar a gravar"}
          className={`relative flex h-20 w-20 items-center justify-center rounded-full shadow-md transition-colors disabled:cursor-not-allowed disabled:opacity-40 ${
            isRecording
              ? "bg-danger text-white hover:bg-danger/90"
              : "bg-accent text-white hover:bg-accent/90"
          }`}
        >
          <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
            {isRecording ? (
              <rect x="7" y="7" width="10" height="10" rx="2" />
            ) : (
              <>
                <rect x="9" y="2" width="6" height="12" rx="3" strokeLinecap="round" />
                <path d="M5 11a7 7 0 0 0 14 0M12 18v3" strokeLinecap="round" strokeLinejoin="round" />
              </>
            )}
          </svg>
        </button>
      </div>

      {isRecording ? (
        <p className="flex items-center gap-2 font-mono text-sm text-danger dark:text-danger-soft">
          <span className="h-2 w-2 rounded-full bg-danger animate-breathe dark:bg-danger-soft" />
          {formatTimestamp(elapsedMs)}
        </p>
      ) : (
        <p className="text-sm text-ink-soft dark:text-ink-night-soft">Clique pra começar a falar</p>
      )}

      <div className="flex w-full items-center gap-3">
        <label className="text-sm text-ink-soft dark:text-ink-night-soft" htmlFor="live-language">
          Idioma
        </label>
        <div className="flex-1" id="live-language">
          <LanguageSelect value={languageCode} onChange={setLanguageCode} disabled={isRecording} />
        </div>
      </div>

      {micError && <p className="text-sm text-danger dark:text-danger-soft">{micError}</p>}
    </div>
  );
}
