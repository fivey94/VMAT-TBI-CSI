﻿using System;
using System.Linq;
using System.Threading;
using System.Timers;
using System.IO;
using System.Collections.Generic;
using EvilDICOM.Core;
using EvilDICOM.Network;
using EvilDICOM.Network.Enums;
using EvilDICOM.Core.Helpers;

namespace ImportListener
{
    class ImportListener
    {
        static string path;
        static string mrn;
        static string ariaDBAET;
        static string ariaDBIP;
        static int ariaDBPort;
        static string localAET;
        static int localPort;
        //timeout in seconds (30 mins by default)
        static double timeoutSec = 30 * 60.0;
        static int updateFrequencyMSec = 100;

        static bool filePresent = false;
        static bool fileReadyForImport = false;
        static string theFile;
        static double elapsedSec = 0.0;
        static System.Timers.Timer aTimer = null;
        const string _twirl = "-\\|/";
        static private int index = 0;

        /// <summary>
        /// Main function. Input arguments are passed from calling script and include import path, mrn, aria database daemon info, local daemon info, and timeout period
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            //args = new string[] { "\\\\shariatscap105\\Dicom\\RSDCM\\Import\\", "$CSIDryRun_2", "VMSDBD" ,"10.151.176.60" ,"51402" ,"DCMTK" ,"50400" ,"3600" };
            if (!ParseInputArguments(args.ToList())) Run();
            else Console.WriteLine("Error! Unable to parse command line arguments! Cannot listen for RT structure set! Exiting");
            Console.WriteLine("Press any key to exit");
            Console.ReadLine();
        }

        /// <summary>
        /// Run control for the listener
        /// </summary>
        /// <returns></returns>
        private static bool Run()
        {
            try
            {
                SetTimer();
                PrintConfiguration();
                Console.WriteLine("Listening for RT structure set...");
                ListenForRTStruct();
                if (filePresent)
                {
                    //wait one minute to ensure autocontouring model is done writing rt struct
                    ResetTimer();
                    Console.WriteLine("Waiting for RT Struct file to be free for import...");
                    WaitForFile();
                    if (fileReadyForImport) ImportRTStructureSet();
                    else Console.WriteLine($"Auto contours for patient ({mrn}) were being used by another process and could not be imported. Exiting");
                }
                else Console.WriteLine($"Auto contours for patient ({mrn}) not found in time allotted. Exiting");
            }
            catch (Exception e)
            {
                aTimer.Stop();
                Console.Error.WriteLine(e.ToString());
                Console.Error.WriteLine(e.StackTrace);
                return true;
            }
            if(aTimer != null) aTimer.Dispose();
            return false;
        }

        /// <summary>
        /// Simple logic to parse the input string array of arguments. At least 7 arguments must be passed, the 8th is optional
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static bool ParseInputArguments(List<string> args)
        {
            if (args.Any())
            {
                path = args.ElementAt(0);
                mrn = args.ElementAt(1);
                ariaDBAET = args.ElementAt(2);
                ariaDBIP = args.ElementAt(3);
                ariaDBPort = int.Parse(args.ElementAt(4));
                localAET = args.ElementAt(5);
                localPort = int.Parse(args.ElementAt(6));
                if (args.Count() == 8) timeoutSec = double.Parse(args.ElementAt(7));
                return false;
            }
            else return true;
        }

        /// <summary>
        /// Print the listener configuration settings for this run
        /// </summary>
        /// <param name="listening"></param>
        private static void PrintConfiguration(bool listening = true)
        {
            Console.WriteLine(DateTime.Now);
            Console.WriteLine("Configuration:");
            Console.WriteLine($"Import path: {path}");
            Console.WriteLine($"Patient Id: {mrn}");
            Console.WriteLine($"Aria DB Daemon AE Title: {ariaDBAET}");
            Console.WriteLine($"Aria DB Daemon IP: {ariaDBIP}");
            Console.WriteLine($"Aria DB Daemon Port: {ariaDBPort}");
            Console.WriteLine($"Local Daemon AE Title: {localAET}");
            Console.WriteLine($"Local Daemon Port: {localPort}");
            Console.WriteLine($"Requested timeout: {timeoutSec} seconds");
            Console.WriteLine("");
        }

        /// <summary>
        /// Create, initialize, and start a timer to keep track of how much time has elapsed while listening
        /// </summary>
        private static void SetTimer()
        {
            // Create a timer with a 100 msec interval.
            aTimer = new System.Timers.Timer(updateFrequencyMSec);
            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += OnTimedEvent;
            aTimer.AutoReset = true;
            aTimer.Start();
        }

        /// <summary>
        /// Simple method to reset the elapsed time to 0 and restart the time
        /// </summary>
        private static void ResetTimer()
        {
            aTimer.Stop();
            elapsedSec = 0.0;
            aTimer.Start();
        }

        /// <summary>
        /// Once the structure set file has been found in the import folder,
        /// </summary>
        private static void WaitForFile()
        {
            while (!fileReadyForImport && elapsedSec < timeoutSec)
            {
                if (!IsFileLocked(new FileInfo(theFile)))
                {
                    fileReadyForImport = true;
                    Console.Write("\b");
                    Console.WriteLine($"RT Struct file ({theFile}) is ready for import");
                    Console.WriteLine("");
                }
                Wait(10000);
            }
            Console.WriteLine($"Elapsed time: {elapsedSec} sec");
        }

