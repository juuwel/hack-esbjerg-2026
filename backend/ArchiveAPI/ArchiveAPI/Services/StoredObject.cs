namespace ArchiveAPI.Services;

public sealed record StoredObject(Stream Content, string ContentType, long? Size);

