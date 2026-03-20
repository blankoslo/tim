# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build app/Tim.csproj

# Run (replace [args] with CLI args)
dotnet run --project app/Tim.csproj -- [args]

# Local publish
./publish-local.sh
```

# Releasing

1 Check the latest released tag in GitHub:

```bash
 gh release list --repo blankoslo/tim --exclude-drafts --limit 1 --json tagName -q '.[0].tagName' | cat
 ```

2 Create a new tag (bump major, minor or patch) and push it the tag:

Ex: If the latest tag is 0.5.2 and we bump patch:
```bash
git tag 0.5.3 && git push origin 0.5.3
```

This will trigger the 'Publish Release' action and create a GitHub release when done.

**Releasing to Homebrew**

The homebrew deploys are dependent on github release assets (tar/zips in the release), so it will need to wait for that to be ready. When they are:

3 Update the `tim-brew` formula with the latest release:
3.1 Run the `updateformula.sh` in the `tim-brew` repo.
3.2 Commit and push to GitHub.


4 Final step: Add a pretty, human friendly description to the GitHub release of the changes. The GitHub workflow creates a draft release tied to the tag that is available for publish. Update this, and publish it.



## Architecture

**tim** is a .NET 10 AOT-compiled CLI for time tracking (timeføring) at Blank Oslo, built on two frameworks:
- **ConsoleAppFramework** (Cysharp) — command routing and DI
- **Spectre.Console** — terminal output/formatting

### Structure

- `app/Program.cs` — entry point; sets Norwegian locale and starts ConsoleAppFramework
- `app/Commands/` — one class per command group, subcommands as separate files (e.g. `Time.cs`, `Time.List.cs`, `Time.Write.cs`)
- `app/Helpers/Floq/` — HTTP clients for Floq/PostgREST API (`FloqClient.cs`, `FloqReportsApiClient.cs`)
- `app/Helpers/Auth/` — JWT session management via .NET user secrets (`UserSecretsId: tim-1337`)
- `app/GlobalUsings.cs` — shared usings across the project

### Key patterns

- Commands use `[RegisterCommands]` + `[Command("name")]` attributes
- Auth is handled via `[ConsoleAppFilter<AuthenticationFilter>]` on protected commands
- Stdin piping is supported via `AddStdinToContext` filter — employee IDs can be piped between commands
- `GlobalState` carries the session token through `ConsoleAppContext.State`
- Time values use Norwegian comma decimal separator (e.g. `3,5` not `3.5`)

### Adding new commands

Add new command files to `app/Commands/`. For subcommands, create a parent class file (e.g. `Foo.cs`) and child files (e.g. `Foo.Bar.cs`), then register the child in `Tim.csproj` with `<DependentUpon>Foo.cs</DependentUpon>`. Reuse helpers from `app/Helpers/`.

### External API

Floq is a PostgREST API. Use `tim curl '<path>'` to make authenticated raw requests. Swagger spec is at `https://api-prod.floq.no/`.

Framework docs:
- ConsoleAppFramework: https://raw.githubusercontent.com/Cysharp/ConsoleAppFramework/refs/heads/master/ReadMe.md
- Spectre.Console: https://spectreconsole.net/
