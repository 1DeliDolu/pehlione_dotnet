namespace Pehlione.Models;

public sealed class TodoItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsComplete { get; set; }
}
