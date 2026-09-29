# Navigation Data and Lifetime Model

Navigation records bind to integrated Story 18.1 source identity and authorization without changing its public read issuer, context, functions or result envelope. Story 18.2 owns the shared trusted physical-workspace lease; Story 18.3 consumes it after a separately authorized verified checkpoint.

| Concept | Required information | Invariant and owner |
| --- | --- | --- |
| Prepared source approval | Existing `PreparedSourceApproval`: batch, Repository, subject, `WorkerSourceRead` purpose, approval/root-binding identities, operator label | Owned by 18.1. Exact current tuple is checked at issuance, during use and before disclosure; no Worker argument selects it. |
| Tool purpose authorization | Trusted application-selected `JdtNavigation` or `CodeGraphPreparation` with current authorization | Owned by shared 18.2 seam. `WorkerSourceRead` alone is insufficient for a subprocess/tool. |
| Trusted workspace lease | Prepared approval, selected tool purpose, held root identity/handle, deadline, cancellation and lifetime | Infrastructure-only physical path access; asynchronous disposal after all adapter calls. JDT-independent for 18.3. A path lease is not subprocess containment. |
| Navigation profile | Application-selected Java/JDT-LS installation identity; disabled automatic Maven/Gradle import, build and restore; operation bounds | Tool Runtime/Infrastructure owns it. Fixed for a live session; no model override. |
| Navigation request | Definition or references; granted Repository-relative Java path; one-based source position; bounded result controls | Must validate against live grant before protocol use. No absolute/traversing/symlink-escaping input. |
| Navigation session | Trusted lease/profile binding; owned process/protocol; separate disposable data/configuration; queue and health state | At most one live session per selected Repository. No cross-binding reuse; cleanup affects owned resources only. |
| Navigation observation | Request kind; observed contained relative path/range locations; observed count/limit state; capability/classpath limitations | A location is evidence only within the live grant. No raw physical URI or source content to Worker. |
| Navigation outcome | Observations plus explicit `observed`, `no result observed`, `limited`, `denied`, `authority unavailable`, `failed`, `cancelled` or `timed out` meaning | Zero locations never proves absence. Authority loss discards all buffered locations/counts/limits across concurrent calls; an unmappable URI is withheld. |

## State transitions

```text
not acquired -> acquiring -> initializing -> ready -> settling -> released
                      |              |         |
                      +----failed-----+--unhealthy/timeout
```

One bounded queue serializes protocol work when concurrent calls are unsafe. A waiting cancellation removes that request; active cancellation/timeout retires the session if protocol settlement cannot be established. Revoked/changed/unavailable authority latches the lease and session closed, cancels owned activity and discards pending outputs. A subsequent attempt requires new issuance under current source and tool-purpose authority; it never reopens a closed lease. This state is disposable, not Knowledge or a durable Source Snapshot.

## Containment and currentness

Validate relative inputs and every returned URI against the live, application-granted prepared root, including symlink/reparse escapes. Never return a location that cannot be mapped safely. Recheck exact current source and tool-purpose authority immediately before release of any response. The operator keeps source unchanged; known absence/change invalidates inspection claims. No repository-wide hash/change detector is introduced. Separate JDT-LS data/configuration from source, and qualify no source writes and actual subprocess confinement in the declared host profile.
