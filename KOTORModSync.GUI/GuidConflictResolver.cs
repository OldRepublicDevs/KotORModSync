// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.


using System;
using KOTORModSync.Core;

namespace KOTORModSync
{

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


		public static GuidResolution ResolveGuidConflict(ModComponent existing, ModComponent incoming)
		{

			if ( existing.Guid == incoming.Guid )
				return null;

			var resolution = new GuidResolution
			{
				ExistingComponent = existing,
				IncomingComponent = incoming
			};

			bool existingHasGuidUsage = HasIntricateGuidUsage(existing);

			bool incomingHasGuidUsage = HasIntricateGuidUsage(incoming);

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

				resolution.ChosenGuid = existing.Guid;
				resolution.RejectedGuid = incoming.Guid;
				return resolution;
			}

			if ( existingHasGuidUsage )
			{
				resolution.ChosenGuid = existing.Guid;
				resolution.RejectedGuid = incoming.Guid;
				resolution.RequiresManualResolution = false;
				resolution.ConflictReason = "Automatically chose existing GUID (has dependencies)";
				return resolution;
			}

			if ( incomingHasGuidUsage )
			{
				resolution.ChosenGuid = incoming.Guid;
				resolution.RejectedGuid = existing.Guid;
				resolution.RequiresManualResolution = false;
				resolution.ConflictReason = "Automatically chose incoming GUID (has dependencies)";
				return resolution;
			}

			resolution.ChosenGuid = existing.Guid;
			resolution.RejectedGuid = incoming.Guid;
			resolution.RequiresManualResolution = false;
			resolution.ConflictReason = "Automatically chose existing GUID (neither has dependencies)";
			return resolution;
		}

		private static bool HasIntricateGuidUsage(ModComponent component)
		{

			if ( component.Dependencies.Count > 0 )
				return true;
			if ( component.Restrictions.Count > 0 )
				return true;
			if ( component.InstallAfter.Count > 0 )
				return true;

			if ( component.Options.Count > 0 )
				return true;

			return false;
		}

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

