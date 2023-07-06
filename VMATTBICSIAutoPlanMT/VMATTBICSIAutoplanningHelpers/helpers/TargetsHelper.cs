﻿using System;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using PlanType = VMATTBICSIAutoPlanningHelpers.Enums.PlanType;
using System.Windows.Media.Media3D;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class TargetsHelper
    {
        public static (List<Tuple<string, string, int, DoseValue, double>>, StringBuilder) GetPrescriptions(List<Tuple<string, double, string>> targets, string initDosePerFxText, string initNumFxText, string initRxText, string boostDosePerFxText = "", string boostNumFxText = "", string boostRxText = "")
        {
            StringBuilder sb = new StringBuilder();
            List<Tuple<string, string, int, DoseValue, double>> prescriptions = new List<Tuple<string, string, int, DoseValue, double>> { };
            double dose_perFx = 0.0;
            int numFractions = 0;
            double boostRxDose = 0.0;
            if(string.IsNullOrEmpty(initRxText) || !double.TryParse(initRxText, out double initRxDose))
            {
                sb.AppendLine("Error! Initial Plan Rx dose is either empty or could not be parsed! Exiting!");
                return (prescriptions, sb);
            }
            if (!string.IsNullOrEmpty(boostRxText) && !double.TryParse(boostRxText, out boostRxDose))
            {
                sb.AppendLine("Error! Boost Plan Rx dose is not empty or could not be parsed! Exiting!");
                return (prescriptions, sb);
            }

            List<Tuple<string, List<Tuple<string, double>>>> orderedList = GetPlanTargetRxDoseList(targets);
            (bool fail, StringBuilder errorMessage) = VerifyRequestedTargetIntegrity(orderedList, initRxDose, boostRxDose);
            if(fail)
            {
                return (prescriptions, errorMessage);
            }

            double priorRxDoses = 0.0;
            double rx;
            foreach (Tuple<string, List<Tuple<string, double>>> itr in orderedList)
            {
                Tuple<string, double> highestRxTgtForPlan = itr.Item2.Last();
                rx = highestRxTgtForPlan.Item2 - priorRxDoses;
                if (rx == initRxDose)
                {
                    if (!double.TryParse(initDosePerFxText, out dose_perFx) || !int.TryParse(initNumFxText, out numFractions))
                    {
                        sb.AppendLine("Error! Could not parse dose per fx or number of fractions for initial plan! Exiting");
                        targets = new List<Tuple<string, double, string>> { };
                        prescriptions = new List<Tuple<string, string, int, DoseValue, double>> { };
                        return (prescriptions, sb);
                    }
                }
                else if (rx == boostRxDose)
                {
                    if (!double.TryParse(boostDosePerFxText, out dose_perFx) || !int.TryParse(boostNumFxText, out numFractions))
                    {
                        sb.AppendLine("Error! Could not parse dose per fx or number of fractions for boost plan! Exiting");
                        targets = new List<Tuple<string, double, string>> { };
                        prescriptions = new List<Tuple<string, string, int, DoseValue, double>> { };
                        return (prescriptions, sb);
                    }
                }
                foreach(Tuple<string,double> itr1 in itr.Item2)
                {
                    prescriptions.Add(Tuple.Create(itr.Item1, itr1.Item1, numFractions, new DoseValue((itr1.Item2 - priorRxDoses) / numFractions, DoseValue.DoseUnit.cGy), itr1.Item2));
                }
                priorRxDoses += rx;
            }
            
            //sort the prescription list by the cumulative rx dose
            prescriptions.Sort(delegate (Tuple<string, string, int, DoseValue, double> x, Tuple<string, string, int, DoseValue, double> y) { return x.Item5.CompareTo(y.Item5); });

            string msg = "Targets set successfully!" + Environment.NewLine + Environment.NewLine;
            msg += "Prescriptions:" + Environment.NewLine;
            foreach (Tuple<string, string, int, DoseValue, double> itr in prescriptions) msg += String.Format("{0}, {1}, {2}, {3}, {4}", itr.Item1, itr.Item2, itr.Item3, itr.Item4.Dose, itr.Item5) + Environment.NewLine;
            MessageBox.Show(msg);
            return (prescriptions, sb);
        }

        private static (bool, StringBuilder) VerifyRequestedTargetIntegrity(List<Tuple<string, List<Tuple<string, double>>>> planTargetDoseList, double initRxDose, double boostRxDose)
        {
            bool fail = false;
            StringBuilder sb = new StringBuilder();
            if (planTargetDoseList.Count > 2)
            {
                sb.AppendLine("Error! Number of request plans is > 2! Exiting!");
                fail = true;
                return (fail, sb);
            }
            double rx;
            double priorRxDoses = 0.0;
            foreach (Tuple<string, List<Tuple<string, double>>> itr in planTargetDoseList)
            {
                Tuple<string, double> highestRxTgtForPlan = itr.Item2.Last();
                rx = highestRxTgtForPlan.Item2 - priorRxDoses;
                priorRxDoses += rx;
                if (rx != initRxDose && rx != boostRxDose)
                {
                    sb.AppendLine($"Error! Highest Rx target ({highestRxTgtForPlan.Item1}, {rx} cGy) for plan: {itr.Item1} does not match either initial ({initRxDose} cGy) or boost ({boostRxDose} cGy) Rx doses! Exiting");
                    fail = true;
                }
            }
            return (fail, sb);
        }

        private static List<Tuple<string,List<Tuple<string,double>>>> GetPlanTargetRxDoseList(List<Tuple<string, double, string>> targets)
        {
            List<Tuple<string, List<Tuple<string, double>>>> theList = new List<Tuple<string, List<Tuple<string, double>>>> { };
            List<Tuple<string, double, string>> tmpList = targets.OrderBy(x => x.Item2).ToList();
            List<Tuple<string, double>> tgtListTmp = new List<Tuple<string, double>> { };
            string tmpPlanId = tmpList.First().Item3;
            foreach(Tuple<string, double, string> itr in tmpList)
            {
                if(!string.Equals(itr.Item3, tmpPlanId))
                {
                    theList.Add(Tuple.Create(tmpPlanId, new List<Tuple<string, double>>(tgtListTmp)));
                    tmpPlanId = itr.Item3;
                    tgtListTmp = new List<Tuple<string, double>> { };
                }
                tgtListTmp.Add(Tuple.Create(itr.Item1, itr.Item2));
            }
            theList.Add(Tuple.Create(tmpPlanId, new List<Tuple<string, double>>(tgtListTmp)));
            return theList;
        }

        public static (bool, Structure, double, StringBuilder) GetLongestTargetInPlan(Tuple<string, List<string>> targetListForAllPlans, StructureSet selectedSS)
        {
            double maxTargetLength = 0.0;
            Structure longestTargetInPlan = null;
            bool fail = false;
            StringBuilder sb = new StringBuilder();
            if(targetListForAllPlans != default)
            {
                foreach (string itr in targetListForAllPlans.Item2)
                {
                    Structure targStruct = StructureTuningHelper.GetStructureFromId(itr, selectedSS);
                    if (targStruct == null || targStruct.IsEmpty)
                    {
                        sb.AppendLine($"Error! No structure named: {itr} found or contoured!");
                        fail = true;
                        return (fail, longestTargetInPlan, maxTargetLength, sb);
                    }
                    Point3DCollection pts = targStruct.MeshGeometry.Positions;
                    double diff = pts.Max(p => p.Z) - pts.Min(p => p.Z);
                    if (diff > maxTargetLength)
                    {
                        longestTargetInPlan = targStruct;
                        maxTargetLength = diff;
                    }
                }
            }
            return (fail, longestTargetInPlan, maxTargetLength, sb);
        }

        //plan id, list of targets for that plan
        public static List<Tuple<string,List<string>>> GetTargetListForEachPlan(List<Tuple<string, string, int, DoseValue, double>> prescriptions)
        {
            List<Tuple<string, List<string>>> planIdTargets = new List<Tuple<string, List<string>>> { };
            string tmpPlanId = prescriptions.First().Item1;
            List<string> targs = new List<string> { };
            foreach (Tuple<string, string, int, DoseValue, double> itr in prescriptions)
            {
                if (itr.Item1 != tmpPlanId)
                {
                    planIdTargets.Add(new Tuple<string, List<string>>(tmpPlanId, new List<string>(targs)));
                    tmpPlanId = itr.Item1;
                    targs = new List<string> { itr.Item2 };
                }
                else targs.Add(itr.Item2);
            }
            planIdTargets.Add(new Tuple<string, List<string>>(tmpPlanId, new List<string>(targs)));
            return planIdTargets;
        }

        public static List<string> GetTargetIdListForPlan(List<Tuple<string, string, int, DoseValue, double>> prescriptions, string planId)
        {
            List<string> targetIds = new List<string> { };
            List<Tuple<string, List<string>>> planIdTargets = GetTargetListForEachPlan(prescriptions);
            if(planIdTargets.Any(x => string.Equals(x.Item1.ToLower(),planId.ToLower())))
            {
                targetIds = planIdTargets.First(x => string.Equals(x.Item1.ToLower(), planId.ToLower())).Item2;
            }
            return targetIds;
        }

        //planId, targetId
        public static List<Tuple<string, string>> GetPlanTargetList(List<Tuple<string, string, int, DoseValue, double>> prescriptions)
        {
            List<Tuple<string, string>> plansTargets = new List<Tuple<string, string>> { };
            if (!prescriptions.Any()) return plansTargets;
            //sort by cumulative dose to targets
            List<Tuple<string, string, int, DoseValue, double>> tmpList = prescriptions.OrderBy(x => x.Item5).ToList();
            string tmpPlan = tmpList.First().Item1;
            string tmpTarget = tmpList.First().Item2;

            foreach (Tuple<string, string, int, DoseValue, double> itr in tmpList)
            {
                //check if this is the start of a new plan, if so, the the previous target was the highest dose target in the previous plan
                if (!string.Equals(itr.Item1, tmpPlan))
                {
                    plansTargets.Add(Tuple.Create<string, string>(tmpPlan, tmpTarget));
                    tmpPlan = itr.Item1;
                }
                tmpTarget = itr.Item2;
            }
            plansTargets.Add(Tuple.Create<string, string>(tmpPlan, tmpTarget));
            return plansTargets;
        }

        //planId, targetId (overloaded method to accept target list rather than prescription list)
        public static List<Tuple<string, string>> GetPlanTargetList(List<Tuple<string, double, string>> targetList)
        {
            //for this list, item1 is the target, item 2 is the cumulated dose (cGy), and item 3 is the plan
            List<Tuple<string, string>> plansTargets = new List<Tuple<string, string>> { };
            if (!targetList.Any()) return plansTargets;
            //sort by cumulative dose to targets
            List<Tuple<string, double, string>> tmpList = targetList.OrderBy(x => x.Item2).ToList();
            string tmpTarget = tmpList.First().Item1;
            string tmpPlan = tmpList.First().Item3;

            foreach (Tuple<string, double, string> itr in tmpList)
            {
                //check if this is the start of a new plan, if so, the the previous target was the highest dose target in the previous plan
                if (!string.Equals(itr.Item3, tmpPlan))
                {
                    plansTargets.Add(Tuple.Create<string, string>(tmpPlan, tmpTarget));
                    tmpPlan = itr.Item3;
                }
                tmpTarget = itr.Item1;
            }
            plansTargets.Add(Tuple.Create<string, string>(tmpPlan, tmpTarget));
            return plansTargets;
        }

        //plan Rx dose
        public static double GetHighestRxForPlan(List<Tuple<string, string, int, DoseValue, double>> prescriptions, string plandId)
        {
            double dose = 0.0;
            List<Tuple<string, string, int, DoseValue, double>> tmpList = prescriptions.OrderBy(x => x.Item5).ToList();
            if (tmpList.Any(x => string.Equals(x.Item1.ToLower(), plandId.ToLower())))
            {
                Tuple<string, string, int, DoseValue, double> rx = prescriptions.Last(x => string.Equals(x.Item1.ToLower(), plandId.ToLower()));
                dose = rx.Item3 * rx.Item4.Dose;
            }
            return dose;
        }

        //target Id with highest Rx for plan
        public static string GetHighestRxTargetIdForPlan(List<Tuple<string, string, int, DoseValue, double>> prescriptions, string plandId)
        {
            string targetId = "";
            List<Tuple<string, string, int, DoseValue, double>> tmpList = prescriptions.OrderBy(x => x.Item5).ToList();
            if (tmpList.Any(x => string.Equals(x.Item1.ToLower(), plandId.ToLower())))
            {
                Tuple<string, string, int, DoseValue, double> rx = prescriptions.Last(x => string.Equals(x.Item1.ToLower(), plandId.ToLower()));
                targetId = rx.Item2;
            }
            return targetId;
        }

        //target id, target prescription dose
        public static (string, double) GetAppropriateTargetIdForRing(List<Tuple<string, string, int, DoseValue, double>> prescriptions, double ringDose)
        {
            string targetId = "";
            double targetRx = 0.0;
            List<Tuple<string, double>> sortedTargets = GetSortedTargetIdsByRxDose(prescriptions);
            if (sortedTargets.Any(x => x.Item2 > ringDose))
            {
                Tuple<string, double> tmp = sortedTargets.First(y => y.Item2 > ringDose);
                targetId = tmp.Item1;
                targetRx = tmp.Item2;
            }
            return (targetId, targetRx);
        }

        //targetId, cumulative Rx dose
        public static List<Tuple<string, double>> GetSortedTargetIdsByRxDose(List<Tuple<string, string, int, DoseValue, double>> prescriptions)
        {
            List<Tuple<string, double>> sortedTargets = new List<Tuple<string, double>> { };
            if (!prescriptions.Any()) return sortedTargets;
            //sort by cumulative dose to targets
            List<Tuple<string, string, int, DoseValue, double>> tmpList = prescriptions.OrderBy(x => x.Item5).ToList();

            foreach (Tuple<string, string, int, DoseValue, double> itr in tmpList)
            {
                sortedTargets.Add(Tuple.Create(itr.Item2, itr.Item5));
            }
            return sortedTargets;
        }

        //list of target IDs
        public static List<string> GetAllTargetIds(List<Tuple<string, string, int, DoseValue, double>> prescriptions)
        {
            return prescriptions.Select(x => x.Item2).ToList();
        }

        //target structure
        public static Structure GetTargetStructureForPlanType(StructureSet ss, string targetId, bool useFlash, PlanType type)
        {
            Structure target;
            if (string.IsNullOrEmpty(targetId))
            {
                //case where no targetId is supplied --> use default target for all plans
                if(type == PlanType.VMAT_TBI)
                {
                    //flash should only be present for vmat tbi plans
                    if (useFlash) target = StructureTuningHelper.GetStructureFromId("ts_ptv_flash", ss); 
                    else target = StructureTuningHelper.GetStructureFromId("ts_ptv_vmat", ss); 
                }
                else target = StructureTuningHelper.GetStructureFromId("ts_ptv_csi", ss);
            }
            else
            {
                target = StructureTuningHelper.GetStructureFromId(targetId, ss);
            }
            return target;
        }

        //plan id
        public static string GetPlanIdFromTargetId(string targetId, List<Tuple<string, string, int, DoseValue, double>> prescriptions)
        {
            string planId = "";
            if(prescriptions.Any(x => string.Equals(x.Item2,targetId)))
            {
                planId = prescriptions.First(x => string.Equals(x.Item2, targetId)).Item1;
            }
            return planId;
        }
    }
}