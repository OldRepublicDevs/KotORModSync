// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Parsing
{
	/// <summary>
	/// Parses natural language installation/download instructions into structured Instruction objects.
	/// Handles all variations found in mod build documentation with maximum tolerance for rephrasement.
	/// </summary>
	public sealed class NaturalLanguageInstructionParser
	{
		private readonly Action<string> _logInfo;
		private readonly Action<string> _logVerbose;

		// Core instruction detection patterns - ordered by specificity (most specific first)
		// These patterns cover ALL variations found in KOTOR mod build documentation
		private static readonly List<InstructionPattern> s_instructionPatterns = new List<InstructionPattern>
		{
			// === HIGHLY SPECIFIC MOVE/COPY PATTERNS (must come first) ===
			// "Move everything from X, Y, and Z folders to override"
			new InstructionPattern(
				@"move\s+everything\s+from\s+the\s+(?<folders>(?:[\w\s\-_]+?,\s+)+(?:and\s+)?[\w\s\-_]+?)\s+folders?\s+to\s+(?:your\s+)?(?<destination>[\w\s\-_/\\]+)",
				Instruction.ActionType.Move,
				RegexOptions.IgnoreCase
			),
			// "Move the files from both X and Y folders"
			new InstructionPattern(
				@"move\s+(?:the\s+)?files?\s+from\s+(?:both\s+)?(?:the\s+)?(?<source1>[\w\s\-_/\\""']+?)\s+(?:and|&)\s+(?<source2>[\w\s\-_/\\""']+?)\s+folders?\s+to\s+(?:your\s+)?(?<destination>[\w\s\-_/\\]+)",
				Instruction.ActionType.Move,
				RegexOptions.IgnoreCase
			),
			// "Move everything from X folder to Y" (general)
			new InstructionPattern(
				@"(?:move|copy|transfer)\s+(?:all|everything|all\s+(?:the\s+)?(?:files?|content|loose\s+files?))\s+(?:from\s+)?(?:the\s+)?(?<source>[\w\s\-_/\\""'&,]+?)\s+(?:folder|directory)?\s+to\s+(?:your\s+)?(?:game'?s?\s+)?(?<destination>[\w\s\-_/\\]+)",
				Instruction.ActionType.Move,
				RegexOptions.IgnoreCase
			),
			// "Move the files from 'specific folder' to override"
			new InstructionPattern(
				@"(?:move|copy)\s+(?:the\s+)?(?:files?|content|everything)\s+from\s+[""'](?<source>[^""']+)[""']\s+to\s+(?:your\s+)?(?<destination>[\w\s\-_/\\]+)",
				Instruction.ActionType.Move,
				RegexOptions.IgnoreCase
			),
			// "Move X.ext, Y.ext, and Z.ext" (file list)
			new InstructionPattern(
				@"(?:move|copy)\s+(?<source>(?:[\w\-_.]+\.[\w]+(?:\s+&\s+\.[\w]+)?(?:,\s*|\s+and\s+|\s+&\s+|\s+plus\s+))+[\w\-_.]+\.[\w]+)(?:\s+to\s+(?<destination>[\w\s\-_/\\]+?))?",
				Instruction.ActionType.Move,
				RegexOptions.IgnoreCase
			),
			// "Move only X" / "Only move X"
			new InstructionPattern(
				@"(?:(?:move|copy|install)\s+(?:only|just|specifically)|(?:only|just)\s+(?:move|copy|install))\s+(?:the\s+)?(?<source>[\w\s\-_.&/\\()""']+?)(?:\s+to\s+(?:your\s+)?(?<destination>[\w\s\-_/\\]+?))?(?:\.|$|,)",
				Instruction.ActionType.Move,
				RegexOptions.IgnoreCase
			),
			// "Move ONLY X and Y (file list)"
			new InstructionPattern(
				@"move\s+ONLY\s+(?<source>[\w\s\-_.&/\\()]+?)(?:\s+to\s+(?<destination>[\w\s\-_/\\]+?))?(?:\.|$)",
				Instruction.ActionType.Move,
				RegexOptions.IgnoreCase
			),
			// "Move all MAN26 files"
			new InstructionPattern(
				@"move\s+all\s+[""']?(?<source>[\w\-_*?]+)[""']?\s+files?",
				Instruction.ActionType.Move,
				RegexOptions.IgnoreCase
			),
			// "Move the six files within"
			new InstructionPattern(
				@"move\s+the\s+(?:[\w]+)\s+files?\s+within(?:\s+\(NOT\s+including\s+(?<exclude>.+?)\))?",
				Instruction.ActionType.Move,
				RegexOptions.IgnoreCase
			),
			// "X should be moved" / "files should be moved from Y"
			new InstructionPattern(
				@"(?:the\s+)?(?:files?\s+)?(?:from\s+)?(?<source>[\w\s\-_/\\""']+?)\s+should\s+be\s+moved\s+(?:from\s+(?:the\s+)?(?<source_folder>[\w\s\-_]+?)\s+)?(?:to\s+)?(?:(?:the\s+)?(?<destination>[\w\s\-_/\\]+?))?",
				Instruction.ActionType.Move,
				RegexOptions.IgnoreCase
			),
			// Generic move (catch-all)
			new InstructionPattern(
				@"(?:move|copy|place|put|drag)\s+(?:the\s+)?(?<source>[\w\s\-_/\\*.,()]+?)(?:\s+(?:to|into)\s+(?<destination>[\w\s\-_/\\]+?))?",
				Instruction.ActionType.Move,
				RegexOptions.IgnoreCase
			),

			// === DELETE PATTERNS (comprehensive) ===
			// "Delete 'X' through 'Y' before installing"
			new InstructionPattern(
				@"delete\s+[""'](?<start>[\w\-_.]+?)[""']\s+through\s+[""'](?<end>[\w\-_.]+?)[""']?\s+before",
				Instruction.ActionType.Delete,
				RegexOptions.IgnoreCase
			),
			// "(sorted by name) X through Y"
			new InstructionPattern(
				@"\(sorted\s+by\s+name\)\s+(?<start>[\w\-_.]+?)\s+through\s+(?<end>[\w\-_.]+)",
				Instruction.ActionType.Delete,
				RegexOptions.IgnoreCase
			),
			// "Delete X, Y, and Z" (list)
			new InstructionPattern(
				@"(?:delete|remove)\s+(?<source>(?:[\w\-_.]+(?:\s+&\s+\.[\w]+)?(?:,\s*|\s+and\s+|\s+&\s+|\s+plus\s+))+[\w\-_.]+)(?:\s+before|\s+after|\.|\s+from|$)",
				Instruction.ActionType.Delete,
				RegexOptions.IgnoreCase
			),
			// "Delete everything inside X except Y"
			new InstructionPattern(
				@"delete\s+everything\s+inside\s+(?:the\s+)?(?<source>[\w\s\-_/\\]+?)\s+except\s+(?<exceptions>.+?)(?:\.|$)",
				Instruction.ActionType.Delete,
				RegexOptions.IgnoreCase
			),
			// "If present in your override, X must be deleted"
			new InstructionPattern(
				@"if\s+present\s+in\s+your\s+override,?\s+(?<source>[\w\s\-_.&/\\]+?)\s+must\s+be\s+deleted",
				Instruction.ActionType.Delete,
				RegexOptions.IgnoreCase
			),
			// General delete
			new InstructionPattern(
				@"(?:delete|remove)\s+(?:the\s+)?(?:file\s+)?(?<source>[\w\s\-_/\\*.,()&""']+?)(?:\s+before|\s+after|\s+from|\.|\s+in\s+your|$)",
				Instruction.ActionType.Delete,
				RegexOptions.IgnoreCase
			),

			// === EXTRACT PATTERNS ===
			// "Extract/Unzip the mod"
			new InstructionPattern(
				@"(?:extract|unzip|decompress|unpack)\s+(?:the\s+)?(?<source>[\w\s\-_/\\]+?)(?:\s+to\s+(?<destination>[\w\s\-_/\\]+?))?",
				Instruction.ActionType.Extract,
				RegexOptions.IgnoreCase
			),
			// "Once extracted/downloaded, enter X folder"
			new InstructionPattern(
				@"(?:once\s+)?(?:the\s+)?(?:mod\s+is\s+)?(?:extracted|downloaded),?\s+(?:enter|navigate\s+to|go\s+into)\s+(?:the\s+)?(?<source>[\w\s\-_/\\]+?)\s+folder",
				Instruction.ActionType.Extract,
				RegexOptions.IgnoreCase
			),

			// === PATCHER/INSTALLER PATTERNS (comprehensive) ===
			// "Run the installer X times for Y, Z"
			new InstructionPattern(
				@"(?:run|execute)\s+(?:the\s+)?(?:installer|patcher)\s+(?<times>[\w]+)\s+times?,?\s+(?:once\s+)?(?:to\s+install|for)\s+(?:each\s+of\s+)?(?:the\s+)?(?:options\s+)?(?<options>.+?)(?:\:|$)",
				Instruction.ActionType.Patcher,
				RegexOptions.IgnoreCase
			),
			// "Install the main mod, then re-run and select X"
			new InstructionPattern(
				@"install\s+(?:the\s+)?(?:main\s+mod|base\s+mod),?\s+then\s+re-run\s+(?:the\s+)?(?:patcher|installer)\s+and\s+select\s+(?<option>.+?)(?:\.|$|,\s+if)",
				Instruction.ActionType.Patcher,
				RegexOptions.IgnoreCase
			),
			// "Run the patcher using X install option"
			new InstructionPattern(
				@"run\s+the\s+(?:patcher|installer)\s+using\s+the\s+(?<option>.+?)\s+(?:install\s+)?option",
				Instruction.ActionType.Patcher,
				RegexOptions.IgnoreCase
			),
			// "When installing, there will be several optional files..."
			new InstructionPattern(
				@"when\s+installing,?\s+there\s+will\s+be\s+several\s+optional\s+files",
				Instruction.ActionType.Patcher,
				RegexOptions.IgnoreCase
			),
			// "Select the X-compatible installation"
			new InstructionPattern(
				@"select\s+the\s+(?<option>[\w\s\-_]+?)-compatible\s+installation",
				Instruction.ActionType.Patcher,
				RegexOptions.IgnoreCase
			),
			// "Use the X version/install"
			new InstructionPattern(
				@"use\s+the\s+(?<option>[\w\s\-_/\\.,&()]+?)\s+(?:version|install|installation|option)",
				Instruction.ActionType.Patcher,
				RegexOptions.IgnoreCase
			),
			// "Install X, then re-run for Y"
			new InstructionPattern(
				@"install\s+(?<option1>.+?),?\s+then\s+re-run\s+(?:it\s+)?(?:once\s+more|twice\s+more|again)?(?:,?\s+(?:once\s+)?for\s+(?:each\s+of\s+)?(?:the\s+)?)?(?<option2>.+?)(?:\.|$)",
				Instruction.ActionType.Patcher,
				RegexOptions.IgnoreCase
			),
			// "The installer will need to be run X times"
			new InstructionPattern(
				@"(?:the\s+)?installer\s+(?:for\s+this\s+mod\s+)?will\s+need\s+to\s+be\s+run\s+(?<times>[\w]+)\s+times",
				Instruction.ActionType.Patcher,
				RegexOptions.IgnoreCase
			),
			// "Don't use the complete installer"
			new InstructionPattern(
				@"don't\s+use\s+the\s+complete\s+installer(?:,\s+instead\s+selecting\s+the\s+(?<option>.+?))?",
				Instruction.ActionType.Patcher,
				RegexOptions.IgnoreCase
			),
			// "Run the HoloPatcher executable"
			new InstructionPattern(
				@"run\s+the\s+holopatcher\s+executable(?:\.\s+select\s+the\s+(?<option>.+?)\s+install)?",
				Instruction.ActionType.Patcher,
				RegexOptions.IgnoreCase
			),
			// "Go into X folder and apply Y"
			new InstructionPattern(
				@"go\s+into\s+the\s+(?<folder>[\w\s\-_""']+?)\s+folder\s+and\s+(?:install|apply)\s+(?:that\s+)?(?<option>[\w\s\-_]+)",
				Instruction.ActionType.Patcher,
				RegexOptions.IgnoreCase
			),
			// Generic installer/patcher
			new InstructionPattern(
				@"(?:run|execute|apply|use|install)\s+(?:the\s+)?(?<source>(?:installer|patcher|tslpatcher|holopatcher))(?:\s+(?:first|to\s+install|for|executable))?",
				Instruction.ActionType.Patcher,
				RegexOptions.IgnoreCase
			),
			// "Install X option"
			new InstructionPattern(
				@"(?:install|apply|select|use|choose)\s+(?:the\s+)?(?<option>[\w\s\-_/\\.,&()""']+?)\s+(?:install\s+)?(?:option|version|patch|compatibility|compatch|install)(?:\s+(?:if\s+using|when\s+using))?",
				Instruction.ActionType.Patcher,
				RegexOptions.IgnoreCase
			),
			// "Re-run the patcher"
			new InstructionPattern(
				@"re-run\s+(?:the\s+)?(?:patcher|installer)(?:\s+(?:and|to))?\s+(?:select|install|apply)?\s*(?<option>[\w\s\-_/\\.,&()""']*?)(?:\s+option)?(?:\.|$|,)",
				Instruction.ActionType.Patcher,
				RegexOptions.IgnoreCase
			),
			// "Apply the patch/main installation/contents"
			new InstructionPattern(
				@"apply\s+the\s+(?<option>(?:patch|main\s+installation|contents?|base\s+mod))(?:\s+(?:first|to))?",
				Instruction.ActionType.Patcher,
				RegexOptions.IgnoreCase
			),

			// === RENAME/COPY PATTERNS (comprehensive) ===
			// "Make a copy of X, paste into same directory, rename to Y"
			new InstructionPattern(
				@"make\s+a\s+copy\s+of\s+(?:the\s+)?(?:file\s+)?[""']?(?<source>[\w\-_.]+?)[""']?\s+(?:and\s+)?paste\s+it\s+into\s+the\s+same\s+(?:directory|folder|location)(?:.*?)rename\s+(?:this\s+duplicate|it)\s+to\s+[""']?(?<destination>[\w\-_.]+)[""']?",
				Instruction.ActionType.Copy,
				RegexOptions.IgnoreCase | RegexOptions.Singleline
			),
			// "Make a copy of X and rename it to Y" (without paste clause)
			new InstructionPattern(
				@"make\s+a\s+copy\s+of\s+(?:the\s+)?(?:file\s+)?[""']?(?<source>[\w\-_.]+?)[""']?\s+and\s+rename\s+(?:it\s+)?(?:to\s+)?[""']?(?<destination>[\w\-_.]+)[""']?",
				Instruction.ActionType.Copy,
				RegexOptions.IgnoreCase
			),
			// "Copy X and paste, this should create Y, rename to Z"
			new InstructionPattern(
				@"(?:copy|duplicate)\s+(?:the\s+)?(?:file\s+)?(?<source>[\w\-_.]+?).*?rename\s+(?:it|that|this\s+duplicate)\s+to\s+[""']?(?<destination>[\w\-_.]+)[""']?",
				Instruction.ActionType.Copy,
				RegexOptions.IgnoreCase | RegexOptions.Singleline
			),
			// "Rename the files X to Y"
			new InstructionPattern(
				@"rename\s+(?:the\s+)?files?\s+[""']?(?<source>[\w\-_.]+?)[""']?\s+(?:to|as)\s+[""']?(?<destination>[\w\-_.]+)[""']?",
				Instruction.ActionType.Rename,
				RegexOptions.IgnoreCase
			),
			// "Rename X to Y"
			new InstructionPattern(
				@"rename\s+(?<source>[\w\-_.]+?)\s+to\s+(?<destination>[\w\-_.]+)",
				Instruction.ActionType.Rename,
				RegexOptions.IgnoreCase
			),

			// === DELETE PATTERNS (exhaustive) ===
			// "Delete X.ext & .txi"
			new InstructionPattern(
				@"delete\s+(?<source>[\w\-_.]+?)(?:\s+&\s+\.[\w]+)+\s+before",
				Instruction.ActionType.Delete,
				RegexOptions.IgnoreCase
			),
			// "Delete 'X' through 'Y'"
			new InstructionPattern(
				@"delete\s+[""']?(?<start>[\w\-_.]+?)[""']?\s+through\s+[""']?(?<end>[\w\-_.]+)[""']?(?:\s+before|\s+in)?",
				Instruction.ActionType.Delete,
				RegexOptions.IgnoreCase
			),
			// "(sorted by name) X through Y"
			new InstructionPattern(
				@"\(sorted\s+by\s+name\)\s+(?<start>[\w\-_.]+?)\s+\*\*through\*\*\s+(?<end>[\w\-_.]+)",
				Instruction.ActionType.Delete,
				RegexOptions.IgnoreCase
			),
			// "Delete the following files: X, Y, Z"
			new InstructionPattern(
				@"delete\s+the\s+following\s+files?:\s+(?<source>.+?)(?:\.|$)",
				Instruction.ActionType.Delete,
				RegexOptions.IgnoreCase
			),
			// "X must be deleted"
			new InstructionPattern(
				@"(?<source>[\w\s\-_.&/\\()]+?)\s+must\s+be\s+deleted",
				Instruction.ActionType.Delete,
				RegexOptions.IgnoreCase
			),
			// Generic delete with file list
			new InstructionPattern(
				@"(?:delete|remove)\s+(?<source>[\w\s\-_/\\*.,()&""']+?)(?:\s+before|\s+after|\.|\s+from)",
				Instruction.ActionType.Delete,
				RegexOptions.IgnoreCase
			),

			// === EXTRACT PATTERNS ===
			new InstructionPattern(
				@"(?:extract|unzip)\s+(?:the\s+)?mod",
				Instruction.ActionType.Extract,
				RegexOptions.IgnoreCase
			),
			new InstructionPattern(
				@"(?:extract|unzip|decompress)\s+(?<source>[\w\s\-_/\\]+?)(?:\s+to\s+(?<destination>[\w\s\-_/\\]+?))?",
				Instruction.ActionType.Extract,
				RegexOptions.IgnoreCase
			),

			// === PATCHER/INSTALLER ===
			// Must cover all variations
			new InstructionPattern(
				@"(?:when\s+)?installing,?\s+(?:select|use|choose)\s+(?<option>.+?)(?:\.|;|$)",
				Instruction.ActionType.Patcher,
				RegexOptions.IgnoreCase
			),
			new InstructionPattern(
				@"re-run\s+the\s+(?:patcher|installer)(?:\s+and)?\s+(?:select|install)?\s*(?<option>.*?)(?:\.|$|,\s+if)",
				Instruction.ActionType.Patcher,
				RegexOptions.IgnoreCase
			),
			new InstructionPattern(
				@"(?:use|apply)\s+the\s+tslpatcher(?:\s+installer\s+method)?",
				Instruction.ActionType.Patcher,
				RegexOptions.IgnoreCase
			),

			// === EXECUTE PATTERNS ===
			// "Place X in Y, run it"
			new InstructionPattern(
				@"place\s+(?<source>[\w\s\-_.]+?)\s+in\s+(?:your\s+)?(?<destination>[\w\s\-_/\\]+?)(?:,\s+|\s+and\s+)run\s+(?:it|the\s+file)",
				Instruction.ActionType.Execute,
				RegexOptions.IgnoreCase
			),
			// "Download X and run the executable"
			new InstructionPattern(
				@"download\s+(?:one\s+of\s+)?(?:the\s+)?(?<source>[\w\s\-_.]+?).*?(?:run|execute)\s+(?:the\s+)?(?:executable|file)",
				Instruction.ActionType.Execute,
				RegexOptions.IgnoreCase | RegexOptions.Singleline
			),

			// === DELDUPLICATE PATTERNS ===
			new InstructionPattern(
				@"place\s+(?<source>DelDuplicateTGA-TPC|bash\s+script\s+version)\s+in\s+your\s+main\s+game\s+folder",
				Instruction.ActionType.DelDuplicate,
				RegexOptions.IgnoreCase
			),
			new InstructionPattern(
				@"say\s+that\s+\*\*(?<extension>TPC|TGA)\s+should\s+be\s+deleted\*\*",
				Instruction.ActionType.DelDuplicate,
				RegexOptions.IgnoreCase
			),
			new InstructionPattern(
				@"run\s+it,?\s+say\s+that\s+(?<extension>TPC|TGA)\s+should\s+be\s+deleted",
				Instruction.ActionType.DelDuplicate,
				RegexOptions.IgnoreCase
			),
		};

		// Conditional/option detection patterns
		private static readonly List<Regex> s_conditionalPatterns = new List<Regex>
		{
			new Regex(@"if\s+(?:you\s+)?(?:are\s+)?using\s+(?<dependency>[\w\s\-_'""]+?)(?:,|\.|;|$)", RegexOptions.IgnoreCase),
			new Regex(@"if\s+(?:you\s+)?(?:intending\s+to\s+use|intend\s+to\s+use)\s+(?<dependency>[\w\s\-_'""]+?)(?:,|\.|;|$)", RegexOptions.IgnoreCase),
			new Regex(@"when\s+using\s+(?<dependency>[\w\s\-_'""]+?)(?:,|\.|;|$)", RegexOptions.IgnoreCase),
			new Regex(@"(?:if\s+)?(?:you're|you\s+are)\s+using\s+(?<dependency>[\w\s\-_'""]+?)(?:,|\.|;|$)", RegexOptions.IgnoreCase),
		};

		// Recommendation/preference patterns for options
		private static readonly List<Regex> s_recommendationPatterns = new List<Regex>
		{
			new Regex(@"(?:I\s+)?(?:recommend|suggest|advise)\s+(?:the\s+)?(?<option>[^.,;]+?)(?:\s+version|\s+option|\s+install)?(?:[.,;]|$)", RegexOptions.IgnoreCase),
			new Regex(@"(?:I\s+)?(?:strongly\s+)?recommend\s+(?:using\s+)?(?:the\s+)?(?<option>[^.,;]+?)", RegexOptions.IgnoreCase),
			new Regex(@"(?:your\s+)?(?:choice|preference)(?:\s+(?:of\s+which|whether))?", RegexOptions.IgnoreCase),
			new Regex(@"(?:download|use|choose|select)\s+(?:the\s+)?(?<option>[^.,;]+?)\s+(?:version|option|variant|file|edition)", RegexOptions.IgnoreCase),
		};

		// File/folder extraction patterns
		private static readonly Dictionary<string, Regex> s_entityPatterns = new Dictionary<string, Regex>(StringComparer.OrdinalIgnoreCase)
		{
			// File ranges: "file01.tga through file10.tga"
			["file_range"] = new Regex(@"[""']?(?<start>[\w\-_]+\.[\w]+)[""']?\s+(?:through|to|-)\s+[""']?(?<end>[\w\-_]+\.[\w]+)[""']?", RegexOptions.IgnoreCase),
			// Numeric ranges: "m36aa_01_lm0 through m36aa_01_lm2.tga"
			["numeric_range"] = new Regex(@"(?<prefix>[\w\-_]+?)(?<start_num>\d+)(?<suffix>[\w\-_.]*?)\s+through\s+\k<prefix>(?<end_num>\d+)(?:\k<suffix>|\.[\w]+)", RegexOptions.IgnoreCase),
			// File lists: "file1.ext, file2.ext, and file3.ext"
			["file_list"] = new Regex(@"(?<files>(?:[\w\-_.]+\.[\w]+(?:\s+&\s+\.[\w]+)?(?:,\s*|\s+and\s+|\s+&\s+))+[\w\-_.]+\.[\w]+)", RegexOptions.IgnoreCase),
			// Single file with extension
			["single_file"] = new Regex(@"[""']?(?<file>[\w\-_]+\.[\w]{2,4})[""']?", RegexOptions.IgnoreCase),
			// Folder references
			["folder_from"] = new Regex(@"from\s+(?:the\s+)?(?:both\s+the\s+)?(?<folder>[\w\s\-_&/\\]+?)\s+(?:and\s+(?<folder2>[\w\s\-_/\\]+?)\s+)?(?:folder|directory)", RegexOptions.IgnoreCase),
			["folder_name"] = new Regex(@"(?:the\s+)?[""']?(?<folder>[\w\s\-_/\\]+?)[""']?\s+folder", RegexOptions.IgnoreCase),
			// Destination patterns
			["override_dest"] = new Regex(@"(?:to\s+)?(?:your\s+)?(?:game'?s?\s+)?override(?:\s+(?:folder|directory))?", RegexOptions.IgnoreCase),
			["main_dir_dest"] = new Regex(@"(?:to\s+)?(?:the\s+)?main\s+(?:game\s+)?(?:directory|folder)", RegexOptions.IgnoreCase),
			["movies_dest"] = new Regex(@"(?:to\s+)?(?:your\s+)?(?:game'?s?\s+)?movies\s+folder", RegexOptions.IgnoreCase),
			// Wildcards and patterns
			["wildcard"] = new Regex(@"(?<pattern>[\w\-_*?]+\*[\w\-_*?]*)", RegexOptions.IgnoreCase),
			// Exclusions
			["except"] = new Regex(@"(?:except|excluding|but\s+not|not\s+including)(?:\s+(?:for|the))?\s+(?:the\s+)?(?<exceptions>.+?)(?:\s*(?:\(|:|;|\.|$))", RegexOptions.IgnoreCase | RegexOptions.Singleline),
			["ignore"] = new Regex(@"(?:ignore|skip|don't\s+(?:install|move|use))\s+(?:the\s+)?(?<ignore>.+?)(?:\.|;|,\s+you|\s+unless|$)", RegexOptions.IgnoreCase),
			// Overwrite detection
			["overwrite"] = new Regex(@"(?:overwrite|replace)(?:\s+when\s+prompted|\s+if\s+(?:asked|prompted))?", RegexOptions.IgnoreCase),
			["no_overwrite"] = new Regex(@"(?:do\s+not|don't)\s+overwrite", RegexOptions.IgnoreCase),
			// Multiple file types
			["file_types"] = new Regex(@"(?<types>(?:\.[\w]+(?:\s+&\s+|,\s*|\s+and\s+))+\.[\w]+)", RegexOptions.IgnoreCase),
		};

		// Destination normalization mappings
		private static readonly Dictionary<string, string> s_destinationMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["override"] = @"<<kotorDirectory>>\Override",
			["override folder"] = @"<<kotorDirectory>>\Override",
			["override directory"] = @"<<kotorDirectory>>\Override",
			["game's override"] = @"<<kotorDirectory>>\Override",
			["your override"] = @"<<kotorDirectory>>\Override",
			["main game directory"] = @"<<kotorDirectory>>",
			["main directory"] = @"<<kotorDirectory>>",
			["game directory"] = @"<<kotorDirectory>>",
			["kotor directory"] = @"<<kotorDirectory>>",
			["movies folder"] = @"<<kotorDirectory>>\Movies",
			["movies"] = @"<<kotorDirectory>>\Movies",
			["your movies folder"] = @"<<kotorDirectory>>\Movies",
		};

		public NaturalLanguageInstructionParser([CanBeNull] Action<string> logInfo = null, [CanBeNull] Action<string> logVerbose = null)
		{
			_logInfo = logInfo ?? (_ => { });
			_logVerbose = logVerbose ?? (_ => { });
		}

		/// <summary>
		/// Parses natural language instructions into structured Instruction objects.
		/// </summary>
		[NotNull]
		public ObservableCollection<Instruction> ParseInstructions(
			[NotNull] string installationInstructions,
			[CanBeNull] string downloadInstructions,
			[NotNull] ModComponent parentComponent)
		{
			if ( installationInstructions == null )
				throw new ArgumentNullException(nameof(installationInstructions));
			if ( parentComponent == null )
				throw new ArgumentNullException(nameof(parentComponent));

			var instructions = new ObservableCollection<Instruction>();

			_logVerbose($"[NLParser] Parsing instructions for component: {parentComponent.Name}");

			// Split into logical units (sentences/clauses)
			var units = SplitIntoProcessingUnits(installationInstructions);
			_logVerbose($"[NLParser] Found {units.Count} instruction units to parse");

			foreach ( string unit in units )
			{
				var parsedInstructions = ParseInstructionUnit(unit, parentComponent);
				foreach ( var instruction in parsedInstructions )
				{
					instructions.Add(instruction);
					string sourcePreview = instruction.Source != null && instruction.Source.Count > 0
						? string.Join(", ", instruction.Source.Take(3))
						: "(no source)";
					_logVerbose($"[NLParser] Created {instruction.Action} instruction: {sourcePreview}");
				}
			}

			// Parse download instructions for options/recommendations
			if ( !string.IsNullOrWhiteSpace(downloadInstructions) )
			{
				var downloadOptions = ParseDownloadInstructions(downloadInstructions, parentComponent);
				foreach ( var option in downloadOptions )
				{
					parentComponent.Options.Add(option);
					_logVerbose($"[NLParser] Created option from download instructions: {option.Name}");
				}
			}

			_logInfo($"[NLParser] Generated {instructions.Count} instructions for '{parentComponent.Name}'");
			return instructions;
		}

		/// <summary>
		/// Splits instruction text into logical processing units (sentences/clauses).
		/// Handles complex multi-clause instructions.
		/// </summary>
		[NotNull]
		private List<string> SplitIntoProcessingUnits([NotNull] string text)
		{
			var units = new List<string>();

			// First, split on sentence boundaries (periods followed by space or newline)
			// But be careful not to split on file extensions
			var sentencePattern = new Regex(@"(?<!\w\.\w)(?<=\.)\s+(?=[A-Z])", RegexOptions.Multiline);
			var sentences = sentencePattern.Split(text);

			foreach ( string sentence in sentences )
			{
				if ( string.IsNullOrWhiteSpace(sentence) )
					continue;

				// Further split on semicolons and "then" clauses for multi-step instructions
				var clausePattern = new Regex(@"(?:;|,?\s+then\s+|,\s+and\s+then\s+)", RegexOptions.IgnoreCase);
				var clauses = clausePattern.Split(sentence);

				foreach ( string clause in clauses )
				{
					string cleaned = clause.Trim().TrimEnd('.');
					if ( cleaned.Length > 15 ) // Ignore very short fragments
					{
						units.Add(cleaned);
					}
				}
			}

			// If no units found, just use the whole text
			if ( units.Count == 0 && !string.IsNullOrWhiteSpace(text) )
			{
				units.Add(text.Trim());
			}

			return units;
		}

		/// <summary>
		/// Parses a single instruction unit into zero or more Instructions.
		/// </summary>
		[NotNull]
		private List<Instruction> ParseInstructionUnit([NotNull] string unit, [NotNull] ModComponent parentComponent)
		{
			var instructions = new List<Instruction>();

			// Skip informational/commentary sentences
			if ( IsInformationalOnly(unit) )
			{
				_logVerbose($"[NLParser] Skipping informational unit: {unit.Substring(0, Math.Min(60, unit.Length))}...");
				return instructions;
			}

			// Try each instruction pattern
			foreach ( var pattern in s_instructionPatterns )
			{
				var match = pattern.Regex.Match(unit);
				if ( !match.Success )
					continue;

				var instruction = CreateInstructionFromMatch(match, pattern, unit, parentComponent);
				if ( instruction != null )
				{
					instructions.Add(instruction);
					// Only use first matching pattern per unit
					break;
				}
			}

			// If no pattern matched but it looks like an action, log it
			if ( instructions.Count == 0 && ContainsActionVerb(unit) )
			{
				_logVerbose($"[NLParser] No pattern matched for unit with action verb: {unit.Substring(0, Math.Min(80, unit.Length))}...");
			}

			return instructions;
		}

		/// <summary>
		/// Determines if a unit is purely informational (no actionable instruction).
		/// </summary>
		private bool IsInformationalOnly([NotNull] string unit)
		{
			string lower = unit.ToLowerInvariant();

			// Commentary patterns
			if ( lower.StartsWith("bear in mind") ||
				lower.StartsWith("keep in mind") ||
				lower.StartsWith("note that") ||
				lower.StartsWith("be aware") ||
				lower.StartsWith("remember") ||
				lower.Contains("this is normal") ||
				lower.Contains("this is intended") ||
				lower.Contains("is your choice") ||
				lower.Contains("is your preference") ||
				lower.Contains("up to you") ||
				lower.Contains("which of these you choose") )
			{
				return true;
			}

			// Questions
			if ( lower.TrimStart().StartsWith("which ") || lower.Contains("?") )
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// Checks if unit contains an action verb indicating it's an instruction.
		/// </summary>
		private bool ContainsActionVerb([NotNull] string unit)
		{
			string lower = unit.ToLowerInvariant();
			string[] actionVerbs = { "move", "copy", "delete", "remove", "install", "apply", "run", "execute",
									 "extract", "unzip", "place", "put", "rename", "download", "use", "select" };

			return actionVerbs.Any(verb => lower.Contains(" " + verb + " ") || lower.StartsWith(verb + " "));
		}

		/// <summary>
		/// Creates an Instruction from a regex match.
		/// </summary>
		[CanBeNull]
		private Instruction CreateInstructionFromMatch(
			[NotNull] Match match,
			[NotNull] InstructionPattern pattern,
			[NotNull] string unit,
			[NotNull] ModComponent parentComponent)
		{
			var instruction = new Instruction
			{
				Guid = Guid.NewGuid(),
				Action = pattern.ActionType,
				Source = new List<string>(),
				Overwrite = true
			};
			instruction.SetParentComponent(parentComponent);

			// === Extract Source(s) ===
			if ( match.Groups["source"].Success )
			{
				string sourceText = match.Groups["source"].Value.Trim();
				var sources = ExtractSources(sourceText, unit, pattern.ActionType);

				// Check for second source (e.g., "from both X and Y folders")
				if ( match.Groups["source2"].Success )
				{
					string source2Text = match.Groups["source2"].Value.Trim();
					var sources2 = ExtractSources(source2Text, unit, pattern.ActionType);
					sources.AddRange(sources2);
				}

				instruction.Source = sources;
			}
			else if ( match.Groups["start"].Success && match.Groups["end"].Success )
			{
				// Handle file ranges directly in main pattern
				string start = match.Groups["start"].Value;
				string end = match.Groups["end"].Value;
				var rangeSources = GenerateFileRange(start, end);
				instruction.Source = rangeSources;
			}

			// === Extract Destination ===
			if ( match.Groups["destination"].Success )
			{
				string destText = match.Groups["destination"].Value.Trim();
				instruction.Destination = NormalizeDestination(destText, unit);
			}
			else
			{
				// Auto-detect destination from context
				instruction.Destination = InferDestination(unit, pattern.ActionType);
			}

			// === Extract Option/Arguments ===
			if ( match.Groups["option"].Success )
			{
				string optionText = match.Groups["option"].Value.Trim();
				// For Patcher instructions, option becomes an argument (namespace selection)
				if ( pattern.ActionType == Instruction.ActionType.Patcher )
				{
					instruction.Arguments = optionText;
				}
			}

			// === Extract Extension for DelDuplicate ===
			if ( match.Groups["extension"].Success )
			{
				string extension = match.Groups["extension"].Value.Trim();
				instruction.Arguments = "." + extension.ToLowerInvariant();
			}

			// === Check for Overwrite Instructions ===
			if ( s_entityPatterns["overwrite"].IsMatch(unit) )
			{
				instruction.Overwrite = true;
			}
			else if ( s_entityPatterns["no_overwrite"].IsMatch(unit) )
			{
				instruction.Overwrite = false;
			}

			// === Handle Exceptions/Exclusions ===
			instruction.Source = ApplyExclusions(instruction.Source, unit);

			// === Handle "Only" Clauses ===
			var onlyMatch = Regex.Match(unit, @"(?:only|just)\s+(?:move|use|install)\s+(?:the\s+)?(?<only>.+?)(?:\.|;|,\s+(?:not|ignore)|$)", RegexOptions.IgnoreCase);
			if ( onlyMatch.Success )
			{
				string onlyText = onlyMatch.Groups["only"].Value;
				var onlySources = ExtractSources(onlyText, unit, pattern.ActionType);
				if ( onlySources.Count > 0 )
				{
					instruction.Source = onlySources;
				}
			}

			// === Validate Instruction ===
			if ( !ValidateInstruction(instruction) )
			{
				_logVerbose($"[NLParser] Rejected incomplete instruction: {pattern.ActionType}");
				return null;
			}

			return instruction;
		}

		/// <summary>
		/// Validates that an instruction has all required fields.
		/// </summary>
		private bool ValidateInstruction([NotNull] Instruction instruction)
		{
			// All instructions need an action type
			if ( instruction.Action == Instruction.ActionType.Unset )
				return false;

			// Source-required actions
			if ( instruction.Action == Instruction.ActionType.Move ||
				instruction.Action == Instruction.ActionType.Copy ||
				instruction.Action == Instruction.ActionType.Delete ||
				instruction.Action == Instruction.ActionType.Rename ||
				instruction.Action == Instruction.ActionType.Extract ||
				instruction.Action == Instruction.ActionType.Execute )
			{
				if ( instruction.Source == null || instruction.Source.Count == 0 )
					return false;
			}

			// Destination-required actions
			if ( instruction.Action == Instruction.ActionType.Move ||
				instruction.Action == Instruction.ActionType.Copy )
			{
				if ( string.IsNullOrWhiteSpace(instruction.Destination) )
					return false;
			}

			return true;
		}

		/// <summary>
		/// Extracts source file/folder paths from text.
		/// </summary>
		[NotNull]
		private List<string> ExtractSources([NotNull] string sourceText, [NotNull] string fullUnit, Instruction.ActionType actionType)
		{
			var sources = new List<string>();

			// === Check for file ranges ===
			var rangeMatch = s_entityPatterns["file_range"].Match(fullUnit);
			if ( !rangeMatch.Success )
			{
				rangeMatch = s_entityPatterns["numeric_range"].Match(fullUnit);
			}

			if ( rangeMatch.Success )
			{
				string start = rangeMatch.Groups["start"].Value;
				string end = rangeMatch.Groups["end"].Value;
				var rangeSources = GenerateFileRange(start, end);
				return rangeSources;
			}

			// === Check for folder references ===
			var folderMatch = s_entityPatterns["folder_from"].Match(sourceText);
			if ( !folderMatch.Success )
			{
				folderMatch = s_entityPatterns["folder_name"].Match(sourceText);
			}

			if ( folderMatch.Success )
			{
				string folder = folderMatch.Groups["folder"].Value.Trim();
				// Normalize folder name
				folder = folder.Trim('"', '\'', ' ');
				sources.Add($"<<modDirectory>>\\{folder}\\*");

				// Check for second folder
				if ( folderMatch.Groups["folder2"].Success )
				{
					string folder2 = folderMatch.Groups["folder2"].Value.Trim().Trim('"', '\'', ' ');
					sources.Add($"<<modDirectory>>\\{folder2}\\*");
				}
				return sources;
			}

			// === Check for file lists ===
			var fileListMatch = s_entityPatterns["file_list"].Match(sourceText);
			if ( fileListMatch.Success )
			{
				string filesText = fileListMatch.Groups["files"].Value;
				var files = ParseFileList(filesText);
				foreach ( string file in files )
				{
					sources.Add($"<<modDirectory>>\\{file}");
				}
				if ( sources.Count > 0 )
					return sources;
			}

			// === Check for wildcards ===
			var wildcardMatch = s_entityPatterns["wildcard"].Match(sourceText);
			if ( wildcardMatch.Success )
			{
				string pattern = wildcardMatch.Groups["pattern"].Value;
				sources.Add($"<<modDirectory>>\\{pattern}");
				return sources;
			}

			// === Check for single file ===
			var singleFileMatch = s_entityPatterns["single_file"].Match(sourceText);
			if ( singleFileMatch.Success )
			{
				string file = singleFileMatch.Groups["file"].Value.Trim('"', '\'');
				sources.Add($"<<modDirectory>>\\{file}");
				return sources;
			}

			// === Fallback: treat as folder or pattern ===
			if ( !string.IsNullOrWhiteSpace(sourceText) )
			{
				string cleaned = sourceText.Trim('"', '\'', ' ', ',', ';');
				if ( cleaned.Length > 0 )
				{
					// Check if it looks like a file (has extension)
					if ( Path.HasExtension(cleaned) )
					{
						sources.Add($"<<modDirectory>>\\{cleaned}");
					}
					// Check if it has wildcards
					else if ( cleaned.Contains("*") || cleaned.Contains("?") )
					{
						sources.Add($"<<modDirectory>>\\{cleaned}");
					}
					// Otherwise treat as folder
					else if ( actionType == Instruction.ActionType.Move || actionType == Instruction.ActionType.Copy )
					{
						sources.Add($"<<modDirectory>>\\{cleaned}\\*");
					}
					else
					{
						sources.Add($"<<modDirectory>>\\{cleaned}");
					}
				}
			}

			return sources;
		}

		/// <summary>
		/// Generates a list of file sources from a range specification.
		/// </summary>
		[NotNull]
		private List<string> GenerateFileRange([NotNull] string start, [NotNull] string end)
		{
			// For file ranges, create a wildcard pattern that covers the range
			// E.g., "file01.tga" through "file10.tga" becomes "file*.tga"

			string startBase = Path.GetFileNameWithoutExtension(start);
			string endBase = Path.GetFileNameWithoutExtension(end);
			string extension = Path.GetExtension(start);

			// Find common prefix
			int commonPrefixLength = 0;
			int minLength = Math.Min(startBase.Length, endBase.Length);
			for ( int i = 0; i < minLength; i++ )
			{
				if ( startBase[i] == endBase[i] )
					commonPrefixLength++;
				else
					break;
			}

			if ( commonPrefixLength > 0 )
			{
				string prefix = startBase.Substring(0, commonPrefixLength);
				return new List<string> { $"<<modDirectory>>\\{prefix}*{extension}" };
			}

			// Fallback: return both explicit files
			return new List<string>
			{
				$"<<modDirectory>>\\{start}",
				$"<<modDirectory>>\\{end}"
			};
		}

		/// <summary>
		/// Parses comma/semicolon-separated file lists.
		/// </summary>
		[NotNull]
		private List<string> ParseFileList([NotNull] string text)
		{
			var files = new List<string>();

			// Split on commas, semicolons, and "and"
			var parts = Regex.Split(text, @"\s*(?:,|;|\s+and\s+|\s+&\s+)\s*", RegexOptions.IgnoreCase);

			foreach ( string part in parts )
			{
				string cleaned = part.Trim().Trim('"', '\'', ' ');

				if ( cleaned.Length > 0 &&
					!cleaned.Equals("and", StringComparison.OrdinalIgnoreCase) &&
					!cleaned.Equals("the", StringComparison.OrdinalIgnoreCase) )
				{
					// Only add if it looks like a filename (has extension or is a known pattern)
					if ( Path.HasExtension(cleaned) || cleaned.Contains("*") )
					{
						files.Add(cleaned);
					}
				}
			}

			return files;
		}

		/// <summary>
		/// Applies exclusions (EXCEPT clauses) to source list.
		/// </summary>
		[NotNull]
		private List<string> ApplyExclusions([NotNull] List<string> sources, [NotNull] string fullUnit)
		{
			// Check for "EXCEPT" clauses
			var exceptMatch = s_entityPatterns["except"].Match(fullUnit);
			if ( exceptMatch.Success )
			{
				string exceptionsText = exceptMatch.Groups["exceptions"].Value;
				var excludedFiles = ExtractExcludedFiles(exceptionsText);

				if ( excludedFiles.Count > 0 )
				{
					_logVerbose($"[NLParser] Found {excludedFiles.Count} exclusions to apply");
					// Filter out excluded files
					sources = sources.Where(s =>
						!excludedFiles.Any(ex => s.Contains(ex, StringComparison.OrdinalIgnoreCase))
					).ToList();
				}
			}

			// Check for "IGNORE" clauses
			var ignoreMatch = s_entityPatterns["ignore"].Match(fullUnit);
			if ( ignoreMatch.Success )
			{
				string ignoreText = ignoreMatch.Groups["ignore"].Value;
				var ignoredFiles = ExtractExcludedFiles(ignoreText);

				if ( ignoredFiles.Count > 0 )
				{
					_logVerbose($"[NLParser] Found {ignoredFiles.Count} items to ignore");
					sources = sources.Where(s =>
						!ignoredFiles.Any(ig => s.Contains(ig, StringComparison.OrdinalIgnoreCase))
					).ToList();
				}
			}

			return sources;
		}

		/// <summary>
		/// Extracts excluded file names from exception text.
		/// </summary>
		[NotNull]
		private List<string> ExtractExcludedFiles([NotNull] string exceptionText)
		{
			var excluded = new List<string>();

			// Check for file list in parentheses
			var parenMatch = Regex.Match(exceptionText, @"\((?<files>[^)]+)\)", RegexOptions.IgnoreCase);
			if ( parenMatch.Success )
			{
				exceptionText = parenMatch.Groups["files"].Value;
			}

			// Parse file lists
			excluded.AddRange(ParseFileList(exceptionText));

			// Check for folder references
			var folderMatch = s_entityPatterns["folder_name"].Match(exceptionText);
			if ( folderMatch.Success )
			{
				string folder = folderMatch.Groups["folder"].Value.Trim();
				excluded.Add(folder);
			}

			return excluded;
		}

		/// <summary>
		/// Normalizes destination paths to use placeholders.
		/// </summary>
		[NotNull]
		private string NormalizeDestination([NotNull] string destination, [NotNull] string fullUnit)
		{
			string lower = destination.ToLowerInvariant().Trim();

			// Check for direct mappings
			foreach ( var mapping in s_destinationMappings )
			{
				if ( lower.Contains(mapping.Key) || lower.Equals(mapping.Key.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) )
				{
					return mapping.Value;
				}
			}

			// Check full unit for destination clues
			if ( fullUnit.Contains("override", StringComparison.OrdinalIgnoreCase) )
			{
				return @"<<kotorDirectory>>\Override";
			}

			if ( fullUnit.Contains("movies", StringComparison.OrdinalIgnoreCase) )
			{
				return @"<<kotorDirectory>>\Movies";
			}

			// Treat as subdirectory of mod directory if it looks like a path
			if ( destination.Contains("\\") || destination.Contains("/") )
			{
				return $"<<modDirectory>>\\{destination}";
			}

			// Default to override for Move/Copy
			return @"<<kotorDirectory>>\Override";
		}

		/// <summary>
		/// Infers destination from context when not explicitly stated.
		/// </summary>
		[NotNull]
		private string InferDestination([NotNull] string fullUnit, Instruction.ActionType actionType)
		{
			string lower = fullUnit.ToLowerInvariant();

			// Check for destination keywords
			if ( lower.Contains("to override") || lower.Contains("to your override") || lower.Contains("in override") )
			{
				return @"<<kotorDirectory>>\Override";
			}

			if ( lower.Contains("to movies") || lower.Contains("in your movies") || lower.Contains("movies folder") )
			{
				return @"<<kotorDirectory>>\Movies";
			}

			if ( lower.Contains("main game directory") || lower.Contains("main directory") || lower.Contains("game directory") )
			{
				return @"<<kotorDirectory>>";
			}

			// Patcher always uses kotorDirectory
			if ( actionType == Instruction.ActionType.Patcher )
			{
				return @"<<kotorDirectory>>";
			}

			// Default for Move/Copy
			if ( actionType == Instruction.ActionType.Move || actionType == Instruction.ActionType.Copy )
			{
				return @"<<kotorDirectory>>\Override";
			}

			// Others don't need destination
			return string.Empty;
		}

		/// <summary>
		/// Parses download instructions into Option objects for user selection.
		/// </summary>
		[NotNull]
		private List<Option> ParseDownloadInstructions([NotNull] string downloadText, [NotNull] ModComponent parentComponent)
		{
			var options = new List<Option>();

			// Look for recommendations
			foreach ( var recommendPattern in s_recommendationPatterns )
			{
				var matches = recommendPattern.Matches(downloadText);
				foreach ( Match match in matches )
				{
					if ( !match.Groups["option"].Success )
						continue;

					string optionName = match.Groups["option"].Value.Trim();

					// Skip if already exists
					if ( options.Any(o => o.Name.Equals(optionName, StringComparison.OrdinalIgnoreCase)) )
						continue;

					// Skip if it's just a generic phrase
					if ( IsGenericPhrase(optionName) )
						continue;

					var option = new Option
					{
						Guid = Guid.NewGuid(),
						Name = optionName,
						Description = $"Recommended option: {optionName}",
						IsSelected = downloadText.Contains("recommend", StringComparison.OrdinalIgnoreCase)
					};
					options.Add(option);
				}
			}

			// Look for version/variant selections
			var versionPattern = new Regex(@"(?:download|use|choose|install|get|select)\s+(?:the\s+)?(?<option>[^.,;!?\r\n]+?)\s+(?:version|variant|option|file|edition)", RegexOptions.IgnoreCase);
			var versionMatches = versionPattern.Matches(downloadText);

			foreach ( Match match in versionMatches )
			{
				string optionName = match.Groups["option"].Value.Trim();

				if ( options.Any(o => o.Name.Equals(optionName, StringComparison.OrdinalIgnoreCase)) )
					continue;

				if ( IsGenericPhrase(optionName) )
					continue;

				var option = new Option
				{
					Guid = Guid.NewGuid(),
					Name = optionName,
					Description = $"Option: {optionName}",
					IsSelected = false
				};
				options.Add(option);
			}

			return options;
		}

		/// <summary>
		/// Determines if a phrase is too generic to be a meaningful option.
		/// </summary>
		private bool IsGenericPhrase([NotNull] string phrase)
		{
			string lower = phrase.ToLowerInvariant().Trim();

			string[] genericPhrases = {
				"this", "that", "these", "those", "it", "them",
				"the", "a", "an", "all", "both", "either",
				"you", "your", "my", "our"
			};

			return genericPhrases.Contains(lower) || lower.Length < 3;
		}

		/// <summary>
		/// Pattern definition for instruction matching.
		/// </summary>
		private class InstructionPattern
		{
			public Regex Regex { get; }
			public Instruction.ActionType ActionType { get; }

			public InstructionPattern(string pattern, Instruction.ActionType actionType, RegexOptions options)
			{
				Regex = new Regex(pattern, options | RegexOptions.Compiled);
				ActionType = actionType;
			}
		}
	}
}
