# Security Policy

## Supported versions

| Version | Supported |
|---------|-----------|
| 0.1.x   | Yes       |
| &lt; 0.1  | No        |

This is a pure numerical optimization library with **no network surface** and no default deserialization of untrusted input. Risk is mainly supply-chain (NuGet) and misuse in safety-critical decision systems.

## Reporting a vulnerability

Please **do not** open a public GitHub issue for security-sensitive reports.

Email the maintainers via the org contact on [AppSprout-dev](https://github.com/AppSprout-dev), or open a **private** security advisory on GitHub:

https://github.com/AppSprout-dev/Unsga3/security/advisories/new

Include:

- Affected package version / commit
- Description and impact
- Reproduction steps or PoC if available

We aim to acknowledge within a few business days.

## Non-goals

Algorithm quality (IGD vs pymoo, convergence failures on a problem class) is **not** a security issue — file a normal bug or equivalence issue instead.
