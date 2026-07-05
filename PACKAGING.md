# MarketProHunter Packaging

The Windows package is created by GitHub Actions and by the local release script.

## Package artifact

Artifact name:

```text
MarketProHunter-win-x64
```

## Required files

The package should include:

```text
MarketProHunter.exe
config\vero-brands.txt
VERSION
QUICK_START.md
RELEASE_NOTES.md
```

## Local commands

Build release package:

```text
tools\run-release.bat
```

Run smoke test:

```text
tools\smoke-test.bat
```

Clean generated reports:

```text
tools\clean-output.bat
```

## Output folder

Local release output:

```text
publish\MarketProHunter
```
