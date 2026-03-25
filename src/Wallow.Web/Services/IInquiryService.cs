using Wallow.Web.Models;

namespace Wallow.Web.Services;

public interface IInquiryService
{
    Task<bool> SubmitInquiryAsync(InquiryModel model, CancellationToken ct = default);
}
