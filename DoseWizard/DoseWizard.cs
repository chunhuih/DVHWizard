using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using Common;

/// <summary>
/// This code is the main program for the esophageal toxicity study.
/// It extracts a number of dosimetric parameters from the RT treatment plan.
/// </summary>
namespace DoseWizard
{
    class DoseWizard
    {
        static void Main(string[] args)
        {
            Execute(args);
        }

        /// <summary>
        /// Here is the working code.
        /// </summary>
        /// <param name="args"></param>
        static void Execute(string[] args)
        {
            string message = "\n\nBeginning of DoseWizard app.\n\n";
            Console.WriteLine(message);

            // First read in the patient ID's in the study from a text file. This part is basically dirty plumbing work.
            string filename = "plan list.txt";
            List<PlanIdentification> planIds = new List<PlanIdentification>();    // This contains the list of plans to analyze.
            try
            {
                using (StreamReader sr = new StreamReader(filename))
                {
                    PlanIdentification planFound = new PlanIdentification();
                    string line;
                    line = sr.ReadLine();
                    // The first line is the header.
                    if (line == null || line.Split()[0] != "MRN")
                    {
                        Console.WriteLine("Input file invalid!!");
                        Environment.Exit(1);
                    }
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.Split().Length != 2)
                        {
                            Console.WriteLine("Invalid input: " + line + "\n\n");
                            continue;
                        }
                        planFound.patientId = line.Split()[0];
                        planFound.planId = line.Split()[1];
                        planIds.Add(planFound);
                        // Console.WriteLine("Patient #" + planIds.Count + "    \t" + line + "\t....\t" + line.Split()[0] + "\t Plan ID: " + line.Split()[1]);
                    }
                    Console.WriteLine("Total number of patients found: " + planIds.Count + "\n");
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
            }

            // Now we have obtained a list of plans to analyze. We go ahead to analyze one by one.
            var username = "chunhui";
            var password = "abc123";
            try
            {
                foreach (PlanIdentification planInList in planIds)
                {
                    using (Application app = Application.CreateApplication(username, password))
                    {
                        Patient patient = app.OpenPatientById(planInList.patientId);
                        PerformDoseWizardry(patient, planInList);
                    }
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
            }
            Console.WriteLine("\n\nPress enter to exit...\n");
            Console.ReadLine();
        }

