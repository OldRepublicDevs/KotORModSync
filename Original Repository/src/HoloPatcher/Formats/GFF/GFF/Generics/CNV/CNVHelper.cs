using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using JetBrains.Annotations;
using KOTORModSync.Common;
using KOTORModSync.Resource.Generics.DLG;

namespace KOTORModSync.Resource.Generics.CNV
{
    // Matching pattern from DLGHelper
    // Original: construct_cnv, dismantle_cnv, read_cnv, bytes_cnv functions for CNV conversation format
    public static class CNVHelper
    {
        // Matching pattern from DLGHelper.ConstructDlg
        // Original: def construct_cnv(gff: GFF) -> CNV:
        /// <summary>
        /// Constructs a CNV object from a GFF structure.
        /// </summary>
        /// <param name="gff">The GFF structure containing CNV data.</param>
        /// <returns>A CNV object with extracted conversation data.</returns>
        /// <remarks>
        /// CNV Construction:
        /// - Extracts conversation tree from GFF root struct
        /// - Handles Eclipse Engine conversation format (Dragon Age, )
        /// - Similar structure to DLG but adapted for Eclipse conversation system
        /// </remarks>
        public static CNV ConstructCnv(GFF gff)
        {
            var cnv = new CNV();

            GFFStruct root = gff.Root;

            GFFList entryList = root.Acquire("EntryList", new GFFList());
            GFFList replyList = root.Acquire("ReplyList", new GFFList());

            var allEntries = new List<CNVEntry>();
            for (int i = 0; i < entryList.Count; i++)
            {
                allEntries.Add(new CNVEntry());
            }

            var allReplies = new List<CNVReply>();
            for (int i = 0; i < replyList.Count; i++)
            {
                allReplies.Add(new CNVReply());
            }

            // Conversation metadata
            cnv.WordCount = root.Acquire("NumWords", 0);
            cnv.OnAbort = root.Acquire("EndConverAbort", ResRef.FromBlank());
            cnv.OnEnd = root.Acquire("EndConversation", ResRef.FromBlank());
            cnv.Skippable = root.Acquire("Skippable", (byte)0) != 0;
            cnv.AmbientTrack = root.Acquire("AmbientTrack", ResRef.FromBlank());
            cnv.AnimatedCut = root.Acquire("AnimatedCut", 0);
            cnv.CameraModel = root.Acquire("CameraModel", ResRef.FromBlank());
            cnv.ComputerType = (CNVComputerType)root.Acquire("ComputerType", (uint)0);
            cnv.ConversationType = (CNVConversationType)root.Acquire("ConversationType", (uint)0);
            cnv.OldHitCheck = root.Acquire("OldHitCheck", (byte)0) != 0;
            cnv.UnequipHands = root.Acquire("UnequipHItem", (byte)0) != 0;
            cnv.UnequipItems = root.Acquire("UnequipItems", (byte)0) != 0;
            cnv.VoId = root.Acquire("VO_ID", string.Empty);
            cnv.NextNodeId = root.Acquire("NextNodeID", 0);
            cnv.DelayEntry = root.Acquire("DelayEntry", 0);
            cnv.DelayReply = root.Acquire("DelayReply", 0);

            // StuntList
            GFFList stuntList = root.Acquire("StuntList", new GFFList());
            foreach (GFFStruct stuntStruct in stuntList)
            {
                var stunt = new CNVStunt();
                stunt.Participant = stuntStruct.Acquire("Participant", string.Empty);
                stunt.StuntModel = stuntStruct.Acquire("StuntModel", ResRef.FromBlank());
                cnv.Stunts.Add(stunt);
            }

            // StartingList
            GFFList startingList = root.Acquire("StartingList", new GFFList());
            for (int linkListIndex = 0; linkListIndex < startingList.Count; linkListIndex++)
            {
                GFFStruct linkStruct = startingList.At(linkListIndex);
                int nodeStructId = (int)linkStruct.Acquire("Index", (uint)0);
                if (nodeStructId >= 0 && nodeStructId < allEntries.Count)
                {
                    CNVEntry starterNode = allEntries[nodeStructId];
                    var link = new CNVLink(starterNode, linkListIndex);
                    cnv.Starters.Add(link);
                    ConstructLink(linkStruct, link);
                }
            }

            // EntryList
            for (int nodeListIndex = 0; nodeListIndex < entryList.Count; nodeListIndex++)
            {
                GFFStruct entryStruct = entryList.At(nodeListIndex);
                CNVEntry entry = allEntries[nodeListIndex];
                entry.Speaker = entryStruct.Acquire("Speaker", string.Empty);
                entry.ListIndex = nodeListIndex;
                ConstructNode(entryStruct, entry);

                GFFList repliesList = entryStruct.Acquire("RepliesList", new GFFList());
                for (int linkListIndex = 0; linkListIndex < repliesList.Count; linkListIndex++)
                {
                    GFFStruct linkStruct = repliesList.At(linkListIndex);
                    int nodeStructId = (int)linkStruct.Acquire("Index", (uint)0);
                    if (nodeStructId >= 0 && nodeStructId < allReplies.Count)
                    {
                        CNVReply replyNode = allReplies[nodeStructId];
                        var link = new CNVLink(replyNode, linkListIndex);
                        link.IsChild = linkStruct.Acquire("IsChild", (byte)0) != 0;
                        link.Comment = linkStruct.Acquire("LinkComment", string.Empty);
                        entry.Links.Add(link);
                        ConstructLink(linkStruct, link);
                    }
                }
            }

            // ReplyList
            for (int nodeListIndex = 0; nodeListIndex < replyList.Count; nodeListIndex++)
            {
                GFFStruct replyStruct = replyList.At(nodeListIndex);
                CNVReply reply = allReplies[nodeListIndex];
                reply.ListIndex = nodeListIndex;
                ConstructNode(replyStruct, reply);

                GFFList entriesList = replyStruct.Acquire("EntriesList", new GFFList());
                for (int linkListIndex = 0; linkListIndex < entriesList.Count; linkListIndex++)
                {
                    GFFStruct linkStruct = entriesList.At(linkListIndex);
                    int nodeStructId = (int)linkStruct.Acquire("Index", (uint)0);
                    if (nodeStructId >= 0 && nodeStructId < allEntries.Count)
                    {
                        CNVEntry entryNode = allEntries[nodeStructId];
                        var link = new CNVLink(entryNode, linkListIndex);
                        link.IsChild = linkStruct.Acquire("IsChild", (byte)0) != 0;
                        link.Comment = linkStruct.Acquire("LinkComment", string.Empty);
                        reply.Links.Add(link);
                        ConstructLink(linkStruct, link);
                    }
                }
            }

            return cnv;
        }

