# Notices & attribution

## This project

**Unsga3** is an independent C# implementation of **U-NSGA-III** (Seada & Deb).  
Copyright © 2026 Jason Bennitt / AppSprout. Licensed under the [MIT License](../LICENSE).

It is **not** affiliated with, endorsed by, or derived from the source code of pymoo, PlatEMO, jMetal, or the Kanpur Genetic Algorithms Laboratory. Algorithms and test problems follow **published papers** and public mathematical definitions.

## Academic references (please cite the papers, not only this library)

| Work | Role in Unsga3 |
|------|----------------|
| Seada, H. & Deb, K. (2016). *A Unified Evolutionary Optimization Procedure for Single, Multiple, and Many Objectives.* IEEE TEVC. | U-NSGA-III algorithm |
| Deb, K. & Jain, H. (2014). *NSGA-III.* IEEE TEVC. | Reference-point survival, hyperplane normalization |
| Das, I. & Dennis, J. E. (1998). *Normal-Boundary Intersection.* SIAM J. Optim. | Das–Dennis reference directions |
| Deb, K. et al. ZDT / DTLZ suites | Standard test problems |
| Deb & Agrawal — SBX; Deb et al. — polynomial mutation | Variation operators |

See also `CITATION.cff` for citing **this** software.

## Tools used for validation (not runtime dependencies)

| Project | License (approx.) | How we use it |
|---------|-------------------|---------------|
| [pymoo](https://pymoo.org/) | Apache-2.0 | **Oracle only** — external Python scripts under `tools/oracle/` compare IGD and fronts. **Not** linked into the NuGet package. |
| NumPy / SciPy (via pymoo env) | BSD | Optional stats / oracle scripts |

No pymoo or SciPy code is vendored in `src/`.

## Runtime dependencies

The **Unsga3** library package is pure managed C# with **no third-party NuGet dependencies** at runtime (framework reference only). Test projects use xUnit.

## Trademarks

Product names (pymoo, .NET, NuGet, etc.) are property of their respective owners and are used only for identification.
