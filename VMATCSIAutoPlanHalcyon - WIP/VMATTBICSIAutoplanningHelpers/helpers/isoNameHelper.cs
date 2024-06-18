using System.Collections.Generic;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class IsoNameHelper
    {
        /// <summary>
        /// Simple method to specify the isocenter names for VMAT CSI
        /// </summary>
        /// <param name="numVMATIsos"></param>
        /// <returns></returns>
        public static List<string> GetCSIIsoNames(int numVMATIsos)
        {
            List<string> isoNames = new List<string> { };
            isoNames.Add("Brain");
            if(numVMATIsos > 1)
            {
                //for( int x = 1; x <= numVMATIsos; x++ )
               // {
                //    string SpineName = "Spine" + x.ToString();
               //     isoNames.Add(SpineName);
                //}
                if (numVMATIsos > 2) isoNames.Add("UpSpine");
                if (numVMATIsos >3) isoNames.Add("MidSpine");
                isoNames.Add("LowSpine");
            }
            return isoNames;
        }

        /// <summary>
        /// Helper method to specify the VMAT isocenter names based on the supplied number of vmat isos and total number of isos
        /// </summary>
        /// <param name="numVMATIsos"></param>
        /// <param name="numIsos"></param>
        /// <returns></returns>
        public static List<string> GetTBIVMATIsoNames(int numVMATIsos, int numIsos)
        {
            List<string> isoNames = new List<string> { };
            isoNames.Add("Head");
            if (numVMATIsos > 1 || numIsos > 1)
            {
                if (numIsos > numVMATIsos)
                {
                    if (numVMATIsos == 2) isoNames.Add("Pelvis");
                    else
                    {
                        isoNames.Add("Chest");
                        if (numVMATIsos == 3) isoNames.Add("Pelvis");
                        else if (numVMATIsos == 4)
                        {
                            isoNames.Add("Abdomen");
                            isoNames.Add("Pelvis");
                        }
                    }
                }
                else
                {
                    if (numVMATIsos == 2) isoNames.Add("Pelvis");
                    else
                    {
                        isoNames.Add("Chest");
                        if (numVMATIsos == 3) isoNames.Add("Legs");
                        else if (numVMATIsos == 4)
                        {
                            isoNames.Add("Pelvis");
                            isoNames.Add("Legs");
                        }
                    }
                }
            }
            return isoNames;
        }

        /// <summary>
        /// Helper method to specify the AP/PA isocenter names based on the supplied number of vmat isos and total number of isos
        /// </summary>
        /// <param name="numVMATIsos"></param>
        /// <param name="numIsos"></param>
        /// <returns></returns>
        public static List<string> GetTBIAPPAIsoNames(int numVMATIsos, int numIsos)
        {
            List<string> isoNames = new List<string> { };
            isoNames.Add("AP / PA upper legs");
            if (numIsos == numVMATIsos + 2) isoNames.Add("AP / PA lower legs");
            return isoNames;
        }
    }
}
