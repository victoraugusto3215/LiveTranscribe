import type { SessionStatus } from "../types/transcription";

const CONFIG: Record<SessionStatus | "idle", { label: string; dot: string; text: string; bg: string; pulse?: boolean }> = {
  idle: {
    label: "Aguardando áudio",
    dot: "bg-ink-soft/50 dark:bg-ink-night-soft/50",
    text: "text-ink-soft dark:text-ink-night-soft",
    bg: "bg-ink-soft/10 dark:bg-ink-night-soft/10",
  },
  Processing: {
    label: "Transcrevendo",
    dot: "bg-warning dark:bg-warning-soft",
    text: "text-warning dark:text-warning-soft",
    bg: "bg-warning/10 dark:bg-warning-soft/10",
    pulse: true,
  },
  Completed: {
    label: "Concluído",
    dot: "bg-success dark:bg-success-soft",
    text: "text-success dark:text-success-soft",
    bg: "bg-success/10 dark:bg-success-soft/10",
  },
  Failed: {
    label: "Falhou",
    dot: "bg-danger dark:bg-danger-soft",
    text: "text-danger dark:text-danger-soft",
    bg: "bg-danger/10 dark:bg-danger-soft/10",
  },
};

export function StatusBadge({ status }: { status: SessionStatus | "idle" }) {
  const config = CONFIG[status];
  return (
    <span className={`inline-flex items-center gap-2 rounded-full px-3 py-1 font-mono text-xs font-semibold ${config.text} ${config.bg}`}>
      <span className={`h-2 w-2 rounded-full ${config.dot} ${config.pulse ? "animate-breathe" : ""}`} />
      {config.label}
    </span>
  );
}
