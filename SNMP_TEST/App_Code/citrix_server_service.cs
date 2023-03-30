using System.Net.NetworkInformation;

namespace SNMP_TEST.App_Code
{
    internal class citrix_server_service
    {
        protected string citrix_server;
        private bool isServerOn (string citrix_server)
        {
            bool pingable = false;
            Ping? pinger = null;

            try
            {
                pinger = new Ping();
                PingReply reply = pinger.Send(citrix_server);
                pingable = reply.Status == IPStatus.Success;
            } catch (PingException e)
            {
                // Console
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
        protected void Restart_Service (String cServer)
        {

        }
    }

   
}
