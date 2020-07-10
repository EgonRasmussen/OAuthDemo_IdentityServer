﻿using IdentityModel;
using IdentityModel.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace Client.M4
{
    [DesignTimeVisible(false)]
    public partial class MainPage : ContentPage
    {
        private static string CodeVerifier { get; set; }
        private static string Code { get; set; }
        private static string Token { get; set; }
        private static string TokenType { get; set; }

        public MainPage()
        {
            InitializeComponent();
        }

        private async void Authorize_Click(object sender, EventArgs e )
        {
            var state = Convert.ToBase64String(CryptoRandom.CreateRandomKey(25));
            CodeVerifier = Convert.ToBase64String(CryptoRandom.CreateRandomKey(50));

            var codeVerifierBytes = Encoding.ASCII.GetBytes(CodeVerifier);
            var hashedBytes = SHA256.Create().ComputeHash(codeVerifierBytes);
            var codeChallenge = Base64Url.Encode(hashedBytes);



            var url = "http://10.0.2.2:5000/connect/authorize" +
                      "?client_id=native_client" +
                      "&scope=wiredbrain_api.rewards" +
                      "&redirect_uri=com.pluralsight.windows:/callback" +
                      "&response_type=code" +
                      $"&state={WebUtility.UrlEncode(state)}" +
                      $"&code_challenge={WebUtility.UrlEncode(codeChallenge)}" +
                      "&code_challenge_method=S256";

            ResultFeed.Text += "\nStarting Authorization";
            ResultFeed.Text += $"\nState = {state}";
            ResultFeed.Text += $"\nCode Verifier = {CodeVerifier}";
            ResultFeed.Text += $"\nCode Challange = {codeChallenge}";

            var result = await new SystemBrowser().InvokeAsync(url);

            ResultFeed.Text += "\n\nAuthorization callback received...";

            if (result.State != state)
            {
                ResultFeed.Text += "\nState not recognised. Cannot trust response.";
                return;
            }

            Code = result.Code;

            ResultFeed.Text += "\nApplication Authorized!";
            ResultFeed.Text += $"\nAuthorization Code: {result.Code}";
            ResultFeed.Text += $"\nState: {result.State}";
        }

        private async void Token_Click(object sender, EventArgs e)
        {
            if (Code == null)
            {
                ResultFeed.Text += "\n\nNot ready! Authorize first.";
                return;
            }

            ResultFeed.Text += "\n\nCalling token endpoint...";

            //var tokenClient = new TokenClient("http://localhost:5000/connect/token", "native_client");
            //var tokenResponse = await tokenClient.RequestAuthorizationCodeAsync(Code, "com.pluralsight.windows:/callback", CodeVerifier);


            var client = new HttpClient();
            var tokenResponse = await client.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
            {
                Address = "http://10.0.2.2:5000/connect/token",
                ClientId = "native_client",
                ClientSecret = CodeVerifier,
                Code = Code,
                RedirectUri = "com.pluralsight.windows:/callback"
            });




            if (tokenResponse.IsError)
            {
                ResultFeed.Text += "\nToken request failed";
                return;
            }

            TokenType = tokenResponse.TokenType;
            Token = tokenResponse.AccessToken;
            ResultFeed.Text += "\n\nToken Received!";
            ResultFeed.Text += $"\naccess_token: {tokenResponse.AccessToken}";
            ResultFeed.Text += $"\nexpires_in: {tokenResponse.ExpiresIn}";
            ResultFeed.Text += $"\ntoken_type: {tokenResponse.TokenType}";
        }

        private async void Api_Click(object sender, EventArgs e)
        {
            var httpClient = new HttpClient();
            if (Token != null)
            {
                ResultFeed.Text += $"\n\nCalling API with Authorization header: {TokenType} {Token}";
                httpClient.SetBearerToken(Token);
            }

            var response = await httpClient.GetAsync("http://10.0.2.2:5002/api/rewards");

            if (response.IsSuccessStatusCode) ResultFeed.Text += "\n\nAPI access authorized!";
            else if (response.StatusCode == HttpStatusCode.Unauthorized) ResultFeed.Text += "\nUnable to contact API: Unauthorized!";
            else ResultFeed.Text += $"\nUnable to contact API. Status code {response.StatusCode}";
        }

    }

    public class SystemBrowser
    {
        static TaskCompletionSource<AuthorizeResponse> inFlightRequest;
        public Task<AuthorizeResponse> InvokeAsync(string url)
        {
            inFlightRequest?.TrySetCanceled();
            inFlightRequest = new TaskCompletionSource<AuthorizeResponse>();

            Browser.OpenAsync(new Uri(url), BrowserLaunchMode.SystemPreferred);

            return inFlightRequest.Task;
        }

        public static void HandleAuthorizationResult(Uri response)
        {
            var result = new AuthorizeResponse(response.OriginalString);

            inFlightRequest.SetResult(result);
            inFlightRequest = null;
        }
    }
}