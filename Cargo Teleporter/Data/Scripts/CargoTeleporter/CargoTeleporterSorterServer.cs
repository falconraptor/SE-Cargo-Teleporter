using System;
using System.Collections.Generic;
using System.Linq;

using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Lights;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using SpaceEngineers.Game.ModAPI;
using System.Timers;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.Game.ObjectBuilders;
using VRage.ObjectBuilders;
using VRage.Library.Utils;
using VRage.ModAPI;
using VRage.Utils;

using Sandbox.ModAPI.Interfaces.Terminal;
using Sandbox.Game.Entities.Inventory;

using IMyFunctionalBlock = Sandbox.ModAPI.IMyFunctionalBlock;
using IMyLaserAntenna = Sandbox.ModAPI.IMyLaserAntenna;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;

namespace CargoTeleporter
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, new string[] { "LargeBlockSmallSorterTeleport", "SmallBlockMediumSorterTeleport" })]
    public class CargoTeleporterSorterServer : MyGameLogicComponent
    {
        MyObjectBuilder_EntityBase ObjectBuilder;
        IMyCubeBlock CargoTeleporter = null;
        VRage.Game.ModAPI.IMyInventory inventory = null;
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            ObjectBuilder = objectBuilder;
            CargoTeleporter = Entity as IMyCubeBlock;
            base.Init(objectBuilder);
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return copy ? ObjectBuilder.Clone() as MyObjectBuilder_EntityBase : ObjectBuilder;
        }

        public override void Close()
        {
            base.Close();
            Logging.close();
        }

        public override void UpdateAfterSimulation()
        {
            base.UpdateAfterSimulation();
        }
        public override void UpdateAfterSimulation10()
        {
            base.UpdateAfterSimulation10();
        }
        public override void UpdateAfterSimulation100()
        {
            base.UpdateAfterSimulation100();
        }
        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();
        }
        public override void UpdateBeforeSimulation10()
        {
            base.UpdateBeforeSimulation10();
        }
        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
        }
        public override void UpdatingStopped()
        {
            base.UpdatingStopped();
        }

        public override void UpdateBeforeSimulation100()
        {
            base.UpdateBeforeSimulation100();
            if (CargoTeleporter == null) return;
            try
            {
                if (!(CargoTeleporter as IMyFunctionalBlock).Enabled)
                {
                    if (constantStuff.debugSorter) Logging.WriteLine(CargoTeleporter.DisplayNameText + " is powered off");
                    return;
                }
                else
                {
                    if (constantStuff.debugSorter) Logging.WriteLine(CargoTeleporter.DisplayNameText + " is powered on");
                }
            }
            catch (Exception ex)
            {
                Logging.WriteLine(ex.Message);
            }
            try
            {
                if (MyAPIGateway.Session == null)
                {
                    if (constantStuff.debugSorter) Logging.WriteLine("MyAPIGateway.Session is null");
                    return;
                }
                if (constantStuff.debugSorter) Logging.WriteLine("MainRun");
                if (inventory == null) inventory = CargoTeleporter.GetInventory(0);
                if (CargoTeleporter.BlockDefinition.SubtypeName == "LargeBlockSmallSorterTeleport" || CargoTeleporter.BlockDefinition.SubtypeName == "SmallBlockMediumSorterTeleport")
                {

                    string name = "";
                    string gridName = "";
                    bool toMode = true;
                    if (CargoTeleporter.DisplayNameText.Contains("[G:"))
                    {
                        int start = CargoTeleporter.DisplayNameText.IndexOf("[G:") + 3;
                        int end = CargoTeleporter.DisplayNameText.IndexOf("]", start);
                        gridName = CargoTeleporter.DisplayNameText.Substring(start, end - start);
                    }
                    if (CargoTeleporter.DisplayNameText.Contains("<G:"))
                    {
                        int start = CargoTeleporter.DisplayNameText.IndexOf("<G:") + 3;
                        int end = CargoTeleporter.DisplayNameText.IndexOf(">", start);
                        gridName = CargoTeleporter.DisplayNameText.Substring(start, end - start);
                    }
                    if (CargoTeleporter.DisplayNameText.Contains("[T:"))
                    {
                        int start = CargoTeleporter.DisplayNameText.IndexOf("[T:") + 3;
                        int end = CargoTeleporter.DisplayNameText.IndexOf("]", start);
                        name = CargoTeleporter.DisplayNameText.Substring(start, end - start);
                    }
                    else if (CargoTeleporter.DisplayNameText.Contains("[F:"))
                    {
                        int start = CargoTeleporter.DisplayNameText.IndexOf("[F:") + 3;
                        int end = CargoTeleporter.DisplayNameText.IndexOf("]", start);
                        name = CargoTeleporter.DisplayNameText.Substring(start, end - start);
                        toMode = false;
                    }
                    if (CargoTeleporter.DisplayNameText.Contains("<T:"))
                    {
                        int start = CargoTeleporter.DisplayNameText.IndexOf("<T:") + 3;
                        int end = CargoTeleporter.DisplayNameText.IndexOf(">", start);
                        name = CargoTeleporter.DisplayNameText.Substring(start, end - start);
                    }
                    else if (CargoTeleporter.DisplayNameText.Contains("<F:"))
                    {
                        int start = CargoTeleporter.DisplayNameText.IndexOf("<F:") + 3;
                        int end = CargoTeleporter.DisplayNameText.IndexOf(">", start);
                        name = CargoTeleporter.DisplayNameText.Substring(start, end - start);
                        toMode = false;
                    }

                    if (toMode && inventory.Empty()) return;
                    //long playerId = CargoTeleporter.OwnerId;
                    //Get Antenna
                    Sandbox.ModAPI.Ingame.IMyRadioAntenna ant = null;

                    if (constantStuff.debugSorter) Logging.WriteLine("GetBlocks");

                    List<IMySlimBlock> Blocks = new List<IMySlimBlock>();

                    List<IMySlimBlock> slimAnts = new List<IMySlimBlock>();
                    CargoTeleporter.CubeGrid.GetBlocks(slimAnts, x => x is IMySlimBlock);
                    foreach (IMySlimBlock block in slimAnts)
                    {
                        if (block.FatBlock != null)
                        {
                            Blocks.Add(block);
                        }
                    }

                    IMyEntity targetEntAnts = null;
                    targetEntAnts = Blocks.Where(x => x.FatBlock != null && x.FatBlock is Sandbox.ModAPI.Ingame.IMyRadioAntenna).First().FatBlock;
                    if (targetEntAnts == null) Logging.WriteLine("targetEnt null");

                    if (constantStuff.debugSorter) Logging.WriteLine("IMyRadioAntenna");
                    ant = targetEntAnts as Sandbox.ModAPI.Ingame.IMyRadioAntenna;

                    if (constantStuff.debugSorter) Logging.WriteLine("VRageMath");
                    VRageMath.BoundingSphereD sphere = new VRageMath.BoundingSphereD(ant.GetPosition(), ant.Radius);
                    if (constantStuff.debugSorter) Logging.WriteLine("VRageMath2");

                    if (constantStuff.debugSorter) Logging.WriteLine("" + CargoTeleporter.OwnerId);
                    try
                    {
                        List<IMyEntity> entities = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).Where(x => x is IMyCubeBlock).ToList();
                        
                        if (constantStuff.debugSorter) Logging.WriteLine("Entitys");
                        HashSet<IMyCubeBlock> gridBlocks = new HashSet<IMyCubeBlock>();
                        foreach (IMyCubeBlock slim in entities)
                        {
                            gridBlocks.Add(slim);
                        }
                        if (constantStuff.debugSorter) Logging.WriteLine("PostGrids");
                        if (constantStuff.debugSorter) Logging.WriteLine("entities " + entities.Count);
                        if (constantStuff.debugSorter) Logging.WriteLine("gridBlocks " + gridBlocks.Count);
                        DateTime startBlockLoop = DateTime.Now;

                        if (!CargoTeleporter.DisplayNameText.Contains("-Off-"))
                        {

                            DateTime compStart = DateTime.Now;

                            if (name.Length > 2)
                            {
                                if (constantStuff.debugSorter) Logging.WriteLine("PostName " + name);
                                IMyEntity targetEnt = null;
                                try
                                {
                                    targetEnt = gridBlocks.Where(x => x != null && x.DisplayNameText != null && x.DisplayNameText == name && OwnershipUtils.isSameFactionOrOwner(CargoTeleporter, x)).First();
                                }
                                catch (InvalidOperationException ex)
                                {
                                    if (gridName.Length > 2)
                                    targetEnt = BeginRecursiveSearch(targetEnt, ant, gridName, name);
                                }
                                if (targetEnt == null) Logging.WriteLine("targetEnt null");
                                if (targetEnt != null && targetEnt is IMyCubeBlock)
                                {
                                    VRage.Game.ModAPI.IMyInventory inventoryTO = targetEnt.GetInventory(0);
                                    if (!inventoryTO.IsFull && !inventory.Empty() && toMode)
                                    {
                                        inventoryTO.TransferItemFrom(inventory, 0, null, true, inventory.GetItems()[0].Amount, false);
                                    }
                                    else if (!inventoryTO.Empty() && !inventory.IsFull && !toMode)
                                    {
                                        inventory.TransferItemFrom(inventoryTO, 0, null, true, inventoryTO.GetItems()[0].Amount, false);
                                    }
                                }
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        Logging.WriteLine(ex.Message);
                    }
                }

            }
            catch (Exception ex)
            {
                Logging.WriteLine(ex.Message);
            }

        }

        private IMyEntity BeginRecursiveSearch(IMyEntity targetEnt, Sandbox.ModAPI.Ingame.IMyRadioAntenna ant, string GridName, string blockName)
        {
            List<IMyCubeGrid> GridsToProcess = new List<IMyCubeGrid>();
            List<IMyCubeGrid> GridsProcessed = new List<IMyCubeGrid>();
            if (constantStuff.debugSorter) write("Going Deep for: " + GridName);

            VRageMath.BoundingSphereD sphere = new VRageMath.BoundingSphereD(ant.GetPosition(), ant.Radius);
            foreach (IMyEntity grid in MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).Where(x => x is IMyCubeGrid).ToList())
            {
                GridsToProcess.Add(grid as IMyCubeGrid);
            }
            if (constantStuff.debugSorter) write("Inital Population: " + GridsToProcess.Count);
            targetEnt = null;

            while (targetEnt == null && GridsToProcess.Count > 0)
            {
                IMyCubeGrid processing = GridsToProcess[0];
                if (constantStuff.debugSorter) write("Processing Grid: " + processing.DisplayName);
                if (processing.DisplayName == GridName)
                {
                    //FOUND IT!
                    if (constantStuff.debugSorter) write("Found Grid");
                    HashSet<IMyCubeBlock> Blocks = new HashSet<IMyCubeBlock>();
                    List<IMySlimBlock> gridBlocks = new List<IMySlimBlock>();
                    processing.GetBlocks(gridBlocks, x => x.FatBlock != null);
                    foreach (IMySlimBlock slim in gridBlocks)
                    {
                        Blocks.Add(slim.FatBlock);
                    }
                    try
                    {
                        targetEnt = Blocks.Where(x => x != null && x.DisplayNameText != null && x.DisplayNameText == blockName).First();
                        if (targetEnt == null) write("WARNING! TARGET ENT IS NULL!!!!!!!");
                        else { if (constantStuff.debugSorter) write("TargetEnt is: " + targetEnt.ToString()); }
                        return targetEnt;
                    }
                    catch (InvalidOperationException ex)
                    {
                        //There was nothing, Return as we failed, due to no reciever on grid.
                        return null;
                    }
                }

                try
                {
                    if (processing.SmallOwners.Contains(ant.OwnerId) || OwnershipUtils.isSameFactionOrOwner(processing, ant as IMyCubeBlock))
                    {
                        //NOT FOUND, FIND MORE ANTENNAS PLOX
                        List<IMySlimBlock> slimAnts = new List<IMySlimBlock>();
                        processing.GetBlocks(slimAnts, x => x is IMySlimBlock && x.FatBlock is Sandbox.ModAPI.Ingame.IMyRadioAntenna);
                        foreach (IMySlimBlock block in slimAnts)
                        {
                            sphere = new VRageMath.BoundingSphereD(block.FatBlock.GetPosition(), (block.FatBlock as Sandbox.ModAPI.Ingame.IMyRadioAntenna).Radius);

                            foreach (IMyEntity grid in MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).Where(x => x is IMyCubeGrid).ToList())
                            {
                                if (!(GridsProcessed.Contains(grid)) && !(GridsToProcess.Contains(grid)))
                                {
                                    if (constantStuff.debugSorter) write("Adding " + (grid as IMyCubeGrid).DisplayName + " to GridsToProcess");
                                    GridsToProcess.Add(grid as IMyCubeGrid);
                                }
                            }
                        }
                    }
                }
                catch { }
                GridsProcessed.Add(processing);
                GridsToProcess.Remove(processing);
            }

            return null;

        }

        private void write(string v)
        {
            if (constantStuff.debugSorter) Logging.WriteLine(v);
        }
    }
}
