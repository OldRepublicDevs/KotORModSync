// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using KOTORModSync.Core;

namespace KOTORModSync
{
	/// <summary>
	/// Intelligently resolves GUID conflicts during component merging.
	/// Automatically chooses the GUID that preserves dependency integrity.
	/// </summary>
	public static class GuidConflictResolver
	{
		public class GuidResolution
		{
			public Guid ChosenGuid { get; set; }
			public Guid RejectedGuid { get; set; }
			public bool RequiresManualResolution { get; set; }
			public string ConflictReason { get; set; }
			public ModComponent ExistingComponent { get; set; }
			public ModComponent IncomingComponent { get; set; }
		}

		/// <summary>
		/// Determines which GUID to use when merging two matched components.
		/// Returns null if GUIDs are the same (no conflict).
		/// </summary>
		public static GuidResolution ResolveGuidConflict(ModComponent existing, ModComponent incoming)
		{
			// No conflict if GUIDs match
			if ( existing.Guid == incoming.Guid )
				return null;

			var resolution = new GuidResolution
			{
				ExistingComponent = existing,
				IncomingComponent = incoming
			};

			// Check if existing component has intricate GUID usage
			bool existingHasGuidUsage = HasIntricateGuidUsage(existing);
			// Check if incoming component has intricate GUID usage
			bool incomingHasGuidUsage = HasIntricateGuidUsage(incoming);

			// Case 1: Both have intricate usage - MANUAL RESOLUTION REQUIRED
			if ( existingHasGuidUsage && incomingHasGuidUsage )
			{
				resolution.RequiresManualResolution = true;
				resolution.ConflictReason = $"⚠️ GUID CONFLICT REQUIRES MANUAL RESOLUTION\n\n" +
					$"Both components have dependencies/restrictions that reference their GUIDs:\n\n" +
					$"EXISTING: {existing.Name}\n" +
					$"  GUID: {existing.Guid}\n" +
					$"  Dependencies: {existing.Dependencies.Count}\n" +
					$"  Restrictions: {existing.Restrictions.Count}\n" +
					$"  InstallAfter: {existing.InstallAfter.Count}\n" +
					$"  Options: {existing.Options.Count}\n\n" +
					$"INCOMING: {incoming.Name}\n" +
					$"  GUID: {incoming.Guid}\n" +
					$"  Dependencies: {incoming.Dependencies.Count}\n" +
					$"  Restrictions: {incoming.Restrictions.Count}\n" +
					$"  InstallAfter: {incoming.InstallAfter.Count}\n" +
					$"  Options: {incoming.Options.Count}\n\n" +
					$"💡 Right-click this component to choose which GUID to use.\n" +
					$"⚠️ Choosing incorrectly may break dependencies!";

				// Default to existing for safety
				resolution.ChosenGuid = existing.Guid;
				resolution.RejectedGuid = incoming.Guid;
				return resolution;
			}

			// Case 2: Only existing has intricate usage - USE EXISTING GUID
			if ( existingHasGuidUsage )
			{
				resolution.ChosenGuid = existing.Guid;
				resolution.RejectedGuid = incoming.Guid;
				resolution.RequiresManualResolution = false;
				resolution.ConflictReason = "Automatically chose existing GUID (has dependencies)";
				return resolution;
			}

			// Case 3: Only incoming has intricate usage - USE INCOMING GUID
			if ( incomingHasGuidUsage )
			{
				resolution.ChosenGuid = incoming.Guid;
				resolution.RejectedGuid = existing.Guid;
				resolution.RequiresManualResolution = false;
				resolution.ConflictReason = "Automatically chose incoming GUID (has dependencies)";
				return resolution;
			}

			// Case 4: Neither has intricate usage - USE EXISTING GUID (preserve stability)
			resolution.ChosenGuid = existing.Guid;
			resolution.RejectedGuid = incoming.Guid;
			resolution.RequiresManualResolution = false;
			resolution.ConflictReason = "Automatically chose existing GUID (neither has dependencies)";
			return resolution;
		}

		/// <summary>
		/// Checks if a component has intricate GUID usage (dependencies, restrictions, options, etc.)
		/// </summary>
		private static bool HasIntricateGuidUsage(ModComponent component)
		{
			// Check if component has any dependencies, restrictions, or install order requirements
			if ( component.Dependencies.Count > 0 )
				return true;
			if ( component.Restrictions.Count > 0 )
				return true;
			if ( component.InstallAfter.Count > 0 )
				return true;

			// Check if component has options (which have their own GUIDs that might be referenced)
			if ( component.Options.Count > 0 )
				return true;

			return false;
		}

		/// <summary>
		/// Checks if any other component in the list references this GUID
		/// </summary>
		public static bool IsGuidReferencedByOthers(Guid guid, System.Collections.Generic.List<ModComponent> allComponents)
		{
			foreach ( ModComponent comp in allComponents )
			{
				if ( comp.Dependencies.Contains(guid) )
					return true;
				if ( comp.Restrictions.Contains(guid) )
					return true;
				if ( comp.InstallAfter.Contains(guid) )
					return true;
			}
			return false;
		}
	}
}

