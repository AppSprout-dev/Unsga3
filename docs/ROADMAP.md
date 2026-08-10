# Roadmap

Living plan for Unsga3. Issues track concrete work; this page is the narrative.

## Done (0.1.x)

- [x] Core U-NSGA-III (Seada & Deb): refs, SBX/PM, tournament, NSGA-III survival  
- [x] ZDT / DTLZ / SO problem suite + IGD / HV metrics  
- [x] pymoo oracle path + mean IGD alignment  
- [x] Hyperplane normalization parity (ASF extremes, persistent ideal)  
- [x] Duplicate elimination  
- [x] Multi-seed Mann–Whitney / Wilcoxon harness  
- [x] GitHub Packages publish on `v*` tags + CI  

## Near term (0.2)

- [ ] Multi-seed Wilcoxon results checked in / refreshed on release  
- [ ] `net8.0` (+ `net10.0`) multi-target for broader NuGet consumers  
- [ ] nuget.org publish (in addition to GitHub Packages)  
- [ ] IGD+ / GD+ indicators  
- [ ] Constrained demos (OSY / TNK) with self-tests  
- [ ] API docs site (DocFX or similar)  

## Medium term (0.3+)

- [ ] WFG1–9 suite  
- [ ] Parallel evaluation hook (`IProblem` batch / `Parallel.For`)  
- [ ] Checkpoint / resume + progress callbacks  
- [ ] Optional PlatEMO / jMetal front-file comparison  
- [ ] Performance pass (allocations in hot survival loop)  

## Non-goals (for now)

- GPU / CUDA kernels  
- Auto-ML / hyperparameter tuning frameworks  
- Full multi-algorithm toolbox (NSGA-II, MOEA/D, …) — may live as sibling packages later  

## Feedback

Open an [issue](https://github.com/AppSprout-dev/Unsga3/issues) with the `enhancement` label, or discuss in a PR. Real-world multi-objective use cases (engineering design, scheduling, finance) are especially welcome — they drive the API.
