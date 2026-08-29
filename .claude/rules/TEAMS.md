## Team agent lifecycle

When a session runs a team of subagents:

- **Shut down each teammate as soon as its output is consumed** and no further tasks will be
  assigned to it — never batch shutdowns to the end.
- Keep an agent alive only when it is explicitly waiting on follow-up from the team lead.
