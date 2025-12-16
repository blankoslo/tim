# Copilot Instructions for `tim` CLI Project

## Project Overview
- **tim** is a cross-platform CLI for time tracking (timeføring) used at Blank Oslo.
- Written in C# (.NET 10), organized as a single app with subcommands in `app/Commands/`.
- Integrates with external APIs (notably Floq via PostgREST) and supports piping/Unix workflows.

## Key Architecture & Patterns
- **Command Pattern:** Each CLI command is a class in `app/Commands/`, often with subcommands as separate files (e.g., `Time.List.cs`, `Projects.Time.cs`).
- **Helpers:** Shared logic is in `app/Helpers/` (e.g., formatting, authentication, API clients).
- **Authentication:** Managed via `Helpers/Auth/` (JWT, secrets, etc.).
- **API Integration:** Floq API access is via `Helpers/Floq/` clients. Use `tim curl` for raw API calls.
- **Global Usings:** Common usings are centralized in `GlobalUsings.cs`.

## Developer Workflows
- **Build:**
  - Standard: `dotnet build app/Tim.csproj`
  - Multi-target: Builds for .NET 10 (see `bin/` and `obj/` structure)
- **Run:**
  - `dotnet run --project app/Tim.csproj -- [args]`
  
- **Test:**
Run the cli via dotnet run to test commands:
Test the Employee List command:
  - `dotnet run --proejct app/Tim.csrpoj -- emp ls
- **Publish:**
  - If asked, use`publish-local.sh` for local deployment.
- **Install:**
  - Homebrew: `brew install blankoslo/tools/tim`
  - .NET Tool: `dotnet tool install --global BlankDev.Tools.Tim --source "github"`

## Project Conventions
- **Norwegian/English mix:** Code and docs may use both languages.
- **Decimal separator:** Time values use comma (e.g., `tim write 3,5`).
- **Pipe-friendly:** Many commands are designed for Unix-style piping.
- **Minimal browser use:** CLI-first philosophy; avoid browser-based flows.
- **API Credentials:** Managed via user secrets/JWT, fetched via OIDC flow.

## Integration Points
- **Floq API:** Main external dependency for time/project data. See `Helpers/Floq/` and `tim curl` docs in README. Floq API Swagger is available, but you must use the `tim curl <path>` command to fetch it.
- **Homebrew/NuGet:** For distribution and installation.

## Examples & References
- See [README.md](../README.md) for usage, install, and API tips.
- Key files: `app/Commands/`, `app/Helpers/`, `app/GlobalUsings.cs`, `publish.sh`.

---

**For AI agents:**
- Prefer adding new commands as new files in `app/Commands/`.
- Reuse helpers from `app/Helpers/`.
- Follow CLI UX patterns from README examples.
- Document new commands in README if user-facing.
- The cli experience is built on top of two frameworks. You can read these resources to verify how to implement certain features.
 1)  ConsoleAppFramework: https://raw.githubusercontent.com/Cysharp/ConsoleAppFramework/refs/heads/master/ReadMe.md
 2)  Spectre.Console: https://spectreconsole.net/
