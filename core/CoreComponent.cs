// Filename:  HttpServer.cs        
// Author:    Benjamin N. Summerton <define-private-public>        
// License:   Unlicense (http://unlicense.org/)

using System;
using System.IO;
using System.Text;
using System.Net;
using System.Threading;
using System.Collections.Generic;

namespace CatchPoint
{
    class CoreComponent
    {
        // We have to get available agents urls from some agents registry database
        // Here we mock it with a function
        private static string GetAgentURL(int id)
        {
            return $"http://localhost:300{id}/";
        }

        private static Dictionary<int, Queue<string[]>> LoadAndValidateConfig(string configPath)
        {
            Dictionary<int, Queue<string[]> > tasks = new Dictionary<int, Queue<string[]>>();
            string line;
            System.IO.StreamReader file;
            try
            {
                // Read and parse the config file.  
               file = new System.IO.StreamReader(configPath);
            }
            catch(Exception e)
            {
                Console.WriteLine("Failed to open config file with error: {0}.", e.Message);
                return tasks;
            }
            while ((line = file.ReadLine()) != null)
            {
                // Parse string
                string[] words = line.Split(' ');

                // Validate record
                if (words.Length != 3)
                {
                    Console.WriteLine("Invalid number of tokens in the record.");
                    continue;
                }
                // First token number convertable to 1, 2 or 3
                int id;
                if (!int.TryParse(words[0], out id) || (id != 1 && id != 2 && id != 3))
                {
                    Console.WriteLine(@"Invalid record. First token should have values ""1"", ""2"", or ""3""");
                    continue;
                }

                //Second token should be url without schema, and of limited length)
                string testUrl = $"http://{words[1]}";
                string testUrlParam = WebUtility.UrlEncode(words[1]); //This is what we are going to send
                if (!Uri.IsWellFormedUriString(testUrl, UriKind.Absolute) || Encoding.UTF8.GetByteCount(testUrlParam) > 50)
                {
                    Console.WriteLine(@"Invalid record. Second token should be valid url of limited length");
                    continue;
                }

                // Third token should be frequency. I assume it should be > 0
                if (!int.TryParse(words[0], out int fr) || fr < 1)
                {
                    Console.WriteLine(@"Invalid record. Thrird field should be convertable to a positive integer");
                    continue;
                }

                // Record is valid. Add it to the dictionary
                string[] par = new string[] { testUrlParam, words[2] };
                Queue<string[]> Value;
                if (tasks.ContainsKey(id))
                {
                    Value = tasks[id];
                }
                else
                {
                    Value = new Queue<string[]>();
                }
                Value.Enqueue(par);
                tasks[id] = Value;
            }

            file.Close();

            return tasks;
        }

        public static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Please supply config file path.");
                Console.ReadLine();
                return;
            }
            string configPath = args[0];

            Console.WriteLine("Starting...");

            Dictionary<int, Queue<string[]> > tasks = LoadAndValidateConfig(configPath);

            if(tasks.Count == 0)
            {
                Console.WriteLine("No valid tasks. Exiting...");
            }

            void AgentTask(object data)
            {
                string[] threadData = (string[])data;
                // Build url
                string testURL = threadData[0]; //It is already encoded
                int frequency = Convert.ToInt32(threadData[1]);
                string agentURL = threadData[2];
                string url = $"{agentURL}?tu={testURL}";
                int goodCount = 0;
                int badCount = 0;
                double runningAverage = 0;
                while (goodCount < 8 && badCount < 3)
                {
                     try
                     {
                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                        HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                        Stream dataStream = response.GetResponseStream();
                        StreamReader reader = new StreamReader(dataStream);
                        string connectTime = reader.ReadToEnd();

                        // This should be a number and not -1
                        if (connectTime != "-1" && double.TryParse(connectTime, out double newValue)) {
                            runningAverage = (runningAverage * goodCount + newValue) / (++goodCount);
                            string runningAverageStr = string.Format("{0:f6}", runningAverage);

                            // Here we output valid results. Console is thread safe, so it should be OK
                            // Ideally at this point I would save (or update, based on the testUrl as a key) in a database.
                            // A database, like Postgres for example is thread safe too.
                            // Did not have enough time to implement it

                            Console.WriteLine("Running average: {0} {1} ({2} runs)", testURL, runningAverageStr, goodCount.ToString());
                        } else
                        {
                            badCount++;
                            Console.WriteLine("Agent {0} failed to run test for {1}: invalid connect time value {2} returned", agentURL, testURL, connectTime);
                        }
                     }
                     catch(Exception e)
                     {
                        badCount++;
                        Console.WriteLine(@"Agent {0} failed to run test for {1} with error: ""{2}""", agentURL, testURL, e.Message);
                     }

                     Thread.Sleep(frequency * 1000);
                }
            }

            List<Thread> agentThreads = new List<Thread>();
            // number of tasks corresponds to number of agents.
            foreach (var entry in tasks)
            {
                // entry is of type (KeyValuePair<int, Queue<string[]>>
                int id = entry.Key;
                string agentUrl = GetAgentURL(id);
                Queue<string[]> testQueue = entry.Value;
                while(testQueue.Count > 0)
                {
                    string[] testData = testQueue.Dequeue();
                    string[] threadData = new string[] {testData[0], testData[1], agentUrl};    

                    Thread agentThread = new Thread(AgentTask);
                    agentThread.IsBackground = true;
                    agentThreads.Add(agentThread);
                    agentThread.Start(threadData);
                }
            }

            foreach (var thread in agentThreads)
            {
                if (thread.IsAlive)
                {
                    thread.Join();
                }
            }

            Console.WriteLine("Done");
        }
    }
}
