# Contract: Exact Cross-Repository Resolution Service

## Coordinator

```text
ResolveAfterPass1Async(
  ExactResolutionRuntimeRequest request,
  RepositoryAccessScope scope,
  CancellationToken cancellationToken)
  -> ExactResolutionOutcome
```

The deterministic coordinator accepts exactly the reviewed profile and two explicit runtime bindings. The reviewed JSON is the sole selector authority; the coordinator cannot accept or substitute implementation-time selectors. It does not enumerate a System, choose Repositories, queue a Scan, run a Worker Agent, call an LLM/MCP service, run Pass 2, or implement a general scheduler.

## Ordering

```text
validate reviewed profile/provenance
-> authorize common System and both Repositories
-> validate binding ownership and Pass1Completed boundaries
-> acquire isolated exact Git snapshots and prove Profiled Revision equality
-> extract/map the reviewed exact occurrence and unsupported observation
-> direction-qualified normalization/cardinality
-> atomic persistence
```

If either prepared execution is not yet at durable `Pass1Completed`, return `WaitingForPass1` before Git/source/write. A later `Completed` phase is accepted only when the same Pass 1 versions remain current.

## Reviewed exact occurrence extraction

For `account-to-channel-cust-update-session-v1` only:

- read `src/main/resources/disgram/in/cust/updateSession.xml` from the pinned provider snapshot;
- require the configured line/ordinal to be one root `trans` element with exactly one non-empty `name` attribute;
- preserve the decoded provider raw identifier and safe Repository-relative element locator, never XML/source text;
- map the exact implementation selector `src/main/java/cn/com/yitong/actions/domain/login/UpdateSessionOp.java` plus type symbol `cn.com.yitong.actions.domain.login.UpdateSessionOp` to one current provider Code Logic Document Version and Source Anchor;
- read `src/main/java/cn/com/yitong/actions/mobile/UpdateChannelSessionOp.java` from the pinned consumer snapshot;
- require line 45 ordinal 0 to fit the exact simple member-invocation token form `netTools.execute`, with one direct ordinary string literal at zero-based argument index 1;
- map that line to exactly one current consumer Code Logic Document Version and containing Source Anchor.

The bounded scanners still reject comments masquerading as calls, variables, computed/concatenated/template targets, malformed escapes/XML, DTD/external entities, extra/missing attributes, unconfigured files/symbols/lines/ordinals, traversal, reparse points, over-bound input, and zero/multiple Document/Source Anchor mappings.

## Match truth

For the reviewed occurrence:

1. consumer is `mbs_account`; provider is `mbs_channel`; candidates are never pooled across the reverse direction;
2. `custom-rpc-in-prefix-v1` applies decoding, Form C, and ASCII-edge trimming, then requires/removes exactly one provider `in/` prefix while leaving the consumer side unchanged;
3. the observed raw identifiers normalize from `cust/updateSession` and `in/cust/updateSession` to the same `cust/updateSession` value;
4. comparison is ordinal;
5. consumer/provider Repositories differ;
6. exactly one eligible provider and exactly one current mapping exist.

Only then is confidence exactly `1.0000` and one `REFERENCES` Edge permitted. LLM output, Pass 1 narrative, or System context cannot create or strengthen any field. A reverse direction requires a separate reviewed occurrence; no Repository gets a durable role classification.

## Canonical non-exact behavior

Durable Uncertainty strings are lowercase. `sub_status` is closed to the authoritative values `cross_repo`, `unknown_target`, and `ambiguous`; no other value is permitted. Detailed distinctions live in closed `reason_code` and `observation_kind` fields.

| Condition | Status / canonical sub_status | Closed detail | Durable behavior |
| --- | --- | --- | --- |
| Missing/duplicate/inaccessible binding, authorization failure, revision mismatch | none | bounded refusal code | no source inspection or write |
| Variable/nonliteral/disallowed consumer with safe owner anchor | `unresolved` / `unknown_target` | `variable_target` or `disallowed_invocation` | one idempotent Uncertainty; no Edge |
| Malformed/out-of-root provider evidence with safe owner anchor | `unresolved` / `unknown_target` | `malformed_provider` or `provider_out_of_root` | one idempotent Uncertainty; no Edge |
| Zero provider match | `unresolved` / `unknown_target` | `unmatched` | one idempotent Uncertainty; no Edge |
| Multiple provider matches | `unresolved` / `ambiguous` | `multiple_providers` | one idempotent Uncertainty; no Edge |
| Normalization mismatch | `unresolved` / `unknown_target` | `normalization_mismatch` | one idempotent Uncertainty; no Edge |

## Reviewed unsupported observation

The profile supplies `account-file-storage-oss-upload-v1` as a deterministic input, not a global kind name. The resolver:

1. authorizes and revision-binds `mbs_account` before reading;
2. maps `src/main/java/cn/com/yitong/actions/cust/UploadOcrToOssOp.java` plus exact type symbol to one current owner Code Logic Document Version and Source Anchor;
3. verifies line 66 ordinal 0 is the configured `ossClientService.simpleUploadToOSSBase64String` simple member invocation;
4. emits the safe configured locator only; and
5. applies one atomic, nonterminal decision with `status=unresolved`, `sub_status=unknown_target`, `reason_code=unsupported_kind`, `observation_kind=file_storage`.

The decision key includes the profile fingerprint, observation identity, internal owner Document Version/Source Anchor identities, Profiled Revision, kind, selector, and locator. Identical replay returns `AlreadyRecorded`; drift returns `Conflict`. Every path creates exactly zero Edges and zero exact Edge-evidence rows. The Uncertainty remains eligible for later full-V1 file/storage resolution or reclassification, and Story 11.4 cannot assign the human-terminal `wontfix` state. A missing, unauthorized, stale, zero/multiple, or out-of-anchor owner mapping writes nothing.

## Closed outcomes

```text
WaitingForPass1
Applied
AlreadyApplied
UncertaintyRecorded
AlreadyRecorded
Refused
Conflict
Cancelled
Failed
```

Returned metadata is bounded and authorization-filtered. Authorized success exposes the stable relationship identity only as opaque `relationshipIdentity` plus safe metadata, never raw Repository/ScanRun/Document Version/Source Anchor PK/FKs. Unauthorized resolution/retrieval exposes only the existing identity-free, metadata-free restricted marker. `Refused`/`Failed` never echo alias presence, endpoint, path, Git/provider diagnostic, source, target details, or internal identifiers.
