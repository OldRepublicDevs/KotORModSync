// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetPositions.java:14-39
// Original: public class SetPositions extends PrunedReversedDepthFirstAdapter
using System;
using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;
using KOTORModSync.Formats.NCS.NCSDecomp;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Utils
{
    public class SetPositions : PrunedReversedDepthFirstAdapter
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetPositions.java:15
        // Original: private NodeAnalysisData nodedata;
        private NodeAnalysisData nodedata;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetPositions.java:16
        // Original: private int currentPos;
        private int currentPos;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetPositions.java:18-21
        // Original: public SetPositions(NodeAnalysisData nodedata) { this.nodedata = nodedata; this.currentPos = 0; }
        public SetPositions(NodeAnalysisData nodedata)
        {
            this.nodedata = nodedata;
            this.currentPos = 0;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetPositions.java:23-25
        // Original: public void done() { this.nodedata = null; }
        public virtual void Done()
        {
            this.nodedata = null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetPositions.java:27-33
        // Original: @Override public void defaultIn(Node node) { int pos = NodeUtils.getCommandPos(node); if (pos > 0) { this.currentPos = pos; } }
        public override void DefaultIn(Node node)
        {
            try
            {
                int pos = NodeUtils.GetCommandPos(node);
                if (pos > 0)
                {
                    this.currentPos = pos;
                }
            }
            catch (Exception)
            {
                // Node doesn't have a position token or GetCommandPos failed
                // Continue with currentPos unchanged - position will be set from parent or remain 0
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetPositions.java:36-38
        // Original: this.nodedata.setPos(node, this.currentPos);
        public override void DefaultOut(Node node)
        {
            // For ASubroutine nodes, try to get position from first command if currentPos is 0
            // Since we're using reversed depth-first traversal, children are visited first,
            // so positions should already be set on command nodes
            if (typeof(ASubroutine).IsInstanceOfType(node))
            {
                int originalPos = this.currentPos;
                if (this.currentPos == 0)
                {
                    Node firstCmd = NodeUtils.GetCommandChild(node);
                    if (firstCmd != null)
                    {
                        // Try to get position that was already set on the first command
                        int existingPos = this.nodedata.TryGetPos(firstCmd);
                        if (existingPos > 0)
                        {
                            this.currentPos = existingPos;
                            Debug("DEBUG SetPositions: Set ASubroutine position from first command: " + existingPos);
                        }
                        else
                        {
                            // Fallback: try GetCommandPos if position wasn't set yet
                            int cmdPos = NodeUtils.GetCommandPos(firstCmd);
                            if (cmdPos > 0)
                            {
                                this.currentPos = cmdPos;
                                Debug("DEBUG SetPositions: Set ASubroutine position from GetCommandPos: " + cmdPos);
                            }
                            else
                            {
                                Debug("DEBUG SetPositions: ASubroutine first command has no position, keeping currentPos=" + this.currentPos);
                            }
                        }
                    }
                    else
                    {
                        Debug("DEBUG SetPositions: ASubroutine has no first command, keeping currentPos=" + this.currentPos);
                    }
                }
                // Always set position on subroutine node, even if it's 0 (main subroutine is at position 0)
                this.nodedata.SetPos(node, this.currentPos);
                Debug("DEBUG SetPositions: Set ASubroutine position to " + this.currentPos + " (original was " + originalPos + ")");
            }
            else
            {
                this.nodedata.SetPos(node, this.currentPos);
            }
        }
    }
}




