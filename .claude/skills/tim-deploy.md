---
name: tim-deploy
description: Full release process for the tim CLI tool. Use this skill whenever the user wants to release, deploy, publish, or cut a new version of tim. Triggers on phrases like "/tim-deploy", "release a new version", "deploy tim", "cut a release", "publish tim", or any request to bump the tim version.
---

# tim-deploy — Full Release Process

This skill walks through the complete process of releasing a new version of tim, from tagging to Homebrew and GitHub release notes.

## Repo locations

Derive locations dynamically — don't hardcode paths:

```bash
# tim repo: the git root of the current working directory
TIM_REPO=$(git rev-parse --show-toplevel)

# tim-brew repo: expected as a sibling named "tim-brew"
TIM_BREW_REPO=$(dirname "$TIM_REPO")/tim-brew
```

If `tim-brew` is not found at that path, ask the user where it is before proceeding.

---

## Step 1 — Determine the new version

Check the latest released tag:

```bash
gh release list --repo blankoslo/tim --exclude-drafts --limit 1 --json tagName -q '.[0].tagName' | cat
```

Show the result to the user and ask what version to release (patch / minor / major bump, or an explicit version number). Wait for confirmation before proceeding.

---

## Step 2 — Tag and push

From the tim repo:

```bash
git -C "$TIM_REPO" tag X.Y.Z && git -C "$TIM_REPO" push origin X.Y.Z
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
cd "$TIM_BREW_REPO"
./updateformula.sh X.Y.Z
git add Formula/tim.rb
git commit -m "Update tim formula to X.Y.Z"
git push
```

The script downloads release assets and computes their checksums automatically — it requires the version as an argument.

---

## Step 5 — Publish the GitHub release with release notes

The GitHub Actions workflow creates a **draft** release. You need to write notes and publish it.

First, get a summary of what changed:

```bash
git -C "$TIM_REPO" log <prev-version>..<new-version> --oneline
```

Write a concise, human-friendly release description based on the commit log. Focus on user-facing changes (features, bug fixes). Ignore chore/tooling/docs commits unless relevant to users.

Then publish:

```bash
gh release edit X.Y.Z --repo blankoslo/tim \
  --draft=false \
  --title "X.Y.Z" \
  --notes "<your release notes>"
```

---

## Done

Confirm to the user that all steps are complete:
- Tag pushed and CI passed
- Homebrew formula updated
- GitHub release published

Remind them they can verify with `brew update && brew upgrade tim && tim --version`.
