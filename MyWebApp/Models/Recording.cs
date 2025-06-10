using System;
namespace MyWebApp.Models
{
    public class Recording
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime Created { get; set; }
    }
}
