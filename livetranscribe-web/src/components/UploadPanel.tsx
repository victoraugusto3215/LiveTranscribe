import { useState } from "react";
import { Dropzone } from "./Dropzone";
import { LanguageSelect } from "./LanguageSelect";
import { AudioPreview } from "./AudioPreview";
import {
  ALLOWED_AUDIO_EXTENSIONS,
  hasAllowedExtension,
  MAX_FILE_SIZE_BYTES,
  MAX_UPLOADS_PER_WINDOW,
  RATE_LIMIT_WINDOW_LABEL,
} from "../constants/audioFormats";
import { formatFileSize } from "../utils/format";

interface UploadPanelProps {
  isUploading: boolean;
  uploadError: string | null;
  isRateLimited: boolean;
  disabled: boolean;
  onSubmit: (file: File, languageCode: string) => void;
}

export function UploadPanel({ isUploading, uploadError, isRateLimited, disabled, onSubmit }: UploadPanelProps) {
  const [file, setFile] = useState<File | null>(null);
  const [languageCode, setLanguageCode] = useState("pt");
  const [fileError, setFileError] = useState<string | null>(null);

  const handleFileSelected = (selected: File) => {
    if (!hasAllowedExtension(selected.name)) {
      setFileError(`Formatos aceitos: ${ALLOWED_AUDIO_EXTENSIONS.map((ext) => ext.slice(1)).join(", ")}.`);
      setFile(null);
      return;
    }
    if (selected.size > MAX_FILE_SIZE_BYTES) {
      setFileError(`Arquivo muito grande (${formatFileSize(selected.size)}). Limite: ${formatFileSize(MAX_FILE_SIZE_BYTES)}.`);
      setFile(null);
      return;
    }
    setFileError(null);
    setFile(selected);
  };

  return (
    <div className="flex flex-col gap-4">
      <Dropzone file={file} onFileSelected={handleFileSelected} error={fileError} />

      {file && <AudioPreview file={file} />}

      <div className="flex items-center gap-3">
        <label className="text-sm text-ink-soft dark:text-ink-night-soft" htmlFor="language">
          Idioma
        </label>
        <div className="flex-1" id="language">
          <LanguageSelect value={languageCode} onChange={setLanguageCode} />
        </div>
      </div>

      <button
        type="button"
        disabled={!file || isUploading || disabled}
        onClick={() => file && onSubmit(file, languageCode)}
        className="rounded-lg bg-accent px-4 py-2.5 font-medium text-white shadow-sm transition-colors hover:bg-accent/90 disabled:cursor-not-allowed disabled:opacity-40"
      >
        {isUploading ? "Enviando…" : "Transcrever áudio"}
      </button>

      <p className="font-mono text-xs text-ink-soft/60 dark:text-ink-night-soft/60">
        Máx. {formatFileSize(MAX_FILE_SIZE_BYTES)} · até {MAX_UPLOADS_PER_WINDOW} envios a cada {RATE_LIMIT_WINDOW_LABEL} por conexão
      </p>

      {uploadError && (
        <p className={`text-sm ${isRateLimited ? "text-warning dark:text-warning-soft" : "text-danger dark:text-danger-soft"}`}>
          {uploadError}
        </p>
      )}
    </div>
  );
}
