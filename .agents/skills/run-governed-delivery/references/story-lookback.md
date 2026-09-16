# Story Lookback and Project Lessons

Read at context/replan for relevant lesson retrieval, and after clean convergence for root-owned closure. This is one bounded completion check in the root's existing turn, without a new worker, judge, quota, or routine approval.

## Read Relevant Lessons

Use `paths.project_lessons` from `.governed-delivery/config.toml`, defaulting to `docs/project-lessons.md`. Search by affected component, path, failure mode, or decision. Read matching entries and only the source needed to test applicability; do not load every story history. Supply relevant guidance to author/reviewer. The author's normal self-review names the precaution or evidence that the entry is obsolete. Lessons never override current authorities or automatically widen testing/exploration.

A missing default file means no recorded lessons. Before writing, validate a dedicated repository-relative file target, resolved containment and existing-file identity; reject traversal, absolute paths, directories, and symlink/hardlink aliases to state, definition, configuration, or canonical authorities. Unreadable/conflicting content requires explicit handling; never silently replace it. The installer does not own consumer lessons or project authorities.

## Complete the Lookback

After clean convergence and before quality review, write exactly one real `## Story Lookback` section in the existing lifecycle development record. Record whether reusable learning exists, whether approved project-document deltas are synchronized, and any genuinely nonblocking follow-up. `No new reusable lesson` is valid; do not create an empty lesson file. Unresolved authority contradictions or required unsynchronized decisions prevent a current lookback and completion.

Known significant document changes should be reconciled when discovered. If lookback reveals changed convergence inputs or an unsatisfied authority, return to the affected owner and revalidate. Ordinary record/lesson updates enter the subsequently pinned quality candidate. Later meaningful failures or changed inputs/outputs refresh relevant lookback evidence; successful commands and bookkeeping do not trigger a full rerun. Never slip project-document changes after reviewed final verification.

## Curate Active Guidance

Promote only verified, reusable, nonduplicative guidance with a recognizable future condition and concrete minimal prevention. Keep transient failures, speculation, raw attempts, and one-off fixes in existing story/evidence records. Prefer an existing regression test or deterministic check where it prevents recurrence.

Use ordinary Markdown headings and concise prose for applicability, evidenced cause/source, minimum preventive action, and relevant version or last verification point. No lesson IDs, front matter, counters, or mandatory entry count. Amend the relevant entry rather than rewriting the file; merge duplicates and supersede conflicting obsolete advice, preserving evidence in Git/story records. Current facts go to existing context/README; approved intent/design goes to its canonical PRD/architecture/ADR.

Treat 8 KiB UTF-8 as a soft active-file target, never a gate or truncation rule. Review the touched topic normally; consolidate more broadly only above that target or after substantial underlying design change. Preserve important exceptions and necessary nonredundant guidance even above the target, briefly explaining why and retrieving selectively. Age alone is not obsolescence.

## Synchronize Authority

Root records and updates only affected canonical sections for already-approved decisions, without duplicate approval. New product guarantees, architecture decisions, cross-story scope, or contradictions use the existing informed decision/course-correction route before dependent changes. Never rewrite PRD to excuse nonconforming code. Unimplemented improvements remain follow-up work. The definition author owns only spec/plan/tasks; root owns lessons and canonical project updates.

## Proof and Freshness

Write ordinary immutable `kind: analysis`, `stage: lookback` evidence under the story's existing evidence convention. Bind the current convergence/product-definition boundary by path/hash, development-record path, `section_sha256` of its real Story Lookback section, `record_content_sha256` of its non-bookkeeping content, touched lesson/project outputs, relevant accepted decisions, disposition, and unresolved items. State stores only the evidence descriptor/status.

The validator extracts exactly one genuine level-two Story Lookback section through block-aware Markdown parsing; fenced/commented examples or duplicate headings do not satisfy it. End at the next peer/higher heading. Put root lifecycle/checkpoint metadata in one reserved `## Workflow Bookkeeping` section, outside Story Lookback; keep product intent, acceptance, decisions, and lessons out of this reserved section. A genuine standalone lifecycle `Status:` line may also change mechanically. `record_content_projection` excludes only that reserved section and the recognized lifecycle-status line, preserving other content. Hash its returned UTF-8 bytes as `record_content_sha256`; duplicate real bookkeeping headings are invalid. Fence/comment examples never become exclusions.

Quality owns and pins the lookback proof, development record, and its outputs. Later record byte changes can pass only when both its reviewed non-bookkeeping content and Story Lookback section still match that pinned proof. This allows routine status/checkpoint maintenance without exempting unrelated story-body edits. Source/proof/state references remain nonrecursive.

Before quality and completion, validate current source inputs, section, and output hashes. Quality includes those changes; proof does not itself grant acceptance. Later meaningful source/definition/authority or proof/output changes invalidate affected review. After binding, audit proof and all its inputs/outputs from the completion commit so legitimate future lesson edits do not invalidate finished stories. Historical 3.0 completion retains its original proof requirements.

The optional epic retrospective consumes story lookbacks for cross-story patterns. It remains separate from story completion and the exact-commit epic architecture gate.
