import { useState } from "react";
import type { ArchiveDocument } from "../types";

interface Props {
  doc: ArchiveDocument;
  onDeleted: (id: string) => void;
}

const PLATFORM_COLORS: Record<string, string> = {
  "Twitter / X": "#1d9bf0",
  Facebook: "#1877f2",
  Instagram: "#e1306c",
  TikTok: "#010101",
  YouTube: "#ff0000",
  Discord: "#5865f2",
  Reddit: "#ff4500",
  "Local news": "#2d6a4f",
  Blog: "#6b5b95",
  Forum: "#b5838d",
  WhatsApp: "#25d366",
};

function formatDate(iso?: string) {
  if (!iso) return null;
  return new Date(iso).toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

export default function ArchiveCard({ doc, onDeleted }: Props) {
  const [deleting, setDeleting] = useState(false);
  const [expanded, setExpanded] = useState(false);

  const handleDelete = async () => {
    if (!confirm(`Delete "${doc.title ?? doc.id}"? This cannot be undone.`))
      return;
    setDeleting(true);
    try {
      await Promise.all([
        doc.objectName
          ? fetch(
              `/api/archive/files?objectName=${encodeURIComponent(doc.objectName)}`,
              { method: "DELETE" },
            )
          : Promise.resolve(),
        fetch(`/api/archive/documents/${doc.id}`, { method: "DELETE" }),
      ]);
      onDeleted(doc.id);
    } catch {
      setDeleting(false);
    }
  };

  const platformColor = doc.sourcePlatform
    ? (PLATFORM_COLORS[doc.sourcePlatform] ?? "var(--accent)")
    : "var(--accent)";

  return (
    <article className={`card${expanded ? " card--expanded" : ""}`}>
      <div className="card__header">
        <div className="card__badges">
          {doc.sourcePlatform && (
            <span
              className="card__badge"
              style={{
                background: platformColor + "22",
                color: platformColor,
                borderColor: platformColor + "55",
              }}
            >
              {doc.sourcePlatform}
            </span>
          )}
          {doc.contentType && (
            <span className="card__badge card__badge--neutral">
              {doc.contentType}
            </span>
          )}
          {doc.language && (
            <span className="card__badge card__badge--neutral">
              {doc.language.toUpperCase()}
            </span>
          )}
        </div>
        <time className="card__date" dateTime={doc.capturedAt}>
          Archived {formatDate(doc.capturedAt)}
        </time>
      </div>

      <h3 className="card__title">{doc.title ?? doc.id}</h3>

      {doc.historicalContext && (
        <blockquote className="card__context">
          <svg viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
            <path d="M6.29 3.31C4.14 4.95 3 7.1 3 9.5c0 2.97 1.67 5.03 4 5.5V17l4-4-4-4v2.14C5.44 10.7 5 9.2 5 9.5c0-1.57.85-3.05 2.47-4.27L6.29 3.31zm8 0C12.14 4.95 11 7.1 11 9.5c0 2.97 1.67 5.03 4 5.5V17l4-4-4-4v2.14c-1.56-.44-2-1.94-2-1.64 0-1.57.85-3.05 2.47-4.27l-1.18-1.92z" />
          </svg>
          <p>{doc.historicalContext}</p>
        </blockquote>
      )}

      {expanded && (
        <div className="card__details">
          {doc.author && (
            <p className="card__detail">
              <span>Author</span>
              {doc.author}
            </p>
          )}
          {doc.archivedBy && (
            <p className="card__detail">
              <span>Archived by</span>
              {doc.archivedBy}
            </p>
          )}
          {doc.originalCreatedAt && (
            <p className="card__detail">
              <span>Originally published</span>
              {formatDate(doc.originalCreatedAt)}
            </p>
          )}
          {doc.location && (
            <p className="card__detail">
              <span>Location</span>
              {doc.location}
            </p>
          )}
          {doc.community && (
            <p className="card__detail">
              <span>Community</span>
              {doc.community}
            </p>
          )}
          {doc.sourceUrl && (
            <p className="card__detail">
              <span>Source URL</span>
              <a
                href={doc.sourceUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="card__link"
              >
                {doc.sourceUrl}
              </a>
            </p>
          )}
          {doc.format && (
            <p className="card__detail">
              <span>Format</span>
              {doc.format}
            </p>
          )}
          {doc.checksumSha256 && (
            <p className="card__detail">
              <span>SHA-256</span>
              <code className="card__hash">{doc.checksumSha256}</code>
            </p>
          )}
        </div>
      )}

      {doc.tags.length > 0 && (
        <ul className="card__tags" aria-label="Tags">
          {doc.tags.map((t) => (
            <li key={t} className="card__tag">
              {t}
            </li>
          ))}
        </ul>
      )}

      <div className="card__footer">
        <button
          className="btn btn--ghost btn--sm"
          onClick={() => setExpanded((v) => !v)}
        >
          {expanded ? "Less detail" : "More detail"}
        </button>
        <div className="card__footer-actions">
          {doc.objectName && (
            <a
              href={`/api/archive/files?objectName=${encodeURIComponent(doc.objectName)}`}
              target="_blank"
              rel="noopener noreferrer"
              className="btn btn--secondary btn--sm"
            >
              <svg viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
                <path d="M10 2a.75.75 0 0 1 .75.75v8.69l2.47-2.47a.75.75 0 1 1 1.06 1.06l-3.75 3.75a.75.75 0 0 1-1.06 0L5.72 10.03a.75.75 0 1 1 1.06-1.06L9.25 11.44V2.75A.75.75 0 0 1 10 2zM4.5 15.25a.75.75 0 0 0 0 1.5h11a.75.75 0 0 0 0-1.5h-11z" />
              </svg>
              Download
            </a>
          )}
          <button
            className="btn btn--danger btn--sm"
            onClick={handleDelete}
            disabled={deleting}
          >
            {deleting ? (
              <span className="spinner spinner--sm" aria-label="Deleting…" />
            ) : (
              <svg viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
                <path
                  fillRule="evenodd"
                  d="M8.75 1A2.75 2.75 0 0 0 6 3.75v.443c-.795.077-1.584.176-2.365.298a.75.75 0 1 0 .23 1.482l.149-.022.841 10.518A2.75 2.75 0 0 0 7.596 19h4.807a2.75 2.75 0 0 0 2.742-2.53l.841-10.52.149.023a.75.75 0 0 0 .23-1.482A41.03 41.03 0 0 0 14 4.193v-.443A2.75 2.75 0 0 0 11.25 1h-2.5zm0 1.5h2.5c.69 0 1.25.56 1.25 1.25v.37a42.553 42.553 0 0 0-5 0v-.37c0-.69.56-1.25 1.25-1.25zm-3.36 4.17.844 10.53a1.25 1.25 0 0 0 1.245 1.15h4.807a1.25 1.25 0 0 0 1.245-1.15l.844-10.53H5.39z"
                  clipRule="evenodd"
                />
              </svg>
            )}
            Delete
          </button>
        </div>
      </div>
    </article>
  );
}
