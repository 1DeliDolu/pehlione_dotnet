using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models;

namespace Pehlione.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TodoItemsController : ControllerBase
{
    private readonly PehlioneDbContext _db;

    public TodoItemsController(PehlioneDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TodoItem>>> GetTodoItems(CancellationToken ct)
    {
        return await _db.TodoItems.AsNoTracking().ToListAsync(ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TodoItem>> GetTodoItem(int id, CancellationToken ct)
    {
        var todoItem = await _db.TodoItems.FindAsync([id], ct);
        if (todoItem is null)
        {
            return NotFound();
        }

        return todoItem;
    }

    [HttpPost]
    public async Task<ActionResult<TodoItem>> PostTodoItem(TodoItem todoItem, CancellationToken ct)
    {
        _db.TodoItems.Add(todoItem);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetTodoItem), new { id = todoItem.Id }, todoItem);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> PutTodoItem(int id, TodoItem todoItem, CancellationToken ct)
    {
        if (id != todoItem.Id)
        {
            return BadRequest();
        }

        _db.Entry(todoItem).State = EntityState.Modified;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            var exists = await _db.TodoItems.AnyAsync(x => x.Id == id, ct);
            if (!exists)
            {
                return NotFound();
            }

            throw;
        }

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTodoItem(int id, CancellationToken ct)
    {
        var todoItem = await _db.TodoItems.FindAsync([id], ct);
        if (todoItem is null)
        {
            return NotFound();
        }

        _db.TodoItems.Remove(todoItem);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}
