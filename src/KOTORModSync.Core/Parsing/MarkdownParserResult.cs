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
        public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>(System.StringComparer.Ordinal);

        public string PreambleContent { get; set; } = string.Empty;

        public string EpilogueContent { get; set; } = string.Empty;

        public string WidescreenWarningContent { get; set; } = string.Empty;

        public string AspyrExclusiveWarningContent { get; set; } = string.Empty;

        public string InstallationWarningContent { get; set; } = string.Empty;

        /// <summary>
        /// Trace information showing exactly what MarkdownParser matched, where, and with which patterns
        /// </summary>
        public ParsingTraceInfo Trace { get; set; } = new ParsingTraceInfo();
    }
}
