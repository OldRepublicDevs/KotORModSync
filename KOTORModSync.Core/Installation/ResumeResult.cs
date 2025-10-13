



using System;
using System.Collections.Generic;

namespace KOTORModSync.Core.Installation
{
	public readonly struct ResumeResult
	{
		public ResumeResult(Guid sessionId, IReadOnlyList<ModComponent> orderedComponents)
		{
			SessionId = sessionId;
			OrderedComponents = orderedComponents;
		}

		public Guid SessionId { get; }
		public IReadOnlyList<ModComponent> OrderedComponents { get; }
	}
}

