# MarketProHunter Release Notes

## 1.0.0-preview

This preview focuses on delivering a usable Windows version for real product research and testing.

### Core scan engine

- Amazon keyword and category scanning.
- Multi-keyword runs.
- Parallel search support.
- Configurable ZIP code, price range, page count, and parallel task count.
- Amazon robot/CAPTCHA detection with safe page skipping.

### Filtering

- Amazon Choice requirement.
- Low stock exclusion.
- Sponsored result exclusion.
- Optional `Customer usually keep this item` handling.
- Duplicate ASIN prevention.
- VeRO brand filtering through `config/vero-brands.txt`.

### Product scoring

- Upload Score.
- Safety Score.
- Sales Score.
- Profit Score.
- Confidence Score.
- Competition Score.
- Smart Queue ranking.

### Listing quality

- Title quality score.
- Image quality score.
- Content quality score.
- Bullet point quality score.
- Description quality score.
- Specification quality score.
- A+ Content detection.
- Product page notes.

### Profit engine

- Estimated eBay sale price.
- eBay fee calculation.
- Promoted listing fee calculation.
- Net profit estimate.
- Net margin estimate.
- Profit decision.

### Smart Queue

- Default target: Top 200 products.
- Maximum queue size: 300.
- Ranking includes upload score, profit, product page quality, listing quality, confidence, and image count.

### Exports

Each scan creates files in the `output` folder:

- full CSV results,
- Smart Queue Top 200 CSV,
- Excel report,
- text summary report.

### Excel report

The Excel workbook contains:

- `Daily Winners`,
- `All Products`,
- `Summary`.

### UI

- Product table with profit, quality, score, and page-quality columns.
- Detail panel for selected products.
- Favorite and reject actions.
- Run progress logging.
- Scan completion summary.

### Known limitations

- Amazon can still block requests with robot/CAPTCHA checks.
- Product page parsing depends on Amazon HTML structure and may need real-world tuning.
- eBay competition analysis is intentionally not included in v1.0-preview; it can be added later after the base scanner is stable.

### v1.0 focus before release

- Stabilize real scans.
- Verify Excel files open correctly.
- Improve error handling where needed.
- Keep the feature set fixed until the first usable release is tested.
