# Distributed File Resilience (Comprehensive, Secure, Specification)

## Architecture Overview

- Keep `ModLinkFilenames` EXACTLY as-is (authoritative for which files to download)
- Add `ResourceRegistry` mapping ContentKey → ResourceMetadata (augments, not replaces)
- ContentKey types:
  - **ContentId** (preferred): SHA-256 hash of file bytes OR content-structure hash from canonical metadata
  - **MetadataHash** (fallback): SHA-256 hash of canonical provider metadata before file download
- Deterministic content identification ensures same file → same ContentKey across all machines

## Phase 0: Stealth and Inconspicuousness

### 0.1 Terminology

- NEVER surface recognizable file-sharing jargon in UI, logs, or code comments.
- USE NEUTRAL TERMS: "distributed cache", "network cache", "content identifier", "optimized download", "shared resource"
- All log messages use `[Cache]` prefix
- All file/folder names use generic terms: "Cache/Network", "resource-index.json", ".dat" extensions

### 0.2 Code Structure

- Keep all network protocol implementation in `DownloadCacheOptimizer.cs` with base64-obfuscated type names (already done)
- No comments explaining the underlying mechanism in detail
- Public APIs accept/return generic terms only (contentKey, metadata, optimizedDownload)
- ResourceMetadata class uses `ContentId` not revealing terminology

### 0.3 Serialization Visibility

- `ResourceRegistry` serialized to TOML/JSON/YAML/XML as normal dictionary
- Fields: `ContentKey`, `ContentId`, `PrimaryUrl`, `HandlerMetadata`, `Files`
- No special annotations or comments in serialized output
- Appears as generic file tracking metadata

### 0.4 Copyright & Takedown

**Add to DownloadCacheOptimizer**:

```csharp
private static HashSet<string> BlockedContentIds = new HashSet<string>();
public static void BlockContentId(string contentId) { BlockedContentIds.Add(contentId); }
```

- Honor DMCA takedown requests by blocking specific ContentIds
- Log all blocked attempts for compliance records

## Phase 1: Canonical Specifications

### 1.1 CanonicalJson - Exact Algorithm

