import { useCallback, useRef, useState } from "react";
import "./FileManager.css";

const API_BASE = "/api/file";

interface UploadedFile {
  objectName: string;
  fileName: string;
  contentType: string;
  size: number;
  url: string;
}

export default function FileManager() {
  const [files, setFiles] = useState<UploadedFile[]>([]);
  const [uploading, setUploading] = useState(false);
  const [dragOver, setDragOver] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [deletingKey, setDeletingKey] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const uploadFile = useCallback(async (file: File) => {
    setUploading(true);
    setError(null);
    const form = new FormData();
    form.append("file", file);

    try {
      const res = await fetch(API_BASE, { method: "POST", body: form });
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error(body.error ?? `Upload failed (${res.status})`);
      }
      const data: UploadedFile = await res.json();
      setFiles((prev) => [data, ...prev]);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unknown error");
    } finally {
      setUploading(false);
    }
  }, []);

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const picked = e.target.files?.[0];
    if (picked) uploadFile(picked);
    e.target.value = "";
  };

  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setDragOver(false);
    const dropped = e.dataTransfer.files?.[0];
    if (dropped) uploadFile(dropped);
  };

  const handleDelete = async (objectName: string) => {
    setDeletingKey(objectName);
    setError(null);
    try {
      const res = await fetch(
        `${API_BASE}?objectName=${encodeURIComponent(objectName)}`,
        {
          method: "DELETE",
        },
      );
      if (!res.ok && res.status !== 204) {
        const body = await res.json().catch(() => ({}));
        throw new Error(body.error ?? `Delete failed (${res.status})`);
      }
      setFiles((prev) => prev.filter((f) => f.objectName !== objectName));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unknown error");
    } finally {
      setDeletingKey(null);
    }
  };

  const formatSize = (bytes: number) => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  const isImage = (contentType: string) => contentType.startsWith("image/");

  return (
    <div className="fm-root">
      <header className="fm-header">
        <svg
          className="fm-header-icon"
          viewBox="0 0 24 24"
          fill="none"
          aria-hidden="true"
        >
          <path
            d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7z"
            stroke="currentColor"
            strokeWidth="1.5"
            strokeLinejoin="round"
          />
        </svg>
        <h1>File Archive</h1>
        <p>Upload, preview and manage your files</p>
      </header>

      {/* Drop Zone */}
      <div
        className={`fm-dropzone${dragOver ? " fm-dropzone--active" : ""}${uploading ? " fm-dropzone--busy" : ""}`}
        onClick={() => !uploading && inputRef.current?.click()}
        onDragOver={(e) => {
          e.preventDefault();
          setDragOver(true);
        }}
        onDragLeave={() => setDragOver(false)}
        onDrop={handleDrop}
        role="button"
        tabIndex={0}
        aria-label="Upload file"
        onKeyDown={(e) => e.key === "Enter" && inputRef.current?.click()}
      >
        <input
          ref={inputRef}
          type="file"
          className="fm-hidden-input"
          onChange={handleInputChange}
          aria-hidden="true"
          tabIndex={-1}
        />
        {uploading ? (
          <span className="fm-spinner" aria-label="Uploading…" />
        ) : (
          <svg
            className="fm-upload-icon"
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
        )}
        <span className="fm-dropzone-label">
          {uploading
            ? "Uploading…"
            : dragOver
              ? "Drop to upload"
              : "Click or drag a file here"}
        </span>
      </div>

      {/* Error banner */}
      {error && (
        <div className="fm-error" role="alert">
          <svg viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
            <path
              fillRule="evenodd"
              d="M10 18a8 8 0 1 0 0-16 8 8 0 0 0 0 16zm-.75-4.75a.75.75 0 0 0 1.5 0v-4.5a.75.75 0 0 0-1.5 0v4.5zm.75-7a.75.75 0 1 0 0 1.5.75.75 0 0 0 0-1.5z"
              clipRule="evenodd"
            />
          </svg>
          {error}
          <button
            className="fm-error-close"
            onClick={() => setError(null)}
            aria-label="Dismiss"
          >
            ✕
          </button>
        </div>
      )}

      {/* File list */}
      {files.length === 0 ? (
        <p className="fm-empty">No files uploaded yet.</p>
      ) : (
        <ul className="fm-list">
          {files.map((f) => (
            <li key={f.objectName} className="fm-item">
              <div className="fm-item-thumb">
                {isImage(f.contentType) ? (
                  <img src={f.url} alt={f.fileName} className="fm-thumb-img" />
                ) : (
                  <svg
                    className="fm-thumb-icon"
                    viewBox="0 0 24 24"
                    fill="none"
                    aria-hidden="true"
                  >
                    <path
                      d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8l-6-6z"
                      stroke="currentColor"
                      strokeWidth="1.5"
                      strokeLinejoin="round"
                    />
                    <path
                      d="M14 2v6h6"
                      stroke="currentColor"
                      strokeWidth="1.5"
                      strokeLinejoin="round"
                    />
                  </svg>
                )}
              </div>
              <div className="fm-item-info">
                <span className="fm-item-name" title={f.fileName}>
                  {f.fileName}
                </span>
                <span className="fm-item-meta">
                  {f.contentType} · {formatSize(f.size)}
                </span>
              </div>
              <div className="fm-item-actions">
                <a
                  href={f.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="fm-btn fm-btn--secondary"
                  title="Download / Preview"
                >
                  <svg
                    viewBox="0 0 20 20"
                    fill="currentColor"
                    aria-hidden="true"
                  >
                    <path d="M10 2a.75.75 0 0 1 .75.75v8.69l2.47-2.47a.75.75 0 1 1 1.06 1.06l-3.75 3.75a.75.75 0 0 1-1.06 0L5.72 10.03a.75.75 0 1 1 1.06-1.06L9.25 11.44V2.75A.75.75 0 0 1 10 2zM4.5 15.25a.75.75 0 0 0 0 1.5h11a.75.75 0 0 0 0-1.5h-11z" />
                  </svg>
                  Download
                </a>
                <button
                  className="fm-btn fm-btn--danger"
                  onClick={() => handleDelete(f.objectName)}
                  disabled={deletingKey === f.objectName}
                  title="Delete file"
                >
                  {deletingKey === f.objectName ? (
                    <span
                      className="fm-spinner fm-spinner--sm"
                      aria-label="Deleting…"
                    />
                  ) : (
                    <svg
                      viewBox="0 0 20 20"
                      fill="currentColor"
                      aria-hidden="true"
                    >
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
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
