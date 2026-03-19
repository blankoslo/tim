---
name: log
description: Log today's hours
---

# Log Hours (tim CLI)

Use the `tim` CLI to log and manage work hours. Act immediately — never ask unnecessary questions.

## Core rule: just run it

When the user wants to log hours:
1. No project mentioned → run `tim write` (logs 7.5h to default project, today)
2. Project name mentioned → run `tim projects` to find the ID, then `tim write -p <projectId>`
3. Custom duration mentioned → append it: `tim write 3,5` or `tim write -p ANE1006 3,5`
4. Past date → check `tim write --help` for the date flag syntax
5. "Previous week" = Monday–Friday only, never weekends (Sat/Sun are never work days)
6. When logging multiple days, run each `tim write` command separately (not chained with &&) to avoid timeouts

After every hours operation (log, view, change), show the full week overview:
```bash
tim ls
```

## Key commands

```bash
# Log default hours (7.5h) on default project
tim write -y

# Log hours on a specific project
tim projects                    # find project IDs
tim write -y -p <projectId>     # log 7.5h on that project

# Log custom hours
tim write -y 3,5                # 3.5h on default project
tim write -y -p ANE1006 3,5     # 3.5h on specific project

# NOTE: Only `tim write` supports -y/--yes. Other commands are non-interactive.

# View logged hours
tim ls                          # current week
tim ls --range PreviousWeek
tim ls --range PreviousMonth
# Valid --range values: SingleDay, CurrentWeek, PreviousWeek, CurrentMonth, PreviousMonth

# Manage default project
tim get-default
tim set-default <projectId>
```
