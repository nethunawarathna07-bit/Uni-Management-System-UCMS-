using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniManage.Data;
using UniManage.Models;

namespace UniManage.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.UserName = user.FullName;
            ViewBag.Role = user.Role;

            if (User.IsInRole("Admin"))
            {
                ViewBag.TotalCourses = await _context.Courses.CountAsync();
                ViewBag.TotalStudents = (await _userManager.GetUsersInRoleAsync("Student")).Count;
                ViewBag.TotalLecturers = (await _userManager.GetUsersInRoleAsync("Lecturer")).Count;
                ViewBag.TotalEnrollments = await _context.Enrollments.CountAsync();
            }
            else if (User.IsInRole("Student"))
            {
                ViewBag.EnrolledCourses = await _context.Enrollments
                .CountAsync(e => e.StudentId == user.Id);

                // Get all assignment IDs for courses the student is enrolled in
                var enrolledCourseIds = await _context.Enrollments
                    .Where(e => e.StudentId == user.Id)
                    .Select(e => e.CourseId)
                    .ToListAsync();

                var totalAssignments = await _context.Assignments
                    .CountAsync(a => enrolledCourseIds.Contains(a.CourseId));

                // Submitted assignments by this student
                var submittedAssignmentIds = await _context.Submissions
                    .Where(s => s.StudentId == user.Id)
                    .Select(s => s.AssignmentId)
                    .ToListAsync();

                // Pending = assignments in enrolled courses that student hasn't submitted yet
                var pendingAssignments = await _context.Assignments
                    .CountAsync(a => enrolledCourseIds.Contains(a.CourseId)
                        && !submittedAssignmentIds.Contains(a.AssignmentId)
                        && a.Deadline >= DateTime.Now);

                ViewBag.TotalAssignments = totalAssignments;
                ViewBag.PendingAssignments = pendingAssignments;
            }
            else if (User.IsInRole("Lecturer"))
            {
                ViewBag.TeachingCourses = await _context.Courses.CountAsync(c => c.LecturerId == user.Id);
                ViewBag.TotalAssignments = await _context.Assignments
                    .CountAsync(a => a.Course.LecturerId == user.Id);
            }

            return View();
        }
    }
}