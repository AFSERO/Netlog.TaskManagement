
using Microsoft.AspNetCore.Mvc;
using Netlog.TaskManagement.Web.Data;
using Netlog.TaskManagement.Web.Services;
using Netlog.TaskManagement.Web.Models;





namespace Netlog.TaskManagement.Web.Controllers;

public class TasksController : Controller
{
    private readonly AppDbContext _context;
    private readonly AssignmentService _assignmentService;

    public TasksController(
        AppDbContext context,
        AssignmentService assignmentService)
    {
        _context = context;
        _assignmentService = assignmentService;
    }

    // Görevleri listeler.
    public IActionResult Index()
    {
        var tasks = _context.Tasks.ToList();

        ViewBag.DeveloperCount = _context.Developers.Count();

        return View(tasks);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(string title, int difficulty)
    {
        if (string.IsNullOrWhiteSpace(title) ||
            difficulty < 1 || difficulty > 8)
        {
            ViewBag.Error = "Geçersiz görev adı veya zorluk seviyesi!";
            return View();
        }

        // Yeni görevi oluştur ve kaydet.
        var taskItem = new TaskItem
        {
            Title = title.Trim(),
            Difficulty = difficulty
        };

        _context.Tasks.Add(taskItem);
        _context.SaveChanges();

        // Otomatik görev atamasını çalıştır.
        _assignmentService.AssignTask(taskItem.Id);

        return RedirectToAction("Index");
    }

}
