public Program() {
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
}

//
// How to Use:  
//   Create LCD panels with a Custom Data field starting with "stik:lcd-info"
//   Then the rest of the Custom Data is a configuration string:
//
//     !stik:lcd-info
//     Ship Mass: <mass:,7>
//     
//     Ores in Cargo:
//     ---------------
//         Iron Ore: <item:Ore_Iron, 7>
//       Nickel Ore: <item:Ore_Nickel, 7>
//         Gold Ore: <item:Ore_Gold, 7>
//  
//     Products:
//     ----------
//       Plates:  <Component_SteelPlate, 5>
//       etc...
//
//   Give it a few seconds to update after you make a change, it only checks and parses
//   this string every few seconds, to avoid unnecessary CPU load
//
// EXAMPLE
/*


!stik:lcd-info
          [ORE]       [INGOT]
-----------------------------
Stone:  <item:Ore_Stone, 7>
Iron:   <item:Ore_Iron, 7>    <item:Ingot_Iron, 7>
Magnes: <item:Ore_Magnesium, 7>    <item:Ingot_Magnesium, 7>
Cobalt: <item:Ore_Cobalt, 7>    <item:Ingot_Cobalt, 7>
Nickel: <item:Ore_Nickel, 7>    <item:Ingot_Nickel, 7>
Silver: <item:Ore_Silver, 7>    <item:Ingot_Silver, 7>
Gold:   <item:Ore_Gold, 7>    <item:Ingot_Gold, 7>
-----------------------------
  
           Motors: <item:Component_Motor, 5>
Construction Comp: <item:Component_Construction, 5>
      Steel Plate: <item:Component_SteelPlate, 5>
   Interior Plate: <item:Component_InteriorPlate, 5>
            Glass: <item:Component_BulletproofGlass, 5>
            Grids: <item:Component_MetalGrid, 5>
       Large Tube: <item:Component_LargeTube, 5>
        Smol Tube: <item:Component_SmallTube, 5>
            Solar: <item:Component_SolarCell, 5>
       Power Cell: <item:Component_PowerCell, 5>


*/


//
// Important! You must set this to the name of a cockpit on your grid
// (It is how the script reads the mass of the ship)
//
public static readonly string COCKPIT_NAME = "Cockpit 2";


private int updateTimer = 0;
private readonly int UPDATE_PERIOD = 70; // 700 ticks
private Dictionary<IMyTextPanel, OutputPanel> panels = new Dictionary<IMyTextPanel, OutputPanel>();

public void Main(string argument, UpdateType updateSource) {
    if (updateTimer-- <= 0) {
        updateTimer = UPDATE_PERIOD;
        UpdateScreens(GridTerminalSystem);
    }
}


private void UpdateScreens(IMyGridTerminalSystem gts) {
    //
    // Look for any changes to LCD configs
    //
    List<IMyTerminalBlock> lst = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(lst);
    foreach(var b in lst) {
        var x = b as IMyTextPanel;
        if (x.CustomData.StartsWith("!stik:lcd-info")) {
            OutputPanel op;
            if (!panels.TryGetValue(x, out op)) 
                panels.Add(x, op = new OutputPanel(x));
            if (!x.CustomData.Equals(op.Config))
                UpdatePanelConfig(op);
        }
    }

    //
    // Get list of all cargo, and apply to any screens we have
    //
    Dictionary<string, int> cargo = GetAllCargo(GridTerminalSystem);
    foreach (KeyValuePair<IMyTextPanel, OutputPanel> e in panels) {
        if (String.IsNullOrEmpty(e.Value.Screen.Error))
            e.Key.WriteText(e.Value.Screen.Evaluate(cargo));   
        else
            e.Key.WriteText(e.Value.Screen.Error);    
    }

    // StringBuilder sb = new StringBuilder();
    // foreach (var e in cargo) {
    //     sb.Append(e.Key).Append("\n");
    // foreach (KeyValuePair<IMyTextPanel, OutputPanel> e in panels) 
    //     e.Key.WriteText(sb.ToString());  
}

