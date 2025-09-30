using System.Collections.Generic;

namespace KOTORModSync.Core.Parsing
{
	public sealed class MarkdownParserResult
	{
		public IList<Component> Components { get; set; } = new List<Component>();
		public IList<string> Warnings { get; set; } = new List<string>();
		public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
	}
}

