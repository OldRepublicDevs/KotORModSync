// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using JetBrains.Annotations;

using KOTORModSync.Core;
using KOTORModSync.Core.Utility;
using KOTORModSync.Models;

using SharpCompress.Archives;

namespace KOTORModSync.Services
{
    /// <summary>
    /// Service for enumerating files in archives and building file tree structures.
    /// </summary>
    public class ArchiveEnumerationService
    {
        /// <summary>
        /// Builds a file tree from the component's ResourceRegistry files.
        /// </summary>
        public async Task<ObservableCollection<FileTreeNode>> BuildFileTreeFromComponentAsync([NotNull] ModComponent component)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            var rootNodes = new ObservableCollection<FileTreeNode>();

            if (component.ResourceRegistry == null || component.ResourceRegistry.Count == 0)
            {
                return rootNodes;
            }

            string modDirectory = MainConfig.SourcePath?.FullName;
            if (string.IsNullOrEmpty(modDirectory))
            {
                return rootNodes;
            }

            foreach (KeyValuePair<string, ResourceMetadata> resource in component.ResourceRegistry)
            {
                if (resource.Value?.Files == null || resource.Value.Files.Count == 0)
                {
                    continue;
                }

                foreach (string fileName in resource.Value.Files.Keys)
                {
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        continue;
                    }

                    string filePath = Path.Combine(modDirectory, fileName);
                    if (!File.Exists(filePath))
                    {
                        continue;
                    }

                    // Check if it's an archive
                    if (ArchiveHelper.IsArchive(filePath))
                    {
                        await AddArchiveNodeAsync(rootNodes, filePath, fileName);
                    }
                    else
                    {
                        // Add as a regular file
                        AddFileNode(rootNodes, filePath, fileName);
                    }
                }
            }

            return rootNodes;
        }

        private async Task AddArchiveNodeAsync(ObservableCollection<FileTreeNode> rootNodes, string filePath, string fileName)
        {
            var archiveNode = new FileTreeNode
            {
                Name = fileName,
                Path = filePath,
                IsArchive = true,
                IsDirectory = false,
                IsExpanded = false,
            };

            try
            {
                // Enumerate archive contents
                List<FileTreeNode> archiveContents = await Task.Run(() => EnumerateArchiveContents(filePath));

                foreach (FileTreeNode child in archiveContents)
                {
                    child.Parent = archiveNode;
                    child.ArchiveSource = filePath;
                    archiveNode.Children.Add(child);
                }
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, $"Failed to enumerate archive contents: {fileName}");
            }

            rootNodes.Add(archiveNode);
        }

        private void AddFileNode(ObservableCollection<FileTreeNode> rootNodes, string filePath, string fileName)
        {
            var fileNode = new FileTreeNode
            {
                Name = fileName,
                Path = filePath,
                IsArchive = false,
                IsDirectory = false,
            };

            rootNodes.Add(fileNode);
        }

        private List<FileTreeNode> EnumerateArchiveContents(string archivePath)
        {
            var nodes = new List<FileTreeNode>();

            try
            {
                (IArchive archive, FileStream stream) = ArchiveHelper.OpenArchive(archivePath);
                if (archive == null || stream == null)
                {
                    stream?.Dispose();
                    return nodes;
                }

                try
                {
                    // Build a tree structure from flat archive entries
                    Dictionary<string, FileTreeNode> directoryMap = new Dictionary<string, FileTreeNode>(StringComparer.OrdinalIgnoreCase);

                    foreach (IArchiveEntry entry in archive.Entries.OrderBy(e => e.Key, StringComparer.Ordinal))
                    {
                        if (string.IsNullOrWhiteSpace(entry.Key))
                        {
                            continue;
                        }

                        // Normalize path separators
                        string normalizedPath = entry.Key.Replace('\\', '/');
                        string[] pathParts = normalizedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                        if (pathParts.Length == 0)
                        {
                            continue;
                        }

                        FileTreeNode parentNode = null;
                        // Use StringBuilder for currentPath construction
                        var currentPathBuilder = new System.Text.StringBuilder();

                        // Build directory hierarchy
                        for (int i = 0; i < pathParts.Length - 1; i++)
                        {
                            if (i > 0)
                            {
                                currentPathBuilder.Append('/');
                            }
                            currentPathBuilder.Append(pathParts[i]);
                            string currentPath = currentPathBuilder.ToString();

                            if (!directoryMap.ContainsKey(currentPath))
                            {
                                var dirNode = new FileTreeNode
                                {
                                    Name = pathParts[i],
                                    Path = currentPath,
                                    IsDirectory = true,
                                    IsArchive = false,
                                    Parent = parentNode,
                                };

                                directoryMap[currentPath] = dirNode;

                                if (parentNode == null)
                                {
                                    nodes.Add(dirNode);
                                }
                                else
                                {
                                    parentNode.Children.Add(dirNode);
                                }
                            }

                            parentNode = directoryMap[currentPath];
                        }

                        // Add the file or final directory
                        if (entry.IsDirectory)
                        {
                            if (pathParts.Length > 1)
                            {
                                currentPathBuilder.Append('/');
                            }

                            currentPathBuilder.Append(pathParts[pathParts.Length - 1]);
                            string currentPath = currentPathBuilder.ToString();

                            if (!directoryMap.ContainsKey(currentPath))
                            {
                                var dirNode = new FileTreeNode
                                {
                                    Name = pathParts[pathParts.Length - 1],
                                    Path = normalizedPath,
                                    IsDirectory = true,
                                    IsArchive = false,
                                    Parent = parentNode,
                                };

                                directoryMap[currentPath] = dirNode;

                                if (parentNode == null)
                                {
                                    nodes.Add(dirNode);
                                }
                                else
                                {
                                    parentNode.Children.Add(dirNode);
                                }
                            }
                        }
                        else
                        {
                            var fileNode = new FileTreeNode
                            {
                                Name = pathParts[pathParts.Length - 1],
                                Path = normalizedPath,
                                IsDirectory = false,
                                IsArchive = false,
                                Parent = parentNode,
                            };

                            if (parentNode == null)
                            {
                                nodes.Add(fileNode);
                            }
                            else
                            {
                                parentNode.Children.Add(fileNode);
                            }
                        }
                    }
                }
                finally
                {
                    stream.Dispose();
                    archive?.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, $"Error enumerating archive: {archivePath}");
            }

            return nodes;
        }
    }
}

