using System.ComponentModel.DataAnnotations;

namespace TaskApi.Models;

public class CreateTaskRequest
{
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;
}
