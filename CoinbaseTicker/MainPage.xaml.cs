using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using CoinbaseTicker.Resources;
using System.IO.IsolatedStorage;
using System.ComponentModel;
using System.Windows.Resources;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Reflection;
using Microsoft.Phone.Tasks;

namespace CoinbaseTicker
{
    public partial class MainPage : PhoneApplicationPage
    {
        
        private CoinbaseModel model;

        private CoinbaseApi api;

        // Constructor
        public MainPage()
        {
            InitializeComponent();
            //IsolatedStorageSettings settings = IsolatedStorageSettings.ApplicationSettings;
            //settings.Clear();
          
        }

        //Sample code for building a localized ApplicationBar
        private void BuildLocalizedApplicationBar()
        {
            // Set the page's ApplicationBar to a new instance of ApplicationBar.
            ApplicationBar = new ApplicationBar();

            // Create a new button and set the text value to the localized string from AppResources.
            //ApplicationBarIconButton appBarButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/appbar.add.rest.png", UriKind.Relative));
            //appBarButton.Text = AppResources.AppBarButtonText;
            //ApplicationBar.Buttons.Add(appBarButton);

             var nameHelper = new AssemblyName(Assembly.GetExecutingAssembly().FullName);
            var version = nameHelper.Version;

            //Create a new menu item with the localized string from AppResources.
            ApplicationBarMenuItem appBarMenuItem = new ApplicationBarMenuItem("Version " + version);
            ApplicationBar.MenuItems.Add(appBarMenuItem);
            ApplicationBar.Mode = ApplicationBarMode.Minimized; 
            ApplicationBarMenuItem contactMenuItem = new ApplicationBarMenuItem(AppResources.Contact);
            ApplicationBar.MenuItems.Add(contactMenuItem);
            contactMenuItem.Click += ContactUsClicked;
        }

        private void ContactUsClicked(object sender, EventArgs e)
        {
            EmailComposeTask emailComposeTask = new EmailComposeTask();
            emailComposeTask.Subject = "CoinbaseWallet Feedback";
            emailComposeTask.To = "codearcherapps@gmail.com";
            emailComposeTask.Show();
        }

        
        private void PhoneApplicationPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadingBar.Visibility = Visibility.Visible;
            BuildLocalizedApplicationBar();
            api = CoinbaseApi.Instance;
            model = new CoinbaseModel();
            //Get the client id and client secret
            Stream s = Application.GetResourceStream(new Uri("/CoinbaseTicker;component/key.txt", UriKind.Relative)).Stream;
            StreamReader reader = new StreamReader(s);
            String x = reader.ReadToEnd();
            JObject obj = JObject.Parse(x);
            CoinbaseApi.ClientId = obj["client_id"].ToString();
            CoinbaseApi.ClientSecret = obj["client_secret"].ToString();

            if (! api.IsAuthenticated || api.AccessToken == null)
            {
                LoginButton.Visibility = Visibility.Visible;
                LoadingBar.Visibility = Visibility.Collapsed;
            }
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += GetUnsecuredInfo;
            worker.RunWorkerAsync();
        }

        private async void GetUnsecuredInfo(object sender, DoWorkEventArgs e)
        {
            try
            {
                model.SellPrice = await api.RequestSellPrice();
                model.BuyPrice = await api.RequestBuyPrice();
                Dispatcher.BeginInvoke(() =>
                {
                    BuyPriceText.Text = Convert.ToString(model.BuyPrice) + " USD";
                    SellPriceText.Text = Convert.ToString(model.SellPrice) + " USD";
                    if (!api.IsAuthenticated)
                    {
                        LoadingBar.Visibility = Visibility.Collapsed;
                    }
                });
                if (api.IsAuthenticated)
                {
                    model.Balance = await api.GetBalance();
                    System.Diagnostics.Debug.WriteLine(" Balance ≈ " + model.Balance);
                    model.user = await api.GetUser();
                    Dispatcher.BeginInvoke(() =>
                    {
                        BalanceText.Text = Convert.ToString(model.Balance) + " BTC ≈ " + Convert.ToString(model.Balance * model.SellPrice) + " USD";
                        UserText.Text = model.user.name;
                        LoadingBar.Visibility = Visibility.Collapsed;
                        BalanceLabel.Visibility = Visibility.Visible;
                        LoginButton.Visibility = Visibility.Collapsed;
                    });
                }

            }
            catch (CoinbaseException ex)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (ex.code == COINBASE_ERROR_CODE.TOKEN_REFRESH_FAILURE)
                    {
                        NavigationService.Navigate(new Uri("/AuthenticationPage.xaml", UriKind.Relative));
                    }
                    else
                    {
                        MessageBox.Show("Ooops! Something went wrong!");
                    }
                });

            }
            catch (Exception ex1)
            {
                System.Diagnostics.Debug.WriteLine(ex1.Message);
            }

        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new Uri("/AuthenticationPage.xaml", UriKind.Relative));
        }
    }
}