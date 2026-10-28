using MimeduAz.Contracts.Ksq;
using MimeduAz.Domain.Entities;

namespace MimeduAz.Application.Common.Mappings;

public static class KsqMappings
{
    public static KsqTestDto ToDto(this KsqTest test) => new(
        test.Id,
        test.Name,
        test.ClosedPoints,
        test.OpenPoints,
        test.KeyA,
        test.KeyB,
        test.CreatedAt,
        test.UpdatedAt);

    public static KsqResultDto ToDto(this KsqResult result) => new(
        result.Id,
        result.TestName,
        result.TestId,
        result.Sinif,
        result.Ad,
        result.Variant,
        result.Kod,
        result.Answers,
        result.OpenScores,
        result.Score,
        result.MaxScore,
        result.WrongQuestions,
        result.CreatedAt);
}
