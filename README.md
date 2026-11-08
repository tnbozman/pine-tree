# PineTree

A flat, bolt-together Christmas tree in pine, generated in SOLIDWORKS from C#.

<p align="center">
  <img src="tree_render.png" alt="Render of the pine tree: three stepped uprights each side, lap-bolted, with fairy lights and a glowing star on top" width="420">
</p>

Change the height, base width or timber size on the command line, and `TreeBuilder` rebuilds every part, the assembly, and an A3 build sheet with cut lengths, saw angles and hole positions. Every joint is a bolt or screw, so it comes apart flat after Christmas.

How the code works, and what went wrong getting it going: [docs/CAD-as-Code.md](docs/CAD-as-Code.md).

## The design

- **One flat plane.** Each side has three uprights stepping inwards: the top one (P1) is on the outer line, the middle one (P2) is one board width inside it, and the bottom one (P3) is one more inside.
- **Lap joints.** The uprights overlap by 130 mm with their side faces touching. Nothing is cut into other wood.
- **Bolts in the plane.** Lap bolts run square through both boards. Apex bolts run level through both top uprights. Nothing goes front to back.
- **Base.** The bottom uprights stand on a full-width rail, 20 mm in from each end. The rail sits on two feet that run front to back. One coach screw per side goes up through the foot and rail into the end grain of the leg.
- **Star.** A full five-point star on a printed collar. The collar's sides continue the line of the top uprights, and it grips both tips in a V slot.

## Quick start

You need Windows, SOLIDWORKS with its API (developed on SOLIDWORKS 3DEXPERIENCE R2026x), and the .NET SDK.

```powershell
dotnet build -c Release
# 1. Start SOLIDWORKS yourself and wait for it to finish loading.
# 2. Then:
.\bin\Release\net48\TreeBuilder.exe D:\Projects\christmas-tree\out
```

- **Start SOLIDWORKS first.** The program attaches to a running SOLIDWORKS and never starts one itself. Run it from a terminal at the same elevation as SOLIDWORKS (both normal, or both as administrator).
- **Re-running overwrites the output.** Files from the output folder that are open in SOLIDWORKS are closed first. If something else still holds them, the program stops and tells you which window to close.
- **Different install folder:** if SOLIDWORKS isn't in the default folder, build with `dotnet build -c Release /p:SWAPI="<install>\SOLIDWORKS\api\redist"`.

Each run writes these to the output folder:

| File | What it is |
| --- | --- |
| `PineTree.PDF`, `PineTree.SLDDRW` | A3 build sheet: front view with balloons, parts table (length, end cuts, holes), notes, and a 1:2.5 close-up of the star |
| `CutList.csv` | The parts table as a spreadsheet |
| `PineTree.SLDASM` | The assembly, every part fixed in place |
| `P1L` … `P3R`, `RAIL`, `FOOT`, `STAR_CAP` (`.SLDPRT`) | One file per part, with custom properties `Description`, `Length`, `Ends`, `Holes` |
| `STAR_CAP.STL` | The star and collar, ready to print |

## Changing the size

Every setting is an option. Try a size with `--preview` first: it prints the slope, cut list and hole positions, checks that the design goes together, and stops without touching SOLIDWORKS.

```powershell
.\bin\Release\net48\TreeBuilder.exe --preview --height 1500 --base 1000
.\bin\Release\net48\TreeBuilder.exe D:\Projects\christmas-tree\out --height 1500 --base 1000
```

| Option | Default (mm) | Meaning |
| --- | --- | --- |
| `--height` | 1210 | Tip of the wood above the underside of the rail |
| `--base` | 922 | Length of the rail, which is the overall width of the base. The upright slope is worked out from height and base. |
| `--W` | 45 | Board width seen from the front; also the rail height |
| `--T` | 90 | Board thickness front to back. For 42 × 42 timber use `--W 42 --T 42`. |
| `--Overlap` | 130 | Overlap of each lap |
| `--L1`, `--L3` | auto | Height covered by the top and bottom uprights (470 at a height of 1210, scaling with height); the middle one takes the rest |
| `--RailOverhang` | 20 | Rail beyond the outside of each leg |
| `--BaseScrewDepth` | 50 | Depth of the base coach screw into the leg |
| `--FootL` | 450 | Foot length, front to back |
| `--BoltD` | 6.5 | Hole diameter (M6 clearance) |
| `--StarR`, `--StarGap`, `--CapWall` | 85, 3, 8 | Star radius, gap between the wood tip and the star, collar wall thickness |
| `--CapNeckH`, `--CapPlateT` | auto, 12 | Collar depth (follows `--T`) and star thickness |
| `--StockLen` | 1800 | Length of timber you buy; only used to warn about boards that won't fit |

The program refuses sizes that won't go together, with a plain reason. Examples: laps on the middle upright that would meet, legs that would meet on the rail, or a slope over 30°.

## Default size

