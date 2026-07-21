export type SourceMode = "upload" | "live";

interface SourceTabsProps {
  value: SourceMode;
  onChange: (mode: SourceMode) => void;
  disabled: boolean;
}

const TABS: { mode: SourceMode; label: string }[] = [
  { mode: "upload", label: "Enviar arquivo" },
  { mode: "live", label: "Microfone ao vivo" },
];

export function SourceTabs({ value, onChange, disabled }: SourceTabsProps) {
  return (
    <div className="mb-4 flex rounded-lg border border-line bg-paper p-1 dark:border-line-night dark:bg-paper-night">
      {TABS.map((tab) => (
        <button
          key={tab.mode}
          type="button"
          disabled={disabled && value !== tab.mode}
          onClick={() => onChange(tab.mode)}
          className={`flex-1 rounded-md px-3 py-1.5 text-sm font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-40 ${
            value === tab.mode
              ? "bg-accent text-white shadow-sm"
              : "text-ink-soft hover:text-ink dark:text-ink-night-soft dark:hover:text-ink-night"
          }`}
        >
          {tab.label}
        </button>
      ))}
    </div>
  );
}
