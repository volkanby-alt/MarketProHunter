using System.Diagnostics;
using MarketProHunter.Amazon;
using MarketProHunter.Ebay;
using MarketProHunter.Models;

namespace MarketProHunter.UI;

public sealed class MainFormV2 : Form
{
    private readonly Button _chooseTargetButton = new();
    private readonly Button _startButton = new();
    private readonly Button _ebayButton = new();
    private readonly Button _openProductButton = new();
    private readonly NumericUpDown _minPrice = new();
    private readonly NumericUpDown _maxPrice = new();
    private readonly TextBox _zipText = new();
    private readonly CheckBox _amazonChoice = new();
    private readonly CheckBox _excludeLowStock = new();
    private readonly CheckBox _excludeSponsored = new();
    private readonly Label _targetLabel = new();
    private readonly Label _statusLabel = new();
    private readonly ProgressBar _progress = new();
    private readonly DataGridView _grid = new();
    private readonly TextBox _log = new();

    private SearchTarget? _target;
    private CancellationTokenSource? _cancellation;

    public MainFormV2()
    {
        Text = "MarketProHunter V2 - Mağaza ve Ürün Tarama";
        Width = 1500;
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

        _startButton.Text = "2. Amazon Tara";
        _startButton.Click += async (_, _) => await ToggleScanAsync();

        _ebayButton.Text = "3. eBay Fiyat Kontrolü";
        _ebayButton.Click += async (_, _) => await RunEbayCheckAsync();

        _openProductButton.Text = "Amazon'da Aç";
        _openProductButton.Click += (_, _) => OpenSelectedProduct();

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
        panel.Controls.Add(_ebayButton, 7, 0);
        panel.SetColumnSpan(_ebayButton, 2);
        panel.Controls.Add(_statusLabel, 9, 0);

        panel.Controls.Add(_amazonChoice, 3, 1);
        panel.Controls.Add(_excludeLowStock, 4, 1);
        panel.Controls.Add(_excludeSponsored, 5, 1);
        panel.Controls.Add(_openProductButton, 7, 1);

        _statusLabel.Text = "Hazır";
        _statusLabel.AutoSize = true;
        _statusLabel.Anchor = AnchorStyles.Left;

        return panel;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.MultiSelect = true;
        _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
        _grid.RowHeadersVisible = true;

        AddReadOnlyColumn("asin", "ASIN");
        AddReadOnlyColumn("brand", "Marka");
        AddReadOnlyColumn("price", "Amazon Fiyatı");
        AddReadOnlyColumn("rating", "Puan");
        AddReadOnlyColumn("reviews", "Yorum");
        AddReadOnlyColumn("choice", "Choice");
        AddReadOnlyColumn("ebayStatus", "eBay Durumu");
        AddReadOnlyColumn("ebayRange", "eBay Fiyat Aralığı");
        AddReadOnlyColumn("page", "Sayfa");
        AddReadOnlyColumn("title", "Başlık");
        AddReadOnlyColumn("url", "URL");

        _grid.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                CopySelectedCells();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };

