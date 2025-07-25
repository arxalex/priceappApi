using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Configuration;
using RestSharp;

namespace priceapp.proxy;

public class SessionParameters
{
    private readonly IConfiguration _configuration;
    private readonly RestClient _client;
    private bool _isActualizeProxyAtbPricesActive;


    public SessionParameters(IConfiguration configuration)
    {
        _configuration = configuration;
        IsActualizeProxyAtbPricesActive = false;
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri($"{_configuration["Domain:ProxyApi"]}/")
        };

        _client = new RestClient(httpClient);
    }

    public bool IsActualizeProxyAtbPricesActive
    {
        get
        {
            if (!bool.Parse(_configuration["Proxy:MultiInstance"]) || bool.Parse(_configuration["Proxy:IsProxy"]))
            {
                return _isActualizeProxyAtbPricesActive;
            }

            try
            {
                var request = new RestRequest("Info/status/actualize/proxy");

                var response = _client.ExecuteAsync(request).Result;

                if (response.StatusCode != HttpStatusCode.OK || response.Content == null)
                    throw new ConnectionAbortedException("Could not get data from Atb");

                var result = bool.Parse(response.Content);

                return result;
            }
            catch (Exception)
            {
                return false;
            }
        }
        set { _isActualizeProxyAtbPricesActive = value; }
    }
}