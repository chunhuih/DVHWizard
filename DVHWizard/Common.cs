/// <summary>
/// This file contains methods and definitions that are common to all the projects
/// </summary>
namespace Common
{

    /// <summary>
    /// This structure identifies a plan in the database. It can be a single plan, or a plan sum.
    /// </summary>
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
