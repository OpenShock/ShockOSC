using Serilog;
using System.Net;
using System.Text;

namespace OpenShock.ShockOsc;

public static class WebRequestApi
{
    private static readonly ILogger Logger = Log.ForContext(typeof(WebRequestApi));
    private static readonly HttpClient client;

    static WebRequestApi()
    {
        ServicePointManager.DefaultConnectionLimit = 10;
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

        var handler = new HttpClientHandler();
        client = new HttpClient(handler)
        {
            BaseAddress = Config.ConfigInstance.ShockLink.OpenShockApi
        };
    }

    public class RequestData
    {
        public string url = string.Empty;
        public HttpMethod method = HttpMethod.Get;
        public string? body;
        public Dictionary<string, string>? headers;
    }

    public static async Task<(HttpStatusCode, string)> DoRequest(RequestData requestData)
    {
        var request = new HttpRequestMessage(requestData.method, requestData.url);
        request.Headers.Add("User-Agent", "ShockOSC");
        request.Headers.Add("OpenShockToken", Config.ConfigInstance.ShockLink.ApiToken);
        if (requestData.headers != null)
        {
            foreach (var header in requestData.headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }
        }

        if (requestData.headers != null && requestData.body != null && !requestData.headers.ContainsKey("Content-Type"))
        {
            request.Headers.Add("Content-Type", "application/json; charset=utf-8");
        }

        if (requestData.body != null)
        {
            request.Content = new StringContent(requestData.body, Encoding.UTF8, "application/json");
        }

        try
        {
            var response = await client.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();
            Logger.Information($"{request.RequestUri} {responseString}");
            return (response.StatusCode, responseString);
        }
        catch (WebException webException)
        {
            if (webException.Response is HttpWebResponse response)
            {
                var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
                return (response.StatusCode, responseString);
            }
        }

        return (HttpStatusCode.InternalServerError, string.Empty);
    }
}