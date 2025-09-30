// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using KOTORModSync.Core;
using Newtonsoft.Json;

namespace KOTORModSync.Core.Installation
{
	[JsonObject(MemberSerialization.OptIn)]
	public sealed class InstallSessionState
	{
		[JsonProperty]
		public string Version { get; set; } = "1.0";

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
		public Component.ComponentInstallState State { get; set; } = Component.ComponentInstallState.Pending;

		[JsonProperty]
		public DateTimeOffset? LastStartedUtc { get; set; }

		[JsonProperty]
		public DateTimeOffset? LastCompletedUtc { get; set; }

		[JsonProperty]
		public List<Component.InstructionCheckpoint> Instructions { get; set; } = new List<Component.InstructionCheckpoint>();
	}
}

