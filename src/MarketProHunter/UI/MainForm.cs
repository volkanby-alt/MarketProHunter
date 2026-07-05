using System.Diagnostics;
using MarketProHunter.Amazon;
using MarketProHunter.Categories;
using MarketProHunter.Decisions;
using MarketProHunter.Models;
using MarketProHunter.Profit;

namespace MarketProHunter.UI;

public sealed class MainForm : Form
{
    private readonly TextBox _keywordTextBox = new();
    private readonly CheckedListBox _categoryListBox = new();
    private readonly NumericUpDown _pagesNumeric = new();
    private readonly NumericUpDown _parallelNumeric = new();
    private readonly NumericUpDown _minPriceNumeric = new();
    private readonly NumericUpDown _maxPriceNumeric = new();
    private readonly TextBox _zipTextBox = new();
    private readonly CheckBox _amazonChoiceCheckBox = new();
    private readonly CheckBox _lowStockCheckBox = new();
    private readonly CheckBox _usuallyKeepCheckBox = new();
    private readonly CheckBox _sponsoredCheckBox = new();
    private readonly ComboBox _recommendationFilter = new();
    private readonly NumericUpDown _minOverallFilter = new();
    private readonly NumericUpDown _ebayFeePercentNumeric = new();
    private readonly NumericUpDown _promotedPercentNumeric = new();
    private readonly NumericUpDown _targetProfitPercentNumeric = new();
    private readonly NumericUpDown _minNetProfitNumeric = new();
    private readonly Button _applyFilterButton = new();
    private readonly Button _clearFilterButton = new();
    private readonly Button _favoriteButton = new();
    private readonly Button _rejectAsinButton = new();
    private readonly Button _rejectBrandButton = new();
    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();
    private readonly ProgressBar _progressBar = new();
    private readonly TextBox _logTextBox = new();
    private readonly DataGridView _resultsGrid = new();
    private readonly TextBox _detailTextBox = new();
    private readonly Label _statusLabel = new();
    private readonly IReadOnlyList<KeywordCategory> _categories = KeywordCategoryProvider.GetDefaultCategories();
    private readonly List<ProductResult> _allResults = new();
    private readonly UserDecisionStore _decisionStore = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private Stopwatch? _runTimer;

