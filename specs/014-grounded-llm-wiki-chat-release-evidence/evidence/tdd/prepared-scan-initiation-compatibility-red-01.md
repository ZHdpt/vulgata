# T020 Prepared Scan tuple compatibility RED

Status: **expected compile RED recorded before fixture adaptation**

Source checkpoint: `fec5784c2eb206c0fc544d89528da3b4c133da00`.

T018 first established a behavioral RED against the old public seams: 0 passed,
2 failed. `IPreparedScanPairBuilder.BuildAsync` began with `Guid systemId`
instead of `PreparedScanInitiationTuple`, and `PreparedScanCoordinator` exposed
no `RecoverPreparedAsync` method.

After the T019 production-only tuple changes, Core, Infrastructure, Agents, and
Web compiled. The test-project build then failed only at these accepted
tuple-less fixture callers:

- `tests/Vulgata.Tests/AnalysisRuntime/PreparedScanRealQualificationTests.cs:356`
- `tests/Vulgata.Tests/AnalysisRuntime/PreparedScanRealQualificationTests.cs:475`
- `tests/Vulgata.Tests/PreparedScanning/PreparedScanReleaseQualificationTests.cs:665`
- `tests/Vulgata.Tests/PreparedScanning/PreparedScanApiTests.cs:276`

Each diagnostic was `CS1503`: argument 1 cannot convert from `System.Guid` to
`Vulgata.Core.DomainServices.PreparedScanInitiationTuple`.

No tuple-less production overload was added. No existing test was modified
before this RED was captured. Existing NU1507, NU1900, and MSB3277 warnings were
non-fatal and unrelated.
