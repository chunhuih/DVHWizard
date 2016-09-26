using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace Tuning
{
    class Tuning
    {
        // According to ESAPI Reference Guide, ESAPI must only be accessed from a single thread.
        [STAThread]
        static void Main(string[] args)
        {
            Execute(args);
        }

        // Here is the working code.
        static void Execute(string[] args)
        {
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
                    Console.WriteLine("Total number of patients: " + planIds.Count);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
            }

            // Now we have obtained a list of plans to analyze. We go ahead to analyze one by one.
            var username = "chunhui";
            var password = "abc123";
            Application app = Application.CreateApplication(username, password);
            foreach (PlanIdentification planInList in planIds)
            {
                try
                {
                    Patient patient = app.OpenPatientById(planInList.patientId);
                    DVHAnalysis(patient, planInList.planId);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.ToString());
                }
            }
            Console.WriteLine("\n\nPress enter to exit...\n");
            Console.ReadLine();
        }

        // Define a method to perform DVH analysis.
        // In principle, a plan for a patient is identified by the course ID and plan ID. However, for bevity here,
        // I am just using the plan ID, assuming that the plan ID occurs only once in the list of courses for the patient.
        // But Eclipse actually allows the same plan ID to occur in different courses.
        static void DVHAnalysis(Patient patient, string planId)
        {
            Console.WriteLine("Analyzing patient: " + patient.Id + "\t" + patient.LastName + ", " + patient.FirstName);

            PlanSetup planToUse = null;
            PlanSum planSumToUse = null;
            PlanningItem selectedPlan = null;
            bool isPlanSum = false;
            foreach (Course course in patient.Courses)
            {
                foreach (PlanSetup plan in course.PlanSetups)
                {
                    if (plan.Id == planId)
                    {
                        Console.WriteLine(course.Id + "....\t" + plan.Id);
                        planToUse = plan;
                        selectedPlan = (PlanningItem)plan;
                    }
                }
                foreach (PlanSum planSum in course.PlanSums)
                {
                    if (planSum.Id == planId)
                    {
                        Console.WriteLine(course.Id + "...\t" + planSum.Id);
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
            Console.WriteLine("Plan ID: " + selectedPlan.Id);

            // Now let's obtain the structure set, and then find the esophagus in the structure set.
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

//            DVHData dvhAbsolute = selectedPlan.GetDVHCumulativeData(structureEsophagus, DoseValuePresentation.Absolute, VolumePresentation.AbsoluteCm3, 0.001);
//            DVHData dvhRelative = selectedPlan.GetDVHCumulativeData(structureEsophagus, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.001);
            // First check if calculated dose exists.
            double doseMax = 2000;
            
            // Now export DVH data to a text file.
            using (StreamWriter writer = new StreamWriter(patient.Id + "_DVH.txt"))
            {
                // Iterate through the dose range.
                for (int i = 0; i <= doseMax; i++)
                {
                    double volumeAbsolute, volumeRelative;
                    DoseValue doseValue = new DoseValue(i, DoseValue.DoseUnit.cGy);

                    if (isPlanSum == false)
                    {
                        volumeRelative = planToUse.GetVolumeAtDose(structureEsophagus, doseValue, VolumePresentation.Relative);
                        volumeAbsolute = planToUse.GetVolumeAtDose(structureEsophagus, doseValue, VolumePresentation.AbsoluteCm3);
                        writer.Write("{0:0.00}\t{1:0.00}\t{2:0.00}\n", doseValue.Dose, volumeAbsolute, volumeRelative);
                    }
                }
                writer.Close();
            }
            return;
        }

        public static double FindVolumeAtDose(DVHData dvhData, Structure structure, DoseValue doseValue)
        {
            DVHPoint[] hist = dvhData.CurveData;
            int index = (int)(hist.Length * doseValue.Dose / dvhData.MaxDose.Dose);
            return hist[index].Volume;
        }
    }

    // This identifies a plan in the database. It can be a single plan, or a plan sum.
    public struct PlanIdentification
    {
        public string patientId;
        public string courseId;
        public string planId;
        public PlanIdentification(string a, string b, string c)
        {
            patientId = a;
            courseId = b;
            planId = c;
        }
    }
}

