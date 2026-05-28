// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace KOTORModSync.Core.Utility
{
    public static class PathFixer
    {
        public static int FixDuplicateFolderPathsInComponent(ModComponent component)
        {
            if (component is null)
            {
                return 0;
            }

            int fixCount = 0;

            fixCount += FixInstructionPathsList(component.Instructions);

            foreach (Option option in component.Options)
            {
                fixCount += FixInstructionPathsList(option.Instructions);
            }

            return fixCount;
        }

        public static int FixInstructionPathsList(System.Collections.ObjectModel.ObservableCollection<Instruction> instructions)
        {
            int fixCount = 0;

            foreach (Instruction instruction in instructions)
            {
                if (instruction.Source.Count == 0)
                {
                    continue;
                }

                for (int i = 0; i < instruction.Source.Count; i++)
                {
                    string sourcePath = instruction.Source[i];
                    if (string.IsNullOrWhiteSpace(sourcePath))
                    {
                        continue;
                    }

                    string fixedPath = TryFixDuplicateFolderPath(sourcePath);
                    if (
                        !string.IsNullOrEmpty(fixedPath)
                        && !string.Equals(fixedPath, sourcePath, StringComparison.OrdinalIgnoreCase))
                    {
                        var newSource = new List<string>(instruction.Source);
                        newSource[i] = fixedPath;
                        instruction.Source = newSource;
                        fixCount++;
                    }
                }

                if (!string.IsNullOrWhiteSpace(instruction.Destination))
                {
                    string fixedDest = TryFixDuplicateFolderPath(instruction.Destination);
                    if (!string.IsNullOrEmpty(fixedDest) && !string.Equals(fixedDest, instruction.Destination, StringComparison.OrdinalIgnoreCase))
                    {
                        instruction.Destination = fixedDest;
                        fixCount++;
                    }
                }
            }

            return fixCount;
        }

        public static string TryFixDuplicateFolderPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                string resolvedPath = UtilityHelper.ReplaceCustomVariables(path);

                if (File.Exists(resolvedPath) || Directory.Exists(resolvedPath))
                {
                    return null;
                }

                string[] pathParts = path.Replace('/', Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar);
                if (pathParts.Length < 3)
                {
                    return null;
                }

                for (int i = 0; i < pathParts.Length - 1; i++)
                {
                    if (string.Equals(pathParts[i], pathParts[i + 1], StringComparison.OrdinalIgnoreCase))
                    {
                        var fixedParts = new List<string>();
                        for (int j = 0; j < pathParts.Length; j++)
                        {
                            if (j == i + 1)
                            {
                                continue;
                            }

                            fixedParts.Add(pathParts[j]);
                        }

                        string fixedPath = string.Join(Path.DirectorySeparatorChar.ToString(), fixedParts);

                        return fixedPath;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogVerbose($"[PathFixer] Error checking duplicate folder path '{path}': {ex.Message}");
            }

            return null;
        }
    }
}
