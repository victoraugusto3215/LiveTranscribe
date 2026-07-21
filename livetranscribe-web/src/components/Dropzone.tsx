import { useCallback, useRef, useState } from "react";
import type { DragEvent } from "react";
import { formatFileSize } from "../utils/format";
import { ACCEPT_ATTRIBUTE, ALLOWED_AUDIO_EXTENSIONS } from "../constants/audioFormats";

interface DropzoneProps {
  file: File | null;
  onFileSelected: (file: File) => void;
  error: string | null;
}

export function Dropzone({ file, onFileSelected, error }: DropzoneProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [isDragging, setIsDragging] = useState(false);

  const handleFiles = useCallback(
    (fileList: FileList | null) => {
      const selected = fileList?.[0];
      if (selected) {
        onFileSelected(selected);
      }
    },
    [onFileSelected],
  );

  const handleDrop = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setIsDragging(false);
    handleFiles(event.dataTransfer.files);
  };

  return (
    <div>
      <div
        role="button"
        tabIndex={0}
        onClick={() => inputRef.current?.click()}
        onKeyDown={(e) => e.key === "Enter" && inputRef.current?.click()}
        onDragOver={(e) => {
          e.preventDefault();
          setIsDragging(true);
        }}
        onDragLeave={() => setIsDragging(false)}
        onDrop={handleDrop}
        className={`flex cursor-pointer flex-col items-center justify-center gap-2 rounded-xl border-2 border-dashed px-6 py-10 text-center transition-colors ${
          isDragging
            ? "border-accent bg-accent/5"
            : "border-line bg-paper hover:border-accent/60 dark:border-line-night dark:bg-paper-night dark:hover:border-accent-soft/60"
        }`}
      >
        <svg
          width="28"
          height="28"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.6"
          className="text-accent dark:text-accent-soft"
        >
          <path d="M12 16V4M12 4l-4 4M12 4l4 4" strokeLinecap="round" strokeLinejoin="round" />
          <path d="M4 16v2a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-2" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
        {file ? (
          <div>
            <p className="font-medium text-ink dark:text-ink-night">{file.name}</p>
            <p className="text-sm text-ink-soft dark:text-ink-night-soft">{formatFileSize(file.size)}</p>
          </div>
        ) : (
          <div>
            <p className="font-medium text-ink dark:text-ink-night">Arraste um arquivo de áudio aqui</p>
            <p className="text-sm text-ink-soft dark:text-ink-night-soft">ou clique para escolher</p>
            <p className="mt-2 text-xs uppercase tracking-wide text-ink-soft/60 dark:text-ink-night-soft/60">
              {ALLOWED_AUDIO_EXTENSIONS.map((ext) => ext.slice(1)).join(" · ")}
            </p>
          </div>
        )}
      </div>
      <input
        ref={inputRef}
        type="file"
        accept={ACCEPT_ATTRIBUTE}
        className="hidden"
        onChange={(e) => handleFiles(e.target.files)}
      />
      {error && <p className="mt-2 text-sm text-danger dark:text-danger-soft">{error}</p>}
    </div>
  );
}
