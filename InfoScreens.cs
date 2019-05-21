public Program() {
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

//
// How to Use:  
//   Create LCD panels with a Custom Data field starting with "stik:lcd-info"
//   Then the rest of the Custom Data is a configuration string:
//
//     stik:lcd-info
//     Ship Mass: <mass,7>
//     
//     Ores in Cargo:
//     ---------------
//         Iron Ore: <IronOre, 7>
//       Nickel Ore: <NickelOre, 7>
//         Gold Ore: <GoldOre, 7>
//  
//     Products:
//     ----------
//       Plates:  <SteelPlate, 5>
//       etc...
//
//   Give it a few seconds to update after you make a change, it only checks and parses
//   this string every few seconds, to avoid unnecessary CPU load
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



//
// Returns a Dictionary with all the cargo items in the grid
//
public Dictionary<string, int> GetAllCargo(IMyGridTerminalSystem gts) {
    Dictionary<string, int> res = new Dictionary<string, int>();

    //
    // Get a list of all containers in the grid
    //
    List<IMyCargoContainer> containers = new List<IMyCargoContainer>();
    gts.GetBlocksOfType<IMyCargoContainer>(containers);

    foreach (IMyCargoContainer container in containers) {
        var items = container.GetInventory(0).GetItems();
        foreach (var item in items) {
            string k = item.Content.TypeId.ToString();
            if (!res.ContainsKey(k)) 
                res.Add(k, 0);
            res[k] = res[k] + item.Amount;
        }
    }  

    return res;
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
                default: throw new InvalidOperationException("Unknown element type: " + t);
            }
            el.Parse(s);
            elements.Add(el);
        }
    }

    public string Evaluate(Dictionary<string, int> cargo) {
        StringBuilder sb = new StringBuilder();
        foreach (ScreenElement e in elements) 
            sb.Append(e.Get(cargo));		
        return sb.ToString();
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
            Console.WriteLine("Error: " + ex.ToString());
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
        if (vPadWidth > 0)
            return PadLeft("9872", vPadWidth);
        IMyShipController c = owner.gts.GetBlockWithName("Cockpit 2") as IMyShipController;
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

    public ScreenElementItemCount(ScreenThing owner) : base(owner) {
    }

    public override void Parse(string data) {
        string[] bits = data.Split(',');
        vResource = bits[0];
        if (bits.Length > 1)
            vPadWidth = int.Parse(bits[1]);
    }

    public override string Get(Dictionary<string, int> storage) {        
        int m;
        string s;
        if (!storage.TryGetValue(vResource, out m)) 
            s = "???";
        else 
            s = m > 1000 ? (int)(m / 1000) + "K" : m.ToString();							
        if (vPadWidth <= 0)
            return s;
        return PadLeft(s, vPadWidth);
    }
}

