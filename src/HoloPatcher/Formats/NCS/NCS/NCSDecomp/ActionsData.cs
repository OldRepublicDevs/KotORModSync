// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ActionsData.java:22-219
// Original: public class ActionsData
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.Utils;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;
using UtilsType = KOTORModSync.Formats.NCS.NCSDecomp.Utils.Type;
namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public class ActionsData
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ActionsData.java:23-26
        // Original: /** Ordered list of parsed actions (index matches opcode value). */ private final List<Action> actions; /** Reader over the nwscript actions block. */ private final BufferedReader actionsreader;
        /** Ordered list of parsed actions (index matches opcode value). */
        private List<object> actions;
        /** Reader over the nwscript actions block. */
        private StreamReader actionsreader;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ActionsData.java:28-38
        // Original: public ActionsData(BufferedReader actionsreader) throws IOException { this.actionsreader = actionsreader; this.actions = new ArrayList<>(877); this.readActions(); }
        public ActionsData(StreamReader actionsreader)
        {
            this.actionsreader = actionsreader;
            this.actions = new List<object>(877);
            this.ReadActions();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ActionsData.java:46-53
        // Original: public String getAction(int index) { try { ActionsData.Action action = this.actions.get(index); return action.toString(); } catch (IndexOutOfBoundsException var3) { throw new RuntimeException("Invalid action call: action " + Integer.toString(index)); } }
        public virtual string GetAction(int index)
        {
            try
            {
                Action action = (Action)this.actions[index];
                return action.ToString();
            }
            catch (IndexOutOfRangeException)
            {
                throw new Exception("Invalid action call: action " + index.ToString());
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ActionsData.java:58-168
        // Original: private void readActions() throws IOException { ... binds signatures to their explicit numeric indices in comment headers ... }
        private void ReadActions()
        {
            // KOTOR/TSL nwscript files interleave documentation comments like:
            //   // 768. GetScriptParameter
            // followed by a signature line:
            //   int GetScriptParameter( int nIndex );
            //
            // Earlier implementations appended every signature line after the first "// 0",
            // assuming indices were contiguous and that no other declarations existed.
            // That is brittle and can desync action indices, breaking stack typing and
            // round-trip fidelity. Instead, bind signatures to their explicit numeric
            // indices in the comment headers.
            // Only treat real action headers as indices. Many nwscript files contain
            // enumerated doc lists like "// 6) ..." which must NOT be treated as
            // an action index, otherwise signatures get mis-assigned (breaks decompile).
            // Accept common header styles:
            // - "// 123:" (K1/K2)
            // - "// 123." (some vendor variants)
            // - "// 123"  (some tool-distributed nwscript files)
            // Reject enumerated lists like "// 6) ..." which otherwise desync indices.
            Pattern header = Pattern.Compile("^\\s*//\\s*(\\d+)\\s*(?:[\\.:]\\s*.*)?$");
            Pattern sig = Pattern.Compile("^\\s*(\\w+)\\s+(\\w+)\\s*\\((.*)\\)\\s*;?.*");

            string str;
            bool started = false;
            int pendingIndex = -1;
            int maxIndex = -1;

            while ((str = this.actionsreader.ReadLine()) != null)
            {
                Matcher h = header.Matcher(str);
                if (h.Matches())
                {
                    int idx;
                    try
                    {
                        idx = int.Parse(h.Group(1));
                    }
                    catch (FormatException)
                    {
                        continue;
                    }
                    // We only consider ourselves "in" the actions table once we see index 0.
                    if (idx == 0)
                    {
                        started = true;
                    }
                    if (started)
                    {
                        pendingIndex = idx;
                        if (idx > maxIndex)
                        {
                            maxIndex = idx;
                        }
                    }
                    continue;
                }

                if (!started)
                {
                    continue;
                }

                // Skip comments/blank lines between header and signature.
                if (string.IsNullOrWhiteSpace(str) || str.Trim().StartsWith("//"))
                {
                    continue;
                }

                // Bind the next signature line to the last seen numeric header.
                if (pendingIndex >= 0)
                {
                    Matcher m = sig.Matcher(str);
                    if (m.Matches())
                    {
                        // Ensure list is large enough
                        while (this.actions.Count <= pendingIndex)
                        {
                            this.actions.Add(null);
                        }
                        this.actions[pendingIndex] = new Action(m.Group(1), m.Group(2), m.Group(3));
                    }
                    pendingIndex = -1;
                }
            }

            // Ensure list size is at least maxIndex+1 (preserve stable indexing).
            while (this.actions.Count <= maxIndex)
            {
                this.actions.Add(null);
            }

            ((JavaPrintStream)JavaSystem.@out).Println("read actions.  There were " + this.actions.Count.ToString());
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ActionsData.java:76-81
        // Original: public Type getReturnType(int index)
        public virtual UtilsType GetReturnType(int index)
        {
            if (index < 0 || index >= this.actions.Count)
            {
                throw new Exception("Invalid action index: " + index + " (actions list size: " + this.actions.Count + ")");
            }
            return ((Action)this.actions[index]).ReturnType();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ActionsData.java:83-88
        // Original: public String getName(int index)
        public virtual string GetName(int index)
        {
            if (index < 0 || index >= this.actions.Count)
            {
                throw new Exception("Invalid action index: " + index + " (actions list size: " + this.actions.Count + ")");
            }
            return ((Action)this.actions[index]).Name();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ActionsData.java:90-95
        // Original: public List<Type> getParamTypes(int index) { if (index < 0 || index >= this.actions.size()) { throw new RuntimeException("Invalid action index: " + index + " (actions list size: " + this.actions.size() + ")"); } return this.actions.get(index).params(); }
        public virtual List<object> GetParamTypes(int index)
        {
            if (index < 0 || index >= this.actions.Count)
            {
                throw new Exception("Invalid action index: " + index + " (actions list size: " + this.actions.Count + ")");
            }
            return ((Action)this.actions[index]).Params();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ActionsData.java:97-102
        // Original: public List<String> getDefaultValues(int index)
        public virtual List<string> GetDefaultValues(int index)
        {
            if (index < 0 || index >= this.actions.Count)
            {
                throw new Exception("Invalid action index: " + index + " (actions list size: " + this.actions.Count + ")");
            }
            return ((Action)this.actions[index]).DefaultValues();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ActionsData.java:104-109
        // Original: public int getRequiredParamCount(int index)
        public virtual int GetRequiredParamCount(int index)
        {
            if (index < 0 || index >= this.actions.Count)
            {
                throw new Exception("Invalid action index: " + index + " (actions list size: " + this.actions.Count + ")");
            }
            return ((Action)this.actions[index]).RequiredParamCount();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ActionsData.java:114-188
        // Original: public static class Action
        public class Action
        {
            private string name;
            private UtilsType returntype;
            private int paramsize;
            private List<object> paramList;
            private List<string> defaultValues;
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ActionsData.java:128-146
            // Original: public Action(String type, String name, String params)
            public Action(string type, string name, string @params)
            {
                this.name = name;
                this.returntype = UtilsType.ParseType(type);
                this.paramList = new List<object>();
                this.defaultValues = new List<string>();
                this.paramsize = 0;
                Pattern p = Pattern.Compile("\\s*(\\w+)\\s+\\w+(\\s*=\\s*(\\S+))?\\s*");
                String[] tokens = @params.Split(",");
                for (int i = 0; i < tokens.Length; ++i)
                {
                    Matcher m = p.Matcher(tokens[i]);
                    if (m.Matches())
                    {
                        this.paramList.Add(new UtilsType(m.Group(1)));
                        string defaultValue = m.Group(3);
                        this.defaultValues.Add(defaultValue != null ? defaultValue.Trim() : null);
                        this.paramsize += UtilsType.TypeSize(m.Group(1));
                    }
                }
            }

            public override string ToString()
            {
                return "\"" + this.name + "\" " + this.returntype.ToValueString() + " " + this.paramsize.ToString();
            }

            public virtual List<object> Params()
            {
                return this.paramList;
            }

            public virtual UtilsType ReturnType()
            {
                return this.returntype;
            }

            public virtual int Paramsize()
            {
                return this.paramsize;
            }

            public virtual string Name()
            {
                return this.name;
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ActionsData.java:173-176
            // Original: public List<String> defaultValues()
            public virtual List<string> DefaultValues()
            {
                return this.defaultValues;
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/ActionsData.java:178-187
            // Original: public int requiredParamCount()
            public virtual int RequiredParamCount()
            {
                int count = 0;
                for (int i = 0; i < this.defaultValues.Count; i++)
                {
                    if (this.defaultValues[i] == null)
                    {
                        count = i + 1;
                    }
                }
                return count;
            }
        }
    }
}




