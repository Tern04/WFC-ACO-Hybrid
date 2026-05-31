# Hybrid Procedural Environment Generation (WFC + ACO)

This repository contains the implementation of my Bachelor's thesis: a hybrid procedural content generation (PCG) system built in **Unity 6.3 / C#** that combines **Wave Function Collapse (WFC)** with **Ant Colony Optimization (ACO)** to generate navigable 3D environments with a *guaranteed* traversable path from `[0, 0, 0]` to `[width-1, floors-1, depth-1]`.

The contribution is a practical demonstration that swarm-derived path constraints lift WFC out of the contradiction trap on long-route 3D grids: an ant colony pre-computes the strongest path through the uncollapsed grid, and that path is committed to WFC as a hard backbone constraint before the rest of the environment collapses around it.

## Tech Stack

* **Engine:** Unity 6.3
* **Language:** C#
* **Algorithms:**
    * Wave Function Collapse (constraint satisfaction)
    * Ant Colony Optimization (swarm-based pathfinding)
    * Greedy Local Search (baseline path crawler)

## Project Status

This is a completed Bachelor's thesis project. The implementation ships with three solvers, a UI for interactive generation, CSV benchmark harnesses, and a Linux, Windows build.

* **Full thesis (Czech):** [`Doc/Thesis_cz.pdf`](Doc/Thesis_cz.pdf)
* **User manual (Czech):** [`manual_cz.md`](manual_cz.md)
* **User manual (English):** [`manual_en.md`](manual_en.md)

## Implemented Algorithms

The application exposes three solvers selectable from the in-game UI (`GLS Crawler`, `ACO Hybrid`, `Pure WFC`):

### Pure WFC

Reference implementation of Wave Function Collapse over a 3D voxel grid (`WFCSolver`). Cells start with all `TileVariant` superpositions; the cell with the lowest entropy is collapsed to a single variant, and constraint propagation prunes incompatible variants from its six neighbours. Two optimisations matter for performance:

* **Min-heap with lazy deletion** (`CellMinHeap`) — cells are enqueued as `(Cell, priority_at_insertion)` tuples; stale entries are discarded at dequeue time, keeping extract-min at *O(log N)* without ever updating heap nodes in place.
* **Snapshot-based backtracking** — every `RemoveVariantFromCell` call is pushed to `removalHistory`; `SaveSnapshot()` / `RestoreSnapshot()` unwind the stack so a failed branch can be undone without rebuilding the grid.

Pure WFC is reliable on small grids but degrades rapidly on long-route 3D layouts, where it dead-ends and needs full map restarts.

### GLS + WFC (baseline hybrid)

> **Naming note:** earlier drafts of this project referred to this baseline as *"WFC + DFS"*. That label is inaccurate — the algorithm is not depth-first. The implementation is a one-step greedy crawler, and the class is named [`GLSHybridSolver`](WFC-ACO-Hybrid/Assets/_Project/Scripts/MapGeneration/Core/GLSHybridSolver.cs). The README, benchmarks, and UI labels now use the correct name (GLS).

`GLSHybridSolver` deploys a single agent that walks from start to finish one cell at a time. At every step it:

1. Inspects the six neighbours and keeps those whose facing socket admits a `path` or `stairs_up` tile.
2. **Sorts the candidates by Manhattan distance to the goal** and picks the closest one (purely greedy — no lookahead, no backtracking).
3. Calls `wfc.ForceCollapse()` to lock that neighbour to a path-bearing variant; WFC propagates the new constraint.

If the crawler runs out of valid neighbours, the entire map is discarded and `MapGenerator` retries from scratch. On large or 3D grids this restart loop is the dominant failure mode.

For **single-floor maps** there is an additional fallback: if GLS lays a valid path but the surrounding WFC fill subsequently contradicts, the solver returns the path anyway under a **`PATH-ONLY`** flag (rendered as a yellow status in the UI) rather than failing outright.

### ACO + WFC (main contribution)

`ACOHybridSolver` (with per-agent logic in `Ant.cs`) decouples path discovery from environment generation:

1. **Exploration.** A colony of artificial ants explores the uncollapsed grid. At each cell, an ant picks its next move with probability proportional to `(τ^α · η^β)`, where `τ` is the pheromone level on the edge and `η = 1 / (manhattan_distance_to_end + 0.1)` is the heuristic pull toward the goal. Each ant maintains a tabu list to avoid revisits.
2. **Pheromone update.** Paths that reach the goal deposit `Q / path_length` pheromone along their cells; the global field evaporates by `ρ` each iteration.
3. **Dry-run validation.** The strongest trail is extracted and *trial-collapsed* into WFC inside a `SaveSnapshot()` / `RestoreSnapshot()` bracket — this is the only safe way to verify the path is buildable under the socket system without corrupting solver state.
4. **Hard-constraint commit.** Once a path passes the dry run, every cell on it is force-collapsed to a path/stairs variant. WFC then fills the surrounding environment around this guaranteed-valid backbone.

