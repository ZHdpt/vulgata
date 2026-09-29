# Navigation Data and Lifetime Model

These are conceptual navigation-only records. Names and shared fields must adapt to the integrated Story 18.1/F0 contract; this document does not define its prepared-source identity or file-tool schemas.

| Concept | Required information | Invariant and owner |
| --- | --- | --- |
| Prepared Repository grant reference | Application-selected F0 Repository binding and its relative-location authority | Owned by 18.1/F0. Worker arguments never carry a physical root or select another Repository. |
| Navigation profile | Application-selected Java/JDT-LS installation identity; disabled automatic Maven/Gradle import, build and restore; operation bounds | Tool Runtime/Infrastructure owns it. Fixed for a live session; no model override. |
| Navigation request | Definition or references; granted Repository-relative Java path; one-based source position; bounded result controls | Must validate against live grant before protocol use. No absolute/traversing/symlink-escaping input. |
| Navigation session | Grant/profile binding; owned process/protocol; separate disposable data/configuration; queue and health state | At most one live session per selected Repository. No cross-binding reuse; cleanup affects owned resources only. |
| Navigation observation | Request kind; observed contained relative path/range locations; observed count/limit state; capability/classpath limitations | A location is evidence only within the live grant. No raw physical URI or source content to Worker. |
| Navigation outcome | Observations plus explicit `observed`, `no result observed`, `limited`, `failed`, `cancelled` or `timed out` meaning | Zero locations never proves absence. An unmappable URI is withheld and reported as a limit/failure. |

## State transitions

```text
not acquired -> acquiring -> initializing -> ready -> settling -> released
                      |              |         |
                      +----failed-----+--unhealthy/timeout
```

One bounded queue serializes protocol work when concurrent calls are unsafe. A waiting cancellation removes that request; active cancellation/timeout retires the session if protocol settlement cannot be established. A subsequent attempt revalidates the same F0 grant and starts a new owned session. This state is disposable and is not Knowledge or a durable Source Snapshot.

## Containment and currentness

Validate relative inputs and every returned URI against the live, application-granted prepared root, including symlink/reparse escapes. Never return a location that cannot be mapped safely. The operator keeps source unchanged; known absence/change invalidates inspection claims. No repository-wide hash/change detector is introduced. Separate JDT-LS data/configuration from source, and qualify no source writes in the declared host profile.
