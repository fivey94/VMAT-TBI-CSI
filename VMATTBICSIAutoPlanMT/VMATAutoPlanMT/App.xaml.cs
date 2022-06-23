﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Reflection;

namespace VMATAutoPlanMT
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            selectOption SO = new selectOption();
            SO.ShowDialog();
            Window mw;
            List<string> theArguments;
            if (SO.isVMATTBI) { theArguments = new List<string>(e.Args.Append(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\VMAT_TBI_config.ini").ToList()); mw = new TBIAutoPlanMW(theArguments); }
            else if (SO.isVMATCSI) { theArguments = new List<string>(e.Args.Append(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\VMAT_CSI_config.ini").ToList()); mw = new CSIAutoPlanMW(theArguments); } 
            else return;
            mw.Show();
        }
    }
}