        // Matching pattern from DLGHelper.ConstructNode
        // Original: def construct_node(gff_struct: GFFStruct, node: CNVNode):
        private static void ConstructNode(GFFStruct gffStruct, CNVNode node)
        {
            node.Text = gffStruct.Acquire("Text", LocalizedString.FromInvalid());
            node.Listener = gffStruct.Acquire("Listener", string.Empty);
            node.VoResRef = gffStruct.Acquire("VO_ResRef", ResRef.FromBlank());
            node.Script1 = gffStruct.Acquire("Script", ResRef.FromBlank());
            uint delay = gffStruct.Acquire("Delay", (uint)0);
            node.Delay = delay == 0xFFFFFFFF ? -1 : (int)delay;
            node.Comment = gffStruct.Acquire("Comment", string.Empty);
            node.Sound = gffStruct.Acquire("Sound", ResRef.FromBlank());
            node.Quest = gffStruct.Acquire("Quest", string.Empty);
            node.PlotIndex = gffStruct.Acquire("PlotIndex", -1);
            node.PlotXpPercentage = gffStruct.Acquire("PlotXPPercentage", 0.0f);
            node.WaitFlags = (int)gffStruct.Acquire("WaitFlags", (uint)0);
            node.CameraAngle = (int)gffStruct.Acquire("CameraAngle", (uint)0);
            node.FadeType = (int)gffStruct.Acquire("FadeType", (byte)0);
            node.SoundExists = (int)gffStruct.Acquire("SoundExists", (byte)0);
            node.VoTextChanged = gffStruct.Acquire("VOTextChanged", (byte)0) != 0;

            // AnimList
            GFFList animList = gffStruct.Acquire("AnimList", new GFFList());
            foreach (GFFStruct animStruct in animList)
            {
                var anim = new CNVAnimation();
                int animationId = (int)animStruct.Acquire("Animation", (ushort)0);
                if (animationId > 10000)
                {
                    animationId -= 10000;
                }
                anim.AnimationId = animationId;
                anim.Participant = animStruct.Acquire("Participant", string.Empty);
                node.Animations.Add(anim);
            }

            // Eclipse-specific node fields (similar to K2-specific fields in DLG)
            node.Script1Param1 = gffStruct.Acquire("ActionParam1", 0);
            node.Script2Param1 = gffStruct.Acquire("ActionParam1b", 0);
            node.Script1Param2 = gffStruct.Acquire("ActionParam2", 0);
            node.Script2Param2 = gffStruct.Acquire("ActionParam2b", 0);
            node.Script1Param3 = gffStruct.Acquire("ActionParam3", 0);
            node.Script2Param3 = gffStruct.Acquire("ActionParam3b", 0);
            node.Script1Param4 = gffStruct.Acquire("ActionParam4", 0);
            node.Script2Param4 = gffStruct.Acquire("ActionParam4b", 0);
            node.Script1Param5 = gffStruct.Acquire("ActionParam5", 0);
            node.Script2Param5 = gffStruct.Acquire("ActionParam5b", 0);
            node.Script1Param6 = gffStruct.Acquire("ActionParamStrA", string.Empty);
            node.Script2Param6 = gffStruct.Acquire("ActionParamStrB", string.Empty);
            node.Script2 = gffStruct.Acquire("Script2", ResRef.FromBlank());
            node.EmotionId = gffStruct.Acquire("Emotion", 0);
            node.FacialId = gffStruct.Acquire("FacialAnim", 0);
            node.NodeId = gffStruct.Acquire("NodeID", 0);
            node.Unskippable = gffStruct.Acquire("NodeUnskippable", (byte)0) != 0;

            if (gffStruct.Exists("QuestEntry"))
            {
                node.QuestEntry = gffStruct.Acquire("QuestEntry", 0);
            }
            if (gffStruct.Exists("FadeDelay"))
            {
                node.FadeDelay = gffStruct.Acquire("FadeDelay", 0.0f);
            }
            if (gffStruct.Exists("FadeLength"))
            {
                node.FadeLength = gffStruct.Acquire("FadeLength", 0.0f);
            }
            if (gffStruct.Exists("CameraAnimation"))
            {
                node.CameraAnim = (int)gffStruct.Acquire("CameraAnimation", (ushort)0);
            }
            if (gffStruct.Exists("CameraID"))
            {
                node.CameraId = gffStruct.Acquire("CameraID", 0);
            }
            if (gffStruct.Exists("CamFieldOfView"))
            {
                node.CameraFov = gffStruct.Acquire("CamFieldOfView", 0.0f);
            }
            if (gffStruct.Exists("CamHeightOffset"))
            {
                node.CameraHeight = gffStruct.Acquire("CamHeightOffset", 0.0f);
            }
            if (gffStruct.Exists("CamVidEffect"))
            {
                node.CameraEffect = gffStruct.Acquire("CamVidEffect", -1);
            }
            if (gffStruct.Exists("TarHeightOffset"))
            {
                node.TargetHeight = gffStruct.Acquire("TarHeightOffset", 0.0f);
            }
            if (gffStruct.Exists("FadeColor"))
            {
                var fadeColorVec = gffStruct.Acquire("FadeColor", new Vector3(0, 0, 0));
                node.FadeColor = Color.FromBgrVector3(fadeColorVec);
            }
        }

