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
            List<IMySlimBlock> Blocks = new List<IMySlimBlock>();
            processing.GetBlocks(Blocks);
            return Blocks.First().FatBlock.GetOwnerFactionTag();
        }

        public static bool isSameFaction(IMyCubeBlock blockA, IMyCubeBlock blockB)
        {
            if (blockA.GetOwnerFactionTag() == blockB.GetOwnerFactionTag()) return true;
            return false;
        }

        public static bool isSameFaction(IMyCubeGrid processing, IMyCubeBlock blockB)
        {
            List<IMySlimBlock> Blocks = new List<IMySlimBlock>();
            processing.GetBlocks(Blocks);
            if (Blocks.First().FatBlock.GetOwnerFactionTag() == blockB.GetOwnerFactionTag()) return true;
            return false;
        }

        //Ownership

        public static bool isSameOwner(IMyCubeBlock blockA, IMyCubeBlock blockB)
        {
            if (blockA.OwnerId == blockB.OwnerId) return true;
            return false;
        }

        public static bool isSameOwner(long playerID, IMyCubeBlock blockB)
        {
            if (playerID == blockB.OwnerId) return true;
            return false;
        }

        public static bool isSameFactionOrOwner(IMyCubeBlock blockA, IMyCubeBlock blockB)
        {
            bool result = false;
            if (isSameOwner(blockA, blockB)) result = true;
            if (isSameFaction(blockA, blockB)) result = true;
            return result;
        }

        internal static bool isSameFactionOrOwner(IMyCubeGrid gridA, IMyCubeBlock blockB)
        {
            List<IMySlimBlock> Blocks = new List<IMySlimBlock>();
            gridA.GetBlocks(Blocks);
            return isSameFactionOrOwner(Blocks.First().FatBlock, blockB);
        }
    }
}
