namespace JROptimizerPro;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var instanceMutex = new Mutex(true, Core.SingleInstance.MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            Core.SingleInstance.RestoreExistingWindow();
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Core.AppPaths.EnsureCreated();
        Core.AppLogger.Info("Aplicativo iniciado.");

        Application.ThreadException += (_, e) =>
        {
            Core.AppLogger.Error("Erro não tratado na interface.", e.Exception);
            MessageBox.Show(
                "Ocorreu um erro. O detalhe foi salvo no log.\n\n" + e.Exception.Message,
                "JR Optimizer Pro",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Core.AppLogger.Error("Erro não tratado.", ex);
        };

        Application.Run(new MainForm());
    }
}
