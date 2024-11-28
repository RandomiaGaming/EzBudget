using Newtonsoft.Json;
using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;

public enum ChartType { Bar, Line, Pie }
public static class MicroServiceAClient
{
    private class RequestJsonSchema
    {
        public string chart_type;
        public double[] data;
        public string[] labels;
        public string title;
    }
    private class ResponseJsonSchema
    {
        public string chart;
    }
    public static Bitmap GenerateChart(ChartType chartType, double[] data, string[] labels, string title)
    {
        // Create request
        RequestJsonSchema request = new RequestJsonSchema();
        if (chartType == ChartType.Bar) { request.chart_type = "bar"; }
        else if (chartType == ChartType.Line) { request.chart_type = "line"; }
        else { request.chart_type = "pie"; }
        request.data = data;
        request.labels = labels;
        request.title = title;
        // Serialize request
        string inputJson = JsonConvert.SerializeObject(request);
        // Send the request
        HttpClient httpClient = new HttpClient();
        string url = $"http://127.0.0.1:8000/generate-chart";
        HttpContent content = new StringContent(inputJson, Encoding.UTF8, "application/json");
        HttpResponseMessage response = httpClient.PostAsync(url, content).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        string outputJson = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        // Parse output
        ResponseJsonSchema responseParsed = JsonConvert.DeserializeObject<ResponseJsonSchema>(outputJson);
        byte[] bytes = Convert.FromBase64String(responseParsed.chart);
        MemoryStream memoryStream = new MemoryStream(bytes);
        Bitmap output = new Bitmap(memoryStream);
        memoryStream.Dispose();
        return output;
    }
}