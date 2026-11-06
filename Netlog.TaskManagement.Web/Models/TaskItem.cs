namespace Netlog.TaskManagement.Web.Models
{
    public class TaskItem
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public int Difficulty { get; set; }

        public TaskStatus Status { get; set; }= TaskStatus.Pending;
        public int? DeveloperId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? AssignedAt { get; set; }
    }
}
