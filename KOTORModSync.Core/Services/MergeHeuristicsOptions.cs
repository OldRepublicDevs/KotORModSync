


using JetBrains.Annotations;

namespace KOTORModSync.Core.Services
{
	
	
	
	
	public sealed class MergeHeuristicsOptions
	{
		
		public bool UseNameExact { get; set; } = true;
		public bool UseAuthorExact { get; set; } = true;
		public bool UseNameSimilarity { get; set; } = true;
		public bool UseAuthorSimilarity { get; set; } = false;
		public bool MatchByDomainIfNoNameAuthorMatch { get; set; } = true;

		
		public bool IgnoreCase { get; set; } = true;
		public bool IgnorePunctuation { get; set; } = true;
		public bool TrimWhitespace { get; set; } = true;

		
		public double MinNameSimilarity { get; set; } = 0.85; 
		public double MinAuthorSimilarity { get; set; } = 0.80;

		
		public bool ValidateExistingLinksBeforeReplace { get; set; } = true;
		public int LinkValidationTimeoutMs { get; set; } = 3000;

		
		public bool AddNewWhenNoMatchFound { get; set; } = true;
		public bool SkipBlankUpdates { get; set; } = true;

		[NotNull]
		public static MergeHeuristicsOptions CreateDefault() => new MergeHeuristicsOptions();
	}
}


