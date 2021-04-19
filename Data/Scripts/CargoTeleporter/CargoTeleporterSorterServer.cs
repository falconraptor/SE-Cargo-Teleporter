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
        private bool _mode;
        private string _grid;
        private string _block;
        private MyCubeBlock _targetBlock;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            _objectBuilder = objectBuilder;
            _cargoTeleporter = Entity as MyCubeBlock;
            base.Init(objectBuilder);
            (_cargoTeleporter as IMyTerminalBlock).AppendingCustomInfo += AppendingCustomInfo;
            (_cargoTeleporter as IMyTerminalBlock).CustomNameChanged += CustomNameChanged;
            if (_inventory == null) _inventory = _cargoTeleporter.GetInventory();
            CustomNameChanged(_cargoTeleporter as IMyTerminalBlock);
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return copy ? _objectBuilder.Clone() as MyObjectBuilder_EntityBase : _objectBuilder;
        }

        public override void Close()
        {
            Logging.Close();
        }

        public override void UpdateBeforeSimulation100()
        {
            if (_cargoTeleporter == null) return;
            if (!((IMyFunctionalBlock) _cargoTeleporter).Enabled) return;

            try
            {
                if (MyAPIGateway.Session == null)
                {
                    Write("MyAPIGateway.Session is null");
                    return;
                }
                
                CustomNameChanged(_cargoTeleporter as IMyTerminalBlock);
                
                if (_mode && _inventory.Empty())
                {
                    UpdateStatus("Status: Source Empty");
                    return;
                }
                
                //We use true, if it goes through the ref; (which I treat like an out) it will be changed.
                //If it goes through the linq; then we are unchanged and true is the correct value. (we have found the grid, it is this one)
                bool gridFound = true;
                var target = _grid.Length > 2 && _grid != _cargoTeleporter.CubeGrid.Name ? 
                    GetTarget(_grid, _block, _cargoTeleporter.CubeGrid, ref gridFound) : _targetBlock;

                var disonnectedStatus = "Status: Disconnected";
                //If grid not found, 
                if (!gridFound) Write("Grid Not Found");
                disonnectedStatus += $"\nGrid: {(gridFound ? "" : "Not ")}Found";
                if (target == null) Write("Target Not Found");
                //This message should only ever say T/S Not Found, but in case I flubbed it; it can say found
                disonnectedStatus += $"\n{(_mode ? "Target" : "Source")}: {(target != null ? "" : "Not ")}Found";

                if (target == null || !gridFound)
				{
                    UpdateStatus(disonnectedStatus);
                    return;
				}
                
                var targetInventory = target.GetInventory();
                var status = "Status: Connected\nTarget: ";
                var targetStatus = targetInventory.IsFull ? "Full" : targetInventory.Empty() ? "Empty" : "Some";
                var inventoryStatus = _inventory.IsFull ? "Full" : _inventory.Empty() ? "Empty" : "Some";
                status += _mode ? targetStatus : inventoryStatus;
                status += "\nSource: ";
                status += !_mode ? targetStatus : inventoryStatus;

                UpdateStatus(status);
                
                if (!targetInventory.IsFull && _mode)
                    targetInventory.TransferItemFrom(_inventory, 0, null, true, null, false);
                else if (!targetInventory.Empty() && !_inventory.IsFull && !_mode)
                    _inventory.TransferItemFrom(targetInventory, 0, null, true, null, false);
            }
            catch (Exception ex)
            {
                Logging.WriteLine(ex.ToString());
            }
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.Clear();
            sb.Append(_status);
        }

        private void CustomNameChanged(IMyTerminalBlock block)
        {
            _block = "";
            _grid = "";
            _mode = true;
            ParseName(ref _block, ref _grid, ref _mode);
            
            if (_block.Length < 2)
            {
                Write("Name too small");
                UpdateStatus("Status: No filters\n\nPlease add [T:Block Name] or [F:Block Name].\nPlease add [G:Grid Name] if using antennas.");
                return;
            }

            if (_grid == "")
            {
                var cubeBlocks = _cargoTeleporter.CubeGrid.GetFatBlocks();
                _targetBlock = cubeBlocks.First(x => x?.DisplayNameText == _block && OwnershipUtils.isSameFactionOrOwner(_cargoTeleporter, x));
            }
        }

        private void UpdateStatus(string status)
        {
            if (status == _status) return;
            _status = status;
            (_cargoTeleporter as IMyTerminalBlock).RefreshCustomInfo();
            if (_cargoTeleporter.IDModule == null) return;
            var share = _cargoTeleporter.IDModule.ShareMode;
            _cargoTeleporter.ChangeOwner(_cargoTeleporter.OwnerId, share == MyOwnershipShareModeEnum.None ? MyOwnershipShareModeEnum.Faction : MyOwnershipShareModeEnum.None);
            _cargoTeleporter.ChangeOwner(_cargoTeleporter.OwnerId, share);
        }

        private void Write(string v)
        {
            if (Config.enableDebug) Logging.WriteLine(v);
        }

        private void ParseName(ref string name, ref string gridName, ref bool toMode)
        {
            var displayName = _cargoTeleporter.DisplayNameText;
            //For ease of reading
            const char startBracket = '[';
            const char stopBracket = ']';
            const char modeDelimiter = ':';
            const char mode = 'T';
            const char toModeLower = 't';
            const char fromMode = 'F';
            const char fromModeLower = 'f';
            const char gridMode = 'G';
            const char gridModeLower = 'g';
            
            var workingIndex = 0;
            while (true)
            {
                if (workingIndex > displayName.Length)
                {
                    Write("Parsing Name - End Of String");
                    break;
                }

                var start = displayName.IndexOf(startBracket, workingIndex);
                if (start == -1)
                {
                    Write("Parsing Name - '[' Not Found");
                    break;
                }

                var stop = displayName.IndexOf(stopBracket, start);
                if (stop == -1)
                {
                    Write("Parsing Name - Closing ']' Not Found");
                    break;
                }
                
                var delimiter = displayName.IndexOf(modeDelimiter, start, stop - start);
                if (delimiter == -1)
                {
                    workingIndex = stop + 1; //We jump to stop instead of start because the delimeter does not exist; not because it is invalid
                    Write("Parsing Name - Missing ':' in [] pair");
                    continue;
                }

                //Trims [mode:name] to just mode and name
                var modePart = displayName.Substring(start + 1, delimiter - start - 1).Trim();
                var namePart = displayName.Substring(delimiter + 1, stop - delimiter - 1).Trim();

                if (modePart.Length != 1)
                {
                    //Mode is invalid, advance start and resume search
                    workingIndex = start + 1;
                    Write($"Parsing Name - '{modePart}' is not 1 character.");
                    continue;
                }
                var modeChar = modePart[0];
                switch (modeChar)
                {
                    case mode:
                    case toModeLower:
                        name = namePart;
                        toMode = true;
                        break;
                    case fromMode:
                    case fromModeLower:
                        name = namePart;
                        toMode = false;
                        break;
                    case gridMode:
                    case gridModeLower:
                        gridName = namePart;
                        break;
                    default:
                        //Mode is invalid char, advance start and resume search
                        workingIndex = start + 1;
                        Write($"Parsing Name - '{modePart}' is not a valid character.");
                        continue;
                }
                workingIndex = stop + 1;

            }
            Write(displayName + ": "+ gridName + ", " + name + ", " + toMode.ToString());
        }

        private IMyCubeBlock GetTarget(string gridName, string name, IMyCubeGrid startingGrid, ref bool foundGrid)
        {
            var gridsToProcess = new List<IMyCubeGrid>();
            var gridsProcessed = new HashSet<IMyCubeGrid>();
            IMyCubeBlock target = null;
            foundGrid = false;
            
            gridsToProcess.Add(startingGrid);

            while (target == null && gridsToProcess.Count > 0)
            {
                var processing = gridsToProcess.Pop();
                // Write("Processing Grid: " + processing.DisplayName);
                
                var fatBlocks = new List<IMySlimBlock>();
                processing.GetBlocks(fatBlocks, x => x?.FatBlock != null);
                var gridBlocks = fatBlocks.ConvertAll(x => x.FatBlock);

                if (processing.DisplayName == gridName)
                {
                    target = gridBlocks.First(x => x?.DisplayNameText == name);
                    foundGrid = true;
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