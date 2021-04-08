using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using IMyFunctionalBlock = Sandbox.ModAPI.IMyFunctionalBlock;
using IMyLaserAntenna = Sandbox.ModAPI.Ingame.IMyLaserAntenna;
using IMyRadioAntenna = Sandbox.ModAPI.Ingame.IMyRadioAntenna;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;

namespace CargoTeleporter
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "LargeBlockSmallSorterTeleport",
        "SmallBlockMediumSorterTeleport")]
    public class CargoTeleporterSorterServer : MyGameLogicComponent
    {
        private MyCubeBlock _cargoTeleporter;
        private IMyInventory _inventory;
        private MyObjectBuilder_EntityBase _objectBuilder;
        private string _status;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            _objectBuilder = objectBuilder;
            _cargoTeleporter = Entity as MyCubeBlock;
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
            if (!((IMyFunctionalBlock) _cargoTeleporter).Enabled) return;

            try
            {
                if (MyAPIGateway.Session == null)
                {
                    Write("MyAPIGateway.Session is null");
                    return;
                }
                
                Write("MainRun");

                var name = "";
                var gridName = "";
                var toMode = true;
                ParseName(ref name, ref gridName, ref toMode);
                
                if (name.Length < 2)
                {
                    Write("Name too small");
                    UpdateStatus("Status: No filters\n\nPlease add [T:Block Name] or [F:Block Name].\nPlease add [G:Grid Name] if using antennas.");
                    return;
                }
                    
                if (_inventory == null) _inventory = _cargoTeleporter.GetInventory();
                if (toMode && _inventory.Empty())
                {
                    UpdateStatus("Status: Source Empty");
                    return;
                }
                    
                Write("GetBlocks");
                var cubeBlocks = _cargoTeleporter.CubeGrid.GetFatBlocks();

                var target = gridName.Length > 2 && gridName != _cargoTeleporter.CubeGrid.Name ? GetTarget(gridName, name, _cargoTeleporter.CubeGrid) : cubeBlocks.First(x => x?.DisplayNameText == name && OwnershipUtils.isSameFactionOrOwner(_cargoTeleporter, x));

                if (target == null)
                {
                    Write("target null");
                    UpdateStatus("Status: Disconnected");
                    return;
                }
                
                var targetInventory = target.GetInventory();

                var status = "Status: Connected\nTarget: ";
                var targetStatus = targetInventory.IsFull ? "Full" : targetInventory.Empty() ? "Empty" : "Some";
                var inventoryStatus = _inventory.IsFull ? "Full" : _inventory.Empty() ? "Empty" : "Some";
                if (toMode) status += targetStatus;
                else status += inventoryStatus;
                status += "\nSource: ";
                if (!toMode) status += targetStatus;
                else status += inventoryStatus;
                UpdateStatus(status);
                
                if (!targetInventory.IsFull && toMode)
                    targetInventory.TransferItemFrom(_inventory, 0, null, true, null, false);
                else if (!targetInventory.Empty() && !_inventory.IsFull && !toMode)
                    _inventory.TransferItemFrom(targetInventory, 0, null, true, null, false);
            }
            catch (Exception ex)
            {
                Logging.WriteLine(ex.ToString());
            }
        }
        
        public override void UpdateOnceBeforeFrame()
        {
            (_cargoTeleporter as IMyTerminalBlock).AppendingCustomInfo += AppendingCustomInfo;
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.Clear();
            sb.Append(_status);
        }

        private void UpdateStatus(string status)
        {
            _status = status;
            (_cargoTeleporter as IMyTerminalBlock).RefreshCustomInfo();
            if (_cargoTeleporter.IDModule == null) return;
            var share = _cargoTeleporter.IDModule.ShareMode;
            _cargoTeleporter.ChangeOwner(_cargoTeleporter.OwnerId, share == MyOwnershipShareModeEnum.None ? MyOwnershipShareModeEnum.Faction : MyOwnershipShareModeEnum.None);
            _cargoTeleporter.ChangeOwner(_cargoTeleporter.OwnerId, share);
        }

        private void Write(string v)
        {
            if (constantStuff.debugSorter) Logging.WriteLine(v);
        }

        private void ParseName(ref string name, ref string gridName, ref bool toMode)
        {
            if (_cargoTeleporter.DisplayNameText.Contains("[G:"))
                gridName = GetNameFromChar("[G:");

            if (_cargoTeleporter.DisplayNameText.Contains("[T:"))
                name = GetNameFromChar("[T:");
            else if (_cargoTeleporter.DisplayNameText.Contains("[F:"))
            {
                name = GetNameFromChar("[F:");
                toMode = false;
            }

            Write(_cargoTeleporter.DisplayNameText + ": "+ gridName + ", " + name + ", " + toMode.ToString());
        }

        private string GetNameFromChar(string ch)
        {
            var start = _cargoTeleporter.DisplayNameText.IndexOf(ch, StringComparison.Ordinal) + 3;
            var length = _cargoTeleporter.DisplayNameText.IndexOf("]", start, StringComparison.Ordinal) - start;
            return length >= 0 ? _cargoTeleporter.DisplayNameText.Substring(start, length) : "";
        }

        private IMyEntity GetTarget(string gridName, string name, IMyCubeGrid startingGrid)
        {
            var gridsToProcess = new List<IMyCubeGrid>();
            var gridsProcessed = new HashSet<IMyCubeGrid>();
            IMyEntity target = null;
            
            gridsToProcess.Add(startingGrid);

            while (target == null && gridsToProcess.Count > 0)
            {
                var processing = gridsToProcess.Pop();
                Write("Processing Grid: " + processing.DisplayName);
                
                var fatBlocks = new List<IMySlimBlock>();
                processing.GetBlocks(fatBlocks, x => x?.FatBlock != null);
                var gridBlocks = fatBlocks.ConvertAll(x => x.FatBlock);

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
                    var sphere = new BoundingSphereD(antenna.TargetCoords, 1);
                    gridsToProcess.AddRange(MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).Where(x => x is IMyCubeGrid).Cast<IMyCubeGrid>().Where(x => !gridsProcessed.Contains(x) && !gridsToProcess.Contains(x)));
                }

                gridsProcessed.Add(processing);
            }

            return target;
        }
    }
}