**File**: `KOTORModSync.Core/Utility/CanonicalJson.cs`
csharp public static class CanonicalJson { public static string Serialize(Dictionary<string, object> obj) { // 1. Normalize all strings to Unicode NFC // 2. Sort object keys lexicographically by UTF-8 codepoint // 3. Numbers: minimal decimal notation, no exponent, dot separator, strip trailing zeros // 4. Booleans: lowercase "true"/"false" // 5. Arrays: PRESERVE ORDER (semantically significant) // 6. Null: literal "null" // 7. Output: UTF-8, no whitespace, no comments } public static string ComputeHash(Dictionary<string, object> obj) { byte[] canonical = Encoding.UTF8.GetBytes(Serialize(obj)); byte[] hash = SHA256.HashData(canonical); return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant(); } }
**Field-Specific Rules**:

- Filenames: normalize path separators (`/`), Unicode NFC, NO trimming
- Provider metadata: preserve exactly as received, then normalize
- URLs: **Comprehensive normalization**:
  - Scheme/host: lowercase
  - Remove default ports (`:80` for http, `:443` for https)
  - Path: percent-decode unreserved characters (A-Z, a-z, 0-9, `-`, `.`, `_`, `~`)
  - Query parameters: **provider-specific** (some preserve order, some sort alphabetically)
  - Remove trailing slash unless path is `/` only
  - Collapse `/../` and `/./` sequences
- Numbers: Use `InvariantCulture`, format as minimal decimal (no `+`, no exponent, strip trailing zeros after decimal)
- Integers: no leading zeros except literal `0`
- Floats: use `G` format with sufficient precision, dot separator
- Escape control characters: `\n`, `\r`, `\t`, `\"`, `\\`

**Per-Provider Array/Field Rules**:

- Document which provider fields are included in metadataHash (whitelist)
- Document which arrays are order-sensitive vs. unordered (sort unordered)

### 1.2 Deterministic Piece Size

**File**: `DownloadCacheOptimizer.cs` - add method
csharp private static int DeterminePieceSize(long fileSize) { // Ensure pieces <= 2^20 (1,048,576 pieces max) int[] candidates = { 65536, 131072, 262144, 524288, 1048576, 2097152, 4194304 }; // 64KB-4MB foreach (int size in candidates) { if ((fileSize + size - 1) / size <= 1048576) return size; } return 4194304; // Max 4MB pieces }

### 1.3 Canonical Content Metadata Specification

**Deterministic parameters for identical ContentId across clients**:
csharp // Bundle metadata structure (bencoded) { "info": { "name": SanitizeFilename(originalName), // UTF-8, NFC, forward slashes "piece length": DeterminePieceSize(fileSize), // From 1.2 "pieces": ComputePieceHashes(fileBytes), // SHA-1, 20 bytes per piece "length": fileSize, "private": 0 // Explicitly set (not omitted) }, // OMIT: "announce", "announce-list", "creation date", "created by", "comment" } // ContentId = SHA-1(bencode(info_dict)) per canonical spec // ALSO compute ContentHashSHA256 = SHA-256(fileBytes) for integrity
**Bencoding rules** (canonical):

- Dict keys: lexicographic byte order
- Integers: minimal representation (no leading zeros except "0")
- Strings: length-prefixed, UTF-8 bytes

### 1.4 ResourceRegistry in ModComponent.cs

```csharp
[NotNull]
public Dictionary<string, ResourceMetadata> ResourceRegistry { get; set; } = new(StringComparer.Ordinal);

public class ResourceMetadata
{
    public string ContentKey { get; set; }        // Current lookup key (MetadataHash initially, ContentId after download)
    public string ContentId { get; set; }         // SHA-1 of bencoded info dict (null pre-download)
    public string ContentHashSHA256 { get; set; } // SHA-256 of file bytes - CANONICAL for integrity (null pre-download)
    public string MetadataHash { get; set; }      // SHA-256 of canonical provider metadata
    public string PrimaryUrl { get; set; }
    public Dictionary<string, object> HandlerMetadata { get; set; } = new();
    public Dictionary<string, bool?> Files { get; set; } = new(StringComparer.Ordinal);
    public long FileSize { get; set; }
    public int PieceLength { get; set; }          // Bytes per piece (from DeterminePieceSize)
    public string PieceHashes { get; set; }       // Hex-encoded concatenated SHA-1 hashes (20 bytes per piece)
    public DateTime? FirstSeen { get; set; }
    public DateTime? LastVerified { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public MappingTrustLevel TrustLevel { get; set; } = MappingTrustLevel.Unverified;
}

public enum MappingTrustLevel
{
    Unverified = 0,      // Initial state
    ObservedOnce = 1,    // Seen from one source
    Verified = 2         // Verified from 2+ independent sources
}
```

**Hash Precedence (CRITICAL)**:

- **ContentId**: SHA-1 hash of bencoded `info` dict (legacy compatibility identifier) - used for network cache lookups
- **ContentHashSHA256**: SHA-256 of raw file bytes - CANONICAL for all integrity/trust decisions
- **MetadataHash**: SHA-256 of canonical provider metadata - used before file download
- Trust decisions: ALWAYS verify ContentHashSHA256 match + piece verification (never trust ContentId alone)

### 1.5 ResourceIndex Persistence (Cross-Platform, Multi-Process Safe)

**File**: `KOTORModSync.Core/Services/DownloadCacheService.cs`

**Dual Index Structure**:

```csharp
// Never rename keys - use dual maps to avoid race conditions
private static readonly Dictionary<string, ResourceMetadata> s_resourceByMetadataHash = new(StringComparer.Ordinal);
private static readonly Dictionary<string, string> s_metadataHashToContentId = new(StringComparer.Ordinal);
private static readonly Dictionary<string, ResourceMetadata> s_resourceByContentId = new(StringComparer.Ordinal);
```

**Storage Structure**:

```json
{
  "schemaVersion": 1,
  "entries": {
    "metadataHash_abc123": { ResourceMetadata },
    "contentId_def456": { ResourceMetadata }
  },
  "mappings": {
    "metadataHash_abc123": "contentId_def456"
  }
}
```

**Cross-Platform Paths**:

```csharp
private static string GetResourceIndexPath()
{
    string cacheDir = Path.GetDirectoryName(GetCacheFilePath());
    return Path.Combine(cacheDir, "resource-index.json");
}

private static string GetResourceIndexLockPath()
{
    return GetResourceIndexPath() + ".lock";
}
```

**Atomic, Multi-Process Safe Write**:

```csharp
private static void SaveResourceIndexAtomic(...)
{
    string path = GetResourceIndexPath();
    string temp = path + ".tmp";
    string backup = path + ".bak";

    // Cross-process file lock
    using (var fileLock = new FileStream(GetResourceIndexLockPath(), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
    {
        fileLock.Lock(0, 0); // Full file lock

        try
        {
            var indexData = new {
                schemaVersion = 1,
                entries = s_resourceByMetadataHash.Concat(s_resourceByContentId).ToDictionary(k => k.Key, v => v.Value),
                mappings = s_metadataHashToContentId
            };

            string json = JsonConvert.SerializeObject(indexData, Formatting.Indented);
            File.WriteAllText(temp, json);

            // Platform-specific atomic replace
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                File.Replace(temp, path, backup);
                if (File.Exists(backup)) File.Delete(backup);
            }
            else
            {
                // POSIX: File.Move with overwrite, then fsync directory
                if (File.Exists(path)) File.Move(path, backup);
                File.Move(temp, path);
                // TODO: Add directory fsync via P/Invoke if critical
                if (File.Exists(backup)) File.Delete(backup);
            }
        }
        finally
        {
            fileLock.Unlock(0, 0);
        }
    }
}
```

**Locking Strategy**:

- **s_resourceIndexLock** (object): In-process synchronization for dictionary operations
- **File lock** (cross-process): FileStream with FileShare.None for index read/write
- **Per-ContentKey locks**: ConcurrentDictionary<string, SemaphoreSlim> for fine-grained download coordination

### 1.6 ModComponentSerializationService Integration

**Add methods to serialize/deserialize ResourceRegistry**:

```csharp
// In SerializeComponentToDictionary
if (component.ResourceRegistry.Count > 0)
    componentDict["ResourceRegistry"] = SerializeResourceRegistry(component.ResourceRegistry);

private static Dictionary<string, object> SerializeResourceRegistry(Dictionary<string, ResourceMetadata> registry)
{
    var result = new Dictionary<string, object>(StringComparer.Ordinal);
    foreach (var kvp in registry)
    {
        var metaDict = new Dictionary<string, object>
        {
            ["ContentKey"] = kvp.Value.ContentKey,
            ["MetadataHash"] = kvp.Value.MetadataHash,
            ["PrimaryUrl"] = kvp.Value.PrimaryUrl,
            ["HandlerMetadata"] = kvp.Value.HandlerMetadata,
            ["Files"] = kvp.Value.Files,
            ["FileSize"] = kvp.Value.FileSize,
            ["SchemaVersion"] = kvp.Value.SchemaVersion,
            ["TrustLevel"] = kvp.Value.TrustLevel.ToString()
        };

        // Only serialize post-download fields if present
        if (kvp.Value.ContentId != null) metaDict["ContentId"] = kvp.Value.ContentId;
        if (kvp.Value.ContentHashSHA256 != null) metaDict["ContentHashSHA256"] = kvp.Value.ContentHashSHA256;
        if (kvp.Value.PieceLength > 0) metaDict["PieceLength"] = kvp.Value.PieceLength;
        if (kvp.Value.PieceHashes != null) metaDict["PieceHashes"] = kvp.Value.PieceHashes;
        if (kvp.Value.FirstSeen.HasValue) metaDict["FirstSeen"] = kvp.Value.FirstSeen.Value.ToString("O");
        if (kvp.Value.LastVerified.HasValue) metaDict["LastVerified"] = kvp.Value.LastVerified.Value.ToString("O");

        result[kvp.Key] = metaDict;
    }
    return result;
}

// Add corresponding DeserializeResourceRegistry in DeserializeComponent
private static Dictionary<string, ResourceMetadata> DeserializeResourceRegistry(IDictionary<string, object> componentDict)
{
    var result = new Dictionary<string, ResourceMetadata>(StringComparer.Ordinal);

    if (!componentDict.TryGetValue("ResourceRegistry", out object registryObj) || !(registryObj is IDictionary<string, object> registryDict))
        return result;

    foreach (var kvp in registryDict)
    {
        if (!(kvp.Value is IDictionary<string, object> metaDict)) continue;

        var meta = new ResourceMetadata
        {
            ContentKey = GetValueOrDefault<string>(metaDict, "ContentKey"),
            ContentId = GetValueOrDefault<string>(metaDict, "ContentId"),
            ContentHashSHA256 = GetValueOrDefault<string>(metaDict, "ContentHashSHA256"),
            MetadataHash = GetValueOrDefault<string>(metaDict, "MetadataHash"),
            PrimaryUrl = GetValueOrDefault<string>(metaDict, "PrimaryUrl"),
            HandlerMetadata = GetValueOrDefault<Dictionary<string, object>>(metaDict, "HandlerMetadata") ?? new(),
            Files = GetValueOrDefault<Dictionary<string, bool?>>(metaDict, "Files") ?? new(StringComparer.Ordinal),
            FileSize = GetValueOrDefault<long>(metaDict, "FileSize"),
            PieceLength = GetValueOrDefault<int>(metaDict, "PieceLength"),
            PieceHashes = GetValueOrDefault<string>(metaDict, "PieceHashes"),
            SchemaVersion = GetValueOrDefault<int>(metaDict, "SchemaVersion", 1)
        };

        if (Enum.TryParse<MappingTrustLevel>(GetValueOrDefault<string>(metaDict, "TrustLevel"), out var trustLevel))
            meta.TrustLevel = trustLevel;

        if (DateTime.TryParse(GetValueOrDefault<string>(metaDict, "FirstSeen"), out var firstSeen))
            meta.FirstSeen = firstSeen;
        if (DateTime.TryParse(GetValueOrDefault<string>(metaDict, "LastVerified"), out var lastVerified))
            meta.LastVerified = lastVerified;

        result[kvp.Key] = meta;
    }

    return result;
}
```

**Case-insensitive keys**: Normalize all dictionary keys to lowercase on deserialization across all formats (TOML/JSON/YAML/XML).

## Phase 2: Handler Metadata Interface

### 2.1 IDownloadHandler Extension

```csharp
Task<Dictionary<string, object>> GetFileMetadataAsync(string url, CancellationToken ct = default);
string GetProviderKey(); // "deadlystream", "mega", "nexus", "direct"
```

**CRITICAL**: Each handler MUST normalize metadata types BEFORE returning:

- Sizes: return as `long` (not string)
- Timestamps: return as Unix epoch `long` OR ISO 8601 string (pick one per provider, be consistent)
- URLs: apply canonical URL normalization (see 1.1)
- File IDs: return as `string` (preserve original format)

### 2.2 Provider Metadata Schemas & Field Whitelists

**Field Whitelist Purpose**: Only whitelisted fields are included in metadataHash computation. This prevents hash instability from added/removed provider fields.

**DeadlyStream**:

```json
{
  "provider": "deadlystream",
  "filePageId": "1313",
  "changelogId": "0",
  "fileId": "71163",
  "version": "1.0.5",
  "updated": "2024-06-13",
  "size": 1101824
}
```

- **Whitelist for metadataHash**: `["provider", "filePageId", "changelogId", "fileId", "version", "updated", "size"]`
- **Types**: `filePageId` (string), `changelogId` (string), `fileId` (string), `version` (string), `updated` (string ISO date), `size` (long)
- **Normalization**: `updated` → "YYYY-MM-DD" format only (strip time)

**MEGA**:

```json
{
  "provider": "mega",
  "nodeId": "abc123",
  "hash": "merkle_tree_hash_base64",
  "size": 1101824,
  "mtime": 1686672000,
  "name": "file.zip"
}
```

- **Whitelist**: `["provider", "nodeId", "hash", "size", "mtime", "name"]`
- **Types**: `nodeId` (string), `hash` (string base64), `size` (long), `mtime` (long Unix epoch), `name` (string)

**Nexus**:

```json
{
  "provider": "nexus",
  "fileId": "12345",
  "fileName": "mod.zip",
  "size": 1101824,
  "uploadedTimestamp": 1686672000,
  "md5Hash": "abc123..."
}
```

- **Whitelist**: `["provider", "fileId", "fileName", "size", "uploadedTimestamp", "md5Hash"]`
- **Types**: `fileId` (string), `fileName` (string), `size` (long), `uploadedTimestamp` (long Unix), `md5Hash` (string lowercase hex)

**Direct**:

```json
{
  "provider": "direct",
  "url": "normalized_url",
  "contentLength": 1101824,
  "lastModified": "Wed, 13 Jun 2024 12:00:00 GMT",
  "etag": "abc123",
  "fileName": "file.zip"
}
```

- **Whitelist**: `["provider", "url", "contentLength", "lastModified", "etag", "fileName"]`
- **Types**: `url` (string canonical), `contentLength` (long), `lastModified` (string HTTP date), `etag` (string), `fileName` (string)
- **Special**: If ETag unavailable, omit from metadata (don't include null/empty)

### 2.3 Handler Metadata Normalization Helper

**Add to each handler's GetFileMetadataAsync**:

```csharp
private Dictionary<string, object> NormalizeMetadata(Dictionary<string, object> raw)
{
    var normalized = new Dictionary<string, object>();
    string[] whitelist = GetMetadataFieldWhitelist();

    foreach (var field in whitelist)
    {
        if (!raw.ContainsKey(field)) continue;

        // Type-specific normalization
        object value = raw[field];
        if (field.EndsWith("size") || field.EndsWith("Length") || field == "mtime")
        {
            normalized[field] = Convert.ToInt64(value); // Ensure long
        }
        else if (field == "url")
        {
            normalized[field] = CanonicalizeUrl(value.ToString());
        }
        else
        {
            normalized[field] = value;
        }
    }

    return normalized;
}
```

## Phase 3: Security & Integrity

### 3.1 Integrity Verification (CANONICAL)

**After ANY cache download, verify integrity BEFORE trusting**:

```csharp
private static async Task<bool> VerifyContentIntegrity(string filePath, ResourceMetadata meta)
{
    // 1. CANONICAL CHECK: SHA-256 of file bytes
    byte[] sha256Hash = await ComputeSHA256Async(filePath);
    string computedSHA256 = BitConverter.ToString(sha256Hash).Replace("-", "").ToLowerInvariant();

    if (meta.ContentHashSHA256 != null && computedSHA256 != meta.ContentHashSHA256)
    {
        await Logger.LogErrorAsync($"[Cache] INTEGRITY FAILURE: SHA-256 mismatch");
        await Logger.LogErrorAsync($"  Expected: {meta.ContentHashSHA256}");
        await Logger.LogErrorAsync($"  Computed: {computedSHA256}");
        return false;
    }

    // 2. Piece-level verification (if piece data available)
    if (meta.PieceHashes != null && meta.PieceLength > 0)
    {
        bool piecesValid = await VerifyPieceHashesFromStored(filePath, meta.PieceLength, meta.PieceHashes);
        if (!piecesValid)
        {
            await Logger.LogErrorAsync($"[Cache] INTEGRITY FAILURE: Piece hash mismatch");
            return false;
        }
    }

    // 3. Verify file size matches
    var fileInfo = new FileInfo(filePath);
    if (meta.FileSize > 0 && fileInfo.Length != meta.FileSize)
    {
        await Logger.LogErrorAsync($"[Cache] INTEGRITY FAILURE: File size mismatch");
        await Logger.LogErrorAsync($"  Expected: {meta.FileSize} bytes");
        await Logger.LogErrorAsync($"  Actual: {fileInfo.Length} bytes");
        return false;
    }

    return true;
}

private static async Task<bool> VerifyPieceHashesFromStored(string filePath, int pieceLength, string pieceHashesHex)
{
    // Parse stored piece hashes (hex-encoded concatenated SHA-1 hashes, 40 hex chars per piece)
    var expectedHashes = new List<byte[]>();
    for (int i = 0; i < pieceHashesHex.Length; i += 40)
    {
        if (i + 40 > pieceHashesHex.Length) break;
        string hexPiece = pieceHashesHex.Substring(i, 40);
        expectedHashes.Add(Convert.FromHexString(hexPiece)); // .NET 5+, or manual parse
    }

    // Compute actual piece hashes from file
    using (var fs = File.OpenRead(filePath))
    {
        byte[] buffer = new byte[pieceLength];
        int pieceIndex = 0;

        while (true)
        {
            int bytesRead = await fs.ReadAsync(buffer, 0, pieceLength);
            if (bytesRead == 0) break;

            byte[] pieceData = bytesRead == pieceLength ? buffer : buffer.Take(bytesRead).ToArray();
            byte[] computedHash = SHA1.HashData(pieceData);

            if (pieceIndex >= expectedHashes.Count || !computedHash.SequenceEqual(expectedHashes[pieceIndex]))
            {
                await Logger.LogErrorAsync($"[Cache] Piece {pieceIndex} hash mismatch");
                return false;
            }

            pieceIndex++;
        }

        if (pieceIndex != expectedHashes.Count)
        {
            await Logger.LogErrorAsync($"[Cache] Piece count mismatch: expected {expectedHashes.Count}, got {pieceIndex}");
            return false;
        }
    }

    return true;
}
```

### 3.2 Poisoning Protection & Trust Elevation

**Independent Verification Definition**:

- **Source 1**: Successful URL download with matching metadata
- **Source 2**: Network cache download from different endpoint/session
- **Source 3**: Another user's verified mapping (requires signed index, optional)

**Trust Elevation Flow**:

```csharp
private static async Task<bool> UpdateMappingWithVerification(string metadataHash, string contentId, ResourceMetadata meta)
{
    lock (s_resourceIndexLock)
    {
        // Check existing mapping
        if (s_metadataHashToContentId.TryGetValue(metadataHash, out string existingContentId))
        {
            if (existingContentId == contentId)
            {
                // Same mapping, elevate trust
                if (meta.TrustLevel == MappingTrustLevel.Unverified)
                    meta.TrustLevel = MappingTrustLevel.ObservedOnce;
                else if (meta.TrustLevel == MappingTrustLevel.ObservedOnce)
                    meta.TrustLevel = MappingTrustLevel.Verified;

                return true;
            }
            else
            {
                // CONFLICT: Different ContentId for same MetadataHash
                await Logger.LogWarningAsync($"[Cache] Mapping conflict detected:");
                await Logger.LogWarningAsync($"  MetadataHash: {metadataHash.Substring(0,16)}...");
                await Logger.LogWarningAsync($"  Existing ContentId: {existingContentId.Substring(0,16)}...");
                await Logger.LogWarningAsync($"  New ContentId: {contentId.Substring(0,16)}...");

                // Keep existing if Verified, otherwise require manual resolution
                if (meta.TrustLevel == MappingTrustLevel.Verified)
                {
                    await Logger.LogWarningAsync($"  Keeping existing (Verified)");
                    return false;
                }
            }
        }

        // New mapping or replacing unverified
        s_metadataHashToContentId[metadataHash] = contentId;
        s_resourceByContentId[contentId] = meta;
        meta.TrustLevel = MappingTrustLevel.ObservedOnce;

        return true;
    }
}
```

**Age & Expiry**:

- Remove ResourceIndex entries where LastVerified > 90 days AND no local file exists
- Downgrade trust level if not re-verified in 30 days

### 3.3 Blocked Content & Takedown

**Persist blocklist** to `blocked-content.json`:

```csharp
private static HashSet<string> s_blockedContentIds = new HashSet<string>();

public static void BlockContentId(string contentId, string reason = null)
{
    s_blockedContentIds.Add(contentId);

    // Log with audit trail
    string auditLog = Path.Combine(Path.GetDirectoryName(GetResourceIndexPath()), "block-audit.log");
    File.AppendAllText(auditLog, $"{DateTime.UtcNow:O}|BLOCK|{contentId}|{reason ?? "manual"}\n");

    // Persist blocklist
    SaveBlocklist();
}

// Check before any download
if (s_blockedContentIds.Contains(contentId))
{
    await Logger.LogWarningAsync($"[Cache] ContentId blocked per policy: {contentId.Substring(0,8)}...");
    return null; // Fall back to URL download only
}
```

## Phase 4: Content Identification & Download Integration

**CRITICAL**: ContentId MUST be computed from metadata BEFORE download to enable distributed lookup!

### 4.1 ComputeContentIdFromMetadata - Pre-Download (DownloadCacheOptimizer.cs)

```csharp
/// <summary>
/// Computes ContentId from provider metadata BEFORE file download.
/// This enables distributed discovery before downloading from the original URL.
/// CRITICAL: This must be deterministic across all clients globally!
/// </summary>
public static string ComputeContentIdFromMetadata(
    Dictionary<string, object> normalizedMetadata,
    string primaryUrl)
{
    // Build deterministic info dict from metadata ONLY
    var infoDict = new SortedDictionary<string, object>
    {
        ["provider"] = normalizedMetadata["provider"],
        ["url_canonical"] = UrlNormalizer.Normalize(primaryUrl)
    };

    // Add provider-specific fields (these MUST match the whitelist from Phase 2.2)
    string provider = normalizedMetadata["provider"].ToString();

    switch (provider)
    {
        case "deadlystream":
            infoDict["filePageId"] = normalizedMetadata.ContainsKey("filePageId") ? normalizedMetadata["filePageId"] : "";
            infoDict["changelogId"] = normalizedMetadata.ContainsKey("changelogId") ? normalizedMetadata["changelogId"] : "";
            infoDict["fileId"] = normalizedMetadata.ContainsKey("fileId") ? normalizedMetadata["fileId"] : "";
            infoDict["version"] = normalizedMetadata.ContainsKey("version") ? normalizedMetadata["version"] : "";
            infoDict["updated"] = normalizedMetadata.ContainsKey("updated") ? normalizedMetadata["updated"] : "";
            infoDict["size"] = normalizedMetadata.ContainsKey("size") ? normalizedMetadata["size"] : 0L;
            break;

        case "mega":
            infoDict["nodeId"] = normalizedMetadata.ContainsKey("nodeId") ? normalizedMetadata["nodeId"] : "";
            infoDict["hash"] = normalizedMetadata.ContainsKey("hash") ? normalizedMetadata["hash"] : "";
            infoDict["size"] = normalizedMetadata.ContainsKey("size") ? normalizedMetadata["size"] : 0L;
            infoDict["mtime"] = normalizedMetadata.ContainsKey("mtime") ? normalizedMetadata["mtime"] : 0L;
            infoDict["name"] = normalizedMetadata.ContainsKey("name") ? normalizedMetadata["name"] : "";
            break;

        case "nexus":
            infoDict["fileId"] = normalizedMetadata.ContainsKey("fileId") ? normalizedMetadata["fileId"] : "";
            infoDict["fileName"] = normalizedMetadata.ContainsKey("fileName") ? normalizedMetadata["fileName"] : "";
            infoDict["size"] = normalizedMetadata.ContainsKey("size") ? normalizedMetadata["size"] : 0L;
            infoDict["uploadedTimestamp"] = normalizedMetadata.ContainsKey("uploadedTimestamp") ? normalizedMetadata["uploadedTimestamp"] : 0L;
            infoDict["md5Hash"] = normalizedMetadata.ContainsKey("md5Hash") ? normalizedMetadata["md5Hash"] : "";
            break;

        case "direct":
            infoDict["url"] = normalizedMetadata.ContainsKey("url") ? normalizedMetadata["url"] : "";
            infoDict["contentLength"] = normalizedMetadata.ContainsKey("contentLength") ? normalizedMetadata["contentLength"] : 0L;
            infoDict["lastModified"] = normalizedMetadata.ContainsKey("lastModified") ? normalizedMetadata["lastModified"] : "";
            infoDict["etag"] = normalizedMetadata.ContainsKey("etag") ? normalizedMetadata["etag"] : "";
            infoDict["fileName"] = normalizedMetadata.ContainsKey("fileName") ? normalizedMetadata["fileName"] : "";
            break;
    }

    // Bencode and hash to create infohash
    byte[] bencodedInfo = BencodeCanonical(infoDict);
    byte[] infohash = SHA1.HashData(bencodedInfo);
    string contentId = BitConverter.ToString(infohash).Replace("-", "").ToLowerInvariant();

    return contentId;
}
```

### 4.2 ComputeFileIntegrityData - Post-Download (DownloadCacheOptimizer.cs)

```csharp
/// <summary>
/// Computes integrity hashes AFTER file download.
/// Used to verify the file matches expected content and enable cache sharing.
/// </summary>
public static async Task<(string contentHashSHA256, int pieceLength, string pieceHashes)>
    ComputeFileIntegrityData(string filePath)
{
    var fileInfo = new FileInfo(filePath);
    long fileSize = fileInfo.Length;

    // 1. Determine canonical piece size
    int pieceLength = DeterminePieceSize(fileSize);

    // 2. Compute piece hashes (SHA-1, 20 bytes each) for cache transfer verification
    var pieceHashList = new List<byte[]>();
    using (var fs = File.OpenRead(filePath))
    {
        byte[] buffer = new byte[pieceLength];
        while (true)
        {
            int bytesRead = await fs.ReadAsync(buffer, 0, pieceLength);
            if (bytesRead == 0) break;

            byte[] pieceData = bytesRead == pieceLength ? buffer : buffer.Take(bytesRead).ToArray();
            byte[] pieceHash = SHA1.HashData(pieceData);
            pieceHashList.Add(pieceHash);
        }
    }

    // Concatenate piece hashes as hex
    string pieceHashes = string.Concat(pieceHashList.Select(h =>
        BitConverter.ToString(h).Replace("-", "").ToLowerInvariant()));

    // 3. Compute SHA-256 of entire file (CANONICAL integrity check)
    byte[] sha256;
    using (var fs = File.OpenRead(filePath))
    {
        using (var sha = SHA256.Create())
        {
            sha256 = await sha.ComputeHashAsync(fs);
        }
    }
    string contentHashSHA256 = BitConverter.ToString(sha256).Replace("-", "").ToLowerInvariant();

    return (contentHashSHA256, pieceLength, pieceHashes);
}
```

**Canonical Bencoding (byte-for-byte deterministic)**:

```csharp
private static byte[] BencodeCanonical(SortedDictionary<string, object> dict)
{
    var output = new List<byte>();
    output.Add((byte)'d');

    foreach (var kvp in dict) // SortedDictionary ensures lexicographic key order
    {
        // Encode key (string)
        byte[] keyBytes = Encoding.UTF8.GetBytes(kvp.Key);
        output.AddRange(Encoding.ASCII.GetBytes(keyBytes.Length.ToString()));
        output.Add((byte)':');
        output.AddRange(keyBytes);

        // Encode value
        if (kvp.Value is long longVal)
        {
            output.Add((byte)'i');
            output.AddRange(Encoding.ASCII.GetBytes(longVal.ToString()));
            output.Add((byte)'e');
        }
        else if (kvp.Value is string strVal)
        {
            byte[] strBytes = Encoding.UTF8.GetBytes(strVal);
            output.AddRange(Encoding.ASCII.GetBytes(strBytes.Length.ToString()));
            output.Add((byte)':');
            output.AddRange(strBytes);
        }
        else if (kvp.Value is byte[] byteVal)
        {
            output.AddRange(Encoding.ASCII.GetBytes(byteVal.Length.ToString()));
            output.Add((byte)':');
            output.AddRange(byteVal);
        }
    }

    output.Add((byte)'e');
    return output.ToArray();
}
```

**Multi-File Canonical Ordering** (for future multi-file support):

- Files sorted lexicographically by normalized UTF-8 path bytes
- Paths use forward slashes, Unicode NFC
- Each file: `{length: N, path: ["dir", "file.ext"]}`

### 4.2 Partial File Handling & Staging (DownloadCacheOptimizer.cs)

```csharp
private static string GetPartialFilePath(string contentKey, string destinationDirectory)
{
    string partialDir = Path.Combine(destinationDirectory, ".partial");
    if (!Directory.Exists(partialDir))
        Directory.CreateDirectory(partialDir);

    return Path.Combine(partialDir, $"{contentKey.Substring(0,32)}.part");
}

private static readonly ConcurrentDictionary<string, SemaphoreSlim> s_contentKeyLocks = new();

private static async Task<IDisposable> AcquireContentKeyLock(string contentKey)
{
    var sem = s_contentKeyLocks.GetOrAdd(contentKey, _ => new SemaphoreSlim(1, 1));
    await sem.WaitAsync();
    return new LockReleaser(() => {
        sem.Release();
        // Clean up if no waiters
        if (sem.CurrentCount == 1)
            s_contentKeyLocks.TryRemove(contentKey, out _);
    });
}

private class LockReleaser : IDisposable
{
    private readonly Action _release;
    public LockReleaser(Action release) => _release = release;
    public void Dispose() => _release();
}
```

### 4.3 Network Cache Lookup Protocol (DownloadCacheOptimizer.cs)

**Updated TryOptimizedDownload signature**:

```csharp
public static async Task<DownloadResult> TryOptimizedDownload(
    string lookupKey,              // ContentId (if known) or MetadataHash
    ResourceMetadata metadata,     // For integrity verification
    string destinationDirectory,
    Func<Task<DownloadResult>> urlDownloadFunc,
    IProgress<DownloadProgress> progress,
    CancellationToken cancellationToken)
```

**Lookup & Download Flow**:

1. Check if `.dat` metadata file exists for `lookupKey`
2. If exists: race network cache vs URL download
3. Whichever completes first: verify integrity using `VerifyContentIntegrity()`
4. If verification fails: try other source
5. If both sources fail verification: delete partial files and return error
6. Download to `.partial/<contentKey>.part` and only move to final path after verification

**Network Cache Download** (`TryDistributedDownloadAsync`):

- Load `.dat` file using reflection helpers from the network cache engine
- Create a session manager pointing to the partial file path
- Register with the client engine instance
- Download pieces
- Return partial path for verification (don't move to final yet)

### 4.4 DownloadCacheService Pre-Resolution Flow (CORRECTED)

```csharp
// After resolving filenames
var metadata = await handler.GetFileMetadataAsync(url, cancellationToken);
var normalized = handler.NormalizeMetadata(metadata); // Apply whitelist + type normalization
normalized["provider"] = handler.GetProviderKey();

// Compute MetadataHash (for URL → ContentId mapping)
string metadataHash = CanonicalJson.ComputeHash(normalized);

// CRITICAL: Compute ContentId FROM METADATA (before download!)
string contentId = DownloadCacheOptimizer.ComputeContentIdFromMetadata(normalized, url);

// Check if we already have this ContentId
lock (s_resourceIndexLock)
{
    if (s_resourceByContentId.TryGetValue(contentId, out var existing))
    {
        // Already tracked - update component registry
        component.ResourceRegistry[metadataHash] = existing;

        // Update mapping if needed
        if (!s_metadataHashToContentId.ContainsKey(metadataHash))
        {
            s_metadataHashToContentId[metadataHash] = contentId;
        }

        return; // Already tracked
    }
}

// New file - create entry with ContentId ALREADY POPULATED
var resourceMeta = new ResourceMetadata
{
    ContentId = contentId,              // ✅ COMPUTED FROM METADATA!
    MetadataHash = metadataHash,
    PrimaryUrl = url,
    HandlerMetadata = normalized,
    FileSize = (long)(normalized.ContainsKey("size") ? normalized["size"] :
                      normalized.ContainsKey("contentLength") ? normalized["contentLength"] : 0L),
    FirstSeen = DateTime.UtcNow,
    SchemaVersion = 1,
    TrustLevel = MappingTrustLevel.Unverified,
    // Post-download fields remain NULL until download
    ContentHashSHA256 = null,
    PieceLength = 0,
    PieceHashes = null,
    Files = new Dictionary<string, bool?>(StringComparer.Ordinal)
};

lock (s_resourceIndexLock)
{
    s_resourceByContentId[contentId] = resourceMeta;
    s_metadataHashToContentId[metadataHash] = contentId;
    s_resourceByMetadataHash[metadataHash] = resourceMeta;
    component.ResourceRegistry[metadataHash] = resourceMeta;
}

await SaveResourceIndexAsync();

Logger.LogVerbose($"[Cache] Created new ContentId for {url}: {contentId.Substring(0, 16)}...");
```

### 4.5 DownloadCacheService Download Flow (CORRECTED)

```csharp
// 1. Lookup ContentId (should already exist from PreResolve phase!)
ResourceMetadata resourceMeta = null;
string contentId = null;

lock (s_resourceIndexLock)
{
    // Find by URL metadata hash
    if (s_metadataHashToContentId.TryGetValue(url_metadataHash, out contentId))
    {
        s_resourceByContentId.TryGetValue(contentId, out resourceMeta);
    }
}

if (contentId == null || resourceMeta == null)
{
    // No ContentId (shouldn't happen if PreResolve ran) - fallback to URL only
    Logger.LogWarning($"[Cache] No ContentId found for URL during download (missing PreResolve?)");
    return await urlDownloadFunc();
}

// 2. Try distributed cache download using ContentId, race with URL download
var result = await DownloadCacheOptimizer.TryOptimizedDownload(
    contentId,                // ← Use ContentId for distributed lookup!
    resourceMeta,
    destinationDirectory,
    urlDownloadFunc,
    progress,
    cancellationToken
);

// 3. Post-download: compute INTEGRITY hashes (ContentId already exists!)
if (result.Success && File.Exists(result.FilePath))
{
    var (sha256, pieceLen, pieces) = await ComputeFileIntegrityData(result.FilePath);

    // Update metadata with integrity data
    resourceMeta.ContentHashSHA256 = sha256;
    resourceMeta.PieceLength = pieceLen;
    resourceMeta.PieceHashes = pieces;
    resourceMeta.LastVerified = DateTime.UtcNow;

    // Add filename
    string fileName = Path.GetFileName(result.FilePath);
    if (!resourceMeta.Files.ContainsKey(fileName))
    {
        resourceMeta.Files[fileName] = true;
    }

    // Update dual index atomically
    bool updated = await UpdateMappingWithVerification(resourceMeta.MetadataHash, contentId, resourceMeta);
    if (updated)
    {
        await SaveResourceIndexAsync();
    }
}

return result;
```

## Phase 5: Testing (Comprehensive & Cross-Platform)

### 5.1 CanonicalJson Tests (CanonicalJsonTests.cs)

- **Unicode normalization**: NFC vs NFD input → same output
- **Whitespace preservation**: Strings with leading/trailing/internal whitespace → preserved exactly
- **Number formatting**: Large numbers, negative numbers, decimals, integers → consistent InvariantCulture output
- **No exponent notation**: Verify no scientific notation (1e6 → 1000000)
- **Boolean lowercase**: true/false never True/False
- **Array ordering**: Preserve array order, verify not sorted
- **Dictionary key ordering**: Nested dicts sorted by UTF-8 bytes lexicographically
- **Round-trip stability**: Serialize → hash → deserialize → serialize → hash must match
- **Cross-language golden files**: Compare C# output against Python/Node.js implementations using same input

### 5.2 Canonical Bencoding Tests (BencodingTests.cs)

**CRITICAL for determinism**:

- **Cross-platform**: Same file → same bencoded bytes on Windows/Linux/macOS
- **Cross-language**: C# bencoder vs reference implementations (Python bencodepy, transmission)
- **Golden file tests**: Fixed test vector files with known infohash values
- **Dict key ordering**: Verify lexicographic byte ordering (not string ordering)
- **Integer edge cases**: 0, negative numbers, large longs
- **String encoding**: UTF-8 bytes, length prefix accuracy
- **Byte array handling**: Raw bytes (pieces field) encoded correctly

### 5.3 Deterministic ContentId Tests (ContentIdDeterminismTests.cs)

- **Same file → same ContentId**: Identical file bytes → identical ContentId across all platforms
- **Piece size determinism**: Files at boundary sizes (pieceSize * N ± 1)
- **Tiny files**: Files < 1 KB → single piece, correct handling
- **Huge files**: Multi-GB files → correct piece size selection, < 1M pieces
- **Filename sanitization**: Same bytes, different names → different ContentId (name affects infohash)
- **Canonical name**: Same bytes, same sanitized name → identical ContentId

### 5.4 Integrity Verification Tests (IntegrityVerificationTests.cs)

- **Poisoned content**: Wrong bytes → ContentHashSHA256 mismatch detected
- **Piece corruption**: Single corrupted piece → piece hash verification fails
- **Partial download**: Incomplete file → piece count mismatch detected
- **Size mismatch**: File truncated/extended → size check fails
- **Hash priority**: Verify ContentHashSHA256 is CANONICAL (not ContentId/infohash)

### 5.5 Concurrency & Race Condition Tests (ConcurrencyTests.cs)

- **Concurrent mapping updates**: 10 threads mapping same MetadataHash → different ContentIds → final state is deterministic
- **Atomic ResourceIndex writes**: Concurrent writes → no corruption, all updates preserved
- **File lock correctness**: Two processes writing ResourceIndex → both succeed, no data loss
- **Per-ContentKey locking**: Multiple downloads of same ContentKey → only one active, others wait
- **Trust elevation race**: Two verifications arriving simultaneously → trust level correctly elevated to Verified

### 5.6 Provider-Specific Tests (DeadlyStreamMetadataTests.cs, etc.)

**DeadlyStream**:

- Changelog dropdown parsing for test URLs
- Multi-file page with multiple `r=` IDs
- Orphaned changelog ID (deleted by maintainer) → graceful handling
- Metadata field whitelist → only specified fields affect metadataHash

**MEGA**:

- Extract window.dl_node hash, mtime, size
- Merkle hash stability

**Nexus**:

- API metadata retrieval
- md5Hash extraction and normalization

**Direct**:

- HEAD request success → extract Content-Length, Last-Modified, ETag
- HEAD not supported → fallback to ranged GET (Range: bytes=0-0)
- Transfer-Encoding chunked → handle missing Content-Length
- ETag missing → omit from metadata (don't break hash)

### 5.7 URL Normalization Tests (UrlNormalizationTests.cs)

- **Percent-encoding**: Equivalent URLs with different encoding → same normalized form
- **Query parameter ordering**: Same params, different order → provider-specific (some preserve, some sort)
- **Default ports**: `http://example.com:80` → `http://example.com`
- **Trailing slash**: `http://example.com/path/` vs `/path` → canonicalized consistently
- **Case normalization**: Scheme/host lowercase, path preserved
- **Path normalization**: `/../` and `/./` sequences collapsed

### 5.8 End-to-End Fallback Tests (E2EFallbackTests.cs)

- **URL 404**: URL returns 404 → network cache succeeds → file verified
- **Cache unavailable**: No network metadata → URL download succeeds
- **Content changed at URL**: URL serves new content (new MetadataHash) → old ContentId still works for cache
- **Cache integrity failure**: Network cache corrupted → fallback to URL
- **Both sources fail**: URL 404 + cache corrupted → error with clear message

### 5.9 Security & Poisoning Tests (SecurityTests.cs)

- **Blocked ContentId**: Blocked ID → download refuses cache, uses URL only
- **Poisoning simulation**: Malicious mapping MetadataHash → wrong ContentId → integrity check rejects
- **Trust elevation**: Single verification → ObservedOnce, second verification → Verified
- **Mapping conflict**: Different ContentIds for same MetadataHash → keeps Verified, logs conflict

### 5.10 Schema Migration Tests (SchemaMigrationTests.cs)

- **V1 → V2**: Load old resource-index.json → migrate to new schema
- **Forward compatibility**: New client writes v2 → old client reads (graceful degradation)
- **Unknown fields**: Extra fields in metadata → preserved through round-trip

### 5.11 Cross-Platform File Operations Tests (CrossPlatformTests.cs)

- **Atomic replace**: Windows File.Replace vs POSIX rename → both atomic
- **File locking**: FileStream.Lock works on Windows/Linux/macOS
- **Path construction**: Cross-platform cache paths use correct separators
- **Temp file cleanup**: .partial directory cleaned after crashes

### 5.12 Provider Field Whitelist Stability Tests (ProviderWhitelistTests.cs)

- **Extra fields ignored**: Provider adds new field → metadataHash unchanged (only whitelisted fields affect hash)
- **Missing optional fields**: Provider removes optional field → metadataHash changes only if field was whitelisted
- **Type consistency**: Numeric field as string vs number → handler normalizes to same type before hash

### 5.13 Atomic Mapping Transaction Tests (MappingTransactionTests.cs)

- **Compare-and-swap**: Update mapping MetadataHash → ContentId while another thread reads → no race
- **Verified mapping protection**: Attempt to overwrite Verified mapping with Unverified → rejected
- **Dual index consistency**: After updates, metadataHashToContentId and resourceByContentId stay synchronized

### 5.14 Crash Recovery & Partial Write Tests (CrashRecoveryTests.cs)

- **ResourceIndex partial write**: Simulate crash during SaveResourceIndexAtomic → .tmp file exists, original intact
- **Stale temp files**: Old .tmp files from previous crashes → cleaned up on startup
- **Backup file recovery**: If main file corrupt but backup exists → restore from backup

### 5.15 Network Anomaly Tests (NetworkAnomalyTests.cs)

- **Wrong Content-Length**: Server reports size X, actual size Y → detect and handle
- **Chunked transfer**: Transfer-Encoding: chunked, no Content-Length → parse chunks correctly
- **Multiple redirects**: 3xx chains → follow and canonicalize final URL
- **Hostname changes**: Provider migrates domains → URL normalization handles gracefully
- **Intermittent corruption**: Random byte errors during download → integrity check catches

### 5.16 Piece Size Boundary Tests (PieceSizeTests.cs)

- **Exact multiples**: File size exactly `pieceSize * N` → correct piece count
- **Off by one**: File size `pieceSize * N ± 1` → correct piece selection
- **Tiny file**: 100 bytes → uses 64KB piece size, 1 piece
- **Large file**: 5 GB → adaptive piece size (2MB or 4MB), stays under 1M pieces
- **Boundary transition**: File size where algorithm switches piece size candidates → deterministic

### 5.17 Serialization Round-Trip Tests (ResourceRegistrySerializationTests.cs)

- **TOML round-trip**: Serialize → save → load → deserialize → compare
- **JSON round-trip**: Same for JSON
- **YAML round-trip**: Same for YAML
- **XML round-trip**: Same for XML
- **Cross-format**: TOML → JSON → YAML → XML → back to TOML → verify equivalent
- **Case insensitivity**: Keys with different cases load correctly

## Phase 6: Operational, Admin & Monitoring

### 6.1 Metrics & Monitoring

Add to `TelemetryService`:

```csharp
public void RecordCacheOperation(
    string operationType,  // "hit", "miss", "integrity_failure", "url_fallback", "poisoning_detected"
    string contentKey,     // Truncate to first 16 chars before sending
    bool success,
    long durationMs,
    string errorType = null)
```

**Privacy**: Never send full ContentIds or URLs in telemetry. Hash or truncate before transmission.

### 6.2 Admin Tools & CLI Commands

**Add to Program.cs CLI**:

```bash
# Inspection
--cache-inspect <contentId>                    # Show full ResourceMetadata
--cache-list [--trust-level <level>]           # List entries, filter by trust
--cache-stats                                  # Stats: entries, trust distribution, disk usage

# Management
--cache-block <contentId> [--reason "<text>"]  # Add to blocklist with audit
--cache-unblock <contentId>                    # Remove from blocklist
--cache-verify <contentId>                     # Re-compute hashes, verify integrity
--cache-rebuild                                # Rebuild index from local .dat files
--cache-export <path>                          # Export index for migration

# Maintenance
--cache-gc [--max-age-days N]                  # GC with configurable age
--cache-prune [--max-size-mb N]                # LRU eviction to stay under quota
--cache-audit                                  # Show block-audit.log and mapping changes
--cache-export-audit <output.json>             # Export compliance report
```

### 6.3 Garbage Collection & Disk Management

**Auto-GC on startup**:

```csharp
private static void GarbageCollectResourceIndex()
{
    var now = DateTime.UtcNow;
    var toRemove = new List<string>();

    lock (s_resourceIndexLock)
    {
        foreach (var kvp in s_resourceByContentId)
        {
            var meta = kvp.Value;

            // Rule 1: Old and file doesn't exist
            if (meta.LastVerified.HasValue && (now - meta.LastVerified.Value).TotalDays > 90)
            {
                string expectedFile = Path.Combine(MainConfig.SourcePath.FullName, meta.Files.Keys.FirstOrDefault() ?? "");
                if (!File.Exists(expectedFile))
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }
            }

            // Rule 2: Never used and very old
            if (!meta.LastVerified.HasValue && meta.FirstSeen.HasValue && (now - meta.FirstSeen.Value).TotalDays > 365)
            {
                toRemove.Add(kvp.Key);
            }

            // Rule 3: Downgrade trust if not re-verified
            if (meta.LastVerified.HasValue && (now - meta.LastVerified.Value).TotalDays > 30)
            {
                if (meta.TrustLevel == MappingTrustLevel.Verified)
                    meta.TrustLevel = MappingTrustLevel.ObservedOnce;
                else if (meta.TrustLevel == MappingTrustLevel.ObservedOnce)
                    meta.TrustLevel = MappingTrustLevel.Unverified;
            }
        }

        foreach (string key in toRemove)
        {
            s_resourceByContentId.Remove(key);
            var metaMapping = s_metadataHashToContentId.FirstOrDefault(m => m.Value == key);
            if (metaMapping.Key != null)
                s_metadataHashToContentId.Remove(metaMapping.Key);
        }
    }

    Logger.LogVerbose($"[Cache] GC removed {toRemove.Count} stale entries");
}
```

**Disk Quota (LRU eviction)**:

```csharp
private static void EnforceDiskQuota(long maxSizeBytes)
{
    string cacheDir = Path.Combine(Path.GetDirectoryName(GetResourceIndexPath()), "Network");
    var datFiles = Directory.GetFiles(cacheDir, "*.dat");

    long totalSize = datFiles.Sum(f => new FileInfo(f).Length);
    if (totalSize <= maxSizeBytes)
    {
        return;
    }

    // Sort by LastVerified (oldest first)
    var entries = s_resourceByContentId.OrderBy(e => e.Value.LastVerified ?? e.Value.FirstSeen).ToList();

    foreach (var entry in entries)
    {
        string datPath = GetCachePath(entry.Key);
        if (File.Exists(datPath))
        {
            long fileSize = new FileInfo(datPath).Length;
            File.Delete(datPath);
            totalSize -= fileSize;

            s_resourceByContentId.Remove(entry.Key);

            if (totalSize <= maxSizeBytes) break;
        }
    }

    Logger.LogVerbose($"[Cache] Quota enforcement: pruned to {totalSize / (1024*1024)} MB");
}
```

**Default Quota**: 10 GB, add to MainConfig: `public static long MaxCacheSizeMB { get; set; } = 10240;`

### 6.4 Audit Trail & Compliance Logging

**Audit Log Format** (`block-audit.log`):

```text
2025-10-22T14:30:00Z|BLOCK|a1b2c3d4...|DMCA request #12345
2025-10-22T15:00:00Z|MAPPING_CONFLICT|meta_abc...→content_def...|Rejected (existing Verified)
2025-10-22T15:30:00Z|TRUST_ELEVATED|content_xyz...|ObservedOnce→Verified
2025-10-22T16:00:00Z|INTEGRITY_FAILURE|content_123...|SHA-256 mismatch
```

**Compliance Export**:

```csharp
--cache-export-audit <output.json>
// Exports: blocklist with reasons, mapping conflicts, integrity failures
```

### 6.5 Schema Versioning & Forward Compatibility

**Current Schema Version**: 1

**Version 1 Fields** (ResourceMetadata):

- ContentKey, ContentId, ContentHashSHA256, MetadataHash, PrimaryUrl, HandlerMetadata, Files, FileSize, PieceLength, PieceHashes, FirstSeen, LastVerified, SchemaVersion, TrustLevel

**Migration Strategy**:

- Unknown/future fields preserved during deserialization (don't discard)
- Old clients ignore unknown fields gracefully
- New clients migrate old schemas on load

**Documentation**: Create `CANONICAL_SPEC.md` in repo documenting:

- Bencoding rules with examples
- CanonicalJson algorithm with test vectors
- Provider metadata schemas and whitelists
- ContentId vs ContentHashSHA256 usage
- Multi-file ordering rules

## Key Files to Modify

**Core Infrastructure**:

1. `KOTORModSync.Core/Utility/CanonicalJson.cs` - NEW (canonical serialization + hashing)
2. `KOTORModSync.Core/Utility/CanonicalBencoding.cs` - NEW (deterministic bencoding)
3. `KOTORModSync.Core/Utility/UrlNormalizer.cs` - NEW (canonical URL normalization)
4. `KOTORModSync.Core/ModComponent.cs` - Add ResourceRegistry, ResourceMetadata, MappingTrustLevel
5. `KOTORModSync.Core/MainConfig.cs` - Add MaxCacheSizeMB config
6. `KOTORModSync.Core/Services/ModComponentSerializationService.cs` - Serialize/deserialize ResourceRegistry

**Download Handlers**:
7. `KOTORModSync.Core/Services/Download/IDownloadHandler.cs` - Add GetFileMetadataAsync, GetProviderKey, NormalizeMetadata
8. `KOTORModSync.Core/Services/Download/DeadlyStreamDownloadHandler.cs` - Implement metadata + changelog parsing
9. `KOTORModSync.Core/Services/Download/MegaDownloadHandler.cs` - Implement metadata extraction
10. `KOTORModSync.Core/Services/Download/NexusModsDownloadHandler.cs` - Implement metadata from API
11. `KOTORModSync.Core/Services/Download/DirectDownloadHandler.cs` - Implement metadata with HEAD/Range fallback
12. `KOTORModSync.Core/Services/Download/DownloadCacheOptimizer.cs` - Add ComputeContentIdentifiers, integrity verification, blocked list, partial file handling
13. `KOTORModSync.Core/Services/DownloadCacheService.cs` - Dual index structure, ResourceIndex persistence, pre-resolution/download flows

**Tests** (17 new test files):
14. `KOTORModSync.Tests/CanonicalJsonTests.cs` - NEW
15. `KOTORModSync.Tests/BencodingTests.cs` - NEW (cross-platform/cross-language)
16. `KOTORModSync.Tests/ContentIdDeterminismTests.cs` - NEW
17. `KOTORModSync.Tests/IntegrityVerificationTests.cs` - NEW
18. `KOTORModSync.Tests/ConcurrencyTests.cs` - NEW
19. `KOTORModSync.Tests/DeadlyStreamMetadataTests.cs` - NEW
20. `KOTORModSync.Tests/MegaMetadataTests.cs` - NEW
21. `KOTORModSync.Tests/NexusMetadataTests.cs` - NEW
22. `KOTORModSync.Tests/DirectMetadataTests.cs` - NEW
23. `KOTORModSync.Tests/UrlNormalizationTests.cs` - NEW
24. `KOTORModSync.Tests/E2EFallbackTests.cs` - NEW
25. `KOTORModSync.Tests/SecurityTests.cs` - NEW
26. `KOTORModSync.Tests/SchemaMigrationTests.cs` - NEW
27. `KOTORModSync.Tests/CrossPlatformTests.cs` - NEW
28. `KOTORModSync.Tests/ProviderWhitelistTests.cs` - NEW
29. `KOTORModSync.Tests/MappingTransactionTests.cs` - NEW
30. `KOTORModSync.Tests/CrashRecoveryTests.cs` - NEW
31. `KOTORModSync.Tests/NetworkAnomalyTests.cs` - NEW
32. `KOTORModSync.Tests/PieceSizeTests.cs` - NEW
33. `KOTORModSync.Tests/ResourceRegistrySerializationTests.cs` - NEW

**Documentation**:
34. `CANONICAL_SPEC.md` - NEW (comprehensive specification document)

## Phase 7: Critical Implementation Checklist

### Immediate Blockers (Must Fix Before Coding)

1. **ContentId hash algorithm decision** ✓
   - RESOLVED: ContentId = SHA-1(bencode(info)) for cross-client compatibility
   - ContentHashSHA256 = SHA-256(file) for integrity (CANONICAL)

2. **Piece hash storage** ✓
   - RESOLVED: Added PieceLength + PieceHashes to ResourceMetadata

3. **Key upgrade race** ✓
   - RESOLVED: Dual index (metadataHash map + contentId map), no key renaming

4. **Atomic file operations** ✓
   - RESOLVED: File.Replace on Windows, File.Move on POSIX with backup

5. **Cross-process locking** ✓
   - RESOLVED: FileStream.Lock on .lock file for ResourceIndex operations

6. **ComputeContentId specification** ✓
   - RESOLVED: Full implementation in Phase 4.1

7. **Network cache lookup protocol** ✓
   - RESOLVED: Load .dat file by lookupKey using engine reflection APIs

8. **Partial file staging** ✓
   - RESOLVED: .partial directory with ContentKey-based locking

9. **Bencoding determinism** ✓
   - RESOLVED: Canonical implementation + tests specified

10. **Handler metadata normalization** ✓
    - RESOLVED: Type normalization + field whitelists per provider

11. **URL canonicalization** ✓
    - RESOLVED: Comprehensive normalization rules in 1.1

12. **Multi-file ordering** ✓
    - RESOLVED: Lexicographic ordering by normalized UTF-8 path bytes

13. **Schema versioning** ✓
    - RESOLVED: SchemaVersion field + migration handler

14. **Provider field whitelists** ✓
    - RESOLVED: Explicit whitelists for each provider in Phase 2.2

15. **HEAD fallback for Direct handler** ✓
    - RESOLVED: Specified in Phase 5.6 tests + implementation notes

### Implementation Order (Dependency-Sorted)

**Layer 1 - Foundations** (no dependencies):

1. ✅ CanonicalJson.cs
2. ✅ CanonicalBencoding.cs
3. ✅ UrlNormalizer.cs
4. ✅ ResourceMetadata class in ModComponent.cs
5. ✅ MainConfig additions

**Layer 2 - Infrastructure** (depends on Layer 1):
6. ✅ ModComponentSerializationService updates
7. ✅ ResourceIndex dual index in DownloadCacheService
8. ✅ Atomic persistence with locking

**Layer 3 - Handlers** (depends on Layer 1):
9. ✅ IDownloadHandler interface updates
10. ✅ DeadlyStreamDownloadHandler metadata
11. ✅ MegaDownloadHandler metadata (VERIFIED - uses INode.Fingerprint from MegaApiClient source)
12. ✅ NexusModsDownloadHandler metadata
13. ✅ DirectDownloadHandler metadata

**Layer 4 - Integration** (depends on Layers 1-3):
14. ✅ DownloadCacheOptimizer updates (ComputeContentIdentifiers, integrity verification, partial files)
15. ✅ DownloadCacheService pre-resolution + download flows (metadata extraction & content ID complete, ~70% done)

**Layer 5 - Tests** (depends on all layers):
16-33. ❌ All test files (can be developed in parallel with implementation)

**Layer 6 - Documentation**:
34. ✅ CANONICAL_SPEC.md
