using System.Diagnostics;
using MarketProHunter.Amazon;
using MarketProHunter.Models;

namespace MarketProHunter.UI;

public sealed class MainFormV2 : Form
{
    private readonly Button _chooseTargetButton = new();
    private readonly Button _startButton = new();
    private readonly Button _openProductButton = new();
    private readonly Button _selectAllButton = new();
    private readonly Button _clearSelectionButton = new();
    private readonly NumericUpDown _minPrice = new();
    private readonly NumericUpDown _maxPrice = new();
    private readonly TextBox _zipText = new();
    private readonly CheckBox _amazonChoice = new();
    private readonly CheckBox _excludeLowStock = new();
    private readonly CheckBox _excludeSponsored = new();
    private readonly Label _targetLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Label _selectedCountLabel = new();
    private readonly ProgressBar _progress = new();
    private readonly DataGridView _grid = new();
    private readonly TextBox _log = new();

    private SearchTarget? _target;
    private CancellationTokenSource? _cancellation;

    public MainFormV2()
    {
        Text = "MarketProHunter V2 - Mağaza ve Ürün Tarama";
        Width = 1450;
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
            RowCount = 5,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 30));

        root.Controls.Add(BuildTopPanel(), 0, 0);

        _targetLabel.Dock = DockStyle.Fill;
        _targetLabel.TextAlign = ContentAlignment.MiddleLeft;
        _targetLabel.Font = new Font(Font, FontStyle.Bold);
        _targetLabel.Text = "Arama hedefi seçilmedi.";
        root.Controls.Add(_targetLabel, 0, 1);

        _progress.Dock = DockStyle.Fill;
        _progress.Style = ProgressBarStyle.Marquee;
        _progress.Visible = false;
        root.Controls.Add(_progress, 0, 2);

        ConfigureGrid();
        root.Controls.Add(_grid, 0, 3);

        _log.Dock = DockStyle.Fill;
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.Font = new Font("Consolas", 9);
        root.Controls.Add(_log, 0, 4);

        Controls.Add(root);
    }

    private Control BuildTopPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 10,
            RowCount = 2
        };

        for (var i = 0; i < 10; i++)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10f));
        }

        _chooseTargetButton.Text = "1. Arama Hedefi";
        _chooseTargetButton.Click += async (_, _) => await ChooseTargetAsync(startAfterSelection: true);

        _startButton.Text = "2. Taramayı Başlat";
        _startButton.Click += async (_, _) => await ToggleScanAsync();

        _openProductButton.Text = "Amazon'da Aç";
        _openProductButton.Click += (_, _) => OpenSelectedProduct();

        _selectAllButton.Text = "Tümünü Seç";
        _selectAllButton.Click += (_, _) => SetAllRowsChecked(true);

        _clearSelectionButton.Text = "Seçimi Temizle";
        _clearSelectionButton.Click += (_, _) => SetAllRowsChecked(false);

        ConfigureMoney(_minPrice, 1, 1000, 9);
        ConfigureMoney(_maxPrice, 1, 1000, 98);
        _zipText.Text = "07073";

        _amazonChoice.Text = "Amazon Choice";
        _amazonChoice.Checked = true;
        _excludeLowStock.Text = "Az stok ele";
        _excludeLowStock.Checked = true;
        _excludeSponsored.Text = "Sponsored ele";
        _excludeSponsored.Checked = true;

        AddLabeled(panel, "Min $", _minPrice, 0, 0);
        AddLabeled(panel, "Max $", _maxPrice, 1, 0);
        AddLabeled(panel, "ZIP", _zipText, 2, 0);
        panel.Controls.Add(_chooseTargetButton, 3, 0);
        panel.SetColumnSpan(_chooseTargetButton, 2);
        panel.Controls.Add(_startButton, 5, 0);
        panel.SetColumnSpan(_startButton, 2);
        panel.Controls.Add(_openProductButton, 7, 0);
        panel.Controls.Add(_statusLabel, 8, 0);
        panel.Controls.Add(_selectedCountLabel, 9, 0);

        panel.Controls.Add(_amazonChoice, 3, 1);
        panel.Controls.Add(_excludeLowStock, 4, 1);
        panel.Controls.Add(_excludeSponsored, 5, 1);
        panel.Controls.Add(_selectAllButton, 7, 1);
        panel.Controls.Add(_clearSelectionButton, 8, 1);

        _statusLabel.Text = "Hazır";
        _statusLabel.AutoSize = true;
        _statusLabel.Anchor = AnchorStyles.Left;

        _selectedCountLabel.Text = "Seçili: 0";
        _selectedCountLabel.AutoSize = true;
        _selectedCountLabel.Anchor = AnchorStyles.Left;

        return panel;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.MultiSelect = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.EditMode = DataGridViewEditMode.EditOnEnter;

        var selectColumn = new DataGridViewCheckBoxColumn
        {
            Name = "selected",
            HeaderText = "Seç",
            Width = 45,
            FillWeight = 35,
            ThreeState = false,
            ReadOnly = false
        };
        _grid.Columns.Add(selectColumn);

        AddReadOnlyColumn("asin", "ASIN");
        AddReadOnlyColumn("brand", "Marka");
        AddReadOnlyColumn("price", "Fiyat");
        AddReadOnlyColumn("rating", "Puan");
        AddReadOnlyColumn("reviews", "Yorum");
        AddReadOnlyColumn("choice", "Choice");
        AddReadOnlyColumn("page", "Sayfa");
        AddReadOnlyColumn("title", "Başlık");
        AddReadOnlyColumn("url", "URL");

        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewCheckBoxCell)
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        _grid.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == _grid.Columns["selected"].Index)
            {
                UpdateSelectedCount();
            }
        };
        _grid.CellClick += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex == _grid.Columns["selected"].Index) return;
            var cell = _grid.Rows[e.RowIndex].Cells["selected"];
            cell.Value = !(cell.Value as bool? ?? false);
            UpdateSelectedCount();
        };
    }

    private void AddReadOnlyColumn(string name, string header)
    {
        var column = new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            ReadOnly = true
        };
        _grid.Columns.Add(column);
    }

    private async Task ChooseTargetAsync(bool startAfterSelection)
    {
        if (_cancellation is not null) return;

        using var dialog = new SearchTargetDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Target is null) return;

        _target = dialog.Target;
        _targetLabel.Text = BuildTargetSummary(_target);
        AppendLog("Arama hedefi seçildi: " + _targetLabel.Text);

        if (startAfterSelection)
        {
            await StartScanAsync();
        }
    }

    private async Task ToggleScanAsync()
    {
        if (_cancellation is not null)
        {
            _cancellation.Cancel();
            return;
        }

        if (_target is null)
        {
            await ChooseTargetAsync(startAfterSelection: false);
            if (_target is null) return;
        }

        await StartScanAsync();
    }

    private async Task StartScanAsync()
    {
        if (_target is null || _cancellation is not null) return;

        if (_minPrice.Value > _maxPrice.Value)
        {
            MessageBox.Show("Minimum fiyat maksimum fiyattan büyük olamaz.", "MarketProHunter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _grid.Rows.Clear();
        UpdateSelectedCount();
        _log.Clear();
        AppendLog("Tarama başlatılıyor: " + BuildTargetSummary(_target));
        SetRunning(true);
        _cancellation = new CancellationTokenSource();

        try
        {
            var settings = new SearchSettings
            {
                ZipCode = string.IsNullOrWhiteSpace(_zipText.Text) ? "07073" : _zipText.Text.Trim(),
                MinPrice = _minPrice.Value,
                MaxPrice = _maxPrice.Value,
                RequireAmazonChoice = _amazonChoice.Checked,
                ExcludeLowStock = _excludeLowStock.Checked,
                ExcludeSponsored = _excludeSponsored.Checked,
                MaxParallelSearches = 1
            };

            var progress = new Progress<string>(AppendLog);
            var scanner = new SearchTargetScanner();
            var products = await scanner.ScanAsync(_target, settings, progress, _cancellation.Token);

            foreach (var product in products)
            {
                AddProduct(product);
            }

            _statusLabel.Text = $"Bitti: {products.Count} ASIN";
            AppendLog($"Tarama tamamlandı. Benzersiz ASIN: {products.Count}");
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
            _cancellation?.Dispose();
            _cancellation = null;
            SetRunning(false);
        }
    }

    private void AddProduct(ProductResult product)
    {
        var index = _grid.Rows.Add(
            false,
            product.Asin,
            product.Brand,
            $"${product.Price:0.00}",
            product.Rating,
            product.ReviewCount,
            product.IsAmazonChoice ? "Evet" : "Hayır",
            product.Page,
            product.Title,
            product.ProductUrl);
        _grid.Rows[index].Tag = product;
    }

    private void SetAllRowsChecked(bool isChecked)
    {
        foreach (DataGridViewRow row in _grid.Rows)
        {
            row.Cells["selected"].Value = isChecked;
        }
        _grid.RefreshEdit();
        UpdateSelectedCount();
    }

    private void UpdateSelectedCount()
    {
        var count = _grid.Rows.Cast<DataGridViewRow>()
            .Count(row => row.Cells["selected"].Value as bool? == true);
        _selectedCountLabel.Text = $"Seçili: {count}";
    }

    private void OpenSelectedProduct()
    {
        var checkedRow = _grid.Rows.Cast<DataGridViewRow>()
            .FirstOrDefault(row => row.Cells["selected"].Value as bool? == true);
        var product = checkedRow?.Tag as ProductResult ?? _grid.CurrentRow?.Tag as ProductResult;
        if (product is null || string.IsNullOrWhiteSpace(product.ProductUrl)) return;
        Process.Start(new ProcessStartInfo(product.ProductUrl) { UseShellExecute = true });
    }

    private void SetRunning(bool running)
    {
        _progress.Visible = running;
        _chooseTargetButton.Enabled = !running;
        _minPrice.Enabled = !running;
        _maxPrice.Enabled = !running;
        _zipText.Enabled = !running;
        _amazonChoice.Enabled = !running;
        _excludeLowStock.Enabled = !running;
        _excludeSponsored.Enabled = !running;
        _selectAllButton.Enabled = !running;
        _clearSelectionButton.Enabled = !running;
        _startButton.Text = running ? "Durdur" : "2. Taramayı Başlat";
        if (running) _statusLabel.Text = "Çalışıyor";
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(message));
            return;
        }

        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private static string BuildTargetSummary(SearchTarget target)
    {
        var category = target.Category ?? "Tümü";
        var subCategory = target.SubCategory ?? "Tümü";
        var pages = target.ScanAllPages ? "Hepsi" : target.MaxPages.ToString();
        return $"{target.DisplayName} | Kategori: {category} | Alt kategori: {subCategory} | Sayfa: {pages}";
    }

    private static void ConfigureMoney(NumericUpDown control, decimal min, decimal max, decimal value)
    {
        control.Minimum = min;
        control.Maximum = max;
        control.Value = value;
        control.DecimalPlaces = 2;
    }

    private static void AddLabeled(TableLayoutPanel panel, string label, Control control, int column, int row)
    {
        var box = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        box.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        box.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        box.Controls.Add(new Label { Text = label, AutoSize = true }, 0, 0);
        control.Dock = DockStyle.Fill;
        box.Controls.Add(control, 0, 1);
        panel.Controls.Add(box, column, row);
    }
}
