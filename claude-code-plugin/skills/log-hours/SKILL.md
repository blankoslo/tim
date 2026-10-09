---
name: log-hours
description: Log, view, and manage work hours using the 'tim' CLI tool. Use this skill whenever the user wants to register hours, check what they've logged, log time on a project, track hours for a client, or do anything related to time tracking — even if they just say "log hours", "føre timer", "I worked today", or mention a project name alongside time.
allowed-tools: Bash(tim *)
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
6. When logging multiple days, chaining with `&&` is fine: `tim write -y -d 01.04 && tim write -y -d 02.04`

After every write operation (logging hours, updating entries), always run `tim ls` and display the result as a table so the user can see the full week overview:
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
tim emp ls -c "Client Name" --ids | tim ls -

# Project hours for a client
tim projects -c "Client Name" --ids | tim projects time -r PreviousMonth -

# Download CSV for invoicing
tim projects -c "Client Name" --ids | tim reports project-employee-hours -r previousmonth -
```

## Raw API access (read-only exploration only)

`tim curl` is strictly for reading and exploring data. Never use it to create, update, or delete entries. Always try native `tim` commands first — only reach for `tim curl` when no native command can get the information you need.

Never call floq-db's RPC functions (`/rpc/...`) — they are being retired. Prefer a floq-platform route (`--platform`); fall back to a PostgREST table query only when no route answers the question.

```bash
# floq-platform routes (GET only) — see the table below
tim curl --platform '/staffing/billable-customers?from=2025-11-01&to=2025-11-30'
tim curl --platform '/reports/employee-days?employeeIds=42&from=2025-11-03&to=2025-11-07'

# Fetch the PostgREST OpenAPI spec — use this to discover tables and columns
tim curl '/'

# Direct PostgREST table queries (GET/read only)
tim curl '/employees?select=first_name,last_name'
```

When the user asks for something `tim` doesn't support natively: check the floq-platform routes below first, then fall back to exploring the PostgREST schema with `tim curl '/'`. If the task requires writing data, tell the user that this operation is not supported.

### floq-platform routes

Dates are `yyyy-MM-dd`, and `from`/`to` are both inclusive. Several ids are comma-separated (`employeeIds=7,9`). Responses are camelCase JSON.

| Route | Answers |
|-------|---------|
| `/employees/me` | The logged-in employee, roles included |
| `/employees?employed=true` | Everyone still employed (omit `employed` for former employees too) |
| `/projects?active=true&customerId=ANE` | Projects, filtered |
| `/projects/customers` | Every customer |
| `/staffing/billable-customers?from&to` | Which customers each employee is staffed on billable work for |
| `/staffing/days?employeeIds&from&to` | Staffing per day, with percentage |
| `/reports/employee-days?employeeIds&from&to` | Per employee, day and code: minutes logged, % staffed, % marked off |
| `/timesheet/hours?employeeId&from&to` | One employee's hours per date and code |
| `/timesheet/absence?employeeIds&from&to` | Days marked off, with percentage |
| `/timesheet/holidays` | Norwegian public holidays |
| `/timesheet/flex-balance?employeeId&asOf` | Flexitid balance in hours, through `asOf` |
| `/timesheet/vacation-balance?employeeId&year` | Vacation days earned, carried over, taken and left |
| `/reports/time-tracking-status?from&to` | What each employee logged against what was available |
| `/reports/billing-degree/achieved?from&to` | Faktureringsgrad per ISO week, per employee and company |
| `/reports/weekly-project-hours?from&to` | Hours per project per ISO week |
| `/reports/customer-hours?customerId&from&to` | Hours for everyone who worked on a customer's projects |

### PostgREST tables/views (fallback)

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

## Error handling

- `tim` not found → tell user to install it via Homebrew:
  ```bash
  brew tap blankoslo/tools git@github.com:blankoslo/homebrew-tools.git
  brew install blankoslo/tools/tim
  ```
- Unknown project name → list with `tim projects` and ask user to pick
- Auth errors → run `tim login`
- Always check command output to confirm the entry was recorded
