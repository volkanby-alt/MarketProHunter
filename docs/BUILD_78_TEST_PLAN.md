# Windows Build #78 Test Plan

Commit: `33a127a8ede9e3d2f04a04a68ef0e07791cd6608`
Feature: in-app report folder button

## Purpose

This build adds a `Raporlar` button inside the app so the user can open the latest report output folder after a scan finishes.

## Pre-scan checks

1. Start the Windows app.
2. Confirm default scan settings:
   - ZIP: `07073`
   - Min price: `9`
   - Max price: `98`
   - Pages: `3`
   - Parallel tasks: `3`
3. Confirm the `Raporlar` button is visible.
4. Confirm the `Raporlar` button is disabled before the first scan.

## Scan test

1. Select the default categories.
2. Start the scan.
3. Wait until the scan finishes or stop after enough products are found.
4. Confirm the app logs these outputs when available:
   - CSV file
   - Smart Queue CSV
   - Excel report
   - Summary report
   - Report folder

## Report button test

1. After the scan finishes, confirm the `Raporlar` button becomes enabled.
2. Click `Raporlar`.
3. Confirm Windows File Explorer opens the folder that contains the latest scan files.
4. Confirm the folder includes at least one report file.

## Error-state test

1. Restart the app.
2. Click `Raporlar` before running a scan.
3. Expected result: the app should not crash and should tell the user to run a scan first.

## Pass criteria

Build #78 passes if:

- The app starts normally.
- A scan can run with the default settings.
- Report files are created.
- The report folder path appears in the log.
- The `Raporlar` button opens the latest report folder.
- The app does not crash when no report folder is available.

## Next improvement after Build #78

If this build passes, the next useful improvement is to add a button for opening the selected product URL directly from the results grid.
