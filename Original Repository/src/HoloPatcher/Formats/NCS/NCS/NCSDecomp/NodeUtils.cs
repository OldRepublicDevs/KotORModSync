// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeUtils.java:16-952
// Original: public final class NodeUtils
using System;
using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;


namespace KOTORModSync.Formats.NCS.NCSDecomp.Utils
{
    public sealed class NodeUtils
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeUtils.java:17-18
        // Original: public static final int CMDSIZE_JUMP = 6; public static final int CMDSIZE_RETN = 2;
        public static readonly int CMDSIZE_JUMP = 6;
        public static readonly int CMDSIZE_RETN = 2;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeUtils.java:20-29
        // Original: public static boolean isStoreStackNode(Node node) { ... }
        public static bool IsStoreStackNode(Node node)
        {
            if (typeof(ALogiiCmd).IsInstanceOfType(node))
            {
                ALogiiCommand lnode = (ALogiiCommand)((ALogiiCmd)node).GetLogiiCommand();
                if (typeof(AOrLogiiOp).IsInstanceOfType(lnode.GetLogiiOp()))
                {
                    return false;
                }
            }

            return true;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeUtils.java:112-116
        // Original: public static boolean isJzPastOne(Node node) { return AConditionalJumpCommand.class.isInstance(node) && AZeroJumpIf.class.isInstance(((AConditionalJumpCommand)node).getJumpIf()) ? Integer.parseInt(((AConditionalJumpCommand)node).getOffset().getText()) == 12 : false; }
        public static bool IsJzPastOne(Node node)
        {
            return typeof(AConditionalJumpCommand).IsInstanceOfType(node) && typeof(AZeroJumpIf).IsInstanceOfType(((AConditionalJumpCommand)node).GetJumpIf()) ? Integer.ParseInt(((AConditionalJumpCommand)node).GetOffset().GetText()) == 12 : false;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeUtils.java:118-120
        // Original: public static boolean isJz(Node node) { return AConditionalJumpCommand.class.isInstance(node) ? AZeroJumpIf.class.isInstance(((AConditionalJumpCommand)node).getJumpIf()) : false; }
        public static bool IsJz(Node node)
        {
            return typeof(AConditionalJumpCommand).IsInstanceOfType(node) ? typeof(AZeroJumpIf).IsInstanceOfType(((AConditionalJumpCommand)node).GetJumpIf()) : false;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeUtils.java:122-142
        // Original: public static boolean isCommandNode(Node node) { return ... }
        public static bool IsCommandNode(Node node)
        {
            return typeof(AConditionalJumpCommand).IsInstanceOfType(node) || typeof(AJumpCommand).IsInstanceOfType(node) || typeof(AJumpToSubroutine).IsInstanceOfType(node) || typeof(AReturn).IsInstanceOfType(node) || typeof(ACopyDownSpCommand).IsInstanceOfType(node) || typeof(ACopyTopSpCommand).IsInstanceOfType(node) || typeof(ACopyDownBpCommand).IsInstanceOfType(node) || typeof(ACopyTopBpCommand).IsInstanceOfType(node) || typeof(AMoveSpCommand).IsInstanceOfType(node) || typeof(ARsaddCommand).IsInstanceOfType(node) || typeof(AConstCommand).IsInstanceOfType(node) || typeof(AActionCommand).IsInstanceOfType(node) || typeof(ALogiiCommand).IsInstanceOfType(node) || typeof(ABinaryCommand).IsInstanceOfType(node) || typeof(AUnaryCommand).IsInstanceOfType(node) || typeof(AStackCommand).IsInstanceOfType(node) || typeof(ADestructCommand).IsInstanceOfType(node) || typeof(ABpCommand).IsInstanceOfType(node) || typeof(AStoreStateCommand).IsInstanceOfType(node);
        }

        public static int GetCommandPos(Node node)
        {
            // Helper to safely get position from a token, returning -1 if null or invalid
            int SafeGetPosFromToken(object tokenObj)
            {
                if (tokenObj == null) return -1;
                try
                {
                    // Token is in AST namespace
                    if (tokenObj is AST.Token token)
                    {
                        string text = token.GetText();
                        if (text == null || text.Length == 0) return -1;
                        return Integer.ParseInt(text);
                    }
                    return -1;
                }
                catch (Exception)
                {
                    return -1;
                }
            }

            // Check root namespace types first
            if (typeof(AConditionalJumpCommand).IsInstanceOfType(node))
            {
                var posToken = ((AConditionalJumpCommand)node).GetPos();
                return SafeGetPosFromToken(posToken);
            }

            if (typeof(AJumpCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AJumpCommand)node).GetPos());
            }

            if (typeof(AJumpToSubroutine).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AJumpToSubroutine)node).GetPos());
            }

            if (typeof(AReturn).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AReturn)node).GetPos());
            }

            if (typeof(ACopyDownSpCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((ACopyDownSpCommand)node).GetPos());
            }

            if (typeof(ACopyTopSpCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((ACopyTopSpCommand)node).GetPos());
            }

            if (typeof(ACopyDownBpCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((ACopyDownBpCommand)node).GetPos());
            }

            if (typeof(ACopyTopBpCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((ACopyTopBpCommand)node).GetPos());
            }

            if (typeof(AMoveSpCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AMoveSpCommand)node).GetPos());
            }

            if (typeof(ARsaddCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((ARsaddCommand)node).GetPos());
            }

            if (typeof(AConstCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AConstCommand)node).GetPos());
            }

            if (typeof(AActionCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AActionCommand)node).GetPos());
            }

            if (typeof(ALogiiCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((ALogiiCommand)node).GetPos());
            }

            if (typeof(ABinaryCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((ABinaryCommand)node).GetPos());
            }

            if (typeof(AUnaryCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AUnaryCommand)node).GetPos());
            }

            if (typeof(AStackCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AStackCommand)node).GetPos());
            }

            if (typeof(ADestructCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((ADestructCommand)node).GetPos());
            }

            if (typeof(ABpCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((ABpCommand)node).GetPos());
            }

            if (typeof(AStoreStateCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AStoreStateCommand)node).GetPos());
            }

            // Check AST namespace types (from NcsToAstConverter)
            if (typeof(AST.AConditionalJumpCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AST.AConditionalJumpCommand)node).GetPos());
            }

            if (typeof(AST.AJumpCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AST.AJumpCommand)node).GetPos());
            }

            if (typeof(AST.AJumpToSubroutine).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AST.AJumpToSubroutine)node).GetPos());
            }

            if (typeof(AST.AReturn).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AST.AReturn)node).GetPos());
            }

            if (typeof(AST.ACopyDownSpCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AST.ACopyDownSpCommand)node).GetPos());
            }

            if (typeof(AST.ACopyTopSpCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AST.ACopyTopSpCommand)node).GetPos());
            }

            if (typeof(AST.ACopyDownBpCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AST.ACopyDownBpCommand)node).GetPos());
            }

            if (typeof(AST.ACopyTopBpCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AST.ACopyTopBpCommand)node).GetPos());
            }

            if (typeof(AST.AMoveSpCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AST.AMoveSpCommand)node).GetPos());
            }

            if (typeof(AST.ARsaddCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AST.ARsaddCommand)node).GetPos());
            }

            if (typeof(AST.AConstCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AST.AConstCommand)node).GetPos());
            }

            if (typeof(AST.AActionCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AST.AActionCommand)node).GetPos());
            }

            if (typeof(AST.ALogiiCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AST.ALogiiCommand)node).GetPos());
            }

            if (typeof(AST.ABinaryCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AST.ABinaryCommand)node).GetPos());
            }

            if (typeof(AST.AUnaryCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AST.AUnaryCommand)node).GetPos());
            }

            if (typeof(AST.ADestructCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AST.ADestructCommand)node).GetPos());
            }

            if (typeof(AST.ABpCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AST.ABpCommand)node).GetPos());
            }

            if (typeof(AST.AStoreStateCommand).IsInstanceOfType(node))
            {
                return SafeGetPosFromToken(((AST.AStoreStateCommand)node).GetPos());
            }

            return -1;
        }

        public static int GetJumpDestinationPos(Node node)
        {
            if (typeof(AConditionalJumpCommand).IsInstanceOfType(node))
            {
                return Integer.ParseInt(((AConditionalJumpCommand)node).GetPos().GetText()) + Integer.ParseInt(((AConditionalJumpCommand)node).GetOffset().GetText());
            }

            if (typeof(AJumpCommand).IsInstanceOfType(node))
            {
                return Integer.ParseInt(((AJumpCommand)node).GetPos().GetText()) + Integer.ParseInt(((AJumpCommand)node).GetOffset().GetText());
            }

            if (typeof(AJumpToSubroutine).IsInstanceOfType(node))
            {
                return Integer.ParseInt(((AJumpToSubroutine)node).GetPos().GetText()) + Integer.ParseInt(((AJumpToSubroutine)node).GetOffset().GetText());
            }

            return -1;
        }

        public static bool IsEqualityOp(ABinaryCommand node)
        {
            PBinaryOp op = node.GetBinaryOp();
            return typeof(AEqualBinaryOp).IsInstanceOfType(op) || typeof(ANequalBinaryOp).IsInstanceOfType(op);
        }

        public static bool IsConditionalOp(ABinaryCommand node)
        {
            PBinaryOp op = node.GetBinaryOp();
            return typeof(AEqualBinaryOp).IsInstanceOfType(op) || typeof(ANequalBinaryOp).IsInstanceOfType(op) || typeof(ALtBinaryOp).IsInstanceOfType(op) || typeof(ALeqBinaryOp).IsInstanceOfType(op) || typeof(AGtBinaryOp).IsInstanceOfType(op) || typeof(AGeqBinaryOp).IsInstanceOfType(op);
        }

        public static bool IsArithmeticOp(ABinaryCommand node)
        {
            PBinaryOp op = node.GetBinaryOp();
            return typeof(AAddBinaryOp).IsInstanceOfType(op) || typeof(ASubBinaryOp).IsInstanceOfType(op) || typeof(ADivBinaryOp).IsInstanceOfType(op) || typeof(AMulBinaryOp).IsInstanceOfType(op) || typeof(AModBinaryOp).IsInstanceOfType(op) || typeof(AShleftBinaryOp).IsInstanceOfType(op) || typeof(AShrightBinaryOp).IsInstanceOfType(op) || typeof(AUnrightBinaryOp).IsInstanceOfType(op);
        }

        public static bool IsVectorAllowedOp(ABinaryCommand node)
        {
            PBinaryOp op = node.GetBinaryOp();
            return typeof(AAddBinaryOp).IsInstanceOfType(op) || typeof(ASubBinaryOp).IsInstanceOfType(op) || typeof(ADivBinaryOp).IsInstanceOfType(op) || typeof(AMulBinaryOp).IsInstanceOfType(op);
        }

        public static string GetOp(AUnaryCommand node)
        {
            PUnaryOp op = node.GetUnaryOp();
            if (typeof(ANegUnaryOp).IsInstanceOfType(op))
            {
                return "-";
            }

            if (typeof(ACompUnaryOp).IsInstanceOfType(op))
            {
                return "~";
            }

            if (typeof(ANotUnaryOp).IsInstanceOfType(op))
            {
                return "!";
            }

            throw new Exception("unknown unary op");
        }

        public static string GetOp(ABinaryCommand node)
        {
            PBinaryOp op = node.GetBinaryOp();
            if (typeof(AAddBinaryOp).IsInstanceOfType(op))
            {
                return "+";
            }

            if (typeof(ASubBinaryOp).IsInstanceOfType(op))
            {
                return "-";
            }

            if (typeof(ADivBinaryOp).IsInstanceOfType(op))
            {
                return "/";
            }

            if (typeof(AMulBinaryOp).IsInstanceOfType(op))
            {
                return "*";
            }

            if (typeof(AModBinaryOp).IsInstanceOfType(op))
            {
                return "%";
            }

            if (typeof(AShleftBinaryOp).IsInstanceOfType(op))
            {
                return "<<";
            }

            if (typeof(AShrightBinaryOp).IsInstanceOfType(op))
            {
                return ">>";
            }

            if (typeof(AUnrightBinaryOp).IsInstanceOfType(op))
            {
                throw new Exception("found an unsigned bit shift.");
            }

            if (typeof(AEqualBinaryOp).IsInstanceOfType(op))
            {
                return "==";
            }

            if (typeof(ANequalBinaryOp).IsInstanceOfType(op))
            {
                return "!=";
            }

            if (typeof(ALtBinaryOp).IsInstanceOfType(op))
            {
                return "<";
            }

            if (typeof(ALeqBinaryOp).IsInstanceOfType(op))
            {
                return "<=";
            }

            if (typeof(AGtBinaryOp).IsInstanceOfType(op))
            {
                return ">";
            }

            if (typeof(AGeqBinaryOp).IsInstanceOfType(op))
            {
                return ">=";
            }

            throw new Exception("unknown binary op");
        }

        public static string GetOp(ALogiiCommand node)
        {
            PLogiiOp op = node.GetLogiiOp();
            if (typeof(AAndLogiiOp).IsInstanceOfType(op))
            {
                return "&&";
            }

            if (typeof(AOrLogiiOp).IsInstanceOfType(op))
            {
                return "||";
            }

            if (typeof(AInclOrLogiiOp).IsInstanceOfType(op))
            {
                return "|";
            }

            if (typeof(AExclOrLogiiOp).IsInstanceOfType(op))
            {
                return "^";
            }

            if (typeof(ABitAndLogiiOp).IsInstanceOfType(op))
            {
                return "&";
            }

            throw new Exception("unknown logii op");
        }

        public static string GetOp(AStackCommand node)
        {
            PStackOp op = node.GetStackOp();
            if (typeof(ADecispStackOp).IsInstanceOfType(op) || typeof(ADecibpStackOp).IsInstanceOfType(op))
            {
                return "--";
            }

            if (typeof(AIncispStackOp).IsInstanceOfType(op) || typeof(AIncibpStackOp).IsInstanceOfType(op))
            {
                return "++";
            }

            throw new Exception("unknown relative-to-stack unary modifier op");
        }

        public static bool IsGlobalStackOp(AStackCommand node)
        {
            PStackOp op = node.GetStackOp();
            return typeof(AIncibpStackOp).IsInstanceOfType(op) || typeof(ADecibpStackOp).IsInstanceOfType(op);
        }

        public static int GetParam1Size(ABinaryCommand node)
        {
            Type type = GetType(node);
            if (type.Equals((byte)59) || type.Equals((byte)58))
            {
                return 3;
            }

            return 1;
        }

        public static int GetParam2Size(ABinaryCommand node)
        {
            Type type = GetType(node);
            if (type.Equals((byte)60) || type.Equals((byte)58))
            {
                return 3;
            }

            return 1;
        }

        public static int GetResultSize(ABinaryCommand node)
        {
            Type type = GetType(node);
            if (type.Equals((byte)60) || type.Equals((byte)59) || type.Equals((byte)58))
            {
                return 3;
            }

            return 1;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeUtils.java:326-333
        // Original: public static Long getIntConstValue(AConstCommand node)
        public static long GetIntConstValue(AConstCommand node)
        {
            PConstant pconst = node.GetConstant();
            Type type = GetType(node);
            if (type.ByteValue() != 3)
            {
                throw new Exception("Expected int const type (3), got " + type);
            }
            return long.Parse(((AIntConstant)pconst).GetIntegerConstant().GetText());
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeUtils.java:335-352
        // Original: public static Float getFloatConstValue(AConstCommand node)
        public static float GetFloatConstValue(AConstCommand node)
        {
            PConstant pconst = node.GetConstant();
            Type type = GetType(node);
            if (type.ByteValue() != 4)
            {
                throw new Exception("Expected float const type (4), got " + type);
            }
            // Handle case where parser created AIntConstant instead of AFloatConstant
            // This can happen when the float value is a whole number or due to parser quirks
            if (typeof(AIntConstant).IsInstanceOfType(pconst))
            {
                // Parse as integer first, then convert to float
                long intValue = long.Parse(((AIntConstant)pconst).GetIntegerConstant().GetText());
                return (float)intValue;
            }
            else if (typeof(AFloatConstant).IsInstanceOfType(pconst))
            {
                return float.Parse(((AFloatConstant)pconst).GetFloatConstant().GetText());
            }
            else
            {
                throw new Exception("Expected AFloatConstant or AIntConstant, got " + pconst.GetType().Name);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeUtils.java:354-360
        // Original: public static String getStringConstValue(AConstCommand node)
        public static string GetStringConstValue(AConstCommand node)
        {
            PConstant pconst = node.GetConstant();
            Type type = GetType(node);
            if (type.ByteValue() != 5)
            {
                throw new Exception("Expected string const type (5), got " + type);
            }
            return ((AStringConstant)pconst).GetStringLiteral().GetText();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/NodeUtils.java:363-370
        // Original: public static Integer getObjectConstValue(AConstCommand node)
        public static int GetObjectConstValue(AConstCommand node)
        {
            PConstant pconst = node.GetConstant();
            Type type = GetType(node);
            if (type.ByteValue() != 6)
            {
                throw new Exception("Expected object const type (6), got " + type);
            }
            return Integer.ParseInt(((AIntConstant)pconst).GetIntegerConstant().GetText());
        }

        public static int GetSubEnd(ASubroutine sub)
        {
            return GetCommandPos(sub.GetReturn());
        }

        public static int GetActionId(AActionCommand node)
        {
            return Integer.ParseInt(node.GetId().GetText());
        }

        public static int GetActionParamCount(AActionCommand node)
        {
            return Integer.ParseInt(node.GetArgCount().GetText());
        }

        public static string GetActionName(AActionCommand node, ActionsData actions)
        {
            if (actions == null)
            {
                return "action_" + GetActionId(node);
            }

            string name = actions.GetName(GetActionId(node));
            return (name != null) ? name : "action_" + GetActionId(node);
        }

        public static List<object> GetActionParamTypes(AActionCommand node, ActionsData actions)
        {
            if (actions == null)
            {
                int count = GetActionParamCount(node);
                List<object> defaultTypes = new List<object>();
                for (int i = 0; i < count; ++i)
                {
                    defaultTypes.Add(new Type((byte)3));
                }

                return defaultTypes;
            }

            List<object> types = actions.GetParamTypes(GetActionId(node));
            if (types == null)
            {
                int count = GetActionParamCount(node);
                List<object> defaultTypes = new List<object>();
                for (int i = 0; i < count; ++i)
                {
                    defaultTypes.Add(new Type((byte)3));
                }

                return defaultTypes;
            }

            return types;
        }

        public static int ActionRemoveElementCount(AActionCommand node, ActionsData actions)
        {
            List<object> types = GetActionParamTypes(node, actions);
            int count = GetActionParamCount(node);
            int remove = 0;
            for (int i = 0; i < count && i < types.Count; ++i)
            {
                remove += ((Type)types[i]).TypeSize();
            }

            return StackSizeToPos(remove);
        }

        public static int StackOffsetToPos(TIntegerConstant offset)
        {
            return -Integer.ParseInt(offset.GetText()) / 4;
        }

        public static int StackSizeToPos(TIntegerConstant offset)
        {
            return Integer.ParseInt(offset.GetText()) / 4;
        }

        public static int StackSizeToPos(int offset)
        {
            return offset / 4;
        }

        public static Node GetCommandChild(Node node)
        {
            if (IsCommandNode(node))
            {
                return node;
            }

            if (typeof(ASubroutine).IsInstanceOfType(node))
            {
                return GetCommandChild(((ASubroutine)node).GetCommandBlock());
            }

            if (typeof(ACommandBlock).IsInstanceOfType(node))
            {
                return GetCommandChild((Node)((ACommandBlock)node).GetCmd()[0]);
            }

            if (typeof(AAddVarCmd).IsInstanceOfType(node))
            {
                return GetCommandChild(((AAddVarCmd)node).GetRsaddCommand());
            }

            if (typeof(ARsaddCmd).IsInstanceOfType(node))
            {
                return GetCommandChild(((ARsaddCmd)node).GetRsaddCommand());
            }

            if (typeof(AActionJumpCmd).IsInstanceOfType(node))
            {
                return GetCommandChild(((AActionJumpCmd)node).GetStoreStateCommand());
            }

            if (typeof(AConstCmd).IsInstanceOfType(node))
            {
                return GetCommandChild(((AConstCmd)node).GetConstCommand());
            }

            if (typeof(ACopydownspCmd).IsInstanceOfType(node))
            {
                return GetCommandChild(((ACopydownspCmd)node).GetCopyDownSpCommand());
            }

            if (typeof(ACopytopspCmd).IsInstanceOfType(node))
            {
                return GetCommandChild(((ACopytopspCmd)node).GetCopyTopSpCommand());
            }

            if (typeof(ACopydownbpCmd).IsInstanceOfType(node))
            {
                return GetCommandChild(((ACopydownbpCmd)node).GetCopyDownBpCommand());
            }

            if (typeof(ACopytopbpCmd).IsInstanceOfType(node))
            {
                return GetCommandChild(((ACopytopbpCmd)node).GetCopyTopBpCommand());
            }

            if (typeof(ACondJumpCmd).IsInstanceOfType(node))
            {
                return GetCommandChild(((ACondJumpCmd)node).GetConditionalJumpCommand());
            }

            if (typeof(AJumpCmd).IsInstanceOfType(node))
            {
                return GetCommandChild(((AJumpCmd)node).GetJumpCommand());
            }

            if (typeof(AJumpSubCmd).IsInstanceOfType(node))
            {
                return GetCommandChild(((AJumpSubCmd)node).GetJumpToSubroutine());
            }

            if (typeof(AMovespCmd).IsInstanceOfType(node))
            {
                return GetCommandChild(((AMovespCmd)node).GetMoveSpCommand());
            }

            if (typeof(ALogiiCmd).IsInstanceOfType(node))
            {
                return GetCommandChild(((ALogiiCmd)node).GetLogiiCommand());
            }

            if (typeof(AUnaryCmd).IsInstanceOfType(node))
            {
                return GetCommandChild(((AUnaryCmd)node).GetUnaryCommand());
            }

            if (typeof(ABinaryCmd).IsInstanceOfType(node))
            {
                return GetCommandChild(((ABinaryCmd)node).GetBinaryCommand());
            }

            if (typeof(ADestructCmd).IsInstanceOfType(node))
            {
                return GetCommandChild(((ADestructCmd)node).GetDestructCommand());
            }

            if (typeof(ABpCmd).IsInstanceOfType(node))
            {
                return GetCommandChild(((ABpCmd)node).GetBpCommand());
            }

            if (typeof(AActionCmd).IsInstanceOfType(node))
            {
                return GetCommandChild(((AActionCmd)node).GetActionCommand());
            }

            if (typeof(AStackOpCmd).IsInstanceOfType(node))
            {
                return GetCommandChild(((AStackOpCmd)node).GetStackCommand());
            }

            if (typeof(AReturnCmd).IsInstanceOfType(node))
            {
                return GetCommandChild(((AReturnCmd)node).GetReturn());
            }

            if (typeof(AStoreStateCmd).IsInstanceOfType(node))
            {
                return GetCommandChild(((AStoreStateCmd)node).GetStoreStateCommand());
            }

            throw new Exception("unexpected node type " + node);
        }

        public static Node GetPreviousCommand(Node node, NodeAnalysisData nodedata)
        {
            if (typeof(AReturn).IsInstanceOfType(node))
            {
                Debug("class " + node.Parent()?.GetType()?.ToString() ?? "null");
                ACommandBlock ablock = (ACommandBlock)((ASubroutine)node.Parent()).GetCommandBlock();
                var cmdList = ablock.GetCmd();
                return GetCommandChild((Node)cmdList[cmdList.Count - 1]);
            }

            Node up;
            for (up = node.Parent(); !typeof(ACommandBlock).IsInstanceOfType(up) && up != null; up = up.Parent())
            {
            }

            if (up == null)
            {
                return null;
            }

            int searchPos = nodedata.GetPos(node);
            ListIterator it = ((ACommandBlock)up).GetCmd().ListIterator();
            while (it.HasNext())
            {
                if (nodedata.GetPos((Node)it.Next()) == searchPos)
                {
                    it.Previous();
                    return GetCommandChild((Node)it.Previous());
                }
            }

            return null;
        }

        public static Node GetNextCommand(Node node, NodeAnalysisData nodedata)
        {
            Node up;
            for (up = node.Parent(); !typeof(ACommandBlock).IsInstanceOfType(up); up = up.Parent())
            {
            }

            int searchPos = nodedata.GetPos(node);
            var cmdList = ((ACommandBlock)up).GetCmd();
            foreach (PCmd cmd in cmdList)
            {
                Node next = (Node)cmd;
                if (nodedata.GetPos(next) == searchPos)
                {
                    int nextIndex = cmdList.IndexOf(cmd) + 1;
                    if (nextIndex < cmdList.Count)
                    {
                        return GetCommandChild((Node)cmdList[nextIndex]);
                    }

                    return null;
                }
            }

            return null;
        }

        public static bool IsReturn(Node node)
        {
            return typeof(AReturnCmd).IsInstanceOfType(node) || typeof(AReturn).IsInstanceOfType(node);
        }

        public static Type GetType(Node node)
        {
            if (typeof(AConditionalJumpCommand).IsInstanceOfType(node))
            {
                return new Type(Byte.ParseByte(((AConditionalJumpCommand)node).GetType().GetText()));
            }

            if (typeof(AJumpCommand).IsInstanceOfType(node))
            {
                return new Type(Byte.ParseByte(((AJumpCommand)node).GetType().GetText()));
            }

            if (typeof(AJumpToSubroutine).IsInstanceOfType(node))
            {
                return new Type(Byte.ParseByte(((AJumpToSubroutine)node).GetType().GetText()));
            }

            if (typeof(AReturn).IsInstanceOfType(node))
            {
                return new Type(Byte.ParseByte(((AReturn)node).GetType().GetText()));
            }

            if (typeof(ACopyDownSpCommand).IsInstanceOfType(node))
            {
                return new Type(Byte.ParseByte(((ACopyDownSpCommand)node).GetType().GetText()));
            }

            if (typeof(ACopyTopSpCommand).IsInstanceOfType(node))
            {
                return new Type(Byte.ParseByte(((ACopyTopSpCommand)node).GetType().GetText()));
            }

            if (typeof(ACopyDownBpCommand).IsInstanceOfType(node))
            {
                return new Type(Byte.ParseByte(((ACopyDownBpCommand)node).GetType().GetText()));
            }

            if (typeof(ACopyTopBpCommand).IsInstanceOfType(node))
            {
                return new Type(Byte.ParseByte(((ACopyTopBpCommand)node).GetType().GetText()));
            }

            if (typeof(AMoveSpCommand).IsInstanceOfType(node))
            {
                return new Type(Byte.ParseByte(((AMoveSpCommand)node).GetType().GetText()));
            }

            if (typeof(ARsaddCommand).IsInstanceOfType(node))
            {
                return new Type(Byte.ParseByte(((ARsaddCommand)node).GetType().GetText()));
            }

            if (typeof(AConstCommand).IsInstanceOfType(node))
            {
                return new Type(Byte.ParseByte(((AConstCommand)node).GetType().GetText()));
            }

            if (typeof(AActionCommand).IsInstanceOfType(node))
            {
                return new Type(Byte.ParseByte(((AActionCommand)node).GetType().GetText()));
            }

            if (typeof(ALogiiCommand).IsInstanceOfType(node))
            {
                return new Type(Byte.ParseByte(((ALogiiCommand)node).GetType().GetText()));
            }

            if (typeof(ABinaryCommand).IsInstanceOfType(node))
            {
                return new Type(Byte.ParseByte(((ABinaryCommand)node).GetType().GetText()));
            }

            if (typeof(AUnaryCommand).IsInstanceOfType(node))
            {
                return new Type(Byte.ParseByte(((AUnaryCommand)node).GetType().GetText()));
            }

            if (typeof(AStackCommand).IsInstanceOfType(node))
            {
                return new Type(Byte.ParseByte(((AStackCommand)node).GetType().GetText()));
            }

            if (typeof(ADestructCommand).IsInstanceOfType(node))
            {
                return new Type(Byte.ParseByte(((ADestructCommand)node).GetType().GetText()));
            }

            if (typeof(ABpCommand).IsInstanceOfType(node))
            {
                return new Type(Byte.ParseByte(((ABpCommand)node).GetType().GetText()));
            }

            throw new Exception("No type for this node type: " + node);
        }

        public static Type GetReturnType(AActionCommand node, ActionsData actions)
        {
            if (actions == null)
            {
                return new Type((byte)0);
            }

            Type returnType = actions.GetReturnType(GetActionId(node));
            return (returnType != null)
                ? returnType
                : new Type((byte)0);
        }

        public static Type GetReturnType(ABinaryCommand node)
        {
            byte nodetype = Byte.ParseByte(node.GetType().GetText());
            byte type;
            if (nodetype == 60 || nodetype == 59 || nodetype == 58)
            {
                type = (byte)240; // -16 as unsigned byte (240 = 256 - 16)
            }
            else if (nodetype == 32)
            {
                type = 3;
            }
            else if (nodetype == 37 || nodetype == 38 || nodetype == 33)
            {
                type = 4;
            }
            else
            {
                if (nodetype != 35)
                {
                    throw new Exception("Unexpected type " + nodetype);
                }

                type = 5;
            }

            if (type == 240) // -16 as unsigned byte
            {
                type = 4;
            }

            return new Type(type);
        }

        public static bool IsConditionalProgram(Start ast)
        {
            return ((AProgram)ast.GetPProgram()).GetConditional() != null;
        }
    }
}




