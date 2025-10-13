// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace KOTORModSync.Core.Installation
{
	[JsonObject(MemberSerialization.OptIn)]
	public sealed class InstallSessionState
	{
		[JsonProperty]
		public string Version { get; set; } = "2.0";

		[JsonProperty]
		public Guid SessionId { get; set; } = Guid.Empty;

		[JsonProperty]
		public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

		[JsonProperty]
		public string DestinationPath { get; set; } = string.Empty;

		[JsonProperty]
		public List<Guid> ComponentOrder { get; set; } = new List<Guid>();

		[JsonProperty]
		public Dictionary<Guid, ComponentSessionEntry> Components { get; set; } = new Dictionary<Guid, ComponentSessionEntry>();

		[JsonProperty]
		public string BackupPath { get; set; } = string.Empty;

		[JsonProperty]
		public int CurrentRevision { get; set; }
	}

	[JsonObject(MemberSerialization.OptIn)]
	public sealed class ComponentSessionEntry
	{
		[JsonProperty]
		public Guid ComponentId { get; set; }

		[JsonProperty]
		public ModComponent.ComponentInstallState State { get; set; } = ModComponent.ComponentInstallState.Pending;

		[JsonProperty]
		public DateTimeOffset? LastStartedUtc { get; set; }

		[JsonProperty]
		public DateTimeOffset? LastCompletedUtc { get; set; }

		[JsonProperty]
		public List<ModComponent.InstructionCheckpoint> Instructions { get; set; } = new List<ModComponent.InstructionCheckpoint>();
	}
}

