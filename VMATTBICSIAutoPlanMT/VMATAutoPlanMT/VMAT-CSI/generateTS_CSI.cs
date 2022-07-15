﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Media.Media3D;

namespace VMATAutoPlanMT
{
    public class generateTS_CSI : generateTSbase
    { 
        //structure, sparing type, added margin
        public List<Tuple<string, string, double>> spareStructList;
        //DICOM types
        //Possible values are "AVOIDANCE", "CAVITY", "CONTRAST_AGENT", "CTV", "EXTERNAL", "GTV", "IRRAD_VOLUME", 
        //"ORGAN", "PTV", "TREATED_VOLUME", "SUPPORT", "FIXATION", "CONTROL", and "DOSE_REGION". 
        List<Tuple<string, string>> TS_structures;
        public int numIsos;
        public int numVMATIsos;
        public bool updateSparingList = false;

        public generateTS_CSI(List<Tuple<string, string>> ts, List<Tuple<string, string, double>> list, StructureSet ss)
        {
            TS_structures = new List<Tuple<string, string>>(ts);
            spareStructList = new List<Tuple<string, string, double>>(list);
            selectedSS = ss;
        }

        public override bool preliminaryChecks()
        {
            //check if user origin was set
            if (isUOriginInside()) return true;

            //get the points collection for the Body (used for calculating number of isocenters)
            Point3DCollection pts = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "body").MeshGeometry.Positions;

            //check if patient length is > 116cm, if so, check for matchline contour
            if ((pts.Max(p => p.Z) - pts.Min(p => p.Z)) > 1160.0 && !(selectedSS.Structures.Where(x => x.Id.ToLower() == "matchline").Any()))
            {
                //check to see if the user wants to proceed even though there is no matchplane contour or the matchplane contour exists, but is not filled
                confirmUI CUI = new confirmUI();
                CUI.message.Text = "No matchplane contour found even though patient length > 116.0 cm!" + Environment.NewLine + Environment.NewLine + "Continue?!";
                CUI.ShowDialog();
                if (!CUI.confirm) return true;

                //checks for LA16 couch and spinning manny couch/bolt will be performed at optimization stage
            }

            //For these cases the maximum number of allowed isocenters is 3.
            //the reason for the explicit statements calculating the number of isos and then truncating them to 3 was to account for patients requiring < 3 isos and if, later on, we want to remove the restriction of 3 isos
            numIsos = numVMATIsos = (int)Math.Ceiling(((pts.Max(p => p.Z) - pts.Min(p => p.Z)) / (400.0 - 20.0)));
            if (numIsos > 3) numIsos = numVMATIsos = 3;
            

            //set isocenter names based on numIsos and numVMATIsos (determined these names from prior cases)
            isoNames = new List<string>(new isoNameHelper().getIsoNames(numVMATIsos, numIsos));

            //check if selected structures are empty or of high-resolution (i.e., no operations can be performed on high-resolution structures)
            string output = "The following structures are high-resolution:" + System.Environment.NewLine;
            List<Structure> highResStructList = new List<Structure> { };
            List<Tuple<string, string, double>> highResSpareList = new List<Tuple<string, string, double>> { };
            foreach (Tuple<string, string, double> itr in spareStructList)
            {
                if (itr.Item2 == "Mean Dose < Rx Dose")
                {
                    if (selectedSS.Structures.First(x => x.Id == itr.Item1).IsEmpty)
                    {
                        MessageBox.Show(String.Format("Error! \nThe selected structure that will be subtracted from PTV_Body and TS_PTV_VMAT is empty! \nContour the structure and try again."));
                        return true;
                    }
                    else if (selectedSS.Structures.First(x => x.Id == itr.Item1).IsHighResolution)
                    {
                        highResStructList.Add(selectedSS.Structures.First(x => x.Id == itr.Item1));
                        highResSpareList.Add(itr);
                        output += String.Format("{0}", itr.Item1) + System.Environment.NewLine;
                    }
                }
            }
            //if there are high resolution structures, they will need to be converted to default resolution.
            if (highResStructList.Count() > 0)
            {
                //ask user if they are ok with converting the relevant high resolution structures to default resolution
                output += "They must be converted to default resolution before proceeding!";
                confirmUI CUI = new confirmUI();
                CUI.message.Text = output + Environment.NewLine + Environment.NewLine + "Continue?!";
                CUI.message.Font = new System.Drawing.Font("Microsoft Sans Serif", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
                CUI.ShowDialog();
                if (!CUI.confirm) return true;

                List<Tuple<string, string, double>> newData = convertHighToLowRes(highResStructList, highResSpareList, spareStructList);
                if (!newData.Any()) return true;
                spareStructList = new List<Tuple<string, string, double>>(newData);
                //inform the main UI class that the UI needs to be updated
                updateSparingList = true;
            }
            return false;
        }

