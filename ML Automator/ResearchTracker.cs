using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ML_Automator
{
    public class ResearchTracker
    {
        /// <summary>Returns the path of the Research Log currently that is currently open. </summary>
        public string CurrenResearchLogPath
        {
            get
            {
                return $"{researchStartPath}{logPath}";
            }
        }
        // File name stored so that we can easily move complete logs and session data to a new location.
        public string CurrentResearchStepFolder { get; private set; }

        private const string researchStartPath = "../ResearchLogs/";
        private const string researchBackupPath = "../ResearchLogs/Backups";
        private const string logFileName = "log.txt";
        private const string logRankName = "rank.txt";
        private const string logTop10Name = "top10.txt";
        private const string yamlBackupFileName = "UsedYaml.yaml";
        // Names used for logging
        private readonly string[] rankNames = new string[]{"Fastest", "Mean", "Step", "MeanReward"};
        // What we refer to our stages, A step is a collection of parts.
        private const string stepName = "Step_";
        private const string partName = "Part_";

        private string logPath = string.Empty;

        /// <summary>Current data stored from the active step, this is cleared when a new Step begins, used to rank all Parts from Step. </summary>
        private readonly Dictionary<string, ResearchSessionData> currentResearchData = new Dictionary<string, ResearchSessionData>();
        /// <summary>Not currently used, but could be utilized to rank all sessions in more detail. </summary>
        private readonly Dictionary<string, ResearchSessionData> allResearchData = new Dictionary<string, ResearchSessionData>();

        // Current stream open that is being used for logging.
        private StreamWriter currentLog = null;
        // Simple counter for logging purposes, reset at the start of a new log. 
        private int updateCount = 0;
        // Time the current log has been operating
        private float elapsedTime = 0.0f;

        public ResearchTracker()
        {
            // Creates a folder for where logs will be left
            Util.CreateFolderIfNoneExists(researchStartPath);
            // Folder for where old logs are moved to if duplicates are discovered.
            Util.CreateFolderIfNoneExists(researchBackupPath);
        }

        /// <summary>Ranks the Parts involved with a complete step. This is found in the summary folder and assists identify the best runs from a Step. </summary>
        public void RankStepSessions()
        {
            foreach (var item in currentResearchData.Keys)
            {
                if (allResearchData.ContainsKey(item))
                    allResearchData.Remove(item);
                allResearchData.Add(item, currentResearchData[item]);
            }
            // 'a lot' of Garbage is generated here, but given how infrequent it is called, it wasn't worth the effort to improve.
            var researchValues = currentResearchData.Values;
            List<ResearchSessionData> FastestTraining = researchValues.OrderBy(c => c.elapsedTime).ToList();
            // Have to order by Descending for the rest since higher numbers are better
            List<ResearchSessionData> BestMean = researchValues.OrderByDescending(c => c.bestMeanReward).ToList();
            List<ResearchSessionData> BestMeanStep = researchValues.OrderByDescending(c => c.bestMeanAtStep).ToList();
            List<ResearchSessionData> MeanReward = researchValues.OrderByDescending(c => c.meanReward).ToList();

            List<List<ResearchSessionData>> researchLists = new List<List<ResearchSessionData>>() { FastestTraining, BestMean, BestMeanStep, MeanReward };
            // Now that we have our data organized, we save it. We step through our FastestTraining and give it a rank by the Index it holds in regards to the other lists.
            // This isn't a very efficient way of doing this, but since it'll only be out of a maximum of 10 elements, it wasn't a focus for improvement.
            for (int i = 0; i < FastestTraining.Count; i++)
            {
                string rankLog =
                    $"Session: `{FastestTraining[i].pathName}`\n" +
                    $"Trainning Rank: {i}/{FastestTraining.Count}\n" +
                    $"Best Mean Rank: {BestMean.IndexOf(FastestTraining[i])} | {FastestTraining[i].bestMeanReward}\n" +
                    $"Best Mean Step: {BestMeanStep.IndexOf(FastestTraining[i])} | {FastestTraining[i].bestMeanAtStep}\n" +
                    $"Average Mean: {MeanReward.IndexOf(FastestTraining[i])} | {FastestTraining[i].meanReward}";
                File.WriteAllText($"{researchStartPath}{FastestTraining[i].pathName}/{logRankName}", rankLog);
                // We provide a bit of detail to the console that can be used if anyone is watching.
                Util.PrintConsoleMessage(ConsoleColor.Gray, $"Current Step\t| '{FastestTraining[i].pathName}' was Rank #{i + 1} in terms of elapsed time. ({FastestTraining[i].elapsedTime}s)");
            }
            // Now we do it again, but for all the scores we can easily compare the bests. More garbage being generated here as well.
            // This is quite ugly, but without making some complex methods, this seemed like the easiest way at the time.
            string bulkRankLog = string.Empty;
            string path = $"{researchStartPath}{FastestTraining[0].pathName.Substring(0, FastestTraining[0].pathName.LastIndexOf('/')+1)}";
            for (int l = 0; l < researchLists.Count; l++)
            {
                bulkRankLog = string.Empty;
                for (int i = 0; i < 10; i++)
                {
                    bulkRankLog +=
                        $"Session: `{researchLists[l][i].pathName}`\n" +
                        $"Trainning Rank: {FastestTraining.IndexOf(researchLists[l][i])} / {researchLists[l].Count}\n" +
                        $"Best Mean Rank: {BestMean.IndexOf(researchLists[l][i])} | {researchLists[l][i].bestMeanReward}\n" +
                        $"Best Mean Step: {BestMeanStep.IndexOf(researchLists[l][i])} | {researchLists[l][i].bestMeanAtStep}\n" +
                        $"Average Mean: {MeanReward.IndexOf(researchLists[l][i])} | {researchLists[l][i].meanReward}\n\n";
                }
                string finalPath = $"{path}{rankNames[l]}{logTop10Name}";
                if (File.Exists(finalPath))
                {
                    // We add the time it was last edited to the name to try improve finability later.
                    File.Move(finalPath, $"{finalPath}_backup_{File.GetLastWriteTime(finalPath).ToShortDateString()}");
                }
                File.WriteAllText(finalPath, bulkRankLog);
            }
            // Clear current research for next Step
            currentResearchData.Clear();
        }
        /// <summary>Starts a new log by backing up previous logs if one exists for the Step and Part, and makes a copy of the YAML that can be used for reference later.</summary>
        public void StartNewLog(int step, int part, ref string activeYaml)
        {
            CurrentResearchStepFolder = $"{researchStartPath}{stepName}{step}";
            string folderFilePathName = $"{stepName}{step}/{partName}{part}";
            elapsedTime = 0;
            updateCount = 0;
            if (Directory.Exists($"{researchStartPath}{folderFilePathName}"))
            {
                DateTime lastWritten = Directory.GetLastWriteTime($"{researchStartPath}{folderFilePathName}");
                Util.PrintConsoleMessage(ConsoleColor.DarkYellow, $"Log Folder with Path '{researchStartPath}{folderFilePathName}' already exists, moving folder to ./backup/");
                Directory.Move($"{researchStartPath}{stepName}{step}", $"{researchBackupPath}/{stepName}{step}_{lastWritten.ToString("ddMM__HH-mm-ss_ffff")}");
            }
            Util.CreateFolderIfNoneExists($"{researchStartPath}{folderFilePathName}");
            StreamWriter writer = File.CreateText($"{researchStartPath}{folderFilePathName}/{yamlBackupFileName}");
            if (writer == null)
            {
                throw new Exception($"Failed to create and open Yaml for ResearchLog.");
            }
            writer.Write(activeYaml);
            writer.Close();
            // Small check to ensure logs was closed, if not, we close it and continue on.
            if (currentLog != null)
                currentLog.Close();
            currentLog = File.CreateText($"{researchStartPath}{folderFilePathName}/{logFileName}");
            logPath = folderFilePathName;
            // Create a new data point
            if (currentResearchData.ContainsKey(folderFilePathName))
                currentResearchData.Remove(folderFilePathName);
            currentResearchData.Add(folderFilePathName, new ResearchSessionData(folderFilePathName));
            // Notify user of change and continue on.
            Util.PrintConsoleMessage(ConsoleColor.DarkGreen, "Cloned New Yaml for Research Log Successfully");
        }

        /// <summary>An update was passed into the Automator from Anaconda, the string is passed in, we check against a collection of Regex queries for data we want to pull out of it and then log those points. </summary>
        public void UpdateTracker(string update)
        {
            updateCount++;
            string steps = Util.MatchWithQuery("STEPS", update);
            string elapsed = Util.MatchWithQuery("ELAPSED", update);
            string mean_reward = Util.MatchWithQuery("MEAN_REWARD", update);
            string std_reward = Util.MatchWithQuery("STD_REWARD", update);
            // Update Elapsed float
            elapsedTime = float.Parse(elapsed);
            // Update our session data
            ResearchSessionData sessionData = currentResearchData[logPath];
            sessionData.elapsedTime = float.Parse(elapsed);
            sessionData.totalReward += float.Parse(mean_reward);
            sessionData.UpdateBestMeanReward(int.Parse(steps), float.Parse(mean_reward));
            // Put a message in console just to inform current progress
            currentLog.WriteLine($"|- Record #{updateCount - 1} ---- {steps} ----");
            string UpdateLog = $"Time Elapsed: {elapsed}\t|\tCurrent Average: {(elapsedTime / updateCount).ToString("0.0000")}\nMean Reward: {mean_reward}\t|\tDeviation: {std_reward}";
            currentLog.WriteLine(UpdateLog);
            currentLog.Flush();
            Util.PrintConsoleMessage(ConsoleColor.DarkGreen, UpdateLog);
        }

        public void RunComplete()
        {
            // We check if a log has started, otherwise we don't do anything.
            if (currentLog != null)
            {
                // Since all trainning sessions take 50*1000 (50000) itterations
                currentResearchData[logPath].meanReward = currentResearchData[logPath].totalReward / updateCount;
                currentLog.WriteLine($"|--- COMPLETE ----");
                currentLog.WriteLine($"Total Trainning Time: {elapsedTime}, Avg Step: {(elapsedTime / updateCount).ToString("0.0000")}");
                currentLog.Close();
                currentLog = null;
            }
        }
    }
    public class ResearchSessionData
    {
        public string pathName = string.Empty;
        public float elapsedTime = float.NegativeInfinity;
        public float meanReward = 0f;
        public float totalReward = 0f;

        public float bestMeanReward = float.NegativeInfinity;
        public int bestMeanAtStep = int.MinValue;

        public ResearchSessionData(string name)
        {
            pathName = name;
        }
        /// <summary> Updates the 'BestMeanReward' if it is higher than previous bests, to reduce casting to only once.</summary>
        public void UpdateBestMeanReward(int steps, float reward)
        {
            if (bestMeanReward < reward)
            {
                bestMeanAtStep = steps;
                bestMeanReward = reward;
            }
        }
    }
}
