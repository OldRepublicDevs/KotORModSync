// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using KOTORModSync.Core;

namespace KOTORModSync.Services
{
	/// <summary>
	/// Service responsible for managing instructions and options (CRUD operations)
	/// </summary>
	public class InstructionManagementService
	{
		/// <summary>
		/// Creates a new instruction at the specified index
		/// </summary>
		public static void CreateInstruction(ModComponent component, int index)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));

			try
			{
				component.CreateInstruction(index);
				Logger.LogVerbose($"Created instruction at index {index} for component '{component.Name}'");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error creating instruction");
			}
		}

		/// <summary>
		/// Deletes an instruction at the specified index
		/// </summary>
		public static void DeleteInstruction(ModComponent component, int index)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));

			try
			{
				component.DeleteInstruction(index);
				Logger.LogVerbose($"Deleted instruction at index {index} for component '{component.Name}'");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error deleting instruction");
			}
		}

		/// <summary>
		/// Moves an instruction to a new index
		/// </summary>
		public static void MoveInstruction(ModComponent component, Instruction instruction, int newIndex)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));
			if ( instruction == null )
				throw new ArgumentNullException(nameof(instruction));

			try
			{
				component.MoveInstructionToIndex(instruction, newIndex);
				Logger.LogVerbose($"Moved instruction to index {newIndex} for component '{component.Name}'");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error moving instruction");
			}
		}

		/// <summary>
		/// Creates a new option at the specified index
		/// </summary>
		public static void CreateOption(ModComponent component, int index)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));

			try
			{
				component.CreateOption(index);
				Logger.LogVerbose($"Created option at index {index} for component '{component.Name}'");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error creating option");
			}
		}

		/// <summary>
		/// Deletes an option at the specified index
		/// </summary>
		public static void DeleteOption(ModComponent component, int index)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));

			try
			{
				component.DeleteOption(index);
				Logger.LogVerbose($"Deleted option at index {index} for component '{component.Name}'");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error deleting option");
			}
		}

		/// <summary>
		/// Moves an option to a new index
		/// </summary>
		public static void MoveOption(ModComponent component, Option option, int newIndex)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));
			if ( option == null )
				throw new ArgumentNullException(nameof(option));

			try
			{
				component.MoveOptionToIndex(option, newIndex);
				Logger.LogVerbose($"Moved option to index {newIndex} for component '{component.Name}'");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error moving option");
			}
		}
	}
}

