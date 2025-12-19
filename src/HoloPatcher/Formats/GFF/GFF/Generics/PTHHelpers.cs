using System.Collections.Generic;
using System.Linq;
using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py
    // Original: construct_pth and dismantle_pth functions
    public static class PTHHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:160-182
        // Original: def construct_pth(gff: GFF) -> PTH:
        public static PTH ConstructPth(GFF gff)
        {
            var pth = new PTH();
            var root = gff.Root;

            var connectionsList = root.Acquire<GFFList>("Path_Conections", new GFFList());
            var pointsList = root.Acquire<GFFList>("Path_Points", new GFFList());

            foreach (var pointStruct in pointsList)
            {
                int connections = pointStruct.Acquire<int>("Conections", 0);
                int firstConnection = pointStruct.Acquire<int>("First_Conection", 0);
                float x = pointStruct.Acquire<float>("X", 0.0f);
                float y = pointStruct.Acquire<float>("Y", 0.0f);

                int sourceIndex = pth.Add(x, y);

                for (int i = firstConnection; i < firstConnection + connections; i++)
                {
                    var connectionStruct = connectionsList.At(i);
                    if (connectionStruct == null)
                    {
                        continue;
                    }
                    int target = connectionStruct.Acquire<int>("Destination", 0);
                    pth.Connect(sourceIndex, target);
                }
            }

            return pth;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/pth.py:185-209
        // Original: def dismantle_pth(pth: PTH, game: Game = Game.K2, *, use_deprecated: bool = True) -> GFF:
        public static GFF DismantlePth(PTH pth, Game game = Game.K2, bool useDeprecated = true)
        {
            var gff = new GFF(GFFContent.PTH);
            var root = gff.Root;

            var connectionsList = new GFFList();
            root.SetList("Path_Conections", connectionsList);
            var pointsList = new GFFList();
            root.SetList("Path_Points", pointsList);

            int pointIndex = 0;
            foreach (var point in pth)
            {
                var outgoingConnections = pth.Outgoing(pointIndex);

                var pointStruct = pointsList.Add(2);
                pointStruct.SetUInt32("Conections", (uint)outgoingConnections.Count);
                pointStruct.SetUInt32("First_Conection", (uint)connectionsList.Count);
                pointStruct.SetSingle("X", point.X);
                pointStruct.SetSingle("Y", point.Y);

                foreach (var outgoing in outgoingConnections)
                {
                    var connectionStruct = connectionsList.Add(3);
                    connectionStruct.SetUInt32("Destination", (uint)outgoing.TargetIndex);
                }

                pointIndex++;
            }

            return gff;
        }
    }
}