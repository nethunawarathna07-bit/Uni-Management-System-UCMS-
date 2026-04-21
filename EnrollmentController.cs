using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniManage.Data;
using UniManage.Models;

namespace UniManage.Controllers
{
    [Authorize(Roles = "Student")]
    public class EnrollmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EnrollmentController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Browse all available courses
        public async Task<IActionResult> BrowseCourses()
        {
            var user = await _userManager.GetUserAsync(User);
            var enrolledCourseIds = await _context.Enrollments
                .Where(e => e.StudentId == user.Id)
                .Select(e => e.CourseId)
                .ToListAsync();

            var courses = await _context.Courses
                .Include(c => c.Lecturer)
                .Include(c => c.Enrollments)
                .ToListAsync();

            ViewBag.EnrolledCourseIds = enrolledCourseIds;
            return View(courses);
        }

        // Enroll in a course
        public async Task<IActionResult> Enroll(int courseId)
        {
            var user = await _userManager.GetUserAsync(User);
            var course = await _context.Courses
                .Include(c => c.Enrollments)
                .FirstOrDefaultAsync(c => c.CourseId == courseId);

            if (course == null)
            {
                TempData["Error"] = "Course not found.";
                return RedirectToAction("BrowseCourses");
            }

            // Check if already enrolled
            var alreadyEnrolled = await _context.Enrollments
                .AnyAsync(e => e.StudentId == user.Id && e.CourseId == courseId);
            if (alreadyEnrolled)
            {
                TempData["Error"] = "You are already enrolled in this course.";
                return RedirectToAction("BrowseCourses");
            }

            // Check enrollment limit
            if (course.Enrollments.Count >= course.MaxStudents)
            {
                TempData["Error"] = "This course is full.";
                return RedirectToAction("BrowseCourses");
            }

            var enrollment = new Enrollment
            {
                StudentId = user.Id,
                CourseId = courseId,
                EnrolledDate = DateTime.Now
            };

            _context.Enrollments.Add(enrollment);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Successfully enrolled!";
            return RedirectToAction("MyCourses");
        }

        // Student's enrolled courses
        public async Task<IActionResult> MyCourses()
        {
            var user = await _userManager.GetUserAsync(User);
            var enrollments = await _context.Enrollments
                .Include(e => e.Course)
                    .ThenInclude(c => c.Lecturer)
                .Where(e => e.StudentId == user.Id)
                .ToListAsync();
            return View(enrollments);
        }

        // Unenroll from a course
        public async Task<IActionResult> Unenroll(int enrollmentId)
        {
            var enrollment = await _context.Enrollments.FindAsync(enrollmentId);
            if (enrollment != null)
            {
                _context.Enrollments.Remove(enrollment);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Successfully unenrolled.";
            }
            return RedirectToAction("MyCourses");
        }
    }
}