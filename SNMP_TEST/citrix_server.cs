using Cassia;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.ServiceProcess;

namespace SNMP_TEST
{
    internal class citrix_server
    {
        //attributes
        public string citrix_server_name { get; set; }
        private static string desktopServiceName = "Citrix Desktop Service";

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
            } finally
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
    }
}
