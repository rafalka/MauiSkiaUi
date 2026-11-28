---
name: ship-fix
description: Fix a GitHub issue end-to-end and open a reviewed PR
---
1. Read issue #$ARGUMENTS with `gh issue view`.
2. Create branch fix/$ARGUMENTS from origin/devel.
3. Reproduce the bug (headless test or device), then fix it. Where the code exists on both layers (`SkUi*` controls and `SkUiCore*` nodes), fix both and keep them at parity.
4. Add regression tests and confirm they FAIL without the fix.
5. Run the full test suite.
6. Run two independent review rounds with subagents and fix every confirmed finding.
7. Open the PR using .github/pull_request_template.md.
8. Enable auto-fix monitoring and report what is verified vs. unverified.
