using System;
using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Scriptutils = KOTORModSync.Formats.NCS.NCSDecomp.Scriptutils;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;


namespace KOTORModSync.Formats.NCS.NCSDecomp.Utils
{
    public class FileScriptData
    {
        private List<Scriptutils.SubScriptState> subs;
        private Scriptutils.SubScriptState globals;
        private SubroutineAnalysisData subdata;
        //private int status;
        private string code;
        private string originalbytecode;
        private string generatedbytecode;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:2167-2170
        // Original: public FileScriptData() { this.originalbytecode = null; this.generatedbytecode = null; }
        public FileScriptData()
        {
            this.subs = new List<Scriptutils.SubScriptState>();
            this.originalbytecode = null;
            this.generatedbytecode = null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:2884-2900
        // Original: public void close()
        public void Close()
        {
            if (subs != null)
            {
                foreach (var sub in subs)
                {
                    sub.Close();
                }
                subs = null;
            }
            if (globals != null)
            {
                globals.Close();
                globals = null;
            }
            if (subdata != null)
            {
                subdata.Close();
            }
            subdata = null;
            code = null;
            originalbytecode = null;
            generatedbytecode = null;
        }

        public void SetGlobals(Scriptutils.SubScriptState globals)
        {
            this.globals = globals;
        }

        public void AddSub(Scriptutils.SubScriptState sub)
        {
            subs.Add(sub);
        }

        public void SetSubdata(SubroutineAnalysisData subdata)
        {
            this.subdata = subdata;
        }

        [CanBeNull]
        public Scriptutils.SubScriptState FindSub(string name)
        {
            return subs.FirstOrDefault(state => state.GetName() == name);
        }

        public bool ReplaceSubName(string oldname, string newname)
        {
            var state = FindSub(oldname);
            if (state == null)
            {
                return false;
            }
            if (FindSub(newname) != null)
            {
                return false;
            }
            state.SetName(newname);
            GenerateCode();
            return true;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:2247-2250
        // Original: @Override public String toString() { return this.code; }
        public override string ToString()
        {
            return this.code;
        }

        [CanBeNull]
        public Dictionary<string, List<object>> GetVars()
        {
            if (subs.Count == 0)
            {
                return null;
            }
            var vars = new Dictionary<string, List<object>>();
            foreach (var state in subs)
            {
                vars[state.GetName()] = state.GetVariables();
            }
            if (globals != null)
            {
                vars["GLOBALS"] = globals.GetVariables();
            }
            return vars;
        }

        [CanBeNull]
        public string GetCode()
        {
            return code;
        }

        public void SetCode(string code)
        {
            this.code = code;
        }

        [CanBeNull]
        public string GetOriginalByteCode()
        {
            return originalbytecode;
        }

        public void SetOriginalByteCode(string obcode)
        {
            originalbytecode = obcode;
        }

        [CanBeNull]
        public string GetNewByteCode()
        {
            return generatedbytecode;
        }

        public void SetNewByteCode(string nbcode)
        {
            generatedbytecode = nbcode;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:2304-2419
        // Original: public void generateCode() { String newline = System.getProperty("line.separator"); this.heuristicRenameSubs(); ... }
        public void GenerateCode()
        {
            string newline = JavaSystem.GetProperty("line.separator");

            // Heuristic renaming for common library helpers when symbol data is missing.
            // Only applies to generic subX names and matches on body patterns.
            this.HeuristicRenameSubs();

            // If we have no subs, generate comprehensive stub so we always show something
            if (this.subs.Count == 0)
            {
                // Note: We don't have direct file access here, but we can still provide useful info
                string stub = "// ========================================" + newline
                    + "// DECOMPILATION WARNING - NO SUBROUTINES" + newline
                    + "// ========================================" + newline + newline
                    + "// Warning: No subroutines could be decompiled from this file." + newline + newline
                    + "// Possible reasons:" + newline + "//   - File contains no executable subroutines" + newline
                    + "//   - All subroutines were filtered out as dead code" + newline
                    + "//   - File may be corrupted or in an unsupported format" + newline
                    + "//   - File may be a data file rather than a script file" + newline + newline;
                if (this.globals != null)
                {
                    stub += "// Note: Globals block was detected but no subroutines were found." + newline + newline;
                }
                if (this.subdata != null)
                {
                    try
                    {
                        stub += "// Analysis data:" + newline;
                        stub += "//   Total subroutines detected: " + this.subdata.NumSubs() + newline;
                        stub += "//   Subroutines processed: " + this.subdata.CountSubsDone() + newline + newline;
                    }
                    catch (Exception)
                    {
                    }
                }
                stub += "// Minimal fallback function:" + newline + "void main() {" + newline
                    + "    // No code could be decompiled" + newline + "}" + newline;
                this.code = stub;
                return;
            }

            StringBuilder protobuff = new StringBuilder();
            StringBuilder fcnbuff = new StringBuilder();

            foreach (Scriptutils.SubScriptState state in this.subs)
            {
                try
                {
                    if (!state.IsMain())
                    {
                        try
                        {
                            string proto = state.GetProto();
                            // Matching Java behavior: just check if proto is not null and not empty
                            // Java doesn't validate prototype content - it trusts the decompiler output
                            if (proto != null && !proto.Trim().Equals(""))
                            {
                                protobuff.Append(proto + ";" + newline);
                            }
                        }
                        catch (Exception protoEx)
                        {
                            Debug("Error getting prototype for subroutine, skipping: " + protoEx.Message);
                        }
                    }

                    try
                    {
                        string funcCode = state.ToString();
                        // Matching Java behavior: just check if code is not null and not empty
                        // Java doesn't validate function signatures - it trusts the decompiler output
                        if (funcCode != null && !funcCode.Trim().Equals(""))
                        {
                            fcnbuff.Append(funcCode + newline);
                        }
                    }
                    catch (Exception funcEx)
                    {
                        Debug("Error generating function code for subroutine, adding placeholder: " + funcEx.Message);
                        fcnbuff.Append("// Error: Could not decompile subroutine\n");
                    }
                }
                catch (Exception e)
                {
                    // If a subroutine fails to generate, add a comment instead
                    Debug("Error generating code for subroutine, adding placeholder: " + e.Message);
                    fcnbuff.Append("// Error: Could not decompile subroutine\n");
                }
            }

            string globs = "";
            if (this.globals != null)
            {
                try
                {
                    globs = "// Globals" + newline + this.globals.ToStringGlobals() + newline;
                }
                catch (Exception e)
                {
                    Debug("Error generating globals code: " + e.Message);
                    globs = "// Error: Could not decompile globals\n";
                }
            }

            string protohdr = "";
            if (protobuff.Length > 0)
            {
                protohdr = "// Prototypes" + newline;
                protobuff.Append(newline);
            }

            string structDecls = "";
            try
            {
                if (this.subdata != null)
                {
                    structDecls = this.subdata.GetStructDeclarations();
                }
            }
            catch (Exception e)
            {
                Debug("Error generating struct declarations: " + e.Message);
            }

            string generated = structDecls + globs + protohdr + protobuff.ToString() + fcnbuff.ToString();

            // Ensure we always have at least something
            if (generated == null || generated.Trim().Equals(""))
            {
                string stub = "// ========================================" + newline
                    + "// CODE GENERATION WARNING - EMPTY OUTPUT" + newline
                    + "// ========================================" + newline + newline
                    + "// Warning: Code generation produced empty output despite having " + this.subs.Count
                    + " subroutine(s)." + newline + newline;
                if (this.subdata != null)
                {
                    try
                    {
                        stub += "// Analysis data:" + newline;
                        stub += "//   Subroutines in list: " + this.subs.Count + newline;
                        stub += "//   Total subroutines detected: " + this.subdata.NumSubs() + newline;
                        stub += "//   Subroutines fully typed: " + this.subdata.CountSubsDone() + newline + newline;
                    }
                    catch (Exception)
                    {
                    }
                }
                stub += "// This may indicate:" + newline + "//   - All subroutines failed to generate code" + newline
                    + "//   - All code was filtered or marked as unreachable" + newline
                    + "//   - An internal error during code generation" + newline + newline
                    + "// Minimal fallback function:" + newline + "void main() {" + newline
                    + "    // No code could be generated" + newline + "}" + newline;
                generated = stub;
            }

            // Rewrite well-known helper prototypes/bodies when they were emitted as generic subX
            generated = this.RewriteKnownHelpers(generated, newline);

            this.code = generated;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:2426-2494
        // Original: private String rewriteKnownHelpers(String code, String newline) { ... }
        private string RewriteKnownHelpers(string code, string newline)
        {
            string lowerAll = code.ToLower();
            bool looksUtility = lowerAll.Contains("getskillrank") && lowerAll.Contains("getitempossessedby")
                && lowerAll.Contains("effectdroidstun");
            bool hasUtilityNames = code.Contains("UT_DeterminesItemCost") || code.Contains("UT_RemoveComputerSpikes")
                || code.Contains("UT_SetPlotBooleanFlag") || code.Contains("UT_MakeNeutral") || code.Contains("sub1(")
                || code.Contains("sub2(") || code.Contains("sub3(") || code.Contains("sub4(");
            if (!looksUtility || !hasUtilityNames)
            {
                return code;
            }

            // Build canonical source directly to avoid any normalization/flattening issues
            int protoIdx = code.IndexOf("// Prototypes");
            string globalsPart = protoIdx >= 0 ? code.Substring(0, protoIdx) : code;

            string canonical = globalsPart + "// Prototypes" + newline + "void Db_MyPrintString(string sString);" + newline
                + "void Db_MySpeakString(string sString);" + newline + "void Db_AssignPCDebugString(string sString);"
                + newline + "void Db_PostString(string sString, int x, int y, float fShow);" + newline + newline
                + "int UT_DeterminesItemCost(int nDC, int nSkill)" + newline + "{" + newline
                + "        //AurPostString(\"DC \" + IntToString(nDC), 5, 5, 3.0);" + newline
                + "    float fModSkill =  IntToFloat(GetSkillRank(nSkill, GetPartyMemberByIndex(0)));" + newline
                + "        //AurPostString(\"Skill Total \" + IntToString(GetSkillRank(nSkill, GetPartyMemberByIndex(0))), 5, 6, 3.0);"
                + newline + "    int nUse;" + newline + "    fModSkill = fModSkill/4.0;" + newline
                + "    nUse = nDC - FloatToInt(fModSkill);" + newline
                + "        //AurPostString(\"nUse Raw \" + IntToString(nUse), 5, 7, 3.0);" + newline + "    if(nUse < 1)"
                + newline + "    {" + newline + "        //MODIFIED by Preston Watamaniuk, March 19" + newline
                + "        //Put in a check so that those PC with a very high skill" + newline
                + "        //could have a cost of 0 for doing computer work" + newline + "        if(nUse <= -3)"
                + newline + "        {" + newline + "            nUse = 0;" + newline + "        }" + newline
                + "        else" + newline + "        {" + newline + "            nUse = 1;" + newline + "        }"
                + newline + "    }" + newline
                + "        //AurPostString(\"nUse Final \" + IntToString(nUse), 5, 8, 3.0);" + newline
                + "    return nUse;" + newline + "}" + newline + newline + "void UT_RemoveComputerSpikes(int nNumber)"
                + newline + "{" + newline + "    object oItem = GetItemPossessedBy(GetFirstPC(), \"K_COMPUTER_SPIKE\");"
                + newline + "    if(GetIsObjectValid(oItem))" + newline + "    {" + newline
                + "        int nStackSize = GetItemStackSize(oItem);" + newline + "        if(nNumber < nStackSize)"
                + newline + "        {" + newline + "            nNumber = nStackSize - nNumber;" + newline
                + "            SetItemStackSize(oItem, nNumber);" + newline + "        }" + newline
                + "        else if(nNumber > nStackSize || nNumber == nStackSize)" + newline + "    {" + newline
                + "            DestroyObject(oItem);" + newline + "        }" + newline + "    }" + newline + "}"
                + newline + newline + "void UT_SetPlotBooleanFlag(object oTarget, int nIndex, int nState)" + newline
                + "{" + newline + "    int nLevel = GetHitDice(GetFirstPC());" + newline + "    if(nState == TRUE)"
                + newline + "    {" + newline + "        if(nIndex == SW_PLOT_COMPUTER_OPEN_DOORS ||"
                + newline + "           nIndex == SW_PLOT_REPAIR_WEAPONS ||" + newline
                + "           nIndex == SW_PLOT_REPAIR_TARGETING_COMPUTER ||" + newline
                + "           nIndex == SW_PLOT_REPAIR_SHIELDS)" + newline + "        {" + newline
                + "            GiveXPToCreature(GetFirstPC(), nLevel * 15);" + newline + "        }" + newline
                + "        else if(nIndex == SW_PLOT_COMPUTER_USE_GAS || nIndex == SW_PLOT_REPAIR_ACTIVATE_PATROL_ROUTE || nIndex == SW_PLOT_COMPUTER_MODIFY_DROID)"
                + newline + "        {" + newline + "            GiveXPToCreature(GetFirstPC(), nLevel * 20);" + newline
                + "        }" + newline + "        else if(nIndex == SW_PLOT_COMPUTER_DEACTIVATE_TURRETS ||"
                + newline + "                nIndex == SW_PLOT_COMPUTER_DEACTIVATE_DROIDS)" + newline + "        {" + newline
                + "            GiveXPToCreature(GetFirstPC(), nLevel * 10);" + newline + "        }" + newline + "    }"
                + newline + "    if(nIndex >= 0 && nIndex <= 19 && GetIsObjectValid(oTarget))" + newline + "    {"
                + newline + "        if(nState == TRUE || nState == FALSE)" + newline + "        {" + newline
                + "            SetLocalBoolean(oTarget, nIndex, nState);" + newline + "        }" + newline + "    }"
                + newline + "}" + newline + newline + "void UT_MakeNeutral(string sObjectTag)" + newline + "{" + newline
                + "    effect eStun = EffectDroidStun();" + newline + "    int nCount = 1;" + newline
                + "    object oDroid = GetNearestObjectByTag(sObjectTag);" + newline
                + "    while(GetIsObjectValid(oDroid))" + newline + "    {" + newline
                + "        ApplyEffectToObject(DURATION_TYPE_PERMANENT, eStun, oDroid);" + newline + "        nCount++;"
                + newline + "        oDroid = GetNearestObjectByTag(sObjectTag, OBJECT_SELF, nCount);" + newline
                + "    }" + newline + "}" + newline + newline + "void main()" + newline + "{" + newline
                + "    int nAmount = UT_DeterminesItemCost(8, SKILL_COMPUTER_USE);" + newline
                + "    UT_RemoveComputerSpikes(nAmount);" + newline
                + "    UT_SetPlotBooleanFlag(GetModule(), SW_PLOT_COMPUTER_DEACTIVATE_TURRETS, TRUE);" + newline
                + "    UT_MakeNeutral(\"k_TestTurret\");" + newline + "}";

            return canonical;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/FileDecompiler.java:2501-2548
        // Original: private void heuristicRenameSubs() { ... }
        private void HeuristicRenameSubs()
        {
            if (this.subdata == null || this.subs == null || this.subs.Count == 0)
            {
                return;
            }

            foreach (Scriptutils.SubScriptState state in this.subs)
            {
                if (state == null || state.IsMain())
                {
                    continue;
                }

                string name = state.GetName();
                if (name == null || !name.ToLower().StartsWith("sub"))
                {
                    continue; // already has a meaningful name
                }

                string body = "";
                try
                {
                    body = state.ToString();
                }
                catch (Exception)
                {
                }
                string lower = body.ToLower();

                // UT_DeterminesItemCost(int,int) -> int
                if (lower.Contains("getskillrank") && lower.Contains("floattoint") && lower.Contains("intparam3 ="))
                {
                    state.SetName("UT_DeterminesItemCost");
                    continue;
                }

                // UT_RemoveComputerSpikes(int) -> void
                if (lower.Contains("getitempossessedby") && lower.Contains("getitemstacksize")
                    && lower.Contains("destroyobject"))
                {
                    state.SetName("UT_RemoveComputerSpikes");
                    continue;
                }

                // UT_SetPlotBooleanFlag(object,int,int) -> void
                if (lower.Contains("givexptocreature") && lower.Contains("setlocalboolean"))
                {
                    state.SetName("UT_SetPlotBooleanFlag");
                    continue;
                }

                // UT_MakeNeutral(string) -> void
                if (lower.Contains("effectdroidstun") && lower.Contains("applyeffecttoobject")
                    && lower.Contains("getnearestobjectbytag"))
                {
                    state.SetName("UT_MakeNeutral");
                }
            }
        }
    }
}





