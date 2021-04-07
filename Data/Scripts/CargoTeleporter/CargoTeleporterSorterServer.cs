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
            if (!((IMyFunctionalBlock) _cargoTeleporter).Enabled) return;

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

                var name = "";
                var gridName = "";
                var toMode = false;
                ParseName(ref name, ref gridName, ref toMode);
                    
                if (_inventory == null) _inventory = _cargoTeleporter.GetInventory();
                if (toMode && _inventory.Empty()) return;
                    
                Write("GetBlocks");
                var fatBlocks = new List<IMySlimBlock>();
                _cargoTeleporter.CubeGrid.GetBlocks(fatBlocks, x => x?.FatBlock != null);
                var cubeBlocks = fatBlocks.ConvertAll(x => x.FatBlock);

                if (name.Length < 2)
                {
                    Write("Name too small");
                    return;
                }

                IMyEntity target = null;

                if (gridName.Length > 2 && gridName != _cargoTeleporter.CubeGrid.CustomName)
                {
                    target = GetTarget(gridName, name, _cargoTeleporter.CubeGrid);
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
                Logging.WriteLine(ex.ToString());
                Logging.WriteLine(ex.StackTrace);
            }
        }

        private void Write(string v)
        {
            if (constantStuff.debugSorter) Logging.WriteLine(v);
        }

        private void ParseName(ref string name, ref string gridName, ref bool toMode)
        {
            if (_cargoTeleporter.DisplayNameText.Contains("G:"))
            {
                var start = _cargoTeleporter.DisplayNameText.IndexOf("G:") + 2;
                var length = _cargoTeleporter.DisplayNameText.IndexOf(GetSymbol(_cargoTeleporter.DisplayNameText[start - 3]), start) - start;
                if (length >= 0) gridName = _cargoTeleporter.DisplayNameText.Substring(start, length);
            }

            if (_cargoTeleporter.DisplayNameText.Contains("T:"))
            {
                var start = _cargoTeleporter.DisplayNameText.IndexOf("T:") + 2;
                var length = _cargoTeleporter.DisplayNameText.IndexOf(GetSymbol(_cargoTeleporter.DisplayNameText[start - 3]), start) - start;
                if (length >= 0) name = _cargoTeleporter.DisplayNameText.Substring(start, length);
            }
            else if (_cargoTeleporter.DisplayNameText.Contains("F:"))
            {
                var start = _cargoTeleporter.DisplayNameText.IndexOf("F:") + 2;
                var length = _cargoTeleporter.DisplayNameText.IndexOf(GetSymbol(_cargoTeleporter.DisplayNameText[start - 3]), start) - start;
                if (length >= 0) name = _cargoTeleporter.DisplayNameText.Substring(start, length);
                toMode = false;
            }
        }

        private static char GetSymbol(char symbol)
        {
            switch (symbol)
            {
                case '{':
                    return '}';
                case '[':
                    return ']';
                case '(':
                    return ')';
                case '<':
                    return '>';
                default:
                    return symbol;
            }
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
                    var sphere = new BoundingSphereD(antenna.TargetCoords, 5);
                    gridsToProcess.AddRange(MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).Where(x => x is IMyCubeGrid).Cast<IMyCubeGrid>().Where(x => !gridsProcessed.Contains(x) && !gridsToProcess.Contains(x)));
                }

                gridsProcessed.Add(processing);
            }

            return target;
        }
    }
}