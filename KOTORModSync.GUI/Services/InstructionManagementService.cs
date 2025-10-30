// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;

using KOTORModSync.Core;

namespace KOTORModSync.Services
{

	public class InstructionManagementService
	{

		public static void CreateInstruction( ModComponent component, int index )
		{
			if (component is null)
				throw new ArgumentNullException( nameof( component ) );

			try
			{
				component.CreateInstruction( index );
				Logger.LogVerbose( $"Created instruction at index {index} for component '{component.Name}'" );
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error creating instruction" );
			}
		}

		public static void DeleteInstruction( ModComponent component, int index )
		{
			if (component is null)
				throw new ArgumentNullException( nameof( component ) );

			try
			{
				component.DeleteInstruction( index );
				Logger.LogVerbose( $"Deleted instruction at index {index} for component '{component.Name}'" );
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error deleting instruction" );
			}
		}

		public static void MoveInstruction( ModComponent component, Instruction instruction, int newIndex )
		{
			if (component is null)
				throw new ArgumentNullException( nameof( component ) );
			if (instruction is null)
				throw new ArgumentNullException( nameof( instruction ) );

			try
			{
				component.MoveInstructionToIndex( instruction, newIndex );
				Logger.LogVerbose( $"Moved instruction to index {newIndex} for component '{component.Name}'" );
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error moving instruction" );
			}
		}

		public static void CreateOption( ModComponent component, int index )
		{
			if (component is null)
				throw new ArgumentNullException( nameof( component ) );

			try
			{
				component.CreateOption( index );
				Logger.LogVerbose( $"Created option at index {index} for component '{component.Name}'" );
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error creating option" );
			}
		}

		public static void DeleteOption( ModComponent component, int index )
		{
			if (component is null)
				throw new ArgumentNullException( nameof( component ) );

			try
			{
				component.DeleteOption( index );
				Logger.LogVerbose( $"Deleted option at index {index} for component '{component.Name}'" );
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error deleting option" );
			}
		}

		public static void MoveOption( ModComponent component, Option option, int newIndex )
		{
			if (component is null)
				throw new ArgumentNullException( nameof( component ) );
			if (option is null)
				throw new ArgumentNullException( nameof( option ) );

			try
			{
				component.MoveOptionToIndex( option, newIndex );
				Logger.LogVerbose( $"Moved option to index {newIndex} for component '{component.Name}'" );
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error moving option" );
			}
		}
	}
}