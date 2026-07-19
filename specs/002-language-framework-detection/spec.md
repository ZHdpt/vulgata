# Feature Specification: Language and Framework Detection

**Feature Directory**: `specs/002-language-framework-detection`

**Created**: 2026-07-19

**Status**: Draft

**Input**: BMad Story `4-2-language-and-framework-detection`: continue Repository profiling by attributing each retained Code Unit's language from CodeGraph parser identity, detecting frameworks deterministically from Repository-local package evidence, and exposing auditable results without guesses or LLM classification.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Trust the Language Profile (Priority: P1)

An authorised SystemOwner profiles a Repository and receives a reliable language attribution for every retained Code Unit plus a Repository-level language breakdown based on distinct source files. This gives later Worker Agents accurate language context while letting the SystemOwner understand a mixed-language Repository at a glance.

**Why this priority**: Language is the minimum context required to interpret a Code Unit correctly, and every retained Code Unit must have an authoritative language before later scanning work can use it.

**Independent Test**: Profile a Repository fixture containing multiple Code Units per file across several parser-recognised languages, then verify that every retained Code Unit carries the normalised parser identity and that the overview counts each source file once per language.

**Acceptance Scenarios**:

1. **Given** Code Units have been extracted from a Repository, **When** deterministic profiling continues, **Then** every retained Code Unit has a normalised language attributed from the CodeGraph parser identity associated with its source file rather than from its file extension.
2. **Given** CodeGraph returns C#, Java, Python, JavaScript, TypeScript, Go, Kotlin, Swift, Rust, C, C++, or another language identifier, **When** the identifier is valid, **Then** the system retains the normalised identifier without requiring a feature-specific list entry for every language.
3. **Given** a Repository contains several Code Units in the same file and source files in multiple languages, **When** profiling completes, **Then** the Scan Run summary and Repository scan overview show each language with its distinct source-file count rather than its Code Unit count.

---

### User Story 2 - Use Auditable Framework Facts (Priority: P2)

An authorised SystemOwner can rely on framework facts attached to each applicable Code Unit because every fact comes from deterministic Repository package evidence, is limited to the Code Unit's own project or package scope, and identifies exactly what justified the detection.

**Why this priority**: Accurate framework context improves later code analysis, but an unsupported or mis-scoped framework claim would be more harmful than leaving the framework unknown.

**Independent Test**: Profile a Repository fixture with direct declarations, lock-backed transitive declarations, overlapping project scopes, duplicate evidence, unsupported packages, and ambiguous evidence; verify the exact framework facts, confidence levels, sources, scoping, and intentional absences.

**Acceptance Scenarios**:

1. **Given** a supported direct package declaration maps to a framework in the version-controlled framework catalog, **When** framework profiling runs, **Then** each Code Unit in the declaration's project or package scope receives one framework fact with `High` confidence and an exact Repository-relative detection source.
2. **Given** a committed lock or resolution artifact deterministically proves a supported transitive framework dependency, **When** framework profiling runs, **Then** each applicable Code Unit receives one framework fact with `Medium` confidence and the exact lock entry as its detection source.
3. **Given** the same framework is justified more than once for one Code Unit, **When** results are persisted, **Then** only one framework fact exists for that Code Unit and normalised framework name.
4. **Given** package evidence is missing, unsupported, ambiguous, outside a Code Unit's project or package scope, or resembles a framework only through a file or symbol name, **When** profiling completes, **Then** no framework fact is created from that evidence.

---

### User Story 3 - Audit Unknown Languages (Priority: P3)

An authorised SystemOwner can see which candidate source files were excluded because CodeGraph supplied no parser identity, without the system fabricating a Code Unit or silently losing the file.

**Why this priority**: Unknown source must not be dispatched with guessed context, but its exclusion must remain visible so gaps in Repository coverage can be reviewed.

**Independent Test**: Profile a Repository fixture containing candidate files with and without parser identities, then verify the retained Code Units, `UnknownLanguage` filtered outcomes, distinct unknown-file count, and Repository-relative audit paths.

**Acceptance Scenarios**:

1. **Given** a source candidate has no parser identity in the completed CodeGraph profile, **When** profiling reconciles provider results with candidate files, **Then** the candidate is recorded as `Filtered` with reason `UnknownLanguage` and no fabricated Code Unit, symbol, or line range is created.
2. **Given** one or more candidates are filtered for `UnknownLanguage`, **When** profiling completes, **Then** the Scan Run summary includes the distinct unknown-file count and Repository-relative paths for audit.
3. **Given** an unknown-language candidate is filtered, **When** later dispatch eligibility is evaluated, **Then** that candidate is not available as a dispatchable Code Unit.

