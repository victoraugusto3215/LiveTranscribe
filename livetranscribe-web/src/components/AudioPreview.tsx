import { useEffect, useState } from "react";

export function AudioPreview({ file }: { file: File }) {
  const [objectUrl, setObjectUrl] = useState<string | null>(null);

  useEffect(() => {
    // criar e revogar dentro do mesmo efeito evita que o StrictMode
    // (monta -> desmonta -> remonta em dev) revogue uma URL que um
    // useMemo memoizado não recriaria na remontagem
    const url = URL.createObjectURL(file);
    setObjectUrl(url);
    return () => URL.revokeObjectURL(url);
  }, [file]);

  if (!objectUrl) {
    return null;
  }

  return (
    // eslint-disable-next-line jsx-a11y/media-has-caption
    <audio controls src={objectUrl} className="w-full" />
  );
}
