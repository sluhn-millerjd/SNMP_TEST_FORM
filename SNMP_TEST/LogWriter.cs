using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using static System.Environment;

namespace SNMP_TEST
{
    internal class LogWriter
    {
        private static string log_file_location;

        protected class data
        {
            public string ServerName { get; set; }
            public string DateTime { get; set; }
            public string Action { get; set; }
        }

        public LogWriter() {
            log_file_location = ConfigurationManager.AppSettings["log_file_location"];
        }

        public LogWriter(string name) { }

        public static void WriteLog(string serverName, string dateTime, string action)
        {
            List<data> _data = new List<data>();
            _data.Add(new data()
            {
                ServerName = serverName,
                DateTime = dateTime, 
                Action = action
            });

            string json = JsonSerializer.Serialize(_data, 
                new JsonSerializerOptions{ 
                    WriteIndented = true
                });
            //var commonPath = GetFolderPath(SpecialFolder.CommonApplicationData);
            if (!Directory.Exists(log_file_location))
            {
                Directory.CreateDirectory(log_file_location);
            }

            var logFilePath = Path.Combine(log_file_location,DateTime.Now.ToString("yyyyMMdd") + ".json");

            if (!File.Exists(logFilePath))
            {
                File.Create(logFilePath).Close();
            }
            _ = File.AppendAllTextAsync(logFilePath, json);
            
        }
    }
}
