# AGENTS.md

## Scope
- This repo has no dedicated AI rule files yet; the only inherited guidance discovered was `README.md` and `frontend/README.md`.
- Treat this file as the root guide for both `frontend/` and `backend/` work.

## Architecture at a glance
- The app is a three-part archive pipeline: React/Vite UI in `frontend/`, ASP.NET Core API in `backend/ArchiveAPI/ArchiveAPI/`, and Docker-managed dependencies in `docker-compose.yml` (`opensearch`, `minio`, `tesseract`, plus the API and nginx-served frontend).
- Primary ingest flow: `frontend/src/components/ArchiveCapture.tsx` sends multipart `POST /api/file`; `Controllers/FileController.cs` computes SHA-256, stores bytes in MinIO, runs OCR for image MIME types through the `tesseract` HTTP client, enriches images with Gemini tags/description, then indexes an `ArchiveDocument` into OpenSearch.
- Retrieval/search flow: `frontend/src/components/SearchView.tsx` calls `/api/archive/search`; `Services/OpenSearchService.cs` performs boosted `multi_match` queries over `title`, `tags`, `aiTags`, `aiDescription`, `historicalContext`, `content`, `sourcePlatform`, `community`, and `location`.
- Binary files are intentionally proxied through the API (`/api/file` and `/api/archive/files`) instead of exposing MinIO directly; the frontend nginx config and Vite dev server both proxy `/api` requests.

## Files that explain the system fastest
- `docker-compose.yml`: real runtime topology, required env vars, container ports, and proxy assumptions.
- `backend/ArchiveAPI/ArchiveAPI/Program.cs`: dependency wiring, OpenAPI/Scalar dev-only setup, MinIO bucket bootstrap, and external service URLs.
- `backend/ArchiveAPI/ArchiveAPI/Controllers/FileController.cs`: the most important end-to-end workflow in the repo.
- `backend/ArchiveAPI/ArchiveAPI/Domain/Entities/ArchiveDocument.cs` and `frontend/src/types.ts`: keep these shapes aligned whenever fields change.
- `frontend/src/components/ArchiveCapture.tsx` and `frontend/src/components/SearchView.tsx`: frontend behavior is mostly concentrated here.

## Local workflows
- Full stack: from repo root, create `.env` with `OPENSEARCH_INITIAL_ADMIN_PASSWORD` and `GEMINI_API_KEY`, then run `docker compose up --build`.
- Backend local loop: from `backend/ArchiveAPI`, run `dotnet build ArchiveAPI.sln` and `dotnet run --project ArchiveAPI/ArchiveAPI.csproj`. `launchSettings.json` and `frontend/vite.config.ts` both assume local API HTTP on `http://localhost:5020`.
- Frontend local loop: from `frontend`, use `npm install`, `npm run dev`, `npm run build`, and `npm run lint`.
- Verified in this workspace: `dotnet build ArchiveAPI.sln` succeeds on .NET SDK `10.0.201`, but it currently emits many CS1591 XML-doc warnings plus a `Scalar.AspNetCore` resolution warning; these are pre-existing.

## Project-specific conventions
- Upload metadata is a flat multipart form matching `Shared/Requests/UploadArchiveRequest.cs`; `tags` travel as a comma-separated string and are split server-side by `ParseTags` in `FileController`.
- `ArchiveCapture.tsx` is intentionally optimistic: it adds a local document immediately after upload, then retries `GET /api/archive/documents/{id}` to hydrate OCR/AI-enriched fields. Preserve that pattern if you change upload responses.
- Search pagination is cursor-based, not page/offset based. `OpenSearchService.SearchAsync` encodes OpenSearch `search_after` sort values into an opaque base64 cursor; do not replace it with numeric pages without updating both API and UI.
- `ArchiveCard.tsx` deletes raw bytes and the search document separately (`DELETE /api/file?...` and `DELETE /api/archive/documents/{id}`); changing deletion semantics requires coordination across both endpoints.
- Image uploads get two layers of enrichment: frontend EXIF/IPTC extraction via `exifr` and backend OCR/Gemini enrichment. Manual edits in the capture form intentionally remove autofill badges.
- Storage naming is deliberate: MinIO bucket is always `archive` (`MinioService.BUCKET`), and object names are `{guid}/{originalFileName}`.
- Error payloads are not fully uniform yet: controllers often return `{ error: "..." }`, while unhandled exceptions go through `Presentation/GlobalExceptionHandler.cs` as `ProblemDetails` JSON.

## Integration notes
- OpenSearch is configured with default index `archive`; search result ordering is deterministic (`capturedAt` descending, `_id` ascending) so cursor pagination stays stable.
- Gemini is only used for image analysis in `Services/GeminiService.cs`; it expects `Gemini:ApiKey` / `GEMINI_API_KEY` and asks for strict JSON output.
- Tesseract OCR is an external HTTP service (`Tesseract__URL`, default `http://localhost:8884`) and is only attempted for image MIME types listed in `FileController.OcrContentTypes`.
- MinIO credentials default to `ROOT` / `PASSWORD` in Docker and `appsettings.json`; browser downloads should still go through the API proxy, not direct MinIO URLs.
