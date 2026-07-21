import { useEffect, useRef } from "react";
import type { TranscriptSegment } from "../types/transcription";
import { formatTimestamp } from "../utils/format";

interface TranscriptFeedProps {
  segments: TranscriptSegment[];
  isActive: boolean;
}

export function TranscriptFeed({ segments, isActive }: TranscriptFeedProps) {
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [segments.length]);

  if (segments.length === 0) {
    return (
      <div className="flex h-full min-h-64 flex-col items-center justify-center gap-2 rounded-2xl border border-dashed border-line px-6 py-16 text-center dark:border-line-night">
        <p className="text-lg font-semibold text-ink-soft dark:text-ink-night-soft">
          {isActive ? "Ouvindo o áudio…" : "A transcrição aparece aqui, trecho por trecho"}
        </p>
        <p className="text-sm text-ink-soft/70 dark:text-ink-night-soft/70">
          {isActive ? "O primeiro trecho leva alguns segundos" : "Envie um arquivo de áudio para começar"}
        </p>
      </div>
    );
  }

  return (
    <div className="rounded-2xl border border-line bg-paper-soft p-5 shadow-sm dark:border-line-night dark:bg-paper-night-soft">
      <div className="flex flex-col gap-4">
        {segments.map((segment) => (
          <article key={segment.id} className="animate-rise flex gap-3">
            <time className="mt-0.5 h-fit shrink-0 rounded-md bg-accent/10 px-1.5 py-0.5 font-mono text-xs text-accent dark:bg-accent-soft/15 dark:text-accent-soft">
              {formatTimestamp(segment.startMs)}
            </time>
            <p className="text-[1.05rem] leading-relaxed text-ink dark:text-ink-night">
              {segment.text || <span className="italic text-ink-soft dark:text-ink-night-soft">(sem fala detectada)</span>}
            </p>
          </article>
        ))}
        <div ref={bottomRef} />
      </div>
    </div>
  );
}
