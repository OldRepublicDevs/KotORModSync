using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using KOTORModSync.Core.Parsing;

namespace KOTORModSync.Core
{
	public static class ModParser
	{
		private static readonly MarkdownImportProfile s_defaultProfile = MarkdownImportProfile.CreateDefault();

		[NotNull]
		public static MarkdownParserResult Parse([NotNull] string markdown)
		{
			var parser = new MarkdownParser(s_defaultProfile);
			return parser.Parse(markdown);
		}

		[NotNull]
		public static MarkdownParser CreateParser([NotNull] MarkdownImportProfile profile) => new MarkdownParser(profile);

		[NotNull]
		public static MarkdownParser CreateParser([NotNull] MarkdownImportProfile profile, [CanBeNull] Action<string> logInfo, [CanBeNull] Action<string> logVerbose)
			=> new MarkdownParser(profile, logInfo, logVerbose);
	}
}

