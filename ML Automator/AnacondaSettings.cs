using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ML_Automator
{
    class AnacondaSettings
    {
        public string EnvironmentName { get; } = string.Empty;

        private readonly string anaConfigPath = "./Resources/AnnaConfig.json";
        private readonly Dictionary<string, string> anaConfig = new Dictionary<string, string>();

        private bool hasArgsChanged = false;
        private string argumentString = string.Empty;

        /// <summary> Returns true if AnnaConfig.Json contains the 'env' key which enables Anaconda to manage Unity instances for us.</summary>
        public bool IsUsingEnv() { return anaConfig.ContainsKey("env"); }

        public AnacondaSettings()
        {
            Util.PrintConsoleMessage(ConsoleColor.Yellow, $"Loading Settings For Anaconda.");
            // Load our JSON files into dictionaries so we can process this further when we have to run Annaconda which has some commandlines that want "--cmd" and others that need "--cmd=40" and will complain.
            Util.LoadJsonFromLocation(anaConfigPath, ref anaConfig);
            hasArgsChanged = true;

            if (anaConfig.ContainsKey("env"))
            {
                // Need index of last / so we can work out a name
                int index = anaConfig["env"].LastIndexOf('/') + 1;
                EnvironmentName = anaConfig["env"].Substring(index, anaConfig["env"].Length - (index));
            }
        }

        /// <summary> Generates the argument string if arguements have been changed since the last trainning session, otherwise it'll reuse the last arguement string generated. </summary>
        public string GetArgumentString()
        {
            if (hasArgsChanged)
            {
                hasArgsChanged = false;
                argumentString = string.Empty;
                foreach (string item in anaConfig.Keys)
                {
                    // Anaconda will complain and shutdown if we pass a parameter after an argument when it isn't expecting them.
                    if (!String.IsNullOrEmpty(anaConfig[item]))
                        argumentString += $"--{item}={anaConfig[item]} ";
                    else
                        argumentString += $"--{item} ";
                }
                Util.PrintConsoleMessage(ConsoleColor.DarkGreen, $"New argument string: '{argumentString}'");
            }
            else
            {
                Util.PrintConsoleMessage(ConsoleColor.Green, $"Argument string has not changed: '{argumentString}'");
            }
            return argumentString;
        }

        public string GetEnvironmentName(string arg)
        {
            if (anaConfig.ContainsKey(arg))
            {
                return anaConfig[arg];
            }
            return "";
        }
    }
}
