﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace ML_Automator
{
    class Automator
    {
        /// <summary>A seperate thread that is running our Anaconda environment. </summary>
        private Process anacondaProcess = null;
        /// <summary>Seperate thread for Unity (If we're not using 'env' command line from Anaconda). </summary>
        private Process unityProcess = null;

        private readonly AnacondaSettings anacondaSettings = null;
        private readonly TrainerGenerator trainerGenerator = null;
        private readonly ResearchTracker researchTracker = null;

        /// <summary>Path of the Bat file used to alunch anaconda.</summary>
        private const string anacondaProcessString = @"./Resources/AnacondaLoad.bat";
        /// <summary>If this value is changed, it must be changed within AnnaConfig.json as well, 'env' must point to the directory of the executable.</summary>
        private const string unityProcessString = @"../MLScene/UnityEnvironment.exe";

        private bool launchUnity = false;
        // Used to track Sessions, the Iterations between step milestones.
        private int sessionCounter = 0;

        // Config was added quite late into development, and only consists of two lines. More could be added with little effort.
        private readonly string configPath = "./Resources/config.json";
        private readonly Dictionary<string, string> config = null;

        public Automator()
        {
            Util.LoadJsonFromLocation(configPath, ref config);
            // Create an Instance of AnacondaSettings which will also Load Json in Constructor
            anacondaSettings = new AnacondaSettings();
            // Create our Logger, it'll do a prilimary check to make sure folders exist
            researchTracker = new ResearchTracker();
            // Creates the Instance for Trainer which loads Json and will also generate new Trainning Data
            if (!int.TryParse(config["STEPS"], out int steps))
            {
                steps = 5;
                Util.PrintConsoleMessage(ConsoleColor.Yellow, $"Failed to parse config.json for Steps, STEPS = 5");
            }
            if (!bool.TryParse(config["SKIPSAMETRAINER"], out bool skipSameTrainers))
            {
                skipSameTrainers = true;
                Util.PrintConsoleMessage(ConsoleColor.Yellow, $"Failed to parse config.json for SkipSameTrainer, SKIPSAMETRAINER = true");
            }
            trainerGenerator = new TrainerGenerator(researchTracker, steps, skipSameTrainers);
            Util.PrintConsoleMessage(ConsoleColor.Green, $"All Configs Loaded.");
        }

        /// <summary>Starts a new process using the process passed in. Filename can be anything natively executable by Windows.
        /// Actions are passed in so that we can hook in different methods for Outputs. ie (Unity, Anaconda) streams. </summary>
        void InitializeAndRunProcess(ref Process process, string fileName, Action<object, DataReceivedEventArgs> dataOutput, Action<object, DataReceivedEventArgs> errorOut, string args)
        {
            Util.PrintConsoleMessage(ConsoleColor.Yellow, $"Creating new instance '{fileName}'");
            if (process != null)
                process.Dispose();
            // Generate a new Process with settings that we need
            process = new Process
            {
                StartInfo = new ProcessStartInfo(fileName)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    Arguments = args
                },
            };
            // Set our intended streams
            process.OutputDataReceived += new DataReceivedEventHandler(dataOutput);
            process.ErrorDataReceived += new DataReceivedEventHandler(errorOut);
            // Start the process and start streaming to our DataEvents
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            Util.PrintConsoleMessage(ConsoleColor.Green, $"Instance Started!");
        }

        /// <summary>The life of the application is spent inside this method.</summary>
        public void MLAutomate()
        {
            while (true)
            {
                // Anaconda isn't Running, we Spin it back up
                //! Anaconda seems to shutdown abrutly without much warning, so we can easily detect this change, however there is no real 'shutdown' state, which means we have to look for signs that it ended.
                if (anacondaProcess == null || anacondaProcess.HasExited)
                {
                    researchTracker.RunComplete();
                    sessionCounter++;
                    // This will only return false when the New Yaml is the same as the previous session, this allows us to skip this session and save
                    int safeAbort = 0;
                    while (!trainerGenerator.GenerateNewTrainerYaml(sessionCounter - 1))
                    {
                        Util.PrintConsoleMessage(ConsoleColor.DarkYellow, $"Newly Generated YAML is same as last, Skipping Trainning and generating next session.");
                        sessionCounter++;
                        safeAbort++;
                        if (safeAbort > trainerGenerator.SessionsPerStep)
                        {
                            Util.PrintConsoleMessage(ConsoleColor.Red, $"Error while generating YAML for trainer, failed to generate unique trainner after {trainerGenerator.SessionsPerStep } attempts.\nAborting Trainning.");
                            throw new Exception($"Error while generating YAML for trainer, failed to generate unique trainner after { trainerGenerator.SessionsPerStep } attempts.");
                        }
                    }
                    InitializeAndRunProcess(ref anacondaProcess, anacondaProcessString, AnacondaDataOut, AnacondaDataOut, $"{trainerGenerator.TrainerFullPath} --run-id={trainerGenerator.CurrentRunID} { anacondaSettings.GetArgumentString()}");
                }
                // We only want to Launch Unity Manually if Env variable isn't set since Anaconda should launch it.
                else if (!anacondaSettings.IsUsingEnv())
                {
                    // If Anaconda has been detected as Ready, we Launch Unity
                    if (launchUnity)
                    {
                        // We only want to launch if Unity is closed.
                        if (unityProcess == null || unityProcess.HasExited)
                        {
                            // Unlike Anaconda we don't need to pass in Arguments
                            InitializeAndRunProcess(ref unityProcess, unityProcessString, UnityDataOut, UnityErrorOut, string.Empty);
                            launchUnity = false;
                        }
                        else
                        {
                            Util.PrintConsoleMessage(ConsoleColor.DarkYellow, "Unity Still Running..");
                        }
                    }
                }
                // Keeps our Updates as low as possible without adding insane wait times.
                Thread.Sleep(250);
            }
        }

        /// <summary> The Stdout stream from Anaconda is passed into this method, and we process what is passed in, this is often just the update logs.
        /// <para>Additional checks are made to catch potential failure points that could lock up training.</para></summary>
        void AnacondaDataOut(object sender, DataReceivedEventArgs e)
        {
            // Check if null, this appears to only happen if Anaconda shutdowns faster than normal, without this the application risks running into an Null Exception error
            if (e == null || e.Data == null)
            {
                // This seems to happen when Anaconda Aburptly shutsdown at the End
                return;
            }
            // If we're manually launching Unity (Not using the 'env' command line) Then we listen and launch manually
            if (!anacondaSettings.IsUsingEnv() && e.Data.Contains("Start training by pressing the Play button"))
            {
                Util.PrintConsoleMessage(ConsoleColor.Cyan, e.Data);
                launchUnity = true;
            }
            // We check for a Time Elapsed message as these indicate progression in trainning which we want to log in our own files.
            else if (e.Data.Contains("Time Elapsed: "))
            {
                Util.PrintConsoleMessage(ConsoleColor.Gray, $"ANNA: {e.Data}");
                researchTracker.UpdateTracker(e.Data);
            }
            //! We check for this string as it means that TensorFlow during training over trainned by a couple steps, it would then edit files and squeeze this information into the logs.
            //! however, the way we have Anaconda running, I don't believe has the correct permissions, as it hangs on this and training stops until it is manually shutdown.
            else if (e.Data.Contains(":Exported"))
            {
                Util.PrintConsoleMessage(ConsoleColor.Gray, $"ANNA: {e.Data}");
                foreach (var process in Process.GetProcessesByName("python"))
                {
                    process.Kill();
                }
                foreach (var process in Process.GetProcessesByName(anacondaSettings.EnvironmentName))
                {
                    // Sometimes after killing the python instances Anaconda shuts down immediately after, I ran into a few instances where it would survive and continue to hang.
                    if (!process.HasExited)
                        process.Kill();
                }
                Util.PrintConsoleMessage(ConsoleColor.DarkRed, $"Trainning Overstep Detected, Killing Python & Unity instances to prevent, unpreventable hang.");
            }
            // Otherwise it is just a normal message and we log forward it through our console stream so that it is visible (But not logged)
            else
            {
                Util.PrintConsoleMessage(ConsoleColor.Gray, $"ANNA: {e.Data}");
            }
        }

        /// <summary> If Anaconda isn't managing the Unity Scenes, and Unity Data is returned through this and displayed.</summary>
        void UnityDataOut(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
                return;
            Util.PrintConsoleMessage(ConsoleColor.Gray, $"UNITY: {e.Data}");
        }
        /// <summary> If Unity Errors while running, we can catch the output (If we're managing the Unity instances). </summary>
        void UnityErrorOut(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
                return;
            Util.PrintConsoleMessage(ConsoleColor.Red, $"UNITY ERROR: {e.Data}");
        }
    }
}
