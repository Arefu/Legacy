namespace Legacy
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Contains("--test-zenkai"))
            {
                Zenkai.ZenkaiRoundTripTests.RunAll();
                return;
            }

            string? tileTest = args.FirstOrDefault(a => a.StartsWith("--test-tiles="));
            if (tileTest != null)
            {
                TileTests.RunAll(tileTest["--test-tiles=".Length..].Trim('"'));
                return;
            }

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // Launched by Dragon Radar for Edit Script/Dialogue, right-click "Edit X code...", and
            // Custom pickups: just the small micro editor, not the whole IDE.
            if (args.Contains("--micro-edit"))
            {
                MicroEditor.Run(args);
                return;
            }

            Application.Run(new Legacy(args));
        }
    }
}