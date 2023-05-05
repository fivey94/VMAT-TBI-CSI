﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoplanningHelpers.Enums;
using VMATTBICSIAutoplanningHelpers.Helpers;

namespace VMATTBICSIAutoplanningHelpers.UIHelpers
{
    public class OptimizationSetupUIHelper
    {
        public List<Tuple<string, OptimizationObjectiveType, double, double, int>> ReadConstraintsFromPlan(ExternalPlanSetup plan)
        {
            //grab the optimization constraints in the existing VMAT TBI plan and display them to the user
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> defaultList = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
            foreach (OptimizationObjective itr in plan.OptimizationSetup.Objectives)
            {
                //do NOT include any cooler or heater tuning structures in the list
                if (!itr.StructureId.ToLower().Contains("ts_cooler") && !itr.StructureId.ToLower().Contains("ts_heater"))
                {
                    if (itr.GetType() == typeof(OptimizationPointObjective))
                    {
                        OptimizationPointObjective pt = (itr as OptimizationPointObjective);
                        defaultList.Add(Tuple.Create(pt.StructureId, OptimizationTypeHelper.GetObjectiveType(pt), pt.Dose.Dose, pt.Volume, (int)pt.Priority));
                    }
                    else if (itr.GetType() == typeof(OptimizationMeanDoseObjective))
                    {
                        OptimizationMeanDoseObjective mean = (itr as OptimizationMeanDoseObjective);
                        defaultList.Add(Tuple.Create(mean.StructureId, OptimizationObjectiveType.Mean, mean.Dose.Dose, 0.0, (int)mean.Priority));
                    }
                }
            }
            return defaultList;
        }

        public bool RemoveOptimizationConstraintsFromPLan(ExternalPlanSetup plan)
        {
            if (plan.OptimizationSetup.Objectives.Count() > 0)
            {
                foreach (OptimizationObjective o in plan.OptimizationSetup.Objectives) plan.OptimizationSetup.RemoveObjective(o);
            }
            return false;
        }

        public StackPanel AddPlanIdtoOptList(StackPanel theSP, string id)
        {
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theSP.Width;
            sp.Orientation = Orientation.Horizontal;
            sp.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Margin = new Thickness(15, 0, 5, 5);

            Label strName = new Label();
            strName.Content = String.Format("Plan Id: {0}", id);
            strName.HorizontalAlignment = HorizontalAlignment.Center;
            strName.VerticalAlignment = VerticalAlignment.Top;
            strName.Width = theSP.Width;
            strName.FontSize = 14;
            strName.FontWeight = FontWeights.Bold;

            sp.Children.Add(strName);
            return sp;
        }

        public StackPanel GetOptHeader(double theWidth)
        {
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theWidth;
            sp.Orientation = Orientation.Horizontal;
            sp.Margin = new Thickness(30, 0, 5, 5);

            Label strName = new Label();
            strName.Content = "Structure";
            strName.HorizontalAlignment = HorizontalAlignment.Center;
            strName.VerticalAlignment = VerticalAlignment.Top;
            strName.Width = 110;
            strName.FontSize = 14;
            strName.Margin = new Thickness(27, 0, 0, 0);

            Label spareType = new Label();
            spareType.Content = "Constraint";
            spareType.HorizontalAlignment = HorizontalAlignment.Center;
            spareType.VerticalAlignment = VerticalAlignment.Top;
            spareType.Width = 90;
            spareType.FontSize = 14;
            spareType.Margin = new Thickness(2, 0, 0, 0);

            Label volLabel = new Label();
            volLabel.Content = "V (%)";
            volLabel.HorizontalAlignment = HorizontalAlignment.Center;
            volLabel.VerticalAlignment = VerticalAlignment.Top;
            volLabel.Width = 60;
            volLabel.FontSize = 14;
            volLabel.Margin = new Thickness(18, 0, 0, 0);

            Label doseLabel = new Label();
            doseLabel.Content = "D (cGy)";
            doseLabel.HorizontalAlignment = HorizontalAlignment.Center;
            doseLabel.VerticalAlignment = VerticalAlignment.Top;
            doseLabel.Width = 60;
            doseLabel.FontSize = 14;
            doseLabel.Margin = new Thickness(3, 0, 0, 0);

            Label priorityLabel = new Label();
            priorityLabel.Content = "Priority";
            priorityLabel.HorizontalAlignment = HorizontalAlignment.Center;
            priorityLabel.VerticalAlignment = VerticalAlignment.Top;
            priorityLabel.Width = 65;
            priorityLabel.FontSize = 14;
            priorityLabel.Margin = new Thickness(13, 0, 0, 0);

            sp.Children.Add(strName);
            sp.Children.Add(spareType);
            sp.Children.Add(volLabel);
            sp.Children.Add(doseLabel);
            sp.Children.Add(priorityLabel);
            return sp;
        }

