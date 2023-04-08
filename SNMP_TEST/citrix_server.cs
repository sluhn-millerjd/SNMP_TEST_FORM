using Cassia;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.ServiceProcess;
using RestSharp;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Newtonsoft.Json;
using Microsoft.Azure.KeyVault;

namespace SNMP_TEST
{
    internal class citrix_server
    {
        //attributes
        const string Epic_Citrix_HyperSpace_Server = "Epic Citrix Hyperspace Server";
        const string Epic_Citrix_WebEAD_Server = "Epic Citrix WebEAD Server";
        public string citrix_server_name { get; set; }
        const string desktopServiceName = "Citrix Desktop Service";
        public static string Citrix_Cloud_API_Id_Value;
        public static string Citrix_Cloud_API_Secret_Value;
        public static string Citrix_Cloud_Customer_Id_Value;
        public static string Citrix_Cloud_Site_ID_Value;
        public static string Citrix_Cloud_Bearer_Token;

        protected static void SetCloudAPIIDValue(string value) => Citrix_Cloud_API_Id_Value = value;

        protected static void SetCloudAPISecretValue(string value) => Citrix_Cloud_API_Secret_Value = value;

        protected static void SetCloudCustomerIdValue(string value) => Citrix_Cloud_Customer_Id_Value = value;

        protected static void SetCloudSiteIdValue(string value) => Citrix_Cloud_Site_ID_Value = value;

        protected static void SetCloudBearerToken(string value) => Citrix_Cloud_Bearer_Token = value;

        // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
        public class TokenRoot
        {
            public string token_type { get; set; }
            public string access_token { get; set; }
            public string expires_in { get; set; }
        }

        public citrix_server()
        {
            CloudAPIData cad = new CloudAPIData();
        }

        public class CloudAPIData
        {
            public string Citrix_Cloud_API_Id_Value { get; set; }
            public string Citrix_Cloud_API_Secret_Value { get; set; }
            public string Citrix_Cloud_Customer_Id_Value { get; set; }
            public string Citrix_Cloud_Site_ID_Value { get; set; }
            public string Citrix_Cloud_Bearer_Token { get; set; }

        }



        public bool PingHost ()
        {
            bool pingable = false;
            Ping pinger = null;

            try
            {
                pinger = new Ping();
                PingReply reply = pinger.Send(citrix_server_name);
                pingable = reply.Status == IPStatus.Success;
            } catch (PingException)
            {
                // 
                EventLog eventLog = new EventLog("Application");
                eventLog.Source = "Application";
                eventLog.WriteEntry(string.Format("Unable to Ping {0} make sure the server is onnline.", citrix_server_name));

            }
            finally
            {
                if(pinger != null)
                {
                    pinger.Dispose();
                }
            }

            return pingable;
        }

        public void RestartDesktopService()
        {
            if (PingHost())
            {
                StopService(desktopServiceName);
                
                StartService(desktopServiceName);
            }
            else
            {
                return;
            }
        }

        public void serviceStatus (string ServiceName, int requestedStatus)
        {

           
            ServiceController sc = new ServiceController(ServiceName, citrix_server_name);
            if (requestedStatus == 1)
            {
                sc.WaitForStatus(ServiceControllerStatus.Running);
            }
            else
            {
                sc.WaitForStatus(ServiceControllerStatus.Stopped);
            }

        }

        protected void StopService (string ServiceName)
        {
            ServiceController sc = new ServiceController(ServiceName, citrix_server_name);
            if (sc.Status == ServiceControllerStatus.Running)
            {
                try { 
                    sc.Stop(); 
                } catch (Exception e)
                {
                    EventLog eventLog = new EventLog("Application");
                    eventLog.Source = "Application";
                    eventLog.WriteEntry(string.Format("Error stopping service {0} on server {1}.\n {2}", ServiceName, citrix_server_name, e.Message));
                }


            }
        }
        
        protected void StartService (string ServiceName)
        {
            ServiceController sc = new ServiceController(ServiceName, citrix_server_name);
            if (sc.Status == ServiceControllerStatus.Stopped)
            {
                try
                {
                    sc.Start();
                }
                catch (Exception e)
                {
                    EventLog eventLog = new EventLog("Application");
                    eventLog.Source = "Application";
                    eventLog.WriteEntry(string.Format("Error starting service {0} on server {1}.\n {2}", ServiceName, citrix_server_name, e.Message));
                }
            }
        }

