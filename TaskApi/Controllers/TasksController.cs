using Microsoft.AspNetCore.Mvc;
using TaskApi.Models;
using TaskApi.Services;

namespace TaskApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITaskStore _store;

    public TasksController(ITaskStore store)
    {
        _store = store;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TaskItem>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<TaskItem>> GetAll()
    {
        return Ok(_store.GetAll());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TaskItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<TaskItem> GetById(Guid id)
    {
        var task = _store.GetById(id);
        return task is null ? NotFound() : Ok(task);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TaskItem), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<TaskItem> Create([FromBody] CreateTaskRequest request)
    {
        var task = _store.Create(request.Title);
        return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TaskItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<TaskItem> Update(Guid id, [FromBody] UpdateTaskRequest request)
    {
        var task = _store.Update(id, request.Title, request.IsCompleted);
        return task is null ? NotFound() : Ok(task);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(Guid id)
    {
        return _store.Delete(id) ? NoContent() : NotFound();
    }
}
