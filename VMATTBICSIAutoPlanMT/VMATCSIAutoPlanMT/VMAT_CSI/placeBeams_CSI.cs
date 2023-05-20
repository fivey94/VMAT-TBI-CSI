﻿using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Media.Media3D;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using System.Runtime.ExceptionServices;
using System.Text;

namespace VMATCSIAutoPlanMT.VMAT_CSI
{
    public class PlaceBeams_CSI : PlaceBeamsBase
    {
        //plan, list<iso name, number of beams>
        private List<Tuple<string, List<Tuple<string, int>>>> planIsoBeamInfo;
        private double isoSeparation = 0;
        private double[] collRot;
        private double[] CW = { 181.0, 179.0 };
        private double[] CCW = { 179.0, 181.0 };
        private ExternalBeamMachineParameters ebmpArc;
        private List<VRect<double>> jawPos;

        public PlaceBeams_CSI(StructureSet ss, List<Tuple<string, List<Tuple<string, int>>>> planInfo, double[] coll, List<VRect<double>> jp, string linac, string energy, string calcModel, string optModel, string gpuDose, string gpuOpt, string mr, bool overlap, double overlapMargin)
        {
            selectedSS = ss;
            planIsoBeamInfo = new List<Tuple<string, List<Tuple<string, int>>>>(planInfo);
            collRot = coll;
            jawPos = new List<VRect<double>>(jp);
            ebmpArc = new ExternalBeamMachineParameters(linac, energy, 600, "ARC", null);
            //copy the calculation model
            calculationModel = calcModel;
            optimizationModel = optModel;
            useGPUdose = gpuDose;
            useGPUoptimization = gpuOpt;
            MRrestart = mr;
            //user wants to contour the overlap between fields in adjacent VMAT isocenters
            contourOverlap = overlap;
            contourOverlapMargin = overlapMargin;
        }

        //to handle system access exception violation
        [HandleProcessCorruptedStateExceptions]
        public override bool Run()
        {
            try
            {
                return GeneratePlanList();
            }
            catch(Exception e)
            {
                ProvideUIUpdate($"{e.Message}", true);
                stackTraceError = e.StackTrace;
                return true;
            }
        }

