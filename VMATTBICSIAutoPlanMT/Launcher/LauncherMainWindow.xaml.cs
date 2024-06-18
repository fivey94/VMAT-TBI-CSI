﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.IO;
using System.Diagnostics;
using System.Reflection;

namespace Launcher
{
    /// <summary>
    /// Interaction logic for LauncherMainWindow.xaml
    /// </summary>
    public partial class LauncherMainWindow : Window
    {
        string arguments = "";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="startupArgs"></param>
        public LauncherMainWindow(List<string> startupArgs)
        {
            InitializeComponent();
            if (startupArgs.Any())
            {
                if (startupArgs.Count > 2) LaunchOptBtn.Visibility = Visibility.Visible;
                //patient mrn, structure set
                arguments = $"{startupArgs.ElementAt(0)} {startupArgs.ElementAt(1)}";
            }
        }

        /// <summary>
        /// Button event to launch the TBI autoplanner prep script
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VMATTBIBtn_Click(object sender, RoutedEventArgs e)
        {
            LaunchExe("VMATTBIAutoPlanMT");
        }

        /// <summary>
        /// Button event to launch the CSI autoplanner prep script
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VMATCSIBtn_Click(object sender, RoutedEventArgs e)
        {
            LaunchExe("VMATCSIAutoPlanMT");
        }

        /// <summary>
        /// Button event to launch the CSI Halcyon autoplanner prep script
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VMATCSIHalBtn_Click(object sender, RoutedEventArgs e)
        {
            LaunchExe("VMATCSIAutoPlanHalcyon");
        }




        /// <summary>
        /// Button event to launch the optimization loop script
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LaunchOptLoopBtn_Click(object sender, RoutedEventArgs e)
        {
            LaunchExe("VMATTBICSIOptLoopMT");
        }

        /// <summary>
        /// Helper method to launch the executable with name matching the supplied name
        /// </summary>
        /// <param name="exeName"></param>
        private void LaunchExe(string exeName)
        {
            string path = AppExePath(exeName);
            if (!string.IsNullOrEmpty(path))
            {
                ProcessStartInfo p = new ProcessStartInfo(path)
                {
                    Arguments = arguments
                };
                Process.Start(p);
                this.Close();
            }
            else MessageBox.Show(String.Format("Error! {0} executable NOT found!", exeName));
        }

        /// <summary>
        /// Same method in the .cs launcher (can't use external libraries in single file plugins)
        /// </summary>
        /// <param name="exeName"></param>
        /// <returns></returns>
        private string AppExePath(string exeName)
        {
            return FirstExePathIn(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), exeName);
        }

        /// <summary>
        /// Same method in the .cs launcher (can't use external libraries in single file plugins)
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="exeName"></param>
        /// <returns></returns>
        private string FirstExePathIn(string dir, string exeName)
        {
            return Directory.GetFiles(dir, "*.exe").FirstOrDefault(x => x.Contains(exeName));
        }
    }
}
