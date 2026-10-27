# Working on Maskot

- Read README.md and docs/SPEC.md. The app is native Windows WPF, not a website.
- Use the existing checkout. Cloud tasks are already isolated; do not create a
  worktree unless the user asks.
- Linux cloud: `bash scripts/bootstrap-cloud.sh`, then `bash scripts/dev.sh check`.
  `bash scripts/dev.sh publish` creates the self-contained Windows x64 executable.
- Native UI cannot run on Linux. Never claim taskbar, transparency, media, tray,
  focus, multi-monitor or DPI checks passed without running them on Windows.
- Keep physical-pixel desktop geometry in Maskot.Core. Panels must remain
  independent of drag handling; feature state must survive mode changes.
- Never move/delete shelf originals or alter the user's taskbar settings.
- Lyrics are opt-in. No telemetry, backend, account, or API key for basic use.
- Keep the acceptance checklist and test report honest; distinguish automated
  logic tests, compile checks, and actual Windows behavior.
