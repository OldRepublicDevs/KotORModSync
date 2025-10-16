// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using JetBrains.Annotations;

namespace KOTORModSync.Core.Services.Validation
{
	/// <summary>
	/// Represents the result of validating a single path in an instruction.
	/// Used for real-time validation feedback in the editor UI.
	/// </summary>
	public class PathValidationResult
	{
		/// <summary>
		/// A short status message to display (e.g., "✓ Valid", "✗ Missing file")
		/// </summary>
		[CanBeNull]
		public string StatusMessage { get; set; }

		/// <summary>
		/// A detailed explanation of the validation result for tooltips/expandable sections
		/// </summary>
		[CanBeNull]
		public string DetailedMessage { get; set; }

		/// <summary>
		/// Whether the path is valid and can be used
		/// </summary>
		public bool IsValid { get; set; }

		/// <summary>
		/// If the path is invalid because it depends on another instruction,
		/// this contains the zero-based index of that instruction
		/// </summary>
		public int? BlockingInstructionIndex { get; set; }

		/// <summary>
		/// If true, the path is invalid because it references a mod archive
		/// that needs to be added to the ModLinks section
		/// </summary>
		public bool NeedsModLinkAdded { get; set; }
	}
}

