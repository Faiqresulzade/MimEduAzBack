namespace MimeduAz.Domain.Entities;

/// <summary>Təlim proqramının bir maddəsi.</summary>
public class TrainingSyllabusItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TrainingId { get; set; }
    public Training? Training { get; set; }

    public int OrderIndex { get; set; }
    public string Text { get; set; } = string.Empty;
}