private void UpdatePanelConfig(OutputPanel p) {
    string s = p.Panel.CustomData;
    if (!s.StartsWith("!stik:lcd-info")) { // guess it's something else now
        panels.Remove(p.Panel);
        return; 
    }
    p.Config = s;
    p.Screen = ScreenThing.Parse(GridTerminalSystem, DeleteFirstLine(s));

}

public static string DeleteFirstLine(string s) {
    int i = s.IndexOf('\n') + 1;
    return s.Substring(i);
}

//
// Returns a Dictionary with all the cargo items in the grid
//
public Dictionary<string, int> GetAllCargo(IMyGridTerminalSystem gts) {
    Dictionary<string, int> res = new Dictionary<string, int>();
    res["power"] = 0;

    //
    // Get a list of all containers in the grid
    //
    List<IMyEntity> containers = new List<IMyEntity>();
    gts.GetBlocksOfType<IMyEntity>(containers);

    foreach (IMyEntity container in containers) {

        //
        // Handle gas containers
        //
        if (container is IMyGasTank) {
            IMyGasTank tank = (IMyGasTank)container;
            string k = "O2";
            if (tank.BlockDefinition.SubtypeId.Contains("Hydrogen")) 
                k = "H2";
            if (!res.ContainsKey("GAS_" + k)) {
                res.Add("GAS_" + k, 0);
                res.Add("GAS_MAX_" + k, 0);
            }
            res["GAS_" + k] = res["GAS_" + k] + (int)(tank.Capacity * tank.FilledRatio);
            res["GAS_MAX_" + k] = res["GAS_MAX_" + k] + (int)tank.Capacity;
        }

        if (container.InventoryCount > 0) {
            for (int j = 0; j < container.InventoryCount; ++j) {
                List<MyInventoryItem> items = new List<MyInventoryItem>();
                container.GetInventory(j).GetItems(items, null);
                foreach (var item in items) {
                    string k = StripClassName(item.Type.TypeId.ToString()) + "_" + item.Type.SubtypeId.ToString();
                    if (!res.ContainsKey(k)) 
                        res.Add(k, 0);
                    res[k] = res[k] + item.Amount.ToIntSafe();
                }
            }
        }

        if (container is IMyPowerProducer) {
            IMyPowerProducer pp = (IMyPowerProducer)container;
            res["power"] = res["power"] + (int) (pp.CurrentOutput * 1000000.0);
        }
    }  

    return res;
}

//
// Remove MyObjectBuilder_ from the start of s, if present
//
private static string StripClassName(string s) {
    if (s.StartsWith("MyObjectBuilder_"))
        return s.Substring(16);
    return s;
}



public class OutputPanel {
    private IMyTextPanel panel;
    public string Config;
    public ScreenThing Screen;

    public OutputPanel(IMyTextPanel panel) {
        this.panel = panel;
    }

    public IMyTextPanel Panel { get { return panel; }}
}


private static string PadLeft(string s, int width) {
    if (s.Length>= width)
        return s;
    return new String(' ', width - s.Length) + s;
}

public class ScreenThing {
    private string error = null;
    private List<ScreenElement> elements = new List<ScreenElement>();

    public readonly IMyGridTerminalSystem gts;

    public string Error { get { return error; }}

    private ScreenThing(IMyGridTerminalSystem gts) {
        this.gts = gts;
    }

