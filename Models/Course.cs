namespace UniManage.Models
{
    public class Course
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; }
        public string Description { get; set; }
        public int Credits { get; set; }
        public int MaxStudents { get; set; }
        public string LecturerId { get; set; }
        public ApplicationUser Lecturer { get; set; }
        public List<Enrollment> Enrollments { get; set; }
        public List<Assignment> Assignments { get; set; } = new();
    }
}