        var copyMenu = new ContextMenuStrip();
        copyMenu.Items.Add("Kopyala", null, (_, _) => CopySelectedCells());
        _grid.ContextMenuStrip = copyMenu;
    }

    private void CopySelectedCells()
    {
        var cells = _grid.SelectedCells.Cast<DataGridViewCell>()
            .Where(cell => cell.RowIndex >= 0 && cell.ColumnIndex >= 0)
            .OrderBy(cell => cell.RowIndex)
            .ThenBy(cell => cell.ColumnIndex)
            .ToList();

        if (cells.Count == 0) return;

        var lines = cells
            .GroupBy(cell => cell.RowIndex)
            .Select(row => string.Join("\t", row.Select(cell => cell.Value?.ToString() ?? string.Empty)));
        var text = string.Join(Environment.NewLine, lines);

        if (string.IsNullOrEmpty(text)) return;
        Clipboard.SetText(text);
        _statusLabel.Text = $"Kopyalandı: {cells.Count} hücre";
    }

    private void AddReadOnlyColumn(string name, string header)
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            ReadOnly = true
        });
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
        _log.Clear();
        AppendLog("Amazon taraması başlatılıyor: " + BuildTargetSummary(_target));
        SetRunning(true, "Amazon taranıyor");
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
            AppendLog($"Amazon taraması tamamlandı. Benzersiz ASIN: {products.Count}");
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

    private async Task RunEbayCheckAsync()
    {
        if (_cancellation is not null)
        {
            _cancellation.Cancel();
            return;
        }

        if (_grid.Rows.Count == 0)
        {
            MessageBox.Show("Önce Amazon ürünlerini tarayın.", "MarketProHunter", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var selectedRowIndexes = _grid.SelectedCells.Cast<DataGridViewCell>()
            .Select(cell => cell.RowIndex)
            .Where(index => index >= 0)
            .Distinct()
            .OrderBy(index => index)
            .ToList();

        var rows = selectedRowIndexes.Count > 0
            ? selectedRowIndexes.Select(index => _grid.Rows[index]).ToList()
            : _grid.Rows.Cast<DataGridViewRow>().Where(row => !row.IsNewRow).ToList();

        var scopeText = selectedRowIndexes.Count > 0 ? "seçili" : "tüm";
        AppendLog($"eBay fiyat kontrolü başladı: {rows.Count} {scopeText} ürün.");
        SetRunning(true, "eBay kontrol ediliyor");
        _cancellation = new CancellationTokenSource();

        try
        {
            using var scanner = new EbayPriceScanner();
            var completed = 0;

            foreach (var row in rows)
            {
                _cancellation.Token.ThrowIfCancellationRequested();
                if (row.Tag is not ProductResult product) continue;

                completed++;
                _statusLabel.Text = $"eBay: {completed}/{rows.Count}";
                AppendLog($"eBay kontrolü {completed}/{rows.Count}: {product.Asin} | {product.Title}");

                var result = await scanner.ScanAsync(product, _cancellation.Token);
                row.Cells["ebayStatus"].Value = result.StatusText;
                row.Cells["ebayRange"].Value = result.PriceRangeText;
                row.Cells["ebayStatus"].ToolTipText = result.Error ?? result.SearchUrl;
                row.Cells["ebayRange"].ToolTipText = result.SearchUrl;

                AppendLog(result.Error is null
                    ? $"{product.Asin}: {result.StatusText} | {result.PriceRangeText}"
                    : $"{product.Asin}: eBay hatası | {result.Error}");
            }

            _statusLabel.Text = $"eBay bitti: {completed}";
            AppendLog($"eBay fiyat kontrolü tamamlandı: {completed} ürün.");
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "Durduruldu";
            AppendLog("eBay fiyat kontrolü durduruldu.");
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Hata";
            AppendLog("eBay HATA: " + ex.Message);
            MessageBox.Show(ex.Message, "eBay Kontrol Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            product.Asin,
            product.Brand,
            $"${product.Price:0.00}",
            product.Rating,
            product.ReviewCount,
            product.IsAmazonChoice ? "Evet" : "Hayır",
            "Bekliyor",
            "-",
            product.Page,
            product.Title,
            product.ProductUrl);
        _grid.Rows[index].Tag = product;
    }

    private void OpenSelectedProduct()
    {
        var product = _grid.CurrentRow?.Tag as ProductResult;
        if (product is null || string.IsNullOrWhiteSpace(product.ProductUrl)) return;
        Process.Start(new ProcessStartInfo(product.ProductUrl) { UseShellExecute = true });
    }

    private void SetRunning(bool running, string? runningText = null)
    {
        _progress.Visible = running;
        _chooseTargetButton.Enabled = !running;
        _minPrice.Enabled = !running;
        _maxPrice.Enabled = !running;
        _zipText.Enabled = !running;
        _amazonChoice.Enabled = !running;
        _excludeLowStock.Enabled = !running;
        _excludeSponsored.Enabled = !running;
        _ebayButton.Enabled = !running || _cancellation is not null;
        _startButton.Text = running ? "Durdur" : "2. Amazon Tara";
        if (running) _statusLabel.Text = runningText ?? "Çalışıyor";
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
