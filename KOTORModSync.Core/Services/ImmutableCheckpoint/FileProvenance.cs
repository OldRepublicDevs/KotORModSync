// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace KOTORModSync.Core.Services.ImmutableCheckpoint
{
	public class FileState
	{
		public string Path { get; set; }

		public string Hash { get; set; }

		public string CASHash { get; set; }

		public long Size { get; set; }

		public DateTime LastModified { get; set; }
	}

	public class Checkpoint
	{
		public string Id { get; set; }

		public string SessionId { get; set; }

		public string ComponentName { get; set; }

		public string ComponentGuid { get; set; }

		public int Sequence { get; set; }

		public DateTime Timestamp { get; set; }

		public string PreviousId { get; set; }

		public bool IsAnchor { get; set; }

		public string PreviousAnchorId { get; set; }

		public Dictionary<string, FileState> Files { get; set; } = new Dictionary<string, FileState>( StringComparer.OrdinalIgnoreCase );

		public List<string> Added { get; set; } = new List<string>();

		public List<FileDelta> Modified { get; set; } = new List<FileDelta>();

		public List<string> Deleted { get; set; } = new List<string>();

		public long TotalSize { get; set; }

		public long DeltaSize { get; set; }

		public int FileCount { get; set; }
	}

	public class FileDelta
	{
		public string Path { get; set; }

		public string SourceHash { get; set; }

		public string TargetHash { get; set; }

		public string SourceCASHash { get; set; }

		public string TargetCASHash { get; set; }

		public string ForwardDeltaCASHash { get; set; }

		public string ReverseDeltaCASHash { get; set; }

		public long SourceSize { get; set; }

		public long TargetSize { get; set; }

		public long ForwardDeltaSize { get; set; }

		public long ReverseDeltaSize { get; set; }

		public string Method { get; set; }
	}

	public class CheckpointSession
	{
		public string Id { get; set; }

		public string Name { get; set; }

		public string GamePath { get; set; }

		public DateTime StartTime { get; set; }

		public DateTime? EndTime { get; set; }

		public List<string> CheckpointIds { get; set; } = new List<string>();

		public bool IsComplete { get; set; }

		public int TotalComponents { get; set; }

		public int CompletedComponents { get; set; }
	}
}