using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.PowerBI.Api.V2;
using Microsoft.PowerBI.Api.V2.Models;
using Microsoft.Rest;
using Newtonsoft.Json;
using PBIEmbedPOC.Models;

namespace PBIEmbedPOC.Services
{
    public class EmbedService : IEmbedService
    {
        private readonly EmbedSettings _settings;

        private TokenCredentials _tokenCredentials;

        private string AuthorityUrl => _settings.AuthorityUrl;
        private string ResourceUrl => _settings.ResourceUrl;
        private string ApplicationId => _settings.ApplicationId;
        private string ApiUrl => _settings.ApiUrl;
        private string WorkspaceId => _settings.WorkspaceId;
        private string ReportId => _settings.ReportId;

        private string AuthenticationType => _settings.AuthenticationType;
        private string ApplicationSecret => _settings.ApplicationSecret;
        private string Tenant => _settings.Tenant;
        private string Username => _settings.Username;
        private string Password => _settings.Password;

        public EmbedConfig EmbedConfig { get; private set; }

        public TileEmbedConfig TileEmbedConfig { get; private set; }

        public EmbedService(IOptions<EmbedSettings> embedSettings)
        {
            _settings = embedSettings.Value;
            _tokenCredentials = null;
            EmbedConfig = new EmbedConfig();
            TileEmbedConfig = new TileEmbedConfig();
        }

        public async Task<bool> EmbedReport(string username, string roles)
        {
            // Get token credentials for user
            var getCredentialsResult = await GetTokenCredentials();
            if (!getCredentialsResult)
            {
                // The error message set in GetTokenCredentials
                return false;
            }

            try
            {
                // Create a Power BI Client object. It will be used to call Power BI APIs.
                using (var client = new PowerBIClient(new Uri(ApiUrl), _tokenCredentials))
                {
                    // Get a list of reports.
                    var reports = await client.Reports.GetReportsInGroupAsync(WorkspaceId);

                    // No reports retrieved for the given workspace.
                    if (!reports.Value.Any())
                    {
                        EmbedConfig.ErrorMessage = "No reports were found in the workspace";
                        return false;
                    }

                    Report report;
                    if (string.IsNullOrWhiteSpace(ReportId))
                    {
                        // Get the first report in the workspace.
                        report = reports.Value.FirstOrDefault();
                    }
                    else
                    {
                        report = reports.Value.FirstOrDefault(r =>
                            r.Id.Equals(ReportId, StringComparison.InvariantCultureIgnoreCase));
                    }

                    if (report == null)
                    {
                        EmbedConfig.ErrorMessage =
                            "No report with the given ID was found in the workspace. Make sure ReportId is valid.";
                        return false;
                    }

                    var datasets = await client.Datasets.GetDatasetByIdInGroupAsync(WorkspaceId, report.DatasetId);
                    EmbedConfig.IsEffectiveIdentityRequired = datasets.IsEffectiveIdentityRequired;
                    EmbedConfig.IsEffectiveIdentityRolesRequired = datasets.IsEffectiveIdentityRolesRequired;
                    GenerateTokenRequest generateTokenRequestParameters;
                    // This is how you create embed token with effective identities
                    if (!string.IsNullOrWhiteSpace(username))
                    {
                        var rls = new EffectiveIdentity(username, new List<string> {report.DatasetId});
                        if (!string.IsNullOrWhiteSpace(roles))
                        {
                            var rolesList = new List<string>();
                            rolesList.AddRange(roles.Split(','));
                            rls.Roles = rolesList;
                        }

                        rls.CustomData = "BR001";

                        // Generate Embed Token with effective identities.
                        generateTokenRequestParameters = new GenerateTokenRequest("view",
                            identities: new List<EffectiveIdentity> {rls});
                    }
                    else
                    {
                        // Generate Embed Token for reports without effective identities.
                        generateTokenRequestParameters = new GenerateTokenRequest("view");
                    }

                    var tokenResponse =
                        await client.Reports.GenerateTokenInGroupAsync(WorkspaceId, report.Id,
                            generateTokenRequestParameters);

                    if (tokenResponse == null)
                    {
                        EmbedConfig.ErrorMessage = "Failed to generate embed token.";
                        return false;
                    }

                    // Generate Embed Configuration.
                    EmbedConfig.EmbedToken = tokenResponse;
                    EmbedConfig.EmbedUrl = report.EmbedUrl;
                    EmbedConfig.Id = report.Id;
                }
            }
            catch (HttpOperationException exc)
            {
                EmbedConfig.ErrorMessage =
                    $"Status: {exc.Response.StatusCode} ({(int) exc.Response.StatusCode})\r\nResponse: {exc.Response.Content}\r\nRequestId: {exc.Response.Headers["RequestId"].FirstOrDefault()}";
                return false;
            }

            return true;
        }

