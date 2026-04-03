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

## Current Implementations

### 1. WFC + DFS Baseline (Greedy Crawler)
To establish a baseline for comparison, a hybrid algorithm combining WFC with a Depth-First Search (DFS) agent was implemented. 

**Principle of Operation:**
* **Navigation:** The agent navigates the discrete grid step-by-step, utilizing a Greedy heuristic to move towards the goal.
* **Local WFC Intervention:** Instead of walking on a pre-generated map, the agent actively builds it. Upon stepping into a new uncollapsed cell, it forces a local WFC collapse, restricting the tile selection to pieces that provide a valid exit.
* **Propagation:** Every local collapse triggers WFC propagation to maintain stability in the surrounding grid (e.g., building walls around corners).
* **Iterative Restart:** Due to the stochastic nature of WFC tile selection, the agent might be forced into building a dead-end. The algorithm utilizes an iterative restart mechanism to discard invalid states and ensure a 100% valid output.

## Project Status & Roadmap

- [x] Initial Research & Literature Review
- [x] Repository Setup & Environment Configuration
- [x] Core WFC Implementation (C#)
- [x] WFC + DFS Baseline & Validation (Performance benchmarking)
- [ ] Optimization (Priority Queue for Entropy, execution time improvements)
- [ ] ACO Integration & Feedback Loop
- [ ] Comparison Framework & Data Collection
- [ ] Final Documentation

---
*This project is being developed as a Bachelor's Thesis at Faculty of Applied Sciences - University of West Bohemia*
