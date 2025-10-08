// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

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

