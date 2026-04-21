namespace UniManage.Models
{
    public class Assignment
    {
        public int AssignmentId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime Deadline { get; set; }
        public int CourseId { get; set; }
        public Course Course { get; set; }
        public List<Submission> Submissions { get; set; }

        // NEW: stores the file path if lecturer uploads a PDF
        public string AssignmentFilePath { get; set; } = string.Empty;
    }
}