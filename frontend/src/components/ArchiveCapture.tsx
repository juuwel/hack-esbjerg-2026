import { useCallback, useRef, useState } from "react";
import type { ArchiveDocument, UploadArchiveRequest } from "../types";

interface Props {
  onArchived: (doc: ArchiveDocument) => void;
}

const PLATFORMS = [
  "Twitter / X",
  "Facebook",
  "Instagram",
  "TikTok",
  "YouTube",
  "Discord",
  "Reddit",
  "Local news",
  "Blog",
  "Forum",
  "WhatsApp",
  "Other",
];

const CONTENT_TYPES = [
  "social-post",
  "news-article",
  "community-thread",
  "video",
  "image",
  "document",
  "audio",
  "other",
];

const LANGUAGES = [
  { code: "da", label: "Danish" },
  { code: "en", label: "English" },
  { code: "de", label: "German" },
  { code: "sv", label: "Swedish" },
  { code: "no", label: "Norwegian" },
  { code: "fr", label: "French" },
  { code: "es", label: "Spanish" },
];

const empty: UploadArchiveRequest = {
  title: "",
  sourceUrl: "",
  sourcePlatform: "",
  author: "",
  archivedBy: "",
  contentType: "",
  language: "",
  tags: "",
  location: "",
  community: "",
  historicalContext: "",
  originalCreatedAt: "",
};

// ── Heuristic derivation from file properties ─────────────────────────────

