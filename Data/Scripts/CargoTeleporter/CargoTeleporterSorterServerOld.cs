using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using IMyFunctionalBlock = Sandbox.ModAPI.IMyFunctionalBlock;
using IMyLaserAntenna = Sandbox.ModAPI.Ingame.IMyLaserAntenna;
using IMyRadioAntenna = Sandbox.ModAPI.Ingame.IMyRadioAntenna;

namespace CargoTeleporter
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "LargeBlockSmallSorterTeleport",
        "SmallBlockMediumSorterTeleport")]
    public class CargoTeleporterSorterServerOld : MyGameLogicComponent
    {
        private IMyCubeBlock _cargoTeleporter;
        private IMyInventory _inventory;
        private MyObjectBuilder_EntityBase _objectBuilder;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            _objectBuilder = objectBuilder;
            _cargoTeleporter = Entity as IMyCubeBlock;
            base.Init(objectBuilder);
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return copy ? _objectBuilder.Clone() as MyObjectBuilder_EntityBase : _objectBuilder;
        }

        public override void Close()
        {
            base.Close();
            Logging.close();
        }

        private static T GetFirstFatBlockAs<T>(IEnumerable<IMySlimBlock> list) where T : class
        {
            return list.First(x => x.FatBlock is T).FatBlock as T;
        }

        //Pulled out to simplify reading the update function
        private void ParseName(out string name, out string gridName, out bool toMode)
        {
            name = "";
            gridName = "";
            toMode = true;
            var start = -1;
            var end = -1;
            
            if (_cargoTeleporter.DisplayNameText.Contains("[G:"))
            {
                start = _cargoTeleporter.DisplayNameText.IndexOf("[G:") + 3;
                end = _cargoTeleporter.DisplayNameText.IndexOf("]", start);
            }
            else if (_cargoTeleporter.DisplayNameText.Contains("<G:"))
            {
                start = _cargoTeleporter.DisplayNameText.IndexOf("<G:") + 3;
                end = _cargoTeleporter.DisplayNameText.IndexOf(">", start);
            }
            
            if (end != -1) gridName = _cargoTeleporter.DisplayNameText.Substring(start, end - start);
            start = -1;
            end = -1;

            if (_cargoTeleporter.DisplayNameText.Contains("[T:"))
            {
                start = _cargoTeleporter.DisplayNameText.IndexOf("[T:") + 3;
                end = _cargoTeleporter.DisplayNameText.IndexOf("]", start);
            }
            else if (_cargoTeleporter.DisplayNameText.Contains("[F:"))
            {
                start = _cargoTeleporter.DisplayNameText.IndexOf("[F:") + 3;
                end = _cargoTeleporter.DisplayNameText.IndexOf("]", start);
                toMode = false;
            }
            else if (_cargoTeleporter.DisplayNameText.Contains("<T:"))
            {
                start = _cargoTeleporter.DisplayNameText.IndexOf("<T:") + 3;
                end = _cargoTeleporter.DisplayNameText.IndexOf(">", start);
            }
            else if (_cargoTeleporter.DisplayNameText.Contains("<F:"))
            {
                start = _cargoTeleporter.DisplayNameText.IndexOf("<F:") + 3;
                end = _cargoTeleporter.DisplayNameText.IndexOf(">", start);
                toMode = false;
            }
            
            if (end != -1) name = _cargoTeleporter.DisplayNameText.Substring(start, end - start);
        }

        public override void UpdateBeforeSimulation100()
        {
            base.UpdateBeforeSimulation100();
            if (_cargoTeleporter == null) return;
            try
            {
                if (!(_cargoTeleporter as IMyFunctionalBlock).Enabled)
                {
                    if (constantStuff.debugSorter)
                        Logging.WriteLine(_cargoTeleporter.DisplayNameText + " is powered off");
                    return;
                }

                if (constantStuff.debugSorter) Logging.WriteLine(_cargoTeleporter.DisplayNameText + " is powered on");
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
                if (_inventory == null) _inventory = _cargoTeleporter.GetInventory(0);
                if (_cargoTeleporter.BlockDefinition.SubtypeName == "LargeBlockSmallSorterTeleport" ||
                    _cargoTeleporter.BlockDefinition.SubtypeName == "SmallBlockMediumSorterTeleport")
                {
                    ParseName(out var name, out var gridName, out var toMode);

                    if (toMode && _inventory.Empty()) return;
                    //long playerId = CargoTeleporter.OwnerId;
                    //Get Antenna

                    if (constantStuff.debugSorter) Logging.WriteLine("GetBlocks");
                    var fatBlocksInGrid = new List<IMySlimBlock>();
                    // Gets all slim blocks that have a non-null fatblock
                    _cargoTeleporter.CubeGrid.GetBlocks(fatBlocksInGrid, x => x?.FatBlock != null);

                    //Gets the first RadioAntenna
                    if (constantStuff.debugSorter) Logging.WriteLine("IMyRadioAntenna");
                    var radioAntenna = GetFirstFatBlockAs<IMyRadioAntenna>(fatBlocksInGrid);
                    if (radioAntenna == null) Logging.WriteLine("radioAntenna null");

                    //Gets the first LaserAntenna
                    if (constantStuff.debugSorter) Logging.WriteLine("IMyLaserAntenna");
                    var laserAntenna = GetFirstFatBlockAs<IMyLaserAntenna>(fatBlocksInGrid);
                    if (laserAntenna == null) Logging.WriteLine("laserAntenna null");

                    //Gets the first RadioAntenna
                    if (radioAntenna != null)
                    {
                        if (constantStuff.debugSorter) Logging.WriteLine("Updating using Radio");
                        DoRadioUpdate(name, gridName, toMode, radioAntenna);
                    }
                    else if (laserAntenna != null)
                    {
                        if (constantStuff.debugSorter) Logging.WriteLine("Updating using Radio");
                        DoLaserUpdate(name, gridName, toMode, laserAntenna);
                    }
                    else if (constantStuff.debugSorter)
                    {
                        Logging.WriteLine("Cannot update, all Antenna null");
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteLine(ex.Message);
            }
        }

        private void DoLaserUpdate(string name, string gridName, bool toMode, IMyLaserAntenna radioAntenna)
        {
            throw new NotImplementedException();
        }

        private void DoRadioUpdate(string name, string gridName, bool toMode, IMyRadioAntenna radioAntenna)
        {
            if (constantStuff.debugSorter) Logging.WriteLine("VRageMath");
            var radioRangeSphere = new BoundingSphereD(radioAntenna.GetPosition(), radioAntenna.Radius);
            if (constantStuff.debugSorter) Logging.WriteLine("VRageMath2");

            if (constantStuff.debugSorter) Logging.WriteLine("" + _cargoTeleporter.OwnerId);
            try
            {
                if (constantStuff.debugSorter) Logging.WriteLine("Entities");
                var entities = MyAPIGateway.Entities.GetEntitiesInSphere(ref radioRangeSphere)
                    .Where(x => x is IMyCubeBlock).Cast<IMyCubeBlock>().ToList();
                var gridBlocks = new HashSet<IMyCubeBlock>(entities);
                if (constantStuff.debugSorter) Logging.WriteLine("PostGrids");
                if (constantStuff.debugSorter) Logging.WriteLine("entities " + entities.Count);
                if (constantStuff.debugSorter) Logging.WriteLine("gridBlocks " + gridBlocks.Count);
                var startBlockLoop = DateTime.Now;

                if (!_cargoTeleporter.DisplayNameText.Contains("-Off-"))
                {
                    var compStart = DateTime.Now;

                    if (name.Length > 2)
                    {
                        if (constantStuff.debugSorter) Logging.WriteLine("PostName " + name);
                        IMyEntity targetEnt = null;
                        try
                        {
                            targetEnt = gridBlocks.First(x =>
                                x.DisplayNameText != null && x.DisplayNameText == name &&
                                OwnershipUtils.isSameFactionOrOwner(_cargoTeleporter, x));
                        }
                        catch (InvalidOperationException ex)
                        {
                            if (gridName.Length > 2)
                                targetEnt = BeginRecursiveSearch(radioAntenna, gridName, name);
                        }

                        if (targetEnt == null) Logging.WriteLine("targetEnt null");
                        if (targetEnt != null && targetEnt is IMyCubeBlock)
                        {
                            var inventoryTO = targetEnt.GetInventory(0);
                            if (!inventoryTO.IsFull && !_inventory.Empty() && toMode)
                                inventoryTO.TransferItemFrom(_inventory, 0, null, true, _inventory.GetItems()[0].Amount,
                                    false);
                            else if (!inventoryTO.Empty() && !_inventory.IsFull && !toMode)
                                _inventory.TransferItemFrom(inventoryTO, 0, null, true, inventoryTO.GetItems()[0].Amount, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteLine(ex.Message);
            }
        }

        private IMyEntity BeginRecursiveSearch(IMyRadioAntenna ant, string GridName, string blockName)
        {
            var GridsToProcess = new List<IMyCubeGrid>();
            var GridsProcessed = new List<IMyCubeGrid>();
            if (constantStuff.debugSorter) write("Going Deep for: " + GridName);

            var sphere = new BoundingSphereD(ant.GetPosition(), ant.Radius);
            foreach (var grid in MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).Where(x => x is IMyCubeGrid)
                .ToList()) GridsToProcess.Add(grid as IMyCubeGrid);
            if (constantStuff.debugSorter) write("Inital Population: " + GridsToProcess.Count);
            IMyEntity targetEnt = null;

            while (targetEnt == null && GridsToProcess.Count > 0)
            {
                var processing = GridsToProcess[0];
                if (constantStuff.debugSorter) write("Processing Grid: " + processing.DisplayName);
                if (processing.DisplayName == GridName)
                {
                    //FOUND IT!
                    if (constantStuff.debugSorter) write("Found Grid");
                    var gridBlocks = new List<IMySlimBlock>();
                    processing.GetBlocks(gridBlocks, x => x.FatBlock != null);
                    var Blocks = new HashSet<IMyCubeBlock>(gridBlocks.Cast<IMyCubeBlock>());
                    try
                    {
                        targetEnt = Blocks.First(x =>
                            x != null && x.DisplayNameText != null && x.DisplayNameText == blockName);
                        if (targetEnt == null)
                        {
                            write("WARNING! TARGET ENT IS NULL!!!!!!!");
                        }
                        else
                        {
                            if (constantStuff.debugSorter) write("TargetEnt is: " + targetEnt);
                        }

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
                    if (processing.SmallOwners.Contains(ant.OwnerId) ||
                        OwnershipUtils.isSameFactionOrOwner(processing, ant as IMyCubeBlock))
                    {
                        //NOT FOUND, FIND MORE ANTENNAS PLOX
                        var slimAnts = new List<IMySlimBlock>();
                        processing.GetBlocks(slimAnts, x => x is IMySlimBlock && x.FatBlock is IMyRadioAntenna);
                        foreach (var block in slimAnts)
                        {
                            sphere = new BoundingSphereD(block.FatBlock.GetPosition(),
                                (block.FatBlock as IMyRadioAntenna).Radius);

                            foreach (var grid in MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere)
                                .Where(x => x is IMyCubeGrid).ToList())
                                if (!GridsProcessed.Contains(grid) && !GridsToProcess.Contains(grid))
                                {
                                    if (constantStuff.debugSorter)
                                        write("Adding " + (grid as IMyCubeGrid).DisplayName + " to GridsToProcess");
                                    GridsToProcess.Add(grid as IMyCubeGrid);
                                }
                        }
                    }
                }
                catch
                {
                    // ignored
                }

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