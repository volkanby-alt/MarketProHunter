using MarketProHunter.Amazon;
using MarketProHunter.Categories;
using MarketProHunter.Decisions;
using MarketProHunter.Models;

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
    private readonly ComboBox _recommendationFilter = new();
    private readonly NumericUpDown _minOverallFilter = new();
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

    public MainForm()
    {
        Text = "MarketProHunter - Smart Product Analyzer";
        Width = 1380;
        Height = 900;
        StartPosition = FormStartPosition.CenterScreen;
        BuildLayout();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(10)
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 115));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));

        var settingsPanel = BuildSettingsPanel();
        var categoryPanel = BuildCategoryPanel();
        var filterPanel = BuildFilterPanel();

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

        var detailPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        detailPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        detailPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detailPanel.Controls.Add(BuildDecisionButtonPanel(), 0, 0);
        detailPanel.Controls.Add(_detailTextBox, 0, 1);

        var resultSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 930
        };
        resultSplit.Panel1.Controls.Add(_resultsGrid);
        resultSplit.Panel2.Controls.Add(detailPanel);

        _logTextBox.Dock = DockStyle.Fill;
        _logTextBox.Multiline = true;
        _logTextBox.ScrollBars = ScrollBars.Vertical;
        _logTextBox.ReadOnly = true;

        root.Controls.Add(settingsPanel, 0, 0);
        root.Controls.Add(categoryPanel, 0, 1);
        root.Controls.Add(filterPanel, 0, 2);
        root.Controls.Add(_progressBar, 0, 3);
        root.Controls.Add(resultSplit, 0, 4);
        root.Controls.Add(_logTextBox, 0, 5);

        Controls.Add(root);
    }

    private FlowLayoutPanel BuildDecisionButtonPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(2)
        };

        _favoriteButton.Text = "⭐ Favorite";
        _favoriteButton.Width = 100;
        _favoriteButton.Click += (_, _) => FavoriteSelectedProduct();

        _rejectAsinButton.Text = "Reject ASIN";
        _rejectAsinButton.Width = 110;
        _rejectAsinButton.Click += (_, _) => RejectSelectedAsin();

        _rejectBrandButton.Text = "Reject Brand";
        _rejectBrandButton.Width = 115;
        _rejectBrandButton.Click += (_, _) => RejectSelectedBrand();

        panel.Controls.Add(_favoriteButton);
        panel.Controls.Add(_rejectAsinButton);
        panel.Controls.Add(_rejectBrandButton);
        return panel;
    }

    private TableLayoutPanel BuildSettingsPanel()
    {
        var settingsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 9,
            RowCount = 2
        };

        for (var i = 0; i < 9; i++)
        {
            settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11f));
        }

        _keywordTextBox.PlaceholderText = "İsteğe bağlı ekstra arama kelimesi";
        _zipTextBox.Text = "07073";
        _pagesNumeric.Minimum = 1;
        _pagesNumeric.Maximum = 100;
        _pagesNumeric.Value = 3;
        _parallelNumeric.Minimum = 1;
        _parallelNumeric.Maximum = 8;
        _parallelNumeric.Value = 3;
        _minPriceNumeric.Minimum = 1;
        _minPriceNumeric.Maximum = 1000;
        _minPriceNumeric.Value = 9;
        _maxPriceNumeric.Minimum = 1;
        _maxPriceNumeric.Maximum = 1000;
        _maxPriceNumeric.Value = 98;
        _amazonChoiceCheckBox.Text = "Amazon Choice";
        _amazonChoiceCheckBox.Checked = true;
        _lowStockCheckBox.Text = "Az stok ele";
        _lowStockCheckBox.Checked = true;
        _usuallyKeepCheckBox.Text = "Usually keep ele";
        _usuallyKeepCheckBox.Checked = true;

        AddLabeledControl(settingsPanel, "Ekstra Arama", _keywordTextBox, 0, 0);
        AddLabeledControl(settingsPanel, "Sayfa", _pagesNumeric, 1, 0);
        AddLabeledControl(settingsPanel, "Paralel", _parallelNumeric, 2, 0);
        AddLabeledControl(settingsPanel, "Min $", _minPriceNumeric, 3, 0);
        AddLabeledControl(settingsPanel, "Max $", _maxPriceNumeric, 4, 0);
        AddLabeledControl(settingsPanel, "ZIP", _zipTextBox, 5, 0);

        settingsPanel.Controls.Add(_amazonChoiceCheckBox, 6, 1);
        settingsPanel.Controls.Add(_lowStockCheckBox, 7, 1);
        settingsPanel.Controls.Add(_usuallyKeepCheckBox, 8, 1);

        _startButton.Text = "Başlat";
        _startButton.Height = 30;
        _startButton.Click += async (_, _) => await StartSearchAsync();

        _stopButton.Text = "Durdur";
        _stopButton.Height = 30;
        _stopButton.Enabled = false;
        _stopButton.Click += (_, _) => _cancellationTokenSource?.Cancel();

        settingsPanel.Controls.Add(_startButton, 6, 0);
        settingsPanel.Controls.Add(_stopButton, 7, 0);

        _statusLabel.Text = "Hazır";
        _statusLabel.AutoSize = true;
        settingsPanel.Controls.Add(_statusLabel, 8, 0);
        return settingsPanel;
    }

    private GroupBox BuildCategoryPanel()
    {
        var categoryPanel = new GroupBox
        {
            Text = "Kategoriler - seçilenlerin anahtar kelimeleri otomatik taranır",
            Dock = DockStyle.Fill
        };

        _categoryListBox.Dock = DockStyle.Fill;
        _categoryListBox.CheckOnClick = true;
        _categoryListBox.MultiColumn = true;
        _categoryListBox.ColumnWidth = 230;

        foreach (var category in _categories)
        {
            _categoryListBox.Items.Add(category.Name, category.Name is "Home & Kitchen" or "Automotive" or "Tools & Home Improvement" or "Beauty & Personal Care");
        }

        categoryPanel.Controls.Add(_categoryListBox);
        return categoryPanel;
    }

    private GroupBox BuildFilterPanel()
    {
        var group = new GroupBox
        {
            Text = "Akıllı analiz filtreleri",
            Dock = DockStyle.Fill
        };

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            Padding = new Padding(5)
        };

        for (var i = 0; i < 6; i++)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66f));
        }

        _recommendationFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _recommendationFilter.Items.AddRange(new object[] { "All", "Upload", "Review", "Caution", "Reject", "Favorite" });
        _recommendationFilter.SelectedIndex = 0;

        _minOverallFilter.Minimum = 0;
        _minOverallFilter.Maximum = 100;
        _minOverallFilter.Value = 0;

        _applyFilterButton.Text = "Filtrele";
        _applyFilterButton.Click += (_, _) => RefreshGrid();

        _clearFilterButton.Text = "Temizle";
        _clearFilterButton.Click += (_, _) =>
        {
            _recommendationFilter.SelectedIndex = 0;
            _minOverallFilter.Value = 0;
            RefreshGrid();
        };

        AddLabeledControl(panel, "Recommendation", _recommendationFilter, 0, 0);
        AddLabeledControl(panel, "Min Overall", _minOverallFilter, 1, 0);
        panel.Controls.Add(_applyFilterButton, 2, 0);
        panel.Controls.Add(_clearFilterButton, 3, 0);
        group.Controls.Add(panel);
        return group;
    }

    private static void AddLabeledControl(TableLayoutPanel panel, string label, Control control, int column, int row)
    {
        var container = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Margin = new Padding(4)
        };

        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        container.Controls.Add(new Label { Text = label, AutoSize = true }, 0, 0);
        control.Dock = DockStyle.Fill;
        container.Controls.Add(control, 0, 1);
        panel.Controls.Add(container, column, row);
    }

    private void ConfigureResultsGrid()
    {
        _resultsGrid.Dock = DockStyle.Fill;
        _resultsGrid.ReadOnly = true;
        _resultsGrid.AllowUserToAddRows = false;
        _resultsGrid.AllowUserToDeleteRows = false;
        _resultsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _resultsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _resultsGrid.SelectionChanged += (_, _) => ShowSelectedProductDetails();

        _resultsGrid.Columns.Add("fav", "Fav");
        _resultsGrid.Columns.Add("overall", "Overall");
        _resultsGrid.Columns.Add("rec", "Rec");
        _resultsGrid.Columns.Add("stars", "Stars");
        _resultsGrid.Columns.Add("safety", "Safety");
        _resultsGrid.Columns.Add("sales", "Sales");
        _resultsGrid.Columns.Add("profit", "Profit");
        _resultsGrid.Columns.Add("asin", "ASIN");
        _resultsGrid.Columns.Add("title", "Title");
        _resultsGrid.Columns.Add("brand", "Brand");
        _resultsGrid.Columns.Add("price", "Price");
        _resultsGrid.Columns.Add("keyword", "Keyword");
        _resultsGrid.Columns.Add("url", "URL");
    }

    private async Task StartSearchAsync()
    {
        var keywords = BuildKeywordList();
        if (keywords.Count == 0)
        {
            MessageBox.Show("En az bir kategori seçin veya ekstra arama kelimesi yazın.", "MarketProHunter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _allResults.Clear();
        _resultsGrid.Rows.Clear();
        _logTextBox.Clear();
        _detailTextBox.Text = "Tarama başladı...";
        AppendLog($"Toplam anahtar kelime: {keywords.Count}");
        AppendLog($"Paralel görev sayısı: {_parallelNumeric.Value}");
        SetRunningState(true);
        _cancellationTokenSource = new CancellationTokenSource();

        var settings = new SearchSettings
        {
            ZipCode = _zipTextBox.Text.Trim(),
            MinPrice = _minPriceNumeric.Value,
            MaxPrice = _maxPriceNumeric.Value,
            MaxParallelSearches = (int)_parallelNumeric.Value,
            RequireAmazonChoice = _amazonChoiceCheckBox.Checked,
            ExcludeLowStock = _lowStockCheckBox.Checked,
            ExcludeUsuallyKeepItem = _usuallyKeepCheckBox.Checked
        };

        var service = new AmazonSearchService();
        var logProgress = new Progress<string>(AppendLog);
        var productProgress = new Progress<ProductResult>(AddProductRow);

        try
        {
            var result = await service.RunManyAsync(
                keywords,
                (int)_pagesNumeric.Value,
                settings,
                logProgress,
                productProgress,
                _cancellationTokenSource.Token);

            _statusLabel.Text = $"Bitti: {result.AcceptedCount} uygun ürün";
            AppendLog($"CSV dosyası: {result.OutputPath}");
            MessageBox.Show($"Tarama bitti. Uygun ürün: {result.AcceptedCount}", "MarketProHunter", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "Durduruldu";
            AppendLog("Tarama kullanıcı tarafından durduruldu.");
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Hata";
            AppendLog("HATA: " + ex.Message);
            MessageBox.Show(ex.Message, "MarketProHunter Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetRunningState(false);
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private IReadOnlyList<string> BuildKeywordList()
    {
        var keywords = new List<string>();
        foreach (var checkedItem in _categoryListBox.CheckedItems)
        {
            var categoryName = checkedItem.ToString();
            var category = _categories.FirstOrDefault(x => x.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
            if (category is not null)
            {
                keywords.AddRange(category.Keywords);
            }
        }

        if (!string.IsNullOrWhiteSpace(_keywordTextBox.Text))
        {
            keywords.AddRange(_keywordTextBox.Text.Split(new[] { ',', ';', Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return keywords.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private bool PassesCurrentFilter(ProductResult product)
    {
        if (_decisionStore.IsRejected(product)) return false;
        var selected = _recommendationFilter.SelectedItem?.ToString() ?? "All";
        if (selected == "Favorite") return _decisionStore.IsFavorite(product.Asin);
        if (selected != "All" && !product.Recommendation.Equals(selected, StringComparison.OrdinalIgnoreCase)) return false;
        return product.OverallScore >= _minOverallFilter.Value;
    }

    private void RefreshGrid()
    {
        _resultsGrid.Rows.Clear();
        foreach (var product in _allResults.Where(PassesCurrentFilter).OrderByDescending(p => p.OverallScore))
        {
            AddProductRowToGrid(product);
        }
        _statusLabel.Text = $"Görünen: {_resultsGrid.Rows.Count} / Toplam: {_allResults.Count}";
    }

    private void SetRunningState(bool running)
    {
        _startButton.Enabled = !running;
        _stopButton.Enabled = running;
        _progressBar.Visible = running;
        _categoryListBox.Enabled = !running;
        _parallelNumeric.Enabled = !running;
        if (running) _statusLabel.Text = "Çalışıyor...";
    }

    private void AppendLog(string message) => _logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");

    private void AddProductRow(ProductResult product)
    {
        if (_decisionStore.IsRejected(product))
        {
            AppendLog($"SKIP {product.Asin} | Kullanıcı kara listesinde");
            return;
        }

        _allResults.Add(product);
        if (PassesCurrentFilter(product)) AddProductRowToGrid(product);
    }

    private void AddProductRowToGrid(ProductResult product)
    {
        var index = _resultsGrid.Rows.Add(
            _decisionStore.IsFavorite(product.Asin) ? "⭐" : "",
            product.OverallScore,
            product.Recommendation,
            product.Stars,
            product.SafetyScore,
            product.SalesScore,
            product.ProfitScore,
            product.Asin,
            product.Title,
            product.Brand,
            $"${product.Price}",
            product.SearchKeyword,
            product.ProductUrl);

        var row = _resultsGrid.Rows[index];
        row.Tag = product;
        row.DefaultCellStyle.BackColor = product.OverallScore switch
        {
            >= 90 => Color.Honeydew,
            >= 75 => Color.LightYellow,
            >= 60 => Color.Moccasin,
            _ => Color.MistyRose
        };
    }

    private ProductResult? GetSelectedProduct() => _resultsGrid.SelectedRows.Count == 0 ? null : _resultsGrid.SelectedRows[0].Tag as ProductResult;

    private void FavoriteSelectedProduct()
    {
        var product = GetSelectedProduct();
        if (product is null) return;
        _decisionStore.AddFavorite(product);
        AppendLog($"FAVORITE {product.Asin} | {product.Brand}");
        RefreshGrid();
    }

    private void RejectSelectedAsin()
    {
        var product = GetSelectedProduct();
        if (product is null) return;
        _decisionStore.RejectAsin(product);
        AppendLog($"REJECT ASIN {product.Asin}");
        RefreshGrid();
    }

    private void RejectSelectedBrand()
    {
        var product = GetSelectedProduct();
        if (product is null) return;
        if (MessageBox.Show($"{product.Brand} markasını kalıcı reddetmek istiyor musun?", "Reject Brand", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _decisionStore.RejectBrand(product);
        AppendLog($"REJECT BRAND {product.Brand}");
        RefreshGrid();
    }

    private void ShowSelectedProductDetails()
    {
        var product = GetSelectedProduct();
        if (product is null) return;
        _detailTextBox.Text = BuildDetailText(product, _decisionStore.IsFavorite(product.Asin));
    }

    private static string BuildDetailText(ProductResult product, bool isFavorite)
    {
        var reasons = new List<string>
        {
            product.IsAmazonChoice ? "+ Amazon Choice" : "- Amazon Choice değil",
            product.HasLowStockWarning ? "- Stok az uyarısı var" : "+ Stok uyarısı yok",
            product.HasUsuallyKeepItemText ? "- Usually keep uyarısı var" : "+ Usually keep uyarısı yok",
            product.IsSponsored ? "- Sponsored sonuç" : "+ Organic sonuç",
            product.Price <= 60 ? "+ Fiyat iyi aralıkta" : "- Fiyat üst aralıkta"
        };

        return $"Favorite: {(isFavorite ? "YES ⭐" : "NO")}{Environment.NewLine}" +
               $"ASIN: {product.Asin}{Environment.NewLine}" +
               $"Brand: {product.Brand}{Environment.NewLine}" +
               $"Price: ${product.Price}{Environment.NewLine}" +
               $"Keyword: {product.SearchKeyword}{Environment.NewLine}" +
               $"URL: {product.ProductUrl}{Environment.NewLine}{Environment.NewLine}" +
               $"Safety: {product.SafetyScore}/100{Environment.NewLine}" +
               $"Sales: {product.SalesScore}/100{Environment.NewLine}" +
               $"Profit: {product.ProfitScore}/100{Environment.NewLine}" +
               $"Overall: {product.OverallScore}/100 {product.Stars}{Environment.NewLine}" +
               $"Recommendation: {product.Recommendation}{Environment.NewLine}{Environment.NewLine}" +
               "Neden bu puan?" + Environment.NewLine +
               string.Join(Environment.NewLine, reasons) + Environment.NewLine + Environment.NewLine +
               $"Title:{Environment.NewLine}{product.Title}";
    }
}
