# Dedicated JDT navigation correction contract

**Date:** 2026-10-02. **Status:** definition only; implementation/runtime paused.
Historical `navigation-tool.md` and all original source/ownership guards are preserved.
This explicit entry point is not fallback from failed mixed-root ownership checks.

## Application-only construction and opening

```csharp
public PreparedJavaNavigationContextFactory(
    PreparedJavaNavigationProfile profile, PreparedJdtHostPolicy trustedHostPolicy);
public Task<PreparedJavaNavigationOpenResult> OpenDedicatedAsync(
    PreparedToolWorkspaceLease workspaceLease,
    CancellationToken cancellationToken = default);
```

The host policy is existing Infrastructure data made visible to trusted application
construction; it does not become a Core/model tool parameter or confer execution
permission. Its first-PATH command selection remains fixed. Trusted application code
supplies a current JdtNavigation lease over one whole dedicated source-only root,
separate state and a qualified/explicitly authorized execution profile. No CodeGraph
coordinator/store/registry parameter is required here. Existing OpenAsync retains its
old guards and semantics. Models cannot select this entry point or authorize startup.

Application checks Opened and keeps both lifetimes. Existing
`PreparedJavaNavigationFunctions(context).Create()` returns exactly
`navigate_java_definition` and `navigate_java_references`; only relativePath, line,
character and maximumLocations are tool inputs. One-based line/character and location
bound 1..100 remain. Existing Core results/statuses and IAsyncDisposable are reused.
Dispose context/session after calls, then the borrowed root lease. Default Web remains
file-only until separately authorized application composition is implemented.

## Root, source, command and state binding

- Check exact current approval/purpose and stable no-follow root object before startup,
  navigation and disclosure. Map raw protocol URI before normalization; deny outside,
  traversing, symlink/device/UNC/query/fragment locations. No buffered disclosure after
  revocation, unavailable verifier, changed input, cancellation or disposal.
- Whole canonical root is LSP rootUri and workspaceFolder; no source mirror, copied
  repository or subproject masking. Source-only assignment excludes concurrent owned
  CodeGraph state preparation/mutation; mixed-cache use is not qualified by this path.
- External state is per-session data/configuration/temp/profile, separate and no-follow
  checked. Seed only required Equinox configuration from pinned installation; no source
  writes are requested. Remove only owned known state after confirmed session settlement.
- Resolve first supported JDT/Java candidates from running-host PATH; reject unsafe or
  ambiguous first matches without later fallback. Pin canonical file identity/digest
  and declared JDT server/bootstrap/version inputs; validate fixed interpreter/child
  chain/environment immediately before start. The human-approved first-PATH Python
  bootstrap into the inspected external JDT checkout is permitted (2026-10-05);
  resolve Python from host PATH and pin its actual chain before launch. Other
  unapproved chains and missing/stale PATH remain blockers, never guessed alternatives.
- Apply the existing Maven/Gradle import disabled/offline settings, autobuild false,
  updateBuildConfiguration disabled and no downloadSources/updateSnapshots. Keep
  dependency restoration disabled. These do not establish network isolation.

## Session and process ownership

Runtime owns partial launch/initialization and settles it before returning a session.
Returned context owns protocol/process lifetime; caller owns lease. Keep one active
session slot, bounded readiness/requests/shutdown, cancellation, serialized operations
and sanitized failure statuses. Unconfirmed cleanup is CleanupUnconfirmed/quarantined,
not successful cancellation. Stderr drain is bounded. Dispose the protocol, bounded
shutdown/exit where supported, and terminate only the owned process/tree if necessary;
record actual exit/cleanup outcome. Process/job tracking is a lifetime mechanism, not
filesystem/network enforcement. No cross-process cache arbitration/adoption is added.

## Human-reserved execution decision

The proposed Windows trusted-JDT-child profile is not authorized by this document,
planning instruction, configured paths or the CodeGraph exception. Human scope must
identify the pinned JDT/Java/bootstrap/interpreter chain, effective principal, owned
fixture/root/state and process permissions before a runtime qualification launch.
Direct execution retains that principal's filesystem reads/writes outside state and
network access. Source/footprint observation detects changes; it does not prevent them.
An unapproved or declined exception leaves launch closed; an enforcing OS profile
requires its own scope. No shared machine PATH/ACL/profile edit follows implicitly.

## Shared input/output

Ordinary readers keep the issued 18.1 read context and relative list/read/search.
Trusted adapters share exact application-selected approval/root and purpose lease;
18.3 selects CodeGraphPreparation and retains its existing positive physical owned-entry
view for ordinary-reader integration. No raw arbitrary projectPath is exposed to a
model. Isolated 18.3 fixture qualification and the new JDT fixture can be considered
separately; simultaneous mixed-root readiness remains unqualified and is not inferred.

## Required real-server proof

Owned Java fixture with a known declaration and caller: actual pinned server initialized
on whole root; both existing AIFunctions invoked and return expected relative file/line
anchors; source/config/hook markers unchanged, writable JDT state external; bounded
shutdown and owned process exit confirmed. Report server identity/settings/results and
limitations separately from controlled tests. Controlled protocol replay alone fails
this criterion; the old full integration story is not marked complete.


## Implementation entry checkpoint — 2026-10-02

The direct human `speckit-implement` instruction authorizes execution of new
T037–T047 phase by phase. It supersedes the correction-only implementation pause
for that scope, preserving all old [SKIP] items and the frozen historical runner.
Separate trusted-JDT/native execution authority remains pending. Read-only Phase 9
inspection found a first-PATH Python/external-checkout JDT shim and Java 17 while
its bootstrap requires Java 21+. No fallback, server launch or production edit was
attempted. T037 remains incomplete and Phase 10 has not started. See
[prerequisite evidence](../evidence/immediate-objective-phase-09-prerequisites.json).


## Bootstrap authorization update — 2026-10-05

Direct human instruction: "Allow this python bootstrap. You can safely assume python
is on the path". This authorizes the inspected first-PATH batch shim invoking Python
against `G:/source/repos/eclipse.jdt.ls/org.eclipse.jdt.ls.product/target/repository/bin/jdtls`.
Resolve Python from running-host PATH; pin the actual interpreter, bootstrap and server
inputs before launch. No fallback executable selector or host PATH change is introduced.
The earlier Java-17 observation is historical: current first PATH Java file metadata
is 27.0.0.0 and JAVA_HOME is unset. This is static evidence, not native qualification.
The separate trusted-JDT-child residual-rights exception and owned native execution
permission remain pending. T037 is incomplete; no server was launched. Earlier
evidence and skipped work remain preserved.


## Runtime authority — 2026-10-05

The human explicitly authorized running JDT-LS within this story after explanation
of the trusted-child residual filesystem/network rights. This covers the inspected
PATH Python bootstrap, pinned JDT/Java chain, owned dedicated Java fixture, external
state, function invocation and bounded shutdown. It grants no machine PATH/ACL edits
or provider/database/CodeGraph/default-Web execution. Fresh pins and current lease
checks remain required. Real native JDT qualification belongs in LocalTools, excluded
from basic SelfContained; controlled zero-process protocol tests retain their
SelfContained classification under testing.md. See the dated runtime authorization
evidence. The old runner and skipped items remain frozen.
