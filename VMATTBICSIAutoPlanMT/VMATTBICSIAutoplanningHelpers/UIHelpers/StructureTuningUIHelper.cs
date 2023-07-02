﻿using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using System.Windows;
using System.Windows.Controls;
using System.Text;
using TSManipulationType = VMATTBICSIAutoPlanningHelpers.Enums.TSManipulationType;
using VMATTBICSIAutoPlanningHelpers.Helpers;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers
{
    public static class StructureTuningUIHelper
    {
        public static StackPanel AddTemplateTSHeader(StackPanel theSP)
        {
            StackPanel sp = new StackPanel
            {
                Height = 30,
                Width = theSP.Width,
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(5, 0, 5, 5)
            };

            Label dcmType = new Label
            {
                Content = "DICOM Type",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 115,
                FontSize = 14,
                Margin = new Thickness(10, 0, 0, 0)
            };

            Label strName = new Label
            {
                Content = "Structure Name",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 150,
                FontSize = 14,
                Margin = new Thickness(80, 0, 0, 0)
            };

            sp.Children.Add(dcmType);
            sp.Children.Add(strName);

            return sp;
        }

        public static StackPanel AddTSVolume(StackPanel theSP, StructureSet selectedSS, Tuple<string, string> listItem, string clearBtnPrefix, int clearBtnCounter, RoutedEventHandler clearEvtHndl)
        {
            StackPanel sp = new StackPanel
            {
                Height = 30,
                Width = theSP.Width,
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(5, 0, 5, 5)
            };

            ComboBox type_cb = new ComboBox
            {
                Name = "type_cb",
                Width = 150,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(45, 5, 0, 0),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            string[] types = new string[] { "--select--", 
                                            "AVOIDANCE", 
                                            "CAVITY", 
                                            "CONTRAST_AGENT", 
                                            "CTV", 
                                            "EXTERNAL", 
                                            "GTV", 
                                            "IRRAD_VOLUME",
                                            "ORGAN", 
                                            "PTV", 
                                            "TREATED_VOLUME", 
                                            "SUPPORT", 
                                            "FIXATION",
                                            "CONTROL", 
                                            "DOSE_REGION" };
            
            foreach (string s in types) type_cb.Items.Add(s);
            type_cb.Text = listItem.Item1;
            sp.Children.Add(type_cb);

            ComboBox str_cb = new ComboBox();
            str_cb.Name = "str_cb";
            str_cb.Width = 150;
            str_cb.Height = sp.Height - 5;
            str_cb.HorizontalAlignment = HorizontalAlignment.Left;
            str_cb.VerticalAlignment = VerticalAlignment.Top;
            str_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            str_cb.Margin = new Thickness(50, 5, 0, 0);

            if (!string.Equals(listItem.Item2, "--select--")) str_cb.Items.Add("--select--");
            //this code is used to fix the issue where the structure exists in the structure set, but doesn't populate as the default option in the combo box.
            int index = 0;
            //j is initially 1 because we already added "--select--" to the combo box
            int j = 1;
            foreach (Structure s in selectedSS.Structures)
            {
                str_cb.Items.Add(s.Id);
                if (string.Equals(s.Id.ToLower(),listItem.Item2.ToLower())) index = j;
                j++;
            }
            //if the structure does not exist in the structure set, add the requested structure id to the combobox option and set the selected index to the last item
            if (!selectedSS.Structures.Any(x => string.Equals(x.Id.ToLower(), listItem.Item2.ToLower())))
            {
                str_cb.Items.Add(listItem.Item2);
                str_cb.SelectedIndex = str_cb.Items.Count - 1;
            }
            else
            {
                str_cb.SelectedIndex = index;
            }
            sp.Children.Add(str_cb);

            Button clearStructBtn = new Button
            {
                Name = clearBtnPrefix + clearBtnCounter,
                Content = "Clear",
                Width = 50,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(20, 5, 0, 0)
            };
            clearStructBtn.Click += clearEvtHndl;
            sp.Children.Add(clearStructBtn);

            return sp;
        }

        public static StackPanel GetTSManipulationHeader(StackPanel theSP)
        {
            StackPanel sp = new StackPanel
            {
                Height = 30,
                Width = 450,
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(5)
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition());

            Label strName = new Label
            {
                Content = "Structure Name",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 150,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 0)
            };

            Label spareType = new Label
            {
                Content = "Sparing Type",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 150,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 0)
            };

            Label marginLabel = new Label
            {
                Content = "Margin (cm)",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 140,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 0)
            };
            g.Children.Add(strName);
            g.Children.Add(spareType);
            g.Children.Add(marginLabel);

            Grid.SetColumn(strName, 0);
            Grid.SetColumn(spareType, 1);
            Grid.SetColumn(marginLabel, 2);

            sp.Children.Add(g);

            return sp;
        }

        public static StackPanel AddTSManipulation(StackPanel theSP, List<string> structureIds, Tuple<string, TSManipulationType, double> listItem, string clearBtnPrefix, int clearSpareBtnCounter, SelectionChangedEventHandler typeChngHndl, RoutedEventHandler clearEvtHndl)
        {
            StackPanel sp = new StackPanel
            {
                Height = 30,
                Width = theSP.Width,
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(30, 0, 5, 5)
            };

            ComboBox str_cb = new ComboBox
            {
                Name = "str_cb",
                Width = 150,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(5, 5, 0, 0)
            };

            str_cb.Items.Add("--select--");
            //this code is used to fix the issue where the structure exists in the structure set, but doesn't populate as the default option in the combo box.
            int index = 0;
            //j is initially 1 because we already added "--select--" to the combo box
            int j = 1;
            foreach (string itr in structureIds)
            {
                str_cb.Items.Add(itr);
                if (itr.ToLower() == listItem.Item1.ToLower()) index = j;
                j++;
            }
            str_cb.SelectedIndex = index;
            sp.Children.Add(str_cb);

            ComboBox type_cb = new ComboBox
            {
                Name = "type_cb",
                Width = 150,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(5, 5, 0, 0),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            
            foreach (TSManipulationType s in Enum.GetValues(typeof(TSManipulationType))) type_cb.Items.Add(s);
            if ((int)listItem.Item2 <= type_cb.Items.Count) type_cb.SelectedIndex = (int)listItem.Item2;
            else type_cb.SelectedIndex = 0;
            type_cb.SelectionChanged += typeChngHndl;
            sp.Children.Add(type_cb);

            TextBox addMargin = new TextBox
            {
                Name = "addMargin_tb",
                Width = 110,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 5, 0, 0),
                Text = String.Format("{0:0.0}", listItem.Item3)
            };
            if (listItem.Item2 == TSManipulationType.None) addMargin.Visibility = Visibility.Hidden;
            sp.Children.Add(addMargin);

            Button clearStructBtn = new Button
            {
                Name = clearBtnPrefix + clearSpareBtnCounter,
                Content = "Clear",
                Width = 50,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10, 5, 0, 0)
            };
            clearStructBtn.Click += clearEvtHndl;
            sp.Children.Add(clearStructBtn);

            return sp;
        }

        public static (List<string>, StringBuilder) VerifyTSManipulationIntputIntegrity(List<string> manipulationListIds, List<string> idsPostUnion, StructureSet ss)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Warning! The following structures are null or empty and can't be used for TS manipulation:");
            List<string> missingEmptyList = new List<string> { };
            foreach (string itr in manipulationListIds)
            {
                //check to ensure the structures in the templateSpareList vector are actually present in the selected structure set and are actually contoured. If they are, add them to the defaultList vector, which will be passed 
                //to the add_sp_volumes method
                if (StructureTuningHelper.DoesStructureExistInSS(itr, ss))
                {
                    //already exists in current structure set, check if it is empty
                    if (!StructureTuningHelper.DoesStructureExistInSS(itr, ss, true))
                    {
                        //it's in the structure set, but it's not contoured
                        missingEmptyList.Add(itr);
                        sb.AppendLine(itr);
                    }
                }
                else if (!idsPostUnion.Any(x => string.Equals(x.ToLower(), itr.ToLower())))
                {
                    //check if this structure will be unioned in the generateTS class
                    missingEmptyList.Add(itr);
                    sb.AppendLine(itr);
                }
            }

            return (missingEmptyList, sb);
        }

        public static (List<Tuple<string, TSManipulationType, double>>, StringBuilder) ParseTSManipulationList(StackPanel theSP)
        {
            StringBuilder sb = new StringBuilder();
            List<Tuple<string, TSManipulationType, double>> TSManipulationList = new List<Tuple<string, TSManipulationType, double>> { };
            string structure = "";
            string spareType = "";
            double margin = -1000.0;
            bool firstCombo = true;
            bool headerObj = true;
            foreach (object obj in theSP.Children)
            {
                //skip over the header row
                if (!headerObj)
                {
                    foreach (object obj1 in ((StackPanel)obj).Children)
                    {
                        if (obj1.GetType() == typeof(ComboBox))
                        {
                            //first combo box is the structure and the second is the sparing type
                            if (firstCombo)
                            {
                                structure = (obj1 as ComboBox).SelectedItem.ToString();
                                firstCombo = false;
                            }
                            else spareType = (obj1 as ComboBox).SelectedItem.ToString();
                        }
                        //try to parse the margin value as a double
                        else if (obj1.GetType() == typeof(TextBox)) if (!string.IsNullOrWhiteSpace((obj1 as TextBox).Text)) double.TryParse((obj1 as TextBox).Text, out margin);
                    }
                    if (structure == "--select--" || spareType == "--select--")
                    {
                        sb.AppendLine("Error! \nStructure or Sparing Type not selected! \nSelect an option and try again");
                        return (new List<Tuple<string, TSManipulationType, double>> { }, sb);
                    }
                    //margin will not be assigned from the default value (-1000) if the input is empty, a whitespace, or NaN
                    else if (margin == -1000.0)
                    {
                        sb.AppendLine("Error! \nEntered margin value is invalid! \nEnter a new margin and try again");
                        return (new List<Tuple<string, TSManipulationType, double>> { }, sb);
                    }
                    //only add the current row to the structure sparing list if all the parameters were successful parsed
                    else TSManipulationList.Add(Tuple.Create(structure, TSManipulationTypeHelper.GetTSManipulationType(spareType), margin));
                    firstCombo = true;
                    margin = -1000.0;
                }
                else headerObj = false;
            }

            return (TSManipulationList, sb);
        }

        public static (List<Tuple<string, string>>, StringBuilder) ParseCreateTSStructureList(StackPanel theSP)
        {
            StringBuilder sb = new StringBuilder();
            List<Tuple<string, string>> TSStructureList = new List<Tuple<string, string>> { };
            string dcmType = "";
            string structure = "";
            bool firstCombo = true;
            bool headerObj = true;
            foreach (object obj in theSP.Children)
            {
                //skip over the header row
                if (!headerObj)
                {
                    foreach (object obj1 in ((StackPanel)obj).Children)
                    {
                        if (obj1.GetType() == typeof(ComboBox))
                        {
                            //first combo box is the structure and the second is the sparing type
                            if (firstCombo)
                            {
                                dcmType = (obj1 as ComboBox).SelectedItem.ToString();
                                firstCombo = false;
                            }
                            else structure = (obj1 as ComboBox).SelectedItem.ToString();
                        }
                    }
                    if (dcmType == "--select--" || structure == "--select--")
                    {
                        sb.AppendLine("Error! \nStructure or DICOM Type not selected! \nSelect an option and try again");
                        return (new List<Tuple<string, string>> { }, sb);
                    }
                    //only add the current row to the structure sparing list if all the parameters were successful parsed
                    else TSStructureList.Add(Tuple.Create(dcmType, structure));
                    firstCombo = true;
                }
                else headerObj = false;
            }

            return (TSStructureList, sb);
        }
    }
}