        // Matching pattern from DLGHelper.ConstructLink
        // Original: def construct_link(gff_struct: GFFStruct, link: CNVLink):
        private static void ConstructLink(GFFStruct gffStruct, CNVLink link)
        {
            link.Active1 = gffStruct.Acquire("Active", ResRef.FromBlank());
            link.Active2 = gffStruct.Acquire("Active2", ResRef.FromBlank());
            link.Logic = gffStruct.Acquire("Logic", (byte)0) != 0;
            link.Active1Not = gffStruct.Acquire("Not", (byte)0) != 0;
            link.Active2Not = gffStruct.Acquire("Not2", (byte)0) != 0;
            link.Active1Param1 = gffStruct.Acquire("Param1", 0);
            link.Active1Param2 = gffStruct.Acquire("Param2", 0);
            link.Active1Param3 = gffStruct.Acquire("Param3", 0);
            link.Active1Param4 = gffStruct.Acquire("Param4", 0);
            link.Active1Param5 = gffStruct.Acquire("Param5", 0);
            link.Active1Param6 = gffStruct.Acquire("ParamStrA", string.Empty);
            link.Active2Param1 = gffStruct.Acquire("Param1b", 0);
            link.Active2Param2 = gffStruct.Acquire("Param2b", 0);
            link.Active2Param3 = gffStruct.Acquire("Param3b", 0);
            link.Active2Param4 = gffStruct.Acquire("Param4b", 0);
            link.Active2Param5 = gffStruct.Acquire("Param5b", 0);
            link.Active2Param6 = gffStruct.Acquire("ParamStrB", string.Empty);
        }

