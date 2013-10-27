using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using System.Text.RegularExpressions;
using System.IO.IsolatedStorage;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Net.Http;
using System.IO;

namespace CoinbaseTicker
{
    public partial class AuthenticationPage : PhoneApplicationPage
    {
        private static String COINBASE_URL = "https://coinbase.com/oauth/authorize?response_type=code&client_id={0}&redirect_uri=urn:ietf:wg:oauth:2.0:oob&scope=balance";
        private static String ACCESS_TOKEN_URL = "https://coinbase.com/oauth/token";


        // public delegate void NavigationEventArgs(object sender, EventArgs e);

        public AuthenticationPage()
        {
           InitializeComponent();
            String url = String.Format(COINBASE_URL, CoinbaseApi.ClientId);
            AuthWebView.Navigate(new Uri(url));
            AuthWebView.Navigated += NavComplete;
        }

        private async void NavComplete(object sender, NavigationEventArgs e)
        {
            
            Match match = Regex.Match(e.Uri.AbsoluteUri, @"https://coinbase.com/oauth/authorize/([A-Za-z0-9])");
            if (match.Success)
            {
                String[] tokens = e.Uri.AbsoluteUri.Split("/".ToCharArray());
                String authCode = tokens[tokens.Length - 1];
                IsolatedStorageSettings settings = IsolatedStorageSettings.ApplicationSettings;
                var values = new List<KeyValuePair<string, string>>
                {
                      new KeyValuePair<string, string>("code", authCode),
                      new KeyValuePair<string, string>("client_id", CoinbaseApi.ClientId),
                      new KeyValuePair<string, string>("client_secret", CoinbaseApi.ClientSecret),
                      new KeyValuePair<string, string>("grant_type", "authorization_code"),
                      new KeyValuePair<string, string>("redirect_uri", "urn:ietf:wg:oauth:2.0:oob")
                };
                var httpClient = new HttpClient(new HttpClientHandler());
                HttpResponseMessage response = await httpClient.PostAsync(ACCESS_TOKEN_URL, new FormUrlEncodedContent(values));
                response.EnsureSuccessStatusCode();
                var responseString = await response.Content.ReadAsStringAsync();
                Dictionary<string, string> jsonAttributes = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseString);
                CoinbaseApi.Instance.AccessToken = jsonAttributes[Constants.ACCESS_TOKEN];
                CoinbaseApi.Instance.RefreshToken = jsonAttributes[Constants.REFRESH_TOKEN];
                CoinbaseApi.Instance.IsAuthenticated = true;
                if (settings.Contains(Constants.ACCESS_TOKEN))
                {
                    settings[Constants.ACCESS_TOKEN] = CoinbaseApi.Instance.AccessToken ;
                }
                else
                {
                    settings.Add(Constants.ACCESS_TOKEN, CoinbaseApi.Instance.AccessToken);
                }
                if (settings.Contains(Constants.REFRESH_TOKEN))
                {
                    settings[Constants.REFRESH_TOKEN] = CoinbaseApi.Instance.RefreshToken;
                }
                else
                {
                    settings.Add(Constants.REFRESH_TOKEN, CoinbaseApi.Instance.RefreshToken);
                }
                settings[Constants.IS_AUTH] = true;
                settings.Save();
                NavigationService.GoBack();
            }
        }


    }
}