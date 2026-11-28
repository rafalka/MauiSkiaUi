# Publish and verify (shared by /release and /release-patch)

`BUMP` is `release` (/release) or `patch` (/release-patch). `VERSION` is the version the pre-flight computed. The repository is `rafalka/MauiSkiaUi`; the procedure behind these steps is [docs/Releasing.md](../../../docs/Releasing.md).

1. **Dispatch** the NuGet publish workflow from `devel`:
   ```bash
   gh workflow run nuget-publish.yml --ref devel -f bump=$BUMP -f dry_run=false
   ```
   Find the run it started. It is the newest `workflow_dispatch` run on `devel`, created after the dispatch:
   ```bash
   gh run list --workflow nuget-publish.yml --branch devel --event workflow_dispatch -L 1 --json databaseId,createdAt,headSha
   ```
   Check that its `headSha` is the `devel` commit the pre-flight checked.
2. **Wait for the gate.** The prepare and pack jobs run first: version, changelog rename, pack on macOS and Windows, API compatibility against the last release. If one of them fails, nothing has been committed, tagged or published. Read the failing job's log (`gh run view <id> --log-failed`), fix the cause on `devel` (a missing API-compat suppression, a changelog heading), and dispatch again. Don't approve anything in the meantime. Watch the run in the background (`gh run watch <id> --exit-status`) or wait on a Monitor until-loop. Don't poll it in the foreground.
3. **Approve on the maintainer's behalf.** The maintainer invoking the skill is their approval of the `nuget.org` environment gate for this run (this run only, this version only). Once the run is waiting:
   ```bash
   gh api repos/rafalka/MauiSkiaUi/actions/runs/<id>/pending_deployments --jq '.[] | "\(.environment.id) \(.environment.name) \(.current_user_can_approve)"'
   gh api -X POST repos/rafalka/MauiSkiaUi/actions/runs/<id>/pending_deployments \
     -F 'environment_ids[]=<environment id>' -f state=approved -f comment="Approved for $VERSION by Claude Code on the maintainer's behalf"
   ```
   Approve only the `nuget.org` environment, only for the run you dispatched, and only when the version in its prepare job's summary is `VERSION`. If `current_user_can_approve` is false or the call fails, stop and ask the maintainer to approve it in the browser. Never change the environment's protection rules.
4. **Watch to the end** (in the background, as in 2). After the gate the run pushes the release commit `Release VERSION` and tag `vVERSION` (and moves `master`), pushes the packages to nuget.org, creates the GitHub release, and then builds the demo and samples apps for it. If it fails after the release commit was pushed (a nuget.org outage, say), dispatch again with `-f bump=none`, which republishes the tagged commit. If only the app builds fail, the release is out: run `gh workflow run demo-publish.yml -f release_tag=vVERSION` (check its inputs first) and report it.
5. **Verify it is live** before anything says it was released:
   - nuget.org lists the version for all four packages: `curl -s https://api.nuget.org/v3-flatcontainer/<id>/index.json | jq -r '.versions[-1]'` for `skiaui.maui`, `skiaui.maui.communitytoolkit`, `skiaui.maui.markdown`, `skiaui.maui.markdown.math`. Indexing can take 10 to 30 minutes, so wait on a Monitor until-loop.
   - `gh release view vVERSION` is there, has the notes, and has the `.nupkg` files and the demo and samples apps (APK and Mac Catalyst zip for each) attached.
   - `git fetch` shows `origin/master` and `origin/devel` at the release commit (a stable or patch release from `devel` moves both).
6. **Bring the release commit in** before editing docs: `git switch devel && git pull --ff-only`. Docs commits go on top of `Release VERSION`, never beside it.
7. **Check the release's issues are closed** (after step 5 only). Collect every bug and enhancement this release implemented, from three places:
   - `#N` in the changelog's `## VERSION` section;
   - `#N` in the release's plan and its roadmap section (`/release` only);
   - the PRs merged since the previous tag: the `(#N)` numbers in `git log vPREV..vVERSION --format=%s`. For each one that is a PR (`gh api repos/rafalka/MauiSkiaUi/issues/N --jq '.pull_request != null'`), the issues it closes: `gh pr view N --json closingIssuesReferences --jq '.closingIssuesReferences[].number'`. The rest are issues (a subject like `… (#140) (#142)` names the issue, then the PR).

   For each issue, run `gh issue view N --json number,title,state,stateReason,labels` and sort it into one of four groups:
   - **Open and implemented in this release:** comment `Released in VERSION: <GitHub release URL>` and close it as completed (`gh issue close N --reason completed --comment …`).
   - **Already closed as completed:** if it has no "Released in" comment yet, add one. Don't reopen anything.
   - **Closed as not planned, or open and only partly implemented:** leave it alone and report it with what shipped and what didn't.
   - **Mentioned but not implemented** (a plan step that was cut, say): leave it open and report it.

   Finally, list the open `bug` and `enhancement` issues (`gh issue list --label bug --label enhancement --state open` runs as AND, so run one query per label) and report any that this release's commits or changelog look like they fixed but that nothing references. Ask about those; don't close them.
