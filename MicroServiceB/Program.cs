using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Collections.Generic;

namespace MicroServiceB
{
    public static class Program
    {
        private const int OverridePort = -1; // 8001
        // Json Schemas
        private sealed class BlankJsonSchema
        {

        }
        private sealed class StatusJsonSchema
        {
            public string status;
        }
        private sealed class ChaseImportInputJsonSchema
        {
            public string chaseCSV;
        }
        private sealed class ChaseImportOutputJsonSchema
        {
            public string status;
            public double[] amounts;
            public string[] descriptions;
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
        private static string[] SplitRespectingQuotes(string input, char delimiter)
        {
            List<string> segments = new List<string>();
            StringBuilder currentSegment = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '\"')
                {
                    inQuotes = !inQuotes;
                }
                else if (input[i] == delimiter && !inQuotes)
                {
                    segments.Add(currentSegment.ToString());
                    currentSegment.Clear();
                }
                else
                {
                    currentSegment.Append(input[i]);
                }
            }
            if (currentSegment.Length > 0)
            {
                segments.Add(currentSegment.ToString());
                currentSegment.Clear();
            }
            return segments.ToArray();
        }
        private static string ChaseImport(string inputJson)
        {
            ChaseImportInputJsonSchema input = JsonConvert.DeserializeObject<ChaseImportInputJsonSchema>(inputJson);

            ChaseImportOutputJsonSchema output = new ChaseImportOutputJsonSchema();
            output.status = "OK";

            string[] lines = input.chaseCSV.Replace("\r", "").Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            output.amounts = new double[lines.Length - 1];
            output.descriptions = new string[lines.Length - 1];
            for (int i = 0; i < lines.Length - 1; i++)
            {
                string[] tokens = SplitRespectingQuotes(lines[i + 1], ',');
                output.descriptions[i] = tokens[2];
                output.amounts[i] = double.Parse(tokens[3]);
            }

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
            else if (endpoint == "/ChaseImport".ToLower())
            {
                return ChaseImport(inputJson);
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