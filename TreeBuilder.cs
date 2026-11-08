// TreeBuilder.cs - builds the flat stepped pine Christmas tree in SOLIDWORKS as individual
// parts (.SLDPRT) plus one assembly (.SLDASM).
//
// Design: ONE flat plane of pine (W wide in the plane, T thick front to back; default 42x42). Each side has three parallel uprights. They overlap
// 130 mm with their long faces touching (no wood cuts into other wood). Each lower upright
// under-laps the one above: the top upright (P1) is on the outer line, the middle (P2) one
// board-width inside it, the bottom (P3) one more inside. All lap ends are cut horizontal.
// Lap bolts run IN the plane, perpendicular to the boards (outer to inner), never front-to-back.
// The two top uprights meet at the apex and are bolted across it; a printed star cap clamps the tip.
//
// Build: .NET Framework 4.8 console app, x64, references SolidWorks.Interop.sldworks.dll and
// SolidWorks.Interop.swconst.dll (see TreeBuilder.csproj). Run with SOLIDWORKS installed.
// Usage: start SOLIDWORKS first (from 3DEXPERIENCE), then
//        TreeBuilder.exe [outputFolder] [--height mm] [--base mm] [--<any Cfg field> value] [--preview]
//        (default folder: Documents\PineTree; see class Cfg for every parameter)
//
// Units: mm here, metres in the API (x MM). Each part is modelled in its final assembly position:
// profile sketched on the Front plane and extruded symmetrically about it (mid-plane), so every
// bolt axis lies in the Front plane. Bolt holes are revolve-cuts about a centre line in that
// plane, which gives round holes at any angle. Components are inserted and fixed - no mates.
// Not compiled or tested here: if a call fails, the README has hand-build tables.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace PineTree
{
    // All sizes in mm. Every field can be overridden on the command line: --Name value (or --Name=value),
    // case-insensitive, plus the aliases --height (H) and --base (BaseWidth). Example:
    //   TreeBuilder.exe D:\out --height 1500 --base 750
    class Cfg
    {
        public double H = 1210;         // apex height: tip of the wood above the underside of the rail (top of the feet)
        public double BaseWidth = 922;  // length of the base rail = overall width of the base
        public double RailOverhang = 20;    // rail sticks out this far past the outside of each leg
        public double BaseScrewDepth = 50;  // how far the base coach screw goes into the leg end grain
        public double W = 45;           // board width seen from the front (in the plane of the tree); also the rail height
        public double T = 90;           // board thickness front to back (e.g. 90x45 on edge: --W 45 --T 90)
        public double Overlap = 130;    // vertical overlap of each lap
        public double L3 = 0;           // vertical extent of bottom upright; 0 = auto (470 at H 1210, scales with H)
        public double L1 = 0;           // vertical extent of top upright; 0 = auto (same rule). Middle one takes the rest.
        public double FootL = 450;      // foot length (front to back)
        public double BoltD = 6.5;      // clearance hole for M6 rod / bolt
        public double CapNeckH = 0;     // printed cap neck thickness (front to back); 0 = same as T
        public double CapPlateT = 12;   // printed star plate thickness
        public double StarR = 85;       // star outer radius
        public double StarGap = 3;      // gap between the wood tip and the star's bottom notch
        public double CapWall = 8;      // wall thickness of the printed collar around the top tips
        public double StockLen = 1800;  // length of the pine you buy (only used for a warning)

        // Derived: slope of the uprights from vertical, solved so the base comes out at BaseWidth.
        public double PhiDeg;
        // --preview: print the parameters and cut list, check them, and stop before SOLIDWORKS.
        public bool Preview;

        static readonly Dictionary<string, string> Alias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { { "height", "H" }, { "base", "BaseWidth" }, { "width", "BaseWidth" } };

        // Returns the output folder (first argument that is not an option) or null.
        public string Parse(string[] args)
        {
            string dir = null;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (!a.StartsWith("--")) { dir = a; continue; }
                if (a.Equals("--preview", StringComparison.OrdinalIgnoreCase)) { Preview = true; continue; }
                string name = a.Substring(2), val;
                int eq = name.IndexOf('=');
                if (eq >= 0) { val = name.Substring(eq + 1); name = name.Substring(0, eq); }
                else if (i + 1 < args.Length) val = args[++i];
                else throw new ArgumentException("No value given for " + a);
                string mapped;
                if (Alias.TryGetValue(name, out mapped)) name = mapped;
                var f = typeof(Cfg).GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                                                   | System.Reflection.BindingFlags.IgnoreCase);
                if (f == null || f.Name == "PhiDeg" || f.FieldType != typeof(double))
                    throw new ArgumentException("Unknown option " + a + ". Options: --height, --base, " +
                        string.Join(", ", typeof(Cfg).GetFields().Where(x => x.Name != "PhiDeg" && x.FieldType == typeof(double)).Select(x => "--" + x.Name)));
                f.SetValue(this, double.Parse(val, System.Globalization.CultureInfo.InvariantCulture));
            }
            return dir;
        }

        // The outside of the bottom upright where it sits on the rail (y = W) is RailOverhang in from the
        // rail end: (H - W) tan(phi) - 2 W / cos(phi) = BaseWidth / 2 - RailOverhang.
        public void Solve()
        {
            if (CapNeckH <= 0) CapNeckH = T;
            if (L3 <= 0) L3 = Math.Round(H * 470 / 1210);
            if (L1 <= 0) L1 = Math.Round(H * 470 / 1210);
            double target = BaseWidth / 2 - RailOverhang;
            Func<double, double> half = p => (H - W) * Math.Tan(p) - 2 * W / Math.Cos(p);
            double lo = 0.001, hi = 50 * Math.PI / 180;
            if (half(hi) < target) throw new ArgumentException("Base is too wide for this height (slope over 50 degrees).");
            if (half(lo) > target) throw new ArgumentException("Base is too narrow for this height.");
            for (int i = 0; i < 100; i++) { double m = (lo + hi) / 2; if (half(m) < target) lo = m; else hi = m; }
            PhiDeg = (lo + hi) / 2 * 180 / Math.PI;
        }

        public void Print()
        {
            Console.WriteLine("Parameters (mm): " + string.Join("  ", typeof(Cfg).GetFields()
                .Where(f => f.Name != "PhiDeg" && f.FieldType == typeof(double)).Select(f => f.Name + "=" + ((double)f.GetValue(this)).ToString("0.###"))));
            Console.WriteLine("Upright slope: {0:F2} deg from vertical (saw: lap ends {0:F1} deg, P1 tip {1:F1} deg)",
                PhiDeg, 90 - PhiDeg);
        }
    }

    class Bolt
    {
        public double X, Y, Dx, Dy, Len;
        public Bolt(double x, double y, double dx, double dy, double len) { X = x; Y = y; Dx = dx; Dy = dy; Len = len; }
    }

    // An extra outline extruded into a part and merged with it, spanning z = Z0 .. Z0 + Depth (mm).
    class Extra
    {
        public List<double[]> Pts = new List<double[]>();
        public double Depth, Z0;
    }

    class PartDef
    {
        public string Name = "", Stock = "";
        public List<double[]> Pts = new List<double[]>();
        public List<Bolt> Bolts = new List<Bolt>();
        public List<List<double[]>> Cuts = new List<List<double[]>>();   // outlines cut through the part
        public List<Extra> Extras = new List<Extra>();                   // more outlines extruded and merged in
        public bool Stl;                                                 // also export the part as .STL
        public double Depth;                 // total thickness (Z), centred on the Front plane
        public double[] Axis = { 1, 0 };     // board axis, used for cut-list length
        public double[] Xs = { 0 };          // X offsets of instances in the assembly
        public bool Printed { get { return Stock.StartsWith("PETG"); } }
        // Manufacturing info, written as custom properties and shown in the drawing's BOM:
        // Description, Length, Ends, Holes.
        public Dictionary<string, string> Props = new Dictionary<string, string>();
    }

    static class Program
    {
        const double MM = 0.001;
        static double BoltD = 6.5;

        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                var cfg = new Cfg();
                string dir = cfg.Parse(args)
                    ?? Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "PineTree");
                cfg.Solve();
                cfg.Print();
                if (!cfg.Preview) Directory.CreateDirectory(dir);

                List<PartDef> parts = BuildParts(cfg);
                PrintCutList(parts);
                if (!CheckParts(cfg, parts)) return 3;
                if (cfg.Preview) { Console.WriteLine("Preview only: nothing built."); return 0; }
                BoltD = cfg.BoltD;

                SldWorks app = ConnectToSolidWorks();
                if (app == null) { Console.WriteLine("Could not connect to SOLIDWORKS."); return 1; }

                // While we draw: no "enter dimension" pop-ups, no inference, no automatic relations and
                // no auto-rotate to face each new sketch. Restored afterwards.
                var toggles = new[] {
                    swUserPreferenceToggle_e.swInputDimValOnCreate, swUserPreferenceToggle_e.swSketchInference,
                    swUserPreferenceToggle_e.swSketchAutomaticRelations, swUserPreferenceToggle_e.swAutoNormalToSketchMode };
                var was = toggles.ToDictionary(t => t, t => app.GetUserPreferenceToggle((int)t));
                foreach (var t in toggles) app.SetUserPreferenceToggle((int)t, false);
                app.CommandInProgress = true;   // tells SOLIDWORKS a program is driving it: fewer UI updates
                CloseDocsIn(app, dir);           // a previous run's files may still be open; we overwrite them
                var openBefore = new HashSet<string>(((object[])app.GetDocuments() ?? new object[0])
                    .Cast<ModelDoc2>().Select(d => d.GetTitle()));
                bool built = false;
                try
                {
                    var paths = new Dictionary<string, string>();
                    var zmin = new Dictionary<string, double>();
                    foreach (PartDef d in parts)
                    {
                        double z;
                        paths[d.Name] = MakePart(app, d, dir, out z);
                        zmin[d.Name] = z;
                    }
                    MakeAssembly(app, parts, paths, zmin, dir);
                    WriteCutListCsv(cfg, parts, Path.Combine(dir, "CutList.csv"));
                    MakeDrawing(app, cfg, Path.Combine(dir, "PineTree.SLDASM"), dir);
                    built = true;
                }
                finally
                {
                    try
                    {
                        // On failure, close what this run opened, so half-built documents don't hold files locked.
                        if (!built)
                            foreach (var d in ((object[])app.GetDocuments() ?? new object[0]).Cast<ModelDoc2>()
                                     .Where(d => !openBefore.Contains(d.GetTitle())).Select(d => d.GetTitle()).ToList())
                            {
                                Console.WriteLine("Closing " + d + " (left over from this failed run)");
                                app.CloseDoc(d);
                            }
                        app.CommandInProgress = false;
                        foreach (var t in toggles) app.SetUserPreferenceToggle((int)t, was[t]);
                    }
                    catch (Exception) { /* SOLIDWORKS already gone */ }
                }
                Console.WriteLine("Done. Evaluate > Interference Detection: expect none (laps only touch).");
                return 0;
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine("Bad parameters: " + ex.Message);
                return 3;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAILED: " + ex);
                return 2;
            }
        }

        // Attach to a SOLIDWORKS that is already running. With 3DEXPERIENCE SOLIDWORKS a copy started
        // over COM has no platform login/licence and quits after a few seconds ("RPC server is
        // unavailable"), so we never start one ourselves: start it from the 3DEXPERIENCE launcher /
        // compass, and this waits (up to 10 min) until it appears and has finished loading.
        static SldWorks ConnectToSolidWorks()
        {
            if (Type.GetTypeFromProgID("SldWorks.Application") == null)
            { Console.WriteLine("SOLIDWORKS is not registered on this PC."); return null; }

            SldWorks app = null;
            bool told = false;
            for (int i = 0; i < 600 && app == null; i++)
            {
                try { app = (SldWorks)System.Runtime.InteropServices.Marshal.GetActiveObject("SldWorks.Application"); }
                catch (Exception)
                {
                    if (!told)
                    {
                        Console.WriteLine("No running SOLIDWORKS found. Start it from 3DEXPERIENCE (log in, open SOLIDWORKS)");
                        Console.WriteLine("and leave it open. Run this from a terminal that is NOT 'Run as administrator'");
                        Console.WriteLine("unless SOLIDWORKS is too. Waiting...");
                        told = true;
                    }
                    System.Threading.Thread.Sleep(1000);
                }
            }
            if (app == null) return null;

            Console.Write("Attached to SOLIDWORKS, waiting for it to finish loading");
            for (int i = 0; i < 300; i++)
            {
                try { if (app.StartupProcessCompleted) break; } catch (Exception) { }
                System.Threading.Thread.Sleep(1000);
                Console.Write(".");
            }
            Console.WriteLine();
            app.Visible = true;
            return app;
        }

        static double[] P(double x, double y) { return new[] { x, y }; }
        static List<double[]> Mirror(List<double[]> pts) { return pts.Select(p => new[] { -p[0], p[1] }).ToList(); }
        static List<Bolt> Mirror(List<Bolt> b) { return b.Select(k => new Bolt(-k.X, k.Y, -k.Dx, k.Dy, k.Len)).ToList(); }

        static List<PartDef> BuildParts(Cfg c)
        {
            double phi = c.PhiDeg * Math.PI / 180.0;
            double tan = Math.Tan(phi), cos = Math.Cos(phi), sin = Math.Sin(phi);
            double a = c.W / cos;                               // horizontal width of an upright
            // outer edge x of the upright k board-widths in from the outer line, at height y
            Func<double, int, double> xo = (y, k) => (c.H - y) * tan - k * a;

            double ha = c.H;                                    // apex height (top uprights meet at x = 0)
            double ym = ha - c.W / sin;                         // where their inner edges meet the centre line
            double y2lo = c.L3 - c.Overlap;                     // middle upright spans y2lo .. y2hi
            double y1lo = ha - c.L1;
            double y2hi = y1lo + c.Overlap;

            string stock = c.W.ToString("0") + "x" + c.T.ToString("0") + " pine";
            const string printed = "PETG print";
            double[] axisR = { sin, -cos }, axisL = { -sin, -cos };
            var list = new List<PartDef>();

            Func<int, double, double, List<double[]>> board = (k, ya, yb) => new List<double[]> {
                P(xo(yb, k), yb), P(xo(ya, k), ya), P(xo(ya, k) - a, ya), P(xo(yb, k) - a, yb) };

            // lap bolts: centred on the shared face, perpendicular to the uprights (outward = (cos, sin))
            var lapLow = new List<Bolt>(); var lapHigh = new List<Bolt>();
            double lapLen = 3 * c.W;                            // hole runs right through both boards
            foreach (double y in new[] { y2lo + 30, c.L3 - 30 }) lapLow.Add(new Bolt(xo(y, 2), y, cos, sin, lapLen));
            foreach (double y in new[] { y1lo + 30, y2hi - 30 }) lapHigh.Add(new Bolt(xo(y, 1), y, cos, sin, lapLen));

            // apex: 2 cross bolts through both top uprights, 2 more through the printed cap neck
            var apex = new List<Bolt>();
            foreach (double dy in new[] { 40.0, 62.0, 90.0, 140.0 })
                apex.Add(new Bolt(0, ha - dy, 1, 0, 2 * (dy * tan + 20)));

            // P3: bottom upright, two boards in (under-laps P2), standing on the rail. Horizontal end cuts.
            var p3 = board(2, c.W, c.L3);                       // stands on top of the rail
            list.Add(new PartDef { Name = "P3R", Stock = stock, Pts = p3, Bolts = lapLow, Depth = c.T, Axis = axisR });
            list.Add(new PartDef { Name = "P3L", Stock = stock, Pts = Mirror(p3), Bolts = Mirror(lapLow), Depth = c.T, Axis = axisL });

            // P2: middle upright, one board in; over P3 (low), under P1 (high)
            var p2 = board(1, y2lo, y2hi);
            var b2 = lapLow.Concat(lapHigh).ToList();
            list.Add(new PartDef { Name = "P2R", Stock = stock, Pts = p2, Bolts = b2, Depth = c.T, Axis = axisR });
            list.Add(new PartDef { Name = "P2L", Stock = stock, Pts = Mirror(p2), Bolts = Mirror(b2), Depth = c.T, Axis = axisL });

            // P1: top upright, on the outer line, over P2; tip is cut vertically at x = 0 (72 deg from square)
            var p1 = new List<double[]> { P(0, ha), P(xo(y1lo, 0), y1lo), P(xo(y1lo, 0) - a, y1lo), P(0, ym) };
            var b1 = lapHigh.Concat(apex).ToList();
            list.Add(new PartDef { Name = "P1R", Stock = stock, Pts = p1, Bolts = b1, Depth = c.T, Axis = axisR });
            list.Add(new PartDef { Name = "P1L", Stock = stock, Pts = Mirror(p1), Bolts = Mirror(b1), Depth = c.T, Axis = axisL });

            // Base: the rail runs the full width (y = 0 .. W, square ends) and the bottom uprights stand on it.
            // The feet run front to back under the rail, one under each leg. Each side is held by one M6
            // coach screw going up through the foot and the rail into the end grain of the leg. The screw
            // is vertical while the leg leans, so it enters a little inside the leg centre and stays in the
            // wood for BaseScrewDepth.
            double sxr = xo(c.W, 2) - a / 2 - c.BaseScrewDepth / 2 * tan;
            double footW = Math.Max(c.W, c.T), footH = Math.Min(c.W, c.T);   // foot lies wide face down
            double railLen = 3 * (c.W + footH);
            list.Add(new PartDef
            {
                Name = "RAIL", Stock = stock, Depth = c.T,
                Pts = Rect(-c.BaseWidth / 2, 0, c.BaseWidth / 2, c.W),
                Bolts = { new Bolt(-sxr, c.W / 2, 0, 1, railLen), new Bolt(sxr, c.W / 2, 0, 1, railLen) }
            });

            // Foot: one part, two instances centred under the screws, wide face down. Hole in the middle of its length.
            list.Add(new PartDef
            {
                Name = "FOOT", Stock = stock, Depth = c.FootL, Xs = new[] { -sxr, sxr },
                Pts = Rect(-footW / 2, -footH, footW / 2, 0),
                Bolts = { new Bolt(0, -footH / 2, 0, 1, railLen) }
            });

            // ---- printed star cap: a full star held up by a collar that continues the tree's outline ----
            // Star: complete 5-point star. Its bottom notch (inner vertex) sits StarGap above the wood tip, so the
            // star never touches the wood and looks like it is carried by the tree.
            double ri = 0.4 * c.StarR, yc = ha + c.StarGap + ri;
            var star = Enumerable.Range(0, 10).Select(k =>
            {
                double r = k % 2 == 0 ? c.StarR : ri, ang = (90 + 36 * k) * Math.PI / 180;
                return P(r * Math.Cos(ang), yc + r * Math.Sin(ang));
            }).ToList();
            // Collar: tapered sleeve over the two top tips, 72 deep. Its sides run parallel to the outside of the
            // top uprights, CapWall out, and meet in a point inside the star, so the tree's outline carries on
            // up into the star. Inside: the V slot the tips sit in (0.4 mm clearance).
            double clr = 0.4, ytip = ha + clr / sin, yb = ha - 72;
            double xb = (ha - yb) * tan + c.CapWall / cos, ypk = ha + c.CapWall / sin;
            double sx = (ytip - (yb - 1)) * tan;
            Func<double, double> collarHalf = y => (ha - y) * tan + c.CapWall / cos;
            var capBolts = new List<Bolt> {
                new Bolt(0, ha - 40, 1, 0, 2 * collarHalf(ha - 40) + 20), new Bolt(0, ha - 62, 1, 0, 2 * collarHalf(ha - 62) + 20) };

            // One printed part: the collar (CapNeckH deep) with the star merged on, both centred on the middle of
            // the wood's thickness. The collar's point overlaps the star, so the two fuse into one solid.
            // Printed back face down, the star's arms need supports underneath.
            list.Add(new PartDef
            {
                Name = "STAR_CAP", Stock = printed, Depth = c.CapNeckH, Bolts = capBolts,
                Pts = new List<double[]> { P(-xb, yb), P(xb, yb), P(0, ypk) },
                Extras = { new Extra { Pts = star, Depth = c.CapPlateT, Z0 = -c.CapPlateT / 2 } },
                Cuts = { new List<double[]> { P(-sx, yb - 1), P(0, ytip), P(sx, yb - 1) } },
                Stl = true
            });
            Describe(c, list);
            return list;
        }

        // Overall length of a timber part (long point to long point).
        static double StockLength(PartDef d)
        {
            if (d.Name == "FOOT") return d.Depth;
            if (d.Name == "RAIL") return d.Pts.Max(p => p[0]) - d.Pts.Min(p => p[0]);
            var dots = d.Pts.Select(p => p[0] * d.Axis[0] + p[1] * d.Axis[1]).ToList();
            return dots.Max() - dots.Min();
        }

        // Fill in the manufacturing properties for every part.
        // Hole positions on an upright are measured along its OUTER face (the side away from the
        // centre of the tree) from the bottom long point, which is the bottom corner of that face.
        static void Describe(Cfg c, List<PartDef> parts)
        {
            double phi = c.PhiDeg;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Func<double, string> f1 = v => v.ToString("0.0", inv);
            var names = new Dictionary<string, string> {
                { "P3", "Bottom upright" }, { "P2", "Middle upright" }, { "P1", "Top upright" } };

            foreach (PartDef d in parts)
            {
                if (d.Printed)
                {
                    d.Props["Description"] = "Star cap, printed in one piece (STAR_CAP.STL)";
                    d.Props["Length"] = "-";
                    d.Props["Ends"] = "print back face down, supports under the star";
                    d.Props["Holes"] = "printed in: level, 40 and 62 below the tip";
                    continue;
                }
                d.Props["Length"] = f1(StockLength(d));
                d.Props["Holes"] = "-";
                if (d.Name == "RAIL")
                {
                    double fromEnd = c.BaseWidth / 2 - d.Bolts.Max(b => b.X);
                    d.Props["Description"] = "Base rail, " + d.Stock;
                    d.Props["Ends"] = "square";
                    d.Props["Holes"] = f1(fromEnd) + " from each end, vertical, centred (base screws)";
                    continue;
                }
                if (d.Name == "FOOT")
                {
                    d.Props["Description"] = "Foot, " + d.Stock + ", wide face down";
                    d.Props["Ends"] = "square";
                    d.Props["Holes"] = f1(c.FootL / 2) + " (middle), vertical, centred (base screw)";
                    continue;
                }

                string side = d.Name.EndsWith("R") ? "right" : "left";
                d.Props["Description"] = names[d.Name.Substring(0, 2)] + " " + side + ", " + d.Stock;
                d.Props["Ends"] = d.Name.StartsWith("P1")
                    ? "bottom " + f1(phi) + "° mitre; tip " + f1(90 - phi) + "° (cut vertical)"
                    : "both " + f1(phi) + "° mitre, parallel";

                // Outer face: from the outer bottom corner Pts[1] to the outer top Pts[0].
                double[] o = d.Pts[1], t = d.Pts[0];
                double ex = t[0] - o[0], ey = t[1] - o[1], el = Math.Sqrt(ex * ex + ey * ey);
                ex /= el; ey /= el;
                var holes = new List<Tuple<double, double>>();   // (distance, angle off square)
                foreach (Bolt b in d.Bolts)
                {
                    // Where the bolt line crosses the outer face line: o + s e = (X,Y) + k (Dx,Dy)
                    double det = ex * (-b.Dy) - ey * (-b.Dx);
                    double s = ((b.X - o[0]) * (-b.Dy) - (b.Y - o[1]) * (-b.Dx)) / det;
                    double bl = Math.Sqrt(b.Dx * b.Dx + b.Dy * b.Dy);
                    double cosA = Math.Abs(ex * b.Dx + ey * b.Dy) / bl;   // cos of angle between bolt and board
                    double off = Math.Abs(90 - Math.Acos(Math.Min(1, cosA)) * 180 / Math.PI);
                    holes.Add(Tuple.Create(s, off));
                }
                var square = holes.Where(h => h.Item2 < 0.5).Select(h => f1(h.Item1)).ToList();
                var angled = holes.Where(h => h.Item2 >= 0.5).OrderBy(h => h.Item1).ToList();
                var parts2 = new List<string>();
                if (square.Count > 0) parts2.Add(string.Join(", ", square) + " square");
                if (angled.Count > 0)
                    parts2.Add(string.Join(", ", angled.Select(h => f1(h.Item1))) + " drilled level (" + f1(angled[0].Item2) + "° off square)");
                if (d.Name.StartsWith("P3")) parts2.Add("base pilot via rail");
                if (parts2.Count > 0) d.Props["Holes"] = string.Join("; ", parts2);
            }
        }

        // Cut list as CSV, next to the SOLIDWORKS files.
        static void WriteCutListCsv(Cfg c, List<PartDef> parts, string path)
        {
            Func<string, string> q = s => "\"" + s.Replace("\"", "\"\"") + "\"";
            var lines = new List<string> { "Part,Qty,Description,Length mm,Ends,Holes (mm along outer face from bottom long point)" };
            foreach (PartDef d in parts)
                lines.Add(string.Join(",", q(d.Name), d.Xs.Length, q(d.Props["Description"]), q(d.Props["Length"]),
                                      q(d.Props["Ends"]), q(d.Props["Holes"])));
            lines.Add("");
            lines.Add(q(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Height {0} mm, base {1} mm, slope {2:F2} deg. Holes {3} mm through the side face, centred in the {4} mm thickness.",
                c.H, c.BaseWidth, c.PhiDeg, c.BoltD, c.T)));
            lines.Add(q(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Base: legs stand on the rail, rail on the feet. One M6 coach screw per side up through foot + rail into the leg end grain ({0:0} mm in, pilot 4.5 drilled through the rail hole).",
                c.BaseScrewDepth)));
            File.WriteAllLines(path, lines, System.Text.Encoding.UTF8);
            Console.WriteLine("Saved " + path);
        }

        static List<double[]> Rect(double x0, double y0, double x1, double y1)
        { return new List<double[]> { P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1) }; }

        // Sanity checks on the derived geometry. Returns false (and says why) if the tree won't go together.
        static bool CheckParts(Cfg c, List<PartDef> parts)
        {
            double phi = c.PhiDeg * Math.PI / 180, ok = 1;
            Action<bool, string> need = (cond, msg) => { if (!cond) { Console.WriteLine("PROBLEM: " + msg); ok = 0; } };
            double y2lo = c.L3 - c.Overlap, y1lo = c.H - c.L1, y2hi = y1lo + c.Overlap;
            need(c.Overlap >= 80, "Overlap must be at least 80 mm (two lap bolts 30 mm from each end).");
            need(y1lo - c.L3 >= 20, string.Format(
                "Height {0} is too small for L1 {1} + L3 {2}: the two laps on the middle upright would meet. " +
                "Lower --L1/--L3 or raise --height.", c.H, c.L1, c.L3));
            need(c.L1 > c.W / Math.Sin(phi) + 150, string.Format(
                "L1 {0} is too short: the top upright needs at least {1:F0} mm for the apex bolts.", c.L1, c.W / Math.Sin(phi) + 150));
            need((c.H - c.W) * Math.Tan(phi) - 3 * c.W / Math.Cos(phi) > 20, "Base is too narrow: the legs would meet on the rail.");
            need(c.L3 - c.Overlap > c.W + 60, "L3 is too short: the bottom upright needs room below its lap.");
            need(c.RailOverhang >= 0, "RailOverhang can't be negative.");
            need(c.PhiDeg <= 30, "Slope over 30 degrees: the star cap and the tip mitre get impractical. Narrow the base.");
            foreach (var d in parts.Where(p => !p.Printed))
            {
                double len = StockLength(d);
                if (len > c.StockLen - 10)
                    Console.WriteLine("WARNING: {0} is {1:F0} mm, longer than the {2:F0} mm stock (--StockLen).", d.Name, len, c.StockLen);
            }
            if (ok == 0) Console.WriteLine("Nothing was built. Change the parameters and run again.");
            return ok == 1;
        }

        static void PrintCutList(List<PartDef> parts)
        {
            Console.WriteLine("Part   Qty  Length  Ends / Holes (mm along outer face from bottom long point)");
            foreach (PartDef d in parts)
            {
                if (d.Printed) continue;
                Console.WriteLine("{0,-6} x{1}  {2,6}  {3}", d.Name, d.Xs.Length, d.Props["Length"], d.Props["Ends"]);
                if (d.Props["Holes"] != "-") Console.WriteLine("                     holes: " + d.Props["Holes"]);
            }
            Console.WriteLine("Printed PETG: STAR_CAP (star + collar, one piece; exported as STAR_CAP.STL)");
        }

        // ------------------------------------------------------------ SOLIDWORKS
        static string MakePart(SldWorks app, PartDef d, string dir, out double zminMm)
        {
            string tpl = app.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
            app.NewDocument(tpl, 0, 0, 0);
            var m = (ModelDoc2)app.ActiveDoc;
            if (m == null)
                throw new Exception("Could not create a part. Set Tools > Options > Default Templates > Part.");

            Console.WriteLine("  " + d.Name + ": new part created");
            // Write sketch entities straight to the database: no snapping/inference and no live
            // redraw per line. Driving those from out of process is a common cause of crashes.
            m.SketchManager.AddToDB = true;
            m.SketchManager.DisplayWhenAdded = false;
            var view = m.ActiveView as ModelView;
            if (view != null) view.EnableGraphicsUpdate = false;   // no redraws while the part is built
            // Profile on the Front plane, extruded symmetrically (mid-plane) so z = -Depth/2 .. +Depth/2.
            string sk = OpenSketchOnFront(m);
            Console.WriteLine("  " + d.Name + ": " + sk + " open, drawing outline");
            DrawLoop(m, d.Pts);
            CloseAndSelectSketch(m, sk);
            Console.WriteLine("  " + d.Name + ": outline done, extruding");

            Feature ext = m.FeatureManager.FeatureExtrusion2(
                true, false, false, (int)swEndConditions_e.swEndCondMidPlane, 0,
                d.Depth * MM, 0, false, false, false, false, 0, 0,
                false, false, false, false, true, true, true, 0, 0, false);
            if (ext == null) throw new Exception("Extrude failed for " + d.Name);

            // Extra outlines (the star on the star cap): extruded from an offset start (Z0) for Depth and merged.
            foreach (var ex in d.Extras)
            {
                string es = OpenSketchOnFront(m);
                Console.WriteLine("  " + d.Name + ": " + es + " merged extrude");
                DrawLoop(m, ex.Pts);
                CloseAndSelectSketch(m, es);
                Func<bool, Feature> extrude = dir => m.FeatureManager.FeatureExtrusion3(
                    true, false, dir, (int)swEndConditions_e.swEndCondBlind, 0,
                    ex.Depth * MM, 0, false, false, false, false, 0, 0,
                    false, false, false, false, true, true, true,
                    (int)swStartConditions_e.swStartOffset, Math.Abs(ex.Z0) * MM, ex.Z0 < 0);
                Feature ef = extrude(false);
                if (ef == null) throw new Exception("Merged extrude failed in " + d.Name);
                // Make sure it went the intended way (into the part, not out of the back): check the z extent.
                double[] bx = (double[])((PartDoc)m).GetPartBox(true);
                double want0 = Math.Min(ex.Z0, -d.Depth / 2), want1 = Math.Max(ex.Z0 + ex.Depth, d.Depth / 2);
                if (bx[2] / MM < want0 - 0.5 || bx[5] / MM > want1 + 0.5)
                {
                    m.EditUndo2(1);
                    m.ClearSelection2(true);
                    m.Extension.SelectByID2(es, "SKETCH", 0, 0, 0, false, 0, null, 0);   // sketch is closed: just reselect it
                    ef = extrude(true);
                    if (ef == null) throw new Exception("Merged extrude failed in " + d.Name + " (reversed)");
                }
                var bodies = (object[])((PartDoc)m).GetBodies2((int)swBodyType_e.swSolidBody, true);
                if (bodies == null || bodies.Length != 1)
                    Console.WriteLine("  WARNING: " + d.Name + " has " + (bodies == null ? 0 : bodies.Length) + " bodies, expected 1");
            }

            // Outline cuts (star cap slot / notch): sketched on the Front plane, cut mid-plane right through.
            foreach (var cut in d.Cuts)
            {
                string cs = OpenSketchOnFront(m);
                Console.WriteLine("  " + d.Name + ": " + cs + " cut");
                DrawLoop(m, cut);
                CloseAndSelectSketch(m, cs);
                Feature cf = m.FeatureManager.FeatureCut4(
                    true, false, false, (int)swEndConditions_e.swEndCondMidPlane, 0,
                    4 * d.Depth * MM, 0, false, false, false, false, 0, 0,
                    false, false, false, false, false, true, true, true, true, false,
                    0, 0, false, false);
                if (cf == null) throw new Exception("Outline cut failed in " + d.Name);
            }

            for (int i = 0; i < d.Bolts.Count; i++)
            {
                Console.WriteLine("  " + d.Name + ": bolt hole " + (i + 1) + "/" + d.Bolts.Count);
                CutBolt(m, d.Bolts[i], d.Name);
            }
            if (view != null) view.EnableGraphicsUpdate = true;

            double[] box = (double[])((PartDoc)m).GetPartBox(true);
            zminMm = box[2] / MM;

            CustomPropertyManager cpm = m.Extension.CustomPropertyManager[""];
            cpm.Add3("Stock", (int)swCustomInfoType_e.swCustomInfoText, d.Stock,
                     (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
            foreach (var kv in d.Props)
                cpm.Add3(kv.Key, (int)swCustomInfoType_e.swCustomInfoText, kv.Value,
                         (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);

            string path = Path.Combine(dir, d.Name + ".SLDPRT");
            SaveDoc(m, path);
            if (d.Stl) ExportStl(app, m, Path.Combine(dir, d.Name + ".STL"));
            app.CloseDoc(m.GetTitle());
            return path;
        }

        // Save the open part as a binary STL in millimetres, fine quality, without any SOLIDWORKS dialogs.
        // The user's STL export settings are put back afterwards.
        static void ExportStl(SldWorks app, ModelDoc2 m, string path)
        {
            int units = (int)swUserPreferenceIntegerValue_e.swExportStlUnits, quality = (int)swUserPreferenceIntegerValue_e.swSTLQuality;
            var toggles = new[] { swUserPreferenceToggle_e.swSTLBinaryFormat, swUserPreferenceToggle_e.swSTLShowInfoOnSave,
                                  swUserPreferenceToggle_e.swSTLPreview, swUserPreferenceToggle_e.swSTLDontTranslateToPositive };
            int unitsWas = app.GetUserPreferenceIntegerValue(units), qualityWas = app.GetUserPreferenceIntegerValue(quality);
            var was = toggles.ToDictionary(t => t, t => app.GetUserPreferenceToggle((int)t));
            try
            {
                app.SetUserPreferenceIntegerValue(units, (int)swLengthUnit_e.swMM);
                app.SetUserPreferenceIntegerValue(quality, (int)swSTLQuality_e.swSTLQuality_Fine);
                app.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat, true);
                app.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLShowInfoOnSave, false);   // no pop-up
                app.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLPreview, false);          // no preview
                app.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLDontTranslateToPositive, false);
                m.ClearSelection2(true);   // with anything selected, SOLIDWORKS exports only the selection
                int err = 0, warn = 0;
                bool ok = m.Extension.SaveAs3(path, (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent | (int)swSaveAsOptions_e.swSaveAsOptions_Copy,
                    null, null, ref err, ref warn);
                if (!ok || err != 0) throw new Exception("STL export failed for " + path + " (error " + err + ")");
                Console.WriteLine("Saved " + path);
            }
            finally
            {
                app.SetUserPreferenceIntegerValue(units, unitsWas);
                app.SetUserPreferenceIntegerValue(quality, qualityWas);
                foreach (var t in toggles) app.SetUserPreferenceToggle((int)t, was[t]);
            }
        }

        // Round hole along any in-plane direction: revolve a thin rectangle 360 deg about its centre line.
        static void CutBolt(ModelDoc2 m, Bolt b, string name)
        {
            double r = BoltD / 2, dl = Math.Sqrt(b.Dx * b.Dx + b.Dy * b.Dy);
            double ux = b.Dx / dl, uy = b.Dy / dl, nx = -uy, ny = ux;
            double ax = b.X - ux * b.Len / 2, ay = b.Y - uy * b.Len / 2;
            double bx = b.X + ux * b.Len / 2, by = b.Y + uy * b.Len / 2;

            string sk = OpenSketchOnFront(m);
            Console.WriteLine("    " + sk + ": axis");
            SM(m).CreateCenterLine(ax * MM, ay * MM, 0, bx * MM, by * MM, 0);
            Console.WriteLine("    " + sk + ": profile");
            var rect = new List<double[]> { P(ax, ay), P(bx, by), P(bx + nx * r, by + ny * r), P(ax + nx * r, ay + ny * r) };
            DrawLoop(m, rect);
            CloseAndSelectSketch(m, sk);
            Console.WriteLine("    " + sk + ": revolve cut");
            Feature rev = m.FeatureManager.FeatureRevolve2(
                true, true, false, true, false, false,
                (int)swEndConditions_e.swEndCondBlind, 0, 2 * Math.PI, 0,
                false, false, 0, 0, 0, 0, 0, true, true, true);
            if (rev == null) throw new Exception("Bolt hole cut failed in " + name);
        }

        static SketchManager SM(ModelDoc2 m) { return m.SketchManager; }

        // Open a sketch on the Front plane and return its name (e.g. "Sketch3").
        static string OpenSketchOnFront(ModelDoc2 m)
        {
            SelectFront(m);
            SM(m).InsertSketch(true);
            var f = SM(m).ActiveSketch as Feature;
            if (f == null) throw new Exception("Could not open a sketch on the Front plane.");
            return f.Name;
        }

        // Close the active sketch and select it by name, ready for a feature.
        static void CloseAndSelectSketch(ModelDoc2 m, string name)
        {
            SM(m).InsertSketch(true);
            m.ClearSelection2(true);
            if (!m.Extension.SelectByID2(name, "SKETCH", 0, 0, 0, false, 0, null, 0))
                SelectLast(m, "ProfileFeature");
        }

        static void DrawLoop(ModelDoc2 m, List<double[]> pts)
        {
            SketchManager sm = m.SketchManager;
            for (int i = 0; i < pts.Count; i++)
            {
                double[] p = pts[i], q = pts[(i + 1) % pts.Count];
                sm.CreateLine(p[0] * MM, p[1] * MM, 0, q[0] * MM, q[1] * MM, 0);
            }
        }

        // The Front plane: try its usual names, then fall back to the first reference plane in the tree.
        static void SelectFront(ModelDoc2 m)
        {
            foreach (string n in new[] { "Front Plane", "Front", "Plane1" })
                if (m.Extension.SelectByID2(n, "PLANE", 0, 0, 0, false, 0, null, 0)) return;
            SelectFirst(m, "RefPlane", false);
        }

        static void SelectFirst(ModelDoc2 m, string typeName, bool append)
        {
            Feature f = (Feature)m.FirstFeature();
            while (f != null)
            {
                if (f.GetTypeName2() == typeName) { f.Select2(append, 0); return; }
                f = (Feature)f.GetNextFeature();
            }
            throw new Exception("Feature type not found: " + typeName);
        }

        static void SelectLast(ModelDoc2 m, string typeName)
        {
            Feature hit = null, f = (Feature)m.FirstFeature();
            while (f != null)
            {
                if (f.GetTypeName2() == typeName) hit = f;
                f = (Feature)f.GetNextFeature();
            }
            if (hit == null) throw new Exception("Feature type not found: " + typeName);
            hit.Select2(false, 0);
        }

        static void MakeAssembly(SldWorks app, List<PartDef> parts, Dictionary<string, string> paths,
                                 Dictionary<string, double> zmin, string dir)
        {
            // AddComponent5 only inserts parts that are already open, so open them all (silently) first.
            foreach (PartDef d in parts)
            {
                int err = 0, warn = 0;
                var doc = app.OpenDoc6(paths[d.Name], (int)swDocumentTypes_e.swDocPART,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref err, ref warn);
                if (doc == null) throw new Exception("Could not open " + paths[d.Name] + " (error " + err + ")");
            }
            Console.WriteLine("Building assembly");

            string tpl = app.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplateAssembly);
            var am = (ModelDoc2)app.NewDocument(tpl, 0, 0, 0);
            if (am == null)
                throw new Exception("Could not create an assembly. Set Tools > Options > Default Templates > Assembly.");
            var asm = (AssemblyDoc)am;
            var mu = (MathUtility)app.GetMathUtility();

            foreach (PartDef d in parts)
            {
                foreach (double x in d.Xs)
                {
                    // AddComponent5 puts the part's bounding-box centre at the given point, but each part is
                    // already modelled in its final position. So insert it anywhere, then set its transform
                    // explicitly: no rotation, shifted only by its X offset (the second foot).
                    Component2 comp = asm.AddComponent5(paths[d.Name],
                        (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,
                        "", false, "", 0, 0, 0);
                    if (comp == null) throw new Exception("Could not insert " + d.Name);
                    var xf = (MathTransform)mu.CreateTransform(new double[] {
                        1, 0, 0,  0, 1, 0,  0, 0, 1,     // rotation (identity)
                        x * MM, 0, 0,                    // translation
                        1,  0, 0, 0 });                  // scale, unused
                    comp.Transform2 = xf;
                    comp.Select4(false, null, false);
                    asm.FixComponent();
                    am.ClearSelection2(true);
                }
            }
            am.EditRebuild3();
            foreach (Component2 c in (object[])asm.GetComponents(true))
            {
                double[] t = (double[])((MathTransform)c.Transform2).ArrayData;
                Console.WriteLine("  {0,-14} at ({1:F1}, {2:F1}, {3:F1}) mm", c.Name2, t[9] / MM, t[10] / MM, t[11] / MM);
            }
            am.ShowNamedView2("*Front", (int)swStandardViews_e.swFrontView);
            am.ViewZoomtofit2();
            SaveDoc(am, Path.Combine(dir, "PineTree.SLDASM"));
        }

        // Close open documents saved in the output folder (assemblies first, so parts are released).
        static void CloseDocsIn(SldWorks app, string dir)
        {
            string full = Path.GetFullPath(dir).TrimEnd('\\') + "\\";
            Func<List<ModelDoc2>> inDir = () => ((object[])app.GetDocuments() ?? new object[0]).Cast<ModelDoc2>()
                .Where(d => (d.GetPathName() ?? "").StartsWith(full, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.GetType() == (int)swDocumentTypes_e.swDocDRAWING ? 0
                            : d.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY ? 1 : 2).ToList();
            // A few passes: closing a drawing or assembly can leave its parts loaded (hidden) until next pass.
            for (int pass = 0; pass < 3; pass++)
            {
                var docs = inDir();
                if (docs.Count == 0) break;
                var info = docs.Select(d => new { Title = d.GetTitle(), Path = d.GetPathName(), d.Visible }).ToList();
                foreach (var d in info)
                {
                    Console.WriteLine("Closing open " + System.IO.Path.GetFileName(d.Path) + (d.Visible ? "" : " (hidden)"));
                    app.CloseDoc(d.Title);
                }
            }

            // SOLIDWORKS can't save over a file it (or anything else) still has locked; stop early and say so.
            var locked = Directory.GetFiles(full, "*.SLD*").Where(f =>
            {
                try { using (File.Open(f, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { } return false; }
                catch (IOException) { return true; }
            }).ToList();
            if (locked.Count > 0)
            {
                var still = inDir();
                throw new Exception("These files are still locked, so they can't be rebuilt: " +
                    string.Join(", ", locked.Select(Path.GetFileName)) + ". " +
                    (still.Count > 0
                        ? "SOLIDWORKS still has loaded: " + string.Join(", ", still.Select(d => d.GetTitle() + (d.Visible ? "" : " (hidden)"))) + ". "
                        : "SOLIDWORKS doesn't list them as open. ") +
                    "Open windows that may be using them: " + string.Join(", ",
                        ((object[])app.GetDocuments() ?? new object[0]).Cast<ModelDoc2>().Where(d => d.Visible)
                        .Select(d => d.GetTitle() + " [" + (string.IsNullOrEmpty(d.GetPathName()) ? "not saved" : d.GetPathName()) + "]")) + ". " +
                    "Close them in SOLIDWORKS (or restart it), or use a different output folder.");
            }
        }

        // A3 drawing: front view of the tree with balloons, a BOM listing each part's Description, Length,
        // Ends and Holes custom properties, and notes on how the numbers are measured. Saved as
        // PineTree.SLDDRW and exported to PineTree.PDF.
        static void MakeDrawing(SldWorks app, Cfg c, string asmPath, string dir)
        {
            Console.WriteLine("Building drawing");
            string tpl = app.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplateDrawing);
            if (string.IsNullOrEmpty(tpl) || !File.Exists(tpl))
                tpl = Directory.GetFiles(@"C:\ProgramData\SOLIDWORKS", "*.drwdot", SearchOption.AllDirectories).FirstOrDefault();
            if (tpl == null) throw new Exception("No drawing template. Set Tools > Options > Default Templates > Drawings.");

            var dm = (ModelDoc2)app.NewDocument(tpl, (int)swDwgPaperSizes_e.swDwgPaperA3size, 0.420, 0.297);
            if (dm == null) throw new Exception("Could not create a drawing from " + tpl);
            var dd = (DrawingDoc)dm;
            var sheet = (Sheet)dd.GetCurrentSheet();
            double sw = 0.420, sh = 0.297;
            try { sheet.GetSize(ref sw, ref sh); } catch (Exception) { }

            // Front view on the left third of the sheet, at a standard scale that fits.
            double treeH = (c.H + 45 + c.StarR + 50) * MM, treeW = (c.BaseWidth + 100) * MM;
            double[] scales = { 1.0 / 5, 1.0 / 8, 1.0 / 10, 1.0 / 15, 1.0 / 20, 1.0 / 25 };
            double scale = scales.FirstOrDefault(s => treeH * s < sh * 0.75 && treeW * s < sw * 0.30);
            if (scale == 0) scale = scales.Last();
            double vx = sw * 0.17, vy = sh * 0.52;
            var view = dd.CreateDrawViewFromModelView3(asmPath, "*Front", vx, vy, 0);
            if (view == null) throw new Exception("Could not place the front view.");
            view.ScaleDecimal = scale;
            view.Position = new double[] { vx, vy };
            Console.WriteLine("  front view at 1:" + Math.Round(1 / scale));

            // Balloons on the view (item numbers match the BOM).
            try
            {
                dm.ClearSelection2(true);
                dm.Extension.SelectByID2(view.Name, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
                var bo = dd.CreateAutoBalloonOptions();
                bo.Layout = (int)swBalloonLayoutType_e.swDetailingBalloonLayout_Square;
                bo.IgnoreMultiple = true;
                bo.Style = (int)swBalloonStyle_e.swBS_Circular;
                dd.AutoBalloon5(bo);
                dm.ClearSelection2(true);
            }
            catch (Exception ex) { Console.WriteLine("  (balloons skipped: " + ex.Message + ")"); }

            // BOM, top right.
            string bomTpl = Path.Combine(Path.GetDirectoryName(app.GetExecutablePath()), @"lang\english\bom-standard.sldbomtbt");
            if (!File.Exists(bomTpl)) bomTpl = "";
            var bom = view.InsertBomTable4(true, sw - 0.010, sh - 0.010,
                (int)swBOMConfigurationAnchorType_e.swBOMConfigurationAnchor_TopRight,
                (int)swBomType_e.swBomType_PartsOnly, "", bomTpl, false,
                (int)swNumberingType_e.swNumberingType_Flat, false);
            if (bom == null) throw new Exception("Could not insert the BOM table.");
            var table = (TableAnnotation)bom;
            Func<string, int> colOf = title => Enumerable.Range(0, table.ColumnCount)
                .FirstOrDefault(i => (table.GetColumnTitle2(i, true) ?? "").Trim() == title);
            // Description goes right after PART NUMBER (unless the template already has one).
            if (!Enumerable.Range(0, table.ColumnCount).Any(i => (table.GetColumnTitle2(i, true) ?? "").Trim() == "DESCRIPTION"))
            {
                int pn = colOf("PART NUMBER");
                table.InsertColumn2((int)swTableItemInsertPosition_e.swTableItemInsertPosition_After, pn,
                    "DESCRIPTION", (int)swInsertTableColumnWidthStyle_e.swInsertColumn_DefaultWidth);
                bom.SetColumnCustomProperty(pn + 1, "Description");
                table.SetColumnTitle2(pn + 1, "DESCRIPTION", true);
            }
            foreach (var col in new[] { "Length", "Ends", "Holes" })
            {
                table.InsertColumn2((int)swTableItemInsertPosition_e.swTableItemInsertPosition_Last, table.ColumnCount - 1,
                    col.ToUpper(), (int)swInsertTableColumnWidthStyle_e.swInsertColumn_DefaultWidth);
                int idx = table.ColumnCount - 1;
                bom.SetColumnCustomProperty(idx, col);
                table.SetColumnTitle2(idx, col == "Length" ? "LENGTH (mm)" : col == "Holes" ? "HOLES (mm)" : "END CUTS", true);
            }
            // Smaller text so the long hole lists fit, and widths to suit.
            try
            {
                var tf = table.GetTextFormat();
                tf.CharHeight = 0.0022;
                table.SetTextFormat(false, tf);
            }
            catch (Exception) { }
            var widths = new Dictionary<string, double> {
                { "ITEM NO.", 0.010 }, { "PART NUMBER", 0.022 }, { "DESCRIPTION", 0.045 }, { "QTY.", 0.009 },
                { "LENGTH (mm)", 0.018 }, { "END CUTS", 0.072 }, { "HOLES (mm)", 0.105 } };
            double tableW = 0;
            for (int i = 0; i < table.ColumnCount; i++)
            {
                double wcol;
                if (widths.TryGetValue((table.GetColumnTitle2(i, true) ?? "").Trim(), out wcol)) table.SetColumnWidth(i, wcol, 0);
                tableW += widths.TryGetValue((table.GetColumnTitle2(i, true) ?? "").Trim(), out wcol) ? wcol : 0.02;
            }
            // Compact rows (they stay tall after the columns are narrowed otherwise).
            double tableH = 0;
            for (int r = 0; r < table.RowCount; r++)
            {
                double rh = r == 0 ? 0.008 : 0.0065;
                table.SetRowHeight(r, rh, 0);
                tableH += rh;
            }

            // Notes under the BOM.
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string notes = string.Join(System.Environment.NewLine, new[] {
                string.Format(inv, "PINE TREE  -  height {0:0} mm, base {1:0} mm, slope {2:0.0}\u00b0 from vertical", c.H, c.BaseWidth, c.PhiDeg),
                "LENGTH is long point to long point. Mitre angles are measured from square (saw setting).",
                string.Format(inv, "HOLES: \u00d8{0} mm, drilled through the side face, centred in the {1:0} mm thickness.", c.BoltD, c.T),
                "Hole positions are measured along the OUTER face (away from the centre) from the bottom long point.",
                "'square' = perpendicular to the board. 'drilled level' = horizontal with the tree standing (apex bolts).",
                "Left parts are mirror images of the right: same numbers.",
                "Drill each lap through both boards together, clamped in place, to keep the holes aligned.",
                string.Format(inv, "BASE: bottom uprights stand on the rail, rail sits on the feet. Clamp each leg in place and drill a Ø4.5 pilot {0:0} mm", c.BaseScrewDepth),
                string.Format(inv, "up through the rail hole into the leg end grain. Fix with an M6 x {0:0} coach screw up through foot + rail into the leg.",
                    Math.Ceiling((c.W + Math.Min(c.W, c.T) + c.BaseScrewDepth) / 10) * 10 - 10),
                string.Format(inv, "BOLTS (M6): laps M6 x {0:0}; apex M6 x {1:0} at 90 and M6 x {2:0} at 140 below the tip (or threaded rod cut to suit).",
                    BoltLen(2 * c.W), BoltLen(2 * 90 * Math.Tan(c.PhiDeg * Math.PI / 180)), BoltLen(2 * 140 * Math.Tan(c.PhiDeg * Math.PI / 180))) });
            var note = (Note)dm.InsertNote(notes);
            if (note != null)
            {
                var ann = (Annotation)note.GetAnnotation();
                ann.SetPosition2(sw - 0.010 - tableW, sh - 0.010 - tableH - 0.012, 0);   // under the BOM
            }

            // Detail A: the star and collar at 1:2.5, under the notes.
            try
            {
                double ri = 0.4 * c.StarR, top = c.H + c.StarGap + ri + c.StarR, bot = c.H - 72;
                double cy = (top + bot) / 2, rad = Math.Max((top - bot) / 2, c.StarR) + 8;   // model mm
                var mu = (MathUtility)app.GetMathUtility();
                var pt = (MathPoint)mu.CreatePoint(new double[] { 0, cy * MM, 0 });
                pt = (MathPoint)pt.MultiplyTransform((MathTransform)view.ModelToViewTransform);
                double[] sp = (double[])pt.ArrayData;                       // sheet coordinates
                dd.ActivateView(view.Name);
                // A view's sketch has its own coordinates: map the sheet centre and a rim point into them.
                var toSketch = (MathTransform)((Sketch)view.GetSketch()).ModelToSketchTransform;
                Func<double, double, double[]> sk = (x, y) =>
                    (double[])((MathPoint)((MathPoint)mu.CreatePoint(new double[] { x, y, 0 })).MultiplyTransform(toSketch)).ArrayData;
                double[] c0 = sk(sp[0], sp[1]), c1 = sk(sp[0] + rad * MM * scale, sp[1]);
                double rSk = Math.Sqrt((c1[0] - c0[0]) * (c1[0] - c0[0]) + (c1[1] - c0[1]) * (c1[1] - c0[1]));
                var circle = (SketchSegment)dm.SketchManager.CreateCircleByRadius(c0[0], c0[1], 0, rSk);
                if (circle == null) throw new Exception("no circle");
                circle.Select4(false, null);
                var det = dd.CreateDetailViewAt4(sw * 0.62, sh * 0.20, 0,
                    (int)swDetViewStyle_e.swDetViewSTANDARD, 1, 2.5, "A",
                    (int)swDetCircleShowType_e.swDetCircleCIRCLE, false, false, false, 5);
                Console.WriteLine(det != null ? "  detail A (star) at 1:2.5" : "  (detail view not created)");
                dm.ClearSelection2(true);
            }
            catch (Exception ex) { Console.WriteLine("  (detail view skipped: " + ex.Message + ")"); }
            // Leave the view's sketch so the drawing isn't left mid-edit (an active view blocks closing it later).
            try { dd.ActivateSheet(sheet.GetName()); dm.ClearSelection2(true); } catch (Exception) { }

            dm.EditRebuild3();
            SaveDoc(dm, Path.Combine(dir, "PineTree.SLDDRW"));
            int err = 0, warn = 0;
            string pdf = Path.Combine(dir, "PineTree.PDF");
            if (dm.Extension.SaveAs3(pdf, (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, null, ref err, ref warn))
                Console.WriteLine("Saved " + pdf);
            else
                Console.WriteLine("PDF export failed (error " + err + "). Use File > Save As > PDF on the drawing.");
        }

        // Bolt length for a grip of `wood` mm: add 20 for washers and nut, round up to 10.
        static double BoltLen(double wood) { return Math.Ceiling((wood + 20) / 10) * 10; }

        static void SaveDoc(ModelDoc2 m, string path)
        {
            int err = 0, warn = 0;
            bool ok = m.Extension.SaveAs3(path, (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, null, ref err, ref warn);
            if (!ok || err != 0)
                throw new Exception(string.Format("Could not save {0} (error {1}, warnings {2}). Is it open elsewhere or read-only?",
                    path, err, warn));
            Console.WriteLine("Saved " + path);
        }
    }
}
