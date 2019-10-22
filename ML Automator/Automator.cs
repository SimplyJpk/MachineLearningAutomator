using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
namespace ML_Automator
{
    class Automator
    {
        // A seperate thread that is running our Anaconda environment
        private Process anacondaProcess = null;
        // Seperate thread for Unity (If we're not using 'env' command line from Anaconda)
        private Process unityProcess = null;

        private AnacondaSettings anacondaSettings = null;
        private TrainerGenerator trainerGenerator = null;
        private ResearchTracker researchTracker = null;

        private const string anacondaProcessString = @"./Resources/AnacondaLoad.bat";
        private const string unityProcessString = @"../MLScene/UnityEnvironment.exe";

        private bool launchUnity = false;
        // Used to track Sessions, the Iterations between step milestones.
        private int sessionCounter = 0;

        public Automator()
        {
            // Create an Instance of AnacondaSettings which will also Load Json in Constructor
            anacondaSettings = new AnacondaSettings();
            // Create our Logger, it'll do a prilimary check to make sure folders exist
            researchTracker = new ResearchTracker();
            // Creates the Instance for Trainer which loads Json and will also generate new Trainning Data
            trainerGenerator = new TrainerGenerator(researchTracker);
            Util.PrintConsoleMessage(ConsoleColor.Green, $"All Configs Loaded.");

            //! Allows rapid generation of logs without running training. Good for debugging.
            //x int count = 0;
            //x while (count < trainerGenerator.SessionsPerStep * 20)
            //x {
            //x     researchTracker.RunComplete();
            //x     sessionCounter++;
            //x     trainerGenerator.GenerateNewTrainerYaml(sessionCounter - 1);
            //x     // We start this after Anaconda Starts as there will be some significant delay between the timer
            //x     //Tracker.StartNewLog($"Step_{TrainningCounter}");
            //x     count++;
            //x }
            //x Console.ReadLine();
        }

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
            // We set this high to give it Priority on the CPU in an attempt to reduce noise from the Operating System itself.
            process.PriorityClass = ProcessPriorityClass.High;
            Util.PrintConsoleMessage(ConsoleColor.Green, $"Instance Started!");
        }

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
                    InitializeAndRunProcess(ref anacondaProcess, anacondaProcessString, AnacondaDataOut, AnacondaDataOut, anacondaSettings.GetArgumentString());
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
            //? If we run more than 2 instances, we run the chance of over stepping our trainning by a couple steps.
            //? This causes Tensorflow to try include the trainning data with other logs? THIS cause the whole process to lock up and kill trainning.
            //? I must have spent over 8 hours over 2 days trying to find any possible way to simply latch and change the steps (Would cause some noise in trainning) with zero success.
            //? THIS works, but it is terrible and likely upsets the OS, hopefully this isn't causing additional noise, but the speed boost from trainning over 2 instances of unity at once
            //? Makes this almost a nessesity.
            else if (e.Data.Contains(":Exported")) // This happens when Tensorflow is trying to edit a saved file or temp file, for some reason it hangs. Not quite sure why.
            {
                Util.PrintConsoleMessage(ConsoleColor.Gray, $"ANNA: {e.Data}");
                foreach (var process in Process.GetProcessesByName("python"))
                {
                    process.Kill();
                }
                foreach (var process in Process.GetProcessesByName(anacondaSettings.EnvironmentName))
                {
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

        void UnityDataOut(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
                return;
            Console.WriteLine($"UNITY: {e.Data}");
        }
        // In my time of use, I don't think this was called even once, but I have it seperate just to make any potential errors (Should they happen) more obvious.
        void UnityErrorOut(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
                return;
            Console.WriteLine($"UNITY ERROR: {e.Data}");
        }
    }
}
