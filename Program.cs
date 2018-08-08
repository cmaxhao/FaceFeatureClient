/*******************************************************************************

INTEL CORPORATION PROPRIETARY INFORMATION
This software is supplied under the terms of a license agreement or nondisclosure
agreement with Intel Corporation and may not be copied or disclosed except in
accordance with the terms of that agreement
Copyright(c) 2013 Intel Corporation. All Rights Reserved.

*******************************************************************************/

using DF_FaceTracking.cs.HttpServer;
using System;
using System.Windows.Forms;
using DF_FaceTracking.cs.File;

namespace DF_FaceTracking.cs
{
    static class Program
    {
        private static String ipAddress = ConfigReader.GetConfigValue("IP");
        private static int portAddress = Int32.Parse(ConfigReader.GetConfigValue("port"));
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            //Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);

            //PXCMSession session = PXCMSession.CreateInstance();
            //if (session != null)
            //{
            //    Application.Run(new MainForm(session,"feature"));
            //    session.Dispose();
            //}
            ExampleServer server = new ExampleServer(ipAddress, portAddress);
            server.Logger = new ConsoleLogger();
            try
            {
                server.Start();
            }
            catch (Exception e)
            {
                WriteLog.WriteError(e.ToString());
            }
        }
    }
}
