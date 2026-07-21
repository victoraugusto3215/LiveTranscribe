import type { ReactNode } from "react";

export type LanguageCode = "pt" | "en" | "es" | "it" | "fr" | "de" | "el" | "ru";

// Simplified flat flags at 20x14, drawn inline so rendering never depends on
// the OS/browser having color emoji fonts installed (regional-indicator flag
// emoji silently fall back to plain two-letter text on several platforms).
const FLAGS: Record<LanguageCode, ReactNode> = {
  pt: (
    <>
      <rect width="20" height="14" fill="#2e7d32" />
      <polygon points="10,1.5 18.5,7 10,12.5 1.5,7" fill="#fcd116" />
      <circle cx="10" cy="7" r="2.6" fill="#1351d8" />
    </>
  ),
  en: (
    <>
      <rect width="20" height="14" fill="#fff" />
      {[0, 2, 4, 6, 8, 10, 12].map((y) =>
        y % 4 === 0 ? <rect key={y} y={y} width="20" height="2" fill="#b22234" /> : null,
      )}
      <rect width="9" height="8" fill="#3c3b6e" />
      <circle cx="2.2" cy="1.8" r="0.5" fill="#fff" />
      <circle cx="4.6" cy="1.8" r="0.5" fill="#fff" />
      <circle cx="7" cy="1.8" r="0.5" fill="#fff" />
      <circle cx="3.4" cy="3.4" r="0.5" fill="#fff" />
      <circle cx="5.8" cy="3.4" r="0.5" fill="#fff" />
      <circle cx="2.2" cy="5" r="0.5" fill="#fff" />
      <circle cx="4.6" cy="5" r="0.5" fill="#fff" />
      <circle cx="7" cy="5" r="0.5" fill="#fff" />
      <circle cx="3.4" cy="6.6" r="0.5" fill="#fff" />
      <circle cx="5.8" cy="6.6" r="0.5" fill="#fff" />
    </>
  ),
  es: (
    <>
      <rect width="20" height="14" fill="#aa151b" />
      <rect y="3.5" width="20" height="7" fill="#f1bf00" />
    </>
  ),
  it: (
    <>
      <rect width="6.67" height="14" fill="#009246" />
      <rect x="6.67" width="6.67" height="14" fill="#fff" />
      <rect x="13.33" width="6.67" height="14" fill="#ce2b37" />
    </>
  ),
  fr: (
    <>
      <rect width="6.67" height="14" fill="#0055a4" />
      <rect x="6.67" width="6.67" height="14" fill="#fff" />
      <rect x="13.33" width="6.67" height="14" fill="#ef4135" />
    </>
  ),
  de: (
    <>
      <rect width="20" height="4.67" fill="#000" />
      <rect y="4.67" width="20" height="4.67" fill="#dd0000" />
      <rect y="9.33" width="20" height="4.67" fill="#ffce00" />
    </>
  ),
  el: (
    <>
      <rect width="20" height="14" fill="#0d5eaf" />
      {[0, 2.8, 5.6, 8.4, 11.2].map((y, i) =>
        i % 2 === 0 ? null : <rect key={y} y={y} width="20" height="1.4" fill="#fff" />,
      )}
      <rect width="8" height="6" fill="#0d5eaf" />
      <rect x="3.2" width="1.6" height="6" fill="#fff" />
      <rect y="2.2" width="8" height="1.6" fill="#fff" />
    </>
  ),
  ru: (
    <>
      <rect width="20" height="4.67" fill="#fff" />
      <rect y="4.67" width="20" height="4.67" fill="#0039a6" />
      <rect y="9.33" width="20" height="4.67" fill="#d52b1e" />
    </>
  ),
};

export function Flag({ code }: { code: LanguageCode }) {
  return (
    <svg
      width="20"
      height="14"
      viewBox="0 0 20 14"
      className="shrink-0 overflow-hidden rounded-[3px] shadow-[0_0_0_1px_rgba(0,0,0,0.08)]"
      aria-hidden="true"
    >
      {FLAGS[code]}
    </svg>
  );
}
