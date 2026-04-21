using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using UniManage.Data;
using UniManage.Models;
using UniManage.ViewModels;

namespace UniManage.Controllers
{
    [Authorize(Roles = "Admin,Lecturer")]
    public class ReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportController(ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            var courseStats = await _context.Courses
                .Include(c => c.Enrollments)
                .Include(c => c.Lecturer)
                .Select(c => new
                {
                    c.CourseName,
                    EnrollmentCount = c.Enrollments.Count,
                    c.MaxStudents,
                    LecturerName = c.Lecturer.FullName
                }).ToListAsync();

            var studentPerformance = await _context.Submissions
                .Include(s => s.Student)
                .Where(s => s.Grade != null)
                .GroupBy(s => s.Student.FullName)
                .Select(g => new
                {
                    StudentName = g.Key,
                    AverageGrade = g.Average(s => s.Grade)
                }).ToListAsync();

            ViewBag.CourseStats = courseStats;
            ViewBag.StudentPerformance = studentPerformance;
            ViewBag.TotalStudents = (await _userManager.GetUsersInRoleAsync("Student")).Count;
            ViewBag.TotalCourses = await _context.Courses.CountAsync();
            ViewBag.TotalSubmissions = await _context.Submissions.CountAsync();
            ViewBag.GradedSubmissions = await _context.Submissions.CountAsync(s => s.Grade != null);

            return View();
        }

        // ─── Course Popularity Report ───
        public async Task<IActionResult> CoursePopularityReport()
        {
            var data = await _context.Courses
                .Include(c => c.Enrollments)
                .Include(c => c.Lecturer)
                .Select(c => new CoursePopularityReport
                {
                    CourseName = c.CourseName,
                    LecturerName = c.Lecturer.FullName,
                    EnrollmentCount = c.Enrollments.Count,
                    MaxStudents = c.MaxStudents,
                    FillPercentage = c.MaxStudents > 0
                        ? (int)((double)c.Enrollments.Count / c.MaxStudents * 100) : 0
                })
                .OrderByDescending(c => c.EnrollmentCount)
                .ToListAsync();

            return View(data);
        }

        public async Task<IActionResult> DownloadCoursePopularity()
        {
            var data = await _context.Courses
                .Include(c => c.Enrollments)
                .Include(c => c.Lecturer)
                .Select(c => new CoursePopularityReport
                {
                    CourseName = c.CourseName,
                    LecturerName = c.Lecturer.FullName,
                    EnrollmentCount = c.Enrollments.Count,
                    MaxStudents = c.MaxStudents,
                    FillPercentage = c.MaxStudents > 0
                        ? (int)((double)c.Enrollments.Count / c.MaxStudents * 100) : 0
                })
                .OrderByDescending(c => c.EnrollmentCount)
                .ToListAsync();

            return new ViewAsPdf("CoursePopularityReport", data)
            {
                FileName = $"CoursePopularity_{DateTime.Now:yyyyMMdd}.pdf",
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                PageSize = Rotativa.AspNetCore.Options.Size.A4
            };
        }

        // ─── Student Performance Report ───
        public async Task<IActionResult> StudentPerformanceReport()
        {
            var students = await _userManager.GetUsersInRoleAsync("Student");
            var data = new List<StudentPerformanceReport>();

            foreach (var student in students)
            {
                var submissions = await _context.Submissions
                    .Where(s => s.StudentId == student.Id)
                    .ToListAsync();

                var graded = submissions.Where(s => s.Grade != null).ToList();
                double avg = graded.Any() ? graded.Average(s => s.Grade.Value) : 0;

                data.Add(new StudentPerformanceReport
                {
                    StudentName = student.FullName,
                    Email = student.Email,
                    TotalSubmissions = submissions.Count,
                    GradedSubmissions = graded.Count,
                    AverageGrade = avg,
                    Performance = avg >= 70 ? "Good" : avg >= 50 ? "Average" : "Needs Improvement"
                });
            }

            return View(data.OrderByDescending(s => s.AverageGrade).ToList());
        }

        public async Task<IActionResult> DownloadStudentPerformance()
        {
            var students = await _userManager.GetUsersInRoleAsync("Student");
            var data = new List<StudentPerformanceReport>();

            foreach (var student in students)
            {
                var submissions = await _context.Submissions
                    .Where(s => s.StudentId == student.Id)
                    .ToListAsync();

                var graded = submissions.Where(s => s.Grade != null).ToList();
                double avg = graded.Any() ? graded.Average(s => s.Grade.Value) : 0;

                data.Add(new StudentPerformanceReport
                {
                    StudentName = student.FullName,
                    Email = student.Email,
                    TotalSubmissions = submissions.Count,
                    GradedSubmissions = graded.Count,
                    AverageGrade = avg,
                    Performance = avg >= 70 ? "Good" : avg >= 50 ? "Average" : "Needs Improvement"
                });
            }

            return new ViewAsPdf("StudentPerformanceReport",
                data.OrderByDescending(s => s.AverageGrade).ToList())
            {
                FileName = $"StudentPerformance_{DateTime.Now:yyyyMMdd}.pdf",
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                PageSize = Rotativa.AspNetCore.Options.Size.A4
            };
        }

        // ─── Workload Analysis Report ───
        public async Task<IActionResult> WorkloadReport()
        {
            var lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");
            var data = new List<WorkloadReport>();

            foreach (var lecturer in lecturers)
            {
                var courses = await _context.Courses
                    .Include(c => c.Enrollments)
                    .Where(c => c.LecturerId == lecturer.Id)
                    .ToListAsync();

                var assignments = await _context.Assignments
                    .Where(a => a.Course.LecturerId == lecturer.Id)
                    .ToListAsync();

                var pendingGrading = await _context.Submissions
                    .CountAsync(s => s.Assignment.Course.LecturerId == lecturer.Id
                        && s.Grade == null);

                data.Add(new WorkloadReport
                {
                    LecturerName = lecturer.FullName,
                    Email = lecturer.Email,
                    CoursesCount = courses.Count,
                    AssignmentsCount = assignments.Count,
                    TotalStudents = courses.Sum(c => c.Enrollments.Count),
                    PendingGrading = pendingGrading
                });
            }

            return View(data.OrderByDescending(w => w.TotalStudents).ToList());
        }

        public async Task<IActionResult> DownloadWorkload()
        {
            var lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");
            var data = new List<WorkloadReport>();

            foreach (var lecturer in lecturers)
            {
                var courses = await _context.Courses
                    .Include(c => c.Enrollments)
                    .Where(c => c.LecturerId == lecturer.Id)
                    .ToListAsync();

                var assignments = await _context.Assignments
                    .Where(a => a.Course.LecturerId == lecturer.Id)
                    .ToListAsync();

                var pendingGrading = await _context.Submissions
                    .CountAsync(s => s.Assignment.Course.LecturerId == lecturer.Id
                        && s.Grade == null);

                data.Add(new WorkloadReport
                {
                    LecturerName = lecturer.FullName,
                    Email = lecturer.Email,
                    CoursesCount = courses.Count,
                    AssignmentsCount = assignments.Count,
                    TotalStudents = courses.Sum(c => c.Enrollments.Count),
                    PendingGrading = pendingGrading
                });
            }

            return new ViewAsPdf("WorkloadReport",
                data.OrderByDescending(w => w.TotalStudents).ToList())
            {
                FileName = $"WorkloadAnalysis_{DateTime.Now:yyyyMMdd}.pdf",
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                PageSize = Rotativa.AspNetCore.Options.Size.A4
            };
        }
    }
}