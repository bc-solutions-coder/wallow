## Team Agent Lifecycle

Applies when a session runs a team of subagents. The repo's own agent definitions are in
`.claude/agents/` (`code-reviewer`, `csharp-developer`, `docfx-specialist`, `enterprise-architect`,
`dotnet-benchmark-designer`, `dotnet-concurrency-specialist`); any other agent name a session uses is
ad-hoc, comes from a user-level `~/.claude/agents/` definition outside this repo, or is contributed
by one of the plugins `.claude/settings.json` enables.

- **Shut down idle teammates immediately** after they deliver their final output, unless they are explicitly waiting for follow-up instructions from the team lead.
- Do NOT batch all shutdowns to the end. Each agent should be shut down as soon as its role is complete.
- An agent's role is complete when: its output has been consumed and no further tasks will be assigned to it.
- Example: a read-only research agent whose findings have been folded into the plan should be shut down when planning is done, not after implementation and verification finish.