**Default parameters** (see [`ACOHybridSolver.cs`](WFC-ACO-Hybrid/Assets/_Project/Scripts/MapGeneration/Core/ACOHybridSolver.cs)): `α = 1.0`, `β = 7.9`, `ρ = 0.15`, `Q = 100`, initial pheromone `0.1`.

**Dynamic swarm sizing.** To keep wall-clock time tractable across orders-of-magnitude grid scales, colony size and iteration count scale with the square root of the cell count:

```
colonySize    = clamp(round(sqrt(cells) * 0.5), 10, 300)
maxIterations = clamp(round(sqrt(cells) * 0.2),  8, 100)
```

Note that the pheromone field is used to **extract a single best path that becomes a hard WFC constraint**, not to bias WFC tile weights during collapse. Path discovery and environment collapse are kept as separate phases.

## Performance: Naive WFC Baseline

To establish a complexity baseline, pure WFC generation was benchmarked across grid sizes over **100 iterations** per configuration. The initial naive implementation uses a linear *O(N)* search to find the cell with the lowest entropy (`GetCellWithLowestEntropyNaive`). The table below shows how that single function becomes the dominant cost as the grid scales, validating the *O(N²)* overall complexity of the selection phase.

| Grid Size | Cell Count | Avg Total (ms) | Avg Entropy Search (ms) | Entropy Share (%) | Max Total Time (ms) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **10x3x10** | 300 | 8.68 | 0.00 | ~ 0.00 % | 17.79 |
| **15x3x15** | 675 | 21.09 | 1.00 | 4.74 % | 30.89 |
| **20x4x20** | 1,600 | 64.17 | 9.67 | 15.07 % | 74.15 |
| **25x4x25** | 2,500 | 116.42 | 25.96 | 22.30 % | 129.14 |
| **30x5x30** | 4,500 | 268.03 | 81.59 | 30.44 % | 306.09 |

**Observation:** while the cell count from a 25×4×25 grid to a 30×5×30 grid grows by a factor of **1.8**, isolated entropy-search time grows by a factor of **3.14**. At 4,500 cells the linear search alone consumes over 30 % of total CPU time — the motivation for the min-heap rewrite.

## Performance: Min-Heap Optimisation

To remove that bottleneck, the *O(N)* linear search was replaced with a custom **min-heap (priority queue)**. To handle the dynamic nature of WFC — where a cell's entropy decreases as constraints propagate — without paying an *O(N)* penalty to find and update heap entries, the implementation uses a **lazy-deletion** strategy: cells are enqueued as static `(Cell, priority_at_insertion)` tuples; when extracting the minimum, the algorithm checks whether the stored priority still matches the cell's current entropy and discards stale "ghost" references in *O(1)*, preserving strict *O(log N)* extract-min for valid entries.

| Grid Size | Cell Count | Avg Total (ms) | Avg Entropy Search (ms) | Entropy Share (%) | Max Total Time (ms) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **10x3x10** | 300 | 9.61 | 0.00 | ~ 0.00 % | 10.39 |
| **15x3x15** | 675 | 24.06 | 0.00 | ~ 0.00 % | 31.63 |
| **20x4x20** | 1,600 | 67.78 | 0.00 | ~ 0.00 % | 77.18 |
| **25x4x25** | 2,500 | 108.70 | 0.99 | 0.91 % | 118.98 |
| **30x5x30** | 4,500 | 212.44 | 1.99 | 0.93 % | 222.62 |

**Result.** On the largest tested grid (4,500 cells):

1. **Target acceleration.** Isolated entropy-search time dropped from **81.59 ms → 1.99 ms** (~40× speedup).
2. **Bottleneck eliminated.** The selection phase shrank from ~30 % of total execution time to **0.93 %**.
3. **Worst-case stability.** Max execution time fell from 306.09 ms to 222.62 ms — no more late-game spikes.

*All benchmarks were run under the same conditions on a MacBook Air with Apple M2 CPU.*

## Algorithm Comparison: GLS vs ACO

To quantify the value of the hybrid contribution, GLS and ACO were run head-to-head across six grid configurations (single-floor and multi-floor, small to large). The numbers below come from [`WFC-ACO-Hybrid/GLS_vs_ACO_Comparison.csv`](WFC-ACO-Hybrid/GLS_vs_ACO_Comparison.csv).

| Grid | GLS Success % | GLS Avg (ms) | ACO Success % | ACO Avg (ms) | ACO Avg Path Length |
| :--- | ---: | ---: | ---: | ---: | ---: |
| 10x1x10 | 10 % | 0.9 | **100 %** | 20.6 | 19.4 |
| 25x1x25 | 0 % | 1.4 | **100 %** | 66.9 | 52.6 |
| 50x1x50 | 0 % | 1.1 | **85 %** | 163.2 | 169.2 |
| 10x3x10 | 0 % | 0.6 | **90 %** | 41.3 | 21.7 |
| 20x4x20 | 5 % | 3.5 | **100 %** | 217.5 | 45.6 |
| 30x5x30 | 0 % | 1.4 | **85 %** | 830.1 | 70.5 |