        //
        // Here we perform a number of dosimetric analysis for each given plan
        //
        public static void PerformDoseWizardry(Patient patient, PlanIdentification planIdentity)
        {
            Console.WriteLine("\n\nAnalyzing patient: " + patient.Id + "\t" + patient.LastName + ", " + patient.FirstName);

            // First find the plan in the database
            PlanSetup planToUse = null;
            PlanSum planSumToUse = null;
            PlanningItem selectedPlan = null;
            bool isPlanSum = false;
            foreach (Course course in patient.Courses)
            {
                foreach (PlanSetup plan in course.PlanSetups)
                {
                    if (plan.Id == planIdentity.planId)
                    {
                        Console.WriteLine(course.Id + " ->\t" + plan.Id + "\n");
                        planToUse = plan;
                        selectedPlan = (PlanningItem)plan;
                    }
                }
                foreach (PlanSum planSum in course.PlanSums)
                {
                    if (planSum.Id == planIdentity.planId)
                    {
                        Console.WriteLine(course.Id + " ->\t" + planSum.Id + "     (Plan Sum.)\n");
                        planSumToUse = planSum;
                        selectedPlan = (PlanningItem)planSum;
                        isPlanSum = true;
                    }
                }
            }
            if (selectedPlan == null)
            {
                Console.WriteLine("Plan not found!\n");
                return;
            }

            // Then find the esophagus in the structure set.
            StructureSet ss = isPlanSum == false ? planToUse.StructureSet : planSumToUse.PlanSetups.First().StructureSet;
            var listStrctures = ss.Structures;
            Structure structureEsophagus = null;
            foreach (Structure structureInList in listStrctures)
            {
                if (structureInList.Id == "Esophagus" || structureInList.Id == "esophagus")
                {
                    structureEsophagus = structureInList;
                }
            }
            if (structureEsophagus == null)
            {
                Console.WriteLine("Structure Esophagus not found.\n");
                return;
            }

            WriteBasicDosimetricData();
            
            /// Now obtain the planning CT image.
            Image imageForPlan = planToUse.StructureSet.Image;


            // We have all the references. Now analyze data.
            double xres = 2.5;
            double yres = 2.5;
            double zres = 3.0;  // Here we standardize the image resolutions.

            Dose doseForPlan = selectedPlan.Dose;
            DoseValue.DoseUnit doseUnit = doseForPlan.DoseMax3D.Unit;

            int xcount = (int)((doseForPlan.XRes * doseForPlan.XSize) / xres);
            System.Collections.BitArray segmentStride = new System.Collections.BitArray(xcount);
            double[] doseArray = new double[xcount];

            selectedPlan.DoseValuePresentation = DoseValuePresentation.Absolute;

            int sliceCount = 0;
            bool flag = false;

            // dosimetric parameters for the esophagus
            double minDosePerSlice = -1.0, maxDosePerSlice = 0.0;

            // iterate through the longitudinal plane slice by slice
            for (int z = 0; z < doseForPlan.ZSize * doseForPlan.ZRes; z++)
            {
                flag = false;
                minDosePerSlice = -1.0;
                maxDosePerSlice = 0.0;
                for (double y = 0; y < doseForPlan.YSize * doseForPlan.YRes; y += yres)
                {
                    int x1 = -1, x2 = -1;
                    VVector start = doseForPlan.Origin + doseForPlan.YDirection * y + doseForPlan.ZDirection * z;
                    VVector end = start + doseForPlan.XDirection * doseForPlan.XRes * doseForPlan.XSize;
                    SegmentProfile segmentProfile = structureEsophagus.GetSegmentProfile(start, end, segmentStride);
                    DoseProfile doseProfile = null;
                    if (doseProfile == null)
                    {
                        doseProfile = doseForPlan.GetDoseProfile(start, end, doseArray);
                    }

                    for (int i = 0; i < segmentProfile.Count; i++)
                    {
                        if (segmentStride[i] && x1 < 0)
                            x1 = i;
                        if (segmentStride[i] && x2 < i)
                            x2 = i;
                        if (segmentStride[i])
                            flag = true;
                    }
                    if (x1 >= 0 && x2 >= 0)
                    {
                        double dose1 = doseProfile[x1].Value;
                        double dose2 = doseProfile[x2].Value;
                        if (minDosePerSlice > dose1 || minDosePerSlice < 0) minDosePerSlice = dose1;
                        if (minDosePerSlice > dose2 || minDosePerSlice < 0) minDosePerSlice = dose2;
                        if (maxDosePerSlice < dose1) maxDosePerSlice = dose1;
                        if (maxDosePerSlice < dose2) maxDosePerSlice = dose2;
                    }
                }
                if (flag) sliceCount++;
            }
            return;
        }

