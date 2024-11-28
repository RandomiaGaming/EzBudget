using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Drawing;

namespace MicroServiceD
{
    public static class Program
    {
        public const string LogoDatasetPath = "D:\\ImportantData\\School\\EzBudget\\MicroServiceD\\dataset";

        // Json Schemas
        public sealed class BlankJsonSchema
        {

        }
        public sealed class StatusJsonSchema
        {
            public string status;
        }
        public sealed class GetCompanyLogoInputJsonSchema
        {
            public string companyName;
        }
        public sealed class GetCompanyLogoOutputJsonSchema
        {
            public string status;
            public string companyLogo;
        }
        // Functions
        public static string Check(string inputJson)
        {
            BlankJsonSchema input = JsonConvert.DeserializeObject<BlankJsonSchema>(inputJson);
            StatusJsonSchema output = new StatusJsonSchema();
            output.status = "OK";
            return JsonConvert.SerializeObject(output);
        }
        public static string Exit(string inputJson)
        {
            BlankJsonSchema input = JsonConvert.DeserializeObject<BlankJsonSchema>(inputJson);
            ExitRequested = true;
            StatusJsonSchema output = new StatusJsonSchema();
            output.status = "OK";
            return JsonConvert.SerializeObject(output);
        }
        public static string GetCompanyLogo(string inputJson)
        {
            GetCompanyLogoInputJsonSchema input = JsonConvert.DeserializeObject<GetCompanyLogoInputJsonSchema>(inputJson);

            Bitmap logo = new Bitmap(Path.Combine(LogoDatasetPath, "DefaultIcon.png"));
            foreach (string file in Directory.GetFiles(LogoDatasetPath))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (input.companyName.ToLower().Replace(" ", "") == fileName.ToLower().Replace(" ", ""))
                {
                    logo.Dispose();
                    logo = new Bitmap(file);
                }
            }

            GetCompanyLogoOutputJsonSchema output = new GetCompanyLogoOutputJsonSchema();
            output.status = "OK";
            MemoryStream logoStream = new MemoryStream();
            logo.Save(logoStream, System.Drawing.Imaging.ImageFormat.Png);
            output.companyLogo = Convert.ToBase64String(logoStream.ToArray());
            return JsonConvert.SerializeObject(output);
        }
        // Status Variables
        public static int Port = -1;
        public static bool ExitRequested = false;
        // Main Helper Functions
        public static int PortFromArgs(string[] args)
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
        public static int GetFreePort()
        {
            int output = -1;
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            output = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return output;
        }
        public static string ProcessMessage(string endpoint, string inputJson)
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
            else if (endpoint == "/GetCompanyLogo".ToLower())
            {
                return GetCompanyLogo(inputJson);
            }
            else
            {
                return "{\"status\":\"Bad endpoint.\"}";
            }
        }
        public static void RunOnPort(int port)
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
                Port = PortFromArgs(args);
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
