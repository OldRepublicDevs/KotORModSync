// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace KOTORModSync.Core.Services.ImmutableCheckpoint
{
	/// <summary>
	/// Represents the state of a single file at a checkpoint.
	/// Similar to git's blob tracking but for binary game files.
	/// </summary>
	public class FileState
	{
		/// <summary>
		/// Relative path from game directory root
		/// </summary>
		public string Path { get; set; }

		/// <summary>
		/// SHA256 hash of file content
		/// </summary>
		public string Hash { get; set; }

		/// <summary>
		/// CAS (Content-Addressable Storage) hash reference.
		/// This is the hash used to store/retrieve the file content from CAS.
		/// For deduplication: identical files share the same CAS hash.
		/// </summary>
		public string CASHash { get; set; }

		/// <summary>
		/// File size in bytes
		/// </summary>
		public long Size { get; set; }

		/// <summary>
		/// Last modified timestamp of the actual file
		/// </summary>
		public DateTime LastModified { get; set; }
	}

	/// <summary>
	/// Represents an immutable checkpoint - a snapshot of the game directory.
	/// Each checkpoint stores only the deltas from the previous checkpoint.
	/// </summary>
	public class Checkpoint
	{
		/// <summary>
		/// Unique checkpoint ID
		/// </summary>
		public string Id { get; set; }

		/// <summary>
		/// Parent session ID
		/// </summary>
		public string SessionId { get; set; }

		/// <summary>
		/// Mod component name (for user display)
		/// </summary>
		public string ComponentName { get; set; }

		/// <summary>
		/// Mod component GUID
		/// </summary>
		public string ComponentGuid { get; set; }

		/// <summary>
		/// Sequence number in installation order
		/// </summary>
		public int Sequence { get; set; }

		/// <summary>
		/// Creation timestamp
		/// </summary>
		public DateTime Timestamp { get; set; }

		/// <summary>
		/// Previous checkpoint ID (for delta chain)
		/// Null for first checkpoint (baseline)
		/// </summary>
		public string PreviousId { get; set; }

		/// <summary>
		/// Whether this is an anchor checkpoint (every 10th).
		/// Anchors store deltas from the previous anchor and full file states for faster navigation.
		/// </summary>
		public bool IsAnchor { get; set; }

		/// <summary>
		/// ID of the previous anchor checkpoint (for anchor-to-anchor deltas).
		/// Null for first anchor.
		/// </summary>
		public string PreviousAnchorId { get; set; }

		/// <summary>
		/// Complete file manifest at this checkpoint.
		/// Key: relative path, Value: file state.
		/// For anchors: contains full file states.
		/// For regular checkpoints: only contains changed files.
		/// </summary>
		public Dictionary<string, FileState> Files { get; set; } = new Dictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Files added in this checkpoint
		/// </summary>
		public List<string> Added { get; set; } = new List<string>();

		/// <summary>
		/// Files modified in this checkpoint (with delta information)
		/// </summary>
		public List<FileDelta> Modified { get; set; } = new List<FileDelta>();

		/// <summary>
		/// Files deleted in this checkpoint
		/// </summary>
		public List<string> Deleted { get; set; } = new List<string>();

		/// <summary>
		/// Total size of all files at this checkpoint
		/// </summary>
		public long TotalSize { get; set; }

		/// <summary>
		/// Size of delta data stored for this checkpoint
		/// Much smaller than TotalSize for incremental checkpoints
		/// </summary>
		public long DeltaSize { get; set; }

		/// <summary>
		/// Number of files at this checkpoint
		/// </summary>
		public int FileCount { get; set; }
	}

	/// <summary>
	/// Binary delta information for a modified file.
	/// Stores only the changed bytes between versions using bidirectional deltas.
	/// </summary>
	public class FileDelta
	{
		/// <summary>
		/// Relative file path
		/// </summary>
		public string Path { get; set; }

		/// <summary>
		/// Hash of source (old) version
		/// </summary>
		public string SourceHash { get; set; }

		/// <summary>
		/// Hash of target (new) version
		/// </summary>
		public string TargetHash { get; set; }

		/// <summary>
		/// CAS hash of source file content
		/// </summary>
		public string SourceCASHash { get; set; }

		/// <summary>
		/// CAS hash of target file content
		/// </summary>
		public string TargetCASHash { get; set; }

		/// <summary>
		/// CAS hash of forward delta (source -> target)
		/// </summary>
		public string ForwardDeltaCASHash { get; set; }

		/// <summary>
		/// CAS hash of reverse delta (target -> source)
		/// </summary>
		public string ReverseDeltaCASHash { get; set; }

		/// <summary>
		/// Source file size
		/// </summary>
		public long SourceSize { get; set; }

		/// <summary>
		/// Target file size
		/// </summary>
		public long TargetSize { get; set; }

		/// <summary>
		/// Forward delta file size (actual storage used)
		/// </summary>
		public long ForwardDeltaSize { get; set; }

		/// <summary>
		/// Reverse delta file size (actual storage used)
		/// </summary>
		public long ReverseDeltaSize { get; set; }

		/// <summary>
		/// Compression method: "octodiff" or "full_copy"
		/// </summary>
		public string Method { get; set; }
	}

	/// <summary>
	/// Installation session containing multiple checkpoints.
	/// </summary>
	public class CheckpointSession
	{
		/// <summary>
		/// Unique session ID
		/// </summary>
		public string Id { get; set; }

		/// <summary>
		/// Session name (e.g., "Installation_2025-01-13_14-30-00")
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Game directory path
		/// </summary>
		public string GamePath { get; set; }

		/// <summary>
		/// Session start time
		/// </summary>
		public DateTime StartTime { get; set; }

		/// <summary>
		/// Session end time (null if in progress)
		/// </summary>
		public DateTime? EndTime { get; set; }

		/// <summary>
		/// Ordered list of checkpoint IDs
		/// </summary>
		public List<string> CheckpointIds { get; set; } = new List<string>();

		/// <summary>
		/// Whether session completed successfully
		/// </summary>
		public bool IsComplete { get; set; }

		/// <summary>
		/// Total components to install
		/// </summary>
		public int TotalComponents { get; set; }

		/// <summary>
		/// Components successfully installed
		/// </summary>
		public int CompletedComponents { get; set; }
	}
}
