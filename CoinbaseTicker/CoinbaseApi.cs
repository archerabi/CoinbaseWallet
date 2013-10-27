using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Net.Http;
using System.IO.IsolatedStorage;
using System.Net.Http.Headers;
using System.Windows.Navigation;

namespace CoinbaseTicker
{
    /// <summary>
    /// Represents a Coinbase.com User 
    /// </summary>
    class CoinbaseUser
    {
        public string id { get; set; }
        public string name { get; set; }
        public string email { get; set; }
    }

    /// <summary>
    /// Coinbase error codes
    /// </summary>
    enum COINBASE_ERROR_CODE
    {
        TOKEN_REFRESH_FAILURE,
        INVALID_RESPONSE,
        UNKNOWN
    }

    /// <summary>
    /// An Exception object to represent errors from the Coinbase API
    /// </summary>
    class CoinbaseException : Exception
    {
        public COINBASE_ERROR_CODE code;

        public CoinbaseException(String message, COINBASE_ERROR_CODE code) :
            base(message)
        {
            this.code = code;
        }
    }

    /// <summary>
    /// C# helper for making requests to the Coinbase Api
    /// </summary>
    class CoinbaseApi
    {

        private static String ACCESS_TOKEN_URL = "https://coinbase.com/oauth/token";
        private static String BASE_URI = "http://coinbase.com/api/v1/";

        public static String ClientId;
        public static String ClientSecret;

        public String AccessToken = null;
        public String RefreshToken = null;

        public bool IsAuthenticated;

        private static CoinbaseApi Ref;

        public static CoinbaseApi Instance
        {
            get
            {
                if (Ref == null)
                {
                    Ref = new CoinbaseApi();
                }
                return Ref;
            }
        }