        public override bool createTSStructures()
        {
            if (RemoveOldTSStructures(TS_structures)) return true;

            //Need to add the Human body, PTV_BODY, and TS_PTV_VMAT contours manually
            //if these structures were present, they should have been removed (regardless if they were contoured or not). 
            foreach (Tuple<string, string> itr in TS_structures.Where(x => x.Item2.ToLower().Contains("human") || x.Item2.ToLower().Contains("ptv")))
            {
                //4-15-2022 
                //if the human_body structure exists and is not null, it is likely this script has been run previously. As a precaution, copy the human_body structure onto the body (in case flash was requested
                //in the previous run of the script)
                //if (itr.Item2.ToLower() == "human_body" && tmp != null) selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "body").SegmentVolume = tmp.Margin(0.0);

                if (itr.Item2.ToLower().Contains("human") || itr.Item2.ToLower().Contains("ptv"))
                {
                    if (selectedSS.CanAddStructure(itr.Item1, itr.Item2))
                    {
                        selectedSS.AddStructure(itr.Item1, itr.Item2);
                        addedStructures.Add(itr.Item2);
                    }
                    else
                    {
                        MessageBox.Show(String.Format("Can't add {0} to the structure set!", itr.Item2));
                        return true;
                    }
                }
            }

            //determine if any TS structures need to be added to the selected structure set (i.e., were not present or were removed in the first foreach loop)
            //this is provided here to only add additional TS if they are relevant to the current case (i.e., it doesn't make sense to add the brain TS's if we 
            //are not interested in sparing brain)
            foreach (Tuple<string, string, double> itr in spareStructList)
            {
                optParameters.Add(Tuple.Create(itr.Item1, itr.Item2));
                if (itr.Item2 == "Mean Dose < Rx Dose")
                {
                    if (itr.Item1.ToLower().Contains("lungs"))
                    {
                        if (itr.Item2 == "Mean Dose < Rx Dose") foreach (Tuple<string, string> itr1 in TS_structures.Where(x => x.Item2.ToLower().Contains("lungs"))) AddTSStructures(itr1);
                        //do NOT add the scleroStructures to the addedStructures vector as these will be handled manually!
                        
                    }
                    else if (itr.Item1.ToLower().Contains("liver")) foreach (Tuple<string, string> itr1 in TS_structures.Where(x => x.Item2.ToLower().Contains("liver"))) AddTSStructures(itr1);
                    else if (itr.Item1.ToLower().Contains("brain")) foreach (Tuple<string, string> itr1 in TS_structures.Where(x => x.Item2.ToLower().Contains("brain"))) AddTSStructures(itr1);
                    else if (itr.Item1.ToLower().Contains("kidneys"))
                    {
                        foreach (Tuple<string, string> itr1 in TS_structures.Where(x => x.Item2.ToLower().Contains("kidneys"))) AddTSStructures(itr1);
                        //do NOT add the scleroStructures to the addedStructures vector as these will be handled manually!
                        
                    }
                }
            }

            //now contour the various structures
            foreach (string s in addedStructures)
            {
                Structure tmp = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == s.ToLower());
                //MessageBox.Show(s);
                if (!(s.ToLower().Contains("ptv")))
                {
                    Structure tmp1 = null;
                    double margin = 0.0;
                    if (s.ToLower().Contains("human")) tmp1 = selectedSS.Structures.First(x => x.Id.ToLower() == "body");
                    else if (s.ToLower().Contains("lungs")) if (selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "lungs_lowres") == null) tmp1 = selectedSS.Structures.First(x => x.Id.ToLower() == "lungs"); else tmp1 = selectedSS.Structures.First(x => x.Id.ToLower() == "lungs_lowres");
                    else if (s.ToLower().Contains("liver")) if (selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "liver_lowres") == null) tmp1 = selectedSS.Structures.First(x => x.Id.ToLower() == "liver"); else tmp1 = selectedSS.Structures.First(x => x.Id.ToLower() == "liver_lowres");
                    else if (s.ToLower().Contains("kidneys")) if (selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "kidneys_lowres") == null) tmp1 = selectedSS.Structures.First(x => x.Id.ToLower() == "kidneys"); else tmp1 = selectedSS.Structures.First(x => x.Id.ToLower() == "kidneys_lowres");
                    else if (s.ToLower().Contains("brain")) if (selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "brain_lowres") == null) tmp1 = selectedSS.Structures.First(x => x.Id.ToLower() == "brain"); else tmp1 = selectedSS.Structures.First(x => x.Id.ToLower() == "brain_lowres");

                    //all structures in TS_structures and scleroStructures are inner margins, which is why the below code works.
                    int pos1 = s.IndexOf("-");
                    int pos2 = s.IndexOf("cm");
                    if (pos1 != -1 && pos2 != -1) double.TryParse(s.Substring(pos1, pos2 - pos1), out margin);

                    //convert from cm to mm
                    tmp.SegmentVolume = tmp1.Margin(margin * 10);
                }
                else if (s.ToLower() == "ptv_body")
                {
                    //get the body contour and create the ptv structure using the user-specified inner margin
                    Structure tmp1 = selectedSS.Structures.First(x => x.Id.ToLower() == "body");
                   // tmp.SegmentVolume = tmp1.Margin(-targetMargin * 10);

                    //subtract all the structures the user wants to spare from PTV_Body
                    foreach (Tuple<string, string, double> spare in spareStructList)
                    {
                        if (spare.Item2 == "Mean Dose < Rx Dose")
                        {
                            
                            tmp1 = selectedSS.Structures.First(x => x.Id.ToLower() == spare.Item1.ToLower());
                            tmp.SegmentVolume = tmp.Sub(tmp1.Margin((spare.Item3) * 10));
                        }
                    }
                }
                else if (s.ToLower() == "ts_ptv_vmat")
                {
                    //copy the ptv_body contour onto the TS_ptv_vmat contour
                    Structure tmp1 = selectedSS.Structures.First(x => x.Id.ToLower() == "ptv_body");
                    tmp.SegmentVolume = tmp1.Margin(0.0);

                    //matchplane exists and needs to be cut from TS_PTV_Body. Also remove all TS_PTV_Body segements inferior to match plane
                    if (selectedSS.Structures.Where(x => x.Id.ToLower() == "matchline").Any())
                    {
                        //find the image plane where the matchline is location. Record this value and break the loop. Also find the first slice where the ptv_body contour starts and record this value
                        Structure matchline = selectedSS.Structures.First(x => x.Id.ToLower() == "matchline");
                        bool lowLimNotFound = true;
                        int lowLim = -1;
                        if (!matchline.IsEmpty)
                        {
                            int matchplaneLocation = 0;
                            for (int i = 0; i != selectedSS.Image.ZSize - 1; i++)
                            {
                                if (matchline.GetContoursOnImagePlane(i).Any())
                                {
                                    matchplaneLocation = i;
                                    break;
                                }
                                if (lowLimNotFound && tmp1.GetContoursOnImagePlane(i).Any())
                                {
                                    lowLim = i;
                                    lowLimNotFound = false;
                                }
                            }

                            if (selectedSS.Structures.Where(x => x.Id.ToLower() == "dummybox").Any()) selectedSS.RemoveStructure(selectedSS.Structures.First(x => x.Id.ToLower() == "dummybox"));
                            Structure dummyBox = selectedSS.AddStructure("CONTROL", "DummyBox");

                            //get min/max positions of ptv_body contour to contour the dummy box for creating TS_PTV_Legs
                            Point3DCollection ptv_bodyPts = tmp1.MeshGeometry.Positions;
                            double xMax = ptv_bodyPts.Max(p => p.X) + 50.0;
                            double xMin = ptv_bodyPts.Min(p => p.X) - 50.0;
                            double yMax = ptv_bodyPts.Max(p => p.Y) + 50.0;
                            double yMin = ptv_bodyPts.Min(p => p.Y) - 50.0;

                            //box with contour points located at (x,y), (x,0), (x,-y), (0,-y), (-x,-y), (-x,0), (-x, y), (0,y)
                            VVector[] pts = new[] {
                                        new VVector(xMax, yMax, 0),
                                        new VVector(xMax, 0, 0),
                                        new VVector(xMax, yMin, 0),
                                        new VVector(0, yMin, 0),
                                        new VVector(xMin, yMin, 0),
                                        new VVector(xMin, 0, 0),
                                        new VVector(xMin, yMax, 0),
                                        new VVector(0, yMax, 0)};

                            //give 5cm margin on TS_PTV_LEGS (one slice of the CT should be 5mm) in case user wants to include flash up to 5 cm
                            for (int i = matchplaneLocation - 1; i > lowLim - 10; i--) dummyBox.AddContourOnImagePlane(pts, i);

                            //do the structure manipulation
                            if (selectedSS.Structures.Where(x => x.Id.ToLower() == "ts_ptv_legs").Any()) selectedSS.RemoveStructure(selectedSS.Structures.First(x => x.Id.ToLower() == "ts_ptv_legs"));
                            Structure TS_legs = selectedSS.AddStructure("CONTROL", "TS_PTV_Legs");
                            TS_legs.SegmentVolume = dummyBox.And(tmp.Margin(0));
                            //subtract both dummybox and matchline from TS_PTV_VMAT
                            tmp.SegmentVolume = tmp.Sub(dummyBox.Margin(0.0));
                            tmp.SegmentVolume = tmp.Sub(matchline.Margin(0.0));
                            //remove the dummybox structure if flash is NOT being used as its no longer needed
                            if (!useFlash) selectedSS.RemoveStructure(dummyBox);
                        }
                    }
                }
            }
            return false;
        }
    }
}