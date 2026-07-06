using System.Diagnostics;
using MarketProHunter.Amazon;
using MarketProHunter.Categories;
using MarketProHunter.Decisions;
using MarketProHunter.Models;
using MarketProHunter.Profit;

namespace MarketProHunter.UI;

public sealed class MainForm : Form
{
    private const int MaxLogLines = 500;

    private readonly ComboBox _categoryComboBox = new();
    private readonly ComboBox _subCategoryComboBox = new();
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
    private readonly Button _openOutputButton = new();
    private readonly Button _favoriteButton = new();
    private readonly Button _rejectAsinButton = new();
    private readonly Button _rejectBrandButton = new();
    private readonly Button _openProductButton = new();
    private readonly Button _startButton = new();
    private readonly ProgressBar _progressBar = new();
    private readonly TextBox _logTextBox = new();
    private readonly DataGridView _resultsGrid = new();
    private readonly TextBox _detailTextBox = new();
    private readonly Label _statusLabel = new();
    private readonly Label _scanScopeLabel = new();
    private readonly IReadOnlyList<KeywordCategory> _categories = KeywordCategoryProvider.GetDefaultCategories();
    private readonly List<ProductResult> _allResults = new();
    private readonly HashSet<string> _seenAsins = new(StringComparer.OrdinalIgnoreCase);
    private readonly UserDecisionStore _decisionStore = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private Stopwatch? _runTimer;
    private string? _lastOutputDirectory;