---

### User Story 4 - Preserve Safe and Authorised Profiling (Priority: P4)

An authorised SystemOwner receives bounded, safe profiling results when individual package evidence is invalid, while provider-level failures remain atomic and users without existing Repository access gain no new visibility.

**Why this priority**: Profiling proprietary source and attacker-controlled manifests must not leak source contents or absolute host paths, weaken Repository access, or leave a misleading partial structural result.

**Independent Test**: Exercise malformed provider data, unsafe paths, invalid manifests, provider-level failure, and authorised versus unauthorised result access; verify bounded audit evidence, absence of invented facts, atomic structural persistence, and unchanged visibility outcomes.

**Acceptance Scenarios**:

1. **Given** one package or project manifest is invalid, unsafe, or unsupported, **When** profiling processes it, **Then** that individual evidence is isolated as a bounded audit outcome, no framework fact is invented from it, and independently valid evidence can still be processed.
2. **Given** provider data is malformed or the provider fails at the Repository level, **When** the result is processed, **Then** Story 4.1's atomic provider-derived structural-record behavior is preserved.
3. **Given** profiling encounters source-derived diagnostics or absolute host paths, **When** it records or returns an outcome, **Then** source contents and absolute host paths are omitted.
4. **Given** a user lacks the existing management or visibility permission for a Repository, **When** the user attempts to start profiling or inspect its language, framework, or audit results, **Then** the established access-constrained outcome is preserved and no new profiling information is exposed.
5. **Given** profiling information or an error is shown to a user, **When** user-visible text is rendered, **Then** the text is in Simplified Chinese.

### Edge Cases

- A file contains many Code Units: its language contributes once to the language breakdown, while each retained Code Unit receives the same normalised parser-derived language.
- A provider language identifier is recognised by CodeGraph but was not previously seen by Vulgata: it is normalised and retained rather than rejected solely because it is new.
- A candidate's file extension suggests one language while its parser identity reports another: the parser identity is authoritative.
- A candidate appears in local source discovery but has no matching parser identity: it becomes one `Filtered`/`UnknownLanguage` audit outcome and does not produce a fabricated Code Unit.
- One Code Unit is covered by nested or overlapping project/package scopes: only evidence demonstrably applicable to that Code Unit may create a framework fact, and duplicate facts are suppressed.
- The same framework appears as both direct and transitive evidence for a Code Unit: the resulting fact preserves the strongest deterministic confidence and an exact justifying source without duplication.
- A lock artifact exists without a corresponding deterministic relationship to the Code Unit's project/package scope: it does not justify a transitive framework fact.
- A manifest contains an unsupported format, malformed value, path escape, reparse-point traversal, excessive content, or excessive diagnostic detail: that evidence is bounded and isolated without executing it or inventing facts.
- A package or symbol name resembles a known framework but no supported package declaration or committed lock/resolution evidence justifies it: no framework fact is created.
- Repository visibility changes while profiling is in progress: completion results do not expose language, framework, or audit details to a user who no longer has access.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST continue the existing pre-scan profiling of exactly one Repository and MUST attribute a normalised language to every retained Code Unit.
- **FR-002**: A retained Code Unit's authoritative language MUST come from the CodeGraph parser identity associated with its source file. File extensions MUST NOT be used as the authoritative language signal.
- **FR-003**: The system MUST accept and normalise C#, Java, Python, JavaScript, TypeScript, Go, Kotlin, Swift, Rust, C, C++, and other valid language identifiers returned by the approved CodeGraph provider without requiring a feature-specific change for each identifier.
- **FR-004**: If a source candidate has no parser identity in the completed provider profile, the system MUST record exactly one terminal `Filtered` candidate outcome with reason `UnknownLanguage`, MUST NOT retain it as a dispatchable Code Unit, and MUST NOT fabricate a symbol, source range, or Code Unit for it.
- **FR-005**: The Scan Run summary MUST record a deterministic language breakdown in which each language's count is the number of distinct Repository source files attributed to that language, not the number of Code Units.
- **FR-006**: The authorised Repository scan overview MUST display every language in the Scan Run language breakdown and its distinct source-file count.
- **FR-007**: The Scan Run summary MUST record the distinct count and Repository-relative paths of source candidates filtered for `UnknownLanguage`.
- **FR-008**: The system MUST detect frameworks only from supported, committed Repository-local package or project manifests and lock or resolution artifacts through a closed, version-controlled framework catalog.
- **FR-009**: Framework detection MUST NOT call an LLM, query an external package service, execute install or restore operations, evaluate build targets or package scripts, or infer a framework from file names or framework-like source symbols.
- **FR-010**: A supported direct dependency declaration MUST create a framework fact with `High` confidence only for Code Units within the declaration's justified project or package scope.
- **FR-011**: A supported transitive dependency MUST create a framework fact with `Medium` confidence only when a committed lock or resolution artifact provides deterministic evidence and only for Code Units within the justified project or package scope.
- **FR-012**: Each persisted `CodeUnitFramework` fact MUST contain the Code Unit identity, a framework name normalised through the closed catalog, the confidence, and a Repository-relative detection source identifying the exact manifest property, package coordinate, or lock entry that justified the fact.
- **FR-013**: The system MUST prevent more than one framework fact for the same Code Unit and normalised framework name. When direct and transitive evidence both justify that fact, the persisted result MUST preserve `High` confidence.
- **FR-014**: Missing, unsupported, ambiguous, unscoped, or conflicting evidence MUST produce no framework fact. Later hypotheses or human review MAY address ambiguity in a later feature but MUST NOT overwrite deterministic facts produced by this feature.
- **FR-015**: Package evidence processing MUST remain non-executing and bounded. It MUST reject Repository-relative path escapes and reparse-point traversal, enforce bounded evidence size and diagnostic detail, and MUST NOT return or record source contents or absolute host paths.
- **FR-016**: Invalid individual manifest evidence MUST be isolated as a bounded audit outcome without inventing a framework fact, while independently valid Repository-local evidence remains eligible for processing.
- **FR-017**: Malformed provider data or provider-level failure MUST preserve Story 4.1's atomic provider-derived structural-record behavior: no partial provider-derived Code Unit, Code Unit Edge, or Unresolved Structural Relationship Candidate set is committed for the failed provider result, while already determined local candidate-filter outcomes remain auditable.
- **FR-018**: The feature MUST preserve existing `ManagementAccess` and Repository visibility rules for starting profiling and viewing language, framework, summary, and audit results. An unauthorised attempt MUST reveal no new profiling information.
- **FR-019**: All new user-visible labels, summaries, warnings, and errors introduced by this feature MUST be Simplified Chinese.
- **FR-020**: This feature MUST NOT introduce or change a Scan Coordinator, Orchestrator, Worker Agent, Worker Agent tool, dispatch or retry policy, Communication Pattern Catalog, Story 4.3 communication-type detection or question gate, document generation, or downstream scanning coverage promises.
- **FR-021**: Language and framework results from this feature MUST be deterministic facts available as later Worker Agent context; no LLM classification pass may create, change, or replace them.

