# Hybrid Procedural Environment Generation (WFC + ACO)

This repository contains the implementation of my Bachelor's thesis project focused on a hybrid approach to procedural content generation (PCG). The project combines the **Wave Function Collapse (WFC)** algorithm with **Ant Colony Optimization (ACO)** to create navigable and structurally logical environments.

## Key Objectives

The main goal is to develop a hybrid algorithm where agent-based feedback (ACO) influences the constraint-based generation (WFC).

* **Pheromone-Driven Probabilities:** Implementing a feedback loop where pheromone density influences WFC tile selection (adjusting weights based on successful pathfinding).
* **Verticality & Navigation:** Support for movement in 2D grids including vertical elements like stairs and ramps.
* **Comparative Analysis:** Evaluating the performance and output quality of the Hybrid approach vs. Pure WFC generation.
* **Visual Debugging:** Real-time visualization of pheromone trails and WFC entropy state within the Unity scene.

## Tech Stack

* **Engine:** Unity 6.3
* **Language:** C#
* **Algorithm Base:** 
    * Wave Function Collapse (Constraint Satisfaction)
    * Ant Colony Optimization (Swarm Intelligence / Pathfinding)

## Project Status & Roadmap

- [x] Initial Research & Literature Review
- [x] Repository Setup & Environment Configuration
- [x] Core WFC Implementation (C#)
- [x] WFC + DFS Baseline & Validation (Performance benchmarking)
- [x] Optimization (Priority Queue for Entropy, execution time improvements)
- [ ] ACO Integration & Feedback Loop
- [ ] Comparison Framework & Data Collection
- [ ] Final Documentation

## Current Implementations

### 1. WFC + DFS Baseline (Greedy Crawler)
To establish a baseline for comparison, a hybrid algorithm combining WFC with a Depth-First Search (DFS) agent was implemented. 

**Principle of Operation:**
* **Navigation:** The agent navigates the discrete grid step-by-step, utilizing a Greedy heuristic to move towards the goal.
* **Local WFC Intervention:** Instead of walking on a pre-generated map, the agent actively builds it. Upon stepping into a new uncollapsed cell, it forces a local WFC collapse, restricting the tile selection to pieces that provide a valid exit.
* **Propagation:** Every local collapse triggers WFC propagation to maintain stability in the surrounding grid (e.g., building walls around corners).
* **Iterative Restart:** Due to the stochastic nature of WFC tile selection, the agent might be forced into building a dead-end. The algorithm utilizes an iterative restart mechanism to discard invalid states and ensure a 100% valid output.

## Performance Profiling: Naive WFC Baseline

To establish a baseline for algorithmic complexity, the pure Wave Function Collapse (WFC) generation was benchmarked across various grid sizes over **100 iterations** per configuration. 

The initial naive implementation utilizes a linear $O(N)$ search to find the cell with the lowest entropy. The data below demonstrates how this specific method (`GetCellWithLowestEntropyNaive`) becomes a computational bottleneck as the environment scales, heavily validating the theoretical $O(N^2)$ overall time complexity for the selection phase.

| Grid Size | Cell Count | Avg Total (ms) | Avg Entropy Search (ms) | Entropy Share (%) | Max Total Time (ms) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **10x3x10** | 300 | 8.68 | 0.00 | ~ 0.00 % | 17.79 |
| **15x3x15** | 675 | 21.09 | 1.00 | 4.74 % | 30.89 |
| **20x4x20** | 1,600 | 64.17 | 9.67 | 15.07 % | 74.15 |
| **25x4x25** | 2,500 | 116.42 | 25.96 | 22.30 % | 129.14 |
| **30x5x30** | 4,500 | 268.03 | 81.59 | 30.44 % | 306.09 |

**Observation:** While the cell count from a $25 \times 4 \times 25$ grid to a $30 \times 5 \times 30$ grid increases by a factor of **1.8**, the isolated entropy search execution time increases by a factor of **3.14**. At 4,500 cells, the linear search function alone consumes over 30% of the entire CPU execution time. This benchmark serves as the foundation for the upcoming Min-Heap (Priority Queue) architectural optimization.

## Performance Profiling: Min-Heap Optimization

To eliminate the computational bottleneck observed in the naive approach, the $O(N)$ linear search was replaced with a custom **Min-Heap (Priority Queue)** data structure. 

To handle the dynamic nature of WFC (where a cell's entropy decreases as constraints propagate) without incurring an $O(N)$ penalty to search and update the heap, the implementation utilizes a **Lazy Deletion** strategy. Cells are enqueued as static tuples `(Cell, priority_at_insertion)`. When extracting the minimum value, the algorithm verifies if the stored priority matches the cell's current entropy. Stale or "ghost" references are discarded in $O(1)$ time, strictly maintaining the $O(\log N)$ operational complexity for valid extractions.

| Grid Size | Cell Count | Avg Total (ms) | Avg Entropy Search (ms) | Entropy Share (%) | Max Total Time (ms) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **10x3x10** | 300 | 9.61 | 0.00 | ~ 0.00 % | 10.39 |
| **15x3x15** | 675 | 24.06 | 0.00 | ~ 0.00 % | 31.63 |
| **20x4x20** | 1,600 | 67.78 | 0.00 | ~ 0.00 % | 77.18 |
| **25x4x25** | 2,500 | 108.70 | 0.99 | 0.91 % | 118.98 |
| **30x5x30** | 4,500 | 212.44 | 1.99 | 0.93 % | 222.62 |

**Conclusion & Comparison:** The transition to an $O(\log N)$ search complexity successfully neutralized the exponential scaling issue. On the largest tested grid (4,500 cells):
1. **Target Acceleration:** The isolated entropy search time plummeted from **81.59 ms to 1.99 ms** (an approximate 40x speedup).
2. **Bottleneck Elimination:** The selection phase, which previously consumed over 30% of the total execution time, was reduced to a marginal **0.93%**.
3. **Worst-Case Stability:** The structural integrity of the tuple-based Min-Heap entirely eliminated execution spikes, reducing the maximum execution time (Worst-Case) from 306.09 ms to 222.62 ms.

*Note: All final benchmarks were executed under the same conditions on MacBook Air with Apple M2 CPU.*

---
*This project is being developed as a Bachelor's Thesis at Faculty of Applied Sciences - University of West Bohemia*
