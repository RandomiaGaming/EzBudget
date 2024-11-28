using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;

public static class MicroServiceDClient
{
    // Json Schemas
    private sealed class BlankJsonSchema
    {

    }
    private sealed class StatusJsonSchema
    {
        public string status;
    }
    // Generic HTTP Post Client Stuff
    private static int _port = -1;
    private static HttpClient _httpClient = null;
    private static string SendRequest(string endpoint, string inputJson)
    {
        if (_port == -1)
        {
            throw new Exception("Init must be called before sending requests.");
        }
        string url = $"http://localhost:{_port}/{endpoint}";
        HttpContent content = new StringContent(inputJson, Encoding.UTF8, "application/json");
        HttpResponseMessage response = _httpClient.PostAsync(url, content).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        string outputJson = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        StatusJsonSchema output = JsonConvert.DeserializeObject<StatusJsonSchema>(outputJson);
        if (output.status.ToLower() != "OK".ToLower())
        {
            throw new Exception(output.status);
        }
        return outputJson;
    }
    public static void InitToExisting(int port)
    {
        _port = port;
        _httpClient = new HttpClient();
    }
    public static void Init()
    {
        Process notificationService = Process.Start(MicroServicePath);
        notificationService.WaitForExit();
        _port = notificationService.ExitCode;

        _httpClient = new HttpClient();
    }
    public static void Check()
    {
        BlankJsonSchema input = new BlankJsonSchema();
        string inputJson = JsonConvert.SerializeObject(input);
        SendRequest("Check", inputJson);
    }
    public static void Exit()
    {
        BlankJsonSchema input = new BlankJsonSchema();
        string inputJson = JsonConvert.SerializeObject(input);
        SendRequest("Exit", inputJson);
        _httpClient.Dispose();
        _httpClient = null;
        _port = -1;
    }
    // Custom HTTP Post Client Stuff
    private static readonly string MicroServicePath = "D:\\ImportantData\\School\\EzBudget\\MicroServiceD\\bin\\Debug\\MicroServiceD.exe";
    public sealed class GetCompanyLogoInputJsonSchema
    {
        public string companyName;
    }
    public sealed class GetCompanyLogoOutputJsonSchema
    {
        public string status;
        public string companyLogo;
    }
    public static Bitmap GetCompanyLogo(string companyName)
    {
        GetCompanyLogoInputJsonSchema input = new GetCompanyLogoInputJsonSchema();
        input.companyName = companyName;
        string inputJson = JsonConvert.SerializeObject(input);
        string outputJson = SendRequest("GetCompanyLogo", inputJson);
        GetCompanyLogoOutputJsonSchema output = JsonConvert.DeserializeObject<GetCompanyLogoOutputJsonSchema>(outputJson);
        byte[] outputBytes = Convert.FromBase64String(output.companyLogo);
        MemoryStream outputStream = new MemoryStream(outputBytes);
        Bitmap outputBitmap = new Bitmap(outputStream);
        outputStream.Dispose();
        return outputBitmap;
    }
}