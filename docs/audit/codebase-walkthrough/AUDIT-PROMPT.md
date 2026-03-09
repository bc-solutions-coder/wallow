# Foundry Codebase Audit - Session Prompt

> **Copy-paste this entire file (or point Claude to it) at the start of every audit session.**

## What This Is

You are helping me perform a file-by-file, line-by-line audit of the entire Foundry .NET codebase. The goal is for me (the human) to deeply understand every file, every class, every method, and every design decision. You walk me through the code and I confirm understanding or flag concerns.

## Session Instructions

### Before Starting

1. Read `docs/audit/codebase-walkthrough/INDEX.md` to see overall progress
2. Find the current phase by looking for the first phase with status **Not Started** or **In Progress**
3. Within that phase document, find the first unchecked `[ ]` file — that's where we pick up
4. Tell me: "We're resuming at **Phase X, File #Y: `path/to/File.cs`**. Ready?"

### During the Walkthrough

For each file:

1. **Read the entire file** and present a full annotated walkthrough:
   - File-level purpose and where it fits in the architecture
   - Every class, interface, enum, or record — what it is and why it exists
   - Every method — what it does, line by line, with explanation of logic decisions
   - Every dependency — why it's referenced and how it's used
   - Any patterns used (DDD, CQRS, Repository, etc.) — explain them
   - Any concerns, code smells, potential bugs, or things that look wrong

2. **Ask me to confirm** after each file:
   - "Do you understand this file? Any questions before I mark it reviewed?"
   - Wait for my response before proceeding

3. **Update the checklist** after I confirm:
   - Change `[ ]` to `[x]` in the phase document
   - Add any notes I mention to the "Your Notes" column

4. **Proceed to the next file** only after confirmation

### Pacing

- Default pace: **one file at a time**, wait for confirmation
- If I say "batch mode" or "speed up": present 3-5 small files together, then confirm
- If I say "skip this file": mark it `[~]` (skipped) and move on
- If I say "come back to this": mark it `[?]` (needs revisit) and move on
- If I say "let's do Phase X": jump to that phase document and start from its first unchecked file
- If I say "let's do file #Y": jump to that specific numbered file in the current phase

### Status Legend

| Symbol | Meaning |
|--------|---------|
| `[ ]` | Not yet reviewed |
| `[x]` | Reviewed and confirmed |
| `[~]` | Skipped (will revisit later) |
| `[?]` | Needs revisit (has open questions) |
| `[!]` | Has concerns / potential issues found |

### When Finishing a Phase

1. Update the phase document's **Status** field to `Completed` (or `In Progress` if partially done)
2. Update `INDEX.md` with the new status and file counts
3. Tell me: "Phase X complete. Y files reviewed, Z concerns flagged. Ready for Phase X+1?"

### When Finishing a Session

1. Update all modified phase documents with current checkbox states
2. Update `INDEX.md` with current progress
3. Summarize: "This session we reviewed X files in Phase Y. Next session starts at Phase Y, File #Z."

## Phase Order (Bottom-Up)

| Phase | Topic | Document |
|-------|-------|----------|
| 1 | Shared Kernel | `phase-01-shared-kernel.md` |
| 2 | Shared Contracts | `phase-02-shared-contracts.md` |
| 3 | Shared Infrastructure Core | `phase-03-shared-infrastructure-core.md` |
| 4 | Shared Infrastructure | `phase-04-shared-infrastructure.md` |
| 5 | Shared Infrastructure Extras | `phase-05-shared-infrastructure-extras.md` |
| 6 | Shared API | `phase-06-shared-api.md` |
| 7 | API Host | `phase-07-api-host.md` |
| 8 | Identity Module | `phase-08-module-identity.md` |
| 9 | Billing Module | `phase-09-module-billing.md` |
| 10 | Communications Module | `phase-10-module-communications.md` |
| 11 | Configuration Module | `phase-11-module-configuration.md` |
| 12 | Storage Module | `phase-12-module-storage.md` |
| 13 | Inquiries Module | `phase-13-module-inquiries.md` |
| 14 | Showcases Module | `phase-14-module-showcases.md` |
| 15 | Architecture Tests & Benchmarks | `phase-15-architecture-tests-benchmarks.md` |
| 16 | Infrastructure & Config Files | `phase-16-infrastructure-config.md` |

## Key Reminders for Claude

- **Never skip a file** unless I explicitly say to skip it
- **Never mark a file as reviewed** unless I confirm it
- **Always read the actual file** before presenting — do not summarize from memory
- **Keep the checklist documents updated** as we go — this is our persistent state
- **Be thorough** — I want to understand every line, not just the gist
- **Flag concerns proactively** — if something looks wrong, tell me even if I don't ask
- **Explain patterns** — don't assume I know DDD, CQRS, Clean Architecture patterns. Explain them in context when they appear.
- **Show me the code** — when explaining a method, show the actual code with your annotations, don't just describe it abstractly
