# Codex workflow setup

The owner maintains the reusable `codex-project-workflow` plugin in the private GitHub repository `Yurii-Tor/codex-project-workflow`. It packages the context-optimization and post-release Git-flow skills referenced by `AGENTS.md`.

## Install on a new computer

Prerequisites: Codex CLI, Git, GitHub SSH access to the private repository, and a successful `ssh -T git@github.com` authentication check.

```powershell
codex plugin marketplace add ssh://git@github.com/Yurii-Tor/codex-project-workflow.git --ref main
codex plugin add codex-project-workflow@yurii-tor
```

Start a new Codex task after installation so the task receives the refreshed skill catalog. Installation is local to each Codex installation; the private Git marketplace makes the source portable but does not automatically synchronize the installation through the ChatGPT account.

## Explicit invocation

```text
$codex-project-workflow:project-context-optimizer
$codex-project-workflow:release-git-flow
```

## Update

```powershell
codex plugin marketplace upgrade yurii-tor
codex plugin add codex-project-workflow@yurii-tor
```

Start a new task after updating.

## Non-blocking fallback

The plugin improves consistency but is not a build dependency. If it is unavailable, repository work may continue by applying the requirements embedded in `AGENTS.md`:

- recommend the task boundary, model/reasoning, environment, context sources, and tools before substantive work;
- keep `main` release-only and use `develop` as the integration base;
- isolate each repository change in a task branch or worktree;
- validate the exact change and finish authorized implementation work with a reproducible pull request;
- never treat workflow guidance as authorization to merge, tag, release, deploy, or publish.