        // Matching pattern from DLGHelper.DismantleDlg
        // Original: def dismantle_cnv(cnv: CNV, game: Game = Game.DA) -> GFF:
        /// <summary>
        /// Dismantles a CNV object into a GFF structure.
        /// </summary>
        /// <param name="cnv">The CNV object to dismantle.</param>
        /// <param name="game">The game type (must be Eclipse engine).</param>
        /// <returns>A GFF structure containing CNV data.</returns>
        /// <remarks>
        /// CNV Dismantling:
        /// - Validates that game is Eclipse (Dragon Age or )
        /// - Creates GFF with "CNV " signature
        /// - Writes conversation tree to GFF root struct
        /// - Handles Eclipse Engine conversation format
        /// </remarks>
        public static GFF DismantleCnv(CNV cnv, Game game)
        {
            // Validate game type - CNV format is only used by Eclipse Engine
            if (!game.IsEclipse())
            {
                throw new ArgumentException(
                    $"CNV format is only supported for Eclipse Engine games (Dragon Age, ). " +
                    $"Provided game: {game}",
                    nameof(game));
            }

            var gff = new GFF(GFFContent.CNV);
            GFFStruct root = gff.Root;

            List<CNVEntry> allEntries = cnv.AllEntries(asSorted: true);
            List<CNVReply> allReplies = cnv.AllReplies(asSorted: true);

            root.SetUInt32("NumWords", (uint)cnv.WordCount);
            root.SetResRef("EndConverAbort", cnv.OnAbort);
            root.SetResRef("EndConversation", cnv.OnEnd);
            root.SetUInt8("Skippable", cnv.Skippable ? (byte)1 : (byte)0);
            // Only write optional fields if they have non-default values
            if (cnv.AmbientTrack != null && !string.IsNullOrEmpty(cnv.AmbientTrack.ToString()))
            {
                root.SetResRef("AmbientTrack", cnv.AmbientTrack);
            }
            if (cnv.AnimatedCut != 0)
            {
                root.SetUInt8("AnimatedCut", (byte)cnv.AnimatedCut);
            }
            root.SetResRef("CameraModel", cnv.CameraModel);
            if (cnv.ComputerType != CNVComputerType.Modern)
            {
                root.SetUInt8("ComputerType", (byte)cnv.ComputerType);
            }
            if (cnv.ConversationType != CNVConversationType.Human)
            {
                root.SetInt32("ConversationType", (int)cnv.ConversationType);
            }
            if (cnv.OldHitCheck)
            {
                root.SetUInt8("OldHitCheck", (byte)1);
            }
            if (cnv.UnequipHands)
            {
                root.SetUInt8("UnequipHItem", (byte)1);
            }
            if (cnv.UnequipItems)
            {
                root.SetUInt8("UnequipItems", (byte)1);
            }
            root.SetString("VO_ID", cnv.VoId);
            // Eclipse-specific root fields
            root.SetInt32("NextNodeID", cnv.NextNodeId);
            // Deprecated fields - write for compatibility
            root.SetInt32("DelayEntry", cnv.DelayEntry);
            root.SetInt32("DelayReply", cnv.DelayReply);

            // StuntList
            var stuntList = new GFFList();
            root.SetList("StuntList", stuntList);
            for (int i = 0; i < cnv.Stunts.Count; i++)
            {
                CNVStunt stunt = cnv.Stunts[i];
                GFFStruct stuntStruct = stuntList.Add(i);
                stuntStruct.SetString("Participant", stunt.Participant);
                stuntStruct.SetResRef("StuntModel", stunt.StuntModel);
            }

            // StartingList
            var startingList = new GFFList();
            root.SetList("StartingList", startingList);
            for (int i = 0; i < cnv.Starters.Count; i++)
            {
                CNVLink link = cnv.Starters[i];
                GFFStruct linkStruct = startingList.Add(i);
                int entryIndex = allEntries.IndexOf(link.Node as CNVEntry);
                linkStruct.SetUInt32("Index", entryIndex >= 0 ? (uint)entryIndex : 0);
                DismantleLink(linkStruct, link, game, "StartingList");
            }

            // EntryList
            var entryList = new GFFList();
            root.SetList("EntryList", entryList);
            for (int i = 0; i < allEntries.Count; i++)
            {
                CNVEntry entry = allEntries[i];
                GFFStruct entryStruct = entryList.Add(i);
                entryStruct.SetString("Speaker", entry.Speaker);
                DismantleNode(entryStruct, entry, allEntries, allReplies, "RepliesList", game);
            }

            // ReplyList
            var replyList = new GFFList();
            root.SetList("ReplyList", replyList);
            for (int i = 0; i < allReplies.Count; i++)
            {
                CNVReply reply = allReplies[i];
                GFFStruct replyStruct = replyList.Add(i);
                DismantleNode(replyStruct, reply, allEntries, allReplies, "EntriesList", game);
            }

            return gff;
        }

