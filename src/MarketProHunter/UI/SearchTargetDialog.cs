using MarketProHunter.Categories;
using MarketProHunter.Models;

namespace MarketProHunter.UI;

public sealed class SearchTargetDialog : Form
{
    private readonly ComboBox _modeCombo = new();
    private readonly ComboBox _categoryCombo = new();
    private readonly ComboBox _subCategoryCombo = new();
    private readonly ComboBox _marketCombo = new();
    private readonly TextBox _storeUrlText = new();
    private readonly TextBox _customQueryText = new();
    private readonly ComboBox _pagesCombo = new();
    private readonly Label _summaryLabel = new();
    private readonly IReadOnlyList<KeywordCategory> _categories = KeywordCategoryProvider.GetDefaultCategories();

    public SearchTarget? Target { get; private set; }

    public SearchTargetDialog()
    {
        Text = "Arama hedefi seç";
        Width = 760;
        Height = 470;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        BuildLayout();
        LoadDefaults();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 9,
            Padding = new Padding(14)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        ConfigureCombo(_modeCombo);
        ConfigureCombo(_categoryCombo);
        ConfigureCombo(_subCategoryCombo);
        ConfigureCombo(_marketCombo);
        ConfigureCombo(_pagesCombo);

        _storeUrlText.PlaceholderText = "Amazon mağaza veya seller linkini yapıştırın";
        _customQueryText.PlaceholderText = "Örneğin: battery tester";
        _summaryLabel.AutoSize = true;
        _summaryLabel.Padding = new Padding(4);

        AddRow(root, 0, "Arama modu", _modeCombo);
        AddRow(root, 1, "Ana kategori", _categoryCombo);
        AddRow(root, 2, "Alt kategori", _subCategoryCombo);
        AddRow(root, 3, "Hazır mağaza", _marketCombo);
        AddRow(root, 4, "Amazon mağaza linki", _storeUrlText);
        AddRow(root, 5, "Özel arama", _customQueryText);
        AddRow(root, 6, "Sayfa", _pagesCombo);
        AddRow(root, 7, "Özet", _summaryLabel);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        var startButton = new Button { Text = "Taramayı Başlat", Width = 140, DialogResult = DialogResult.None };
        var cancelButton = new Button { Text = "İptal", Width = 90, DialogResult = DialogResult.Cancel };
        startButton.Click += (_, _) => ConfirmSelection();
        buttons.Controls.Add(startButton);
        buttons.Controls.Add(cancelButton);
        root.Controls.Add(buttons, 1, 8);

        AcceptButton = startButton;
        CancelButton = cancelButton;
        Controls.Add(root);

        _modeCombo.SelectedIndexChanged += (_, _) => UpdateEnabledState();
        _categoryCombo.SelectedIndexChanged += (_, _) => LoadCategoryChoices();
        _subCategoryCombo.SelectedIndexChanged += (_, _) => UpdateSummary();
        _marketCombo.SelectedIndexChanged += (_, _) => UpdateSummary();
        _storeUrlText.TextChanged += (_, _) => UpdateSummary();
        _customQueryText.TextChanged += (_, _) => UpdateSummary();
        _pagesCombo.SelectedIndexChanged += (_, _) => UpdateSummary();
    }

    private void LoadDefaults()
    {
        _modeCombo.Items.AddRange(new object[] { "Hazır Mağaza", "Amazon Mağaza Linki", "Özel Arama" });
        _modeCombo.SelectedIndex = 0;

        _categoryCombo.Items.Add("Tümü");
        _categoryCombo.Items.AddRange(_categories.Select(x => x.Name).Cast<object>().ToArray());
        _categoryCombo.SelectedIndex = 0;

        _pagesCombo.Items.AddRange(new object[] { "1", "2", "3", "5", "10", "20", "50", "Hepsi" });
        _pagesCombo.SelectedItem = "5";

        LoadCategoryChoices();
        UpdateEnabledState();
    }

