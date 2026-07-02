using MarketProHunter.Amazon;
using MarketProHunter.Models;

namespace MarketProHunter.UI;

public sealed class MainForm : Form
{
    private readonly TextBox _keywordBox = new();
    private readonly NumericUpDown _pageCountBox = new();
    private readonly Button _startButton = new();
    private readonly Button _cancelButton = new();
    private readonly DataGridView _grid = new();
    private readonly TextBox _logBox = new();
    private readonly Label _statusLabel = new();

    private CancellationTokenSource? _cancellationTokenSource;

    public MainForm()
    {
        Text = "MarketProHunter - Amazon Search Engine";
        Width = 1180;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;

        BuildLayout();
    }

    private void BuildLayout()
    {
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 86,
            Padding = new Padding(12)
        };

        var keywordLabel = new Label
        {
            Text = "Arama kelimesi",
            Left = 12,
            Top = 12,
            Width = 110
        };

        _keywordBox.Left = 12;
        _keywordBox.Top = 36;
        _keywordBox.Width = 320;
        _keywordBox.Text = "home cleaner";

        var pageLabel = new Label
        {
            Text = "Sayfa",
            Left = 350,
            Top = 12,
            Width = 80
        };

        _pageCountBox.Left = 350;
        _pageCountBox.Top = 36;
        _pageCountBox.Width = 80;
        _pageCountBox.Minimum = 1;
        _pageCountBox.Maximum = 100;
        _pageCountBox.Value = 3;

        var infoLabel = new Label
        {
            Text = "ZIP: 07073 | Fiyat: 9-98 USD | Amazon Choice açık | VeRO filtre açık",
            Left = 450,
            Top = 40,
            Width = 420
        };

        _startButton.Text = "Taramayı Başlat";
        _startButton.Left = 900;
        _startButton.Top = 32;
        _startButton.Width = 120;
        _startButton.Click += StartButton_Click;

        _cancelButton.Text = "Durdur";
        _cancelButton.Left = 1030;
        _cancelButton.Top = 32;
        _cancelButton.Width = 90;
        _cancelButton.Enabled = false;
        _cancelButton.Click += CancelButton_Click;

        topPanel.Controls.AddRange(new Control[]
        {
            keywordLabel, _keywordBox, pageLabel, _pageCountBox, infoLabel, _startButton, _cancelButton
        });

        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.ReadOnly = true;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.Columns.Add("Asin", "ASIN");
        _grid.Columns.Add("Title", "Ürün Başlığı");
        _grid.Columns.Add("Brand", "Marka");
        _grid.Columns.Add("Price", "Fiyat");
        _grid.Columns.Add("Url", "Amazon Link");

        _logBox.Dock = DockStyle.Bottom;
        _logBox.Height = 180;
        _logBox.Multiline = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.ReadOnly = true;

        _statusLabel.Dock = DockStyle.Bottom;
        _statusLabel.Height = 28;
        _statusLabel.Text = "Hazır";
        _statusLabel.Padding = new Padding(8, 6, 0, 0);

        Controls.Add(_grid);
        Controls.Add(_statusLabel);
        Controls.Add(_logBox);
        Controls.Add(topPanel);
    }

    private async void StartButton_Click(object? sender, EventArgs e)
    {
        var keyword = _keywordBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            MessageBox.Show("Arama kelimesi yazmalısın.", "Eksik Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _grid.Rows.Clear();
        _logBox.Clear();
        SetRunningState(true);

        _cancellationTokenSource = new CancellationTokenSource();

        var service = new AmazonSearchService();
        var settings = SearchSettings.Default;
        var logProgress = new Progress<string>(AppendLog);
        var productProgress = new Progress<ProductResult>(AddProductRow);

        try
        {
            var result = await service.RunAsync(
                keyword,
                (int)_pageCountBox.Value,
                settings,
                logProgress,
                productProgress,
                _cancellationTokenSource.Token);

            _statusLabel.Text = $"Bitti. Taranan: {result.ScannedCount} | Kabul: {result.AcceptedCount} | Elenen: {result.SkippedCount}";
            AppendLog($"CSV dosyası oluşturuldu: {result.OutputPath}");
            MessageBox.Show($"Tarama bitti.\nKabul edilen ürün: {result.AcceptedCount}\nCSV: {result.OutputPath}", "Bitti", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "Tarama durduruldu.";
            AppendLog("Tarama kullanıcı tarafından durduruldu.");
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Hata oluştu.";
            AppendLog("HATA: " + ex.Message);
            MessageBox.Show(ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            SetRunningState(false);
        }
    }

    private void CancelButton_Click(object? sender, EventArgs e)
    {
        _cancellationTokenSource?.Cancel();
    }

    private void SetRunningState(bool running)
    {
        _startButton.Enabled = !running;
        _cancelButton.Enabled = running;
        _keywordBox.Enabled = !running;
        _pageCountBox.Enabled = !running;
        _statusLabel.Text = running ? "Tarama çalışıyor..." : _statusLabel.Text;
    }

    private void AppendLog(string message)
    {
        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void AddProductRow(ProductResult product)
    {
        _grid.Rows.Add(product.Asin, product.Title, product.Brand, "$" + product.Price, product.ProductUrl);
    }
}