        // Matching pattern from DLGHelper.DismantleNode
        // Original: def dismantle_node(gff_struct: GFFStruct, node: CNVNode, nodes: list[CNVNode], list_name: str, game: Game):
        private static void DismantleNode(GFFStruct gffStruct, CNVNode node, List<CNVEntry> allEntries, List<CNVReply> allReplies, string listName, Game game)
        {
            gffStruct.SetLocString("Text", node.Text);
            gffStruct.SetString("Listener", node.Listener);
            gffStruct.SetResRef("VO_ResRef", node.VoResRef);
            gffStruct.SetResRef("Script", node.Script1);
            gffStruct.SetUInt32("Delay", node.Delay == -1 ? 0xFFFFFFFF : (uint)node.Delay);
            gffStruct.SetString("Comment", node.Comment);
            gffStruct.SetResRef("Sound", node.Sound);
            gffStruct.SetString("Quest", node.Quest);
            gffStruct.SetInt32("PlotIndex", node.PlotIndex);
            if (node.PlotXpPercentage != 0.0f)
            {
                gffStruct.SetSingle("PlotXPPercentage", node.PlotXpPercentage);
            }
            gffStruct.SetUInt32("WaitFlags", (uint)node.WaitFlags);
            gffStruct.SetUInt32("CameraAngle", (uint)node.CameraAngle);
            gffStruct.SetUInt8("FadeType", (byte)node.FadeType);
            gffStruct.SetUInt8("SoundExists", (byte)node.SoundExists);
            if (node.VoTextChanged)
            {
                gffStruct.SetUInt8("VOTextChanged", (byte)(node.VoTextChanged ? 1 : 0));
            }

            // AnimList
            var animList = new GFFList();
            gffStruct.SetList("AnimList", animList);
            for (int i = 0; i < node.Animations.Count; i++)
            {
                CNVAnimation anim = node.Animations[i];
                GFFStruct animStruct = animList.Add(i);
                int animationId = anim.AnimationId;
                if (animationId <= 10000)
                {
                    animStruct.SetUInt16("Animation", (ushort)animationId);
                }
                else
                {
                    animStruct.SetUInt16("Animation", (ushort)(animationId + 10000));
                }
                animStruct.SetString("Participant", anim.Participant);
            }

            if (!string.IsNullOrEmpty(node.Quest) && node.QuestEntry.HasValue)
            {
                gffStruct.SetUInt32("QuestEntry", (uint)node.QuestEntry.Value);
            }
            if (node.FadeDelay.HasValue)
            {
                gffStruct.SetSingle("FadeDelay", node.FadeDelay.Value);
            }
            if (node.FadeLength.HasValue)
            {
                gffStruct.SetSingle("FadeLength", node.FadeLength.Value);
            }
            if (node.CameraAnim.HasValue)
            {
                gffStruct.SetUInt16("CameraAnimation", (ushort)node.CameraAnim.Value);
            }
            if (node.CameraId.HasValue)
            {
                gffStruct.SetInt32("CameraID", node.CameraId.Value);
            }
            if (node.CameraFov.HasValue)
            {
                gffStruct.SetSingle("CamFieldOfView", node.CameraFov.Value);
            }
            if (node.CameraHeight.HasValue)
            {
                gffStruct.SetSingle("CamHeightOffset", node.CameraHeight.Value);
            }
            if (node.CameraEffect.HasValue)
            {
                gffStruct.SetInt32("CamVidEffect", node.CameraEffect.Value);
            }
            if (node.TargetHeight.HasValue)
            {
                gffStruct.SetSingle("TarHeightOffset", node.TargetHeight.Value);
            }
            if (node.FadeColor != null)
            {
                gffStruct.SetVector3("FadeColor", node.FadeColor.ToBgrVector3());
            }

            // Eclipse-specific node fields (always write for Eclipse games)
            gffStruct.SetInt32("ActionParam1", node.Script1Param1);
            gffStruct.SetInt32("ActionParam1b", node.Script2Param1);
            gffStruct.SetInt32("ActionParam2", node.Script1Param2);
            gffStruct.SetInt32("ActionParam2b", node.Script2Param2);
            gffStruct.SetInt32("ActionParam3", node.Script1Param3);
            gffStruct.SetInt32("ActionParam3b", node.Script2Param3);
            gffStruct.SetInt32("ActionParam4", node.Script1Param4);
            gffStruct.SetInt32("ActionParam4b", node.Script2Param4);
            gffStruct.SetInt32("ActionParam5", node.Script1Param5);
            gffStruct.SetInt32("ActionParam5b", node.Script2Param5);
            gffStruct.SetString("ActionParamStrA", node.Script1Param6);
            gffStruct.SetString("ActionParamStrB", node.Script2Param6);
            gffStruct.SetResRef("Script2", node.Script2);
            gffStruct.SetInt32("Emotion", node.EmotionId);
            gffStruct.SetInt32("FacialAnim", node.FacialId);
            gffStruct.SetInt32("NodeID", node.NodeId);
            gffStruct.SetInt32("NodeUnskippable", node.Unskippable ? 1 : 0);

            // Links
            var linkList = new GFFList();
            gffStruct.SetList(listName, linkList);
            var sortedLinks = node.Links.OrderBy(l => l.ListIndex == -1).ThenBy(l => l.ListIndex).ToList();
            for (int i = 0; i < sortedLinks.Count; i++)
            {
                CNVLink link = sortedLinks[i];
                GFFStruct linkStruct = linkList.Add(i);
                int nodeIndex = -1;
                if (link.Node is CNVEntry entry)
                {
                    nodeIndex = allEntries.IndexOf(entry);
                }
                else if (link.Node is CNVReply reply)
                {
                    nodeIndex = allReplies.IndexOf(reply);
                }
                linkStruct.SetUInt32("Index", nodeIndex >= 0 ? (uint)nodeIndex : 0);
                DismantleLink(linkStruct, link, game, listName);
            }
        }

