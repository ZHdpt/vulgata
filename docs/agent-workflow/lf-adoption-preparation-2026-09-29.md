# LF adoption pilot

Status: **four-path LF pilot applied and verified locally; downstream adoption pending**.
Prepared September 29, 2026; subsequently authorized by the user after Story 18.1
was merged. Scope is the four-path pilot below and its separate hygiene commit,
not repository-wide normalization, Git configuration changes or worker resumption.

## Decision and boundary

Use LF for ordinary maintained text, with explicit exceptions for byte-sensitive
content. Apply the allowlisted pilot at the integrated 18.1 checkpoint; defer
a repository-wide default until its affected paths and exceptions are qualified.
This pilot does **not** eliminate all warnings or repair workflow hash semantics.

Only the four allowlisted paths belong to this execution. The integration checkout
is `G:\source\repos\vulgata`, branch `oct14/main`, starting HEAD
`3632b2cc5e0752b66162e71ec4da95628fe3efcd` (Story 18.1 completion).
Existing uncommitted `AGENTS.md`, `docs/agents/project-state.md` and
`docs/agent-workflow/adoption-0.7.md` changes belong to the earlier delegation work;
they are accounted for, preserved byte-for-byte and excluded from this commit.
The earlier preparation baseline was `11e78b352929c2cc059cd157441faa413d5c3e01`.

## Observed reason for the change

- Root `.editorconfig` requests LF, but repository-local `core.autocrlf=true`.
- The inventory found 1,853 files with LF index blobs and CRLF working copies,
  plus mixed-ending files. These counts are an observation, not a conversion list.
- Existing attributes deliberately select LF, CRLF, or raw-byte handling for
  particular historical artifacts and resources. Nested fixture attributes exist.
- Workflow readers compare raw working-copy bytes, while committed checkpoint
  validation can read Git blobs. Representation changes can invalidate bindings;
  a conversion warning alone is not proof of such a failure.

Sources: [.editorconfig](../../.editorconfig), [.gitattributes](../../.gitattributes),
[raw evidence reader](../../.agents/skills/run-governed-delivery/scripts/workflow_runtime.py),
[checkpoint validator](../../.agents/skills/run-governed-delivery/scripts/validate_workflow.py).

## Four-path policy

The authorized policy adds only these exact-path rules to the existing root
attributes; all existing Story 18.1 and historical rules retain their order:

```gitattributes
/.gitattributes text eol=lf
/.editorconfig text eol=lf
/.gitignore text eol=lf
/docs/agent-workflow/lf-adoption-preparation-2026-09-29.md text eol=lf
```

These four paths are the initial write/conversion allowlist. Preserve all existing
attribute rules and their ordering. Root `.editorconfig` already requests LF;
its settings need no semantic change. `README.md` is a later optional candidate:
it currently has mixed endings and needs individual inspection before inclusion.

Files outside this allowlist keep their current attributes, index blobs,
working-copy bytes and evidence bindings. Do not add broad `* text=auto eol=lf`,
extension-wide rules, or exclusion rules using `-text`, `!text` or `!eol` in this
pilot. In particular, adding `-text` to a file with LF in Git and CRLF on disk does
not preserve both representations. Do not change global, repository or worktree
Git configuration to silence warnings.

New ordinary files may be added to a later explicit LF policy before their first
hash/review. Existing accepted artifacts need their owner's qualified migration;
they cannot be enrolled just because their extension is Markdown or JSON.

## Exclusions: leave untouched

| Category / concrete paths | Why it is excluded |
| --- | --- |
| All existing `specs/**`, including each worker's plans, reviews, tasks and evidence | Existing review/approval/checkpoint bindings must remain reproducible. |
| `docs/bmad/history/**`, existing implementation-artifact evidence and raw logs | Preserve historical bytes and outcomes, including existing LF/CRLF exceptions. |
| `tests/Vulgata.Tests/Fixtures/**` and byte/newline-sensitive test inputs | Manifests pin file hashes; some inputs intentionally test line-ending differences. Preserve nested attributes. |
| `src/dotnet/Vulgata.Infrastructure/CrossRepository/Profiles/demo-resolution-profile.v1.json` | The profile loader fingerprints original JSON bytes; consumers compare that identity. |
| `src/dotnet/Vulgata.Infrastructure/CommunicationDetection/Catalog/communication-pattern-catalog.v1.json` | Catalog hashes are referenced by Prepared Scan manifests. |
| Embedded prompts/resources, including `src/dotnet/Vulgata.Agents/Prompts/**` | Preserve existing LF pins and any accepted resource identities. |
| Installed assets under `.agents/**`, `.codex/**`, `.governed-delivery/**`, `.specify/**`, `_bmad/**`, plus their lock/manifests | EOL changes require upstream-qualified installation handling, not a local formatting sweep. |
| Remaining product/test/build files, binaries, secrets, generated output and the three pending delegation files | Not part of the initial allowlist; no incidental source, encoding or resource changes. |

Exclusion means **no mutation**, not new Git attributes. Disable save-all,
format-all and bulk whitespace cleanup for this operation; `.editorconfig` alone
does not protect an excluded file from editor changes.

Evidence for resource/fixture sensitivity:
[profile loader](../../src/dotnet/Vulgata.Infrastructure/CrossRepository/ExactResolutionProfileCatalog.cs),
[catalog loader](../../src/dotnet/Vulgata.Infrastructure/CommunicationDetection/CommunicationCatalogLoader.cs),
[fixture manifest](../../tests/Vulgata.Tests/Fixtures/AnalysisRuntime/fixture-manifest.json).

## Coordination with the four workers

