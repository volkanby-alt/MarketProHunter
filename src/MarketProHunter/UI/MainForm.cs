using MarketProHunter.Amazon;
using MarketProHunter.Categories;
using MarketProHunter.Models;

namespace MarketProHunter.UI;

public sealed class MainForm : Form
{
    private readonly TextBox _keywordTextBox = new();
    private readonly CheckedListBox _categoryListBox = new();
    private readonly NumericUpDown _pagesNumeric = new();
    private readonly NumericUpDown _minPriceNumeric = new();
    private readonly NumericUpDown _maxPriceNumeric = new();
    private readonly TextBox _zipTextBox = new();
    private readonly CheckBox _amazonChoiceCheckBox = new();
    private readonly CheckBox _lowStockCheckBox = new();
    private readonly CheckBox _usuallyKeepCheckBox = new();
    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();
    private readonly ProgressBar _progressBar = new();
    private readonly TextBox _logTextBox = new();
    private readonly DataGridView _resultsGrid = new();
    private readonly Label _statusLabel = new();
    private readonly IReadOnlyList<KeywordCategory> _categories = KeywordCategoryProvider.GetDefaultCategories();

    private CancellationTokenSource? _cancellationTokenSource;

    public MainForm()
    {
        Text = "MarketProHunter - Amazon Search Engine";
        Width = 1180;
        Height = 820;
        StartPosition = FormStartPosition.CenterScreen;
        BuildLayout();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(10)
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 115));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

        var settingsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
            RowCount = 2
        };

        for (var i = 0; i < 8; i++)
        {
            settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5f));
        }

        _keywordTextBox.Text = "";
        _keywordTextBox.PlaceholderText = "İsteğe bağlı ekstra arama kelimesi";
        _zipTextBox.Text = "07073";
        _pagesNumeric.Minimum = 1;
        _pagesNumeric.Maximum = 100;
        _pagesNumeric.Value = 3;
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
        AddLabeledControl(settingsPanel, "Min $", _minPriceNumeric, 2, 0);
        AddLabeledControl(settingsPanel, "Max $", _maxPriceNumeric, 3, 0);
        AddLabeledControl(settingsPanel, "ZIP", _zipTextBox, 4, 0);

        settingsPanel.Controls.Add(_amazonChoiceCheckBox, 5, 1);
        settingsPanel.Controls.Add(_lowStockCheckBox, 6, 1);
        settingsPanel.Controls.Add(_usuallyKeepCheckBox, 7, 1);

        _startButton.Text = "Başlat";
        _startButton.Height = 30;
        _startButton.Click += async (_, _) => await StartSearchAsync();

        _stopButton.Text = "Durdur";
        _stopButton.Height = 30;
        _stopButton.Enabled = false;
        _stopButton.Click += (_, _) => _cancellationTokenSource?.Cancel();

        settingsPanel.Controls.Add(_startButton, 5, 0);
        settingsPanel.Controls.Add(_stopButton, 6, 0);

        _statusLabel.Text = "Hazır";
        _statusLabel.AutoSize = true;
        settingsPanel.Controls.Add(_statusLabel, 7, 0);

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

        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Style = ProgressBarStyle.Marquee;
        _progressBar.Visible = false;

        ConfigureResultsGrid();

        _logTextBox.Dock = DockStyle.Fill;
        _logTextBox.Multiline = true;
        _logTextBox.ScrollBars = ScrollBars.Vertical;
        _logTextBox.ReadOnly = true;

        root.Controls.Add(settingsPanel, 0, 0);
        root.Controls.Add(categoryPanel, 0, 1);
        root.Controls.Add(_progressBar, 0, 2);
        root.Controls.Add(_resultsGrid, 0, 3);
        root.Controls.Add(_logTextBox, 0, 4);

        Controls.Add(root);
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

        _resultsGrid.Rows.Clear();
        _logTextBox.Clear();
        AppendLog($"Toplam anahtar kelime: {keywords.Count}");
        SetRunningState(true);
        _cancellationTokenSource = new CancellationTokenSource();

        var settings = new SearchSettings
        {
            ZipCode = _zipTextBox.Text.Trim(),
            MinPrice = _minPriceNumeric.Value,
            MaxPrice = _maxPriceNumeric.Value,
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
            keywords.AddRange(
                _keywordTextBox.Text
                    .Split(new[] { ',', ';', Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return keywords
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void SetRunningState(bool running)
    {
        _startButton.Enabled = !running;
        _stopButton.Enabled = running;
        _progressBar.Visible = running;
        _categoryListBox.Enabled = !running;
        if (running)
        {
            _statusLabel.Text = "Çalışıyor...";
        }
    }

    private void AppendLog(string message)
    {
        _logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void AddProductRow(ProductResult product)
    {
        _resultsGrid.Rows.Add(product.Asin, product.Title, product.Brand, $"${product.Price}", product.SearchKeyword, product.ProductUrl);
    }
}
