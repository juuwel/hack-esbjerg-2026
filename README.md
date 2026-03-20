# Tomorrow's Archive

> *Preserving the present for people who aren't born yet.*

**Tomorrow's Archive** is a full-stack digital archiving platform built for [Hack Esbjerg 2026](https://hackesbjerg.dk) as part of the WebLager challenge. It lets users capture, enrich, and search files and documents before they disappear from the open web — social posts, local news stories, community threads, images, and anything else worth keeping.

---

## Features

- **Upload anything** — drag-and-drop files of any type with rich provenance metadata (source URL, platform, author, location, historical context, tags)
- **Automatic OCR** — text is extracted from images (PNG, JPEG, TIFF, WEBP, etc.) via Tesseract
- **AI enrichment** — images are analysed by Google Gemini 2.5 Flash, producing a short description and search tags automatically
- **EXIF / IPTC metadata extraction** — image files are pre-filled with embedded camera/author/location data client-side before upload
- **Full-text search** — boosted multi-field search across titles, tags, AI tags, OCR content, platform, community, location, and historical context
- **Cursor-based pagination** — stable, deterministic page navigation backed by OpenSearch `search_after`
- **SHA-256 integrity** — every stored file receives a checksum for tamper detection and longevity auditing
- **Optimistic UI** — uploads appear immediately in the archive while AI/OCR enrichment continues in the background

---

## Technology stack

### Frontend
| Technology | Role |
|---|---|
| [React 19](https://react.dev) | UI framework |
| [TypeScript 5.9](https://www.typescriptlang.org) | Type-safe component code |
| [Vite 8](https://vite.dev) | Dev server, bundler, and HMR |
| [exifr](https://github.com/MikeKovarik/exifr) | Client-side EXIF/IPTC/XMP extraction from image files |
| ESLint + typescript-eslint | Linting |

### Backend
| Technology | Role |
|---|---|
| [ASP.NET Core](https://dotnet.microsoft.com/en-us/apps/aspnet) (.NET 10) | REST API |
| [OpenSearch.Client 1.5](https://github.com/opensearch-project/opensearch-net) | Indexing and full-text search |
| [Minio SDK 7](https://github.com/minio/minio-dotnet) | Object storage client |
| [Scalar.AspNetCore](https://scalar.com) | Interactive OpenAPI docs (dev only, at `/scalar`) |

### Infrastructure & external services
| Technology | Role |
|---|---|
| [OpenSearch](https://opensearch.org) | Search and document index (default index: `archive`) |
| [MinIO](https://min.io) | S3-compatible binary object storage (bucket: `archive`) |
| [Tesseract Server](https://github.com/hertzg/tesseract-server) | External OCR HTTP service for image-to-text |
| [Google Gemini 2.5 Flash](https://ai.google.dev) | Image description and tag generation |
| [Docker Compose](https://docs.docker.com/compose/) | Container orchestration for the full stack |
| [nginx](https://nginx.org) | Static file serving and `/api` reverse proxy for the frontend container |

---

## Architecture

```
Browser
  │
  ├── /api/* ──────────────────────────────► ASP.NET Core API (:8080)
  │    (proxied by Vite in dev,              │
  │     proxied by nginx in Docker)          ├── MinIO       (object storage)
  │                                          ├── OpenSearch  (search index)
  └── static assets ◄── nginx / Vite        ├── Tesseract   (OCR)
                                             └── Gemini API  (AI enrichment)
```

**Ingest path:** `ArchiveCapture.tsx` → `POST /api/file` → SHA-256 checksum + MinIO upload → OCR (images only) → Gemini analysis (images only) → OpenSearch index → optimistic UI hydration via `GET /api/archive/documents/{id}` polling.

**Search path:** `SearchView.tsx` → `GET /api/archive/search?q=&size=&cursor=` → `OpenSearchService.SearchAsync` → boosted `multi_match` → cursor-paginated results.

---

## Getting started

### Prerequisites
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (recommended for the full stack)
- A [Google Gemini API key](https://aistudio.google.com/app/apikey) (free tier works)

### Full stack (Docker)

1. Create a `.env` file in the repository root:
   ```env
   OPENSEARCH_INITIAL_ADMIN_PASSWORD=YourStr0ngPassword!
   GEMINI_API_KEY=your-gemini-api-key
   ```
2. Start everything:
   ```bash
   docker compose up --build
   ```
3. Open the apps:
   | Service | URL |
   |---|---|
   | Frontend | http://localhost:3000 |
   | API | http://localhost:8080 |
   | OpenAPI docs (Scalar) | http://localhost:8080/scalar |
   | MinIO Console | http://localhost:9001 |

   MinIO login: `ROOT` / `PASSWORD`

---

## Local development

### Backend
Requires .NET SDK 10.

```powershell
cd backend/ArchiveAPI
dotnet build ArchiveAPI.sln
dotnet run --project ArchiveAPI/ArchiveAPI.csproj
# API listens on http://localhost:5020
```

> **Note:** The build succeeds but emits pre-existing CS1591 XML-doc warnings and a `Scalar.AspNetCore` version resolution warning — both are harmless and can be ignored.

### Frontend
Requires Node.js 20+.

```powershell
cd frontend
npm install
npm run dev       # dev server with HMR on http://localhost:5173
npm run build     # type-check + production bundle
npm run lint      # ESLint
```

The Vite dev server proxies all `/api` requests to `http://localhost:5020` by default (override with the `VITE_API_PROXY_TARGET` env var).

---

## Project structure

```
hack-esbjerg-2026/
├── docker-compose.yml
├── .env                          # required – not committed
├── frontend/
│   ├── src/
│   │   ├── components/
│   │   │   ├── ArchiveCapture.tsx   # upload form + optimistic UI
│   │   │   ├── SearchView.tsx       # search bar + cursor pagination
│   │   │   └── ArchiveCard.tsx      # result card + delete
│   │   ├── types.ts                 # ArchiveDocument, SearchResult (keep in sync with backend)
│   │   └── App.tsx
│   ├── nginx.conf                   # production proxy config
│   └── vite.config.ts
└── backend/ArchiveAPI/ArchiveAPI/
    ├── Controllers/
    │   ├── FileController.cs        # POST/GET/DELETE /api/file
    │   └── ArchiveController.cs     # search, documents, health
    ├── Services/
    │   ├── OpenSearchService.cs
    │   ├── MinioService.cs
    │   └── GeminiService.cs
    ├── Domain/Entities/
    │   └── ArchiveDocument.cs       # canonical document model
    ├── Shared/Requests/
    │   └── UploadArchiveRequest.cs  # multipart form metadata shape
    └── Program.cs                   # DI wiring + startup
```

---

## API overview

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/file` | Upload a file with metadata; triggers OCR + AI enrichment for images |
| `GET` | `/api/file?objectName=` | Stream a stored file through the API |
| `DELETE` | `/api/file?objectName=` | Remove a stored binary from MinIO |
| `GET` | `/api/archive/search?q=&size=&cursor=` | Full-text search with cursor pagination |
| `GET` | `/api/archive/documents/{id}` | Fetch a single document by ID |
| `POST` | `/api/archive/documents` | Index a document directly (no file) |
| `DELETE` | `/api/archive/documents/{id}` | Remove a document from the search index |
| `GET` | `/api/archive/health` | OpenSearch connectivity check |

---

*Built for **WebLager** · Hack Esbjerg 2026*
