using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Text;

namespace MicroServiceC
{
    public static class Program
    {
        private const int OverridePort = -1; // 8002
        private static readonly string[] CompanyNames = new string[] { "Walmart", "Amazon", "Apple", "United Health", "Berkshire Hathaway", "CVS", "ExxonMobil", "Alphabet", "Google", "McKesson", "Cencora", "Costco", "Chase", "Microsoft", "Cardinal", "Chevron", "Cigna", "Ford", "Bank Of America", "General Motors", "Elevance Health", "Citigroup", "Centene", "Home Depot", "Marathon", "Kroger", "Phillips", "Fannie Mae", "Walgreens", "Valero", "Facebook", "Meta", "Verizon", "AT&T", "Comcast", "Wells Fargo", "Goldman Sachs", "Freddie Mac", "Target", "Humana", "State Farm", "Tesla", "Morgan Stanley", "Johnson And Johnson", "Archer Daniels Midland", "PepsiCo", "United Parcel Service", "FedEx", "Disney", "Dell", "Lowes", "Procter And Gamble", "Energy Transfer Partners", "Boeing", "Albertsons", "Sysco", "RTX Corporation", "General Electric", "Lockheed Martin", "American Express", "Caterpillar", "MetLife", "HCA Healthcare", "Progressive Corporation", "IBM", "John Deere", "Nvidia", "StoneX Group", "Merck", "ConocoPhillips", "Pfizer", "Delta", "TD Synnex", "Publix", "Allstate", "Cisco", "Nationwide Mutual Insurance Company", "Charter Communications", "AbbVie", "New York Life Insurance Company", "Intel", "TJX", "Prudential Financial", "HP", "United Airlines", "Performance Food Group", "Tyson Foods", "American Airlines", "Liberty Mutual", "Nike", "Oracle", "Enterprise Products", "Capital One", "Plains All American Pipeline", "World Kinect Corporation", "AIG", "Coca Cola", "TIAA", "CHS", "Bristol Myers Squibb", "Dow Chemical Company", "Best Buy" };
        // Json Schemas
        private sealed class BlankJsonSchema
        {

        }
        private sealed class StatusJsonSchema
        {
            public string status;
        }
        private sealed class GetCompanyNameInputJsonSchema
        {
            public string description;
        }
        private sealed class GetCompanyNameOutputJsonSchema
        {
            public string status;
            public string companyName;
        }
        // Functions
        private static string Check(string inputJson)
        {
            BlankJsonSchema input = JsonConvert.DeserializeObject<BlankJsonSchema>(inputJson);
            StatusJsonSchema output = new StatusJsonSchema();
            output.status = "OK";
            return JsonConvert.SerializeObject(output);
        }
        private static string Exit(string inputJson)
        {
            BlankJsonSchema input = JsonConvert.DeserializeObject<BlankJsonSchema>(inputJson);
            ExitRequested = true;
            StatusJsonSchema output = new StatusJsonSchema();
            output.status = "OK";
            return JsonConvert.SerializeObject(output);
        }
        private static string GetCompanyName(string inputJson)
        {
            GetCompanyNameInputJsonSchema input = JsonConvert.DeserializeObject<GetCompanyNameInputJsonSchema>(inputJson);

            GetCompanyNameOutputJsonSchema output = new GetCompanyNameOutputJsonSchema();
            foreach (string companyName in CompanyNames)
            {
                if (input.description.ToLower().Contains(companyName.ToLower()))
                {
                    output.companyName = companyName;
                    output.status = "OK";
                    return JsonConvert.SerializeObject(output);
                }
            }

            output.companyName = null;
            output.status = "OK";
            return JsonConvert.SerializeObject(output);
        }
        // Status Variables
        private static int Port = -1;
        private static bool ExitRequested = false;
        // Main Helper Functions
        private static int PortFromArgs(string[] args)
        {
            int output = -1;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].ToLower() == "/port")
                {
                    if (args.Length == i + 1 || !int.TryParse(args[i + 1], out output) || output < -1)
                    {
                        throw new Exception($"Invalid port specified.");
                    }
                    else
                    {
                        i++;
                    }
                }
                else
                {
                    throw new Exception($"Invalid argument \"{args[i]}\".");
                }
            }
            return output;
        }
        private static int GetFreePort()
        {
            int output = -1;
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            output = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return output;
        }
        private static string ProcessMessage(string endpoint, string inputJson)
        {
            endpoint = endpoint.ToLower();
            if (endpoint == "/Check".ToLower())
            {
                return Check(inputJson);
            }
            else if (endpoint == "/Exit".ToLower())
            {
                return Exit(inputJson);
            }
            else if (endpoint == "/GetCompanyName".ToLower())
            {
                return GetCompanyName(inputJson);
            }
            else
            {
                return "{\"status\":\"Bad endpoint.\"}";
            }
        }
        private static void RunOnPort(int port)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();
            Console.WriteLine($"Started server on port {Port}...");

            while (!ExitRequested)
            {
                try
                {
                    HttpListenerContext context = listener.GetContext();
                    HttpListenerRequest request = context.Request;

                    if (request.HttpMethod == "POST")
                    {
                        string outputJson;
                        try
                        {
                            StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding);
                            string inputJson = reader.ReadToEnd();
                            reader.Dispose();

                            string endpoint = request.Url.AbsolutePath;

                            outputJson = ProcessMessage(endpoint, inputJson);
                        }
                        catch (Exception ex)
                        {
                            StatusJsonSchema output = new StatusJsonSchema();
                            output.status = ex.Message;
                            outputJson = JsonConvert.SerializeObject(output);
                        }

                        HttpListenerResponse response = context.Response;
                        byte[] buffer = Encoding.UTF8.GetBytes(outputJson);
                        response.ContentLength64 = buffer.Length;
                        response.ContentType = "application/json";
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                        response.OutputStream.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }
        public static int Main(string[] args)
        {
            try
            {
                if (OverridePort != -1)
                {
                    Port = OverridePort;
                }
                else
                {
                    Port = PortFromArgs(args);
                }
                if (Port == -1)
                {
                    Console.WriteLine($"No port given. Selecting random...");
                    Port = GetFreePort();
                    Console.WriteLine($"Selected port {Port}. Relaunching...");
                    Process.Start(typeof(Program).Assembly.Location, $"/port {Port}");
                    return Port;
                }
                else
                {
                    Console.WriteLine($"Starting server on port {Port}...");
                    RunOnPort(Port);
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 0;
            }
        }
    }
}
