// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using KOTORModSync.Core;

namespace KOTORModSync.Tests.TestHelpers
{
	internal static class TestComponentFactory
	{
		public static ModComponent CreateComponent(string name, DirectoryInfo workingDirectory)
		{
			if ( workingDirectory is null )
				throw new ArgumentNullException(nameof(workingDirectory));

			string fakeArchivePath = Path.Combine(workingDirectory.FullName, name + ".zip");
			CreateMinimalZip(fakeArchivePath);

			string extractDestination = Path.Combine(workingDirectory.FullName, "extracted", name);
			_ = Directory.CreateDirectory(extractDestination);

			Instruction extractInstruction = new()
			{
				Action = Instruction.ActionType.Extract,
				Source = [fakeArchivePath],
				Destination = extractDestination,
			};

			return new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = name,
				IsSelected = true,
				Instructions = [extractInstruction],
			};
		}

		private static void CreateMinimalZip(string path)
		{
			_ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			byte[] emptyZip = [
				0x50, 0x4B, 0x05, 0x06,
				0x00, 0x00, 0x00, 0x00,
				0x00, 0x00, 0x00, 0x00,
				0x00, 0x00, 0x00, 0x00,
				0x00, 0x00, 0x00, 0x00,
				0x00, 0x00
			];
			File.WriteAllBytes(path, emptyZip);
		}
	}
}
