// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.FileSystemUtils;

namespace KOTORModSync.Core.Services
{
	public static class FileLoadingService
	{
		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> LoadFromFile([NotNull] string filePath)
		{
			if ( filePath == null )
				throw new ArgumentNullException(nameof(filePath));

			if ( !File.Exists(filePath) )
				throw new FileNotFoundException($"File not found: {filePath}");

			if ( MainConfig.CaseInsensitivePathing )
				filePath = PathHelper.GetCaseSensitivePath(filePath, isFile: true).Item1;

			string content = ReadFileWithEncodingFallback(filePath);

			string extension = Path.GetExtension(filePath)?.TrimStart('.').ToLowerInvariant();
			string format = null;

			if ( !string.IsNullOrEmpty(extension) )
			{
				switch ( extension )
				{
					case "md":
						format = "markdown";
						break;
					case "toml":
						format = "toml";
						break;
					case "yaml":
						format = "yaml";
						break;
					case "yml":
						format = "yaml";
						break;
					case "json":
						format = "json";
						break;
					case "xml":
						format = "xml";
						break;
					case "ini":
						format = "ini";
						break;
					default:
						format = null;
						break;
				}
			}

			return ModComponentSerializationService.LoadFromString(content, format);
		}

		[NotNull]
		[ItemNotNull]
		public static async Task<List<ModComponent>> LoadFromFileAsync([NotNull] string filePath)
		{
			if ( filePath == null )
				throw new ArgumentNullException(nameof(filePath));

			if ( !File.Exists(filePath) )
				throw new FileNotFoundException($"File not found: {filePath}");

			if ( MainConfig.CaseInsensitivePathing )
				filePath = PathHelper.GetCaseSensitivePath(filePath, isFile: true).Item1;

			string content = await Task.Run(() => ReadFileWithEncodingFallback(filePath));

			string extension = Path.GetExtension(filePath)?.TrimStart('.').ToLowerInvariant();
			string format = null;

			if ( !string.IsNullOrEmpty(extension) )
			{
				switch ( extension )
				{
					case "md":
					case "markdown":
						format = "markdown";
						break;
					case "toml":
					case "tml":
						format = "toml";
						break;
					case "yaml":
					case "yml":
						format = "yaml";
						break;
					case "json":
						format = "json";
						break;
					case "xml":
						format = "xml";
						break;
					case "ini":
						format = "ini";
						break;
					default:
						format = null;
						break;
				}
			}

			return await ModComponentSerializationService.LoadFromStringAsync(content, format);
		}

		public static void SaveToFile([NotNull] List<ModComponent> components, [NotNull] string filePath)
		{
			if ( components == null )
				throw new ArgumentNullException(nameof(components));
			if ( filePath == null )
				throw new ArgumentNullException(nameof(filePath));

			if ( MainConfig.CaseInsensitivePathing )
				filePath = PathHelper.GetCaseSensitivePath(filePath, isFile: true).Item1;

			string extension = Path.GetExtension(filePath)?.TrimStart('.').ToLowerInvariant() ?? "toml";
			string content = ModComponentSerializationService.SaveToString(components, extension);

			string outputDir = Path.GetDirectoryName(filePath);
			if ( !string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir) )
			{
				Directory.CreateDirectory(outputDir);
			}

			File.WriteAllText(filePath, content);
		}

		public static async Task SaveToFileAsync([NotNull] List<ModComponent> components, [NotNull] string filePath)
		{
			if ( components == null )
				throw new ArgumentNullException(nameof(components));
			if ( filePath == null )
				throw new ArgumentNullException(nameof(filePath));

			if ( MainConfig.CaseInsensitivePathing )
				filePath = PathHelper.GetCaseSensitivePath(filePath, isFile: true).Item1;

			string extension = Path.GetExtension(filePath)?.TrimStart('.').ToLowerInvariant() ?? "toml";
			string content = await ModComponentSerializationService.SaveToStringAsync(components, extension);

			string outputDir = Path.GetDirectoryName(filePath);
			if ( !string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir) )
			{
				Directory.CreateDirectory(outputDir);
			}

			await Task.Run(() => File.WriteAllText(filePath, content));
		}

		private static string ReadFileWithEncodingFallback(string filePath)
		{
			try
			{
				var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
				var bytes = File.ReadAllBytes(filePath);
				var content = encoding.GetString(bytes);

				content = content.Replace('\uFFFD', '_');

				return content;
			}
			catch ( Exception )
			{
				return File.ReadAllText(filePath);
			}
		}
	}
}

