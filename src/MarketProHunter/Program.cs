using MarketProHunter.UI;

namespace MarketProHunter;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainFormV2());
    }
}
