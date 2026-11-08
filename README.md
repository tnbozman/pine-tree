# Stepped pine Christmas tree (flat, 42x42 pine, bolt-together)

One flat plane. Three parallel uprights per side overlap 130 mm with their long faces touching.
The bottom upright under-laps the middle, and the middle under-laps the top (the top one is on the outer line).
All lap ends are cut horizontal. Nothing is cut into other wood. All bolts run in the plane, perpendicular to the boards
(outer to inner). Nothing goes front to back. See `plane_layout4.png`.

Size: about 610 mm wide at the base (outside of the legs) and 1210 mm to the tip of the wood, star on top.

## Shopping (Bunnings)
- 42x42 DAR pine 1.8 m x 3 (about $69). Cuts fit with 5 mm kerf:
  1. P2 571 + P2 571 + RAIL 521
  2. P3 508 + P3 508 + P1 494
  3. P1 494 + FOOT 450 + FOOT 450
- M6 x 100 bolts (or M6 threaded rod cut to 100) x 8 for the laps, plus 8 nuts and 16 washers
- Apex: M6 x 80 (at 90 mm below tip) and M6 x 120 (at 140 mm), plus 2 x M6 x 80 for the star cap (40 and 62 mm below tip). Nuts and washers for each
- 4 x M6 x 100 coach screws (rail to legs) and 2 x M6 x 80 coach screws (feet)

## Cut list (stock length, mitre saw)
| Part | Qty | Length | Ends |
|---|---|---|---|
| P3 bottom upright | 2 | 507.8 | both ends 18 degree mitre, parallel (a parallelogram) |
| P2 middle upright | 2 | 570.9 | both ends 18 degree mitre, parallel |
| P1 top upright | 2 | 494.2 | lap end 18 degree mitre (horizontal), tip end 72 degree mitre (vertical cut so the pair meet) |
| RAIL | 1 | 521.3 | both ends 18 degree mitre, short face up |
| FOOT | 2 | 450 | square |

The 72 degree tip cut is beyond most mitre saws. Either cut it with a pull saw along a marked line held in the vice,
or clamp a 27 degree wedge jig to the saw fence (45 + 27 = 72).
Mirror the left side (P3R/P3L etc.): cut right boards, then flip the saw angle for the left.

## Hole positions (right side; left is the mirror)
All holes 6.5 mm, drilled square across the board face (drill press or a drilling guide), 42 mm board width.
Coordinates are x, y of the bolt centre in the face-on view; y up from the underside of the rail, x from the centreline.
Bolt direction is perpendicular to the uprights.
| Bolt | x | y | goes through |
|---|---|---|---|
| Lap 1 | 184.6 | 370 | P3 + P2 |
| Lap 2 | 161.9 | 440 | P3 + P2 |
| Lap 3 | 98.8 | 770 | P2 + P1 |
| Lap 4 | 76.1 | 840 | P2 + P1 |
On each board measure along its length from the lap end: bolts are 30 mm in from each end of each lap.
Apex (horizontal, through both P1 and the star neck): at 40, 62, 90 and 140 mm below the tip.

## Printed part (Bambu P1S, PETG)
`stl/star_cap.stl`: star plus neck block as one piece, 162 x 202 x 42 mm. The V slot grips both tips.
- Print as it sits (star flat on the bed), 0.2 mm layers, 5 walls, 40% gyroid, no supports
- One M6 nut pocket on the left and a washer recess on the right for each of the two neck bolts
- Use ASA instead of PETG if the tree sits in sun

## Assembly
1. Lay out one side: P1 on the outer line, P2 one board-width inside it, P3 one more inside, laps touching face to face. Clamp each lap and drill 6.5 mm through both boards. Repeat for the other side.
2. Bolt each lap (head on the outside, nut inside, washers both sides).
3. Stand the two sides on the feet. Screw the feet to the leg ends with a coach screw from below (pilot 4.5 mm).
4. Fit the rail between the legs at the bottom; fix with a coach screw horizontally through each leg into the rail end (pilot 4.5 mm).
5. Bring the two tips together. Bolt across at 90 and 140 mm. Fit the star cap over the tips and bolt it at 40 and 62 mm.
Dismantling after Christmas is the reverse: every joint is a bolt or screw.

## SolidWorks
`TreeBuilder.cs` + `TreeBuilder.csproj` (.NET Framework 4.8, x64). Set `SWAPI` if SOLIDWORKS is not in the default folder.
Start SOLIDWORKS from 3DEXPERIENCE first and leave it open; the program attaches to it (it never starts SOLIDWORKS itself).
Then run `TreeBuilder.exe [folder] [options]`: it writes each part and `PineTree.SLDASM`, with the bolt holes.
Re-running overwrites the files (it closes any of them that are open in SOLIDWORKS first).

Build instructions come out with every run, for the current size:
- `PineTree.PDF` / `PineTree.SLDDRW`: A3 sheet with the front view, balloons, and a BOM listing each part's length, end cuts and hole positions
- `CutList.csv`: the same table
- each part carries custom properties `Description`, `Length`, `Ends`, `Holes` (File > Properties)

Hole positions are measured along the board's outer face (away from the centre of the tree) from the bottom long point.

Options (mm):
- `--height 1210`: tip of the wood above the floor
- `--base 622`: length of the base rail (overall width of the base). The bottom uprights stand on the rail,
  `--RailOverhang` (20) in from each end, and the rail sits on the two feet. The upright slope is worked out from height and base.
- `--BaseScrewDepth 50`: how far the M6 coach screw (up through foot + rail) goes into each leg's end grain
- `--L1`, `--L3`: vertical extent of the top / bottom upright (default scales with height, 470 at 1210); the middle one takes the rest
- also `--W`, `--Overlap`, `--FootL`, `--BoltD`, `--StarR`, `--CapNeckH`, `--CapPlateT`, `--StockLen`
- `--preview`: print the slope, cut list and bolt positions, check the sizes, and stop without touching SOLIDWORKS

Example: `TreeBuilder.exe D:\Projects\christmas-tree\out --height 1500 --base 750`

The cut list and hole tables above are for the default size; use the program's printed output for any other size.
`stl/star_cap.stl` is also the default size only: if the slope changes, the V slot will not match. Use STAR_NECK + STAR_PLATE from SOLIDWORKS (or edit H/PHI in `geometry_check.py`).
Parts sit in final position and are fixed. Bolt holes are revolve cuts about an in-plane centreline.
If a call fails, build by hand: sketch each board outline on the Front plane from `geometry_check.py` (the board() polygons), extrude 42 mid-plane.
`geometry_check.py` regenerates the numbers and the STL (needs shapely, trimesh, manifold3d).
