import type { ConnectionState } from "../hooks/useTranscriptionSession";

const CONFIG: Record<ConnectionState, { label: string; dot: string }> = {
  connecting: { label: "Conectando", dot: "bg-warning dark:bg-warning-soft" },
  connected: { label: "Ao vivo", dot: "bg-success dark:bg-success-soft" },
  reconnecting: { label: "Reconectando", dot: "bg-warning dark:bg-warning-soft" },
  offline: { label: "Sem conexão", dot: "bg-danger dark:bg-danger-soft" },
};

export function ConnectionIndicator({ state }: { state: ConnectionState }) {
  const config = CONFIG[state];
  return (
    <span className="inline-flex items-center gap-1.5 font-mono text-xs text-ink-soft dark:text-ink-night-soft">
      <span className={`h-1.5 w-1.5 rounded-full ${config.dot}`} />
      {config.label}
    </span>
  );
}
