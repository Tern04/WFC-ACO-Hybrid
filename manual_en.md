## User Manual — WFC-ACO-Hybrid Environment Generator

## Launching the Application

The application ships as a standalone Unity build for Windows and Linux. On launch the main menu appears with the generator configuration options.

---

## Main Menu

The startup panel exposes three configurable parameters:

**1. Grid size**

Determines the dimensions of the generated environment. Available presets:

| Label | Dimensions (X × Y × Z) | Cells | Type |
|---|---|---|---|
| Small 2D  | 10 × 1 × 10 | 100   | 2D |
| Medium 2D | 25 × 1 × 25 | 625   | 2D |
| Large 2D  | 50 × 1 × 50 | 2,500 | 2D |
| Small 3D  | 10 × 3 × 10 | 300   | 3D |
| Medium 3D | 20 × 4 × 20 | 1,600 | 3D |
| Large 3D  | 30 × 5 × 30 | 4,500 | 3D |

**2. Algorithm**

| Option | Description |
|---|---|
| **Pure WFC** | Plain Wave Function Collapse with no pre-computed path. Fast, but prone to contradictions on large grids (especially 2D). |
| **GLS Crawler** | Greedy Local Search pre-computes a path, then WFC fills the surroundings. Suitable for small and medium configurations. |
| **ACO Hybrid** | Ant Colony Optimization pre-computes a backbone path. Recommended for medium and large configurations. |

> **Recommendation:** For Large 2D (50 × 1 × 50) and all 3D configurations, use **ACO Hybrid** exclusively. GLS Crawler is very likely to fail on these. The system displays a warning when an unsuitable algorithm is selected.

**3. Generator seed**

An optional text field accepts an integer seed for deterministic reproduction of a specific result. If left empty, a seed is generated randomly and its value is shown in the result panel.

---

## Generating an Environment

After selecting parameters, press the **GENERATE** button.

The system automatically:

1. Initializes the grid and runs the selected algorithm.
2. On failure (contradiction), retries — the maximum attempt count scales automatically with grid size; ACO has a fixed cap of three attempts.
3. On success, displays the resulting 2D / 3D environment.

Progress is indicated by a status message in the top-left corner (*SUCCESS* / *FAILED*, attempt count, and where applicable the length of the built path).

---

## Navigating the Generated Environment

| Key / input | Action |
|---|---|
| `W` / `S` | Move forward / back |
| `A` / `D` | Strafe left / right |
| Mouse | Look around |
| `E` | Move up |
| `Q` | Move down |
| `Left Shift` | Sprint |
| `ESC` | Return to main menu |

The camera moves freely — no collision constraints are applied.

---

## Minimap

A 2D minimap of the current floor is shown in the top-right corner. It switches automatically to the floor matching the camera's current height. The camera position is rendered as a small marker on the minimap.

---

## Diagnostic Visualization (Pheromone Heatmap)

If ACO Hybrid fails across all attempts, a pheromone-matrix visualization is activated automatically. Grid nodes are rendered as volumetric markers:

- **Blue nodes (small)** — low pheromone concentration; the area was not actively explored by the agents.
- **Red nodes (large)** — high pheromone concentration; the area was visited intensively.

The visualization is a diagnostic aid — it highlights regions where the agents repeatedly failed to find a traversable path.

---

## Path-Only Mode

If GLS Crawler successfully builds a backbone path but the subsequent WFC fill ends in contradiction, the system enters Path-Only mode. In this mode only the cells forming the backbone path are rendered (highlighted in yellow); the surrounding environment is not instantiated.

This mode is available **only for single-floor (2D) configurations**.
