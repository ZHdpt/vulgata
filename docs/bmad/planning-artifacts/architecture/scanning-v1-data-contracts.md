# Vulgata V1 Scanning Data Contracts

**Date:** 2026-07-09
**Status:** Schema and service-contract handoff for scanning implementation
**Companion:** [scanning-v1-technical-plan.md](scanning-v1-technical-plan.md)

This document gives the minimum V1 table and contract shape implied by the July scanning plan. Names can be adjusted to match EF conventions, but the ownership boundaries and facts must remain intact.

## 1. Storage Ownership

Section ownership:

- Section 04 owns `communication_endpoints`, `dispatch_sites`, and `dispatch_site_endpoint_candidates`
- Section 07 owns Document Version storage, `document_sections`, flow-group and flow-trace persistence
- Section 08 consumes data through retrieval services and should not define new storage truth

Relational tables are authoritative for:

- graph traversal
- impact analysis
- authorization
- Source Anchoring
- Cross-Repository Resolution
- flow trace explanation
- retrieval section indexing

`content_jsonb` may render copies of facts for display, but it is not the only source of truth for graph or trace facts.

## 2. Document Identity

V1 distinguishes:

- Logical Document: stable semantic slot for a Code Unit or flow group
- Document Version: immutable generated snapshot

Generated edges point at Document Versions. Same-scan references may be staged against Logical Documents, but scan finalization must resolve them to generated versions or downgrade them to uncertainties.

Minimum Document Version additions:

```text
documents
  id
  logical_document_id
  repository_id
  system_id
  doc_type
  status
  title
  path
  content_jsonb
  content_markdown
  export_md_path
  schema_version
  is_current
  superseded_by_id
  scan_id
  resolution_outdated_at
  resolution_outdated_reason
  created_at
```

## 3. Source Anchoring

Source Anchoring records must preserve:

- repository ID
- scanned commit hash
- file path at scan time
- Code Unit ID
- line start and line end
- source hash or snippet hash
- Document Version ID that made the claim

These payloads support source citations even after file renames or line drift.

## 4. Graph Tables

`edges` remains the document graph table:

```text
edges
  id
  source_document_version_id
  target_document_version_id
  edge_type              -- REFERENCES, GENERATED_FROM
  protocol
  reference_kind
  resolution_kind
  candidate_group_id
  candidate_rank
  candidate_count
  confidence
  impact_traversal_eligible
  scan_id
  metadata_jsonb
  created_at
```

Rules:

- `REFERENCES` is a dependency edge between generated Document Versions
- `GENERATED_FROM` points from BL Document Version to CL Document Versions used in synthesis
- Pass 2 may not create base Code Logic `REFERENCES` edges from narrative alone
- BL-to-BL semantic references require validation and must declare whether they are impact-traversal eligible

## 5. Endpoint and Dispatch Tables

Provider-side endpoint:

```text
communication_endpoints
  id
  repository_id
  system_id
  protocol
  endpoint_identifier
  endpoint_identifier_normalized
  provider_document_version_id
  source_code_unit_id
  operation_name
  contract_symbol
  confidence
  scan_id
  metadata_jsonb
  created_at
```

Consumer-side abstract invocation:

```text
dispatch_sites
  id
  scan_id
  repository_id
  source_document_id
  source_document_version_id
  source_code_unit_id
  call_symbol
  receiver_kind
  receiver_type_hint
  dispatch_kind
  resolution_scope        -- local, caller_bound, config_bound, runtime_open
  protocol
  endpoint_identifier_hint
  contract_symbol
  operation_name
  normalization_context_jsonb
  confidence
  status                  -- unresolved, candidate, bound_in_trace, promoted_to_edge, wontfix
  created_at

dispatch_site_endpoint_candidates
  id
  dispatch_site_id
  communication_endpoint_id
  rank
  confidence
  evidence_jsonb
```

Trace-specific dispatch choices are stored in flow trace binding tables, not on the base dispatch-site row.

## 6. Uncertainties

Uncertainty rows represent known unknowns.

```text
uncertainties
  id
  document_version_id
  type
  status                  -- unresolved, resolved, wontfix
  sub_status              -- cross_repo, unknown_target, ambiguous
  description
  context_snippet
  endpoint_identifier
  endpoint_identifier_normalized
  target_repository_id
  resolved_by_document_version_id
  resolved_by_human_input
  created_at
  resolved_at
```

Unauthorized target details must be redacted at retrieval time.

## 7. Flow Trace Tables

Flow-group and trace evidence is persisted relationally.