### Key Entities

- **Code Unit**: An existing line-range-bounded structural source segment in a Repository. For this feature, every retained Code Unit has one normalised language sourced from its file's CodeGraph parser identity and may have zero or more Code Unit Framework facts.
- **CodeUnitFramework**: One auditable framework fact for a Code Unit, identified by the Code Unit and normalised framework name, with `High` or `Medium` confidence and the exact Repository-relative evidence source.
- **Framework Catalog Entry**: A version-controlled mapping from supported deterministic package evidence to one normalised framework name. It defines recognised facts and does not contain LLM hypotheses.
- **Package Evidence**: A bounded, non-executed declaration or committed resolution record from the Repository, including its Repository-relative location, exact property or coordinate, dependency relationship, and project/package scope.
- **Scan Run Summary**: The authorised outcome of profiling one Repository, including a language breakdown by distinct source-file count and the count and Repository-relative paths of `UnknownLanguage` candidates.
- **Filtered Candidate Outcome**: The existing auditable terminal result for a source candidate excluded from Code Unit retention; this feature uses reason `UnknownLanguage` when no parser identity exists.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Across acceptance fixtures containing all twelve named languages plus at least one additional valid provider language identifier, 100% of retained Code Units carry the expected normalised parser-derived language, and zero language values are justified solely by file extension.
- **SC-002**: For a mixed-language fixture with multiple Code Units per file, 100% of displayed language counts equal the independently verified distinct source-file counts, with no Code Unit counted as an additional file.
- **SC-003**: Across direct, lock-backed transitive, overlapping-scope, duplicate, unsupported, missing, and ambiguous package-evidence fixtures, 100% of expected framework facts have the correct normalised name, scope, confidence, and exact Repository-relative source; zero unsupported, ambiguous, or out-of-scope facts are created; and zero duplicate Code Unit/framework pairs exist.
- **SC-004**: Across unknown-language fixtures, 100% of candidates without parser identity are represented by one `Filtered`/`UnknownLanguage` audit outcome, the summary's distinct count and paths match the fixture exactly, and zero such candidates are retained as dispatchable Code Units.
- **SC-005**: Across profiling acceptance tests, the number of LLM classification calls, external package lookups, package install or restore executions, build-target evaluations, and package-script executions is zero.
- **SC-006**: Across malformed-provider, unsafe-path, invalid-manifest, and provider-level-failure tests, 100% of outcomes preserve the required bounded or atomic behavior, zero invented framework facts are created, and inspection of user-visible and recorded diagnostics finds zero source-content disclosures or absolute host paths.
- **SC-007**: In authorization acceptance tests, 100% of users without existing Repository management visibility receive the established access-constrained outcome and see zero language, framework, or unknown-file audit details.
- **SC-008**: In representative acceptance review, at least 90% of SystemOwner reviewers can correctly identify the languages present, their source-file counts, the evidence and confidence for a detected framework, and any unknown-language audit paths from the Repository scan overview without developer assistance.