        private CoinbaseApi()
        {
            IsAuthenticated = false;
            IsolatedStorageSettings settings = IsolatedStorageSettings.ApplicationSettings;
            if (!settings.Contains(Constants.IS_AUTH))
            {
                settings.Add(Constants.IS_AUTH, IsAuthenticated);
            }
            else
            {
                IsAuthenticated = Convert.ToBoolean( settings[Constants.IS_AUTH] );
            }
            if (settings.Contains(Constants.ACCESS_TOKEN))
            {
                AccessToken = settings[Constants.ACCESS_TOKEN].ToString();
            }
            if (settings.Contains(Constants.REFRESH_TOKEN))
            {
                RefreshToken = settings[Constants.REFRESH_TOKEN].ToString();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<double> RequestBuyPrice()
        {
            String response = await Call(BASE_URI + "/prices/buy", HttpMethod.Get,false);
            JObject jsonObject = JObject.Parse(response);
            String value = jsonObject.GetValue("amount").ToString();
            return Convert.ToDouble(value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<double> RequestSellPrice()
        {
             String response = await Call(BASE_URI + "/prices/sell", HttpMethod.Get,false);
             JObject jsonObject = JObject.Parse(response);
             String value = jsonObject.GetValue("amount").ToString();
             return Convert.ToDouble(value);
        }

        /// <summary>
        /// Get information about the user
        /// </summary>
        /// <returns></returns>
        public async Task<CoinbaseUser> GetUser()
        {
            String response = await Call(BASE_URI + "/users", HttpMethod.Get,true);
            JObject root = JObject.Parse(response);
            IList<JToken> results = root["users"].Children().ToList();
            try
            {
                JToken userToken = results[0]["user"];
                CoinbaseUser user = JsonConvert.DeserializeObject<CoinbaseUser>(userToken.ToString());
                return user;
            }
            catch (Exception e)
            {
                throw new CoinbaseException(e.Message, COINBASE_ERROR_CODE.INVALID_RESPONSE);
            }
        }


        /// <summary>
        /// Get current users Balance in BTC
        /// </summary>
        /// <returns>the users balance in BTC</returns>
        public async Task<Double> GetBalance()
        {
            String response = null;
            Task<String> task = Call(BASE_URI + "account/balance", HttpMethod.Get,true);
            response = await task;
            if (response == null)
            {
                throw new CoinbaseException("Server returned null.", COINBASE_ERROR_CODE.INVALID_RESPONSE);
            }
            JObject jsonObject = JObject.Parse(response);
            return Convert.ToDouble(jsonObject.GetValue("amount").ToString());

        }

        /// <summary>
        /// Given a url and requestType , makes a call to the coinbase API
        /// </summary>
        /// <param name="url">url of the method. This is appended to https://coinbase.com/api/v1</param>
        /// <param name="requestType">HTTP metho</param>
        /// <returns></returns>
        private async Task<String> Call(String url, HttpMethod requestType,bool authRequired)
        {
            var httpClient = new HttpClient(new HttpClientHandler());
            IsolatedStorageSettings settings = IsolatedStorageSettings.ApplicationSettings;
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri(url),
                Method = HttpMethod.Get,
            };
            if (authRequired)
            {
                String accessToken = (string)settings[Constants.ACCESS_TOKEN];
                request.Headers.Add("Authorization", "Bearer " + accessToken);
            }
            try
            {
               
                if (requestType == HttpMethod.Get)
                {
                    HttpResponseMessage response = await httpClient.SendAsync(request);
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        bool success = await RefreshTokens();
                        if (!success)
                        {
                            throw new CoinbaseException("Token Refresh Failed", COINBASE_ERROR_CODE.TOKEN_REFRESH_FAILURE);
                        }
                        return await Call(url, requestType,authRequired);
                    }
                    else if (response.StatusCode == HttpStatusCode.OK)
                    {
                        var responseString = await response.Content.ReadAsStringAsync();
                        return responseString;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                if (e is CoinbaseException)
                {
                    throw e;
                }
                throw new CoinbaseException(e.Message, COINBASE_ERROR_CODE.UNKNOWN);
            }
            return null;
        }

        /// <summary>
        /// Refreshes the  access_token and refresh_token
        /// </summary>
        /// <returns></returns>
        private async Task<bool> RefreshTokens()
        {
            IsolatedStorageSettings settings = IsolatedStorageSettings.ApplicationSettings;
            var values = new List<KeyValuePair<string, string>>
                {
                      new KeyValuePair<string, string>("refresh_token",(string) settings[Constants.REFRESH_TOKEN]),
                      new KeyValuePair<string, string>("client_id", CoinbaseApi.ClientId),
                      new KeyValuePair<string, string>("client_secret", CoinbaseApi.ClientSecret),
                      new KeyValuePair<string, string>("grant_type", "refresh_token")
                };
            var httpClient = new HttpClient(new HttpClientHandler());
            try
            {
                HttpResponseMessage response = await httpClient.PostAsync(ACCESS_TOKEN_URL, new FormUrlEncodedContent(values));
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    SetAuth(false);
                    return false;
                }
                SetAuth(true);
                var responseString = await response.Content.ReadAsStringAsync();
                Dictionary<string, string> jsonAttributes = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseString);
                AccessToken = jsonAttributes[Constants.ACCESS_TOKEN];
                RefreshToken = jsonAttributes[Constants.REFRESH_TOKEN];
                settings[Constants.ACCESS_TOKEN] = AccessToken;
                settings[Constants.REFRESH_TOKEN] = RefreshToken;
                settings.Save();
            }
            catch (Exception e)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        ///  Updates the Auth state in settings and in memory
        /// </summary>
        /// <param name="isAuthenticated"></param>
        private void SetAuth(bool isAuthenticated)
        {
            this.IsAuthenticated = isAuthenticated;
            IsolatedStorageSettings settings = IsolatedStorageSettings.ApplicationSettings;
            settings[Constants.IS_AUTH] = isAuthenticated;
            settings.Save();
        }
    }
}
