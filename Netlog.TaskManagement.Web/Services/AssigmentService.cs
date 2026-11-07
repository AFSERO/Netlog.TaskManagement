using Netlog.TaskManagement.Web.Models;
using Netlog.TaskManagement.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Netlog.TaskManagement.Web.Services;

public class AssignmentService
{
    private readonly AppDbContext _context;

    public AssignmentService(AppDbContext context)
    {
        _context = context;
    }



    public void AssignTask(int taskId)
    {
        var taskItem = _context.Tasks.Find(taskId);

        if (taskItem == null || taskItem.Status != TaskItemStatus.Pending)
        {
            return;
        }

        var developers = _context.Developers
            .Include(d => d.Tasks)
            .ToList();

        Developer? selectedDeveloper = null;

        // En uygun developerı bul
        foreach (var developer in developers)
        {
            if (!CanAssignTask(developer, taskItem.Difficulty))
                continue;

            if (selectedDeveloper == null)
            {
                selectedDeveloper = developer;
                continue;
            }

            int activeLoad = CalculateActiveWorkload(developer);
            int selectedActiveLoad = CalculateActiveWorkload(selectedDeveloper);

            if (activeLoad < selectedActiveLoad)
            {
                selectedDeveloper = developer;
            }
            else if (activeLoad == selectedActiveLoad)
            {
                int completedLoad = CalculateCompletedWorkload(developer);
                int selectedCompletedLoad = CalculateCompletedWorkload(selectedDeveloper);

                if (completedLoad < selectedCompletedLoad ||
                    (completedLoad == selectedCompletedLoad &&
                     developer.Id < selectedDeveloper.Id))
                {
                    selectedDeveloper = developer;
                }
            }
        }

        // Uygun developer yoksa görev Pending kalır
        if (selectedDeveloper == null)
        {
            return;
        }

        // Görevi seçilen developera ata
        taskItem.DeveloperId = selectedDeveloper.Id;
        taskItem.Status = TaskItemStatus.Assigned;
        taskItem.AssignedAt = DateTime.UtcNow;

        _context.SaveChanges();
    }



    private int CalculateActiveWorkload(Developer developer)
    {
        int total = 0;

        foreach (var task in developer.Tasks)
        {
            if (task.Status == TaskItemStatus.Assigned ||
                task.Status == TaskItemStatus.InProgress)
            {
                total = total + task.Difficulty;
            }
        }

        return total;
    }


    private int CalculateCompletedWorkload(Developer developer)
    {
        int total = 0;

        foreach (var task in developer.Tasks)
        {
            if (task.Status == TaskItemStatus.Completed)
            {
                total += task.Difficulty;
            }
        }

        return total;
    }

    // Son atanan görevin zorluğuna göre,
    // yeni görevin ardışık zorluk kuralına uyup uymadığını kontrol eder.
    private bool CanAssignTask(Developer developer, int difficulty)
    {
        TaskItem? lastTask = null;

        foreach (var task in developer.Tasks)
        {
            if (task.AssignedAt == null)
                continue;

            if (lastTask == null || task.AssignedAt > lastTask.AssignedAt)
            {
                lastTask = task;
            }
        }

        if (lastTask == null)
            return true;

        int difference = Math.Abs(lastTask.Difficulty - difficulty);

        return difference != 1;
    }


}