**Headline finding.** GLS is fast but collapses to ≤ 10 % success on all but the smallest grid — its greedy single-step heuristic walks itself into dead ends that the surrounding WFC fill can't accommodate. ACO trades wall-clock time for **85–100 % success across every tested configuration** and produces meaningfully shorter paths (e.g. 45 vs 118 cells on 20×4×20). For the largest 3D grid (30×5×30, 4,500 cells) ACO is the only solver that succeeds at all.

## Project Structure

```
WFC-ACO-Hybrid/
├── Assets/_Project/Scripts/
│   ├── MapGeneration/
│   │   ├── Core/
│   │   │   ├── WFCsolver.cs          # constraint propagation + snapshots
│   │   │   ├── CellMinHeap.cs        # lazy-deletion priority queue
│   │   │   ├── GLSHybridSolver.cs    # greedy crawler baseline
│   │   │   ├── ACOHybridSolver.cs    # main contribution (swarm + dry-run)
│   │   │   ├── Ant.cs                # per-agent ACO logic
│   │   │   ├── Cell.cs               # voxel state
│   │   │   └── MapGeneration.cs      # orchestrator (MapGenerator MonoBehaviour)
│   │   └── Data/
│   │       ├── TileData.cs           # ScriptableObject: prefab + sockets + weight
│   │       └── TileVariant.cs        # runtime rotated state of a tile
│   ├── UI/MapGeneratorUI.cs          # IMGUI: presets + algorithm selector
│   └── Utils/
│       ├── EntropyBenchmark.cs       # CSV benchmark runner
│       ├── SpectatorCamera.cs        # WASD + Q/E free-fly camera
│       └── MinimapController.cs      # per-floor top-down minimap
```

## Running From Source

1. Open `WFC-ACO-Hybrid/` as a project in **Unity 6.3**.
2. Open the scene under `Assets/Scenes/`.
3. Press **Play**.

The in-game UI exposes six grid-size presets — **10×3×10**, **10×1×10**, **20×4×20**, **25×1×25**, **30×5×30**, **50×1×50** — and three solvers: **GLS Crawler**, **ACO Hybrid**, **Pure WFC**. The UI surfaces dynamic warnings when GLS or Pure WFC are selected on grids where they are known to fail, and recommends ACO instead.

## Running the Prebuilt Linux Build

A pre-compiled standalone version for Linux and Windows is available via Github Release:

**[Download link](https://github.com/Tern04/WFC-ACO-Hybrid/releases/latest)**

This build uses the dynamically scaled ACO solver with the experimentally chosen direction heuristic (`β = 7.9`). It demonstrates the speed, reliability, and capability of producing a fully valid path on grids up to ~60,000 cells with a near-99 % first-attempt success rate. On failure, the application renders a **volumetric heatmap** of the residual pheromone field as a visual debugging aid.

The build is compiled for standard 64-bit Linux distributions (Ubuntu, Pop!_OS, etc.):

**Method A — Terminal**

1. Extract the ZIP archive.
2. Open a terminal in the extracted directory.
3. Grant execute permission: `chmod +x Hybrid_LNX.x86_64`
4. Launch: `./Hybrid_LNX.x86_64`

**Method B — GUI**

1. Extract the ZIP archive.
2. Right-click `Hybrid_LNX.x86_64` and select **Properties**.
3. Open the **Permissions** tab.
4. Tick **"Allow executing file as program"**.
5. Close the dialog and double-click to run.

## Controls (Spectator Camera)

On successful generation the cursor locks and a free-fly camera activates:

* **W / A / S / D** — move forward / left / back / right
* **E / Q** — move up / down
* **Shift** (hold) — sprint
* **Mouse** — look around
* **ESC** — unlock cursor and reopen the main menu

## Benchmarking

`MapGenerator` exposes two programmatic benchmark entry points used to produce the tables above:

* `RunWFCBenchmark()` — sweeps pure WFC across the preset grid sizes and writes `WFC_Heap_Entropy.csv` to the Unity project root.
* `RunACOBenchmark()` — sweeps ACO under the dynamic-scaling regime and writes `ACO_Benchmark_SqrtDyn.csv`.

Additional comparison CSVs in `WFC-ACO-Hybrid/` (e.g. `GLS_vs_ACO_Comparison.csv`, `LargeGrid_Stresstest.csv`, `Algorithm_Comparison_Results*.csv`) capture earlier sweeps used while tuning parameters.

---

*Bachelor's Thesis — Faculty of Applied Sciences, University of West Bohemia.*