        // Matching pattern from DLGHelper.DismantleLink
        // Original: def dismantle_link(gff_struct: GFFStruct, link: CNVLink, game: Game, list_name: str):
        private static void DismantleLink(GFFStruct gffStruct, CNVLink link, Game game, string listName)
        {
            if (listName == "StartingList")
            {
                gffStruct.SetUInt8("IsChild", link.IsChild ? (byte)1 : (byte)0);
            }
            gffStruct.SetResRef("Active", link.Active1);
            if (!string.IsNullOrWhiteSpace(link.Comment))
            {
                gffStruct.SetString("LinkComment", link.Comment);
            }
            
            // Eclipse-specific link fields (always write for Eclipse games)
            gffStruct.SetResRef("Active2", link.Active2);
            gffStruct.SetInt32("Logic", link.Logic ? 1 : 0);
            gffStruct.SetUInt8("Not", link.Active1Not ? (byte)1 : (byte)0);
            gffStruct.SetUInt8("Not2", link.Active2Not ? (byte)1 : (byte)0);
            gffStruct.SetInt32("Param1", link.Active1Param1);
            gffStruct.SetInt32("Param2", link.Active1Param2);
            gffStruct.SetInt32("Param3", link.Active1Param3);
            gffStruct.SetInt32("Param4", link.Active1Param4);
            gffStruct.SetInt32("Param5", link.Active1Param5);
            gffStruct.SetString("ParamStrA", link.Active1Param6);
            gffStruct.SetInt32("Param1b", link.Active2Param1);
            gffStruct.SetInt32("Param2b", link.Active2Param2);
            gffStruct.SetInt32("Param3b", link.Active2Param3);
            gffStruct.SetInt32("Param4b", link.Active2Param4);
            gffStruct.SetInt32("Param5b", link.Active2Param5);
            gffStruct.SetString("ParamStrB", link.Active2Param6);
        }

        // Matching pattern from DLGHelper.ReadDlg
        // Original: def read_cnv(source: SOURCE_TYPES, offset: int = 0, size: int | None = None) -> CNV:
        /// <summary>
        /// Reads a CNV file from byte data.
        /// </summary>
        /// <param name="data">The CNV file data.</param>
        /// <param name="offset">Byte offset to start reading from (default: 0).</param>
        /// <param name="size">Number of bytes to read (-1 = read all).</param>
        /// <returns>A CNV object with parsed conversation data.</returns>
        public static CNV ReadCnv(byte[] data, int offset = 0, int size = -1)
        {
            byte[] dataToRead = data;
            if (size > 0 && offset + size <= data.Length)
            {
                dataToRead = new byte[size];
                Array.Copy(data, offset, dataToRead, 0, size);
            }
            GFF gff = GFF.FromBytes(dataToRead);
            return ConstructCnv(gff);
        }

        // Matching pattern from DLGHelper.BytesDlg
        // Original: def bytes_cnv(cnv: CNV, game: Game = Game.DA, file_format: ResourceType = ResourceType.GFF) -> bytes:
        /// <summary>
        /// Converts a CNV object to bytes.
        /// </summary>
        /// <param name="cnv">The CNV object to convert.</param>
        /// <param name="game">The game type (must be Eclipse engine).</param>
        /// <param name="fileFormat">File format (default: CNV, must be CNV or GFF).</param>
        /// <returns>Byte array containing CNV file data.</returns>
        public static byte[] BytesCnv(CNV cnv, Game game, ResourceType fileFormat = null)
        {
            if (fileFormat == null)
            {
                fileFormat = ResourceType.CNV;
            }
            GFF gff = DismantleCnv(cnv, game);
            return GFFAuto.BytesGff(gff, fileFormat);
        }

