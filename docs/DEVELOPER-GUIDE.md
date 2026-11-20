# Developer Guide
## Building
Run `Build-All.ps1` from the solution root. Requires Visual Studio 2022 with ASP.NET workload and Inno Setup 6.

## Testing
- Functional: `node tools/functional-test.js` (requires Node.js, Playwright)
- Unit: `dotnet test` (if test project exists)
- PDF rasterise: `tools/pdf-check.ps1`

## Architecture
See [README.md](../README.md#architecture).
