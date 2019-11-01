using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ML_Automator
{
    /// <summary>
    /// Entry point for C# Console which will create the automator
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
                // This helps identify problems with the application as most of the training is done when i'm not around, I was missing when it was crashing.
                Util.PrintConsoleMessage(ConsoleColor.Red, $"Application Error: {ex.Message}");
                Console.ReadLine();
                throw;
            }
        }
    }
}
