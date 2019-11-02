using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ML_Automator
{
    class TrainerGenerator
    {
        private readonly ResearchTracker researchTracker = null;

        private readonly int trainingSteps = 10;

        public int SessionsPerStep { get; } = int.MinValue;

        private const string trainingDefaultsPath = "./Resources/TrainingDefaults.json";
        private readonly string trainingDefaultsString = string.Empty;
        private readonly Dictionary<string, string> trainingDefaultConfig = null;

        private const string trainingMaxPath = "./Resources/TrainingMax.json";
        private readonly string trainingMaxString = string.Empty;
        private readonly Dictionary<string, string> trainingMaxConfig = null;

        /// <summary> Dictionary of the Iterative values so that we don't repeatedly generate the same values for each session. </summary>
        private readonly Dictionary<string, float> trainningIterationValues = null;

        private const string defaultYamlTrainerPath = "./Resources/DefaultTrainer.yaml";
        private readonly string defaultYamlString = string.Empty;

        private readonly string modifiedTrainerPath = "./Resources/";
        private readonly string modifiedTrainerName = "trainer.yaml";

        /// <summary> Used to compare previous to newly generated Yaml, Int changes can result in duplicate trainning, and we can skip these to save some trainning time. </summary>
        private string previousModifiedYaml = string.Empty;
        /// <summary> The current Yaml used for trainning. </summary>
        private string activeModifiedYaml = string.Empty;
        /// <summary> Added to the beggining of Anaconda Arguments as it simplifies summaries</summary>
        public string currentRunID { get; private set; } = string.Empty;

        public TrainerGenerator(ResearchTracker tracker)
        {
            researchTracker = tracker;
            // Load our JSON files into dictionaries so that we can manipulate these when generating new Trainning Data.
            Util.LoadJsonFromLocation(trainingDefaultsPath, ref trainingDefaultsString, ref trainingDefaultConfig);
            Util.LoadJsonFromLocation(trainingMaxPath, ref trainingMaxString, ref trainingMaxConfig);
            // Read contents of file to our Yaml.
            defaultYamlString = Util.ReadFile(defaultYamlTrainerPath);
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
            activeModifiedYaml = defaultYamlString;
            // MultiStep is used to determine Which combination of configuration settings to use for current training
            int MultiStep;
            int BaseMultiplier = (Math.DivRem(Step, trainingMaxConfig.Count + 1, out MultiStep) % trainingSteps) ;
            /// I've kept the below incase I return to use binary step if I refine my variables, 10 is just to omany as it means 1000 sessions of trainning which is insane.
            //x // We convert our MultiStep to a Binary representation so that we can check against the chars for if we want to adjust the value from the Multiplier
            //x string BinaryStep = Convert.ToString(MultiStep, 2).PadLeft(trainingMaxConfig.Count, '0');
            // This allows us to check the index against our Binary Configuration quick
            int ConfigIndex = 0;
            foreach (var item in trainingMaxConfig.Keys)
            {
                float IncreasedValue = (trainningIterationValues[item] * (BaseMultiplier + (ConfigIndex == MultiStep ? 1 : 0))); //x(BinaryStep[ConfigIndex] == '1' ? 1f : 0f)));
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
            if (previousModifiedYaml == activeModifiedYaml)
            {
                return false;
            }
            else
            {
                currentRunID = $"Step{BaseMultiplier}-Part{MultiStep}";
                if (Step > 0)
                {
                    if (MultiStep == 0)
                    {
                        // This means we've moved up to the next BaseMultiplier or next 'level' of trainning, we ask our Logger to prepare us ranks for all the previous sessions before we start a new log.
                        researchTracker.RankStepSessions();
                    }
                    if (Directory.Exists("./summaries"))
                    {
                        Directory.Move("./summaries", $"{researchTracker.CurrenResearchLogPath}/summaries");
                        Util.PrintConsoleMessage(ConsoleColor.Green, $"Completed sumarries moved to log directory");
                    }
                    if (Directory.Exists("./models"))
                    {
                        Directory.Move("./models", $"{researchTracker.CurrenResearchLogPath}/models");
                        Util.PrintConsoleMessage(ConsoleColor.Green, $"Completed model moved to log directory");
                    }
                }
                // Otherwise we save the Modified Yaml
                File.WriteAllText(modifiedTrainerPath + modifiedTrainerName, activeModifiedYaml);
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