        public int GetDisconnectedSesssion()
        {
            int disconnectedCount = 0;
            ITerminalServicesManager manager = new TerminalServicesManager();
            using (ITerminalServer server = manager.GetRemoteServer(citrix_server_name))
            {
                server.Open();
                foreach(ITerminalServicesSession session in server.GetSessions())
                {
                    if(session.ConnectionState == ConnectionState.Disconnected)
                    {
                        disconnectedCount ++;
                    }
                }
            }
            return disconnectedCount;
        }

        public async void shutdownCitrixServer()
        {

            if(citrix_server.Citrix_Cloud_Customer_Id_Value == null || citrix_server.Citrix_Cloud_Site_ID_Value == null || citrix_server.Citrix_Cloud_Bearer_Token == null)
            {
                GetAzureSecrets();
                GetDaaSBearerToken();
            }
            string URL = String.Format("https://api-us.cloud.com/cvad/manage/Machines/{0}.slhn.org/{1}", citrix_server_name, "$shutdown");

            var client = new RestClient();
            var request = new RestRequest(URL, Method.Post);
            request.AddHeader("Citrix-CustomerId", Citrix_Cloud_Customer_Id_Value);
            request.AddHeader("Citrix-InstanceId", Citrix_Cloud_Site_ID_Value);
            request.AddHeader("Authorization", string.Format("CwsAuth Bearer={0}", Citrix_Cloud_Bearer_Token));
            RestResponse response = await client.ExecuteAsync(request);
            //Console.Write(response.Content);
            LogWriter logWriter = new LogWriter();
            logWriter.WriteLog(citrix_server_name, "Shutdown", Epic_Citrix_HyperSpace_Server);
        }

        private static void GetDaaSBearerToken()
        {
            //var options = new RestClientOptions("")
            //{
            //    MaxTimeout = -1,
            //};
            var client = new RestClient();
            var request = new RestRequest("https://api-us.cloud.com/cctrustoauth2/root/tokens/clients", Method.Post);
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("grant_type", "client_credentials");
            request.AddParameter("client_id", Citrix_Cloud_API_Id_Value);
            request.AddParameter("client_secret", Citrix_Cloud_API_Secret_Value);
            RestResponse response = client.Execute(request);

            TokenRoot bearerToken = JsonConvert.DeserializeObject<TokenRoot>(response.Content);
            if (bearerToken != null)
            {
                SetCloudBearerToken(bearerToken.access_token);
            } else
            {
                return;
            }
            //Console.WriteLine(response.Content);
        }

        public static void GetAzureSecrets()
        {
            const string secretName_Citrix_Cloud_API_ID = "Citrix-Cloud-API-ID";
            const string secretName_Citrix_Cloud_API_Secret = "Citrix-Cloud-API-Secret";
            const string secretName_Citrix_Cloud_Customer_Id = "Citrix-Cloud-Customer-Id";
            const string secretName_Citrix_Cloud_Site_ID = "Citrix-Cloud-Site-ID";
            const string keyVaultName = "KV-SLUHNPROD-Automation";
            string kvUri = "https://" + keyVaultName + ".vault.azure.net";

            // Get the secret info to receive the bearer token
            // Generate the connection to the secret vault
            var creds = new DefaultAzureCredential();
            var token = creds.GetToken(
                new Azure.Core.TokenRequestContext(
                    new[] { "https://vault.azure.net/.default" }));
            var client = new SecretClient(new Uri(kvUri), creds);
            
            
            var secretCloudAPIID = client.GetSecret(secretName_Citrix_Cloud_API_ID);
            var secretCloudAPISecret = client.GetSecret(secretName_Citrix_Cloud_API_Secret);
            var secretCloudCustomerID = client.GetSecret(secretName_Citrix_Cloud_Customer_Id);
            var secretCloudSiteID = client.GetSecret(secretName_Citrix_Cloud_Site_ID);

            if (secretCloudAPIID != null)
            {
                SetCloudAPIIDValue(secretCloudAPIID.Value.Value.ToString());
            }
            if (secretCloudAPISecret != null)
            {
                SetCloudAPISecretValue(secretCloudAPISecret.Value.Value.ToString());
                //Citrix_Cloud_API_Secret_Value = secretCloudAPISecret.Value.Value.ToString(); 
            }
            if (secretCloudCustomerID != null)
            {
                SetCloudCustomerIdValue(secretCloudCustomerID.Value.Value.ToString());
                //Citrix_Cloud_Customer_Id_Value = secretCloudCustomerID.Value.Value.ToString(); 
            }
            if (secretCloudSiteID != null)
            {
                SetCloudSiteIdValue(secretCloudSiteID.Value.Value.ToString());
                //Citrix_Cloud_Site_ID_Value = secretCloudSiteID.Value.Value.ToString(); 
            }

            GetDaaSBearerToken();
        }
    }
}
