# Unsga3 API

Reference pages are generated from the public surface of `src/Unsga3`.

```bash
dotnet tool restore
dotnet docfx docfx.json
```

Output is `_site/` (gitignored). Metadata YAML lands in `api/` (also gitignored). The library targets `net8.0` and `net10.0`; DocFX extracts the `net10.0` build.