## Assumptions

- Story 4.1 has completed Repository-local CodeGraph profiling, candidate discovery, filtered-outcome auditing, Scan Run persistence, and its atomic provider-failure behavior.
- CodeGraph parser identity is available for each projected structural source file through the approved, bounded provider contract; this feature does not depend on the provider's internal data store.
- The framework catalog begins with the supported package mappings selected during planning, but the specification requires only deterministic closed-catalog behavior and does not promise recognition of every ecosystem package.
- Direct evidence means an explicit dependency declaration in a committed project or package manifest. Transitive evidence is accepted only from a committed lock or resolution artifact that deterministically relates the dependency to the applicable scope.
- CodeGraph may return language identifiers beyond the V1 demo Repository languages. This feature records those valid identifiers, but it does not promise complete later Worker Agent scanning support for them.
- Existing Repository visibility, management authorization, Scan Run access, and Simplified-Chinese presentation remain the governing boundaries.
- Any later sampled LLM hypotheses or question-gate review remain separate from these deterministic facts and cannot overwrite them.
- Because this feature materially changes Scan parsing and filtering behavior, an independently accepted implementation plan requires the explicit planned-change authorization defined by `AGENTS.md` before task generation.

## Out of Scope

- Scan Coordinator, Orchestrator, Worker Agent, Worker Agent tool, superstep, dispatch, scheduling, concurrency, or retry behavior.
- LLM-based language or framework classification, external package-service lookup, package installation or restoration, build-target evaluation, and package-script execution.
- Story 4.3 communication-type detection, Communication Pattern Catalog changes, ambiguity questions, human approval gates, and user-entered framework overrides.
- Document generation, Code Logic Documents, Business Logic Documents, graph edges, Uncertainties, and Cross-Repository Resolution.
- Expanding downstream Worker Agent support to every language that CodeGraph can identify.
- A parallel profiling overview, new authorization model, or full Scan dashboard.

## Source Traceability

- Story 4.2 defines the seven acceptance-criteria groups, deterministic/no-LLM boundary, framework evidence confidence, unknown-language audit behavior, safety, and authorization: `docs/bmad/implementation-artifacts/4-2-language-and-framework-detection.md`.
- Epic 4 and Story 4.2 establish pre-scan language/framework profiling and the original user value: `docs/bmad/epics.md`.
- The current PRD establishes pre-scan profiling before Worker Agent dispatch and the V1 language-scope boundary: `docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md`.
- The July scanning architecture separates deterministic facts from later sampled LLM hypotheses and defines the downstream use of language/framework context: `docs/bmad/planning-artifacts/architecture/scanning-v1-technical-plan.md`.
- Story 4.1 supplies the existing Code Unit, filtered candidate, Scan Run, provider, and atomic-failure boundaries: `docs/bmad/implementation-artifacts/4-1-codegraph-integration-and-code-unit-extraction.md` and `specs/001-codegraph-extraction/spec.md`.
- The project constitution and `AGENTS.md` govern domain terminology, security, authorization, Chinese UI, project boundaries, and the required post-plan Scan-strategy approval.
- The canonical glossary defines Repository, Scan, Code Unit, Worker Agent, and Cross-Repository Resolution: `docs/UBIQUITOUS_LANGUAGE.md`.
- Specification authority was read at source commit `cfd1b2d45a1bb03d4c8f0f2b1271a06f42ce5531`.
