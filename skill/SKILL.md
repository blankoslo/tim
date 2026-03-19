---
name: log-hours
description: Log, view, and manage work hours using the 'tim' CLI tool. Use this skill whenever the user wants to register hours, check what they've logged, log time on a project, track hours for a client, or do anything related to time tracking — even if they just say "log hours", "føre timer", "I worked today", or mention a project name alongside time.
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

## Viewing and reporting

```bash
# Week overview for a client
tim emp ls -c "Client Name" --ids | tim ls

# Project hours for a client
tim projects -c "Client Name" --ids | tim projects time -r PreviousMonth

# Download CSV for invoicing
tim projects -c "Client Name" --ids | tim reports project-employee-hours -r previousmonth
```

## Raw API access

**Always prefer native `tim` commands** (`tim ls`, `tim write`, `tim projects`, `tim emp`, `tim reports`). Only use `tim curl` as a last resort when no native command covers the task.

```bash
# Fetch the OpenAPI spec — use this to discover available tables, columns, and RPC functions
tim curl '/'

# Direct PostgREST queries
tim curl '/employees?select=first_name,last_name'
tim curl -x post '/rpc/employees_on_projects' --data '{"from_date":"2025-11-01","to_date":"2025-11-30"}'
```

When the user asks for something `tim` doesn't support natively: fetch `tim curl '/'` first to explore the schema, then construct the appropriate query.

### Available tables/views

| Name | Description |
|------|-------------|
| `absence` | Employee absence entries |
| `absence_reasons` | Valid absence reason types |
| `available_projects` | Projects available for time entry |
| `customers` | Customers/clients |
| `employees` | Employee records |
| `expense` | Expense entries |
| `holidays` | Public holidays |
| `projects` | All projects |
| `staffing` | Staffing allocations |
| `staffing_per_week` | Weekly staffing view |
| `time_entry` | Raw time entries |
| `timelock_events` | Hour lock events |
| `vacation_days` | Vacation day records |
| `vacation_days_by_year` | Vacation days grouped by year |
| `vacation_days_earnt` | Earned vacation days |
| `vacation_days_spent` | Spent vacation days |
| `write_off` | Write-off entries |

### Useful RPC functions

| Function | Description |
|----------|-------------|
| `employees_on_projects` | Employees on projects in a date range |
| `accumulated_time_tracking` | Accumulated time tracking per employee |
| `hours_per_employee` | Hours logged per employee |
| `hours_per_project` | Hours logged per project |
| `time_tracking_status` | Time tracking status per employee |
| `time_tracking_status_by_week` | Weekly time tracking status |
| `unregistered_days` | Days with missing time entries |
| `get_periodic_report` | Periodic hours report |
| `projects_info_for_employee_in_period` | Projects for an employee in a period |
| `who_am_i` | Returns the currently authenticated employee |
| `kpi_fg` | Billable rate KPI |
| `staffing_and_billing_overview` | Staffing vs billing overview |

## Error handling

- `tim` not found → tell user to install it via Homebrew:
  ```bash
  brew tap blankoslo/tools git@github.com:blankoslo/homebrew-tools.git
  brew install blankoslo/tools/tim
  ```
- Unknown project name → list with `tim projects` and ask user to pick
- Auth errors → run `tim login`
- Always check command output to confirm the entry was recorded
