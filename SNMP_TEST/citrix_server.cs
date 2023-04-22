using Cassia;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.ServiceProcess;
using RestSharp;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Newtonsoft.Json;
using Microsoft.Azure.KeyVault;
using System.Configuration;
using System.Reflection.Metadata;
using System.Net;

namespace SNMP_TEST
{
    internal class citrix_server
    {
        //attributes
        const string Epic_Citrix_HyperSpace_Server = "Epic Citrix Hyperspace Server";
        const string Epic_Citrix_WebEAD_Server = "Epic Citrix WebEAD Server";
        const string desktopServiceName = "Citrix Desktop Service";
        const string EventLogSource = "Application";
        const string EventLogName = "Application";

        public string citrix_server_name { get; set; }
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
                WriteToEventLog(EventLogSource, string.Format("Unable to Ping make sure the server is online.", citrix_server_name),"Ping Failed");
                //EventLog eventLog = new EventLog("Application");
                //eventLog.Source = "SLUHNTrapperKeeper";
                //eventLog.WriteEntry(string.Format("Unable to Ping {0} make sure the server is onnline.", citrix_server_name));

            }
            finally
            {
                if (pinger != null)
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
                // Wait for Service to stop

                do
                {
                    // Just sit here and wait till the service is stopped.
                } while (serviceStatus(desktopServiceName, 0));

                StartService(desktopServiceName);
                LogWriter log = new LogWriter();
                log.WriteLog(citrix_server_name, "Restarted Desktop Service", Epic_Citrix_HyperSpace_Server);
            }
            else
            {

                EventLog eventLog = new EventLog(EventLogName);
                eventLog.Source = "SLUHNTrapperKeeper";
                eventLog.WriteEntry(string.Format("{0} is not respinding to ping.", citrix_server_name));

                return;
            }
        }

        public bool serviceStatus (string ServiceName, int requestedStatus)
        {

            try
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
            catch (Exception e)
            {
                EventLog eventLog = new EventLog(EventLogName);
                eventLog.Source = EventLogSource;
                eventLog.WriteEntry(string.Format("Error getting the status of the {0}\n Error message {1}", ServiceName, e.Message));
            }
            return false;
        }

        protected void StopService (string ServiceName)
        {
            try
            {

                ServiceController sc = new ServiceController(ServiceName, citrix_server_name);
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    try
                    {
                        sc.Stop();
                    }
                    catch (Exception e)
                    {
                        EventLog eventLog = new EventLog(EventLogName);
                        eventLog.Source = EventLogSource;
                        eventLog.WriteEntry(string.Format("Error stopping service {0} on server {1}.\n {2}", ServiceName, citrix_server_name, e.Message));
                    }


                }
            }
            catch (Exception e)
            {
                WriteToEventLog(EventLogSource, "Error connecting to machine.", e.Message);               
            }
        }
        
        protected void StartService (string ServiceName)
        {
            try
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
                        eventLog.Source = "SLUHNTrapperKeeper";
                        eventLog.WriteEntry(string.Format("Error starting service {0} on server {1}.\n {2}", ServiceName, citrix_server_name, e.Message));
                    }
                }
            }
            catch (Exception e)
            {
                WriteToEventLog(EventLogSource, "Error connecting to machine.", e.Message);
            }
        }

        // This seemed to either fail or take forever to get a response.  
        // Not even shure this is necessary.
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
                //GetAzureSecrets();
                if (!TestBearerToken())
                {
                    GetDaaSBearerToken();
                }
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
                EventLog eventLog = new EventLog("Application");
                eventLog.Source = "SLUHNTrapperKeeper";
                eventLog.WriteEntry(string.Format("Error getting the Bearer Token with {0}\n{1}\n{2}", 
                    Citrix_Cloud_API_Id_Value, 
                    Citrix_Cloud_API_Secret_Value));
                return;
            }
            //Console.WriteLine(response.Content);
        }

        private bool TestBearerToken()
        {
            string URL = "https://api-us.cloud.com/cvad/manage/About";
            var client = new RestClient();
            var request = new RestRequest(URL, Method.Post);
            request.AddHeader("Citrix-CustomerId", Citrix_Cloud_Customer_Id_Value);
            request.AddHeader("Citrix-InstanceId", Citrix_Cloud_Site_ID_Value);
            request.AddHeader("Authorization", string.Format("CwsAuth Bearer={0}", Citrix_Cloud_Bearer_Token));
            RestResponse response = client.Execute(request);
            if (response != null)
            {
                if(response.StatusCode != HttpStatusCode.OK)
                {
                    return false;
                } else { 
                    return true;
                }
            } else
            {
                return false;
            }
        }

        public static void GetAzureSecrets()
        {
            const string secretName_Citrix_Cloud_API_ID = "Citrix-Cloud-API-ID";
            const string secretName_Citrix_Cloud_API_Secret = "Citrix-Cloud-API-Secret";
            const string secretName_Citrix_Cloud_Customer_Id = "Citrix-Cloud-Customer-Id";
            const string secretName_Citrix_Cloud_Site_ID = "Citrix-Cloud-Site-ID";

            SetCloudAPIIDValue(ConfigurationManager.AppSettings[secretName_Citrix_Cloud_API_ID]);
            SetCloudAPISecretValue(ConfigurationManager.AppSettings[secretName_Citrix_Cloud_API_Secret]);
            SetCloudCustomerIdValue(ConfigurationManager.AppSettings[secretName_Citrix_Cloud_Customer_Id]);
            SetCloudSiteIdValue(ConfigurationManager.AppSettings[secretName_Citrix_Cloud_Site_ID]);

            // This is not working at the moment, leaving this for now until i can figure out how to get it working properly.  
            //const string keyVaultName = "KV-SLUHNPROD-Automation";
            //string kvUri = "https://" + keyVaultName + ".vault.azure.net";

            // Get the secret info to receive the bearer token
            // Generate the connection to the secret vault
            //var creds = new ManagedIdentityCredential("51f45e34-c525-4099-b325-49f711f6c5e9");
            //var creds = new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = "51f45e34-c525-4099-b325-49f711f6c5e9" });
            //var token = creds.GetToken(
            //    new Azure.Core.TokenRequestContext(
            //        new[] { "https://vault.azure.net/.default" }));
            //try
            //{

            //    //var client = new SecretClient(new Uri(kvUri), creds);


            //    var secretCloudAPIID = client.GetSecret(secretName_Citrix_Cloud_API_ID);
            //    var secretCloudAPISecret = client.GetSecret(secretName_Citrix_Cloud_API_Secret);
            //    var secretCloudCustomerID = client.GetSecret(secretName_Citrix_Cloud_Customer_Id);
            //    var secretCloudSiteID = client.GetSecret(secretName_Citrix_Cloud_Site_ID);
            //    if (secretCloudAPIID != null)
            //    {
            //        SetCloudAPIIDValue(secretCloudAPIID.Value.Value.ToString());
            //    }
            //    if (secretCloudAPISecret != null)
            //    {
            //        SetCloudAPISecretValue(secretCloudAPISecret.Value.Value.ToString());
            //        //Citrix_Cloud_API_Secret_Value = secretCloudAPISecret.Value.Value.ToString(); 
            //    }
            //    if (secretCloudCustomerID != null)
            //    {
            //        SetCloudCustomerIdValue(secretCloudCustomerID.Value.Value.ToString());
            //        //Citrix_Cloud_Customer_Id_Value = secretCloudCustomerID.Value.Value.ToString(); 
            //    }
            //    if (secretCloudSiteID != null)
            //    {
            //        SetCloudSiteIdValue(secretCloudSiteID.Value.Value.ToString());
            //        //Citrix_Cloud_Site_ID_Value = secretCloudSiteID.Value.Value.ToString(); 
            //    }

            //    //GetDaaSBearerToken();
            //}
            //catch (Exception e)
            //{
            //    EventLog eventLog = new EventLog("Application");
            //    eventLog.Source = "SLUHNTrapperKeeper";
            //    eventLog.WriteEntry(string.Format("Error getting Secrets from the KeyVault with message {0}", e.Message, creds.ToString()));
            //}


        }

        private void WriteToEventLog(string Source, string Message, string ExceptionMessage)
        {
            EventLog eventLog = new EventLog(EventLogName);
            eventLog.Source = Source;
            eventLog.WriteEntry(string.Format("{0} with exception {1}", Message, ExceptionMessage));

        }
    }
}
