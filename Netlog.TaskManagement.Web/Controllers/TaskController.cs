
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Netlog.TaskManagement.Web.Data;
using Netlog.TaskManagement.Web.Models;
using Netlog.TaskManagement.Web.Services;





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
        var tasks = _context.Tasks
            .OrderByDescending(task => task.Id)
            .ToList();

        var developers = _context.Developers
            .Include(d => d.Tasks)
            .ToList();

        ViewBag.Developers = developers;

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





    [HttpGet]
    public IActionResult Edit(int id)
    {
        var taskItem = _context.Tasks.Find(id);

        if (taskItem == null)
        {
            return NotFound();
        }

        return View(taskItem);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, string title, int difficulty)
    {
        var taskItem = _context.Tasks.Find(id);

        if (taskItem == null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            ViewBag.Error = "Görev adı boş olamaz!";
            return View(taskItem);
        }

        // Görev adını güncelle
        taskItem.Title = title;

        // Zorluk yalnızca Pending durumunda değiştirilebilir
        if (taskItem.Status == TaskItemStatus.Pending)
        {
            if (difficulty < 1 || difficulty > 8)
            {
                ViewBag.Error = "Zorluk 1 ile 8 arasında olmalı!";
                return View(taskItem);
            }

            taskItem.Difficulty = difficulty;
        }

        _context.SaveChanges();

        return RedirectToAction("Index");
    }




    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var taskItem = _context.Tasks.Find(id);

        if (taskItem == null)
        {
            return NotFound();
        }

        // Tamamlanan görevler silinemez
        if (taskItem.Status == TaskItemStatus.Completed)
        {
            TempData["Error"] = "Tamamlanan görev silinemez!";
            return RedirectToAction("Index");
        }

        _context.Tasks.Remove(taskItem);
        _context.SaveChanges();

        return RedirectToAction("Index");
    }



    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateStatus(int id, TaskItemStatus newStatus)
    {
        var taskItem = _context.Tasks.Find(id);

        if (taskItem == null)
        {
            return NotFound();
        }

        // Pending yapılırsa developer atamasını kaldır
        if (newStatus == TaskItemStatus.Pending)
        {
            taskItem.DeveloperId = null;
            taskItem.AssignedAt = null;
        }
        else if (taskItem.DeveloperId == null)
        {
            // Developer yoksa önce otomatik atama yap
            _assignmentService.AssignTask(id);

            if (taskItem.DeveloperId == null)
            {
                return RedirectToAction("Index");
            }
        }

        taskItem.Status = newStatus;
        _context.SaveChanges();

        return RedirectToAction("Index");
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RetryAssignment(int id)
    {
        var taskItem = _context.Tasks.Find(id);

        if (taskItem == null)
        {
            return NotFound();
        }

        // Sadece Pending görevler yeniden atanabilir
        if (taskItem.Status == TaskItemStatus.Pending)
        {
            _assignmentService.AssignTask(id);
        }

        return RedirectToAction("Index");
    }




}
