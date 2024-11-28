using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;

public sealed class ParsedChaseImport
{
    public double[] Amounts;
    public string[] Descriptions;
}
public static class MicroServiceBClient
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
    private static readonly string MicroServicePath = "D:\\ImportantData\\School\\EzBudget\\MicroServiceB\\bin\\Debug\\MicroServiceB.exe";
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
    public static ParsedChaseImport ChaseImport(string chaseCSV)
    {
        ChaseImportInputJsonSchema input = new ChaseImportInputJsonSchema();
        input.chaseCSV = chaseCSV;
        string inputJson = JsonConvert.SerializeObject(input);
        string outputJson = SendRequest("Exit", inputJson);
        ChaseImportOutputJsonSchema output = JsonConvert.DeserializeObject<ChaseImportOutputJsonSchema>(outputJson);
        ParsedChaseImport parsedOutput = new ParsedChaseImport();
        parsedOutput.Amounts = output.amounts;
        parsedOutput.Descriptions = output.descriptions;
        return parsedOutput;
    }
}