# Security policy

## Supported versions

Security fixes go into the next stable release of the latest `1.x` line (a patch release when it can't wait); older versions and prereleases are not patched. Update to the [latest stable `SkiaUi.Maui` package](https://www.nuget.org/packages/SkiaUi.Maui).

## Reporting a vulnerability

**Do not open a public issue, discussion or pull request for a security problem.**

Report it privately through GitHub: open the [Security tab](https://github.com/rafalka/MauiSkiaUi/security) and choose **Report a vulnerability** ([direct link](https://github.com/rafalka/MauiSkiaUi/security/advisories/new)). Only the maintainer can see the report.

Please include:

- the affected `SkiaUi.Maui` version and platform;
- what an attacker can do, and under which conditions (for example, untrusted HTML passed to a rich-text label, or an image loaded from an untrusted URL);
- steps or code to reproduce it.

You can expect an acknowledgement within a week. Once the problem is confirmed, the fix ships in a new release, and the advisory is published with credit to you, unless you prefer to stay anonymous.

Vulnerabilities in a dependency (.NET MAUI, SkiaSharp, HarfBuzz) should be reported to that project. Open an issue here as well if SkiaUi needs to move to a fixed version.