        public StackPanel AddOptVolume<T>(StackPanel theSP, StructureSet selectedSS, Tuple<string, OptimizationObjectiveType, double, double, T> listItem, string clearBtnNamePrefix, int clearOptBtnCounter, RoutedEventHandler e, bool addStructureEvenIfNotInSS = false)
        {
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theSP.Width;
            sp.Orientation = Orientation.Horizontal;
            sp.Margin = new Thickness(30, 5, 5, 5);

            ComboBox opt_str_cb = new ComboBox();
            opt_str_cb.Name = "opt_str_cb";
            opt_str_cb.Width = 120;
            opt_str_cb.Height = sp.Height - 5;
            opt_str_cb.HorizontalAlignment = HorizontalAlignment.Left;
            opt_str_cb.VerticalAlignment = VerticalAlignment.Top;
            opt_str_cb.Margin = new Thickness(5, 5, 0, 0);

            opt_str_cb.Items.Add("--select--");
            //this code is used to fix the issue where the structure exists in the structure set, but doesn't populate as the default option in the combo box.
            int index = 0;
            //j is initially 1 because we already added "--select--" to the combo box 
            int j = 1;
            foreach (Structure s in selectedSS.Structures)
            {
                opt_str_cb.Items.Add(s.Id);
                if (s.Id.ToLower() == listItem.Item1.ToLower()) index = j;
                j++;
            }
            if (addStructureEvenIfNotInSS && !selectedSS.Structures.Any(x => string.Equals(x.Id.ToLower(), listItem.Item1.ToLower())))
            {
                opt_str_cb.Items.Add(listItem.Item1);
                opt_str_cb.SelectedIndex = opt_str_cb.Items.Count - 1;
            }
            else opt_str_cb.SelectedIndex = index;
            opt_str_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            sp.Children.Add(opt_str_cb);

            ComboBox constraint_cb = new ComboBox();
            constraint_cb.Name = "type_cb";
            constraint_cb.Width = 100;
            constraint_cb.Height = sp.Height - 5;
            constraint_cb.HorizontalAlignment = HorizontalAlignment.Left;
            constraint_cb.VerticalAlignment = VerticalAlignment.Top;
            constraint_cb.Margin = new Thickness(5, 5, 0, 0);
            string[] types = new string[] { "--select--", "Upper", "Lower", "Mean", "Exact" };
            foreach (string s in types) constraint_cb.Items.Add(s);
            if (listItem.Item2 != OptimizationObjectiveType.None) constraint_cb.Text = listItem.Item2.ToString();
            else constraint_cb.SelectedIndex = 0;
            constraint_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            sp.Children.Add(constraint_cb);

            //the order of the dose and volume values are switched when they are displayed to the user. This way, the optimization objective appears to the user as it would in the optimization workspace.
            //However, due to the way ESAPI assigns optimization objectives via VMATplan.OptimizationSetup.AddPointObjective, they need to be stored in the order listed in the templates above
            TextBox vol_tb = new TextBox();
            vol_tb.Name = "vol_tb";
            vol_tb.Width = 65;
            vol_tb.Height = sp.Height - 5;
            vol_tb.HorizontalAlignment = HorizontalAlignment.Left;
            vol_tb.VerticalAlignment = VerticalAlignment.Top;
            vol_tb.Margin = new Thickness(5, 5, 0, 0);
            vol_tb.Text = String.Format("{0:0.#}", listItem.Item4);
            vol_tb.TextAlignment = TextAlignment.Center;
            sp.Children.Add(vol_tb);

            TextBox dose_tb = new TextBox();
            dose_tb.Name = "dose_tb";
            dose_tb.Width = 70;
            dose_tb.Height = sp.Height - 5;
            dose_tb.HorizontalAlignment = HorizontalAlignment.Left;
            dose_tb.VerticalAlignment = VerticalAlignment.Top;
            dose_tb.Margin = new Thickness(5, 5, 0, 0);
            dose_tb.Text = String.Format("{0:0.#}", listItem.Item3);
            dose_tb.TextAlignment = TextAlignment.Center;
            sp.Children.Add(dose_tb);

            if(listItem.Item5.GetType() == typeof(int))
            {
                TextBox priority_tb = new TextBox();
                priority_tb.Name = "priority_tb";
                priority_tb.Width = 65;
                priority_tb.Height = sp.Height - 5;
                priority_tb.HorizontalAlignment = HorizontalAlignment.Left;
                priority_tb.VerticalAlignment = VerticalAlignment.Top;
                priority_tb.Margin = new Thickness(5, 5, 0, 0);
                priority_tb.Text = Convert.ToString(listItem.Item5);
                priority_tb.TextAlignment = TextAlignment.Center;
                sp.Children.Add(priority_tb);
            }
            else
            {
                TextBox dvPresentation_tb = new TextBox();
                dvPresentation_tb.Name = "dvPresentation_tb";
                dvPresentation_tb.Width = 65;
                dvPresentation_tb.Height = sp.Height - 5;
                dvPresentation_tb.HorizontalAlignment = HorizontalAlignment.Left;
                dvPresentation_tb.VerticalAlignment = VerticalAlignment.Top;
                dvPresentation_tb.Margin = new Thickness(5, 5, 0, 0);
                dvPresentation_tb.Text = Convert.ToString(listItem.Item5) == "Absolute" ? "cGy" : "%";
                dvPresentation_tb.TextAlignment = TextAlignment.Center;
                sp.Children.Add(dvPresentation_tb);
            }

            Button clearOptStructBtn = new Button();
            clearOptStructBtn.Name = clearBtnNamePrefix + clearOptBtnCounter;
            clearOptStructBtn.Content = "Clear";
            clearOptStructBtn.Width = 50;
            clearOptStructBtn.Height = sp.Height - 5;
            clearOptStructBtn.HorizontalAlignment = HorizontalAlignment.Left;
            clearOptStructBtn.VerticalAlignment = VerticalAlignment.Top;
            clearOptStructBtn.Margin = new Thickness(10, 5, 0, 0);
            clearOptStructBtn.Click += e;
            sp.Children.Add(clearOptStructBtn);

            return sp;
        }

