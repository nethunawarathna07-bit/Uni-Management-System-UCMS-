using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniManage.Data;
using UniManage.Models;

namespace UniManage.Controllers
{
    [Authorize]
    public class CourseController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CourseController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Admin: View all courses
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {
            var courses = await _context.Courses
                .Include(c => c.Lecturer)
                .Include(c => c.Enrollments)
                .ToListAsync();
            return View(courses);
        }

        // Lecturer: View their own courses
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> MyCourses()
        {
            var user = await _userManager.GetUserAsync(User);
            var courses = await _context.Courses
                .Include(c => c.Enrollments)
                .Where(c => c.LecturerId == user.Id)
                .ToListAsync();
            return View(courses);
        }

        // Admin: Create course - GET
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            var lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");
            ViewBag.Lecturers = lecturers;
            return View();
        }

        // Admin: Create course - POST
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Course model)
        {
            if (ModelState.IsValid)
            {
                _context.Courses.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Course created successfully!";
                return RedirectToAction("Index");
            }
            var lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");
            ViewBag.Lecturers = lecturers;
            return View(model);
        }

        // Admin: Edit course - GET
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();
            var lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");
            ViewBag.Lecturers = lecturers;
            return View(course);
        }

        // Admin: Edit course - POST
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Course model)
        {
            if (ModelState.IsValid)
            {
                _context.Courses.Update(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Course updated successfully!";
                return RedirectToAction("Index");
            }
            var lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");
            ViewBag.Lecturers = lecturers;
            return View(model);
        }

        // Admin: Delete course
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();
            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Course deleted successfully!";
            return RedirectToAction("Index");
        }

        // Course details
        public async Task<IActionResult> Details(int id)
        {
            var course = await _context.Courses
                .Include(c => c.Lecturer)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Student)
                .FirstOrDefaultAsync(c => c.CourseId == id);
            if (course == null) return NotFound();
            return View(course);
        }
    }
}