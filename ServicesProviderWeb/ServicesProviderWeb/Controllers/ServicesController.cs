using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;

namespace ServicesProviderWeb.Controllers
{
    [ApiController]
    public class ServicesController : ControllerBase, IDisposable
    {
        private static readonly string _stockProviderUrl = "http://dev.markitondemand.com/Api/v2/Quote?symbol=MSFT";
        private static readonly string _weatherProviderBaseUrl = "https://albigiready19weatherservice.azurewebsites.net/api/";
        private static HttpClient _stockClient;
        private static HttpClient _weatherClient;

        public ServicesController()
        {
            _stockClient = new HttpClient();
            _weatherClient = new HttpClient(){ BaseAddress = new Uri(_weatherProviderBaseUrl)};
        }
        
        [HttpGet]
        [Route("api/stock")]
        public async Task<ActionResult<string>> GetStockAsync()
        {
            try
            {
                string result = await _stockClient.GetStringAsync(_stockProviderUrl);
                //convert to XML to extract the stock value
                System.Xml.Linq.XDocument doc = System.Xml.Linq.XDocument.Parse(result.Trim('\"'));
                var price = doc.Descendants("LastPrice").FirstOrDefault();

                return price.Value;                
            }
            catch (Exception e)
            {
                return e.ToString();
            }
        }

        [HttpGet]
        [Route("api/weather/{location=seattle}")]
        public async Task<ActionResult<string>> GetWeatherAsync(string location)
        {
            if (string.IsNullOrEmpty(location))
                return "Invalid location";

            location = location.Trim('/');
            try
            {
                return await _weatherClient.GetStringAsync(location);     
            }
            catch (Exception e)
            {
                return e.ToString();
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _stockClient.Dispose();
                    _weatherClient.Dispose();
                }
                //free native code
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

    }
}
