﻿using System;
using System.Windows.Forms;
using engine;
using System.IO;

namespace sharpq3load_ui
{
    class Simulator
    {
		public static GameWindow mainfrm = null;

        [STAThread]
        static void Main(string[] args)
        {
			if (args.Length > 0)
			{
				throw new Exception("Unexpected number of command line arguments: " + Convert.ToString(args.Length));
			}

			try
			{
				if(!File.Exists(PATHS.GetMapsZipFile)) 
				{
					Directory.CreateDirectory(PATHS.GetTempDir);
					File.Copy(PATHS.GetSourceMapsZipFile, PATHS.GetMapsZipFile);
				}

				LOGGER.Info("Running simulator on " + DateTime.Now.ToString());
				mainfrm = new GameWindow();
				Application.Run(mainfrm);

				LOGGER.Close();
			}
			catch (Exception e)
			{
				if(mainfrm != null) mainfrm.Halt();
				ExceptionForm exForm = new ExceptionForm(e.Message + "\n" + e.StackTrace);
				LOGGER.Info(e.Message);
				LOGGER.Info(e.StackTrace);
				LOGGER.Close();
				exForm.ShowDialog();
			}
        }
    }
}