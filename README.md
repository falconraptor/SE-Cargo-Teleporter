# Cargo Teleporter

## What is it?

This block will teleport items to (or from) another storage container. I cannot model, so vanilla models have been used.

Works with both laser and radio antennas.

Conveyor Sorter Based - Uses power and can be turned off.

Large Ships/Stations: Uses Large Sorter model. 1 MW Power Usage

Small Ships : Uses Small ship varient of Large Sorter model. 500 kW Power Usage

Note: Drain mode will dump to / pull from connected storage.

## How to use

### Teleport to Storage

Add [T:{Block Name}] to the name of the Teleporter. 
ex "Cargo Teleporter [T:Small Cargo Container 2]"

This will push the inventory of the Teleporter into "Small Cargo Container 2".

### Teleport from Storage

Add [F:{Block Name}] to the name of the Teleporter. 
ex "Cargo Teleporter [F:Small Cargo Container 2]"

This will pull the inventory of "Small Cargo Container 2" into the Teleporter.

### Teleport to / from a different grid

Add [G:{Grid Name}] to the name of the Teleporter. 
ex "Cargo Teleporter [G:Static 7763] [T:Small Cargo Container 2]"

This will push the inventory of the Teleporter into "Small Cargo Container 2" on the grid named "Static 7763" if they are connected via an antenna network.

Antenna signal relaying (aka using the chain of antennas you own), you can now use chains/networks of antennas to find specific targets.


### Note

The Teleporter and destination blocks must be owned by the same person or the same faction. 'Nobody' is a valid choice as well.

The Mode (T/F/G) is case insensitive. 
ex Both "[T:Small Cargo Container 2]" and "[t:Small Cargo Container 2]" will push the inventory of the Teleporter into "Small Cargo Container 2".

Leading and trailing spaces are ignored. 
ex "[ T :Small Cargo Container 2]", "[T: Small Cargo Container 2 ]", "[ T : Small Cargo Container 2 ]" are all equivalent to "[T:Small Cargo Container 2]"


## Credits

[Original Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=565601395&searchtext=cargo+teleporter) by [Peter Cashel](https://github.com/pacas00)

Rewritten by [Dustin Surwill](https://github.com/falconraptor) and [Marcus Kertesz](https://github.com/ModernMAK)
