using Cassia;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.ServiceProcess;
using RestSharp;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace SNMP_TEST
{
    internal class citrix_server
    {

        public citrix_server()
        {
            GetAzureSecrets();
        }


        //attributes
        public string? citrix_server_name { get; set; }
        const string desktopServiceName = "Citrix Desktop Service";
        private static string? Citrix_Cloud_API_Id_Value { get; set; }
        private static string? Citrix_Cloud_API_Secret_Value { get; set; }
        private static string? Citrix_Cloud_Customer_Id_Value { get; set; }
        private static string? Citrix_Cloud_Site_ID_Value { get; set; }

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

        public void shutdownCitrixServer()
        {

            string URL = String.Format("https://api-us.cloud.com/cvad/manage/Machines/{0}.slhn.org/{1}", citrix_server_name,"$shutdown");

            var options = new RestClientOptions("")
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest(URL, Method.Post);

            

        }

        private static async void GetAzureSecrets()
        {
            const string secretName_Citrix_Cloud_API_ID = "Citrix-Cloud-API-ID";
            const string secretName_Citrix_Cloud_API_Secret = "Citrix-Cloud-API-Secret";
            const string secretName_Citrix_Cloud_Customer_Id = "Citrix-Cloud-Customer-Id";
            const string secretName_Citrix_Cloud_Site_ID = "Citrix-Cloud-Site-ID";
            const string keyVaultName = "KV-SLUHNPROD-Automation";
            string kvUri = "https://" + keyVaultName + ".vault.azure.net";
            // Get the secret info to receive the bearer token
            // Generate the connection to the secret vault
            var client = new SecretClient(new Uri(kvUri), new DefaultAzureCredential());
            var secretCloudAPIID = await client.GetSecretAsync(secretName_Citrix_Cloud_API_ID);
            var secretCloudAPISecret = await client.GetSecretAsync(secretName_Citrix_Cloud_API_Secret);
            var secretCloudCustomerID = await client.GetSecretAsync(secretName_Citrix_Cloud_Customer_Id);
            var secretCloudSiteID = await client.GetSecretAsync(secretName_Citrix_Cloud_Site_ID);

            if (secretCloudAPIID != null)
            {
                Citrix_Cloud_API_Id_Value = secretCloudAPIID.Value.Value.ToString(); 
            }
            if (secretCloudAPISecret != null)
            {
                Citrix_Cloud_API_Secret_Value = secretCloudAPISecret.Value.Value.ToString(); 
            }
            if (secretCloudCustomerID != null)
            {
                Citrix_Cloud_Customer_Id_Value = secretCloudCustomerID.Value.Value.ToString(); 
            }
            if (secretCloudSiteID != null)
            {
                Citrix_Cloud_Site_ID_Value = secretCloudSiteID.Value.Value.ToString(); 
            }
        }
    }
}
