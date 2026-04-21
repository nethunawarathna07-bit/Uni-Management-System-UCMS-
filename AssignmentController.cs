using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniManage.Data;
using UniManage.Models;

namespace UniManage.Controllers
{
    [Authorize]
    public class AssignmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public AssignmentController(ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
        }

        // Lecturer: View all assignments
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var assignments = await _context.Assignments
                .Include(a => a.Course)
                .Include(a => a.Submissions)
                .Where(a => a.Course.LecturerId == user.Id)
                .ToListAsync();
            return View(assignments);
        }

        // Lecturer: Create assignment - GET
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            var courses = await _context.Courses
                .Where(c => c.LecturerId == user.Id)
                .ToListAsync();
            ViewBag.Courses = courses;
            return View();
        }

        // Lecturer: Create assignment - POST
        [HttpPost]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Create(Assignment model, IFormFile assignmentFile)
        {
            // Remove navigation property validation errors
            ModelState.Remove("Course");
            ModelState.Remove("Submissions");
            ModelState.Remove("AssignmentFilePath");

            if (ModelState.IsValid)
            {
                // Handle PDF upload if provided
                if (assignmentFile != null && assignmentFile.Length > 0)
                {
                    if (Path.GetExtension(assignmentFile.FileName).ToLower() != ".pdf")
                    {
                        ModelState.AddModelError("", "Only PDF files are allowed.");
                        var user2 = await _userManager.GetUserAsync(User);
                        ViewBag.Courses = await _context.Courses
                            .Where(c => c.LecturerId == user2.Id).ToListAsync();
                        return View(model);
                    }

                    string uploadsFolder = Path.Combine(_environment.WebRootPath,
                        "uploads", "assignments");
                    Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = $"{Guid.NewGuid()}_{assignmentFile.FileName}";
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await assignmentFile.CopyToAsync(stream);
                    }

                    model.AssignmentFilePath = $"/uploads/assignments/{uniqueFileName}";
                }

                _context.Assignments.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Assignment created successfully!";
                return RedirectToAction("Index");
            }

            var user = await _userManager.GetUserAsync(User);
            ViewBag.Courses = await _context.Courses
                .Where(c => c.LecturerId == user.Id).ToListAsync();
            return View(model);
        }

        // Student: View their assignments
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> StudentAssignments()
        {
            var user = await _userManager.GetUserAsync(User);
            var enrolledCourseIds = await _context.Enrollments
                .Where(e => e.StudentId == user.Id)
                .Select(e => e.CourseId)
                .ToListAsync();

            var assignments = await _context.Assignments
                .Include(a => a.Course)
                .Include(a => a.Submissions)
                .Where(a => enrolledCourseIds.Contains(a.CourseId))
                .ToListAsync();

            ViewBag.StudentId = user.Id;
            return View(assignments);
        }

        // Student: Submit assignment - GET
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Submit(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var assignment = await _context.Assignments
                .Include(a => a.Course)
                .Include(a => a.Submissions)
                .FirstOrDefaultAsync(a => a.AssignmentId == id);

            if (assignment == null) return NotFound();

            var existingSubmission = assignment.Submissions
                .FirstOrDefault(s => s.StudentId == user.Id);

            ViewBag.ExistingSubmission = existingSubmission;
            return View(assignment);
        }

        // Student: Submit assignment - POST (PDF upload)
        [HttpPost]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Submit(int assignmentId, IFormFile submissionFile)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Verify the assignment exists
            var assignment = await _context.Assignments
                .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId);
            if (assignment == null)
            {
                TempData["Error"] = "Assignment not found.";
                return RedirectToAction("StudentAssignments");
            }

            // Verify the student is enrolled in the assignment's course
            var isEnrolled = await _context.Enrollments
                .AnyAsync(e => e.StudentId == user.Id
                    && e.CourseId == assignment.CourseId);
            if (!isEnrolled)
            {
                TempData["Error"] = "You are not enrolled in this course.";
                return RedirectToAction("StudentAssignments");
            }

            if (submissionFile == null || submissionFile.Length == 0)
            {
                TempData["Error"] = "Please select a PDF file to upload.";
                return RedirectToAction("Submit", new { id = assignmentId });
            }

            if (Path.GetExtension(submissionFile.FileName).ToLower() != ".pdf")
            {
                TempData["Error"] = "Only PDF files are allowed.";
                return RedirectToAction("Submit", new { id = assignmentId });
            }

            // Save PDF file
            string uploadsFolder = Path.Combine(_environment.WebRootPath,
                "uploads", "submissions");
            Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(submissionFile.FileName)}";
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await submissionFile.CopyToAsync(stream);
            }

            string savedFilePath = $"/uploads/submissions/{uniqueFileName}";

            // Check for existing submission
            var existing = await _context.Submissions
                .FirstOrDefaultAsync(s => s.AssignmentId == assignmentId
                    && s.StudentId == user.Id);

            if (existing != null)
            {
                // Delete old file
                if (!string.IsNullOrEmpty(existing.FilePath))
                {
                    string oldFile = Path.Combine(_environment.WebRootPath,
                        existing.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldFile))
                        System.IO.File.Delete(oldFile);
                }
                existing.FilePath = savedFilePath;
                existing.SubmittedDate = DateTime.Now;
            }
            else
            {
                var submission = new Submission
                {
                    AssignmentId = assignmentId,
                    StudentId = user.Id,
                    FilePath = savedFilePath,
                    SubmittedDate = DateTime.Now,
                    Feedback = string.Empty
                };
                _context.Submissions.Add(submission);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Assignment submitted successfully!";
            return RedirectToAction("StudentAssignments");
        }

        // Lecturer: View submissions for grading
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Grades()
        {
            var user = await _userManager.GetUserAsync(User);
            var submissions = await _context.Submissions
                .Include(s => s.Student)
                .Include(s => s.Assignment)
                    .ThenInclude(a => a.Course)
                .Where(s => s.Assignment.Course.LecturerId == user.Id)
                .ToListAsync();
            return View(submissions);
        }

        // Lecturer: Grade a submission - POST
        [HttpPost]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Grade(int submissionId, int grade, string feedback)
        {
            var submission = await _context.Submissions.FindAsync(submissionId);
            if (submission != null)
            {
                submission.Grade = grade;
                submission.Feedback = feedback;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Grade saved successfully!";
            }
            return RedirectToAction("Grades");
        }

        // Download any file (assignment or submission)
        public IActionResult Download(string filePath, string fileName)
        {
            if (string.IsNullOrEmpty(filePath)) return NotFound();

            string fullPath = Path.Combine(_environment.WebRootPath,
                filePath.TrimStart('/'));

            if (!System.IO.File.Exists(fullPath)) return NotFound();

            var fileBytes = System.IO.File.ReadAllBytes(fullPath);
            return File(fileBytes, "application/pdf",
                string.IsNullOrEmpty(fileName) ? "download.pdf" : fileName);
        }

        // Lecturer: Delete assignment
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Delete(int id)
        {
            var assignment = await _context.Assignments
                .Include(a => a.Submissions)
                .FirstOrDefaultAsync(a => a.AssignmentId == id);

            if (assignment == null) return NotFound();

            // Delete associated submission PDF files
            foreach (var submission in assignment.Submissions)
            {
                if (!string.IsNullOrEmpty(submission.FilePath))
                {
                    string filePath = Path.Combine(_environment.WebRootPath,
                        submission.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                        System.IO.File.Delete(filePath);
                }
            }

            // Delete assignment PDF file if exists
            if (!string.IsNullOrEmpty(assignment.AssignmentFilePath))
            {
                string assignmentFile = Path.Combine(_environment.WebRootPath,
                    assignment.AssignmentFilePath.TrimStart('/'));
                if (System.IO.File.Exists(assignmentFile))
                    System.IO.File.Delete(assignmentFile);
            }

            _context.Assignments.Remove(assignment);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Assignment deleted successfully!";
            return RedirectToAction("Index");
        }
    }
}