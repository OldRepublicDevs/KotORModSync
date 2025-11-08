# KOTORModSync Distributed Cache Tests

This directory contains comprehensive integration tests for the distributed cache (P2P) system in KOTORModSync.

## Test Organization

Tests are organized into several categories, each with specific focus areas:

### 1. ContentIdTests (20+ tests)

Tests for ContentId generation, idempotency, and collision detection:

- Same file produces same hash
- Different files produce different hashes
- Various file sizes (empty to 100MB+)
- Binary patterns and edge cases
- Deterministic across multiple runs

### 2. SeedingIntegrationTests (12+ tests) [Docker Required]

Integration tests using real BitTorrent clients in Docker containers:

- qBittorrent, Transmission, and Deluge support
- Local Peer Discovery (LPD) validation
- Upload tracking and verification
- Multi-peer scenarios
- Connection and state management

### 3. PortManagementTests (12+ tests)

Tests for port management, persistence, and NAT traversal:

- Port selection and persistence
- Configuration file handling
- NAT traversal (UPnP, NAT-PMP)
- Port conflict resolution

### 4. CacheEngineTests (20+ tests)

Tests for the distributed cache engine lifecycle and statistics:

- Engine initialization and shutdown
- Statistics accuracy
- Resource sharing management
- Bandwidth limiting
- Connection limits

### 5. RealModIntegrationTests (10+ tests)

Integration tests using real KOTOR mod builds:

- Tests with KOTOR1_Full.toml and KOTOR2_Full.toml from mod-builds submodule
- ResourceRegistry population
- ContentId generation for real mods
- Metadata hash validation

### 6. MetadataConsistencyTests (16+ tests)

Tests for metadata consistency and integrity:

- Torrent file generation
- Piece hash correctness
- Deterministic metadata
- Various content patterns
- Size boundary conditions

### 7. ErrorHandlingAndEdgeCaseTests (40+ tests)

Comprehensive error handling and edge case tests:

- Zero-byte files
- Piece size boundaries
- Invalid inputs
- Concurrent access
- Special characters in filenames
- Various binary patterns

## Running Tests

### All Tests

```bash
dotnet test KOTORModSync.Tests --filter Category=DistributedCache
```

### Specific Category

```bash
dotnet test KOTORModSync.Tests --filter "FullyQualifiedName~ContentIdTests"
dotnet test KOTORModSync.Tests --filter "FullyQualifiedName~CacheEngineTests"
```

### Docker Tests (Requires Docker/Podman)

```bash
# Remove Skip attributes first or set environment variable
dotnet test KOTORModSync.Tests --filter "FullyQualifiedName~SeedingIntegrationTests"
```

### CLI Commands

#### Run Tests via CLI

```bash
dotnet run --project KOTORModSync.Core -- cache-test --category All --verbose
```

#### Start Seeding Operation

```bash
dotnet run --project KOTORModSync.Core -- cache-seed \
  --toml ./mod-builds/TOMLs/KOTOR1_Full.toml \
  --source-path ~/kotor-mods \
  --duration 21600 \
  --limit 10 \
  --verbose
```

## GitHub Actions Integration

Tests run automatically on GitHub Actions:

### Scheduled Runs (Hourly)

- Unit tests (no Docker)
- Docker integration tests
- Long-running seed operation (5-6 hours)

### Manual Triggers

- Select test categories
- Enable/disable Docker tests
- Configure seed duration

See `.github/workflows/distributed-cache-tests.yml` for workflow configuration.

## Test Infrastructure

### DockerBitTorrentClient

Manages containerized BitTorrent clients for integration testing:

- Auto-detects Docker or Podman
- Supports qBittorrent, Transmission, Deluge
- API interaction for adding torrents and checking stats
- Automatic cleanup

### DistributedCacheTestFixture

Provides test infrastructure:

- Temporary directories and files
- Torrent file creation
- ContentId computation
- Wait conditions for async operations

## Requirements

### For Unit Tests

- .NET 8.0 SDK
- No additional dependencies

### For Docker Tests

- Docker or Podman
- Network access for pulling container images
- At least 2GB RAM available for containers

### For Real Mod Tests

- mod-builds submodule initialized
- KOTOR1_Full.toml and KOTOR2_Full.toml present

## Total Test Count

**Current Total: 130+ comprehensive tests**

- ContentId: 20 tests
- Seeding Integration: 12 tests (Docker)
- Port Management: 12 tests
- Cache Engine: 20 tests
- Real Mod Integration: 10 tests
- Metadata Consistency: 16 tests
- Error Handling & Edge Cases: 40 tests

## Continuous Seeding

The GitHub Actions workflow includes a long-running seed operation that:

- Runs for 5-6 hours (GitHub Actions max timeout)
- Seeds multiple files concurrently
- Reports statistics every 5 minutes
- Serves as a low-end seedbox for the distributed cache network

This ensures the distributed cache network has active seeders for testing and development.

## Contributing

When adding new tests:

1. Follow the existing test organization
2. Add appropriate `[Fact]` or `[Theory]` attributes
3. Use `[Collection("DistributedCache")]` attribute
4. Mark Docker-requiring tests with `[Fact(Skip = "Requires Docker")]`
5. Add test descriptions to this README

## Notes

- Docker tests are skipped by default (marked with `Skip` attribute)
- Remove the Skip attribute or set environment variables to run them
- GitHub Actions automatically runs them in CI
- Seeding tests require network access and may take several minutes
