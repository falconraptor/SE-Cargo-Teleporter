﻿using System;
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
    // Class to allow caching the reference
    // Allows us to set the 'out' value once in parse, and not worry about changing it after
    internal class NameInfo
	{
        public string name;
        public string grid;

        public void Clear()
		{
            name = default;
            grid = default;
		}
	
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "LargeBlockSmallSorterTeleport",
        "SmallBlockMediumSorterTeleport")]
    public class CargoTeleporterSorterServer : MyGameLogicComponent
    {
        private MyCubeBlock _cargoTeleporter;
        private IMyInventory _inventory;
        private MyObjectBuilder_EntityBase _objectBuilder;
        private string _status;

        //Cached to avoid creating new instances every 100 ticks
        private NameInfo _squareName;
        private NameInfo _angleName;
        private NameInfo _curlyName;



        private const char StartSquareBracket = '[';
        private const char StartCurlyBracket = '{';
        private const char StartAngleBracket = '<';
        private const char StopSquareBracket = ']';
        private const char StopCurlyBracket = '}';
        private const char StopAngleBracket = '>';

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            _objectBuilder = objectBuilder;
            _cargoTeleporter = Entity as MyCubeBlock;

            _squareName = new NameInfo();
            _angleName = new NameInfo();
            _curlyName = new NameInfo();

            base.Init(objectBuilder);
            (_cargoTeleporter as IMyTerminalBlock).AppendingCustomInfo += AppendingCustomInfo;
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

                //We use true, if it goes through the ref; (which I treat like an out) it will be changed.
                //If it goes through the linq; then we are unchanged and true is the correct value. (we have found the grid, it is this one)
                bool gridFound = true;
                var target = gridName.Length > 2 && gridName != _cargoTeleporter.CubeGrid.Name ? 
                    GetTarget(gridName, name, _cargoTeleporter.CubeGrid, ref gridFound) : 
                    cubeBlocks.First(x => x?.DisplayNameText == name && OwnershipUtils.isSameFactionOrOwner(_cargoTeleporter, x));


                var disonnectedStatus = "Status: Disconnected";
                //If grid not found, 
                if (!gridFound)
                {
                    Write("Grid Not Found");
                }
                disonnectedStatus += $"\nGrid: {(gridFound ? "" : "Not ")}Found";
                if (target == null)
                {
                    Write("Target Not Found");
                }
                //This message should only ever say T/S Not Found, but in case I flubbed it; it can say found
                disonnectedStatus += $"\n{(toMode ? "Target" : "Source")}: {(target != null ? "" : "Not ")}Found";

                if (target == null || !gridFound)
				{
                    UpdateStatus(disonnectedStatus);
                    return;
				}

                var targetInventory = target.GetInventory();
                var status = "Status: Connected\nTarget: ";
                var targetStatus = targetInventory.IsFull ? "Full" : targetInventory.Empty() ? "Empty" : "Some";
                var inventoryStatus = _inventory.IsFull ? "Full" : _inventory.Empty() ? "Empty" : "Some";
                status += toMode ? targetStatus : inventoryStatus;
                status += "\nSource: ";
                status += !toMode ? targetStatus : inventoryStatus;

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

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.Clear();
            sb.Append(_status);
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

        private int IndexOfStartBracket(string str, int index, ref char stopBracket)
		{

            for (var i = index; i < str.Length; i++)
            {
                var c = str[i];
                switch(c)
                {
                    case StartAngleBracket:
                        stopBracket = StopAngleBracket;
                        return i;
                    case StartCurlyBracket:
                        stopBracket = StopCurlyBracket;
                        return i;
                    case StartSquareBracket:
                        stopBracket = StopSquareBracket;
                        return i;
                    default:
                        continue;
                }
            }
            return -1;
        }

        ///<remarks>
        /// From and To cannot be cached.
        /// To cache these values; create a copy.
        /// </remarks>
        private void ParseName(ref NameInfo from, ref NameInfo to)
        {
            var displayName = _cargoTeleporter.DisplayNameText;
            //For ease of reading
            const int NotFound = -1;
             const char ModeDelimiter = ':';
            const char ToMode = 'T';
            const char FromMode = 'F';
            const char GlobalMode = 'G';
            //Name Info based on bracket type

            _squareName.Clear();
            _curlyName.Clear();
            _angleName.Clear();


            var workingIndex = 0;
            while (true)
            {
                if (workingIndex > displayName.Length)
                {
                    Write("Parsing Name - End Of String");
                    break;
                }

                char matchingBracket = ' ';
                var start = IndexOfStartBracket(displayName, workingIndex, ref matchingBracket);
                if (start == NotFound)
                {
                    Write("Parsing Name - Starting Bracket '[ < {' Not Found");
                    break;
                }

                var stop = displayName.IndexOf(matchingBracket, start);
                if (stop == NotFound)
                {
                    Write("Parsing Name - Closing ']' Not Found");
                    break;
                }
                var delimiter = displayName.IndexOf(ModeDelimiter, start, stop - start);
                if (delimiter == NotFound)
                {
                    workingIndex = stop + 1; //We jump to stop instead of start because the delimeter does not exist; not because it is invalid
                    Write("Parsing Name - Missing ':' in [] pair");
                    continue;
                }

                //Trims [mode:name] to just mode and name
                var modePart = displayName.Substring(start + 1, delimiter - start - 1);
                var namePart = displayName.Substring(delimiter + 1, stop - delimiter - 1);

                //Parses name
                namePart = namePart.Trim();

                //Parses mode
                //First strip whitespace
                modePart = modePart.Trim();

                if (modePart.Length != 1)
                {
                    //Mode is invalid, advance start and resume search
                    workingIndex = start + 1;
                    Write($"Parsing Name - '{modePart}' is not 1 charachter.");
                    continue;
                }
                //Make uppercase
                var modeChar = char.ToUpper(modePart[0]);
                NameInfo current;
                switch(matchingBracket)
				{
                    case StopCurlyBracket:
                        current = _curlyName;
                        break;
                    case StopSquareBracket:
                        current = _squareName;
                        break;
                    case StopAngleBracket:
                        current = _angleName;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(matchingBracket));
				}

                //Determine which name
                switch (modeChar)
                {
                    case ToMode:
                        current.name = namePart;
                        to = current;
                        break;
                    case FromMode:
                        current.name = namePart;
                        from = current;
                        break;
                    case GlobalMode:
                        current.grid = namePart;
                        break;
                    default:
                        //Mode is invalid char, advance start and resume search
                        workingIndex = start + 1;
                        Write($"Parsing Name - '{modePart}' is not a valid charachter.");
                        continue;
                }
                workingIndex = stop + 1;

            }
            var fromStr = from != null ? $"'{from.name}' ON '{from.grid}'" : "NULL";
            var toStr = from != null ? $"'{to.name}' ON '{to.grid}'" : "NULL";
            Write($"{displayName}\n\tFrom: {fromStr}\n\tTo: {toStr}");
        }

        private IMyEntity GetTarget(string gridName, string name, IMyCubeGrid startingGrid, ref bool foundGrid)
        {
            var gridsToProcess = new List<IMyCubeGrid>();
            var gridsProcessed = new HashSet<IMyCubeGrid>();
            IMyEntity target = null;
            foundGrid = false;
            
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