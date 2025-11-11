using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using KOTORModSync.Core;
using KOTORModSync.Core.Services.FileSystem;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class CleanListTests
    {
        private string _testRootDir = string.Empty;
        private string _workDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _testRootDir = Path.Combine(Path.GetTempPath(), "KOTORModSync_CleanList_Tests_" + Guid.NewGuid().ToString("N"));
            _workDir = Path.Combine(_testRootDir, "Work");
            _ = Directory.CreateDirectory(_workDir);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_testRootDir))
                {
                    Directory.Delete(_testRootDir, recursive: true);
                }
            }
            catch
            {
                // ignore on CI
            }
        }

        [Test]
        public async Task Test_CleanList_DeletesExpectedFiles_BasedOnSelectedMods()
        {
            string overrideDir = Path.Combine(_workDir, "Copy contents to KotOR's Override folder");
            Directory.CreateDirectory(overrideDir);

            // Seed a subset of files from the CSV into the target directory
            string[] filesToSeed = new[]
            {
                "P_BastilaH04.tpc", // Mandatory Deletions
				"C_DrdAstro01.tpc", "C_DrdAstro02.tpc", // HD Astromechs by Dark Hope
				"C_DrdProt01.tpc", // HD Protocol Droids by Dark Hope
				"L_Alien02.mdl", // K1CP
				"N_Tusken02.tpc", // HD Realistic Sand People
				"Twilek_F01.tpc", // HD Twi'lek Females by Dark Hope
				"Unrelated_KeepMe.tpc", // should remain
			};
            foreach (string f in filesToSeed)
            {
                await File.WriteAllTextAsync(Path.Combine(overrideDir, f), "dummy");
            }

            // Cleanlist CSV content
            string csv = string.Join(Environment.NewLine, new[]
            {
                "Mandatory Deletions: Click Y,P_BastilaH04.tpc",
                "HD Astromechs by Dark Hope,C_DrdAstro01.tpc,C_DrdAstro02.tpc,C_DrdAstro03.tpc,P_T3M3_01.tpc",
                "HD Protocol Droids by Dark Hope,C_DrdProt01.tpc,C_DrdProt02.tpc,C_DrdProt03.tpc,C_DrdProt04.tpc",
                "KOTOR 1 Community Patch,L_Alien02.mdl,L_Alien02.mdx,L_Alien05.mdl,L_Alien05.mdx",
                "HD Realistic Sand People by Etienne76,N_Tusken_F.tpc,N_Tusken_F2.tpc,N_Tusken02.tpc,N_Tusken03.tpc,N_Tusken04.tpc,N_Tusken05.tpc",
                "HD Twi'lek Females by Dark Hope,Twilek_F01.tpc,Twilek_F02.tpc,Twilek_F03.tpc,Twilek_F04.tpc",
            });
            string cleanlistPath = Path.Combine(_workDir, "cleanlist_k1.txt");
            await File.WriteAllTextAsync(cleanlistPath, csv);

            // Configure MainConfig to point to work directories
            DirectoryInfo originalSourcePath = MainConfig.SourcePath;
            DirectoryInfo originalDestPath = MainConfig.DestinationPath;
            _ = new MainConfig
            {
                sourcePath = new DirectoryInfo(_workDir),
                destinationPath = new DirectoryInfo(_workDir),
            };

            try
            {
                // Build instruction: CleanList on overrideDir using the CSV
                var cleanList = new Instruction
                {
                    Action = Instruction.ActionType.CleanList,
                    Source = new List<string> { cleanlistPath },
                    Destination = overrideDir,
                };

                // Selected components to drive matching
                var selectedComponents = new List<ModComponent>
                {
                    new ModComponent { Name = "HD Astromech Droids", IsSelected = true },
                    new ModComponent { Name = "Protocol Droids HD", IsSelected = true },
                    new ModComponent { Name = "KOTOR 1 Community Patch", IsSelected = true },
					// Intentionally NOT selecting Sand People to verify those stay if not selected
					new ModComponent { Name = "HD Twi'lek Females", IsSelected = true },
                };

                // Use Virtual FS so we don't touch disk state beyond our work dir but still simulate deletions
                var vfs = new VirtualFileSystemProvider();
                await vfs.InitializeFromRealFileSystemAsync(_workDir);

                var component = new ModComponent
                {
                    Name = "Character Textures & Model Fixes",
                    Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>(new[] { cleanList }),
                };

                // Execute the single CleanList instruction
                Instruction.ActionExitCode code = await component.ExecuteSingleInstructionAsync(
                    cleanList,
                    instructionIndex: 1,
                    componentsList: selectedComponents,
                    fileSystemProvider: vfs,
                    skipDependencyCheck: true,
                    cancellationToken: CancellationToken.None
                );

                Assert.That(code, Is.EqualTo(Instruction.ActionExitCode.Success));

                // Assert deletions happened for: Mandatory deletion, Astromechs, Protocol Droids, K1CP, Twi'lek
                string[] shouldBeDeleted =
                {
                    "P_BastilaH04.tpc",
                    "C_DrdAstro01.tpc", "C_DrdAstro02.tpc",
                    "C_DrdProt01.tpc",
                    "L_Alien02.mdl",
                    "Twilek_F01.tpc",
                };
                foreach (string f in shouldBeDeleted)
                {
                    string p = Path.Combine(overrideDir, f);
                    Assert.That(vfs.FileExists(p), Is.False, $"Expected deleted: {f}");
                }

                Assert.Multiple(() =>
                {
                    // Not selected: Sand People line, so keep N_Tusken02.tpc
                    Assert.That(vfs.FileExists(Path.Combine(overrideDir, "N_Tusken02.tpc")), Is.True, "Unexpected deletion of non-selected mod file");
                    // Unrelated file should remain
                    Assert.That(vfs.FileExists(Path.Combine(overrideDir, "Unrelated_KeepMe.tpc")), Is.True, "Unrelated file should remain");
                });
            }
            finally
            {
                // Restore MainConfig
                _ = new MainConfig
                {
                    sourcePath = originalSourcePath,
                    destinationPath = originalDestPath,
                };
            }
        }
    }
}
