using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace ESAPITest
{
    class Test
    {
        // According to ESAPI Reference Guide, ESAPI must only be accessed from a single thread.
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                var username = "chunhui";
                var password = "abc123";
                using (Application app = Application.CreateApplication(username, password))
                {
                    Execute(app);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
            }
        }

        // Here is the working code.
        static void Execute(Application app)
        {
            string message = "Current user is: " + app.CurrentUser.Id + "\n\n" +
                "The number of patients in the database is " +
                app.PatientSummaries.Count() + "\n\n";
            Console.WriteLine(message);

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // This piece of code just iterates through all the patients in the database, and print out basic information.
            // To save resources, it stops at 100 patients.
            int patientCount = 0;
            foreach (PatientSummary patientSummary in app.PatientSummaries)
            {
                const int maxNumOfPatients = 100;
                patientCount++;
                if (patientCount >= maxNumOfPatients)
                    break;
                Console.WriteLine(patientSummary.Id + "\t Patient name: " + patientSummary.LastName + ", " + patientSummary.FirstName);

                Patient patient = app.OpenPatient(patientSummary);
                foreach (Course course in patient.Courses)
                {
                    Console.WriteLine(course.Id);
                    foreach (PlanSetup planSetup in course.PlanSetups)
                        Console.WriteLine(planSetup.Id);
                    foreach (PlanSum planSum in course.PlanSums)
                        Console.WriteLine(planSum.Id);
                }
                app.ClosePatient();
            }
        }
    }
}