These are planning-time chat reports, except for the locally verified 18.1
completion. They are not direct filesystem inspections or permission to operate
another worktree. Each worker must refresh its own boundary before adopting.

| Worker / stable chat ID | Reported boundary | Adoption condition |
| --- | --- | --- |
| Worker 18.1 / `01a0e60f-6a57-73e3-a9cb-ef13292cac62` | Completion `3632b2cc` integrated; targeted completion validation passes | Preserve the completion commit and its exact evidence; do not amend it. |
| Worker 18.2 / `01a0eae8-edeb-7fb3-874e-35ce36ee4a92` | Accepted plan, clean commit `6d6fa291` | Owner adopts the qualified baseline before tasks/implementation; retains the plan-only stop until authorized. |
| Worker 18.3 / `01a0eae9-657d-7f90-80e0-6bf7d83a5c7c` | Accepted plan/review still uncommitted | Owner first preserves the exact work in a valid checkpoint, then adopts. |
| Worker 18.4 / `01a0e728-b7cc-78f1-8a3d-d0d49c7dfde2` | Clean detached checkpoint `dd481724`, planning only | Owner establishes the workflow-supported continuation route; preserve existing lineage, evidence and dependency stop. |

The parallel scheduler proposes the window and integration owner; the monitor
relays status. Neither role grants new execution authority. Only the assigned
owner changes a worker's checkout or story records. Waiting is not authorization
to rewrite accepted files. No communications, worker resumes or merges are
authorized by this preparation document.

## Execution and downstream-adoption checklist

1. Obtain explicit cutover authority. Confirm 18.1's completion is integrated and
   verified; if it is actually blocked by EOLs first, handle only that proven
   boundary separately rather than expanding this conversion.
2. Start from a clean, named integration baseline after unrelated work is safely
   accounted for. Record the full baseline SHA and exact allowlist. Recheck
   attributes, EOLs, index/working-copy differences and any new evidence references.
3. Save before-byte fingerprints for protected paths in the owned checkout. Apply
   only the four candidate rules and CRLF-to-LF conversion on allowlisted text.
   Keep encoding/BOM, trailing spaces, final-newline presence and content unchanged;
   investigate lone CR rather than silently deleting it. Do not renormalize `.`.
4. Verify each converted file differs only by the declared CRLF-to-LF transform.
   Review the intended `.gitattributes` rule addition separately. Confirm every
   protected path is byte-identical and its effective attributes are unchanged.
5. Check `git diff --check` and effective attributes. Use an owned temporary
   checkout/index to verify the candidate's round trip without touching active
   worktrees. Expected allowlisted text is LF both in Git and after checkout;
   excluded paths must show no new conversion relative to the pre-cutover baseline.
6. Make one separate, explicit-path hygiene commit and pin its full SHA. Validate
   the final committed round trip, including historical completed evidence from
   its own checkpoint. Do not rewrite historical hashes or claim an unrelated
   pre-existing validator finding was fixed. This remains metadata verification,
   not a product/LLM qualification claim.
7. Each worker owner preserves its current checkpoint, adopts the exact qualified
   baseline through an authorized ancestry-preserving route, and validates its
   current state and accepted artifact bindings before tasks or implementation.
   A mismatch requires diagnosis, not blind hash replacement or automatic re-review
   of the entire story. Retain all current approval and stop conditions.

No product build is required solely to validate this documentation/metadata-only
pilot. If later scope includes source or build inputs, use the current
[testing guidance](../agents/testing.md), including `eng/test.ps1`, and the affected
byte-sensitive checks. Do not mix that expansion into this pilot silently.

## Completion and failure conditions

Preparation completed before execution without changing existing files or Git
configuration. The execution record below distinguishes local policy adoption
from adoption in the other workers' checkouts.

Cutover is complete only when the narrow commit and round trip are verified and
each adopting worker reports a valid current boundary. New story starts use that
qualified baseline. Any unlisted change, protected-byte drift or unresolved hash
mismatch stops the affected adoption; retain exact pre-images and return to its
owner. If rollback is needed, make an authorized scoped corrective/revert commit;
do not reset other work or rewrite completed checkpoints.

## Execution record

- Starting branch/commit: `oct14/main` at
  `3632b2cc5e0752b66162e71ec4da95628fe3efcd`; no staged changes.
- Protected scope: 3,109 tracked paths outside the allowlist, including all three
  pending delegation edits. No product code, story state, evidence or runtime changed.
- Baseline targeted validators: Story 18.1 and historical Story 13.1 both returned
  `ok: true`, with no errors. No aggregate historical validation or product suite
  is claimed for this metadata-only change.
- All four paths contain LF-only text. `.editorconfig` and `.gitignore` changed
  only by CRLF-to-LF conversion; their Git blobs were already LF and stay unchanged.
- `.gitattributes` adds four exact-path rules and a comment. Removing that prefix
  yields the original attribute file after CRLF-to-LF conversion, with all existing
  rules and their order preserved.
- All 3,109 protected paths retained their bytes/symlink targets and effective
  `text`, `eol` and `whitespace` attributes. The pending delegation edits remain
  outside the staged boundary. Git configuration is unchanged.
- An owned temporary `git checkout-index` export verified all four files match
  both their staged blobs and working-copy bytes, with no CR bytes. Only
  `.gitattributes` and this new record have staged content changes.
- Targeted Story 18.1 and historical Story 13.1 validators also passed after the
  change. Links and whitespace checks passed. No product build/test suite, provider
  call, database access or source normalization was needed or performed.
- This record and `.gitattributes` are committed together as the LF hygiene change;
  the commit containing this record identifies the qualified policy baseline.
- Other worktrees: untouched; each owner must adopt the eventual qualified commit
  under the existing story, review and authorization conditions.
