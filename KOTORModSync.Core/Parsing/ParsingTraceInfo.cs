// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.Generic;

namespace KOTORModSync.Core.Parsing
{
	/// <summary>
	/// Tracks what MarkdownParser extracted, where, and with which pattern
	/// </summary>
	public sealed class ParsingTraceInfo
	{
		public List<MatchTrace> Matches { get; } = new List<MatchTrace>();
		public List<SectionTrace> Sections { get; } = new List<SectionTrace>();
		public List<string> DebugMessages { get; } = new List<string>();
	}

	public sealed class MatchTrace
	{
		public int StartIndex { get; set; }
		public int EndIndex { get; set; }
		public string ExtractedText { get; set; }
		public string PatternName { get; set; }
		public string GroupName { get; set; }
		public int ComponentIndex { get; set; }
		public bool WasUsed { get; set; }
	}

	public sealed class SectionTrace
	{
		public int StartIndex { get; set; }
		public int EndIndex { get; set; }
		public int ComponentIndex { get; set; }
		public bool WasSkipped { get; set; }
		public string SkipReason { get; set; }
		public bool ResultedInComponent { get; set; }
	}
}