    public MainForm()
    {
        Text = "MarketProHunter - Smart Product Analyzer";
        Width = 1500;
        Height = 940;
        StartPosition = FormStartPosition.CenterScreen;
        BuildLayout();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 115));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));

        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Style = ProgressBarStyle.Marquee;
        _progressBar.Visible = false;
        ConfigureResultsGrid();

        _detailTextBox.Dock = DockStyle.Fill;
        _detailTextBox.Multiline = true;
        _detailTextBox.ScrollBars = ScrollBars.Vertical;
        _detailTextBox.ReadOnly = true;
        _detailTextBox.Font = new Font("Consolas", 10);
        _detailTextBox.Text = "Bir ürün seçildiğinde detaylı analiz burada görünecek.";

        var detailPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        detailPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        detailPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detailPanel.Controls.Add(BuildDecisionButtonPanel(), 0, 0);
        detailPanel.Controls.Add(_detailTextBox, 0, 1);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 1040 };
        split.Panel1.Controls.Add(_resultsGrid);
        split.Panel2.Controls.Add(detailPanel);

        _logTextBox.Dock = DockStyle.Fill;
        _logTextBox.Multiline = true;
        _logTextBox.ScrollBars = ScrollBars.Vertical;
        _logTextBox.ReadOnly = true;

        root.Controls.Add(BuildSettingsPanel(), 0, 0);
        root.Controls.Add(BuildCategoryPanel(), 0, 1);
        root.Controls.Add(BuildFilterPanel(), 0, 2);
        root.Controls.Add(_progressBar, 0, 3);
        root.Controls.Add(split, 0, 4);
        root.Controls.Add(_logTextBox, 0, 5);
        Controls.Add(root);
    }

    private FlowLayoutPanel BuildDecisionButtonPanel()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(2) };
        _favoriteButton.Text = "⭐ Favorite"; _favoriteButton.Width = 100; _favoriteButton.Click += (_, _) => FavoriteSelectedProduct();
        _rejectAsinButton.Text = "Reject ASIN"; _rejectAsinButton.Width = 110; _rejectAsinButton.Click += (_, _) => RejectSelectedAsin();
        _rejectBrandButton.Text = "Reject Brand"; _rejectBrandButton.Width = 115; _rejectBrandButton.Click += (_, _) => RejectSelectedBrand();
        panel.Controls.Add(_favoriteButton); panel.Controls.Add(_rejectAsinButton); panel.Controls.Add(_rejectBrandButton);
        return panel;
    }

    private TableLayoutPanel BuildSettingsPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 9, RowCount = 2 };
        for (var i = 0; i < 9; i++) panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11f));
        _keywordTextBox.PlaceholderText = "İsteğe bağlı ekstra arama kelimesi";
        _zipTextBox.Text = "07073";
        _pagesNumeric.Minimum = 1; _pagesNumeric.Maximum = 100; _pagesNumeric.Value = 3;
        _parallelNumeric.Minimum = 1; _parallelNumeric.Maximum = 8; _parallelNumeric.Value = 3;
        _minPriceNumeric.Minimum = 1; _minPriceNumeric.Maximum = 1000; _minPriceNumeric.Value = 9;
        _maxPriceNumeric.Minimum = 1; _maxPriceNumeric.Maximum = 1000; _maxPriceNumeric.Value = 98;
        _amazonChoiceCheckBox.Text = "Amazon Choice"; _amazonChoiceCheckBox.Checked = true;
        _lowStockCheckBox.Text = "Az stok ele"; _lowStockCheckBox.Checked = true;
        _usuallyKeepCheckBox.Text = "Usually keep ele"; _usuallyKeepCheckBox.Checked = false;
        _sponsoredCheckBox.Text = "Sponsored ele"; _sponsoredCheckBox.Checked = true;
        AddLabeledControl(panel, "Ekstra Arama", _keywordTextBox, 0, 0);
        AddLabeledControl(panel, "Sayfa", _pagesNumeric, 1, 0);
        AddLabeledControl(panel, "Paralel", _parallelNumeric, 2, 0);
        AddLabeledControl(panel, "Min $", _minPriceNumeric, 3, 0);
        AddLabeledControl(panel, "Max $", _maxPriceNumeric, 4, 0);
        AddLabeledControl(panel, "ZIP", _zipTextBox, 5, 0);
        panel.Controls.Add(_amazonChoiceCheckBox, 6, 1); panel.Controls.Add(_lowStockCheckBox, 7, 1); panel.Controls.Add(_usuallyKeepCheckBox, 8, 1);
        _startButton.Text = "Başlat"; _startButton.Height = 30; _startButton.Click += async (_, _) => await StartSearchAsync();
        _stopButton.Text = "Durdur"; _stopButton.Height = 30; _stopButton.Enabled = false; _stopButton.Click += (_, _) => _cancellationTokenSource?.Cancel();
        panel.Controls.Add(_startButton, 6, 0); panel.Controls.Add(_stopButton, 7, 0);
        _statusLabel.Text = "Hazır"; _statusLabel.AutoSize = true; panel.Controls.Add(_statusLabel, 8, 0);
        return panel;
    }

    private GroupBox BuildCategoryPanel()
    {
        var group = new GroupBox { Text = "Kategoriler - seçilenlerin anahtar kelimeleri otomatik taranır", Dock = DockStyle.Fill };
        _categoryListBox.Dock = DockStyle.Fill; _categoryListBox.CheckOnClick = true; _categoryListBox.MultiColumn = true; _categoryListBox.ColumnWidth = 230;
        foreach (var c in _categories) _categoryListBox.Items.Add(c.Name, c.Name is "Home & Kitchen" or "Automotive" or "Tools & Home Improvement" or "Beauty & Personal Care");
        group.Controls.Add(_categoryListBox);
        return group;
    }

    private GroupBox BuildFilterPanel()
    {
        var group = new GroupBox { Text = "Akıllı analiz ve kâr ayarları", Dock = DockStyle.Fill };
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 10, RowCount = 1, Padding = new Padding(5) };
        for (var i = 0; i < 10; i++) panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10f));
        _recommendationFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _recommendationFilter.Items.AddRange(new object[] { "All", "Upload", "Review", "Caution", "Reject", "Favorite", "Profitable" });
        _recommendationFilter.SelectedIndex = 0;
        _minOverallFilter.Minimum = 0; _minOverallFilter.Maximum = 100; _minOverallFilter.Value = 0;
        SetupMoneyNumeric(_ebayFeePercentNumeric, 0, 30, 13.25m);
        SetupMoneyNumeric(_promotedPercentNumeric, 0, 20, 5m);
        SetupMoneyNumeric(_targetProfitPercentNumeric, 0, 50, 12m);
        SetupMoneyNumeric(_minNetProfitNumeric, 0, 100, 2m);
        _applyFilterButton.Text = "Filtrele"; _applyFilterButton.Click += (_, _) => RefreshGrid();
        _clearFilterButton.Text = "Temizle"; _clearFilterButton.Click += (_, _) => { _recommendationFilter.SelectedIndex = 0; _minOverallFilter.Value = 0; RefreshGrid(); };
        AddLabeledControl(panel, "Filter", _recommendationFilter, 0, 0);
        AddLabeledControl(panel, "Min Score", _minOverallFilter, 1, 0);
        AddLabeledControl(panel, "eBay %", _ebayFeePercentNumeric, 2, 0);
        AddLabeledControl(panel, "Promoted %", _promotedPercentNumeric, 3, 0);
        AddLabeledControl(panel, "Target %", _targetProfitPercentNumeric, 4, 0);
        AddLabeledControl(panel, "Min Net $", _minNetProfitNumeric, 5, 0);
        panel.Controls.Add(_applyFilterButton, 6, 0); panel.Controls.Add(_clearFilterButton, 7, 0); panel.Controls.Add(_sponsoredCheckBox, 8, 0);
        group.Controls.Add(panel);
        return group;
    }

    private static void SetupMoneyNumeric(NumericUpDown numeric, decimal min, decimal max, decimal value)
    {
        numeric.Minimum = min; numeric.Maximum = max; numeric.DecimalPlaces = 2; numeric.Increment = 0.25m; numeric.Value = value;
    }

    private ProfitSettings BuildProfitSettings() => new()
    {
        EbayFinalValueFeePercent = _ebayFeePercentNumeric.Value,
        PromotedPercent = _promotedPercentNumeric.Value,
        TargetProfitPercent = _targetProfitPercentNumeric.Value,
        MinimumNetProfit = _minNetProfitNumeric.Value,
        EbayFixedFee = ProfitSettings.Default.EbayFixedFee,
        AmazonTaxPercent = 0m,
        ShippingCost = 0m
    };

    private static void AddLabeledControl(TableLayoutPanel panel, string label, Control control, int column, int row)
    {
        var box = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = new Padding(4) };
        box.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); box.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        box.Controls.Add(new Label { Text = label, AutoSize = true }, 0, 0); control.Dock = DockStyle.Fill; box.Controls.Add(control, 0, 1); panel.Controls.Add(box, column, row);
    }

    private void ConfigureResultsGrid()
    {
        _resultsGrid.Dock = DockStyle.Fill; _resultsGrid.ReadOnly = true; _resultsGrid.AllowUserToAddRows = false; _resultsGrid.AllowUserToDeleteRows = false;
        _resultsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; _resultsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _resultsGrid.SelectionChanged += (_, _) => ShowSelectedProductDetails();
        _resultsGrid.Columns.Add("fav", "Fav"); _resultsGrid.Columns.Add("uploadScore", "Upload"); _resultsGrid.Columns.Add("uploadDecision", "Decision"); _resultsGrid.Columns.Add("titleQ", "TitleQ"); _resultsGrid.Columns.Add("imageQ", "ImageQ"); _resultsGrid.Columns.Add("contentQ", "ContentQ"); _resultsGrid.Columns.Add("visualRisk", "Visual"); _resultsGrid.Columns.Add("imageCount", "Imgs"); _resultsGrid.Columns.Add("competition", "Comp"); _resultsGrid.Columns.Add("confidence", "Conf %"); _resultsGrid.Columns.Add("overall", "Overall"); _resultsGrid.Columns.Add("rec", "Rec");
        _resultsGrid.Columns.Add("sell", "eBay Sell"); _resultsGrid.Columns.Add("net", "Net $"); _resultsGrid.Columns.Add("margin", "Margin %"); _resultsGrid.Columns.Add("profitDecision", "Profit");
        _resultsGrid.Columns.Add("rating", "Rating"); _resultsGrid.Columns.Add("reviews", "Reviews"); _resultsGrid.Columns.Add("stars", "Stars"); _resultsGrid.Columns.Add("safety", "Safety"); _resultsGrid.Columns.Add("sales", "Sales"); _resultsGrid.Columns.Add("profitScore", "ProfitScore");
        _resultsGrid.Columns.Add("asin", "ASIN"); _resultsGrid.Columns.Add("title", "Title"); _resultsGrid.Columns.Add("brand", "Brand"); _resultsGrid.Columns.Add("price", "Amazon $"); _resultsGrid.Columns.Add("keyword", "Keyword"); _resultsGrid.Columns.Add("url", "URL");
    }

    private async Task StartSearchAsync()
    {
        var keywords = BuildKeywordList();
        if (keywords.Count == 0) { MessageBox.Show("En az bir kategori seçin veya ekstra arama kelimesi yazın.", "MarketProHunter", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        _allResults.Clear(); _resultsGrid.Rows.Clear(); _logTextBox.Clear(); _detailTextBox.Text = "Tarama başladı...";
        _runTimer = Stopwatch.StartNew();
        AppendLog($"Toplam anahtar kelime: {keywords.Count}"); AppendLog($"Paralel görev sayısı: {_parallelNumeric.Value}");
        SetRunningState(true); _cancellationTokenSource = new CancellationTokenSource();
        var settings = new SearchSettings { ZipCode = _zipTextBox.Text.Trim(), MinPrice = _minPriceNumeric.Value, MaxPrice = _maxPriceNumeric.Value, MaxParallelSearches = (int)_parallelNumeric.Value, RequireAmazonChoice = _amazonChoiceCheckBox.Checked, ExcludeLowStock = _lowStockCheckBox.Checked, ExcludeUsuallyKeepItem = _usuallyKeepCheckBox.Checked, ExcludeSponsored = _sponsoredCheckBox.Checked };
        var profitSettings = BuildProfitSettings();
        var service = new AmazonSearchService(); var logProgress = new Progress<string>(AppendLog); var productProgress = new Progress<ProductResult>(AddProductRow);
        try
        {
            var result = await service.RunManyAsync(keywords, (int)_pagesNumeric.Value, settings, profitSettings, logProgress, productProgress, _cancellationTokenSource.Token);
            _runTimer?.Stop();
            var failedText = result.FailedPageCount > 0 ? $" | Hatalı sayfa: {result.FailedPageCount}" : string.Empty;
            var acceptanceRate = CalculateAcceptanceRate(result.AcceptedCount, result.ScannedCount);
            _statusLabel.Text = $"Bitti: {result.AcceptedCount} uygun ürün | Kabul: %{acceptanceRate:0.00} | Süre: {FormatElapsed()}{failedText}"; AppendLog($"CSV dosyası: {result.OutputPath}");
            AppendLog($"Kabul oranı: %{acceptanceRate:0.00}");
            if (result.FailedPageCount > 0) AppendLog($"Hatalı sayfa sayısı: {result.FailedPageCount}");
            if (!string.IsNullOrWhiteSpace(result.SmartQueuePath)) AppendLog($"Smart Queue CSV: {result.SmartQueuePath}");
            if (!string.IsNullOrWhiteSpace(result.SummaryPath)) AppendLog($"Özet rapor: {result.SummaryPath}");
            MessageBox.Show($"Tarama bitti. Uygun ürün: {result.AcceptedCount}\nKabul oranı: %{acceptanceRate:0.00}\nSüre: {FormatElapsed()}\nHatalı sayfa: {result.FailedPageCount}", "MarketProHunter", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException) { _runTimer?.Stop(); _statusLabel.Text = $"Durduruldu | Süre: {FormatElapsed()}"; AppendLog("Tarama kullanıcı tarafından durduruldu."); }
        catch (Exception ex) { _runTimer?.Stop(); _statusLabel.Text = "Hata"; AppendLog("HATA: " + ex.Message); MessageBox.Show(ex.Message, "MarketProHunter Hata", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { SetRunningState(false); _cancellationTokenSource.Dispose(); _cancellationTokenSource = null; }
    }

    private IReadOnlyList<string> BuildKeywordList()
    {
        var keywords = new List<string>();
        foreach (var item in _categoryListBox.CheckedItems)
        {
            var category = _categories.FirstOrDefault(x => x.Name.Equals(item.ToString(), StringComparison.OrdinalIgnoreCase));
            if (category is not null) keywords.AddRange(category.Keywords);
        }
        if (!string.IsNullOrWhiteSpace(_keywordTextBox.Text)) keywords.AddRange(_keywordTextBox.Text.Split(new[] { ",", ";", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return keywords.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private bool PassesCurrentFilter(ProductResult product)
    {
        if (_decisionStore.IsRejected(product)) return false;
        var selected = _recommendationFilter.SelectedItem?.ToString() ?? "All";
        if (selected == "Favorite") return _decisionStore.IsFavorite(product.Asin);
        if (selected == "Profitable") return product.ProfitDecision.Equals("Profitable", StringComparison.OrdinalIgnoreCase);
        if (selected != "All" && !product.Recommendation.Equals(selected, StringComparison.OrdinalIgnoreCase)) return false;
        return product.OverallScore >= _minOverallFilter.Value;
    }

    private void RefreshGrid()
    {
        _resultsGrid.Rows.Clear();
        foreach (var p in _allResults.Where(PassesCurrentFilter).OrderByDescending(p => p.UploadScore).ThenByDescending(p => p.NetProfit).ThenByDescending(p => p.ContentQualityScore).ThenByDescending(p => p.TitleQualityScore).ThenByDescending(p => p.ImageQualityScore).ThenBy(p => p.CompetitionScore).ThenByDescending(p => p.ConfidenceScore).ThenByDescending(p => p.ImageCount)) AddProductRowToGrid(p);
        UpdateLiveStatus();
    }

    private void SetRunningState(bool running)
    {
        _startButton.Enabled = !running; _stopButton.Enabled = running; _progressBar.Visible = running; _categoryListBox.Enabled = !running; _parallelNumeric.Enabled = !running;
        if (running) _statusLabel.Text = "Çalışıyor...";
    }

    private void AppendLog(string message) => _logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");

    private void AddProductRow(ProductResult product)
    {
        if (_decisionStore.IsRejected(product)) { AppendLog($"SKIP {product.Asin} | Kullanıcı kara listesinde"); return; }
        _allResults.Add(product); if (PassesCurrentFilter(product)) AddProductRowToGrid(product);
        UpdateLiveStatus();
    }

    private void AddProductRowToGrid(ProductResult product)
    {
        var index = _resultsGrid.Rows.Add(_decisionStore.IsFavorite(product.Asin) ? "⭐" : "", product.UploadScore, product.UploadDecision, product.TitleQualityScore, product.ImageQualityScore, product.ContentQualityScore, product.VisualRiskLevel, product.ImageCount, product.CompetitionScore, product.ConfidenceScore, product.OverallScore, product.Recommendation, $"${product.RecommendedSalePrice}", $"${product.NetProfit}", product.NetMarginPercent, product.ProfitDecision, product.Rating, product.ReviewCount, product.Stars, product.SafetyScore, product.SalesScore, product.ProfitScore, product.Asin, product.Title, product.Brand, $"${product.Price}", product.SearchKeyword, product.ProductUrl);
        var row = _resultsGrid.Rows[index]; row.Tag = product;
        row.DefaultCellStyle.BackColor = product.UploadScore switch { >= 88 => Color.Honeydew, >= 74 => Color.LightYellow, >= 60 => Color.Moccasin, _ => Color.MistyRose };
    }

    private void UpdateLiveStatus()
    {
        var profit = _allResults.Sum(x => x.NetProfit);
        var avgQuality = _allResults.Count == 0 ? 0m : Math.Round(_allResults.Average(x => (x.TitleQualityScore + x.ImageQualityScore + x.ContentQualityScore) / 3m), 2);
        var acceptanceRate = CalculateAcceptanceRate(_allResults.Count, _allResults.Count + _resultsGrid.Rows.Count);
        _statusLabel.Text = $"Görünen: {_resultsGrid.Rows.Count} / Toplam: {_allResults.Count} | Quality: {avgQuality:0.00} | Net: ${profit:0.00} | Süre: {FormatElapsed()}";
    }

    private static decimal CalculateAcceptanceRate(int acceptedCount, int scannedCount)
    {
        return scannedCount <= 0 ? 0m : Math.Round(acceptedCount * 100m / scannedCount, 2);
    }

    private string FormatElapsed()
    {
        var elapsed = _runTimer?.Elapsed ?? TimeSpan.Zero;
        return elapsed.TotalHours >= 1 ? elapsed.ToString(@"h\:mm\:ss") : elapsed.ToString(@"mm\:ss");
    }

    private ProductResult? GetSelectedProduct() => _resultsGrid.SelectedRows.Count == 0 ? null : _resultsGrid.SelectedRows[0].Tag as ProductResult;
    private void FavoriteSelectedProduct() { var p = GetSelectedProduct(); if (p is null) return; _decisionStore.AddFavorite(p); AppendLog($"FAVORITE {p.Asin} | {p.Brand}"); RefreshGrid(); }
    private void RejectSelectedAsin() { var p = GetSelectedProduct(); if (p is null) return; _decisionStore.RejectAsin(p); AppendLog($"REJECT ASIN {p.Asin}"); RefreshGrid(); }
    private void RejectSelectedBrand() { var p = GetSelectedProduct(); if (p is null) return; if (MessageBox.Show($"{p.Brand} markasını kalıcı reddetmek istiyor musun?", "Reject Brand", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return; _decisionStore.RejectBrand(p); AppendLog($"REJECT BRAND {p.Brand}"); RefreshGrid(); }
    private void ShowSelectedProductDetails() { var p = GetSelectedProduct(); if (p is null) return; _detailTextBox.Text = BuildDetailText(p, _decisionStore.IsFavorite(p.Asin)); }

    private static string BuildDetailText(ProductResult p, bool isFavorite)
    {
        var reasons = new List<string> { p.IsAmazonChoice ? "+ Amazon Choice" : "- Amazon Choice değil", p.HasLowStockWarning ? "- Stok az uyarısı var" : "+ Stok uyarısı yok", p.HasUsuallyKeepItemText ? "+ Usually keep bilgisi var" : "+ Usually keep bilgisi yok", p.IsSponsored ? "- Sponsored sonuç" : "+ Organic sonuç", p.Price <= 60 ? "+ Amazon fiyatı iyi aralıkta" : "- Amazon fiyatı üst aralıkta", p.Rating >= 4.3m ? "+ Rating güçlü" : "- Rating zayıf veya okunamadı", p.ReviewCount >= 100 ? "+ Yorum sayısı güven veriyor" : "- Yorum sayısı düşük veya okunamadı", p.CompetitionScore <= 45 ? "+ Rekabet düşük/orta" : "- Rekabet yüksek olabilir", p.ImageCount >= 4 ? "+ Görsel seti yeterli" : "- Görsel sayısı eksik", p.VisualRiskLevel == "LOW" ? "+ Görsel risk düşük" : "- Görsel kontrol gerekli", p.UploadScore >= 88 ? "+ Upload Score güçlü" : "- Upload Score izleme gerektiriyor", p.ProfitDecision == "Profitable" ? "+ eBay kâr hedefini karşılıyor" : "- eBay kârı düşük" };
        return $"Favorite: {(isFavorite ? "YES ⭐" : "NO")}{Environment.NewLine}" +
               $"UPLOAD DECISION: {p.UploadDecision}{Environment.NewLine}Upload Score: {p.UploadScore}/100 | Competition: {p.CompetitionScore}/100{Environment.NewLine}" +
               $"Listing Quality: Title {p.TitleQualityScore}/100 | Images {p.ImageQualityScore}/100 | Content {p.ContentQualityScore}/100{Environment.NewLine}" +
               $"Quality Notes: {p.ListingQualityNotes}{Environment.NewLine}" +
               $"Visual Risk: {p.VisualRiskLevel} | Images: {p.ImageCount}/6 | {p.VisualRiskNotes}{Environment.NewLine}" +
               $"ASIN: {p.Asin}{Environment.NewLine}Brand: {p.Brand}{Environment.NewLine}Amazon Price: ${p.Price}{Environment.NewLine}" +
               $"Rating: {p.Rating}/5 | Reviews: {p.ReviewCount}{Environment.NewLine}" +
               $"Recommended eBay Price: ${p.RecommendedSalePrice}{Environment.NewLine}Net Profit: ${p.NetProfit}{Environment.NewLine}Net Margin: {p.NetMarginPercent}%{Environment.NewLine}" +
               $"Profit Decision: {p.ProfitDecision}{Environment.NewLine}eBay Fee: ${p.EbayFee}{Environment.NewLine}Promoted Fee: ${p.PromotedFee}{Environment.NewLine}" +
               $"Keyword: {p.SearchKeyword}{Environment.NewLine}URL: {p.ProductUrl}{Environment.NewLine}{Environment.NewLine}" +
               $"Image 1: {p.ImageUrl1}{Environment.NewLine}Image 2: {p.ImageUrl2}{Environment.NewLine}Image 3: {p.ImageUrl3}{Environment.NewLine}Image 4: {p.ImageUrl4}{Environment.NewLine}Image 5: {p.ImageUrl5}{Environment.NewLine}Image 6: {p.ImageUrl6}{Environment.NewLine}{Environment.NewLine}" +
               $"Confidence: {p.ConfidenceScore}%{Environment.NewLine}Safety: {p.SafetyScore}/100{Environment.NewLine}Sales: {p.SalesScore}/100{Environment.NewLine}Profit Score: {p.ProfitScore}/100{Environment.NewLine}" +
               $"Overall: {p.OverallScore}/100 {p.Stars}{Environment.NewLine}Recommendation: {p.Recommendation}{Environment.NewLine}{Environment.NewLine}" +
               "Neden bu puan?" + Environment.NewLine + string.Join(Environment.NewLine, reasons) + Environment.NewLine + Environment.NewLine + $"Title:{Environment.NewLine}{p.Title}";
    }
}
