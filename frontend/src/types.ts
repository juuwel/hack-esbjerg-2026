export interface ArchiveDocument {
    id: string;
    title?: string;
    content?: string;

    // Provenance
    sourceUrl?: string;
    sourcePlatform?: string;
    author?: string;
    archivedBy?: string;

    // Timestamps
    capturedAt: string;
    originalCreatedAt?: string;

    // Classification
    contentType?: string;
    format?: string;
    language?: string;
    tags: string[];

    // Geographic & community
    location?: string;
    community?: string;

    // Context
    historicalContext?: string;
    connectedIds: string[];

    // Integrity
    checksumSha256?: string;
    migrationHistory: string[];

    // Storage
    objectName?: string;
    metadata: Record<string, string>;
}

export interface UploadArchiveRequest {
    title?: string;
    sourceUrl?: string;
    sourcePlatform?: string;
    author?: string;
    archivedBy?: string;
    contentType?: string;
    language?: string;
    tags?: string;
    location?: string;
    community?: string;
    historicalContext?: string;
    originalCreatedAt?: string;
}

export interface SearchResult {
    total: number;
    hits: ArchiveDocument[];
}