        public (List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>, StringBuilder) ParseOptConstraints(StackPanel sp, bool checkInputIntegrity = true)
        {
            StringBuilder sb = new StringBuilder();
            if (sp.Children.Count == 0)
            {
                sb.AppendLine("No optimization parameters present to assign to plans!");
                return (new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>(), sb);
            }

            //get constraints
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> optParametersList = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> optParametersListList = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> { };
            string structure = "";
            string constraintType = "";
            double dose = -1.0;
            double vol = -1.0;
            int priority = -1;
            int txtBxNum = 1;
            bool firstCombo = true;
            //bool headerObj = true;
            int numElementsPerRow = 0;
            string planId = "";
            object copyObj = null;
            foreach (object obj in sp.Children)
            {
                foreach (object obj1 in ((StackPanel)obj).Children)
                {
                    if (obj1.GetType() == typeof(ComboBox))
                    {
                        if (firstCombo)
                        {
                            //first combobox is the structure
                            structure = (obj1 as ComboBox).SelectedItem.ToString();
                            firstCombo = false;
                        }
                        //second combobox is the constraint type
                        else constraintType = (obj1 as ComboBox).SelectedItem.ToString();
                    }
                    else if (obj1.GetType() == typeof(TextBox))
                    {
                        if (!string.IsNullOrWhiteSpace((obj1 as TextBox).Text))
                        {
                            //first text box is the volume percentage
                            if (txtBxNum == 1) double.TryParse((obj1 as TextBox).Text, out vol);
                            //second text box is the dose constraint
                            else if (txtBxNum == 2) double.TryParse((obj1 as TextBox).Text, out dose);
                            //third text box is the priority
                            else int.TryParse((obj1 as TextBox).Text, out priority);
                        }
                        txtBxNum++;
                    }
                    else if (obj1.GetType() == typeof(Label)) copyObj = obj1;
                    numElementsPerRow++;
                }
                if (numElementsPerRow == 1)
                {
                    if (optParametersList.Any())
                    {
                        optParametersListList.Add(new Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>(planId, new List<Tuple<string, OptimizationObjectiveType, double, double, int>>(optParametersList)));
                        optParametersList = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
                    }
                    string planIdHeader = (copyObj as Label).Content.ToString();
                    planId = planIdHeader.Substring(planIdHeader.IndexOf(":") + 2, planIdHeader.Length - planIdHeader.IndexOf(":") - 2);
                }
                else if (numElementsPerRow != 5)
                {
                    //do some checks to ensure the integrity of the data
                    if (checkInputIntegrity && (structure == "--select--" || constraintType == "--select--"))
                    {
                        sb.AppendLine("Error! \nStructure or Sparing Type not selected! \nSelect an option and try again");
                        return (new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>(), sb);
                    }
                    else if (checkInputIntegrity && (dose == -1.0 || vol == -1.0 || priority == -1.0))
                    {
                        sb.AppendLine("Error! \nDose, volume, or priority values are invalid! \nEnter new values and try again");
                        return (new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>(), sb);
                    }
                    //if the row of data passes the above checks, add it the optimization parameter list
                    else optParametersList.Add(Tuple.Create(structure, OptimizationTypeHelper.GetObjectiveType(constraintType), Math.Round(dose, 3, MidpointRounding.AwayFromZero), Math.Round(vol, 3, MidpointRounding.AwayFromZero), priority));
                    //reset the values of the variables used to parse the data
                    firstCombo = true;
                    txtBxNum = 1;
                    dose = -1.0;
                    vol = -1.0;
                    priority = -1;
                }
                numElementsPerRow = 0;
            }
            optParametersListList.Add(new Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>(planId, new List<Tuple<string, OptimizationObjectiveType, double, double, int>>(optParametersList)));
            return (optParametersListList, sb);
        }

