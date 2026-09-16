# BMad review and codebase-design architecture adapter

This adapter combines `codebase-design` vocabulary with the applicable `bmad-review` adversarial, edge-case, verification-gap, and structural lenses. It produces independently classified, source-cited architecture candidates against one pinned clean boundary.

The independent auditor returns structured candidates. The active root validates citations and renders any temporary human report from that payload; neither upstream skill is modified or vendored.
