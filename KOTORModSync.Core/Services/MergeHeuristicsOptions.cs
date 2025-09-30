using System;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Services
{
	/// <summary>
	///     Configurable options that control how parsed markdown components are matched and merged
	///     into an existing TOML components list.
	/// </summary>
	public sealed class MergeHeuristicsOptions
	{
		// Identity matching toggles
		public bool UseNameExact { get; set; } = true;
		public bool UseAuthorExact { get; set; } = true;
		public bool UseNameSimilarity { get; set; } = true;
		public bool UseAuthorSimilarity { get; set; } = false;
		public bool MatchByDomainIfNoNameAuthorMatch { get; set; } = true;

		// Normalization options
		public bool IgnoreCase { get; set; } = true;
		public bool IgnorePunctuation { get; set; } = true;
		public bool TrimWhitespace { get; set; } = true;

		// Similarity thresholds (0..1)
		public double MinNameSimilarity { get; set; } = 0.85; // fairly strict
		public double MinAuthorSimilarity { get; set; } = 0.80;

		// Link handling
		public bool ValidateExistingLinksBeforeReplace { get; set; } = true;
		public int LinkValidationTimeoutMs { get; set; } = 3000;

		// Update rules
		public bool AddNewWhenNoMatchFound { get; set; } = true;
		public bool SkipBlankUpdates { get; set; } = true;

		[NotNull]
		public static MergeHeuristicsOptions CreateDefault() => new MergeHeuristicsOptions();
	}
}


