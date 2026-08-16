# Operator-norm bounds (read-only awareness)

**Related research repository (isolated):** [AppSprout-dev/crouzeix-extensions](https://github.com/AppSprout-dev/crouzeix-extensions)

The ordinary (scalar) Crouzeix theorem is settled (constant 2, August 2026).  
That repository works on the still-open completely-bounded analogue and multi-operator / joint spectral-set extensions.

- Agents and developers **may read** settled lemmas and numerical evidence from `crouzeix-extensions`.
- No code or data dependency is required by Unsga3.
- Do not open PRs or write into `crouzeix-extensions` from this repository (and vice versa).

When Unsga3 is used in hybrid loops with physics-based local search or matrix-function evaluations (e.g. inside Torquon-GB or Hygra pipelines), the sharp scalar factor 2 can tighten residual and approximation bounds on non-normal operators.
