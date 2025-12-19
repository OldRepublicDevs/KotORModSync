using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace HoloPatcher.UI
{
    /// <summary>
    /// RTF stripper implementation matching Python's striprtf library.
    /// Equivalent to utility/string_util.py striprtf function.
    /// </summary>
    public static class RtfStripper
    {
        // Control words which specify a "destination" - content to be ignored
        private static readonly HashSet<string> Destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "aftncn", "aftnsep", "aftnsepc", "annotation", "atnauthor", "atndate",
            "atnicn", "atnid", "atnparent", "atnref", "atntime", "atrfend", "atrfstart",
            "author", "background", "bkmkend", "bkmkstart", "blipuid", "buptim",
            "category", "colorschememapping", "colortbl", "comment", "company",
            "creatim", "datafield", "datastore", "defchp", "defpap", "do", "doccomm",
            "docvar", "dptxbxtext", "ebcend", "ebcstart", "factoidname", "falt",
            "fchars", "ffdeftext", "ffentrymcr", "ffexitmcr", "ffformat", "ffhelptext",
            "ffl", "ffname", "ffstattext", "field", "file", "filetbl", "fldinst",
            "fldrslt", "fldtype", "fname", "fontemb", "fontfile", "fonttbl", "footer",
            "footerf", "footerl", "footerr", "footnote", "formfield", "ftncn",
            "ftnsep", "ftnsepc", "g", "generator", "gridtbl", "header", "headerf",
            "headerl", "headerr", "hl", "hlfr", "hlinkbase", "hlloc", "hlsrc", "hsv",
            "htmltag", "info", "keycode", "keywords", "latentstyles", "lchars",
            "levelnumbers", "leveltext", "lfolevel", "linkval", "list", "listlevel",
            "listname", "listoverride", "listoverridetable", "listpicture",
            "liststylename", "listtable", "listtext", "lsdlockedexcept", "macc",
            "maccPr", "mailmerge", "maln", "malnScr", "manager", "margPr", "mbar",
            "mbarPr", "mbaseJc", "mbegChr", "mborderBox", "mborderBoxPr", "mbox",
            "mboxPr", "mchr", "mcount", "mctrlPr", "md", "mdeg", "mdegHide", "mden",
            "mdiff", "mdPr", "me", "mendChr", "meqArr", "meqArrPr", "mf", "mfName",
            "mfPr", "mfunc", "mfuncPr", "mgroupChr", "mgroupChrPr", "mgrow",
            "mhideBot", "mhideLeft", "mhideRight", "mhideTop", "mhtmltag", "mlim",
            "mlimloc", "mlimlow", "mlimlowPr", "mlimupp", "mlimuppPr", "mm",
            "mmaddfieldname", "mmath", "mmathPict", "mmathPr", "mmaxdist", "mmc",
            "mmcJc", "mmconnectstr", "mmconnectstrdata", "mmcPr", "mmcs",
            "mmdatasource", "mmheadersource", "mmmailsubject", "mmodso",
            "mmodsofilter", "mmodsofldmpdata", "mmodsomappedname", "mmodsoname",
            "mmodsorecipdata", "mmodsosort", "mmodsosrc", "mmodsotable", "mmodsoudl",
            "mmodsoudldata", "mmodsouniquetag", "mmPr", "mmquery", "mmr", "mnary",
            "mnaryPr", "mnoBreak", "mnum", "mobjDist", "moMath", "moMathPara",
            "moMathParaPr", "mopEmu", "mphant", "mphantPr", "mplcHide", "mpos", "mr",
            "mrad", "mradPr", "mrPr", "msepChr", "mshow", "mshp", "msPre", "msPrePr",
            "msSub", "msSubPr", "msSubSup", "msSubSupPr", "msSup", "msSupPr",
            "mstrikeBLTR", "mstrikeH", "mstrikeTLBR", "mstrikeV", "msub", "msubHide",
            "msup", "msupHide", "mtransp", "mtype", "mvertJc", "mvfmf", "mvfml",
            "mvtof", "mvtol", "mzeroAsc", "mzeroDesc", "mzeroWid", "nesttableprops",
            "nextfile", "nonesttables", "objalias", "objclass", "objdata", "object",
            "objname", "objsect", "objtime", "oldcprops", "oldpprops", "oldsprops",
            "oldtprops", "oleclsid", "operator", "panose", "password", "passwordhash",
            "pgp", "pgptbl", "picprop", "pict", "pn", "pnseclvl", "pntext", "pntxta",
            "pntxtb", "printim", "private", "propname", "protend", "protstart",
            "protusertbl", "pxe", "result", "revtbl", "revtim", "rsidtbl", "rxe",
            "shp", "shpgrp", "shpinst", "shppict", "shprslt", "shptxt", "sn", "sp",
            "staticval", "stylesheet", "subject", "sv", "svb", "tc", "template",
            "themedata", "title", "txe", "ud", "upr", "userprops", "wgrffmtfilter",
            "windowcaption", "writereservation", "writereservhash", "xe", "xform",
            "xmlattrname", "xmlattrvalue", "xmlclose", "xmlname", "xmlnstbl", "xmlopen"
        };

        // Translation of special RTF characters
        private static readonly Dictionary<string, string> SpecialChars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "par", "\n" },
            { "sect", "\n\n" },
            { "page", "\n\n" },
            { "line", "\n" },
            { "tab", "\t" },
            { "emdash", "\u2014" },
            { "endash", "\u2013" },
            { "emspace", "\u2003" },
            { "enspace", "\u2002" },
            { "qmspace", "\u2005" },
            { "bullet", "\u2022" },
            { "lquote", "\u2018" },
            { "rquote", "\u2019" },
            { "ldblquote", "\u201C" },
            { "rdblquote", "\u201D" }
        };

        // Main RTF parsing pattern matching Python's implementation
        private static readonly Regex RtfPattern = new Regex(
            @"\\([a-z]{1,32})(-?\d{1,10})?[ ]?|\\'([0-9a-f]{2})|\\([^a-z])|([{}])|[\r\n]+|(.)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        /// <summary>
        /// Strips RTF formatting from text, returning plain text.
        /// Matches Python's striprtf function from utility/string_util.py
        /// </summary>
        /// <param name="rtfText">The RTF-formatted text to strip</param>
        /// <returns>Plain text with RTF formatting removed</returns>
        public static string StripRtf(string rtfText)
        {
            if (string.IsNullOrEmpty(rtfText))
            {
                return "";
            }

            // Stack to track state: (ucskip, ignorable)
            var stack = new Stack<(int ucskip, bool ignorable)>();
            bool ignorable = false; // Whether this group (and all inside it) are "ignorable"
            int ucskip = 1; // Number of ASCII characters to skip after a unicode character
            int curskip = 0; // Number of ASCII characters left to skip
            var output = new StringBuilder();

            foreach (Match match in RtfPattern.Matches(rtfText))
            {
                string word = match.Groups[1].Value;    // Control word
                string arg = match.Groups[2].Value;     // Numeric argument
                string hexcode = match.Groups[3].Value; // Hex code (\'xx)
                string charCode = match.Groups[4].Value; // Special char (\x not a letter)
                string brace = match.Groups[5].Value;   // Brace { or }
                string tchar = match.Groups[6].Value;   // Text character

                if (!string.IsNullOrEmpty(brace))
                {
                    curskip = 0;
                    if (brace == "{")
                    {
                        // Push state
                        stack.Push((ucskip, ignorable));
                    }
                    else if (brace == "}")
                    {
                        // Pop state
                        if (stack.Count > 0)
                        {
                            (int ucskip, bool ignorable) state = stack.Pop();
                            ucskip = state.ucskip;
                            ignorable = state.ignorable;
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(charCode))
                {
                    // \x (not a letter)
                    curskip = 0;
                    if (charCode == "~")
                    {
                        if (!ignorable)
                        {
                            output.Append('\u00A0'); // Non-breaking space
                        }
                    }
                    else if (charCode == "{" || charCode == "}" || charCode == "\\")
                    {
                        if (!ignorable)
                        {
                            output.Append(charCode);
                        }
                    }
                    else if (charCode == "*")
                    {
                        ignorable = true;
                    }
                }
                else if (!string.IsNullOrEmpty(word))
                {
                    // \foo control word
                    curskip = 0;
                    if (Destinations.Contains(word))
                    {
                        ignorable = true;
                    }
                    else if (ignorable)
                    {
                        // Skip ignorable content
                    }
                    else if (SpecialChars.TryGetValue(word, out string specialChar))
                    {
                        output.Append(specialChar);
                    }
                    else if (word == "uc")
                    {
                        if (int.TryParse(arg, out int ucValue))
                        {
                            ucskip = ucValue;
                        }
                    }
                    else if (word == "u")
                    {
                        if (int.TryParse(arg, out int c))
                        {
                            if (c < 0)
                            {
                                c += 0x10000;
                            }
                            output.Append((char)c);
                            curskip = ucskip;
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(hexcode))
                {
                    // \'xx hex character
                    if (curskip > 0)
                    {
                        curskip--;
                    }
                    else if (!ignorable)
                    {
                        int c = Convert.ToInt32(hexcode, 16);
                        output.Append((char)c);
                    }
                }
                else if (!string.IsNullOrEmpty(tchar))
                {
                    // Plain text character
                    if (curskip > 0)
                    {
                        curskip--;
                    }
                    else if (!ignorable)
                    {
                        output.Append(tchar);
                    }
                }
            }

            return output.ToString();
        }
    }
}

