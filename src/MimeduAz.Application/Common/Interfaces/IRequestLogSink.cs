using MimeduAz.Domain.Entities;

namespace MimeduAz.Application.Common.Interfaces;

/// <summary>
/// Audit qeydlərini yaddaşdakı növbəyə atır. Bazaya yazma arxa planda,
/// batch şəklində baş verir - beləliklə loglama sorğunun cavab müddətinə təsir etmir.
/// </summary>
public interface IRequestLogSink
{
    /// <summary>
    /// Qeydi növbəyə əlavə edir. Növbə doludursa qeyd atılır və <c>false</c> qaytarılır -
    /// loglama heç vaxt sorğunu bloklamamalı və ya xəta verməməlidir.
    /// </summary>
    bool TryEnqueue(RequestLog log);
}
