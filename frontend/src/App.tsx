import { useState } from "react";
import "./App.css";
import ArchiveCapture from "./components/ArchiveCapture";
import SearchView from "./components/SearchView";
import type { ArchiveDocument } from "./types";

function App() {
  const [archived, setArchived] = useState<ArchiveDocument[]>([]);

  const handleArchived = (doc: ArchiveDocument) => {
    setArchived((prev) => [doc, ...prev]);
  };

  const handleDeleted = (id: string) => {
    setArchived((prev) => prev.filter((d) => d.id !== id));
  };

  return (
    <div className="app">
      <header className="app__hero">
        <div className="app__hero-inner">
          <div className="app__logo" aria-hidden="true">
            <svg viewBox="0 0 40 40" fill="none">
              <rect
                width="40"
                height="40"
                rx="10"
                fill="var(--accent)"
                opacity=".12"
              />
              <path
                d="M8 14a3 3 0 0 1 3-3h6l3 3h9a3 3 0 0 1 3 3v10a3 3 0 0 1-3 3H11a3 3 0 0 1-3-3V14z"
                stroke="var(--accent)"
                strokeWidth="1.8"
                strokeLinejoin="round"
              />
              <path
                d="M20 22v-6m0 0-2 2m2-2 2 2"
                stroke="var(--accent)"
                strokeWidth="1.5"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </div>
          <div>
            <h1 className="app__title">Tomorrow's Archive</h1>
            <p className="app__tagline">
              Preserving the present for people who aren't born yet.
            </p>
          </div>
        </div>
        <p className="app__sub">
          Half the links from 2015 are already dead. Every meme, every Discord
          thread, every local news story that nobody else saved — it deserves a
          home that outlasts the platform it came from.
        </p>
      </header>

      <main className="app__main">
        <div className="app__panel app__panel--capture">
          <h2 className="app__panel-title">
            <svg viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
              <path d="M9.25 13.25a.75.75 0 0 0 1.5 0V4.636l2.955 3.129a.75.75 0 0 0 1.09-1.03l-4.25-4.5a.75.75 0 0 0-1.09 0l-4.25 4.5a.75.75 0 1 0 1.09 1.03L9.25 4.636v8.614z" />
              <path d="M3.5 12.75a.75.75 0 0 0-1.5 0v2.5A2.75 2.75 0 0 0 4.75 18h10.5A2.75 2.75 0 0 0 18 15.25v-2.5a.75.75 0 0 0-1.5 0v2.5c0 .69-.56 1.25-1.25 1.25H4.75c-.69 0-1.25-.56-1.25-1.25v-2.5z" />
            </svg>
            Capture
          </h2>
          <ArchiveCapture onArchived={handleArchived} />
        </div>

        <div className="app__panel app__panel--search">
          <h2 className="app__panel-title">
            <svg viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
              <path
                fillRule="evenodd"
                d="M9 3.5a5.5 5.5 0 1 0 0 11 5.5 5.5 0 0 0 0-11zM2 9a7 7 0 1 1 12.452 4.391l3.328 3.329a.75.75 0 1 1-1.06 1.06l-3.329-3.328A7 7 0 0 1 2 9z"
                clipRule="evenodd"
              />
            </svg>
            The Archive
          </h2>
          <SearchView newItems={archived} onDeleted={handleDeleted} />
        </div>
      </main>

      <footer className="app__footer">
        <p>
          Built for <strong>WebLager</strong> · Hack Esbjerg 2026 · Every byte
          saved is a future unlocked.
        </p>
      </footer>
    </div>
  );
}

export default App;
