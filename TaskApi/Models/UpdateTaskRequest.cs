using System.ComponentModel.DataAnnotations;

namespace TaskApi.Models;

public class UpdateTaskRequest
{
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }
}
