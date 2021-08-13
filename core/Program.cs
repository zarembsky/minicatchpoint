// Filename:  HttpServer.cs        
// Author:    Benjamin N. Summerton <define-private-public>        
// License:   Unlicense (http://unlicense.org/)

using System;
using System.IO;
using System.Text;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace HttpListenerExample
{
    class HttpServer
    {
        public static HttpListener listener;
        public static string url = "http://localhost:8000/";
        public static int pageViews = 0;
        public static int requestCount = 0;
        public static string pageData =
            "<!DOCTYPE>" +
            "<html>" +
            "  <head>" +
            "    <title>HttpListener Example</title>" +
            "  </head>" +
            "  <body>" +
            "    <p>Page Views: {0}</p>" +
            "    <form method=\"post\" action=\"shutdown\">" +
            "      <input type=\"submit\" value=\"Shutdown\" {1}>" +
            "    </form>" +
            "  </body>" +
            "</html>";


        public static async Task HandleIncomingConnections()
        {
            bool runServer = true;

            // While a user hasn't visited the `shutdown` url, keep on handling requests
            while (runServer)
            {
                // Will wait here until we hear from a connection
                HttpListenerContext ctx = await listener.GetContextAsync();

                // Peel out the requests and response objects
                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse resp = ctx.Response;

                // Print out some info about the request
                Console.WriteLine("Request #: {0}", ++requestCount);
                Console.WriteLine(req.Url.ToString());
                Console.WriteLine(req.HttpMethod);
                Console.WriteLine(req.UserHostName);
                Console.WriteLine(req.UserAgent);
                Console.WriteLine();

                // If `shutdown` url requested w/ POST, then shutdown the server after serving the page
                if ((req.HttpMethod == "POST") && (req.Url.AbsolutePath == "/shutdown"))
                {
                    Console.WriteLine("Shutdown requested");
                    runServer = false;
                }

                // Make sure we don't increment the page views counter if `favicon.ico` is requested
                if (req.Url.AbsolutePath != "/favicon.ico")
                    pageViews += 1;

                // Write the response info
                string disableSubmit = !runServer ? "disabled" : "";
                byte[] data = Encoding.UTF8.GetBytes(String.Format(pageData, pageViews, disableSubmit));
                resp.ContentType = "text/html";
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = data.LongLength;

                // Write out to the response stream (asynchronously), then close it
                await resp.OutputStream.WriteAsync(data, 0, data.Length);
                resp.Close();
            }
        }


        public static void Main(string[] args)
        {
            if(args.Length < 2)
            {
                Console.WriteLine("Please supply two arguments: config file path and agents file path.");
                //System.Console.ReadLine();
                return;
            }
            string configPath = args[0];
            string agentsPath = args[1];
            //string configPath = @"c:\projects\core\config.txt";
            //string agentsPath = @"c:\projects\core\agents.txt";

            // Read and parse the config file.  
            string line;
            System.IO.StreamReader file =
                new System.IO.StreamReader(configPath);

            Dictionary<int, Queue<string[]> > tasks = new Dictionary<int, Queue<string[]>>();

            while ((line = file.ReadLine()) != null)
            {
                // Parse string
                string[] words = line.Split(' ');
                System.Console.WriteLine(line);
                 // Some validation
                if(words.Length != 3)
                {
                    System.Console.WriteLine("Wrong record");
                    continue;
                }

                if(Int32.TryParse(words[0], out int id))
                {
                    string[] par = new string[] {words[1], words[2]};
                    Queue<string[]> Value;
                    if (tasks.ContainsKey(id))
                    {
                        Value = tasks[id];
                    } else {
                        Value = new Queue<string[]>();
                    }
                    Value.Enqueue(par);
                    tasks[id] = Value;
                } else
                {
                    continue;
                }
            }

            file.Close();


            void AgentTask(object data)
            {
                System.Console.WriteLine(((Queue<string[]>)data).Count);
                Thread.Sleep(5000);
            }

            List<Thread> agentThreads = new List<Thread>();

            foreach (var entry in tasks)
            {
                Thread agentThread = new Thread(AgentTask);
                agentThread.IsBackground = true;
                agentThreads.Add(agentThread);
                agentThread.Start(entry.Value);
            }

            foreach(var thread in agentThreads)
            {
                if(thread.IsAlive)
                {
                    thread.Join();
                }
            }

            System.Console.WriteLine("FINITA LA COMEDIA");

            //var thread = new Thread()
            //=========================================
            //// Create a Http server and start listening for incoming connections
            //listener = new HttpListener();
            //listener.Prefixes.Add(url);
            //listener.Start();
            //Console.WriteLine("Listening for connections on {0}", url);

            //// Handle requests
            //Task listenTask = HandleIncomingConnections();
            //listenTask.GetAwaiter().GetResult();

            //// Close the listener
            //listener.Close();
            //=============================================
            //string url = "https://www.ghostery.com";

            ////HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            ////HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            ////Stream resStream = response.GetResponseStream();
            ////
            ////HttpClient httpClient = new HttpClient();
            ////var result = httpClient.GetAsync(url).Result;

            ////if (args == null || args.Length == 0)
            ////{
            ////    throw new ApplicationException("Specify the URI of the resource to retrieve.");
            ////}
            //WebClient client = new WebClient();

            //// Add a user agent header in case the
            //// requested URI contains a query.

            //client.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

            //Stream data = client.OpenRead(url /*args[0]*/);
            //StreamReader reader = new StreamReader(data);
            //string s = reader.ReadToEnd();
            //Console.WriteLine(s);
            //data.Close();
            //reader.Close();
        }
    }
}
