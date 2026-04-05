using System.Collections.Concurrent;
using TaskApi.Models;

namespace TaskApi.Services;

public class TaskStore : ITaskStore
{
    private readonly ConcurrentDictionary<Guid, TaskItem> _tasks = new();

    public IReadOnlyCollection<TaskItem> GetAll() => _tasks.Values.OrderBy(t => t.CreatedAtUtc).ToList();

    public TaskItem? GetById(Guid id) => _tasks.GetValueOrDefault(id);

    public TaskItem Create(string title)
    {
        var item = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            IsCompleted = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        _tasks[item.Id] = item;
        return item;
    }

    public TaskItem? Update(Guid id, string title, bool isCompleted)
    {
        if (!_tasks.TryGetValue(id, out var existing))
            return null;

        existing.Title = title;
        existing.IsCompleted = isCompleted;
        return existing;
    }

    public bool Delete(Guid id) => _tasks.TryRemove(id, out _);
}
