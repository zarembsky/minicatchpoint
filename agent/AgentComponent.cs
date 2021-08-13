// Filename:  AgentComponent.cs        
using System;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Agent
{
    class AgentServer
    {
        public static HttpListener listener;
        public static string url = "";
        public static int pageViews = 0;
        public static int requestCount = 0;
        public static string pageData = "Hello world";

        public static string GetConnectTime(string url)
        {
            string commandArgs = @"-w %{time_connect} -o /dev/null -s " + url;
            using (var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "curl.exe",
                    Arguments = commandArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Environment.SystemDirectory
                }
            })
            {
                proc.Start();

                string retVal = "-1";
                if(proc.WaitForExit(60000 /* 1 min */))
                {
                    retVal = proc.StandardOutput.ReadToEnd();
                }

                proc.Close();
                return retVal;
            }
        }
       
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

                string testUrl = $"http://{WebUtility.UrlDecode(req.QueryString["tu"])}";
                if(!Uri.IsWellFormedUriString(testUrl, UriKind.Absolute))
                {
                    Console.WriteLine("Invlid Url: {0}", testUrl);
                    resp.Close();
                    continue;
                }
                double connectTime;
                try
                {
                    connectTime = Convert.ToDouble(GetConnectTime(testUrl));
                    Console.WriteLine("Connect time: {0}", connectTime);
                } catch {
                    Console.WriteLine("Failed to determine connect time");
                    connectTime = -1;
                }

                // Print out some info about the request
                Console.WriteLine("Request #: {0}", ++requestCount);
                Console.WriteLine(req.Url.ToString());

                // With a special request core can shutdown agent (not implemented)
                if (req.Url.AbsolutePath == "/shutdown")
                {
                    Console.WriteLine("Shutdown requested");
                    runServer = false;
                }

                // Write the response info
                byte[] data = Encoding.UTF8.GetBytes(String.Format(connectTime.ToString()));
                resp.ContentType = "text/html";
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = data.LongLength;

                // Write out to the response stream (asynchronously)
                await resp.OutputStream.WriteAsync(data, 0, data.Length);
                resp.Close();
            }
        }
        // This is a hack. I think core should run a server which would listen to
        // registration request from agent once it starts. Agent info may populate agent registry
        // which lives in core location. Then core may use supplied info to contact this agent.
        private static string GetAgentURL(int id)
        {
            return $"http://localhost:300{id}/";
        }

        public static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Please supply agent id.");
                Console.ReadLine();
                return;
            }

            string url;

            if (int.TryParse(args[0], out int id))
            {
                url = GetAgentURL(id);
            }
            else
            {
                Console.WriteLine("Invalid value for agent id. Should be convertable to a number");
                Console.ReadLine();
                return;
            }

            // Create a Http server and start listening for incoming connections
            listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            Console.WriteLine("Listening for connections on {0}", url);

            // Handle requests
            Task listenTask = HandleIncomingConnections();
            listenTask.GetAwaiter().GetResult();

            // Close the listener
            listener.Close();
        }
    }
}