        // Matching pattern from DLGHelper - conversion between formats
        /// <summary>
        /// Converts a CNV object to a DLG object.
        /// </summary>
        /// <param name="cnv">The CNV object to convert.</param>
        /// <returns>A DLG object with converted conversation data.</returns>
        /// <remarks>
        /// CNV to DLG Conversion:
        /// - CNV and DLG share the same structure (conversation trees with entries/replies/links)
        /// - Main difference is GFF signature ("CNV " vs "DLG ") and some engine-specific fields
        /// - All common fields are mapped directly
        /// - Eclipse-specific fields are preserved where possible or mapped to closest DLG equivalent
        /// - Used for converting Eclipse conversation files to Aurora/Odyssey format
        /// </remarks>
        public static DLG.DLG ToDlg(CNV cnv)
        {
            if (cnv == null)
            {
                throw new ArgumentNullException(nameof(cnv));
            }

            var dlg = new DLG.DLG();

            // Copy metadata fields (identical structure)
            dlg.AmbientTrack = cnv.AmbientTrack;
            dlg.AnimatedCut = cnv.AnimatedCut;
            dlg.CameraModel = cnv.CameraModel;
            dlg.ComputerType = (DLG.DLGComputerType)(int)cnv.ComputerType;
            dlg.ConversationType = (DLG.DLGConversationType)(int)cnv.ConversationType;
            dlg.OnAbort = cnv.OnAbort;
            dlg.OnEnd = cnv.OnEnd;
            dlg.WordCount = cnv.WordCount;
            dlg.OldHitCheck = cnv.OldHitCheck;
            dlg.Skippable = cnv.Skippable;
            dlg.UnequipItems = cnv.UnequipItems;
            dlg.UnequipHands = cnv.UnequipHands;
            dlg.VoId = cnv.VoId;
            dlg.Comment = cnv.Comment;
            dlg.NextNodeId = cnv.NextNodeId;
            dlg.DelayEntry = cnv.DelayEntry;
            dlg.DelayReply = cnv.DelayReply;

            // Convert stunts
            foreach (CNVStunt cnvStunt in cnv.Stunts)
            {
                var dlgStunt = new DLG.DLGStunt
                {
                    Participant = cnvStunt.Participant,
                    StuntModel = cnvStunt.StuntModel
                };
                dlg.Stunts.Add(dlgStunt);
            }

            // Build node maps for conversion
            var cnvEntryToDlgEntry = new Dictionary<CNVEntry, DLGEntry>();
            var cnvReplyToDlgReply = new Dictionary<CNVReply, DLGReply>();

            // Convert all entries
            List<CNVEntry> allCnvEntries = cnv.AllEntries(asSorted: true);
            foreach (CNVEntry cnvEntry in allCnvEntries)
            {
                DLGEntry dlgEntry = ConvertNode(cnvEntry);
                cnvEntryToDlgEntry[cnvEntry] = dlgEntry;
            }

            // Convert all replies
            List<CNVReply> allCnvReplies = cnv.AllReplies(asSorted: true);
            foreach (CNVReply cnvReply in allCnvReplies)
            {
                DLG.DLGReply dlgReply = ConvertNode(cnvReply);
                cnvReplyToDlgReply[cnvReply] = dlgReply;
            }

            // Convert links in entries
            foreach (CNVEntry cnvEntry in allCnvEntries)
            {
                DLG.DLGEntry dlgEntry = cnvEntryToDlgEntry[cnvEntry];
                foreach (CNVLink cnvLink in cnvEntry.Links)
                {
                    if (cnvLink.Node is CNVReply cnvReply && cnvReplyToDlgReply.ContainsKey(cnvReply))
                    {
                        DLG.DLGReply dlgReply = cnvReplyToDlgReply[cnvReply];
                        DLG.DLGLink dlgLink = ConvertLink(cnvLink, dlgReply);
                        dlgEntry.Links.Add(dlgLink);
                    }
                }
            }

            // Convert links in replies
            foreach (CNVReply cnvReply in allCnvReplies)
            {
                DLG.DLGReply dlgReply = cnvReplyToDlgReply[cnvReply];
                foreach (CNVLink cnvLink in cnvReply.Links)
                {
                    if (cnvLink.Node is CNVEntry cnvEntry && cnvEntryToDlgEntry.ContainsKey(cnvEntry))
                    {
                        DLG.DLGEntry dlgEntry = cnvEntryToDlgEntry[cnvEntry];
                        DLG.DLGLink dlgLink = ConvertLink(cnvLink, dlgEntry);
                        dlgReply.Links.Add(dlgLink);
                    }
                }
            }

            // Convert starters
            foreach (CNVLink cnvStarter in cnv.Starters)
            {
                if (cnvStarter.Node is CNVEntry cnvEntry && cnvEntryToDlgEntry.ContainsKey(cnvEntry))
                {
                    DLG.DLGEntry dlgEntry = cnvEntryToDlgEntry[cnvEntry];
                    DLG.DLGLink dlgLink = ConvertLink(cnvStarter, dlgEntry);
                    dlg.Starters.Add(dlgLink);
                }
            }

            return dlg;
        }

        // Helper method to convert CNVNode to DLGNode
        private static DLG.DLGEntry ConvertNode(CNVEntry cnvEntry)
        {
            var dlgEntry = new DLG.DLGEntry
            {
                Speaker = cnvEntry.Speaker,
                ListIndex = cnvEntry.ListIndex,
                Comment = cnvEntry.Comment,
                CameraAngle = cnvEntry.CameraAngle,
                CameraAnim = cnvEntry.CameraAnim,
                CameraId = cnvEntry.CameraId,
                CameraFov = cnvEntry.CameraFov,
                CameraHeight = cnvEntry.CameraHeight,
                CameraEffect = cnvEntry.CameraEffect,
                Delay = cnvEntry.Delay,
                FadeType = cnvEntry.FadeType,
                FadeColor = cnvEntry.FadeColor,
                FadeDelay = cnvEntry.FadeDelay,
                FadeLength = cnvEntry.FadeLength,
                Text = cnvEntry.Text,
                Script1 = cnvEntry.Script1,
                Script2 = cnvEntry.Script2,
                Sound = cnvEntry.Sound,
                SoundExists = cnvEntry.SoundExists,
                VoResRef = cnvEntry.VoResRef,
                WaitFlags = cnvEntry.WaitFlags,
                Script1Param1 = cnvEntry.Script1Param1,
                Script1Param2 = cnvEntry.Script1Param2,
                Script1Param3 = cnvEntry.Script1Param3,
                Script1Param4 = cnvEntry.Script1Param4,
                Script1Param5 = cnvEntry.Script1Param5,
                Script1Param6 = cnvEntry.Script1Param6,
                Script2Param1 = cnvEntry.Script2Param1,
                Script2Param2 = cnvEntry.Script2Param2,
                Script2Param3 = cnvEntry.Script2Param3,
                Script2Param4 = cnvEntry.Script2Param4,
                Script2Param5 = cnvEntry.Script2Param5,
                Script2Param6 = cnvEntry.Script2Param6,
                Quest = cnvEntry.Quest,
                QuestEntry = cnvEntry.QuestEntry,
                PlotIndex = cnvEntry.PlotIndex,
                PlotXpPercentage = cnvEntry.PlotXpPercentage,
                EmotionId = cnvEntry.EmotionId,
                FacialId = cnvEntry.FacialId,
                Listener = cnvEntry.Listener,
                TargetHeight = cnvEntry.TargetHeight,
                NodeId = cnvEntry.NodeId,
                Unskippable = cnvEntry.Unskippable,
                VoTextChanged = cnvEntry.VoTextChanged
            };

            // Convert animations
            foreach (CNVAnimation cnvAnim in cnvEntry.Animations)
            {
                var dlgAnim = new DLGAnimation
                {
                    AnimationId = cnvAnim.AnimationId,
                    Participant = cnvAnim.Participant
                };
                dlgEntry.Animations.Add(dlgAnim);
            }

            return dlgEntry;
        }

