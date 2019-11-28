using System;
using System.Collections.Generic;

namespace ML_Automator
{
    class AnacondaSettings
    {
        /// <summary>Returns whether or not Anaconda will be trying to launch Unity itself. If it is, we need to act differently.</summary>
        public bool IsUsingEnv() { return anaConfig.ContainsKey("env"); }
        public string EnvironmentName { get; } = string.Empty;

        private readonly string anaConfigPath = "./Resources/AnnaConfig.json";
        private readonly Dictionary<string, string> anaConfig = new Dictionary<string, string>();

        /// <summary>We only update argumentString when an arguement has changed, it no longer does so, but incase someone else implements something that needs this, this'll at least update arguments before launching. </summary>
        private bool hasArgsChanged = false;
        private string argumentString = string.Empty;

        /// <summary> Returns true if AnnaConfig.Json contains the 'env' key which enables Anaconda to manage Unity instances for us.</summary>

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
                    if (!string.IsNullOrEmpty(anaConfig[item]))
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

        /// <summary>Changes an argument used by anaconda, no checks are used, and next time ArgumentString is returned a new arg list is returned.</summary>
        public void ChangeArgument(string arg, string value)
        {
            anaConfig[arg] = value;
            // Incase we're trying to remove a value by making it empty, we remove it.
            if (string.IsNullOrEmpty(value))
                anaConfig.Remove(arg);
            hasArgsChanged = true;
        }
    }
}