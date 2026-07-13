using System.Text;
using MarketProHunter.UI;

namespace MarketProHunter;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var form = new MainFormV2();
        form.Shown += (_, _) => ConfigurePlainTextCopy(form);
        Application.Run(form);
    }

    private static void ConfigurePlainTextCopy(Control root)
    {
        var grid = FindControl<DataGridView>(root);
        if (grid is null) return;

        var copyAction = new Action(() => CopySelectedCellsAsPlainText(grid));

        var menu = new ContextMenuStrip();
        menu.Items.Add("Kopyala", null, (_, _) => copyAction());
        grid.ContextMenuStrip = menu;

        Application.AddMessageFilter(new GridCopyMessageFilter(grid, copyAction));
    }

    private static void CopySelectedCellsAsPlainText(DataGridView grid)
    {
        var selected = grid.SelectedCells
            .Cast<DataGridViewCell>()
            .Where(cell => cell.RowIndex >= 0 && cell.ColumnIndex >= 0)
            .OrderBy(cell => cell.RowIndex)
            .ThenBy(cell => cell.ColumnIndex)
            .ToList();

        if (selected.Count == 0 && grid.CurrentCell is not null)
        {
            selected.Add(grid.CurrentCell);
        }

        if (selected.Count == 0) return;

        var rows = selected
            .GroupBy(cell => cell.RowIndex)
            .OrderBy(group => group.Key)
            .Select(group => string.Join("\t", group
                .OrderBy(cell => cell.ColumnIndex)
                .Select(cell => cell.FormattedValue?.ToString() ?? string.Empty)));

        var text = string.Join(Environment.NewLine, rows);
        Clipboard.SetText(text);
    }

    private static T? FindControl<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match) return match;
            var nested = FindControl<T>(child);
            if (nested is not null) return nested;
        }

        return null;
    }

    private sealed class GridCopyMessageFilter : IMessageFilter
    {
        private const int WmKeyDown = 0x0100;
        private readonly DataGridView _grid;
        private readonly Action _copyAction;

        public GridCopyMessageFilter(DataGridView grid, Action copyAction)
        {
            _grid = grid;
            _copyAction = copyAction;
        }

        public bool PreFilterMessage(ref Message message)
        {
            if (message.Msg != WmKeyDown || !_grid.ContainsFocus) return false;
            if ((Keys)message.WParam.ToInt32() != Keys.C || Control.ModifierKeys != Keys.Control) return false;

            _copyAction();
            return true;
        }
    }
}