        public async Task<bool> EmbedDashboard()
        {
            // Get token credentials for user
            var getCredentialsResult = await GetTokenCredentials();
            if (!getCredentialsResult)
            {
                // The error message set in GetTokenCredentials
                return false;
            }

            try
            {
                // Create a Power BI Client object. It will be used to call Power BI APIs.
                using (var client = new PowerBIClient(new Uri(ApiUrl), _tokenCredentials))
                {
                    // Get a list of dashboards.
                    var dashboards = await client.Dashboards.GetDashboardsInGroupAsync(WorkspaceId);

                    // Get the first report in the workspace.
                    var dashboard = dashboards.Value.FirstOrDefault();

                    if (dashboard == null)
                    {
                        EmbedConfig.ErrorMessage = "Workspace has no dashboards.";
                        return false;
                    }

                    // Generate Embed Token.
                    var generateTokenRequestParameters = new GenerateTokenRequest("view");
                    var tokenResponse = await client.Dashboards.GenerateTokenInGroupAsync(WorkspaceId, dashboard.Id,
                        generateTokenRequestParameters);

                    if (tokenResponse == null)
                    {
                        EmbedConfig.ErrorMessage = "Failed to generate embed token.";
                        return false;
                    }

                    // Generate Embed Configuration.
                    EmbedConfig = new EmbedConfig
                    {
                        EmbedToken = tokenResponse,
                        EmbedUrl = dashboard.EmbedUrl,
                        Id = dashboard.Id
                    };

                    return true;
                }
            }
            catch (HttpOperationException exc)
            {
                EmbedConfig.ErrorMessage =
                    $"Status: {exc.Response.StatusCode} ({(int) exc.Response.StatusCode})\r\nResponse: {exc.Response.Content}\r\nRequestId: {exc.Response.Headers["RequestId"].FirstOrDefault()}";
                return false;
            }
        }

        public async Task<bool> EmbedTile()
        {
            // Get token credentials for user
            var getCredentialsResult = await GetTokenCredentials();
            if (!getCredentialsResult)
            {
                // The error message set in GetTokenCredentials
                TileEmbedConfig.ErrorMessage = EmbedConfig.ErrorMessage;
                return false;
            }

            try
            {
                // Create a Power BI Client object. It will be used to call Power BI APIs.
                using (var client = new PowerBIClient(new Uri(ApiUrl), _tokenCredentials))
                {
                    // Get a list of dashboards.
                    var dashboards = await client.Dashboards.GetDashboardsInGroupAsync(WorkspaceId);

                    // Get the first report in the workspace.
                    var dashboard = dashboards.Value.FirstOrDefault();

                    if (dashboard == null)
                    {
                        TileEmbedConfig.ErrorMessage = "Workspace has no dashboards.";
                        return false;
                    }

                    var tiles = await client.Dashboards.GetTilesInGroupAsync(WorkspaceId, dashboard.Id);

                    // Get the first tile in the workspace.
                    var tile = tiles.Value.FirstOrDefault();

                    // Generate Embed Token for a tile.
                    var generateTokenRequestParameters = new GenerateTokenRequest("view");
                    var tokenResponse = await client.Tiles.GenerateTokenInGroupAsync(WorkspaceId, dashboard.Id, tile?.Id,
                        generateTokenRequestParameters);

                    if (tokenResponse == null)
                    {
                        TileEmbedConfig.ErrorMessage = "Failed to generate embed token.";
                        return false;
                    }

                    // Generate Embed Configuration.
                    TileEmbedConfig = new TileEmbedConfig
                    {
                        EmbedToken = tokenResponse,
                        EmbedUrl = tile.EmbedUrl,
                        Id = tile.Id,
                        dashboardId = dashboard.Id
                    };

                    return true;
                }
            }
            catch (HttpOperationException exc)
            {
                EmbedConfig.ErrorMessage =
                    $"Status: {exc.Response.StatusCode} ({(int) exc.Response.StatusCode})\r\nResponse: {exc.Response.Content}\r\nRequestId: {exc.Response.Headers["RequestId"].FirstOrDefault()}";
                return false;
            }
        }

