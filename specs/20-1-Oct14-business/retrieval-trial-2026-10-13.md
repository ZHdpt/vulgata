# First retrieval-enabled BL trial — October 13, 2026

## Accepted execution boundary

Implementation `1142575e6c672b48214d40b20fa20a1d54f9fd1a`; original retained database
`vulgata_oct_pilot_7e073703d96e4368a869ccace9bc827d`. The user approved a fresh verified
backup, purpose-only migration, then confirmed Chat worked normally and authorized
one paid scan. No recording-app deployment, restart or configuration change.

Before migration, a snapshot-consistent custom dump was restored into
`vulgata_oct_backupverify_20261013_ce86fc3d` and every table fingerprint matched the
exported snapshot. The protected persistent backup is under
`%LOCALAPPDATA%/Vulgata/OctoberBackups/pre-purpose-20261013-ce86fc3d/`.
Only `20261013000000_BusinessSourceReadApprovalPurpose` was pending/applied. Across
102 tables, only October migration history changed; all application data was unchanged.
The recording process remained the same. Backup/restore evidence:
`TestResults/business-original-migration/evidence.json`.

One BL-only existing-CL continuation used publication
`fff8ddfc-d8ae-480c-aadd-286f03c8f4d9`, the exact `userLogin.flw` / `finalize` root,
project/repository advisory overrides, retained resolution outcomes and from-start
CL-referenced retrieval. DeepSeek `deepseek-flash`, low reasoning, 393216 output-token
limit, 1200-second run deadline, 900-second synthesis bound, 26-request and
20-million-total-token ceilings. No CL regeneration or source rescan.

## Actual result

Run `d0f80660-33d3-45c6-9beb-c01e4594bb2a`, October 13 23:31–23:34 Asia/Shanghai,
ended with exit 1 after 162.32 seconds, without watchdog expiry. Five requests used
122570 input + 30853 output = **153423 known tokens**. No unknown usage was reported;
monetary cost was not supplied.

The model invoked ten source reads; **all returned Unavailable with no text**.
Each diagnostic charged 65547 examined bytes (conservative budget charge, not proof
of actual file I/O). Source grants/tool dispatch therefore do not establish
successful source retrieval. The precise lower-level failure remains undiagnosed.

Draft generation completed after three requests. Review failed after two requests
with `JsonInvalid`; no validated semantic verdict or new BL publication resulted.
This classification alone does not establish whether the cause was model output,
transport replay or host validation. Do not describe it as a proven bad-model answer.
Protected per-read/draft/review records remain in the owned run log directory.

Finalization `0b9f6ac2-5d9f-4920-a362-fc410e7f523c` committed a failed disposition;
exact Begin/Complete owner receipts confirm settlement. No recovery or automatic
retry is needed or authorized by this result. Read-only owner verification confirmed
all ten earlier publications retain exact document membership/content and the original
163 CL documents retain their digest. No new ordinary-reader publication exists to
qualify. The user's prior Chat confirmation is user-observed evidence, not a new
browser test by this task.

Evidence: `TestResults/business-retrieval-run/` (authorization, preflight, settings,
process, terminal report and encrypted diagnostics) and
`TestResults/business-retrieval-postrun/failed-verification.json`,
`diagnostics-final.log`. Next work is controlled diagnosis of source unavailability
and review failure before requesting/performing another paid attempt.
