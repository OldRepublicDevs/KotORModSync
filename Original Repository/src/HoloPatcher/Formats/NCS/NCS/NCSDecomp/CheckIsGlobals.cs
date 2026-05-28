// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/CheckIsGlobals.java:16-42
// Original: public class CheckIsGlobals extends PrunedReversedDepthFirstAdapter
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Utils
{
    public class CheckIsGlobals : PrunedReversedDepthFirstAdapter
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/CheckIsGlobals.java:17
        // Original: private boolean isGlobals = false;
        private bool isGlobals = false;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/CheckIsGlobals.java:20-22
        // Original: @Override public void inABpCommand(ABpCommand node)
        public override void InABpCommand(ABpCommand node)
        {
            this.isGlobals = true;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/CheckIsGlobals.java:25-37
        // Original: @Override public void caseACommandBlock(ACommandBlock node) { this.inACommandBlock(node); PCmd[] temp = node.getCmd().toArray(new PCmd[0]); for (int i = temp.length - 1; i >= 0; i--) { temp[i].apply(this); if (this.isGlobals) { return; } } this.outACommandBlock(node); }
        public override void CaseACommandBlock(ACommandBlock node)
        {
            this.InACommandBlock(node);
            PCmd[] temp = node.GetCmd().ToArray();

            for (int i = temp.Length - 1; i >= 0; i--)
            {
                temp[i].Apply(this);
                if (this.isGlobals)
                {
                    return;
                }
            }

            this.OutACommandBlock(node);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/CheckIsGlobals.java:39-41
        // Original: public boolean getIsGlobals()
        public virtual bool GetIsGlobals()
        {
            return this.isGlobals;
        }
    }
}
