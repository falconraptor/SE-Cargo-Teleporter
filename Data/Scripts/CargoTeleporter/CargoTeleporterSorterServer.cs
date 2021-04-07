using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
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
    public class CargoTeleporterSorterServer : MyGameLogicComponent
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

        public override void UpdateBeforeSimulation100()
        {
            base.UpdateBeforeSimulation100();
            if (_cargoTeleporter == null) return;
            try
            {
                if (!((IMyFunctionalBlock) _cargoTeleporter).Enabled)
                {
                    Write(_cargoTeleporter.DisplayNameText + " is powered off");
                    return;
                }

                Write(_cargoTeleporter.DisplayNameText + " is powered on");
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
                
                Write("MainRun");
                if (_cargoTeleporter.BlockDefinition.SubtypeName != "LargeBlockSmallSorterTeleport" &&
                    _cargoTeleporter.BlockDefinition.SubtypeName != "SmallBlockMediumSorterTeleport") return;
                
                ParseName(out var name, out var gridName, out var toMode);
                    
                if (_inventory == null) _inventory = _cargoTeleporter.GetInventory();
                if (toMode && _inventory.Empty()) return;
                    
                Write("GetBlocks");
                var fatBlocks = new List<IMySlimBlock>();
                _cargoTeleporter.CubeGrid.GetBlocks(fatBlocks, x => x?.FatBlock != null);
                var cubeBlocks = fatBlocks.Cast<IMyCubeBlock>();

                if (name.Length < 2)
                {
                    Write("Name too small");
                    return;
                }

                IMyEntity target = null;

                if (gridName.Length > 2 && gridName != _cargoTeleporter.CubeGrid.CustomName)
                {
                    GetTarget(gridName, name, _cargoTeleporter.CubeGrid, out target);
                }
                else
                {
                    target = cubeBlocks.First(x => x?.DisplayNameText == name && OwnershipUtils.isSameFactionOrOwner(_cargoTeleporter, x));
                }

                if (target == null)
                {
                    Write("target null");
                    return;
                }

                var targetInventory = target.GetInventory();
                if (!targetInventory.IsFull && toMode)
                    targetInventory.TransferItemFrom(_inventory, 0, null, true, null, false);
                else if (!targetInventory.Empty() && !_inventory.IsFull && !toMode)
                    _inventory.TransferItemFrom(targetInventory, 0, null, true, null, false);
            }
            catch (Exception ex)
            {
                Logging.WriteLine(ex.Message);
            }
        }

        private void Write(string v)
        {
            if (constantStuff.debugSorter) Logging.WriteLine(v);
        }

        private void ParseName(out string name, out string gridName, out bool toMode)
        {
            name = "";
            gridName = "";
            toMode = true;

            if (_cargoTeleporter.DisplayNameText.Contains("G:"))
            {
                var start = _cargoTeleporter.DisplayNameText.IndexOf("G:") + 2;
                gridName = _cargoTeleporter.DisplayNameText.Substring(start, _cargoTeleporter.DisplayNameText.IndexOf(_cargoTeleporter.DisplayNameText[start - 3], start) - start);
            }

            if (_cargoTeleporter.DisplayNameText.Contains("T:"))
            {
                var start = _cargoTeleporter.DisplayNameText.IndexOf("T:") + 2;
                name = _cargoTeleporter.DisplayNameText.Substring(start, _cargoTeleporter.DisplayNameText.IndexOf(_cargoTeleporter.DisplayNameText[start - 3], start) - start);
            }
            else if (_cargoTeleporter.DisplayNameText.Contains("F:"))
            {
                var start = _cargoTeleporter.DisplayNameText.IndexOf("F:") + 2;
                name = _cargoTeleporter.DisplayNameText.Substring(start, _cargoTeleporter.DisplayNameText.IndexOf(_cargoTeleporter.DisplayNameText[start - 3], start) - start);
                toMode = false;
            }
        }

        private Void GetTarget(string gridName, string name, IMyCubeGrid startingGrid, out IMyEntity target)
        {
            var gridsToProcess = new List<IMyCubeGrid>();
            var gridsProcessed = new HashSet<IMyCubeGrid>();
            target = null;
            
            gridsToProcess.Add(startingGrid);

            while (target == null && gridsToProcess.Count > 0)
            {
                var processing = gridsToProcess.Pop();
                Write("Processing Grid: " + processing.DisplayName);
                
                var fatBlocks = new List<IMySlimBlock>();
                processing.GetBlocks(fatBlocks, x => x?.FatBlock != null);
                var gridBlocks = fatBlocks.Cast<IMyCubeBlock>();

                if (processing.DisplayName == gridName)
                {
                    
                    target = gridBlocks.First(x => x?.DisplayNameText == name);
                    break;
                }
                
                foreach (var antenna in gridBlocks.Where(x => x is IMyRadioAntenna && OwnershipUtils.isSameFactionOrOwner(processing, x)).Cast<IMyRadioAntenna>())
                {
                    var sphere = new BoundingSphereD(antenna.GetPosition(), antenna.Radius);
                    gridsToProcess.AddRange(MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).Where(x => x is IMyCubeGrid).Cast<IMyCubeGrid>().Where(x => !gridsProcessed.Contains(x) && !gridsToProcess.Contains(x)));
                }

                foreach (var antenna in gridBlocks.Where(x => x is IMyLaserAntenna && OwnershipUtils.isSameFactionOrOwner(processing, x)).Cast<IMyLaserAntenna>().Where(x => x.Status == MyLaserAntennaStatus.Connected))
                {
                    var sphere = new BoundingSphereD(antenna.TargetCoords, 5);
                    gridsToProcess.AddRange(MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).Where(x => x is IMyCubeGrid).Cast<IMyCubeGrid>().Where(x => !gridsProcessed.Contains(x) && !gridsToProcess.Contains(x)));
                }

                gridsProcessed.Add(processing);
            }
        }
    }
}