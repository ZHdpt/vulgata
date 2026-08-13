# Contract: Demo Resolution Profile

## Reviewed profile artifact

The implementation input and sole selector authority is the fixed-name embedded JSON resource whose complete reviewed planning form is [demo-resolution-profile.v1.json](demo-resolution-profile.v1.json). The catalog loads that resource only; runtime callers cannot supply a path, replace its bytes, select another resource, or choose replacement selectors during implementation. The reviewed artifact contains no database identity, Git URL, credential, environment variable, absolute host/WSL/container path, or external working-copy location.

```text
ExactResolutionProfile
  schemaVersion: "1.0"
  profileId/profileVersion
  repositories: ProfileRepository[2]
  occurrences: ResolutionOccurrence[1..16]
  unsupportedObservations: UnsupportedObservation[1..16]
```

The catalog rejects duplicate/unknown properties, invalid UTF-8, trailing JSON, depth over 16, oversized resource (256 KiB), noncanonical ordering, duplicate identities/aliases, and unbounded collections. It computes SHA-256 over canonical UTF-8 JSON and exposes the lowercase fingerprint.

## Immutable Repository entries

The reviewed repository facts are:

| Alias / stable label | Expected full ref | Expected full commit |
| --- | --- | --- |
| `mbs_account` | `refs/tags/vulgata/August15th-sample` | `451b3a99b179b711ae66260651eb6b12db482e0b` |
| `mbs_channel` | `refs/tags/vulgata/August15th-sample` | `d2e11bacd157195c421065292674ab0c29260081` |

Aliases/stable labels are profile data and never production code branches. Forbidden profile values include persistence IDs, Git URLs, filesystem paths other than safe Repository-relative selectors, credentials, mutable branches, and Repository names used as matching conditionals.

## Reviewed exact occurrence

Read-only `rev-parse`, `grep`, `show`, and `ls-tree` against the two tagged objects established the occurrence below. No checkout, fetch, reset, stage, commit, or external-repository write was used.

| Field | Pinned value |
| --- | --- |
| Occurrence / direction | `account-to-channel-cust-update-session-v1`; consumer `mbs_account` -> provider `mbs_channel` |
| Consumer commit | `451b3a99b179b711ae66260651eb6b12db482e0b` |
| Consumer source | `src/main/java/cn/com/yitong/actions/mobile/UpdateChannelSessionOp.java`, line 45 |
| Accepted call | simple member invocation `netTools.execute`; receiver `netTools`, member `execute`, direct ordinary string literal at zero-based argument index `1`, ordinal `0` on the line |
| Observed consumer identifier | `cust/updateSession` |
| Provider commit | `d2e11bacd157195c421065292674ab0c29260081` |
| Provider XML | `src/main/resources/disgram/in/cust/updateSession.xml`, root `trans`, attribute `name`, line 2, ordinal `0` |
| Observed provider identifier | `in/cust/updateSession` |
| Provider implementation Document selector | `src/main/java/cn/com/yitong/actions/domain/login/UpdateSessionOp.java`; exact type symbol `cn.com.yitong.actions.domain.login.UpdateSessionOp` |
| Normalizer / comparison | `custom-rpc-in-prefix-v1`; ordinal |

The consumer tokens fit the closed scanner grammar exactly:

```text
Identifier("netTools") . Identifier("execute")
  ( expression-argument-0 , direct-ordinary-string-literal-argument-1 ) ;
```

The XML root fits the closed provider grammar exactly: one `trans` element with one non-empty `name` attribute. `custom-rpc-in-prefix-v1` first applies Java/XML decoding, Unicode Form C, and ASCII SP/HT/CR/LF edge trimming to both sides; it then requires and removes exactly one leading `in/` from the provider identifier while leaving the consumer identifier unchanged. It rejects a missing, repeated, empty-result, control-containing, or over-512-byte prefix result. It performs no case folding, URL decoding, slash rewriting, regex, mapping-table lookup, alias inference, LLM interpretation, or System-context inference. Both normalized values are therefore the observed `cust/updateSession`, without inventing an endpoint.

## Resolution occurrence shape

```text
ResolutionOccurrence
  occurrenceId
  protocol: "custom-rpc"
  consumerAlias / providerAlias
  consumer:
    files: exact Repository-relative file[1..16]
    invocation:
      form: "simple-member-invocation"
      receiverSymbol / memberName
      endpointArgumentIndex: 0..15
      line: positive integer
      occurrenceOrdinalOnLine: 0..15
  provider:
    root: "src/main/resources/disgram/in"
    files: exact Repository-relative XML file[1..16]
    declaration: element/attribute/line/ordinal
    implementationDocument: Repository-relative path + exact type symbol
  normalization: closed ID and role-specific prefix rule
  comparison: "ordinal"
```

Consumer/provider aliases must be distinct and present in `repositories`. All file paths are safe Form-C Repository-relative paths. Provider files must remain beneath the fixed root; no glob or directory walk is accepted. The consumer scanner accepts only the configured receiver/member/form/argument/line occurrence, so other `netTools*` calls remain outside this occurrence.

## Reviewed unsupported observation

The same read-only account object contains the bounded file/storage observation pinned in the JSON resource:

| Field | Pinned value |
| --- | --- |
| Observation identity / kind | `account-file-storage-oss-upload-v1`; `file-storage` |
| Owner Repository | `mbs_account` at `451b3a99b179b711ae66260651eb6b12db482e0b` |
| Authorized owner Document selector | `src/main/java/cn/com/yitong/actions/cust/UploadOcrToOssOp.java`; exact type symbol `cn.com.yitong.actions.cust.UploadOcrToOssOp` |
| Observation selector | simple member invocation; receiver `ossClientService`; member `simpleUploadToOSSBase64String`; line 66; ordinal 0 |
| Safe locator | `src/main/java/cn/com/yitong/actions/cust/UploadOcrToOssOp.java#line=66;rule=unsupported-member-invocation-v1` |

Runtime binding must map the owner selector and line to exactly one authorized current Code Logic Document Version and one containing Source Anchor for the same Repository, ScanRun, and Profiled Revision. Zero/multiple/currentness-mismatched mappings refuse the observation before persistence. A successful mapping produces exactly one idempotent, nonterminal Uncertainty with `status=unresolved`, canonical `sub_status=unknown_target`, `reason_code=unsupported_kind`, and `observation_kind=file_storage`; it produces zero Edges and remains eligible for later full-V1 file/storage resolution or reclassification. Story 11.4 never assigns the human-terminal `wontfix` state. Its deterministic identity covers the profile fingerprint, observation identity, internal owner Document Version/Source Anchor identities, Profiled Revision, kind, selector, and safe locator.

## Runtime binding contract

```text
ExactResolutionRuntimeRequest
  systemId
  profileId/profileVersion/profileFingerprint
  bindings:
    alias / stableGitIdentity
    repositoryId / scanRunId / scanExecutionId
```

There must be exactly one binding per profile alias, no extras, two distinct Repositories, and one common visible System. Runtime managed identities are never copied into the embedded resource or textual evidence/diagnostic/log/exception fields. After validation, typed internal PK/FK columns may persist solely for ownership, referential integrity, deterministic idempotency, and authorized retrieval.

Before Git/source access, Web and Infrastructure both prove authenticated management access, current System visibility, Repository scope, ScanRun/ScanExecution ownership, and exact profile provenance. All binding failures return one bounded refusal family and write nothing.
