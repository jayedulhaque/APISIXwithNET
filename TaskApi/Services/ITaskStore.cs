using TaskApi.Models;

namespace TaskApi.Services;

public interface ITaskStore
{
    IReadOnlyCollection<TaskItem> GetAll();
    TaskItem? GetById(Guid id);
    TaskItem Create(string title);
    TaskItem? Update(Guid id, string title, bool isCompleted);
    bool Delete(Guid id);
}
