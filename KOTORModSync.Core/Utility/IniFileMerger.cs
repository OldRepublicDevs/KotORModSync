// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;

using IniParser.Model;

using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{
	public static class IniFileMerger
	{
		[NotNull]
		public static IniData MergeIniFiles( [NotNull] IniData iniData1, [NotNull] IniData iniData2 )
		{
			if (iniData1 == null)
				throw new ArgumentNullException( nameof( iniData1 ) );
			if (iniData2 == null)
				throw new ArgumentNullException( nameof( iniData2 ) );

			IniData mergedIniData = new IniData();

			foreach (SectionData section in iniData1.Sections)
			{
				_ = mergedIniData.Sections.AddSection( section.SectionName );
				foreach (KeyData key in section.Keys)
				{
					_ = mergedIniData[section.SectionName].AddKey( key.KeyName, key.Value );
				}
			}

			foreach (SectionData section in iniData2.Sections)
			{

				string mergedSectionName = section.SectionName;
				for (int sectionNumber = 1;
					mergedIniData.Sections.ContainsSection( mergedSectionName );
					sectionNumber++)
				{
					mergedSectionName = section.SectionName + sectionNumber;
				}

				_ = mergedIniData.Sections.AddSection( mergedSectionName );
				foreach (KeyData key in section.Keys)
				{
					_ = mergedIniData[mergedSectionName].AddKey( key.KeyName, key.Value );
				}
			}

			return mergedIniData;
		}
	}
}