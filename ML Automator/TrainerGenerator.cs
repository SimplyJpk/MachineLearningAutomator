using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ML_Automator
{
    class TrainerGenerator
    {
        /// <summary> Added to the beggining of Anaconda Arguments as it simplifies summaries</summary>
        public string CurrentRunID { get; private set; } = string.Empty;
        /// <summary> How many times are needed to train before moving up to the next level of training. </summary>
        public int SessionsPerStep { get; } = int.MinValue;
        /// <summary> Returns the path to the Trainer that Anaconda has to use to run. </summary>
        public string TrainerFullPath
        {
            get
            {
                return $"{runtimeTrainerPath}{runtimeTrainerName}";
            }
        }

        private readonly ResearchTracker researchTracker = null;
        // Settings that are loaded from config.json
        private readonly int trainingSteps = 10;
        private readonly bool skipSameTrainers = true;

        private const string trainingDefaultsPath = "./Resources/TrainingDefaults.json";
        private readonly Dictionary<string, string> trainingDefaultConfig = null;

        private const string trainingMaxPath = "./Resources/TrainingMax.json";
        private readonly Dictionary<string, string> trainingMaxConfig = null;

        /// <summary> Dictionary of the Iterative values that are used when generating new trainers, reducing the need for repeated calculations. </summary>
        private readonly Dictionary<string, float> trainningIterationValues = null;

        private const string regexYamlTrainerPath = "./Resources/DefaultTrainer.yaml";
        private readonly string regexYamlString = string.Empty;

        private readonly string runtimeTrainerPath = "./Resources/";
        private readonly string runtimeTrainerName = "trainer.yaml";

        /// <summary> Used to compare previous to newly generated Yaml, Int changes can result in duplicate trainning, and we can skip these to save some trainning time. </summary>
        private string previousModifiedYaml = string.Empty;
        /// <summary> The current Yaml used for trainning. </summary>
        private string activeModifiedYaml = string.Empty;

        public TrainerGenerator(ResearchTracker tracker, int steps, bool skipSameTrainer = true)
        {
            researchTracker = tracker;
            trainingSteps = steps;
            skipSameTrainers = skipSameTrainer;
            Util.PrintConsoleMessage(ConsoleColor.Green, $"TrainerGenerator using {steps} step{ (steps > 1 ? "s" : string.Empty)} for training");
            // Load our JSON files into dictionaries so that we can manipulate these when generating new Trainning Data.
            Util.LoadJsonFromLocation(trainingDefaultsPath, ref trainingDefaultConfig);
            Util.LoadJsonFromLocation(trainingMaxPath, ref trainingMaxConfig);
            // Read contents of file to our Yaml.
            regexYamlString = Util.ReadFile(regexYamlTrainerPath);
            // How many times we train at the current 'base' value with the combination of configurations
            SessionsPerStep = trainingMaxConfig.Count + 1;

            trainningIterationValues = new Dictionary<string, float>();
            foreach (var item in trainingMaxConfig.Keys)
            {
                // We know how many Steps we need to train, so we can generate the value we can quickly multiply instead of doing ((Max-Default)/Sessions) each session
                trainningIterationValues.Add(item, ((float.Parse(trainingMaxConfig[item]) - float.Parse(trainingDefaultConfig[item])) / (trainingSteps - 1)));
            }
        }

        public bool GenerateNewTrainerYaml(int Step)
        {
            previousModifiedYaml = activeModifiedYaml;
            Util.PrintConsoleMessage(ConsoleColor.DarkGreen, $"Generating New Training Data");
            // Copy our Default Trainer
            activeModifiedYaml = regexYamlString;
            // MultiStep is used to determine Which combination of configuration settings to use for current training
            int BaseMultiplier = (Math.DivRem(Step, trainingMaxConfig.Count + 1, out int MultiStep) % trainingSteps) ;
            // This allows us to check the index against our Binary Configuration quick
            int ConfigIndex = 0;
            foreach (var item in trainingMaxConfig.Keys)
            {
                float IncreasedValue = (trainningIterationValues[item] * (BaseMultiplier + (ConfigIndex == MultiStep ? 1 : 0)));
                // If it doesn't contain a period, we want a whole value, not a decimal
                if (!trainingMaxConfig[item].Contains('.'))
                {
                    // If growth doesn't contain a decimal, we want an int
                    IncreasedValue = (float)Math.Round(IncreasedValue, 0);
                }
                activeModifiedYaml = activeModifiedYaml.Replace($"<{item}>",
                    (float.Parse(trainingDefaultConfig[item]) + IncreasedValue).ToString());

                ConfigIndex++;
            }
            // Step through Stock values to fill in anything else
            foreach (var item in trainingDefaultConfig.Keys)
            {
                activeModifiedYaml = activeModifiedYaml.Replace($"<{item}>", trainingDefaultConfig[item]);
            }
            // If Prev and Active are the Same we return false, which will skip the current trainning.
            if (skipSameTrainers == true && previousModifiedYaml == activeModifiedYaml)
            {
                return false;
            }
            else
            {
                CurrentRunID = $"Step{BaseMultiplier}-Part{MultiStep}";
                if (Step > 0)
                {
                    if (MultiStep == 0)
                    {
                        // We don't want to rank stuff if we only have 1 file, so we skip ranks in those cases
                        if (trainningIterationValues != null && trainningIterationValues.Count > 0)
                        {
                            // This means we've moved up to the next BaseMultiplier or next 'level' of trainning, we ask our Logger to prepare us ranks for all the previous sessions before we start a new log.
                            researchTracker.RankStepSessions();
                        }
                    }
                    Util.MoveFolderContents(new DirectoryInfo("./summaries"), new DirectoryInfo($"{researchTracker.CurrentResearchStepFolder}/summaries"));
                    Util.MoveFolderContents(new DirectoryInfo("./models"), new DirectoryInfo($"{researchTracker.CurrenResearchLogPath}/models"));
                }
                // Otherwise we save the Modified Yaml
                File.WriteAllText(runtimeTrainerPath + runtimeTrainerName, activeModifiedYaml);
                // Log actions
                Util.PrintConsoleMessage(ConsoleColor.Green, $"-------------------------------------");
                Util.PrintConsoleMessage(ConsoleColor.Green, $"Starting Train Loop {BaseMultiplier}\t|\t STEP: {MultiStep}/{SessionsPerStep}");
                // And initialize a new Log that represents the current Session/Part
                // Start a new Log, Current Step ie; 2 (as a folder) and then in 
                researchTracker.StartNewLog(BaseMultiplier,MultiStep, ref activeModifiedYaml);
                return true;
            }
        }
    }
}