    private void Add(string s, bool literal) {
        if (literal) {
            elements.Add(new ScreenElementLiteral(this, s));
        } else {
            string t;
            int p = s.IndexOf(':');
            if (p != -1) {
                t = s.Substring(0, p);
                s = s.Substring(p + 1);
            } else {
                t = s;
                s = "";
            }
            ScreenElement el;
            switch (t) {
                case "mass": el = new ScreenElementMass(this); break;
                case "item": el = new ScreenElementItemCount(this); break;
                case "gas":  el = new ScreenElementGas(this); break;
                case "gps":  el = new ScreenElementGPS(this); break;
                case "power":el = new ScreenElementPower(this); break;
                default: throw new InvalidOperationException("Unknown element type: " + t);
            }
            el.Parse(s);
            elements.Add(el);
        }
    }

    public string Evaluate(Dictionary<string, int> cargo) {
        try {
            StringBuilder sb = new StringBuilder();
            foreach (ScreenElement e in elements) 
                sb.Append(e.Get(cargo));		
            return sb.ToString();        
        } catch (Exception ex) {
            return ex.ToString();
        }

    }

    public static ScreenThing Parse(IMyGridTerminalSystem gts, String input) {
        ScreenThing res = new ScreenThing(gts);
        try {
            int last = 0;
            bool b = false;
            int i = 0;
            while (i < input.Length) {
                char ch = input[i];
                if (!b) {
                    if (ch == '<') {
                        b = true;
                        if (i > 0)	
                            res.Add(input.Substring(last, i - last), true);
                        last = i + 1;
                    }					  
                } else {
                    if (ch == '>') {
                        res.Add(input.Substring(last, i - last), false);
                        last = i + 1;
                        b = false;
                    }
                }
                ++i;			
            }
            if (b)
                throw new InvalidOperationException("Unterminated instruction");
            if (last != input.Length)
                res.Add(input.Substring(last), true);
        } catch (Exception ex) {
            res.error = ex.ToString();
        }
        return res;
    }
}

public abstract class ScreenElement {
    protected ScreenThing owner;
    public ScreenElement(ScreenThing owner) {
        this.owner = owner;
    }
    public abstract string Get(Dictionary<string, int> storage);
    public virtual void Parse(string data) { }
}

public class ScreenElementLiteral : ScreenElement {
    private string val;

    public ScreenElementLiteral(ScreenThing owner, string val) : base(owner) {
        this.val = val;
    }
    public override string Get(Dictionary<string, int> storage) {
        return val;
    }
}

public class ScreenElementMass : ScreenElement {
    private int vPadWidth = -1;
    private bool vDry = false;

    public ScreenElementMass(ScreenThing owner) : base(owner) {
    }

    public override void Parse(string data) {
        string[] bits = data.Split(',');
        vDry = "dry".Equals(bits[0].ToLower());
        if (bits.Length > 1)
            vPadWidth = int.Parse(bits[1]);
    }		

    public override string Get(Dictionary<string, int> storage) {
        IMyShipController c = owner.gts.GetBlockWithName(COCKPIT_NAME) as IMyShipController;
        int m = (int)(c.CalculateShipMass().TotalMass / 1000.0);
        string s = m.ToString();    
        if (vPadWidth <= 0)
            return s;
        return PadLeft(s, vPadWidth);
    }
}

public class ScreenElementItemCount : ScreenElement {
    private string vResource;
    private int vPadWidth = -1;
    private int vDivOpt = 1; // 0=1, 1=1000, 2=1e6

    public ScreenElementItemCount(ScreenThing owner) : base(owner) {
    }

    public override void Parse(string data) {
        string[] bits = data.Split(',');
        vResource = bits[0];
        if (bits.Length > 1)
            vPadWidth = int.Parse(bits[1]);
        if (bits.Length > 2) {
            string s = bits[2];
            switch (s.ToLower()[0]) {
                case 'n': vDivOpt = 0; break;
                case 'k': vDivOpt = 1; break;
                case 'm': vDivOpt = 2; break;
                default: throw new InvalidOperationException("2nd option for item: must be n,k,m");
            }
        }
    }