        private static DLG.DLGReply ConvertNode(CNVReply cnvReply)
        {
            var dlgReply = new DLG.DLGReply
            {
                ListIndex = cnvReply.ListIndex,
                Comment = cnvReply.Comment,
                CameraAngle = cnvReply.CameraAngle,
                CameraAnim = cnvReply.CameraAnim,
                CameraId = cnvReply.CameraId,
                CameraFov = cnvReply.CameraFov,
                CameraHeight = cnvReply.CameraHeight,
                CameraEffect = cnvReply.CameraEffect,
                Delay = cnvReply.Delay,
                FadeType = cnvReply.FadeType,
                FadeColor = cnvReply.FadeColor,
                FadeDelay = cnvReply.FadeDelay,
                FadeLength = cnvReply.FadeLength,
                Text = cnvReply.Text,
                Script1 = cnvReply.Script1,
                Script2 = cnvReply.Script2,
                Sound = cnvReply.Sound,
                SoundExists = cnvReply.SoundExists,
                VoResRef = cnvReply.VoResRef,
                WaitFlags = cnvReply.WaitFlags,
                Script1Param1 = cnvReply.Script1Param1,
                Script1Param2 = cnvReply.Script1Param2,
                Script1Param3 = cnvReply.Script1Param3,
                Script1Param4 = cnvReply.Script1Param4,
                Script1Param5 = cnvReply.Script1Param5,
                Script1Param6 = cnvReply.Script1Param6,
                Script2Param1 = cnvReply.Script2Param1,
                Script2Param2 = cnvReply.Script2Param2,
                Script2Param3 = cnvReply.Script2Param3,
                Script2Param4 = cnvReply.Script2Param4,
                Script2Param5 = cnvReply.Script2Param5,
                Script2Param6 = cnvReply.Script2Param6,
                Quest = cnvReply.Quest,
                QuestEntry = cnvReply.QuestEntry,
                PlotIndex = cnvReply.PlotIndex,
                PlotXpPercentage = cnvReply.PlotXpPercentage,
                EmotionId = cnvReply.EmotionId,
                FacialId = cnvReply.FacialId,
                Listener = cnvReply.Listener,
                TargetHeight = cnvReply.TargetHeight,
                NodeId = cnvReply.NodeId,
                Unskippable = cnvReply.Unskippable,
                VoTextChanged = cnvReply.VoTextChanged
            };

            // Convert animations
            foreach (CNVAnimation cnvAnim in cnvReply.Animations)
            {
                var dlgAnim = new DLGAnimation
                {
                    AnimationId = cnvAnim.AnimationId,
                    Participant = cnvAnim.Participant
                };
                dlgReply.Animations.Add(dlgAnim);
            }

            return dlgReply;
        }

        // Helper method to convert CNVLink to DLGLink
        private static DLG.DLGLink ConvertLink(CNVLink cnvLink, DLG.DLGNode dlgNode)
        {
            return new DLG.DLGLink(dlgNode, cnvLink.ListIndex)
            {
                Active1 = cnvLink.Active1,
                Active2 = cnvLink.Active2,
                Logic = cnvLink.Logic,
                Active1Not = cnvLink.Active1Not,
                Active2Not = cnvLink.Active2Not,
                Active1Param1 = cnvLink.Active1Param1,
                Active1Param2 = cnvLink.Active1Param2,
                Active1Param3 = cnvLink.Active1Param3,
                Active1Param4 = cnvLink.Active1Param4,
                Active1Param5 = cnvLink.Active1Param5,
                Active1Param6 = cnvLink.Active1Param6,
                Active2Param1 = cnvLink.Active2Param1,
                Active2Param2 = cnvLink.Active2Param2,
                Active2Param3 = cnvLink.Active2Param3,
                Active2Param4 = cnvLink.Active2Param4,
                Active2Param5 = cnvLink.Active2Param5,
                Active2Param6 = cnvLink.Active2Param6,
                IsChild = cnvLink.IsChild,
                Comment = cnvLink.Comment
            };
        }
    }
}