At the defaults the tree is 922 mm wide, about 1375 mm tall to the top of the star (including the 45 mm feet), and the uprights lean 24.9° from vertical.

### Cut list

| Part | Qty | Length (mm) | Ends |
| --- | --- | --- | --- |
| P3 bottom upright | 2 | 489.3 | both 24.9° mitre, parallel |
| P2 middle upright | 2 | 605.1 | both 24.9° mitre, parallel |
| P1 top upright | 2 | 518.1 | bottom 24.9° mitre; tip 65.1°, so it stands vertical and the pair meet |
| RAIL | 1 | 922.0 | square |
| FOOT | 2 | 450.0 | square, laid wide face down |

- **Lengths** are long point to long point, and saw angles are measured from square.
- **Left and right:** cut the right-hand boards, then flip the saw angle for the left.
- **The 65.1° tip cut** is beyond most mitre saws. Mark it and cut by hand, or clamp a 20.1° wedge to the fence and set the saw to 45°.
- **Cutting plan**, from two 3.0 m lengths of 90 × 45 with a 5 mm kerf:
  1. RAIL 922, P2 605, P2 605, P1 518 (2670 mm)
  2. P1 518, P3 489, P3 489, FOOT 450, FOOT 450 (2421 mm)

### Holes

All holes are 6.5 mm, drilled through the side face, centred in the 90 mm thickness. Positions are measured along the board's outer face (the side away from the centre of the tree) from its bottom long point.

| Part | Square holes (lap bolts) | Level holes (apex and star) |
| --- | --- | --- |
| P3 | 358.2, 435.4 | none; base pilot drilled through the rail |
| P2 | 53.9, 131.1, 474.0, 551.1 | none |
| P1 | 53.9, 131.1 | 363.8, 418.9, 449.7, 474.0 (24.9° off square, so level when standing) |
| RAIL | 56.4 from each end, vertical | none |
| FOOT | 225 (the middle), vertical | none |

The lap positions are for marking. For holes that line up, clamp each lap in place and drill through both boards together.

### Hardware

| Item | Qty | Use |
| --- | --- | --- |
| M6 × 110 bolt | 8 | Laps, two per lap |
| M6 × 110 bolt | 1 | Apex, 90 mm below the tip |
| M6 × 150 bolt | 1 | Apex, 140 mm below the tip |
| M6 × 80 and M6 × 100 bolt | 1 each | Star collar, 40 and 62 mm below the tip |
| M6 nut | 12 | One per bolt |
| M6 washer | 24 | Both sides of every bolt |
| M6 × 130 coach screw | 2 | Base: up through foot and rail into each leg |

M6 threaded rod cut to length works in place of any of the bolts.

## Assembly

1. **Cut and mark.** Cut the boards from the cut list, then mark the holes.
2. **Lay out one side.** Put P1 on the outer line, P2 one board width inside it, and P3 one more inside, with the laps touching face to face. Clamp each lap, drill through both boards, then bolt it with the head outside and the nut inside, washers both sides. Repeat for the other side.
3. **Join the tips.** Bring the two top uprights together and bolt across at 90 and 140 mm below the tip.
4. **Fix the base.** Stand the frame on the rail with each leg 20 mm in from the end. Clamp each leg, then drill a 4.5 mm pilot 50 mm deep up through the rail hole into the leg. Put a foot under each end and drive an M6 × 130 coach screw up through foot and rail into the leg.
5. **Fit the star.** Slide the collar over the tips and bolt it at 40 and 62 mm below the tip.

To take it apart, reverse the steps.

## The printed star

The star and its collar are one part, `STAR_CAP`, and every run exports it as `STAR_CAP.STL`. The export is binary, in millimetres, at fine quality. It follows the size you build: the V slot matches the slope and the collar matches the timber thickness.

- **Shape:** the star is 12 mm thick and centred in the depth of the wood. The collar is as deep as the timber (90 mm at the defaults). At the defaults the cap is 162 × 194 × 90 mm.
- **Printing:** PETG, or ASA if the tree sits in the sun. Lay it back face down. The star's arms are about 39 mm off the bed, so add supports under them (tree supports work well).
- **Fixing:** the two M6 holes through the collar are printed in.

## Repository

| Path | What it is |
| --- | --- |
| `TreeBuilder.cs`, `TreeBuilder.csproj` | The generator (.NET Framework 4.8, x64) |
| `docs/CAD-as-Code.md` | Guide to the approach, API patterns and an FAQ of problems hit getting started |
| `geometry_check.py` | The original Python geometry model and STL generator (42 × 42 design; needs shapely, trimesh and manifold3d) |
| `tree_render.png` | Render of the tree, shown at the top of this page |
| `plane_layout4.png` | Early layout sketch (the base and star have changed since) |
| `stl/` | Star cap from the original 42 × 42 design (superseded by the generated `STAR_CAP.STL`) |

## License

MIT. See [License.txt](License.txt).
