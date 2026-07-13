using System.Windows.Forms;
using MarketProHunter.UI;

ApplicationConfiguration.Initialize();

var mainForm = new MainFormV2();
mainForm.Shown += (_, _) => EnableAsinDoubleClickCopy(mainForm);
Application.Run(mainForm);

static void EnableAsinDoubleClickCopy(Control root)
{
    var grid = FindControl<DataGridView>(root);
    if (grid is null || !grid.Columns.Contains("asin")) return;

    grid.CellDoubleClick += (_, e) =>
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        if (!string.Equals(grid.Columns[e.ColumnIndex].Name, "asin", StringComparison.OrdinalIgnoreCase)) return;

        var asin = grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(asin)) return;

        Clipboard.SetText(asin);
        grid.CurrentCell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
    };
}

static T? FindControl<T>(Control root) where T : Control
{
    foreach (Control child in root.Controls)
    {
        if (child is T match) return match;
        var nested = FindControl<T>(child);
        if (nested is not null) return nested;
    }

    return null;
}
