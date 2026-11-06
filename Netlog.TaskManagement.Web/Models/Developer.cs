namespace Netlog.TaskManagement.Web.Models
{
    public class Developer
    {
        public int Id { get; set; }

        public string Name { get; set; }  = string.Empty;

        public List<Task> Tasks { get; set; } = new();
    }
}