        private void WriteBasicDosimetricData(Patient patient, PlanSetup plan, Structure structure)
        {
            string fileName = "basic dosimetric.txt";
            if (!File.Exists(fileName))
            {
                using (StreamWriter writer = File.CreateText(fileName))
                {
                    for (int doseInGray = 10; doseInGray <= 70; doseInGray += 5)
                    {
                        writer.Write("V{0:0}\t", doseInGray);
                    }
                    for (int doseInGray = 10; doseInGray <= 70; doseInGray += 5)
                    {
                        writer.Write("V{0:0}_r\t", doseInGray);
                    }
                    writer.Write("Mean\tMedian\tMaxDose\tVolume\n");
                }
            }

            using (StreamWriter writer = File.AppendText(fileName))
            {
                for (int doseInGray = 10; doseInGray <= 70; doseInGray += 5)
                {
                    DoseValue doseValue = new DoseValue(doseInGray * 100.0, DoseValue.DoseUnit.cGy);
                    double volumeAbsolute = plan.GetVolumeAtDose(structure, doseValue, VolumePresentation.AbsoluteCm3);
                    writer.Write("{0:0.00}\t", volumeAbsolute);
                }
                for (int doseInGray = 10; doseInGray <= 70; doseInGray += 5)
                {
                    DoseValue doseValue = new DoseValue(doseInGray * 100.0, DoseValue.DoseUnit.cGy);
                    double volumeRelative = plan.GetVolumeAtDose(structure, doseValue, VolumePresentation.Relative);
                    writer.Write("{0:0.00}\t", volumeRelative, volumeRelative);
                }
                DVHData dvhData = plan.GetDVHCumulativeData(structure, DoseValuePresentation.Absolute, VolumePresentation.AbsoluteCm3, 0.001);
                double meanDose = dvhData.MeanDose.Dose;
                double medianDose = dvhData.MedianDose.Dose;
                double maxDose = dvhData.MaxDose.Dose;
                double volume = dvhData.Volume;
                writer.Write("{0:0.00}\t{0:0.00}\t{0:0.00}\t{0:0.00}\n", meanDose, medianDose, maxDose, volume);
            }
        }

        private void WriteBasicDosimetricData(Patient patient, PlanSum plan, Structure structure)
        {
            string fileName = "basic dosimetric.txt";
            DVHData dvhAbsolute = plan.GetDVHCumulativeData(structure, DoseValuePresentation.Absolute, VolumePresentation.AbsoluteCm3, 0.001);
            DVHData dvhRelative = plan.GetDVHCumulativeData(structure, DoseValuePresentation.Absolute, VolumePresentation.AbsoluteCm3, 0.001);
            if (!File.Exists(fileName))
            {
                using (StreamWriter writer = File.CreateText(fileName))
                {
                    for (int doseInGray = 10; doseInGray <= 70; doseInGray += 5)
                    {
                        writer.Write("V{0:0}\t", doseInGray);
                    }
                    for (int doseInGray = 10; doseInGray <= 70; doseInGray += 5)
                    {
                        writer.Write("V{0:0}_r\t", doseInGray);
                    }
                    writer.Write("Mean\tMedian\tMaxDose\tVolume\n");
                }
            }

            using (StreamWriter writer = File.AppendText(fileName))
            {
                for (int doseInGray = 10; doseInGray <= 70; doseInGray += 5)
                {
                    DoseValue doseValue = new DoseValue(doseInGray * 100.0, DoseValue.DoseUnit.cGy);
                    double volumeAbsolute = FindVolumeAtDose(dvhRelative, structure, doseValue);
                    writer.Write("{0:0.00}\t", volumeAbsolute);
                }
                for (int doseInGray = 10; doseInGray <= 70; doseInGray += 5)
                {
                    DoseValue doseValue = new DoseValue(doseInGray * 100.0, DoseValue.DoseUnit.cGy);
                    double volumeRelative = FindVolumeAtDose(dvhAbsolute, structure, doseValue);
                    writer.Write("{0:0.00}\t", volumeRelative);
                }
                double meanDose = dvhAbsolute.MeanDose.Dose;
                double medianDose = dvhAbsolute.MedianDose.Dose;
                double maxDose = dvhAbsolute.MaxDose.Dose;
                double volume = dvhAbsolute.Volume;
                writer.Write("{0:0.00}\t{0:0.00}\t{0:0.00}\t{0:0.00}\n", meanDose, medianDose, maxDose, volume);
            }
        }

        /// <summary>
        /// This method returns the volume in a DVH curve for a given dose.
        /// </summary>
        /// <param name="dvhData"></param>
        /// <param name="structure"></param>
        /// <param name="doseValue"></param>
        /// <returns></returns>
        public static double FindVolumeAtDose(DVHData dvhData, Structure structure, DoseValue doseValue)
        {
            DVHPoint[] hist = dvhData.CurveData;
            int index = (int)(hist.Length * doseValue.Dose / dvhData.MaxDose.Dose);
            return hist[index].Volume;
        }
    }
}