const MIME_TO_CONTENT_TYPE: [RegExp, string][] = [
  [/^image\//, "image"],
  [/^video\//, "video"],
  [/^audio\//, "audio"],
  [/^application\/pdf/, "document"],
  [/^application\/(msword|vnd\.openxmlformats|vnd\.ms-)/, "document"],
  [/^text\/html/, "social-post"],
  [/^text\//, "document"],
];

const MIME_TO_TAGS: [RegExp, string[]][] = [
  [/^image\/gif/, ["gif", "image"]],
  [/^image\//, ["image"]],
  [/^video\//, ["video"]],
  [/^audio\//, ["audio"]],
  [/^application\/pdf/, ["pdf", "document"]],
  [/^text\/html/, ["html", "webpage"]],
];

const PLATFORM_PATTERNS: [RegExp, string][] = [
  [/twitter|tweet|^x[-_]/i, "Twitter / X"],
  [/facebook|fb[-_]/i, "Facebook"],
  [/instagram|ig[-_]/i, "Instagram"],
  [/tiktok/i, "TikTok"],
  [/youtube|yt[-_]/i, "YouTube"],
  [/discord/i, "Discord"],
  [/reddit/i, "Reddit"],
  [/whatsapp/i, "WhatsApp"],
];

const LANG_SUFFIXES: [RegExp, string][] = [
  [/[_-](da|dansk)\b/i, "da"],
  [/[_-](en|english)\b/i, "en"],
  [/[_-](de|deutsch)\b/i, "de"],
  [/[_-](sv|swedish)\b/i, "sv"],
  [/[_-](no|norsk)\b/i, "no"],
  [/[_-](fr|french)\b/i, "fr"],
  [/[_-](es|spanish)\b/i, "es"],
];

/** Convert a raw filename into a human-readable title. */
function fileNameToTitle(name: string): string {
  // Strip extension
  const noExt = name.replace(/\.[^/.]+$/, "");
  // Replace separators with spaces, trim
  return noExt.replace(/[-_]+/g, " ").replace(/\s+/g, " ").trim();
}

/** Format a Date as a datetime-local string (YYYY-MM-DDTHH:MM). */
function toDatetimeLocal(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function deriveMetadata(file: File): Partial<UploadArchiveRequest> {
  const name = file.name;
  const mime = file.type || "";
  const derived: Partial<UploadArchiveRequest> = {};

  // Title from filename
  derived.title = fileNameToTitle(name);

  // Content type from MIME
  for (const [pattern, ct] of MIME_TO_CONTENT_TYPE) {
    if (pattern.test(mime)) {
      derived.contentType = ct;
      break;
    }
  }

  // Platform from filename keywords
  for (const [pattern, platform] of PLATFORM_PATTERNS) {
    if (pattern.test(name)) {
      derived.sourcePlatform = platform;
      break;
    }
  }

  // Language from filename suffix
  for (const [pattern, lang] of LANG_SUFFIXES) {
    if (pattern.test(name)) {
      derived.language = lang;
      break;
    }
  }

  // Tags from MIME
  for (const [pattern, tags] of MIME_TO_TAGS) {
    if (pattern.test(mime)) {
      derived.tags = tags.join(", ");
      break;
    }
  }

  // Original created-at from file last-modified (if it looks plausible — not today)
  if (file.lastModified) {
    const modified = new Date(file.lastModified);
    const now = new Date();
    // Only prefill if the file is at least 1 minute old (avoids "just saved" noise)
    if (now.getTime() - modified.getTime() > 60_000) {
      derived.originalCreatedAt = toDatetimeLocal(modified);
    }
  }

  return derived;
}

export default function ArchiveCapture({ onArchived }: Props) {
  const [step, setStep] = useState<"file" | "meta">("file");
  const [file, setFile] = useState<File | null>(null);
  const [dragOver, setDragOver] = useState(false);
  const [meta, setMeta] = useState<UploadArchiveRequest>(empty);
  const [derivedKeys, setDerivedKeys] = useState<
    Set<keyof UploadArchiveRequest>
  >(new Set());
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const handleFilePick = useCallback((picked: File) => {
    setFile(picked);
    const derived = deriveMetadata(picked);
    setMeta((m) => ({
      ...empty,
      ...derived,
      // Preserve any fields the user has already manually filled
      ...(m.archivedBy ? { archivedBy: m.archivedBy } : {}),
    }));
    setDerivedKeys(
      new Set(Object.keys(derived) as (keyof UploadArchiveRequest)[]),
    );
    setStep("meta");
  }, []);

  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setDragOver(false);
    const f = e.dataTransfer.files?.[0];
    if (f) handleFilePick(f);
  };

  const set =
    (key: keyof UploadArchiveRequest) =>
    (
      e: React.ChangeEvent<
        HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement
      >,
    ) => {
      setMeta((m) => ({ ...m, [key]: e.target.value }));
      setDerivedKeys((prev) => {
        const next = new Set(prev);
        next.delete(key);
        return next;
      });
    };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!file) return;
    setSubmitting(true);
    setError(null);

    const form = new FormData();
    form.append("file", file);
    Object.entries(meta).forEach(([k, v]) => {
      if (v) form.append(k, v);
    });

    try {
      const res = await fetch("/api/archive/files", {
        method: "POST",
        body: form,
      });
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error(body.error ?? `Upload failed (${res.status})`);
      }
      const data = await res.json();
      // Fetch the full document so we can show it in the results
      const docRes = await fetch(`/api/archive/documents/${data.id}`);
      const doc: ArchiveDocument = await docRes.json();
      onArchived(doc);
      // Reset
      setFile(null);
      setMeta(empty);
      setStep("file");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unknown error");
    } finally {
      setSubmitting(false);
    }
  };

  const autoBadge = (key: keyof UploadArchiveRequest) =>
    derivedKeys.has(key) ? (
      <span className="capture__auto-badge">auto</span>
    ) : null;

  return (
    <div className="capture">
      {step === "file" ? (
        <div
          className={`capture__drop${dragOver ? " capture__drop--over" : ""}`}
          onClick={() => inputRef.current?.click()}
          onDragOver={(e) => {
            e.preventDefault();
            setDragOver(true);
          }}
          onDragLeave={() => setDragOver(false)}
          onDrop={handleDrop}
          role="button"
          tabIndex={0}
          aria-label="Choose a file to archive"
          onKeyDown={(e) => e.key === "Enter" && inputRef.current?.click()}
        >
          <input
            ref={inputRef}
            type="file"
            style={{ display: "none" }}
            onChange={(e) => {
              const f = e.target.files?.[0];
              if (f) handleFilePick(f);
              e.target.value = "";
            }}
            tabIndex={-1}
            aria-hidden="true"
          />
          <svg
            className="capture__drop-icon"
            viewBox="0 0 24 24"
            fill="none"
            aria-hidden="true"
          >
            <path
              d="M12 16V8m0 0-3 3m3-3 3 3"
              stroke="currentColor"
              strokeWidth="1.5"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
            <path
              d="M4 16v1a3 3 0 0 0 3 3h10a3 3 0 0 0 3-3v-1"
              stroke="currentColor"
              strokeWidth="1.5"
              strokeLinecap="round"
            />
          </svg>
          <p className="capture__drop-label">
            {dragOver
              ? "Drop to begin archiving"
              : "Drop a file or click to pick one"}
          </p>
          <p className="capture__drop-sub">
            Screenshots, PDFs, images, video, audio — anything worth keeping
          </p>
        </div>
      ) : (
        <form className="capture__form" onSubmit={handleSubmit} noValidate>
          {/* Selected file pill */}
          <div className="capture__file-pill">
            <svg viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
              <path d="M3 4a2 2 0 0 1 2-2h6l5 5v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V4z" />
            </svg>
            <span>{file?.name}</span>
            <button
              type="button"
              className="capture__file-pill-remove"
              onClick={() => {
                setFile(null);
                setStep("file");
              }}
              aria-label="Remove file"
            >
              ✕
            </button>
          </div>

          <fieldset className="capture__fieldset">
            <legend className="capture__legend">Core</legend>
            <label className="capture__label" htmlFor="title">
              Title {autoBadge("title")}
            </label>
            <input
              className="capture__input"
              id="title"
              type="text"
              placeholder="Give this item a clear title"
              value={meta.title}
              onChange={set("title")}
            />

            <label className="capture__label" htmlFor="sourceUrl">
              Original URL
            </label>
            <input
              className="capture__input"
              id="sourceUrl"
              type="url"
              placeholder="https://…"
              value={meta.sourceUrl}
              onChange={set("sourceUrl")}
            />

            <div className="capture__row">
              <div className="capture__col">
                <label className="capture__label" htmlFor="sourcePlatform">
                  Platform {autoBadge("sourcePlatform")}
                </label>
                <select
                  className="capture__select"
                  id="sourcePlatform"
                  value={meta.sourcePlatform}
                  onChange={set("sourcePlatform")}
                >
                  <option value="">— pick one —</option>
                  {PLATFORMS.map((p) => (
                    <option key={p} value={p}>
                      {p}
                    </option>
                  ))}
                </select>
              </div>
              <div className="capture__col">
                <label className="capture__label" htmlFor="contentType">
                  Content type {autoBadge("contentType")}
                </label>
                <select
                  className="capture__select"
                  id="contentType"
                  value={meta.contentType}
                  onChange={set("contentType")}
                >
                  <option value="">— pick one —</option>
                  {CONTENT_TYPES.map((c) => (
                    <option key={c} value={c}>
                      {c}
                    </option>
                  ))}
                </select>
              </div>
            </div>
          </fieldset>

          <fieldset className="capture__fieldset">
            <legend className="capture__legend">Provenance</legend>
            <div className="capture__row">
              <div className="capture__col">
                <label className="capture__label" htmlFor="author">
                  Original author
                </label>
                <input
                  className="capture__input"
                  id="author"
                  type="text"
                  placeholder="Name or handle"
                  value={meta.author}
                  onChange={set("author")}
                />
              </div>
              <div className="capture__col">
                <label className="capture__label" htmlFor="archivedBy">
                  Archived by
                </label>
                <input
                  className="capture__input"
                  id="archivedBy"
                  type="text"
                  placeholder="Your name or system"
                  value={meta.archivedBy}
                  onChange={set("archivedBy")}
                />
              </div>
            </div>
            <div className="capture__row">
              <div className="capture__col">
                <label className="capture__label" htmlFor="originalCreatedAt">
                  Originally published {autoBadge("originalCreatedAt")}
                </label>
                <input
                  className="capture__input"
                  id="originalCreatedAt"
                  type="datetime-local"
                  value={meta.originalCreatedAt}
                  onChange={set("originalCreatedAt")}
                />
              </div>
              <div className="capture__col">
                <label className="capture__label" htmlFor="language">
                  Language {autoBadge("language")}
                </label>
                <select
                  className="capture__select"
                  id="language"
                  value={meta.language}
                  onChange={set("language")}
                >
                  <option value="">— pick one —</option>
                  {LANGUAGES.map((l) => (
                    <option key={l.code} value={l.code}>
                      {l.label}
                    </option>
                  ))}
                </select>
              </div>
            </div>
          </fieldset>

          <fieldset className="capture__fieldset">
            <legend className="capture__legend">Context &amp; discovery</legend>
            <div className="capture__row">
              <div className="capture__col">
                <label className="capture__label" htmlFor="location">
                  Location
                </label>
                <input
                  className="capture__input"
                  id="location"
                  type="text"
                  placeholder="City, region or country"
                  value={meta.location}
                  onChange={set("location")}
                />
              </div>
              <div className="capture__col">
                <label className="capture__label" htmlFor="community">
                  Community
                </label>
                <input
                  className="capture__input"
                  id="community"
                  type="text"
                  placeholder="Group, forum, school…"
                  value={meta.community}
                  onChange={set("community")}
                />
              </div>
            </div>
            <label className="capture__label" htmlFor="tags">
              Tags {autoBadge("tags")}{" "}
              <span className="capture__hint">(comma-separated)</span>
            </label>
            <input
              className="capture__input"
              id="tags"
              type="text"
              placeholder="esbjerg, 2026, election, local"
              value={meta.tags}
              onChange={set("tags")}
            />

            <label className="capture__label" htmlFor="historicalContext">
              Historical context
            </label>
            <textarea
              className="capture__textarea"
              id="historicalContext"
              rows={3}
              placeholder="What was happening when this was saved? Why does it matter? What should someone in 2075 know to understand it?"
              value={meta.historicalContext}
              onChange={set("historicalContext")}
            />
          </fieldset>

          {error && (
            <div className="capture__error" role="alert">
              <svg viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
                <path
                  fillRule="evenodd"
                  d="M10 18a8 8 0 1 0 0-16 8 8 0 0 0 0 16zm-.75-4.75a.75.75 0 0 0 1.5 0v-4.5a.75.75 0 0 0-1.5 0v4.5zm.75-7a.75.75 0 1 0 0 1.5.75.75 0 0 0 0-1.5z"
                  clipRule="evenodd"
                />
              </svg>
              {error}
            </div>
          )}

          <div className="capture__actions">
            <button
              type="button"
              className="btn btn--ghost"
              onClick={() => setStep("file")}
              disabled={submitting}
            >
              ← Back
            </button>
            <button
              type="submit"
              className="btn btn--primary"
              disabled={submitting}
            >
              {submitting ? (
                <span className="spinner" aria-label="Saving…" />
              ) : null}
              {submitting ? "Archiving…" : "Archive it"}
            </button>
          </div>
        </form>
      )}
    </div>
  );
}
