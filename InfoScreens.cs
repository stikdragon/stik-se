public Program() {
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

//
// Create LCD panels which have a Custom Data field of "mass"
// and they will display the total mass of the ship, in tonnes
//
public void Main(string argument, UpdateType updateSource) {
    IMyShipController gController = GridTerminalSystem.GetBlockWithName("Cockpit 2") as IMyShipController;

    int totalMass = (int)(gController.CalculateShipMass().TotalMass / 1000.0);
    string s = "Mass: " + totalMass + " T";

    List<IMyTerminalBlock> tmp = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(tmp);
    foreach(var b in tmp) {
        var x = b as IMyTextPanel;
        if (x.CustomData.Equals("mass"))
            x.WriteText(s);
   }    
}
