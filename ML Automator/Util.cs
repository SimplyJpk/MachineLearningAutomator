using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ML_Automator
{
    public static class Util
    {
        /// <summary> Colour that will be used for all console messages that aren't printed through PrintConsoleMessage.</summary>
        private readonly static ConsoleColor DefaultConsoleColour = ConsoleColor.Gray;

        /// <summary> Prints a message to console using the colour provided before returning the colour to the default colour.</summary>
        public static void PrintConsoleMessage(ConsoleColor color, string message)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = DefaultConsoleColour;
        }

        // The Regex Queries we use to pull data out of Anaconda Stream
        //! TRAINER         "(?:mlagents.trainers:)(.*?)(?:\:)"
        //! ENV NAME        "(?:mlagents.trainers:)(?:.*?:)(.*?)(?:\:))"
        //! STEPS           "(?:Step:)(.*?)(?:\.)"
        //! TIME ELAPSED    "(?:Time Elapsed:)(.*?)(?:s)"
        //! MEAN REWARD     "(?:Mean Reward:)(.*?)(?:\. )"
        //! STD of REWARD   "(?:Std of Reward:)(.*?)(?:\. )"
        /// <summary>Dictionary of Regex values used to strip nessisary details from Anaconda stream Logs. </summary>
        public static Dictionary<string, Regex> TrainningQueries = new Dictionary<string, Regex>()
        {
            ["TRAINER"] = new Regex(@"(?:mlagents.trainers:)(.*?)(?:\:)"),
            ["NAME"] = new Regex(@"(?:mlagents.trainers:)(?:.*?:)(.*?)(?:\:)"),
            ["STEPS"] = new Regex(@"(?:Step:)(.*?)(?:\.)"),
            ["ELAPSED"] = new Regex(@"(?:Time Elapsed:)(.*?)(?:s)"),
            ["MEAN_REWARD"] = new Regex(@"(?:Mean Reward:)(.*?)(?:\. )"),
            ["STD_REWARD"] = new Regex(@"(?:Std of Reward:)(.*?)(?:\. )")
        };

        /// <summary> Returns the matched value from the Regex key provided and the input string used. No error handling. </summary>
        public static string MatchWithQuery(string RegexKey, string Input)
        {
            return TrainningQueries[RegexKey].Match(Input).Groups[1].Value;
        }

        /// <summary> A helper method that attempts to read the file at the path provided and return the contents as a string. </summary>
        public static string ReadFile(string path)
        {
            if (File.Exists(path))
            {
                string fileContents = File.ReadAllText(path);
                if (string.IsNullOrEmpty(fileContents))
                    throw new Exception($"File is empty or null '{path}'");
                return fileContents;
            }
            else
                throw new Exception($"File path '{path}' does not Exist.");
        }

        /// <summary> A helper method that attempts to read the file at the path provided and fills _jsonString with a raw json string and the _config file with the values from the json. </summary>
        public static void LoadJsonFromLocation(string path, ref Dictionary<string, string> config)
        {
            PrintConsoleMessage(ConsoleColor.Yellow, $"Loading JSON from '{path}'");
            if (File.Exists(path))
            {
                string _jsonString = File.ReadAllText(path);
                if (!string.IsNullOrEmpty(_jsonString))
                {
                    config = JsonConvert.DeserializeObject<Dictionary<string, string>>(_jsonString);
                }
                if (config == null)
                    throw new Exception($"Dictionary is null, JSON Error?");
            }
            else
                throw new Exception($"JSON file path '{path}' does not Exist.");
        }

        public static void CreateFolderIfNoneExists(string path)
        {
            DirectoryInfo _info = Directory.CreateDirectory(path);
            if (_info.Exists)
            {
                PrintConsoleMessage(ConsoleColor.Yellow, $"Created directory at '{_info.FullName}'");
            }
            else
            {
                throw new Exception($"Failed to create new directory at '{path}'");
            }
        }

        /// <summary> Moves the passed in source folder contents and sub-directories to a new folder recursively. </summary>
        public static void MoveFolderContents(DirectoryInfo source, DirectoryInfo target)
        {
            if (source.Exists)
            {
                // We make sure our Target Folder exists, as Directory.MoveTo will not create it
                if (!target.Exists)
                    Directory.CreateDirectory(target.FullName);
                foreach (DirectoryInfo dir in source.GetDirectories())
                    dir.MoveTo($"{target.FullName}/{dir.Name}");
                foreach (FileInfo file in source.GetFiles())
                    file.MoveTo(Path.Combine(target.FullName, file.Name));
                PrintConsoleMessage(ConsoleColor.Green, $"Contents of '{source}' moved to '{target}'");
            }
            else
            {
                PrintConsoleMessage(ConsoleColor.DarkYellow, $"Failed to move directory contents '{source.FullName}', Directory doesn't exist?");
            }
        }
    }
}
