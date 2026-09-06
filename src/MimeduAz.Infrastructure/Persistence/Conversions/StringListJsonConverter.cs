using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MimeduAz.Infrastructure.Persistence.Conversions;

/// <summary>
/// <see cref="List{String}"/> sahələrini (quiz variantları, blog paraqrafları) MySQL-də
/// doğma massiv tipi olmadığı üçün JSON mətn sütununda (<c>json</c> tipi) saxlayır.
/// </summary>
public static class StringListJsonConverter
{
    public static readonly ValueConverter<List<string>, string> Converter = new(
        list => JsonSerializer.Serialize(list, (JsonSerializerOptions?)null),
        json => JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? new List<string>());

    /// <summary>
    /// EF Core dəyər müqayisəsi üçün: siyahının məzmununa görə bərabərlik yoxlanılsın,
    /// referensə görə yox - əks halda dəyişiklik izlənməsi düzgün işləməz.
    /// </summary>
    public static readonly ValueComparer<List<string>> Comparer = new(
        (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
        list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
        list => list.ToList());
}
