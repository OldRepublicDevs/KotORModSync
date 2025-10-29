// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using JetBrains.Annotations;

namespace KOTORModSync.Core.Services
{

	public class ComponentManagerService
	{



		public static async Task<bool> FindDuplicateComponentsAsync( [NotNull][ItemNotNull] List<ModComponent> components, bool promptUser = true )
		{
			if (components == null)
				throw new ArgumentNullException( nameof( components ) );

			bool duplicatesFixed = true;
			bool continuePrompting = promptUser;

			foreach (ModComponent component in components)
			{
				ModComponent duplicateComponent = components.Find( c => c.Guid == component.Guid && c != component );

				if (duplicateComponent is null)
					continue;

				if (!Guid.TryParse( duplicateComponent.Guid.ToString(), out Guid _ ))
				{
					await Logger.LogWarningAsync(
						$"Invalid GUID for component '{component.Name}'. Got '{component.Guid}'"






					).ConfigureAwait( false );

					if (MainConfig.AttemptFixes)
					{
						await Logger.LogVerboseAsync( "Fixing the above issue automatically..." ).ConfigureAwait( false );
						duplicateComponent.Guid = Guid.NewGuid();
					}
				}

				string message = $"ModComponent '{component.Name}' has a duplicate GUID with component '{duplicateComponent.Name}'";
				await Logger.LogAsync( message ).ConfigureAwait( false );

				bool? confirm = true;
				if (continuePrompting)
				{

					if (MainConfig.AttemptFixes)
					{
						confirm = true;
					}
					else
					{

						return false;
					}
				}

				switch (confirm)
				{
					case true:
						duplicateComponent.Guid = Guid.NewGuid();


						await Logger.LogAsync( $"Replaced GUID of component '{duplicateComponent.Name}'" )

.ConfigureAwait( false );
						break;
					case false:
						await Logger.LogVerboseAsync( $"User canceled GUID replacement for component '{duplicateComponent.Name}'" ).ConfigureAwait( false );
						duplicatesFixed = false;
						break;
					case null:
						continuePrompting = false;
						break;
				}
			}

			return duplicatesFixed;
		}



		public static async Task<bool> ValidateComponentsForInstallationAsync( [NotNull][ItemNotNull] List<ModComponent> components )
		{
			if (components == null)
				throw new ArgumentNullException( nameof( components ) );



			await Logger.LogAsync( "Validating individual components, this might take a while..." ).ConfigureAwait( false );
			bool individuallyValidated = true;

			foreach (ModComponent component in components)
			{
				if (!component.IsSelected)
					continue;

				if (component.Restrictions.Count > 0 && component.IsSelected)
				{
					List<ModComponent> restrictedComponentsList = ModComponent.FindComponentsFromGuidList(
						component.Restrictions,
						components
					);
					foreach (ModComponent restrictedComponent in restrictedComponentsList)
					{
						if (restrictedComponent?.IsSelected == true)


						{
							await Logger.LogErrorAsync( $"Cannot install '{component.Name}' due to '{restrictedComponent.Name}' being selected for install." ).ConfigureAwait( false );
							individuallyValidated = false;
						}
					}
				}

				if (component.Dependencies.Count > 0 && component.IsSelected)
				{
					List<ModComponent> dependencyComponentsList = ModComponent.FindComponentsFromGuidList( component.Dependencies, components );
					foreach (ModComponent dependencyComponent in dependencyComponentsList)
					{
						if (dependencyComponent?.IsSelected != true)


						{
							await Logger.LogErrorAsync( $"Cannot install '{component.Name}' due to '{dependencyComponent?.Name}' not being selected for install." ).ConfigureAwait( false );
							individuallyValidated = false;
						}
					}
				}

				ComponentValidation validator = new ComponentValidation( component, components );


				await Logger.LogVerboseAsync( $" == Validating '{component.Name}' == " )

.ConfigureAwait( false );
				individuallyValidated &= validator.Run();
			}

			await Logger.LogVerboseAsync( "Finished validating all components." ).ConfigureAwait( false );
			return individuallyValidated;
		}


		public static ModComponent CreateNewComponent() => new ModComponent
		{
			Guid = Guid.NewGuid(),
			Name = "new mod_" + Path.GetFileNameWithoutExtension( Path.GetRandomFileName() ),
		};


		public static bool CanRemoveComponent( [NotNull] ModComponent component, [NotNull][ItemNotNull] List<ModComponent> components )
		{
			if (component == null)
				throw new ArgumentNullException( nameof( component ) );
			if (components == null)
				throw new ArgumentNullException( nameof( components ) );

			return !components.Exists( c => c.Dependencies.Exists( g => g == component.Guid ) );
		}


		public static void MoveComponent( [NotNull] ModComponent component, [NotNull][ItemNotNull] List<ModComponent> components, int relativeIndex )
		{
			if (component == null)
				throw new ArgumentNullException( nameof( component ) );
			if (components == null)
				throw new ArgumentNullException( nameof( components ) );

			int index = components.IndexOf( component );
			if (component is null
				|| (index == 0 && relativeIndex < 0)
				|| index == -1
				|| index + relativeIndex == components.Count)
			{
				return;
			}

			_ = components.Remove( component );
			components.Insert( index + relativeIndex, component );
		}
	}
}