```text
flow_groups
  id
  scan_id
  repository_id
  business_logic_document_id
  business_logic_document_version_id
  title
  entry_point_document_ids_jsonb
  grouping_score_jsonb
  trace_count
  confidence
  created_at

flow_traces
  id
  flow_group_id
  entry_point_document_id
  entry_point_document_version_id
  confidence_floor
  termination_reason
  resolution_state_jsonb
  created_at

flow_trace_steps
  id
  flow_trace_id
  step_order
  document_id
  document_version_id
  edge_id
  step_kind              -- reference, candidate_selection, dispatch_binding, endpoint_handoff, uncertainty, termination
  condition_text
  confidence
  evidence_jsonb

flow_trace_candidate_selections
  id
  flow_trace_id
  candidate_group_id
  selected_document_id
  selected_document_version_id
  status                 -- selected, unresolved, capped
  basis
  confidence

flow_trace_dispatch_bindings
  id
  flow_trace_id
  dispatch_site_id
  selected_document_id
  selected_document_version_id
  selected_endpoint_id
  status                 -- bound, candidate, unresolved
  basis
  confidence

flow_trace_endpoint_bindings
  id
  flow_trace_id
  communication_endpoint_id
  target_document_id
  target_document_version_id
  status                 -- resolved, unscanned, unauthorized, ambiguous
  basis
  confidence
```

Unresolved branches are rows with unresolved/capped status, not only JSON arrays.

## 8. Document Sections

`document_sections` is required for V1 retrieval.

```text
document_sections
  id
  document_id
  document_version_id
  section_id
  section_order
  section_kind
  heading
  content_markdown
  token_estimate
  source_anchor_refs_jsonb
  document_refs_jsonb
  uncertainty_refs_jsonb
  search_text
  search_vector
  created_at
```

Build rules:

- materialize from `content_jsonb.sections` whenever a Document Version is committed
- rebuild by replacing all rows for that Document Version
- use `search_vector` for English and identifier-heavy text
- use `search_text` plus trigram indexes for Chinese and mixed Chinese/English fallback

## 9. Worker Result Contracts

Pass 1 result:

```text
Pass1Result
  document_payload
  source_anchors
  document_sources
  references_edges
  dispatch_sites
  uncertainties
  entry_point_tags
  deferred_enrichment_dependencies
  verification_notes
  disagreements
  confidence_summary
```

Pass 1 refinement result:

```text
Pass1RefinementResult
  replacement_document_payload
  changed_graph_facts
  resolved_or_preserved_uncertainties
  source_anchors
  confidence_summary
```

Pass 2 result:

```text
Pass2Result
  business_logic_document_payload
  generated_from_edges
  semantic_reference_requests
  flow_group_metadata
  trace_evidence_refs
  unresolved_branch_notes
  confidence_summary
```

## 10. Prompt Templates

Minimum prompt resources:

```text
src/dotnet/Vulgata.Agents/Prompts/
  pass1-code-logic-worker.md
  pass1-refinement-worker.md
  pass2-business-flow-worker.md
  chat-retrieval-synthesis.md
```

The Pass 1 prompt must enforce the edge/dispatch/uncertainty rubric. The Pass 2 prompt must enforce graph-first synthesis and generation-time resolution state. Chat synthesis must preserve uncertainty, authorization redaction, and stale BL notices.

## 11. Retrieval Tool Contracts

Tool set:

```text
search_documents(scope, mode, query, filters)
get_document(document_version_id)
get_document_sections(document_version_id, section_filters)
expand_related_documents(seed_ids, expansion_policy)
get_uncertainty_summary(document_version_id)
get_endpoint_context(endpoint_id)
get_flow_trace_context(flow_group_id)
get_source_anchor_context(document_version_id)
get_scan_status(repository_id)
```

All tools must apply authorization and redaction before returning data to the Chat Agent.

## 12. Search Strategy

V1 search uses:

- PostgreSQL full-text where it works well
- `document_sections`
- exact symbol and endpoint matching
- business-term aliases
- trigram fallback for Chinese and mixed Chinese/English
- graph expansion

V1 does not depend on built-in PostgreSQL tokenization for Chinese semantic search.

## 13. Usage Accounting

```text
scan_llm_usage
  scan_id
  agent_type              -- pre_scan, pass1_worker, pass1_refinement, pass2_worker, chat
  call_count
  input_tokens
  output_tokens
  estimated_cost_units
```

Provider pricing remains configuration.

## 14. Index Suggestions

Initial indexes:

- `documents(repository_id, is_current, doc_type)`
- `documents(logical_document_id, is_current)`
- `edges(source_document_version_id, edge_type)`
- `edges(target_document_version_id, edge_type)`
- `edges(candidate_group_id)`
- `uncertainties(status, sub_status, target_repository_id)`
- `communication_endpoints(repository_id, protocol, endpoint_identifier_normalized)`
- `dispatch_sites(source_document_version_id)`
- `dispatch_site_endpoint_candidates(dispatch_site_id)`
- `flow_groups(scan_id, repository_id)`
- `flow_traces(flow_group_id)`
- `flow_trace_steps(flow_trace_id, step_order)`
- `flow_trace_dispatch_bindings(dispatch_site_id)`
- `document_sections(document_version_id, section_kind)`
- trigram index on `document_sections.search_text`

