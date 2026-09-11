# GitHub issue tracking

Use the `gh` CLI for authorized GitHub issue operations and verify the target repository
from the assigned checkout before writing. The project's recorded repository is
`ZHdpt/vulgata`; repository-local PRD, epics and workflow artifacts remain their own
authorities rather than becoming current requirements merely because an issue exists.

- Read a requested ticket with `gh issue view <number> --comments`; select additional
  fields as needed using the installed CLI's help/JSON options.
- Creating issues, commenting, changing labels or closing work requires task authority.
  A read-only review or a skill recommendation to publish is not user authorization.
- For an authorized multi-line issue body, prefer a prepared UTF-8 body file and the
  CLI's `--body-file` option over shell-dependent heredoc/quoting tricks.
- Use [the triage mapping](triage-labels.md) for label vocabulary. Recheck current
  labels/issue state before mutation; never claim an external operation succeeded
  without its result.

When a task explicitly authorizes publishing to the issue tracker, create the requested
GitHub issue. When it asks to fetch a relevant ticket, read it without inferring write
permission.