        /// <summary>
        /// Check if web.config embed parameters have valid values.
        /// </summary>
        /// <returns>Null if web.config parameters are valid, otherwise returns specific error string.</returns>
        private string GetWebConfigErrors()
        {
            // Application Id must have a value.
            if (string.IsNullOrWhiteSpace(ApplicationId))
            {
                return
                    "ApplicationId is empty. please register your application as Native app in https://dev.powerbi.com/apps and fill client Id in web.config.";
            }

            // Application Id must be a Guid object.
            Guid result;
            if (!Guid.TryParse(ApplicationId, out result))
            {
                return
                    "ApplicationId must be a Guid object. please register your application as Native app in https://dev.powerbi.com/apps and fill application Id in web.config.";
            }

            // Workspace Id must have a value.
            if (string.IsNullOrWhiteSpace(WorkspaceId))
            {
                return "WorkspaceId is empty. Please select a group you own and fill its Id in web.config";
            }

            // Workspace Id must be a Guid object.
            if (!Guid.TryParse(WorkspaceId, out result))
            {
                return
                    "WorkspaceId must be a Guid object. Please select a workspace you own and fill its Id in web.config";
            }

            if (AuthenticationType.Equals("MasterUser"))
            {
                // Username must have a value.
                if (string.IsNullOrWhiteSpace(Username))
                {
                    return "Username is empty. Please fill Power BI username in web.config";
                }

                // Password must have a value.
                if (string.IsNullOrWhiteSpace(Password))
                {
                    return "Password is empty. Please fill password of Power BI username in web.config";
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(ApplicationSecret))
                {
                    return
                        "ApplicationSecret is empty. please register your application as Web app and fill appSecret in web.config.";
                }

                // Must fill tenant Id
                if (string.IsNullOrWhiteSpace(Tenant))
                {
                    return "Invalid Tenant. Please fill Tenant ID in Tenant under web.config";
                }
            }

            return null;
        }

        private async Task<AuthenticationResult> DoAuthentication()
        {
//            if (AuthenticationType.Equals("MasterUser"))
//            {
//                var authResult = await AuthenticateAsync();
//                return authResult?.AccessToken;
//            }

            // For app only authentication, we need the specific tenant id in the authority url
            var tenantSpecificUrl = $"{AuthorityUrl}{Tenant}";
            var authenticationContext = new AuthenticationContext(tenantSpecificUrl);

            // Authentication using app credentials
            var credential = new ClientCredential(ApplicationId, ApplicationSecret);
            var authenticationResult = await authenticationContext.AcquireTokenAsync(ResourceUrl, credential);
            return authenticationResult;
        }
        
        private async Task<OAuthResult> AuthenticateAsync()
        {
            var oauthEndpoint = new Uri($"https://login.microsoftonline.com/common/oauth2/token");

            using (var client = new HttpClient())
            {
                var result = await client.PostAsync(oauthEndpoint, new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("resource", ResourceUrl),
                    new KeyValuePair<string, string>("client_id", ApplicationId),
                    new KeyValuePair<string, string>("grant_type", "password"),
                    new KeyValuePair<string, string>("username", Username),
                    new KeyValuePair<string, string>("password", Password),
                    new KeyValuePair<string, string>("scope", "openid")
                }));

                var content = await result.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<OAuthResult>(content);
            }
        }

        private class OAuthResult
        {
            [JsonProperty("token_type")]
            public string TokenType { get; set; }
            [JsonProperty("scope")]
            public string Scope { get; set; }
            [JsonProperty("experies_in")]
            public int ExpiresIn { get; set; }
            [JsonProperty("ext_experies_in")]
            public int ExtExpiresIn { get; set; }
            [JsonProperty("experies_on")]
            public int ExpiresOn { get; set; }
            [JsonProperty("not_before")] 
            public int NotBefore { get; set; }
            [JsonProperty("resource")]
            public Uri Resource { get; set; }
            [JsonProperty("access_token")]
            public string AccessToken { get; set; }
            [JsonProperty("refresh_token")]
            public string RefreshToken { get; set; }
        }
        
        private async Task<bool> GetTokenCredentials()
        {
            // var result = new EmbedConfig { Username = username, Roles = roles };
            var error = GetWebConfigErrors();
            if (error != null)
            {
                EmbedConfig.ErrorMessage = error;
                return false;
            }

            // Authenticate using created credentials
            AuthenticationResult authenticationResult;
            try
            {
                authenticationResult = await DoAuthentication();
            }
            catch (AggregateException exc)
            {
                EmbedConfig.ErrorMessage = exc.InnerException.Message;
                return false;
            }

            if (authenticationResult == null)
            {
                EmbedConfig.ErrorMessage = "Authentication Failed.";
                return false;
            }

            _tokenCredentials = new TokenCredentials(authenticationResult.AccessToken, "Bearer");
            return true;
        }
    }
}