    public MainForm()
    {
        Text = "MarketProHunter - ASIN Hunter";
        Width = 1450;
        Height = 900;
        StartPosition = FormStartPosition.CenterScreen;
        BuildLayout();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 115));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 32));

        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Style = ProgressBarStyle.Marquee;
        _progressBar.Visible = false;
        ConfigureResultsGrid();

        _detailTextBox.Dock = DockStyle.Fill;
        _detailTextBox.Multiline = true;
        _detailTextBox.ScrollBars = ScrollBars.Vertical;
        _detailTextBox.ReadOnly = true;
        _detailTextBox.Font = new Font("Consolas", 10);
        _detailTextBox.Text = "Bir ASIN seçildiğinde detaylı analiz burada görünecek.";

        var detailPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        detailPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        detailPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detailPanel.Controls.Add(BuildDecisionButtonPanel(), 0, 0);
        detailPanel.Controls.Add(_detailTextBox, 0, 1);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 960 };
        split.Panel1.Controls.Add(_resultsGrid);
        split.Panel2.Controls.Add(detailPanel);

        _logTextBox.Dock = DockStyle.Fill;
        _logTextBox.Multiline = true;
        _logTextBox.ScrollBars = ScrollBars.Vertical;
        _logTextBox.ReadOnly = true;

        root.Controls.Add(BuildSettingsPanel(), 0, 0);
        root.Controls.Add(BuildScanScopePanel(), 0, 1);
        root.Controls.Add(BuildFilterPanel(), 0, 2);
        root.Controls.Add(_progressBar, 0, 3);
        root.Controls.Add(split, 0, 4);
        root.Controls.Add(_logTextBox, 0, 5);
        Controls.Add(root);
    }

    private FlowLayoutPanel BuildDecisionButtonPanel()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(2) };
        _favoriteButton.Text = "⭐ Favori"; _favoriteButton.Width = 100; _favoriteButton.Click += (_, _) => FavoriteSelectedProduct();
        _rejectAsinButton.Text = "ASIN Ele"; _rejectAsinButton.Width = 95; _rejectAsinButton.Click += (_, _) => RejectSelectedAsin();
        _rejectBrandButton.Text = "Markayı Ele"; _rejectBrandButton.Width = 110; _rejectBrandButton.Click += (_, _) => RejectSelectedBrand();
        _openProductButton.Text = "Amazon'da Aç"; _openProductButton.Width = 115; _openProductButton.Click += (_, _) => OpenSelectedProductUrl();
        panel.Controls.Add(_favoriteButton); panel.Controls.Add(_rejectAsinButton); panel.Controls.Add(_rejectBrandButton); panel.Controls.Add(_openProductButton);
        return panel;
    }

    private TableLayoutPanel BuildSettingsPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 9, RowCount = 2 };
        for (var i = 0; i < 9; i++) panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11f));

        _categoryComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _categoryComboBox.Items.AddRange(_categories.Cast<object>().ToArray());
        _categoryComboBox.DisplayMember = nameof(KeywordCategory.Name);
        _categoryComboBox.SelectedIndexChanged += (_, _) => LoadSubCategoriesForSelectedCategory();

        _subCategoryComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _subCategoryComboBox.SelectedIndexChanged += (_, _) => UpdateScanScopeLabel();

        _zipTextBox.Text = "07073";
        _pagesNumeric.Minimum = 1; _pagesNumeric.Maximum = 100; _pagesNumeric.Value = 3;
        _parallelNumeric.Minimum = 1; _parallelNumeric.Maximum = 8; _parallelNumeric.Value = 2;
        _minPriceNumeric.Minimum = 1; _minPriceNumeric.Maximum = 1000; _minPriceNumeric.Value = 9;
        _maxPriceNumeric.Minimum = 1; _maxPriceNumeric.Maximum = 1000; _maxPriceNumeric.Value = 98;
        _minPriceNumeric.ValueChanged += (_, _) => UpdateScanScopeLabel();
        _maxPriceNumeric.ValueChanged += (_, _) => UpdateScanScopeLabel();
        _amazonChoiceCheckBox.Text = "Amazon Choice"; _amazonChoiceCheckBox.Checked = true;
        _lowStockCheckBox.Text = "Az stok ele"; _lowStockCheckBox.Checked = true;
        _usuallyKeepCheckBox.Text = "Usually keep ele"; _usuallyKeepCheckBox.Checked = false;
        _sponsoredCheckBox.Text = "Sponsored ele"; _sponsoredCheckBox.Checked = true;

        AddLabeledControl(panel, "Kategori", _categoryComboBox, 0, 0);
        AddLabeledControl(panel, "Alt Kategori", _subCategoryComboBox, 1, 0);
        AddLabeledControl(panel, "Min $", _minPriceNumeric, 2, 0);
        AddLabeledControl(panel, "Max $", _maxPriceNumeric, 3, 0);
        AddLabeledControl(panel, "Sayfa", _pagesNumeric, 4, 0);
        AddLabeledControl(panel, "ZIP", _zipTextBox, 5, 0);
        panel.Controls.Add(_amazonChoiceCheckBox, 6, 1); panel.Controls.Add(_lowStockCheckBox, 7, 1); panel.Controls.Add(_usuallyKeepCheckBox, 8, 1);
        _startButton.Text = "▶ Taramayı Başlat"; _startButton.Height = 32; _startButton.Click += async (_, _) => await ToggleSearchAsync();
        panel.Controls.Add(_startButton, 6, 0); panel.SetColumnSpan(_startButton, 2);
        _statusLabel.Text = "Hazır"; _statusLabel.AutoSize = true; panel.Controls.Add(_statusLabel, 8, 0);

        if (_categoryComboBox.Items.Count > 0) _categoryComboBox.SelectedIndex = 0;
        return panel;
    }

    private GroupBox BuildScanScopePanel()
    {
        var group = new GroupBox { Text = "Tarama hedefi", Dock = DockStyle.Fill };
        _scanScopeLabel.Dock = DockStyle.Fill;
        _scanScopeLabel.TextAlign = ContentAlignment.MiddleLeft;
        _scanScopeLabel.Padding = new Padding(8, 0, 0, 0);
        group.Controls.Add(_scanScopeLabel);
        return group;
    }

    private GroupBox BuildFilterPanel()
    {
        var group = new GroupBox { Text = "Akıllı analiz ve kâr ayarları", Dock = DockStyle.Fill };
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 10, RowCount = 1, Padding = new Padding(5) };
        for (var i = 0; i < 10; i++) panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10f));
        _recommendationFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _recommendationFilter.Items.AddRange(new object[] { "Listele + İncele", "All", "Buy Immediately", "Upload", "Review", "Watch", "Caution", "Reject", "Favorite", "Profitable" });
        _recommendationFilter.SelectedIndex = 0;
        _minOverallFilter.Minimum = 0; _minOverallFilter.Maximum = 100; _minOverallFilter.Value = 0;
        SetupMoneyNumeric(_ebayFeePercentNumeric, 0, 30, 13.25m);
        SetupMoneyNumeric(_promotedPercentNumeric, 0, 20, 5m);
        SetupMoneyNumeric(_targetProfitPercentNumeric, 0, 50, 12m);
        SetupMoneyNumeric(_minNetProfitNumeric, 0, 100, 2m);
        _applyFilterButton.Text = "Filtrele"; _applyFilterButton.Click += (_, _) => RefreshGrid();
        _clearFilterButton.Text = "Temizle"; _clearFilterButton.Click += (_, _) => { _recommendationFilter.SelectedIndex = 0; _minOverallFilter.Value = 0; RefreshGrid(); };
        _openOutputButton.Text = "Raporlar"; _openOutputButton.Enabled = false; _openOutputButton.Click += (_, _) => OpenOutputFolder();
        AddLabeledControl(panel, "Görüntü", _recommendationFilter, 0, 0);
        AddLabeledControl(panel, "Min Score", _minOverallFilter, 1, 0);
        AddLabeledControl(panel, "eBay %", _ebayFeePercentNumeric, 2, 0);
        AddLabeledControl(panel, "Promoted %", _promotedPercentNumeric, 3, 0);
        AddLabeledControl(panel, "Target %", _targetProfitPercentNumeric, 4, 0);
        AddLabeledControl(panel, "Min Net $", _minNetProfitNumeric, 5, 0);
        panel.Controls.Add(_applyFilterButton, 6, 0); panel.Controls.Add(_clearFilterButton, 7, 0); panel.Controls.Add(_sponsoredCheckBox, 8, 0); panel.Controls.Add(_openOutputButton, 9, 0);
        group.Controls.Add(panel);
        return group;
    }

    private void LoadSubCategoriesForSelectedCategory()
    {
        _subCategoryComboBox.Items.Clear();
        if (_categoryComboBox.SelectedItem is not KeywordCategory category) return;
        _subCategoryComboBox.Items.AddRange(category.SubCategories.Cast<object>().ToArray());
        if (_subCategoryComboBox.Items.Count > 0) _subCategoryComboBox.SelectedIndex = 0;
        UpdateScanScopeLabel();
    }

    private void UpdateScanScopeLabel()
    {
        var subCategory = _subCategoryComboBox.SelectedItem as KeywordSubCategory;
        var keywordCount = subCategory?.Keywords.Count ?? 0;
        _scanScopeLabel.Text = subCategory is null
            ? "Alt kategori seçin."
            : $"Seçilen alt kategori: {subCategory.Name} | Taranacak arama seti: {keywordCount} | Fiyat aralığı: ${_minPriceNumeric.Value:0.##} - ${_maxPriceNumeric.Value:0.##} | Amaç: sadece uygun ASIN'leri bulmak";
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
        _resultsGrid.Columns.Add("fav", "Fav");
        _resultsGrid.Columns.Add("finalDecision", "Final");
        _resultsGrid.Columns.Add("finalScore", "Final Score");
        _resultsGrid.Columns.Add("uploadDecision", "Karar");
        _resultsGrid.Columns.Add("asin", "ASIN");
        _resultsGrid.Columns.Add("brand", "Marka");
        _resultsGrid.Columns.Add("price", "Amazon $");
        _resultsGrid.Columns.Add("net", "Net $");
        _resultsGrid.Columns.Add("margin", "Margin %");
        _resultsGrid.Columns.Add("uploadScore", "Upload Score");
        _resultsGrid.Columns.Add("profitDecision", "Kâr");
        _resultsGrid.Columns.Add("keyword", "Alt Arama");
        _resultsGrid.Columns.Add("title", "Title");
        _resultsGrid.Columns.Add("url", "URL");
    }

    private async Task ToggleSearchAsync()
    {
        if (_cancellationTokenSource is not null)
        {
            _cancellationTokenSource.Cancel();
            return;
        }

        await StartSearchAsync();
    }

    private async Task StartSearchAsync()
    {
        var keywords = BuildKeywordList();
        if (keywords.Count == 0) { MessageBox.Show("Önce bir kategori ve alt kategori seçin.", "MarketProHunter", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (_minPriceNumeric.Value > _maxPriceNumeric.Value) { MessageBox.Show("Minimum fiyat, maksimum fiyattan büyük olamaz.", "MarketProHunter", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        UpdateScanScopeLabel();
        _allResults.Clear(); _seenAsins.Clear(); _resultsGrid.Rows.Clear(); _logTextBox.Clear(); _detailTextBox.Text = "Tarama başladı...";
        _runTimer = Stopwatch.StartNew();
        AppendLog($"Kategori: {GetSelectedCategoryName()} | Alt kategori: {GetSelectedSubCategoryName()}");
        AppendLog($"Fiyat aralığı: ${_minPriceNumeric.Value:0.##} - ${_maxPriceNumeric.Value:0.##}");
        AppendLog($"Toplam arama seti: {keywords.Count}"); AppendLog($"Paralel görev sayısı: {_parallelNumeric.Value}");
        SetRunningState(true); _cancellationTokenSource = new CancellationTokenSource();
        var settings = new SearchSettings { ZipCode = _zipTextBox.Text.Trim(), MinPrice = _minPriceNumeric.Value, MaxPrice = _maxPriceNumeric.Value, MaxParallelSearches = (int)_parallelNumeric.Value, RequireAmazonChoice = _amazonChoiceCheckBox.Checked, ExcludeLowStock = _lowStockCheckBox.Checked, ExcludeUsuallyKeepItem = _usuallyKeepCheckBox.Checked, ExcludeSponsored = _sponsoredCheckBox.Checked };
        var profitSettings = BuildProfitSettings();
        var service = new AmazonSearchService(); var logProgress = new Progress<string>(AppendLog); var productProgress = new Progress<ProductResult>(AddProductRow);
        try
        {
            var result = await service.RunManyAsync(keywords, (int)_pagesNumeric.Value, settings, profitSettings, logProgress, productProgress, _cancellationTokenSource.Token);
            _runTimer?.Stop();
            RememberOutputFolder(result);
            var failedText = result.FailedPageCount > 0 ? $" | Hatalı sayfa: {result.FailedPageCount}" : string.Empty;
            var acceptanceRate = CalculateAcceptanceRate(result.AcceptedCount, result.ScannedCount);
            _statusLabel.Text = $"Bitti: {result.AcceptedCount} uygun ASIN | Kabul: %{acceptanceRate:0.00} | Süre: {FormatElapsed()}{failedText}"; AppendLog($"CSV dosyası: {result.OutputPath}");
            AppendLog($"Kabul oranı: %{acceptanceRate:0.00}");
            if (result.FailedPageCount > 0) AppendLog($"Hatalı sayfa sayısı: {result.FailedPageCount}");
            if (!string.IsNullOrWhiteSpace(result.SmartQueuePath)) AppendLog($"Smart Queue CSV: {result.SmartQueuePath}");
            if (!string.IsNullOrWhiteSpace(result.ExcelPath)) AppendLog($"Excel raporu: {result.ExcelPath}");
            if (!string.IsNullOrWhiteSpace(result.SummaryPath)) AppendLog($"Özet rapor: {result.SummaryPath}");
            if (!string.IsNullOrWhiteSpace(_lastOutputDirectory)) AppendLog($"Rapor klasörü: {_lastOutputDirectory}");
            var excelText = string.IsNullOrWhiteSpace(result.ExcelPath) ? string.Empty : $"\nExcel: {result.ExcelPath}";
            MessageBox.Show($"Tarama bitti. Uygun ASIN: {result.AcceptedCount}\nKabul oranı: %{acceptanceRate:0.00}\nSüre: {FormatElapsed()}\nHatalı sayfa: {result.FailedPageCount}{excelText}", "MarketProHunter", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException) { _runTimer?.Stop(); _statusLabel.Text = $"Durduruldu | Süre: {FormatElapsed()}"; AppendLog("Tarama kullanıcı tarafından durduruldu."); }
        catch (Exception ex) { _runTimer?.Stop(); _statusLabel.Text = "Hata"; AppendLog("HATA: " + ex.Message); MessageBox.Show(ex.Message, "MarketProHunter Hata", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { SetRunningState(false); _cancellationTokenSource.Dispose(); _cancellationTokenSource = null; }
    }

    private IReadOnlyList<string> BuildKeywordList()
    {
        if (_subCategoryComboBox.SelectedItem is not KeywordSubCategory subCategory) return Array.Empty<string>();
        return subCategory.Keywords.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private string GetSelectedCategoryName() => _categoryComboBox.SelectedItem is KeywordCategory category ? category.Name : "-";
    private string GetSelectedSubCategoryName() => _subCategoryComboBox.SelectedItem is KeywordSubCategory subCategory ? subCategory.Name : "-";

    private bool PassesCurrentFilter(ProductResult product)
    {
        if (_decisionStore.IsRejected(product)) return false;
        var selected = _recommendationFilter.SelectedItem?.ToString() ?? "Listele + İncele";
        if (selected == "Listele + İncele") return IsUsefulCandidate(product) && product.FinalScore >= _minOverallFilter.Value;
        if (selected == "Favorite") return _decisionStore.IsFavorite(product.Asin);
        if (selected == "Profitable") return product.ProfitDecision.Equals("Profitable", StringComparison.OrdinalIgnoreCase);
        if (selected != "All" && !product.FinalDecision.Equals(selected, StringComparison.OrdinalIgnoreCase) && !product.Recommendation.Equals(selected, StringComparison.OrdinalIgnoreCase)) return false;
        return product.FinalScore >= _minOverallFilter.Value;
    }

    private static bool IsUsefulCandidate(ProductResult product)
    {
        return !product.FinalDecision.Equals("Reject", StringComparison.OrdinalIgnoreCase)
            && !product.Recommendation.Equals("Reject", StringComparison.OrdinalIgnoreCase)
            && !product.UploadDecision.Equals("Reject", StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshGrid()
    {
        _resultsGrid.Rows.Clear();
        foreach (var p in _allResults.Where(PassesCurrentFilter).OrderByDescending(p => p.FinalDecision.Equals("Buy Immediately", StringComparison.OrdinalIgnoreCase)).ThenByDescending(p => p.FinalScore).ThenByDescending(p => p.UploadScore).ThenByDescending(p => p.BrandScore).ThenByDescending(p => p.NetProfit).ThenByDescending(AverageQuality).ThenBy(p => p.CompetitionScore).ThenByDescending(p => p.ConfidenceScore)) AddProductRowToGrid(p);
        UpdateLiveStatus();
    }

    private void SetRunningState(bool running)
    {
        _startButton.Enabled = true;
        _startButton.Text = running ? "■ Durdur" : "▶ Taramayı Başlat";
        _progressBar.Visible = running;
        _categoryComboBox.Enabled = !running;
        _subCategoryComboBox.Enabled = !running;
        _parallelNumeric.Enabled = !running;
        _minPriceNumeric.Enabled = !running;
        _maxPriceNumeric.Enabled = !running;
        _openOutputButton.Enabled = !running && HasOutputFolder();
        if (running) _statusLabel.Text = "Çalışıyor...";
    }

    private void AppendLog(string message)
    {
        _logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");

        if (_logTextBox.Lines.Length <= MaxLogLines) return;

        _logTextBox.Lines = _logTextBox.Lines.Skip(_logTextBox.Lines.Length - MaxLogLines).ToArray();
        _logTextBox.SelectionStart = _logTextBox.TextLength;
        _logTextBox.ScrollToCaret();
    }

    private void AddProductRow(ProductResult product)
    {
        if (_decisionStore.IsRejected(product)) { AppendLog($"SKIP {product.Asin} | Kullanıcı kara listesinde"); return; }
        if (string.IsNullOrWhiteSpace(product.Asin) || !_seenAsins.Add(product.Asin)) return;
        _allResults.Add(product); if (PassesCurrentFilter(product)) AddProductRowToGrid(product);
        UpdateLiveStatus();
    }

    private void AddProductRowToGrid(ProductResult product)
    {
        var index = _resultsGrid.Rows.Add(_decisionStore.IsFavorite(product.Asin) ? "⭐" : "", ToFinalDecisionText(product), product.FinalScore, ToSimpleDecision(product), product.Asin, product.Brand, $"${product.Price}", $"${product.NetProfit}", product.NetMarginPercent, product.UploadScore, product.ProfitDecision, product.SearchKeyword, product.Title, product.ProductUrl);
        var row = _resultsGrid.Rows[index]; row.Tag = product;
        row.DefaultCellStyle.BackColor = product.FinalScore switch { >= 93 => Color.Honeydew, >= 86 => Color.LightGreen, >= 74 => Color.LightYellow, >= 65 => Color.Moccasin, _ => Color.MistyRose };
    }

    private static string ToFinalDecisionText(ProductResult product)
    {
        return product.FinalDecision switch
        {
            "Buy Immediately" => "🟢 Kaçırma",
            "Upload" => "🟢 Listele",
            "Review" => "🟡 İncele",
            "Watch" => "🟠 İzle",
            "Reject" => "🔴 Geç",
            _ => string.IsNullOrWhiteSpace(product.FinalDecision) ? ToSimpleDecision(product) : product.FinalDecision
        };
    }

    private static string ToSimpleDecision(ProductResult product)
    {
        if (product.UploadDecision.Equals("Upload", StringComparison.OrdinalIgnoreCase) || product.Recommendation.Equals("Upload", StringComparison.OrdinalIgnoreCase)) return "🟢 Listele";
        if (product.UploadDecision.Equals("Reject", StringComparison.OrdinalIgnoreCase) || product.Recommendation.Equals("Reject", StringComparison.OrdinalIgnoreCase)) return "🔴 Geç";
        return "🟡 İncele";
    }

    private void UpdateLiveStatus()
    {
        var profit = _allResults.Sum(x => x.NetProfit);
        var avgQuality = _allResults.Count == 0 ? 0m : Math.Round(_allResults.Average(AverageQuality), 2);
        var usefulCount = _allResults.Count(IsUsefulCandidate);
        _statusLabel.Text = $"Görünen: {_resultsGrid.Rows.Count} / Aday ASIN: {usefulCount} / Toplam: {_allResults.Count} | Quality: {avgQuality:0.00} | Net: ${profit:0.00} | Süre: {FormatElapsed()}";
    }

    private void RememberOutputFolder(SearchRunResult result)
    {
        var path = FirstNonEmpty(result.ExcelPath, result.SmartQueuePath, result.OutputPath, result.SummaryPath);
        _lastOutputDirectory = string.IsNullOrWhiteSpace(path) ? null : Path.GetDirectoryName(path);
        _openOutputButton.Enabled = HasOutputFolder();
    }

    private bool HasOutputFolder() => !string.IsNullOrWhiteSpace(_lastOutputDirectory) && Directory.Exists(_lastOutputDirectory);

    private void OpenOutputFolder()
    {
        if (!HasOutputFolder())
        {
            MessageBox.Show("Henüz açılacak rapor klasörü yok. Önce bir tarama çalıştırın.", "MarketProHunter", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = _lastOutputDirectory!, UseShellExecute = true });
    }

    private void OpenSelectedProductUrl()
    {
        var product = GetSelectedProduct();
        if (product is null)
        {
            MessageBox.Show("Önce sonuç listesinden bir ASIN seçin.", "MarketProHunter", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(product.ProductUrl))
        {
            MessageBox.Show("Seçili ASIN için açılacak Amazon URL bilgisi yok.", "MarketProHunter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = product.ProductUrl, UseShellExecute = true });
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static decimal CalculateAcceptanceRate(int acceptedCount, int scannedCount)
    {
        return scannedCount <= 0 ? 0m : Math.Round(acceptedCount * 100m / scannedCount, 2);
    }

    private static decimal AverageQuality(ProductResult p)
    {
        return Math.Round((p.TitleQualityScore + p.ImageQualityScore + p.ContentQualityScore + p.BulletPointQualityScore + p.DescriptionQualityQualityScore + p.SpecificationQualityScore) / 6m, 2);
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
        var reasons = new List<string> { p.IsAmazonChoice ? "+ Amazon Choice" : "- Amazon Choice değil", p.HasLowStockWarning ? "- Stok az uyarısı var" : "+ Stok uyarısı yok", p.HasUsuallyKeepItemText ? "+ Usually keep bilgisi var" : "+ Usually keep bilgisi yok", p.IsSponsored ? "- Sponsored sonuç" : "+ Organic sonuç", p.Price <= 60 ? "+ Amazon fiyatı iyi aralıkta" : "- Amazon fiyatı üst aralıkta", p.Rating >= 4.3m ? "+ Rating güçlü" : "- Rating zayıf veya okunamadı", p.ReviewCount >= 100 ? "+ Yorum sayısı güven veriyor" : "- Yorum sayısı düşük veya okunamadı", p.CompetitionScore <= 45 ? "+ Rekabet düşük/orta" : "- Rekabet yüksek olabilir", p.ImageCount >= 4 ? "+ Görsel seti yeterli" : "- Görsel sayısı eksik", p.VisualRiskLevel == "LOW" ? "+ Görsel risk düşük" : "- Görsel kontrol gerekli", p.BrandScore >= 70 ? "+ Marka riski kabul edilebilir" : "- Marka riski kontrol edilmeli", p.UploadScore >= 88 ? "+ Upload Score güçlü" : "- Upload Score izleme gerektiriyor", p.ProfitDecision == "Profitable" ? "+ eBay kâr hedefini karşılıyor" : "- eBay kârı düşük" };
        return $"Favorite: {(isFavorite ? "YES ⭐" : "NO")}{Environment.NewLine}" +
               $"FINAL: {ToFinalDecisionText(p)} | Score: {p.FinalScore}/100 | Tier: {p.FinalTier}{Environment.NewLine}" +
               $"Final Reason: {p.FinalReason}{Environment.NewLine}" +
               $"KARAR: {ToSimpleDecision(p)}{Environment.NewLine}Upload Score: {p.UploadScore}/100 | Brand Score: {p.BrandScore}/100 {p.BrandLevel} | Competition: {p.CompetitionScore}/100{Environment.NewLine}" +
               $"Brand Action: {p.BrandAction} | Brand Notes: {p.BrandProfileNotes}{Environment.NewLine}" +
               $"Listing Quality: Title {p.TitleQualityScore}/100 | Images {p.ImageQualityScore}/100 | Content {p.ContentQualityScore}/100{Environment.NewLine}" +
               $"Product Page Quality: Bullets {p.BulletPointQualityScore}/100 ({p.BulletPointCount}) | Description {p.DescriptionQualityScore}/100 | Specs {p.SpecificationQualityScore}/100 ({p.SpecificationCount}) | A+ {(p.HasAPlusContent ? "YES" : "NO")}{Environment.NewLine}" +
               $"Quality Notes: {p.ListingQualityNotes}{Environment.NewLine}" +
               $"Page Notes: {p.ProductPageQualityNotes}{Environment.NewLine}" +
               $"Visual Risk: {p.VisualRiskLevel} | Images: {p.ImageCount}/6 | {p.VisualRiskNotes}{Environment.NewLine}" +
               $"ASIN: {p.Asin}{Environment.NewLine}Brand: {p.Brand}{Environment.NewLine}Amazon Price: ${p.Price}{Environment.NewLine}" +
               $"Rating: {p.Rating}/5 | Reviews: {p.ReviewCount}{Environment.NewLine}" +
               $"Recommended eBay Price: ${p.RecommendedSalePrice}{Environment.NewLine}Net Profit: ${p.NetProfit}{Environment.NewLine}Net Margin: {p.NetMarginPercent}%{Environment.NewLine}" +
               $"Profit Decision: {p.ProfitDecision}{Environment.NewLine}eBay Fee: ${p.EbayFee}{Environment.NewLine}Promoted Fee: ${p.PromotedFee}{Environment.NewLine}" +
               $"Alt Arama: {p.SearchKeyword}{Environment.NewLine}URL: {p.ProductUrl}{Environment.NewLine}{Environment.NewLine}" +
               $"Image 1: {p.ImageUrl1}{Environment.NewLine}Image 2: {p.ImageUrl2}{Environment.NewLine}Image 3: {p.ImageUrl3}{Environment.NewLine}Image 4: {p.ImageUrl4}{Environment.NewLine}Image 5: {p.ImageUrl5}{Environment.NewLine}Image 6: {p.ImageUrl6}{Environment.NewLine}{Environment.NewLine}" +
               $"Confidence: {p.ConfidenceScore}%{Environment.NewLine}Safety: {p.SafetyScore}/100{Environment.NewLine}Sales: {p.SalesScore}/100{Environment.NewLine}Profit Score: {p.ProfitScore}/100{Environment.NewLine}" +
               $"Overall: {p.OverallScore}/100 {p.Stars}{Environment.NewLine}Recommendation: {p.Recommendation}{Environment.NewLine}{Environment.NewLine}" +
               "Neden bu karar?" + Environment.NewLine + string.Join(Environment.NewLine, reasons) + Environment.NewLine + Environment.NewLine + $"Title:{Environment.NewLine}{p.Title}";
    }
}
