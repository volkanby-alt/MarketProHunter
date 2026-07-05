# MarketProHunter v1 Test Checklist

Use this checklist before calling the preview ready for daily use.

## Build and package

- [ ] GitHub Actions Windows Build is green.
- [ ] Artifact `MarketProHunter-win-x64` downloads successfully.
- [ ] ZIP extracts without errors.
- [ ] `MarketProHunter.exe` starts on Windows.
- [ ] `config/vero-brands.txt` exists inside the extracted folder.

## First controlled scan

Recommended first test:

```text
ZIP: 07073
Min price: 9
Max price: 98
Pages: 1
Parallel: 1
Amazon Choice: enabled
Exclude low stock: enabled
Exclude sponsored: enabled
```

Expected result:

- [ ] Program does not crash.
- [ ] Log window shows progress.
- [ ] Products appear in the table or clear SKIP/WARN messages appear.
- [ ] Stop button cancels the scan safely.

## Normal scan

Recommended second test:

```text
Pages: 3
Parallel: 3
Selected categories: Home & Kitchen, Automotive, Tools & Home Improvement
```

Expected result:

- [ ] UI remains responsive.
- [ ] Failed product pages do not stop the scan.
- [ ] CAPTCHA/blocked pages are logged and skipped.
- [ ] Duplicate ASINs are not repeated.

## Output files

After a completed scan, check the `output` folder:

- [ ] `amazon_results_*.csv` exists.
- [ ] `smart_queue_top200_*.csv` exists.
- [ ] `marketprohunter_report_*.xlsx` exists.
- [ ] `run_summary_*.txt` exists.

## Excel report

Open the Excel file and check:

- [ ] Workbook opens without repair warning.
- [ ] `Daily Winners` sheet exists.
- [ ] `All Products` sheet exists.
- [ ] `Summary` sheet exists.
- [ ] Upload Score, Net Profit, TitleQ, ImageQ, BulletQ, DescQ, SpecQ columns are visible.

## Product review flow

- [ ] Selecting a row shows product details.
- [ ] Favorite button marks product as favorite.
- [ ] Reject ASIN hides/rejects that ASIN.
- [ ] Reject Brand hides/rejects that brand after confirmation.

## Success criteria for v1 preview

The preview is usable when:

- [ ] It can run a normal scan without crashing.
- [ ] It exports CSV and Excel correctly.
- [ ] Smart Queue produces enough usable products for manual review.
- [ ] Real-world test feedback is collected for v1.1.
