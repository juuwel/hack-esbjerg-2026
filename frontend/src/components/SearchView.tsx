import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ChangeEvent,
  type FormEvent,
} from "react";
import type { ArchiveDocument, SearchResult } from "../types";
import ArchiveCard from "./ArchiveCard";

const PAGE_SIZE = 20;

interface Props {
  // New items pushed in from the capture panel appear at the top
  newItems: ArchiveDocument[];
  onDeleted: (id: string) => void;
}

function mergeDocuments(primary: ArchiveDocument[], secondary: ArchiveDocument[]) {
  const merged: ArchiveDocument[] = [];
  const seen = new Set<string>();

  for (const doc of [...primary, ...secondary]) {
    if (seen.has(doc.id)) continue;
    seen.add(doc.id);
    merged.push(doc);
  }

  return merged;
}

export default function SearchView({ newItems, onDeleted }: Props) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<ArchiveDocument[]>([]);
  const [total, setTotal] = useState<number | null>(null);
  const [cursorStack, setCursorStack] = useState<(string | undefined)[]>([
    undefined,
  ]); // stack of cursors; index 0 = page 1
  const [pageIndex, setPageIndex] = useState(0);
  const [loading, setLoading] = useState(false);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const doSearch = useCallback(
    async (q: string, cursor?: string, newStack?: (string | undefined)[]) => {
      setLoading(true);
      try {
        const params = new URLSearchParams({ size: String(PAGE_SIZE) });
        if (q.trim()) params.set("q", q.trim());
        if (cursor) params.set("cursor", cursor);
        const res = await fetch(`/api/archive/search?${params}`);
        if (!res.ok) throw new Error("Search failed");
        const data: SearchResult = await res.json();
        setResults(data.hits ?? []);
        setTotal(data.total ?? 0);
        // Build the cursor stack for this query if provided, else keep current
        if (newStack !== undefined) {
          // Append next cursor at the end so we can navigate forward
          setCursorStack([...newStack, data.nextCursor]);
          setPageIndex(newStack.length - 1);
        }
      } catch {
        setResults([]);
        setTotal(null);
      } finally {
        setLoading(false);
      }
    },
    [],
  );

  const resetAndSearch = useCallback(
    (q: string) => {
      doSearch(q, undefined, [undefined]);
    },
    [doSearch],
  );

  // Load all documents on mount
  useEffect(() => {
    resetAndSearch("");
  }, [resetAndSearch]);

  // Debounced search as user types
  const handleChange = (e: ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value;
    setQuery(val);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => resetAndSearch(val), 400);
  };

  const handleSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (debounceRef.current) clearTimeout(debounceRef.current);
    resetAndSearch(query);
  };

  const handleNext = () => {
    const nextCursor = cursorStack[pageIndex + 1];
    if (!nextCursor) return;
    const newStack = cursorStack.slice(0, pageIndex + 2);
    setPageIndex(pageIndex + 1);
    doSearch(query, nextCursor, newStack);
  };

  const handlePrev = () => {
    if (pageIndex === 0) return;
    const prevCursor = cursorStack[pageIndex - 1];
    const newStack = cursorStack.slice(0, pageIndex);
    setPageIndex(pageIndex - 1);
    doSearch(query, prevCursor, newStack);
  };

  const hasNext = !!cursorStack[pageIndex + 1];
  const hasPrev = pageIndex > 0;
  const showingLiveUploads = !query.trim() && pageIndex === 0;

  const displayedResults = useMemo(() => {
    if (!showingLiveUploads) {
      return results;
    }

    return mergeDocuments(newItems, results).slice(0, PAGE_SIZE);
  }, [newItems, results, showingLiveUploads]);

  const displayedTotal = useMemo(() => {
    if (total === null) {
      return null;
    }

    if (!showingLiveUploads) {
      return total;
    }

    const fetchedIds = new Set(results.map((doc) => doc.id));
    const localOnlyCount = newItems.reduce(
      (count, doc) => count + (fetchedIds.has(doc.id) ? 0 : 1),
      0,
    );

    return total + localOnlyCount;
  }, [newItems, results, showingLiveUploads, total]);

  const totalPages =
    displayedTotal !== null ? Math.ceil(displayedTotal / PAGE_SIZE) : null;

  const handleDelete = useCallback(
    (id: string) => {
      setResults((prev: ArchiveDocument[]) =>
        prev.filter((doc: ArchiveDocument) => doc.id !== id),
      );
      setTotal((prev: number | null) =>
        prev === null ? null : Math.max(prev - 1, 0),
      );
      onDeleted(id);
    },
    [onDeleted],
  );

  return (
    <section className="search">
      <form className="search__bar" onSubmit={handleSubmit} role="search">
        <label htmlFor="search-input" className="sr-only">
          Search the archive
        </label>
        <div className="search__input-wrap">
          <svg
            className="search__icon"
            viewBox="0 0 20 20"
            fill="currentColor"
            aria-hidden="true"
          >
            <path
              fillRule="evenodd"
              d="M9 3.5a5.5 5.5 0 1 0 0 11 5.5 5.5 0 0 0 0-11zM2 9a7 7 0 1 1 12.452 4.391l3.328 3.329a.75.75 0 1 1-1.06 1.06l-3.329-3.328A7 7 0 0 1 2 9z"
              clipRule="evenodd"
            />
          </svg>
          <input
            id="search-input"
            className="search__input"
            type="search"
            placeholder="Search titles, tags, context, platform…"
            value={query}
            onChange={handleChange}
            autoComplete="off"
          />
          {loading && (
            <span className="spinner search__spinner" aria-label="Searching…" />
          )}
        </div>
        <button type="submit" className="btn btn--primary">
          Search
        </button>
      </form>

      {displayedTotal !== null && (
        <p className="search__meta">
          {displayedTotal === 0 ? (
            "No results"
          ) : query.trim() ? (
            <>
              {displayedTotal} result{displayedTotal === 1 ? "" : "s"} for{" "}
              <strong>"{query}"</strong>
            </>
          ) : (
            `${displayedTotal} document${displayedTotal === 1 ? "" : "s"} in the archive`
          )}
        </p>
      )}

      {displayedTotal === 0 && !loading && (
        <div className="search__empty">
          <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <path
              d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7z"
              stroke="currentColor"
              strokeWidth="1.5"
              strokeLinejoin="round"
            />
          </svg>
          <p>
            {query.trim()
              ? "No documents match your search."
              : "Nothing here yet. Archive something to get started."}
          </p>
        </div>
      )}

      {displayedResults.length > 0 && (
        <ul className="search__results">
          {displayedResults.map((doc) => (
            <li key={doc.id}>
              <ArchiveCard doc={doc} onDeleted={handleDelete} />
            </li>
          ))}
        </ul>
      )}

      {(hasPrev || hasNext) && (
        <div className="search__pagination">
          <button
            className="btn btn--secondary"
            disabled={!hasPrev || loading}
            onClick={handlePrev}
          >
            ← Previous
          </button>
          <span>
            Page {pageIndex + 1}
            {totalPages !== null ? ` of ${totalPages}` : ""}
          </span>
          <button
            className="btn btn--secondary"
            disabled={!hasNext || loading}
            onClick={handleNext}
          >
            Next →
          </button>
        </div>
      )}
    </section>
  );
}