        /// <summary>
        /// Janky method to see if the listener can import/access the structure set file
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        private static bool IsFileLocked(FileInfo file)
        {
            try
            {
                using (FileStream stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            //file is not locked
            return false;
        }

        /// <summary>
        /// Called each 'tick' event. Updates the UI and increments the elapsed time by the update frequency
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private static void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            //increment the time on the progress window for each "tick", which is set to intervals of 0.1 second
            UpdateProgress();
            elapsedSec += (double)updateFrequencyMSec / 1000;
        }

        /// <summary>
        /// Update the UI with the twirl animation
        /// </summary>
        private static void UpdateProgress()
        {
            Console.Write("\b");
            Console.Write(_twirl[index++ % _twirl.Length]);
        }

        /// <summary>
        /// Method to construct the aria database and local daemons
        /// </summary>
        /// <returns></returns>
        private static (Entity, Entity) ConstructDaemons()
        {
            Entity ariaDBDaemon = new Entity(ariaDBAET, ariaDBIP, ariaDBPort);
            Entity localDaemon = Entity.CreateLocal(localAET, localPort);
            return (ariaDBDaemon, localDaemon);
        }

        /// <summary>
        /// Simple method to monitor the import folder for the structure set file (checks every 10 sec). Once found, set the file present flag to true
        /// </summary>
        private static void ListenForRTStruct()
        {
            while(!filePresent && elapsedSec < timeoutSec)
            {
                if (CheckDirectoryForRTStruct())
                {
                    filePresent = true;
                    aTimer.Stop();
                    Console.Write("\b");
                    Console.WriteLine($"Auto contours for patient {mrn} found");
                    Console.WriteLine("");
                }
                Wait(10000);
            }
            Console.WriteLine($"Elapsed time: {elapsedSec} sec");
        }

        /// <summary>
        /// Helper function to wait a specified amount of msec
        /// </summary>
        /// <param name="waitTime"></param>
        private static void Wait(int waitTime)
        {
            Thread.Sleep(waitTime);
        }

        /// <summary>
        /// Query the import folder for new dicom files. Open each dicom file present in import folder and see if the patient mrn matches the 
        /// mrn supplied as an input argument to the listener
        /// </summary>
        /// <returns></returns>
        private static bool CheckDirectoryForRTStruct()
        {
            foreach (string file in Directory.GetFiles(path))
            {
                //get the names of each patient whose CT data is in the CT DICOM dump directory
                DICOMObject dcmObj = DICOMObject.Read(file);
                if (string.Equals(dcmObj.FindFirst(TagHelper.PatientID).DData as string, mrn))
                {
                    theFile = file;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Push the dicom structure set file to the aria database
        /// </summary>
        /// <returns></returns>
        private static bool ImportRTStructureSet()
        {
            Console.WriteLine("Importing structure set now...");
            (Entity ariaDBDaemon, Entity localDaemon) = ConstructDaemons();
            if (PingDaemon(ariaDBDaemon, localDaemon)) return true;

            DICOMSCU client = new DICOMSCU(localDaemon);
            EvilDICOM.Network.SCUOps.CStorer storer = client.GetCStorer(ariaDBDaemon);
            ushort msgId = 1;
            DICOMObject dcm = DICOMObject.Read(theFile);

            Console.WriteLine("Executing C-store operation now...");
            EvilDICOM.Network.DIMSE.CStoreResponse response = storer.SendCStore(dcm, ref msgId);
            if ((Status)response.Status != Status.SUCCESS)
            {
                Console.WriteLine($"CStore failed");
            }
            else
            {
                Console.WriteLine($"DICOM C-Store from {localDaemon.AeTitle} => {ariaDBDaemon.AeTitle} @{ariaDBDaemon.IpAddress}:{ariaDBDaemon.Port}: {(Status)response.Status}");
                RemoveRTStructDcmFile(theFile);
            }
            return false;
        }

        /// <summary>
        /// Simple method to ensure the local and aria database daemons can communicate
        /// </summary>
        /// <param name="daemon"></param>
        /// <param name="local"></param>
        /// <returns></returns>
        private static bool PingDaemon(Entity daemon, Entity local)
        {
            Console.WriteLine($"C Echo from {local.AeTitle} => {daemon.AeTitle} @ {daemon.IpAddress} : {daemon.Port}");
            DICOMSCU client = new DICOMSCU(local);
            //5 sec timeout
            bool success = client.Ping(daemon, 5000);
            Console.WriteLine($"Success: {success}", !success);
            return !success;
        }

        /// <summary>
        /// Once the structure set file has been imported, remove the dicom file from the import folder
        /// </summary>
        /// <param name="theFile"></param>
        /// <returns></returns>
        private static bool RemoveRTStructDcmFile(string theFile)
        {
            Console.WriteLine($"Removing {theFile} now");
            try
            {
                File.Delete(theFile);
                Console.WriteLine($"{theFile} has been removed");
                return false;
            }
            catch(Exception e)
            {
                Console.WriteLine($"Could not remove {theFile}");
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                return true;
            }
        }
    }
}
