using System;

namespace ML_Automator
{
    /// <summary>
    /// Entry point for C# Console which will create the automator.
    /// </summary>
    class Program
    {
        public static void Main()
        {
            try
            {
                new Automator().MLAutomate();
            }
            catch (Exception ex)
            {
                // We attempt to catch the Exception as these were happening long after research would be started, with no indication of what caused it.
                // This helps by at least pointing to what crashed the app.
                Util.PrintConsoleMessage(ConsoleColor.Red, $"Application Error: {ex.Message}\n\nSTACK\n{ex.StackTrace}");
                Console.ReadLine();
                throw;
            }
        }
    }
}