        private (bool, double) GetBrainZCenter(ref int counter, ref int calcItems)
        {
            bool fail = false;
            double brainZCenter = 0.0;
            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Retrieving PTV_Brain Structure");
            Structure ptvBrain = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "ptv_brain"));
            if (ptvBrain == null)
            {
                calcItems += 1;
                ProvideUIUpdate((int)(100 * ++counter / calcItems), "Failed to find PTV_Brain Structure! Retrieving brain structure");
                ptvBrain = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "brain"));
                if (ptvBrain == null)
                {
                    ProvideUIUpdate("Failed to retrieve brain structure! Cannot calculate isocenter positions! Exiting", true);
                    fail = true;
                    return (fail, brainZCenter);
                }
            }

            ProvideUIUpdate($"Calculating center of PTV_Brain");
            brainZCenter = ptvBrain.CenterPoint.z;
            ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Center of PTV_Brain: {brainZCenter:0.0} mm");
            return (fail, brainZCenter);
        }

        private double ScaleSpineYPosition(double spineYMin, double spineYCenter, double scaleFactor)
        {
            spineYMin *= scaleFactor;
            //absolute value accounts for positive or negative y position in DCM coordinates
            if (Math.Abs(spineYMin) < Math.Abs(spineYCenter))
            {
                ProvideUIUpdate($"0.8 * PTV_Spine Ymin is more posterior than center of PTV_Spine!: {spineYMin:0.0} mm vs {spineYCenter:0.0} mm");
                spineYMin = spineYCenter;
                ProvideUIUpdate($"Assigning Ant-post iso location to center of PTV_Spine: {spineYMin:0.0} mm");
            }
            else
            {
                ProvideUIUpdate($"0.8 * Anterior extent of PTV_spine: {spineYMin:0.0} mm");
            }
            return spineYMin;
        }

        private (bool, double, double, double) GetSpineYminZminZMax(ref int counter, ref int calcItems)
        {
            bool fail = false;
            double spineYMin = 0.0;
            double spineZMax = 0.0;
            double spineZMin = 0.0;
            calcItems += 5;
            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Retrieving PTV_Spine Structure");
            Structure ptvSpine = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "ptv_spine"));
            if (ptvSpine == null)
            {
                calcItems += 1;
                ProvideUIUpdate((int)(100 * ++counter / calcItems), "Failed to find PTV_Spine Structure! Retrieving spinal cord structure");
                ptvSpine = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "spinalcord") || string.Equals(x.Id.ToLower(), "spinal_cord"));
                if (ptvSpine == null)
                {
                    ProvideUIUpdate("Failed to retrieve spinal cord structure! Cannot calculate isocenter positions! Exiting", true);
                    fail = true;
                    return (fail, spineYMin, spineZMin, spineZMax);
                }
            }

            ProvideUIUpdate("Calculating anterior extent of PTV_Spine");
            //Place field isocenters in y-direction at 2/3 the max 
            spineYMin = (ptvSpine.MeshGeometry.Positions.Min(p => p.Y));
            ProvideUIUpdate("Calculating superior and inferior extent of PTV_Spine");
            spineZMax = ptvSpine.MeshGeometry.Positions.Max(p => p.Z);
            spineZMin = ptvSpine.MeshGeometry.Positions.Min(p => p.Z);
            if (!ptvSpine.Id.ToLower().Contains("ptv"))
            {
                ProvideUIUpdate("Adding 5 mm anterior margin to spinal cord structure to mimic anterior extent of PTV_Spine!");
                spineYMin += 5;
                ProvideUIUpdate("Adding 10 mm superior margin to spinal cord structure to mimic superior extent of PTV_Spine!");
                spineZMax += 10.0;
                ProvideUIUpdate("Adding 15 mm inferior margin to spinal cord structure to mimic inferior extent of PTV_Spine!");
                spineZMin -= 15.0;
            }
            ProvideUIUpdate($"Anterior extent of PTV_Spine: {spineYMin:0.0} mm");
            ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Superior extent of PTV_Spine: {spineZMax:0.0} mm");
            ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Inferior extent of PTV_Spine: {spineZMin:0.0} mm");

            spineYMin = ScaleSpineYPosition(spineYMin, ptvSpine.CenterPoint.y, 0.8);
            ProvideUIUpdate((int)(100 * ++counter / calcItems));
            return (fail, spineYMin, spineZMin, spineZMax);
        }

        private VVector RoundIsocenterPositions(VVector v, ExternalPlanSetup plan, ref int counter, ref int calcItems)
        {
            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Rounding Y- and Z-positions to nearest integer values");
            //round z position to the nearest integer
            v = selectedSS.Image.DicomToUser(v, plan);
            v.x = Math.Round(v.x / 10.0f) * 10.0f;
            v.y = Math.Round(v.y / 10.0f) * 10.0f;
            v.z = Math.Round(v.z / 10.0f) * 10.0f;
            ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Calculated isocenter position (user coordinates): ({v.x}, {v.y}, {v.z})");
            v = selectedSS.Image.UserToDicom(v, plan);
            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Adding calculated isocenter position to stack!");
            return v;
        }

        protected override List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> GetIsocenterPositions()
        {
            UpdateUILabel("Calculating isocenter positions: ");
            ProvideUIUpdate(0, "Extracting isocenter positions for all plans");
            List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> allIsocenters = new List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> { };
            Image image = selectedSS.Image;
            VVector userOrigin = image.UserOrigin;
            int count = 0;
            List<Tuple<string, List<string>>> planIdTargets = new List<Tuple<string, List<string>>>(TargetsHelper.GetTargetListForEachPlan(prescriptions));
            foreach (ExternalPlanSetup itr in plans)
            {
                ProvideUIUpdate($"Retrieving number of isocenters for plan: {itr.Id}");
                int numIsos = planIsoBeamInfo.FirstOrDefault(x => string.Equals(x.Item1, itr.Id)).Item2.Count;
                int counter = 0;
                int calcItems = numIsos;
                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Num isos for plan (from generateTS): {itr.Id}");

                ProvideUIUpdate($"Retrieving prescriptions for plan: {itr.Id}");
                //grab the target in this plan with the greatest z-extent (plans can now have multiple targets assigned)
                ProvideUIUpdate((int)(100 * ++counter / calcItems), "Retrieved Presciptions");

                ProvideUIUpdate("Determining target with greatest extent");
                (bool fail, Structure longestTargetInPlan, double maxTargetLength, StringBuilder errorMessage) = TargetsHelper.GetLongestTargetInPlan(planIdTargets.FirstOrDefault(x => string.Equals(x.Item1, itr.Id)), selectedSS);
                if (fail)
                {
                    ProvideUIUpdate($"Error! No structure named: {errorMessage} found or contoured!", true);
                    return new List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> { };
                }
                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Longest target in plan {itr.Id}: {longestTargetInPlan.Id}");

                List<Tuple<VVector, string, int>> tmp = new List<Tuple<VVector, string, int>> { };
                isoSeparation = 380.0;
                if (string.Equals(longestTargetInPlan.Id.ToLower(), "ptv_csi"))
                {
                    (bool failSpineRetrival, double spineYMin, double spineZMin, double spineZMax) = GetSpineYminZminZMax(ref counter, ref calcItems);
                    if (failSpineRetrival) return allIsocenters;

                    (bool failBrainRetrival, double brainZCenter) = GetBrainZCenter(ref counter, ref calcItems);
                    if (failBrainRetrival) return allIsocenters;
                    //since Brain CTV = Brain and PTV = CTV + 5 mm uniform margin, center of brain is unaffected by adding the 5 mm margin if the PTV_Brain structure could not be found
                    ProvideUIUpdate($"Calculating distance between center of PTV_Brain and inf extent of PTV_Spine");
                    maxTargetLength = brainZCenter - spineZMin;
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Extent: {maxTargetLength:0.0} mm");

                    for (int i = 0; i < numIsos; i++)
                    {
                        VVector v = new VVector();
                        ProvideUIUpdate($"Determining position for isocenter: {i + 1}");
                        //special case when the main target is ptv_csi
                        //asign y position to spineYmin
                        v.y = spineYMin;
                        //assign the first isocenter to the center of the ptv_brain
                        if (i == 0) v.z = brainZCenter;
                        else
                        {
                            v.z = (spineZMin + (numIsos - i - 1) * isoSeparation + 180.0);
                            if(i == 1)
                            {
                                if (v.z + 200.0 > tmp.ElementAt(0).Item1.z) v.z = tmp.ElementAt(0).Item1.z - 200.0;
                            }
                        }
                        
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Calculated isocenter position {i + 1}");
                        tmp.Add(new Tuple<VVector, string, int>(RoundIsocenterPositions(v, itr, ref counter, ref calcItems),
                                                                planIsoBeamInfo.FirstOrDefault(x => string.Equals(x.Item1, itr.Id)).Item2.ElementAt(i).Item1,
                                                                planIsoBeamInfo.FirstOrDefault(x => string.Equals(x.Item1, itr.Id)).Item2.ElementAt(i).Item2));
                    }
                }
                else
                {
                    //assumes only one isocenter position for the plan (assuming it's the boost plan)
                    ProvideUIUpdate($"Determining position for isocenter: {1}");
                    VVector v = new VVector
                    {
                        x = userOrigin.x,
                        //assign y isocenter position to the center of the target
                        y = longestTargetInPlan.CenterPoint.y,
                        //assumes one isocenter if the target is not ptv_csi
                        z = longestTargetInPlan.CenterPoint.z
                    };

                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Calculated isocenter position {1}");
                    tmp.Add(new Tuple<VVector, string, int>(RoundIsocenterPositions(v, itr, ref counter, ref calcItems),
                                                            planIsoBeamInfo.FirstOrDefault(x => string.Equals(x.Item1, itr.Id)).Item2.ElementAt(0).Item1,
                                                            planIsoBeamInfo.FirstOrDefault(x => string.Equals(x.Item1, itr.Id)).Item2.ElementAt(0).Item2));
                }

                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Finished retrieving isocenters for plan: {itr.Id}");
                allIsocenters.Add(Tuple.Create(itr, new List<Tuple<VVector, string, int>>(tmp)));
                count++;
            }
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return allIsocenters;
        }

        protected override bool SetBeams(Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>> iso)
        {
            ProvideUIUpdate(0, $"Preparing to set isocenters for plan: {iso.Item1.Id}");
            int counter = 0;
            int calcItems = 3;
            bool initCSIPlan = false;
            //DRR parameters (dummy parameters to generate DRRs for each field)
            DRRCalculationParameters DRR = new DRRCalculationParameters
            {
                DRRSize = 500.0,
                FieldOutlines = true,
                StructureOutlines = true
            };
            DRR.SetLayerParameters(1, 1.0, 100.0, 1000.0);
            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Created default DRR parameters");

            //grab all prescriptions assigned to this plan
            List<string> targetIds = TargetsHelper.GetTargetIdListForPlan(prescriptions, iso.Item1.Id);
            //if any of the targets for this plan are ptv_csi, then you must use the special beam placement logic for the initial plan
            if (targetIds.Any(x => x.ToLower().Contains("ptv_csi")))
            {
                //verify that BOTH PTV spine and PTV brain exist in the current structure set! If not, create them (used to fit the field jaws to the target
                if (!selectedSS.Structures.Any(x => string.Equals(x.Id.ToLower(), "ptv_brain")))
                {
                    //uniform 5mm outer margin to create brain ptv from brain ctv/brain structure
                    (bool fail, StringBuilder info) = ContourHelper.CreateTargetStructure("PTV_Brain", "brain", selectedSS, new AxisAlignedMargins(StructureMarginGeometry.Outer, 5.0, 5.0, 5.0, 5.0, 5.0, 5.0));
                    ProvideUIUpdate(info.ToString());
                    if (fail) return true;
                    ProvideUIUpdate(100);
                }
                if (!selectedSS.Structures.Any(x => string.Equals(x.Id.ToLower(), "ptv_spine")))
                {
                    //ctv_spine = spinal_cord+0.5cm ANT, +1.5cm Inf, and +1.0 cm in all other directions
                    //ptv_spine = ctv_spine + 5 mm outer margin --> add 5 mm to the asymmetric margins used to create the ctv
                    (bool fail, StringBuilder info) = ContourHelper.CreateTargetStructure("PTV_Spine", "spinalcord", selectedSS, new AxisAlignedMargins(StructureMarginGeometry.Outer, 15.0, 10.0, 20.0, 15.0, 15.0, 15.0), "spinal_cord");
                    ProvideUIUpdate(info.ToString());
                    if (fail) return true;
                    ProvideUIUpdate(100);
                }
                //grab ptv_brain as we will need it for the first iso field placement
                target = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "ptv_brain"));
                initCSIPlan = true;
            }
            //assumes only one target for the boos plan
            else
            {
                target = selectedSS.Structures.FirstOrDefault(x => x.Id.Contains(TargetsHelper.GetHighestRxTargetIdForPlan(prescriptions, iso.Item1.Id)));
            }

            if (target == null || target.IsEmpty) 
            { 
                ProvideUIUpdate(0, $"Error! Target not found or is not contoured in plan {iso.Item1.Id}! Exiting!", true); 
                return true; 
            }
            ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Retrieved target for plan: {target.Id}");
            ProvideUIUpdate(100, "Preparation complete!");

            //place the beams for the VMAT plan
            int count = 0;
            string beamName;
            VRect<double> jp;
            calcItems = 0;
            counter = 0;
            //iso counter
            for (int i = 0; i < iso.Item2.Count; i++)
            {
                calcItems += iso.Item2.ElementAt(i).Item3 * 6;
                ProvideUIUpdate(0, $"Assigning isocenter: {i + 1}");

                if (target.Id.ToLower().Contains("ptv_brain") && i > 0) target = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("ptv_spine"));
                //beam counter
                for (int j = 0; j < iso.Item2.ElementAt(i).Item3; j++)
                {
                    Beam b;
                    beamName = $"{count + 1} ";

                    //kind of messy, but used to increment the collimator rotation one element in the array so you don't end up in a situation where the 
                    //single beam in this isocenter has the same collimator rotation as the single beam in the previous isocenter
                    if (i > 0 && iso.Item2.ElementAt(i).Item3 == 1 && iso.Item2.ElementAt(i - 1).Item3 == 1) j++;

                    jp = jawPos.ElementAt(j);
                    (bool result, VRect<double> jaws) = GetXYJawPositionsForStructure(initCSIPlan, i == 0, iso.Item2.ElementAt(i).Item1, 3.0, target);
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Retrieved jaw positions (iso: {i + 1}, beam: {j + 1})");
                    if(!result)
                    {
                        ProvideUIUpdate($"Calculated jaw positions:");
                        ProvideUIUpdate($"x1: {jaws.X1:0.0}");
                        ProvideUIUpdate($"x2: {jaws.X2:0.0}");
                        ProvideUIUpdate($"y1: {jaws.Y1:0.0}");
                        ProvideUIUpdate($"y2: {jaws.Y2:0.0}");
                    }

                    double coll = collRot[j];
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Retrieved collimator positions (iso: {i + 1}, beam: {j + 1})");

                    //all even beams (e.g., 2, 4, etc.) will be CCW and all odd beams will be CW
                    if (count % 2 == 0)
                    {
                        b = iso.Item1.AddArcBeam(ebmpArc, jp, coll, CCW[0], CCW[1], GantryDirection.CounterClockwise, 0, iso.Item2.ElementAt(i).Item1);
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Added arc beam to iso: {i}");

                        if (j >= 2) beamName += $"CCW {iso.Item2.ElementAt(i).Item2}{90}";
                        else beamName += $"CCW {iso.Item2.ElementAt(i).Item2}";
                    }
                    else
                    {
                        b = iso.Item1.AddArcBeam(ebmpArc, jp, coll, CW[0], CW[1], GantryDirection.Clockwise, 0, iso.Item2.ElementAt(i).Item1);
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Added arc beam to iso: {i + 1}");

                        if (j >= 2) beamName += $"CW {iso.Item2.ElementAt(i).Item2}{90}";
                        else beamName += $"CW {iso.Item2.ElementAt(i).Item2}";
                    }
                    //auto fit collimator to target structure
                    //circular margin (in mm), target structure, use asymmetric x Jaws, use asymmetric y jaws, optimize collimator rotation
                    if (target.Id.ToLower().Contains("ptv_brain"))
                    {
                        double buffer = Math.Abs(target.MeshGeometry.Positions.Min(p => p.Y)) - Math.Abs(b.IsocenterPosition.y);
                        buffer -= Math.Abs(target.MeshGeometry.Positions.Min(p => p.X)) - Math.Abs(b.IsocenterPosition.x);
                        if (buffer < 0) buffer = 0;
                        ProvideUIUpdate($"Delta between lateral and AP projection of {target.Id} structure: {buffer:0.0} mm");
                        //original (3/28/23) 30.0,40.0,30.0,30.0
                        b.FitCollimatorToStructure(new FitToStructureMargins(30.0 + buffer, 40.0, 30.0 + buffer, 30.0), target, true, true, false);
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Fit collimator to: {target.Id}");
                        ProvideUIUpdate($"Asymmetric margin: {3.0 + buffer / 10: 0.0} cm Lat, {3.0} cm Sup, {4.0} cm Inf");
                    }
                    else
                    {
                        //original (3/28/23) 30.0
                        b.FitCollimatorToStructure(new FitToStructureMargins(45.0, 30.0, 45.0, 30.0), target, true, true, false);
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Fit collimator to: {target.Id}");
                        ProvideUIUpdate($"Asymmetric margin: {4.5} cm Lat, {3.0} cm Sup-Inf");
                    }

                    b.Id = beamName;
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Assigned beam id: {beamName}");

                    b.CreateOrReplaceDRR(DRR);
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Assigned DRR to beam: {beamName}");

                    count++;
                }
            }
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        private (bool, VRect<double>) GetXYJawPositionsForStructure(bool isInitCSIPlan, bool isFirstIso, VVector iso, double margin, Structure target = null)
        {
            double x1, y1, x2, y2;
            x1 = x2 = y1 = y2 = 0.0;
            if (isInitCSIPlan)
            {
                double startZ, stopZ;
                if(isFirstIso)
                {
                    //first isocenter in brain
                    Structure brain = StructureTuningHelper.GetStructureFromId("PTV_Brain", selectedSS);
                    if (brain == null || brain.IsEmpty) return (true, new VRect<double>());
                    y1 = brain.MeshGeometry.Positions.Min(p => p.Z) - iso.z - margin * 10.0;
                    y2 = brain.MeshGeometry.Positions.Max(p => p.Z) - iso.z + margin * 10.0;
                    startZ = brain.MeshGeometry.Positions.Min(p => p.Z);
                    stopZ = brain.MeshGeometry.Positions.Max(p => p.Z);
                }
                else
                {
                    //first isocenter in brain
                    Structure spine = StructureTuningHelper.GetStructureFromId("PTV_Spine", selectedSS);
                    if (spine == null || spine.IsEmpty) return (true, new VRect<double>());
                    y2 = spine.MeshGeometry.Positions.Max(p => p.Z) - iso.z + margin*10.0;
                    if (y2 > 200.0) y2 = 200.0;
                    y1 = spine.MeshGeometry.Positions.Min(p => p.Z) - iso.z - margin * 10.0;
                    if (y1 < -200.0) y1 = -200.0;
                    startZ = iso.z - y1;
                    stopZ = iso.z + y2;
                }
                Structure ptv_csi = StructureTuningHelper.GetStructureFromId("PTV_CSI", selectedSS);
                if (ptv_csi == null || ptv_csi.IsEmpty) return (true, new VRect<double>());
                x2 = GetMaxLatProjectionDistance(GetLateralStructureBoundingBox(ptv_csi, startZ, stopZ), iso) + margin * 10.0;
                x1 = -x2;
            }
            else
            {
                if(target == null || target.IsEmpty) return (true, new VRect<double>());
                x2 = GetMaxLatProjectionDistance(target, iso) + margin * 10.0;
                x2 = -x2;
                y2 = target.MeshGeometry.Positions.Max(p => p.Z) - iso.z + margin * 10.0;
                if (y2 > 200.0) y2 = 200.0;
                y1 = target.MeshGeometry.Positions.Min(p => p.Z) - iso.z  - margin * 10.0;
                if (y1 < -200.0) y1 = -200.0;
            }
            return (false, new VRect<double> (x1, y1, x2, y2));
        }
        
        private double GetMaxLatProjectionDistance(Structure target, VVector v)
        {
            double maxDimension = 0;
            Point3DCollection pts = target.MeshGeometry.Positions;
            if (pts.Max(p => p.X) - v.x > maxDimension) maxDimension = pts.Max(p => p.X) - v.x;
            if (Math.Abs(pts.Min(p => p.X) - v.x) > maxDimension) maxDimension = Math.Abs(pts.Min(p => p.X) - v.x);
            if (pts.Max(p => p.Y) - v.y > maxDimension) maxDimension = pts.Max(p => p.Y) - v.y;
            if (Math.Abs(pts.Min(p => p.Y) - v.y) > maxDimension) maxDimension = Math.Abs(pts.Min(p => p.Y) - v.y);
            return maxDimension;
        }
        private double GetMaxLatProjectionDistance(VVector[] boundingBox, VVector v)
        {
            double maxDimension = 0;
            if (boundingBox.Max(p => p.x) - v.x > maxDimension) maxDimension = boundingBox.Max(p => p.x) - v.x;
            if (Math.Abs(boundingBox.Min(p => p.x)) - v.x > maxDimension) maxDimension = Math.Abs(boundingBox.Min(p => p.x) - v.x);
            if (boundingBox.Max(p => p.y) - v.y > maxDimension) maxDimension = boundingBox.Max(p => p.y) - v.y;
            if (Math.Abs(boundingBox.Min(p => p.y)) - v.y > maxDimension) maxDimension = Math.Abs(boundingBox.Max(p => p.y) - v.y);
            return maxDimension;
        }

        private VVector[] GetLateralStructureBoundingBox(Structure target, double zMin, double zMax) 
        {
            MeshGeometry3D mesh = target.MeshGeometry;
            //get most inferior slice of ptv_csi (mesgeometry.bounds.z indicates the most inferior part of a structure)
            int startSlice = CalculationHelper.ComputeSlice(zMin, selectedSS);
            //only go to the most superior part of the lungs for contouring the arms
            int stopSlice = CalculationHelper.ComputeSlice(zMax, selectedSS);
            VVector[][] pts;
            double xMax, xMin, yMax, yMin;
            xMax = -500000000000.0;
            xMin = 500000000000.0;
            yMax = -500000000000.0;
            yMin = 500000000000.0; 
            for (int slice = startSlice; slice <= stopSlice; slice++)
            {
                //get body contour points
                pts = target.GetContoursOnImagePlane(slice);
                
                //find min and max x positions for the body on this slice (so we can adapt the box positions for each slice)
                for (int i = 0; i < pts.GetLength(0); i++)
                {
                    if (pts[i].Max(p => p.x) > xMax) xMax = pts[i].Max(p => p.x);
                    if (pts[i].Min(p => p.x) < xMin) xMin = pts[i].Min(p => p.x);
                    if (pts[i].Max(p => p.y) > yMax) yMax = pts[i].Max(p => p.y);
                    if (pts[i].Min(p => p.y) < yMin) yMin = pts[i].Min(p => p.y);
                }
            }
            VVector[] boundinBox = new[] {
                                    new VVector(xMax, 0, 0),
                                    new VVector(0, yMin, 0),
                                    new VVector(xMin, 0, 0),
                                    new VVector(0, yMax, 0)};
            return boundinBox;
        }
    }
}
