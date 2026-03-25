---
name: tim-deploy
description: Full release process for the tim CLI tool. Use this skill whenever the user wants to release, deploy, publish, or cut a new version of tim. Triggers on phrases like "/tim-deploy", "release a new version", "deploy tim", "cut a release", "publish tim", or any request to bump the tim version.
allowed-tools:
  - Bash(tim-*.sh*)
  - Bash(gh run list:*)
  - Bash(gh run view:*)
  - Bash(git -C * status:*)
  - Bash(git -C * push:*)
  - Bash(git -C * log:*)
  - Bash(brew update && brew upgrade tim:*)
---

# tim-deploy — Full Release Process

This skill walks through the complete process of releasing a new version of tim, from tagging to Homebrew and GitHub release notes.

## Step 1 — Determine the new version

First, sync with the remote to pick up any commits others may have pushed:

```bash
${CLAUDE_SKILL_DIR}/scripts/tim-sync-remote.sh
```

Then run the check script to get the latest tag, commits since it, and whether `app/` was touched:

```bash
${CLAUDE_SKILL_DIR}/scripts/tim-check-release.sh
```

If there are **no new commits**, inform the user and abort — there is nothing to release.

If no `app/` files changed, inform the user that there are no user-facing changes and **abort immediately — never ask if they want to release anyway**.

If there are new commits, show the latest tag and the commit list to the user, then ask what version to release (patch / minor / major bump, or an explicit version number). Wait for confirmation before proceeding.

Also verify that all local commits are pushed to the remote before tagging:

```bash
git -C "$(git rev-parse --show-toplevel)" status --short --branch
```

If there are unpushed commits, push them first:

```bash
git -C "$(git rev-parse --show-toplevel)" push
```

---

## Step 2 — Tag and push

```bash
${CLAUDE_SKILL_DIR}/scripts/tim-tag-and-push.sh X.Y.Z
```

This triggers the "Publish Release" GitHub Actions workflow.

---

## Step 3 — Wait for the GitHub Actions run to complete

Get the run ID immediately after pushing:

```bash
gh run list --repo blankoslo/tim --limit 1 --json status,conclusion,databaseId
```

Schedule a cron job to poll every minute:

```
CronCreate: cron="*/1 * * * *", recurring=true
prompt: Check `gh run view <databaseId> --repo blankoslo/tim --json status,conclusion`.
If status=="completed" and conclusion=="success": notify the user that the release assets are ready, then cancel this cron job with CronDelete.
If conclusion is anything other than "success" once completed: alert the user that the run failed.
```

Notify the user that you're waiting and will proceed automatically once the run finishes. Cancel the cron job (CronDelete) as soon as it completes — don't leave it running.

---

## Step 4 — Update the Homebrew formula

Once the run succeeds:

```bash
${CLAUDE_SKILL_DIR}/scripts/tim-update-homebrew.sh X.Y.Z
```

The script downloads release assets, computes checksums, commits, and pushes automatically.

---

## Step 5 — Publish the GitHub release with release notes

The GitHub Actions workflow creates a **draft** release. You need to write notes and publish it.

First, get a summary of what changed:

```bash
git -C "$(git rev-parse --show-toplevel)" log <prev-version>..<new-version> --oneline
```

Write a concise, human-friendly release description based on the commit log. Focus on user-facing changes (features, bug fixes). Ignore chore/tooling/docs commits unless relevant to users.

Then publish:

```bash
${CLAUDE_SKILL_DIR}/scripts/tim-publish-release.sh X.Y.Z "<your release notes>"
```

---

## Done

Confirm to the user that all steps are complete:
- Tag pushed and CI passed
- Homebrew formula updated
- GitHub release published

Remind them they can verify with `brew update && brew upgrade tim && tim --version`.
