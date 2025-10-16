// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.Generic;

namespace KOTORModSync.Core.Parsing
{
	public sealed class MarkdownParserResult
	{
		public IList<ModComponent> Components { get; set; } = new List<ModComponent>();
		public IList<string> Warnings { get; set; } = new List<string>();
		public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

		public string BeforeModListContent { get; set; } = string.Empty;

		public string AfterModListContent { get; set; } = string.Empty;

		public string WidescreenSectionContent { get; set; } = string.Empty;

		public string AspyrSectionContent { get; set; } = string.Empty;
	}
}