        public (bool, StringBuilder) AssignOptConstraints(List<Tuple<string, OptimizationObjectiveType, double, double, int>> parameters, ExternalPlanSetup VMATplan, bool useJawTracking, double NTOpriority)
        {
            bool isError = false;
            StringBuilder sb = new StringBuilder();
            foreach (Tuple<string, OptimizationObjectiveType, double, double, int> opt in parameters)
            {
                //assign the constraints to the plan. I haven't found a use for the exact constraint yet, so I just wrote the script to throw a warning if the exact constraint was selected (that row of data will NOT be
                //assigned to the VMAT plan)
                Structure s = VMATplan.StructureSet.Structures.First(x => x.Id == opt.Item1);
                if (opt.Item2 != OptimizationObjectiveType.Mean) VMATplan.OptimizationSetup.AddPointObjective(s, OptimizationTypeHelper.GetObjectiveOperator(opt.Item2), new DoseValue(opt.Item3, DoseValue.DoseUnit.cGy), opt.Item4, (double)opt.Item5);
                else VMATplan.OptimizationSetup.AddMeanDoseObjective(s, new DoseValue(opt.Item3, DoseValue.DoseUnit.cGy), (double)opt.Item5);
                //else 
                //{ 
                //    sb.AppendLine("Constraint type not recognized!"); 
                //    isError = true;
                //    return (isError, sb);
                //}
            }
            //turn on/turn off jaw tracking
            try { VMATplan.OptimizationSetup.UseJawTracking = useJawTracking; }
            catch (Exception except) 
            { 
                sb.AppendLine(String.Format("Warning! Could not set jaw tracking for VMAT plan because: {0}\nJaw tacking will have to be set manually!", except.Message)); 
            }
            //set auto NTO priority to zero (i.e., shut it off). It has to be done this way because every plan created in ESAPI has an instance of an automatic NTO, which CAN'T be deleted.
            VMATplan.OptimizationSetup.AddAutomaticNormalTissueObjective(NTOpriority);
            return (isError, sb);
        }
    }
}
