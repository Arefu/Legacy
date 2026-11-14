namespace DBZKit
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // Headless map dump (same as Dump all assets' map part), handy for checking it works:
            //   DBZKit.exe --dump-maps=<rom.gba> --out=<folder>
            string? dumpRom = args.FirstOrDefault(a => a.StartsWith("--dump-maps="))?["--dump-maps=".Length..].Trim('"');
            string? dumpOut = args.FirstOrDefault(a => a.StartsWith("--out="))?["--out=".Length..].Trim('"');
            if (dumpRom != null && dumpOut != null)
            {
                byte[] bytes = File.ReadAllBytes(dumpRom);
                var (game, problem) = RomBrowser.LoadProfile(bytes);
                if (problem != null) { Console.WriteLine(problem); return; }
                var (maps, banks, errors) = MapDumper.DumpAll(DrGero.IO.ROM.FromBytes(bytes), game, dumpOut, null);
                Console.WriteLine($"Dumped {maps} map(s), {banks} atlas bank(s), {errors} problem(s) to {dumpOut}");
                return;
            }

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new DBZKit());
        }
    }
}