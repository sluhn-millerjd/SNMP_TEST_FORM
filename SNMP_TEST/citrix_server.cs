using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace SNMP_TEST
{
    internal class citrix_server
    {
        //attributes
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public string citrix_server_name { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        
        public bool PingHost ()
        {
            bool pingable = false;
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            Ping pinger = null;
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

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
                sc.Stop();
            }
        }
        
        protected void StartService (string ServiceName)
        {
            ServiceController sc = new ServiceController(ServiceName, citrix_server_name);
            if (sc.Status == ServiceControllerStatus.Stopped)
            {
                sc.Start();
            }
        }
    }
}