    public override string Get(Dictionary<string, int> storage) {
        int m;
        string s;
        if (!storage.TryGetValue(vResource, out m)) 
            s = "-";
        else {
            switch (vDivOpt) {
                case 0: s = m.ToString(); break;
                case 1: s = m > 1000 ? (int)(m / 1000) + "K" : m.ToString(); break;
                default: s = m > 1000000 ? (int)(m / 1000000) + "M" : m.ToString(); break;
            }
            
        }
        if (vPadWidth <= 0)
            return s;
        return PadLeft(s, vPadWidth);
    }
}


// <gas:o2,pad,%>
public class ScreenElementGas : ScreenElement {
    private string vResource; // o2 or h2
    private bool absolute = true; // absolute or %age?
    private int vPadWidth = -1;

    public ScreenElementGas(ScreenThing owner) : base(owner) {
    }

    public override void Parse(string data) {
        string[] bits = data.Split(',');
        vResource = bits[0].ToUpper();
        if ("O2".Equals(vResource) || "H2".Equals(vResource)) {
        } else
            throw new InvalidOperationException("Resource type must be O2 or H2");
        if (bits.Length > 1)
            vPadWidth = int.Parse(bits[1]);
        if (bits.Length > 2) 
            absolute = !"%".Equals(bits[2]);
    }

    public override string Get(Dictionary<string, int> storage) {
        int m;
        string s;
        if (absolute) {
            if (!storage.TryGetValue("GAS_" + vResource, out m)) 
                s = "-";
            else 
                s = m > 1000 ? (int)(m / 1000) + "K" : m.ToString();
        } else {
            if (!storage.TryGetValue("GAS_" + vResource, out m)) 
                s = "0";
            else  {
                int max = storage["GAS_MAX_" + vResource];
                if (max == 0)
                    max = 1;
                s = ((int)((double)m * 100.0 / max)).ToString();
            }
        }

        if (vPadWidth <= 0)
            return s;
        return PadLeft(s, vPadWidth);
    }
}


// <gps:x,pad,decimals>
public class ScreenElementGPS : ScreenElement {
    private string coordinate;
    private int vPadWidth = -1;
    private int decimals = 2;

    public ScreenElementGPS(ScreenThing owner) : base(owner) {
    }

    public override void Parse(string data) {
        string[] bits = data.Split(',');
        coordinate = bits[0].ToUpper();
        if ("X".Equals(coordinate) || "Y".Equals(coordinate) || "Z".Equals(coordinate)) {
        } else
            throw new InvalidOperationException("Axis must be X,Y, or Z");
        if (bits.Length > 1)
            vPadWidth = int.Parse(bits[1]);
        if (bits.Length > 2) 
            decimals = int.Parse(bits[2]);
    }

    public override string Get(Dictionary<string, int> storage) {
        IMyShipController c = owner.gts.GetBlockWithName(COCKPIT_NAME) as IMyShipController;
        Vector3D pos = c.GetPosition();
        double f = 0.0;
        switch (coordinate) {
            case "X": f = pos.X; break;
            case "Y": f = pos.Y; break;
            case "Z": f = pos.Z; break;
        }
        string s = f.ToString("C" + decimals);
        if (vPadWidth <= 0)
            return s;
        return PadLeft(s, vPadWidth);
    }
}

// <power:kw,pad>
public class ScreenElementPower : ScreenElement {
    private int vPadWidth = -1;
    private int scale = 1; 

    public ScreenElementPower(ScreenThing owner) : base(owner) {
    }

    public override void Parse(string data) {
        string[] bits = data.Split(',');        
        scale = int.Parse(bits[0]);
        if (scale < 1) 
            scale = 1;
        if (bits.Length > 1)
            vPadWidth = int.Parse(bits[1]);
    }

    public override string Get(Dictionary<string, int> storage) {
        int n = (int)(storage["power"] / scale);
        string s = n.ToString();
        if (vPadWidth <= 0)
            return s;
        return PadLeft(s, vPadWidth);
    }
}

