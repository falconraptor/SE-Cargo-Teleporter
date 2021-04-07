using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;

namespace CargoTeleporter
{
    public static class OwnershipUtils
    {

        //Faction
        public static string GetFaction(IMyCubeGrid processing)
        {
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            processing.GetBlocks(blocks, x => x?.FatBlock != null);
            return blocks.First().FatBlock.GetOwnerFactionTag();
        }

        public static bool isSameFaction(IMyCubeBlock blockA, IMyCubeBlock blockB)
        {
            return blockA.GetOwnerFactionTag() == blockB.GetOwnerFactionTag();
        }

        public static bool isSameFaction(IMyCubeGrid processing, IMyCubeBlock blockB)
        {
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            processing.GetBlocks(blocks, x => x?.FatBlock != null);
            return blocks.Count(x => isSameFaction(x.FatBlock, blockB)) / blocks.Count > .5;
        }

        //Ownership

        public static bool isSameOwner(IMyCubeBlock blockA, IMyCubeBlock blockB)
        {
            return blockA.OwnerId == blockB.OwnerId;
        }

        public static bool isSameOwner(long playerID, IMyCubeBlock blockB)
        {
            return playerID == blockB.OwnerId;
        }

        public static bool isSameFactionOrOwner(IMyCubeBlock blockA, IMyCubeBlock blockB)
        {
            return isSameOwner(blockA, blockB) || isSameFaction(blockA, blockB);
        }

        internal static bool isSameFactionOrOwner(IMyCubeGrid gridA, IMyCubeBlock blockB)
        {
            return isSameFaction(gridA, blockB) || gridA.BigOwners.Any(x => isSameOwner(x, blockB));
        }
    }
}
