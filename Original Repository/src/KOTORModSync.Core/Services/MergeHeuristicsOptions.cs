// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using JetBrains.Annotations;

namespace KOTORModSync.Core.Services
{



    public sealed class MergeHeuristicsOptions
    {
        public bool UseNameExact { get; set; } = true;
        public bool UseAuthorExact { get; set; } = true;
        public bool UseNameSimilarity { get; set; } = true;
        public bool UseAuthorSimilarity { get; set; }
        public bool MatchByDomainIfNoNameAuthorMatch { get; set; } = true;
        public bool IgnoreCase { get; set; } = true;
        public bool IgnorePunctuation { get; set; } = true;
        public bool TrimWhitespace { get; set; } = true;
        public double MinNameSimilarity { get; set; } = 0.85;
        public bool ValidateExistingLinksBeforeReplace { get; set; } = true;
        public bool SkipBlankUpdates { get; set; } = true;

        [NotNull]
        public static MergeHeuristicsOptions CreateDefault() => new MergeHeuristicsOptions();
    }
}