    private void LoadCategoryChoices()
    {
        var categoryName = _categoryCombo.SelectedItem?.ToString() ?? "Tümü";

        _subCategoryCombo.Items.Clear();
        _subCategoryCombo.Items.Add("Tümü");
        var category = _categories.FirstOrDefault(x => x.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
        if (category is not null)
        {
            _subCategoryCombo.Items.AddRange(category.SubCategories.Select(x => x.Name).Cast<object>().ToArray());
        }
        _subCategoryCombo.SelectedIndex = 0;

        _marketCombo.Items.Clear();
        _marketCombo.Items.AddRange(MarketCatalog.GetMarkets(categoryName).Cast<object>().ToArray());
        if (_marketCombo.Items.Count > 0) _marketCombo.SelectedIndex = 0;

        UpdateSummary();
    }

    private void UpdateEnabledState()
    {
        var mode = GetMode();
        _marketCombo.Enabled = mode == SearchMode.ReadyMarket;
        _storeUrlText.Enabled = mode == SearchMode.AmazonStoreLink;
        _customQueryText.Enabled = mode == SearchMode.CustomSearch;
        _categoryCombo.Enabled = true;
        _subCategoryCombo.Enabled = true;
        UpdateSummary();
    }

    private void ConfirmSelection()
    {
        try
        {
            var target = BuildTarget();
            SearchTargetResolver.Validate(target);
            Target = target;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "MarketProHunter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private SearchTarget BuildTarget()
    {
        var pagesText = _pagesCombo.SelectedItem?.ToString() ?? "5";
        var scanAll = pagesText.Equals("Hepsi", StringComparison.OrdinalIgnoreCase);
        var maxPages = scanAll ? 500 : int.TryParse(pagesText, out var value) ? value : 5;

        return new SearchTarget
        {
            Mode = GetMode(),
            Category = NullIfAll(_categoryCombo.SelectedItem?.ToString()),
            SubCategory = NullIfAll(_subCategoryCombo.SelectedItem?.ToString()),
            MarketName = _marketCombo.SelectedItem?.ToString(),
            StoreUrl = _storeUrlText.Text.Trim(),
            CustomQuery = _customQueryText.Text.Trim(),
            ScanAllPages = scanAll,
            MaxPages = maxPages
        };
    }

    private SearchMode GetMode() => _modeCombo.SelectedIndex switch
    {
        1 => SearchMode.AmazonStoreLink,
        2 => SearchMode.CustomSearch,
        _ => SearchMode.ReadyMarket
    };

    private void UpdateSummary()
    {
        var modeText = _modeCombo.SelectedItem?.ToString() ?? "Hazır Mağaza";
        var targetText = GetMode() switch
        {
            SearchMode.ReadyMarket => _marketCombo.SelectedItem?.ToString() ?? "Mağaza seçilmedi",
            SearchMode.AmazonStoreLink => string.IsNullOrWhiteSpace(_storeUrlText.Text) ? "Link bekleniyor" : _storeUrlText.Text.Trim(),
            SearchMode.CustomSearch => string.IsNullOrWhiteSpace(_customQueryText.Text) ? "Arama metni bekleniyor" : _customQueryText.Text.Trim(),
            _ => "-"
        };
        var category = _categoryCombo.SelectedItem?.ToString() ?? "Tümü";
        var subCategory = _subCategoryCombo.SelectedItem?.ToString() ?? "Tümü";
        var pages = _pagesCombo.SelectedItem?.ToString() ?? "5";
        _summaryLabel.Text = $"{modeText} | {targetText} | Kategori: {category} | Alt kategori: {subCategory} | Sayfa: {pages}";
    }

    private static string? NullIfAll(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("Tümü", StringComparison.OrdinalIgnoreCase)) return null;
        return value.Trim();
    }

    private static void ConfigureCombo(ComboBox combo)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.Dock = DockStyle.Fill;
    }

    private static void AddRow(TableLayoutPanel panel, int row, string label, Control control)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, row == 7 ? 55 : 42));
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        control.Dock = DockStyle.Fill;
        panel.Controls.Add(control, 1, row);
    }
}
