using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ML_Automator
{
    public class ResearchTracker
    {
        private const string researchStartPath = "../ResearchLogs/";
        private const string researchBackupPath = "../ResearchLogs/Backups";
        private const string logFileName = "log.txt";
        private const string logRankName = "rank.txt";
        private const string logTop10Name = "top10.txt";
        private const string yamlFileName = "UsedYaml.yaml";
        private readonly string[] rankNames = new string[]{"Fastest", "Mean", "Step", "MeanReward"};

        private StreamWriter currentLog = null;
        private string currentLogPath = string.Empty;

        private int updateCount = 0;

        Dictionary<string, ResearchSessionData> currentResearchData = new Dictionary<string, ResearchSessionData>();
        Dictionary<string, ResearchSessionData> allResearchData = new Dictionary<string, ResearchSessionData>();

        // We may need this
        private float elapsedTime = 0.0f;

        public ResearchTracker()
        {
            // Creates a folder for where logs will be left
            Util.CreateFolderIfNoneExists(researchStartPath);
            // Folder for where old logs are moved to if duplicates are discovered.
            Util.CreateFolderIfNoneExists(researchBackupPath);
        }

        public void RankStepSessions()
        {
            foreach (var item in currentResearchData.Keys)
            {
                // Store research
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
            // Now that we have our data organized, we save it.
            for (int i = 0; i < FastestTraining.Count; i++)
            {
                string rankLog =
                    $"Session: `{FastestTraining[i].pathName}`\n" +
                    $"Trainning Rank: {i}/{FastestTraining.Count}\n" +
                    $"Best Mean Rank: {BestMean.IndexOf(FastestTraining[i])} | {FastestTraining[i].bestMeanReward}\n" +
                    $"Best Mean Step: {BestMeanStep.IndexOf(FastestTraining[i])} | {FastestTraining[i].bestMeanAtStep}\n" +
                    $"Average Mean: {MeanReward.IndexOf(FastestTraining[i])} | {FastestTraining[i].meanReward}";
                File.WriteAllText($"{researchStartPath}{FastestTraining[i].pathName}/{logRankName}", rankLog);
                // We don't break out here as we still want the logs to be made for each session. We don't generate all 100 for all 4 scores, only the time as it is helpful in contrast.
                Util.PrintConsoleMessage(ConsoleColor.Gray, $"From current step, '{FastestTraining[i].pathName}' was Rank #{i + 1} in terms of elapsed time. ({FastestTraining[i].elapsedTime}s)");
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
                    // In an attempt to not lose data
                    // We add the time it was last edited to the name.
                    File.Move(finalPath, $"{finalPath}_backup_{File.GetLastWriteTime(finalPath).ToShortDateString()}");
                }
                File.WriteAllText(finalPath, bulkRankLog);
            }
            // Clear current research for next Step
            currentResearchData.Clear();
        }
        public void StartNewLog(string name, ref string activeYaml)
        {
            elapsedTime = 0;
            updateCount = 0;
            if (Directory.Exists($"{researchStartPath}{name}"))
            {
                DateTime lastWritten = Directory.GetLastWriteTime($"{researchStartPath}{name}");
                Util.PrintConsoleMessage(ConsoleColor.DarkYellow, $"Log Folder with Path '{researchStartPath}{name}' already exists, moving folder to ./backup/");
                Directory.Move($"{researchStartPath}{name.Substring(0, name.LastIndexOf('/'))}", $"{researchBackupPath}/{name.Substring(0, name.LastIndexOf('/'))}_{lastWritten.ToString("ddMM__HH-mm-ss_ffff")}");
            }

            Util.CreateFolderIfNoneExists($"{researchStartPath}{name}");
            StreamWriter writer = File.CreateText($"{researchStartPath}{name}/{yamlFileName}");
            if (writer == null)
            {
                throw new Exception($"Failed to create and open Yaml for ResearchLog.");
            }
            writer.Write(activeYaml);
            writer.Close();

            if (currentLog != null)
                currentLog.Close();
            currentLog = File.CreateText($"{researchStartPath}{name}/{logFileName}");
            currentLogPath = name;
            // Create a new data point
            if (currentResearchData.ContainsKey(name))
                currentResearchData.Remove(name);
            currentResearchData.Add(name, new ResearchSessionData(name));

            Util.PrintConsoleMessage(ConsoleColor.DarkGreen, "Cloned New Yaml for Research Log Successfully");
        }

        public void UpdateTracker(string update)
        {
            updateCount++;
            string steps = Util.MatchWithQuery("STEPS", ref update);
            string elapsed = Util.MatchWithQuery("ELAPSED", ref update);
            string mean_reward = Util.MatchWithQuery("MEAN_REWARD", ref update);
            string std_reward = Util.MatchWithQuery("STD_REWARD", ref update);
            // Update Elapsed float
            elapsedTime = float.Parse(elapsed);
            // Update our session data
            ResearchSessionData sessionData = currentResearchData[currentLogPath];
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
                currentResearchData[currentLogPath].meanReward = currentResearchData[currentLogPath].totalReward / 50;
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

        public float bestMeanReward = 0f;
        public int bestMeanAtStep = int.MinValue;

        public ResearchSessionData(string name)
        {
            pathName = name;
        }
        /// <summary> Updates the 'BestMeanReward' if it is highest than previous bests, to reduce casting to only once.</summary>
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
