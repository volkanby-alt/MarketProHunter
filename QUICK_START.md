# MarketProHunter Quick Start

MarketProHunter is a Windows tool for finding Amazon products that are safer and more useful for eBay listing research.

## Main goal

The current v1.0 target is:

- scan Amazon keywords and categories,
- filter risky products,
- calculate estimated eBay profit,
- score listing quality,
- prepare a Top 200 Smart Queue,
- export CSV and Excel reports.

## How to run from GitHub Actions

1. Open the repository on GitHub.
2. Go to **Actions**.
3. Open **Windows Build**.
4. Click **Run workflow**.
5. When the workflow finishes green, open the run.
6. Download the artifact named:

```text
MarketProHunter-win-x64
```

7. Extract the ZIP file on Windows.
8. Run `MarketProHunter.exe`.

## How to build locally on Windows

Use this only if you have .NET 8 SDK installed.

1. Download or clone the repository.
2. Open the repository folder.
3. Double-click:

```text
tools\run-release.bat
```

4. When it finishes, run:

```text
publish\MarketProHunter\MarketProHunter.exe
```

## Recommended first settings

Use these first so the scan stays controlled:

```text
ZIP: 07073
Min price: 9
Max price: 98
Pages: 3
Parallel: 3
Amazon Choice: enabled
Exclude low stock: enabled
Exclude sponsored: enabled
Usually keep filter: optional
```

## Daily target

For a 13,000 item eBay inventory limit, the working target is:

```text
Daily upload-ready products: 200+
Backup pool: 100+
```

## Output files

After each scan, the program creates files inside the `output` folder:

```text
amazon_results_YYYYMMDD_HHMMSS.csv
smart_queue_top200_YYYYMMDD_HHMMSS.csv
marketprohunter_report_YYYYMMDD_HHMMSS.xlsx
run_summary_YYYYMMDD_HHMMSS.txt
```

## Excel report sheets

The Excel report contains:

- **Daily Winners**: Smart Queue Top 200 products.
- **All Products**: all accepted products.
- **Summary**: run statistics, top keywords, and top brands.

## Important notes

- If Amazon shows a robot/CAPTCHA page, the program skips that page and continues.
- A single failed product page should not stop the scan.
- CSV files are useful for other tools.
- Excel is best for manual review.

## Suggested workflow

1. Run the scan.
2. Open the Excel report.
3. Start with the **Daily Winners** sheet.
4. Review the highest Upload Score products first.
5. Use image count, title quality, bullet quality, description quality, specification quality, net profit, and notes before uploading.

## Current v1.0 focus

No more large new features before v1.0. The focus is now:

- stability,
- speed,
- clear exports,
- reliable Windows package,
- real-world testing.
